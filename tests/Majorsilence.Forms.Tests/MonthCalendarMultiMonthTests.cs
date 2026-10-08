using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // SMP-46's remainder: CalendarDimensions greater than 1x1. Before, one month was painted across the
    // whole client area whatever the dimensions said, while GetDisplayRange and the scroll step already
    // counted several months -- so the control reported months it never drew, and none of their days
    // could be clicked. The months now tile the client area, one block each, as upstream's do.
    //
    // Relational assertions only: blocks abut, a date's cell is inside its month's block, a click on
    // that cell is that date. No pixel rectangles are hard-coded.
    [Collection ("Headless")]
    public class MonthCalendarMultiMonthTests
    {
        // 1 March 2026 is a Sunday and March has 31 days, so a Sunday-first March grid ends with eleven
        // April days -- days the second month on screen draws instead.
        private static readonly DateTime March = new DateTime (2026, 3, 14);
        private static readonly DateTime April = new DateTime (2026, 4, 1);

        private sealed class TestCalendar : MonthCalendar
        {
            internal void ClickAt (Point p)
            {
                OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, 0));
                OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, 0));
            }
        }

        private static TestCalendar Calendar (int columns, int rows)
        {
            HeadlessRenderer.Use ();

            var calendar = new TestCalendar { Width = 220 * columns, Height = 162 * rows };
            calendar.SetDate (March);
            calendar.SetCalendarDimensions (columns, rows);

            return calendar;
        }

        private static Point Centre (MonthCalendar calendar, Rectangle device)
            => new Point (calendar.DeviceToLogicalUnits (device.Left + (device.Width / 2)),
                          calendar.DeviceToLogicalUnits (device.Top + (device.Height / 2)));

        private static SKBitmap Render (MonthCalendar calendar)
        {
            var bitmap = PaintSurface.RenderOnForm (calendar);

            // A 0x0 bitmap would make every ink assertion below vacuous.
            Assert.Equal (calendar.DeviceClientRectangle.Width, bitmap.Width);
            Assert.True (bitmap.Height > 0);

            return bitmap;
        }

        private static SKColor Background (SKBitmap bitmap)
        {
            var counts = new Dictionary<SKColor, int> ();

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++) {
                    var colour = bitmap.GetPixel (x, y);
                    counts[colour] = counts.TryGetValue (colour, out var n) ? n + 1 : 1;
                }

            return counts.OrderByDescending (p => p.Value).First ().Key;
        }

        private static int Ink (SKBitmap bitmap, Rectangle area, SKColor background)
        {
            var count = 0;

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                    if (bitmap.GetPixel (x, y) != background)
                        count++;

            return count;
        }

        [Fact]
        public void Two_months_across_get_a_block_each_and_each_date_is_in_its_own_months_block ()
        {
            using var calendar = Calendar (2, 1);

            var first = calendar.GetMonthGeometry (0);
            var second = calendar.GetMonthGeometry (1);

            Assert.Equal (new DateTime (2026, 3, 1), first.Month);
            Assert.Equal (April, second.Month);
            Assert.Equal (first.Block.Right, second.Block.Left);
            Assert.Equal (first.Block.Top, second.Block.Top);
            Assert.True (first.Grid.Right <= second.Block.Left, "the first month's grid stops at its block");

            Assert.True (first.Grid.Contains (calendar.GetDateCellBounds (new DateTime (2026, 3, 20))));
            Assert.True (second.Grid.Contains (calendar.GetDateCellBounds (new DateTime (2026, 4, 15))));

            // An April day the March grid would pad with is drawn by April, not twice.
            Assert.True (second.Grid.Contains (calendar.GetDateCellBounds (new DateTime (2026, 4, 2))));
        }

        [Fact]
        public void The_second_month_is_drawn_and_the_padding_days_between_months_are_not ()
        {
            using var calendar = Calendar (2, 1);
            using var bitmap = Render (calendar);
            var background = Background (bitmap);

            var first = calendar.GetMonthGeometry (0);
            var second = calendar.GetMonthGeometry (1);

            Assert.True (Ink (bitmap, second.Title, background) > 0, "no title over the second month");

            for (var day = 1; day <= 30; day++) {
                var cell = calendar.GetDateCellBounds (new DateTime (2026, 4, day));
                Assert.True (Ink (bitmap, cell, background) > 0, $"April {day} is not drawn");
            }

            // The last cell of the March grid (11 April) is left blank: April's own block draws it.
            var march_last = MonthCalendar.GetCellBounds (first, 5, 6);
            Assert.Equal (0, Ink (bitmap, march_last, background));

            // ...but in a single-month calendar that same cell carries the trailing day.
            using var single = Calendar (1, 1);
            using var single_bitmap = Render (single);
            Assert.True (Ink (single_bitmap, single.GetCellBounds (5, 6), Background (single_bitmap)) > 0);
        }

        [Fact]
        public void A_click_on_the_second_month_selects_that_date ()
        {
            using var calendar = Calendar (2, 1);
            var target = new DateTime (2026, 4, 15);
            var point = Centre (calendar, calendar.GetDateCellBounds (target));

            var hit = calendar.HitTest (point);
            Assert.Equal (MonthCalendar.HitArea.Date, hit.HitArea);
            Assert.Equal (target, hit.Time);

            calendar.ClickAt (point);
            Assert.Equal (target, calendar.SelectionStart);

            // The blank padding cell between the two months is not a date.
            var blank = calendar.HitTest (Centre (calendar, MonthCalendar.GetCellBounds (calendar.GetMonthGeometry (0), 5, 6)));
            Assert.Equal (MonthCalendar.HitArea.CalendarBackground, blank.HitArea);
        }

        [Fact]
        public void The_scroll_arrows_sit_at_the_two_ends_of_the_top_row_and_page_by_a_screen ()
        {
            using var calendar = Calendar (2, 1);

            var first = calendar.GetMonthGeometry (0);
            var second = calendar.GetMonthGeometry (1);

            Assert.True (first.Block.Contains (calendar.Geometry.PrevButton));
            Assert.True (second.Block.Contains (calendar.Geometry.NextButton));
            Assert.True (first.NextButton.IsEmpty && second.PrevButton.IsEmpty);

            // The second title is a caption, not an arrow, at its left end.
            var left_of_second = new Rectangle (second.Title.Left, second.Title.Top, 2, second.Title.Height);
            Assert.NotEqual (MonthCalendar.HitArea.PrevMonthButton, calendar.HitTest (Centre (calendar, left_of_second)).HitArea);

            calendar.ClickAt (Centre (calendar, calendar.Geometry.NextButton));

            // ScrollChange 0 is one screenful: two months.
            Assert.Equal (new DateTime (2026, 5, 1), calendar.GetMonthGeometry (0).Month);
        }

        [Fact]
        public void Rows_of_months_stack_and_the_today_strip_runs_under_them_all ()
        {
            using var calendar = Calendar (2, 2);

            var top_left = calendar.GetMonthGeometry (0);
            var bottom_left = calendar.GetMonthGeometry (2);
            var bottom_right = calendar.GetMonthGeometry (3);

            Assert.Equal (top_left.Block.Bottom, bottom_left.Block.Top);
            Assert.Equal (top_left.Block.Left, bottom_left.Block.Left);
            Assert.Equal (new DateTime (2026, 5, 1), bottom_left.Month);
            Assert.True (bottom_left.Title.Height > 0 && bottom_left.Title.Top == bottom_left.Block.Top);

            Assert.Equal (bottom_left.Block.Bottom, calendar.Geometry.TodayBand.Top);
            Assert.Equal (calendar.DeviceClientRectangle.Width, calendar.Geometry.TodayBand.Width);

            // Only the ends of the run of months show the neighbouring months' days.
            Assert.True (top_left.ShowsLeadingDays && !top_left.ShowsTrailingDays);
            Assert.True (bottom_right.ShowsTrailingDays && !bottom_right.ShowsLeadingDays);
            Assert.False (bottom_left.ShowsLeadingDays || bottom_left.ShowsTrailingDays);
        }

        [Fact]
        public void One_month_lays_out_across_the_whole_control_as_before ()
        {
            // A guard, not a regression test: the 1x1 layout was already this; it pins that tiling did
            // not move the default control.
            using var calendar = Calendar (1, 1);
            var geometry = calendar.Geometry;
            var client = calendar.DeviceClientRectangle;

            Assert.Equal (client.Width, geometry.Title.Width);
            Assert.Equal (client.Right, geometry.NextButton.Right);
            Assert.True (geometry.ShowsLeadingDays && geometry.ShowsTrailingDays);
        }

        [Fact]
        public void The_CalendarDimensions_setter_validates_and_caps_as_SetCalendarDimensions_does ()
        {
            using var calendar = new MonthCalendar ();

            calendar.CalendarDimensions = new Size (6, 6);
            Assert.True (calendar.CalendarDimensions.Width * calendar.CalendarDimensions.Height <= 12);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.CalendarDimensions = new Size (0, 2));
        }
    }
}
