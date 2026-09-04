using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.20c (findings SMP-42 P0, SMP-43, SMP-46): MonthCalendar drew no calendar and could not be
    // clicked.
    //
    // OnPaint drew one centred ToShortDateString() in an empty 220x162 box, RenderManager had no
    // entry for the type, there was no OnMouseDown/OnMouseMove/OnMouseUp/OnKeyDown anywhere on the
    // class, HitTest returned SelectionStart for every point in the body, and DateSelected was
    // declared `add { } remove { }` so subscriptions were discarded. Nothing here could pass before
    // the slice.
    //
    // The assertions are deliberately relational rather than positional: "the 1st sits in the column
    // its weekday implies", "the cell whose centre we computed is the date that gets selected", "week
    // numbers move the day columns one cell right". No hard-coded pixel rectangles, so the tests
    // survive a change of metrics and still catch a change of behaviour.
    [Collection ("Headless")]
    public class MonthCalendarBehaviourTests
    {
        // A month that exercises the padding: 1 March 2026 is a Sunday and the month has 31 days, so a
        // Sunday-first grid fills row 0 exactly and needs trailing days at the end, while a
        // Monday-first grid needs six leading days.
        private static readonly DateTime March = new DateTime (2026, 3, 14);

        private static TestCalendar Calendar (Action<TestCalendar>? configure = null)
        {
            HeadlessRenderer.Use ();

            var calendar = new TestCalendar { Width = 220, Height = 162 };
            calendar.SetDate (March);
            configure?.Invoke (calendar);

            return calendar;
        }

        // OnMouseDown/OnMouseUp/OnKeyDown are the entry points the control's own gestures arrive on
        // (Control.RaiseMouseDown routes to them after hit-testing the control tree), and they are
        // protected, so the tests drive them through a subclass rather than through a window.
        private sealed class TestCalendar : MonthCalendar
        {
            internal void Press (Point p) => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, 0));

            internal void Drag (Point p) => OnMouseMove (new MouseEventArgs (MouseButtons.Left, 0, p.X, p.Y, 0));

            internal void Release (Point p) => OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, 0));

            internal void ClickAt (Point p)
            {
                Press (p);
                Release (p);
            }

            internal KeyEventArgs Key (Keys keys)
            {
                var e = new KeyEventArgs (keys);

                OnKeyDown (e);

                return e;
            }
        }

        // Cell bounds are device pixels (they come from ClientRectangle); a MouseEventArgs carries
        // logical units. Converting here is what makes every click test hold at MF_HEADLESS_SCALE=2 as
        // well as at 1.
        private static Point CentreOf (MonthCalendar calendar, DateTime date)
        {
            var cell = calendar.GetDateCellBounds (date);

            Assert.False (cell.IsEmpty, $"{date:d} is not on the displayed grid");

            return new Point (calendar.DeviceToLogicalUnits (cell.Left + (cell.Width / 2)),
                              calendar.DeviceToLogicalUnits (cell.Top + (cell.Height / 2)));
        }

        private static Point CentreOf (MonthCalendar calendar, Rectangle deviceBounds)
            => new Point (calendar.DeviceToLogicalUnits (deviceBounds.Left + (deviceBounds.Width / 2)),
                          calendar.DeviceToLogicalUnits (deviceBounds.Top + (deviceBounds.Height / 2)));

        // ---------------- rendering: the grid exists at all

        // The bitmap's size is asserted against the control's own client rectangle, and asserted to be
        // non-empty, because a pixel test here can pass by measuring nothing two ways: PaintSurface
        // sizes the bitmap from Control.Scaling, which is 0 for an unhosted control (a 0x0 bitmap
        // makes every ink assertion below vacuously true), and Control.Visible is ambient, so a
        // parentless control reports false and the paint pass skips it. RenderOnForm fixes the second;
        // letting it resolve the scaling itself -- rather than pinning 1f -- keeps the bitmap the same
        // size as the device-pixel geometry the assertions index into, which is what makes them hold
        // under MF_HEADLESS_SCALE=2 as well as at 1.
        private static SKBitmap Render (MonthCalendar calendar)
        {
            var bitmap = PaintSurface.RenderOnForm (calendar);

            Assert.True (bitmap.Width > 0 && bitmap.Height > 0, "nothing was measured");
            Assert.Equal (calendar.ClientRectangle.Width, bitmap.Width);
            Assert.Equal (calendar.ClientRectangle.Height, bitmap.Height);

            return bitmap;
        }

        // The most common colour in the bitmap: whatever the theme paints the empty calendar body
        // with. Derived rather than hard-coded so the tests do not depend on which theme is active.
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

        // The most common colour inside one region: a cell's fill, as opposed to the glyph on it.
        private static SKColor Fill (SKBitmap bitmap, Rectangle area)
        {
            var counts = new Dictionary<SKColor, int> ();

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    var colour = bitmap.GetPixel (x, y);
                    counts[colour] = counts.TryGetValue (colour, out var n) ? n + 1 : 1;
                }

            return counts.OrderByDescending (p => p.Value).First ().Key;
        }

        [Fact]
        public void Every_day_cell_in_the_grid_is_drawn ()
        {
            using var calendar = Calendar ();
            using var bitmap = Render (calendar);
            var background = Background (bitmap);

            // 42 cells, every one of them carrying a day number. The old implementation drew a single
            // centred string, so all but the two or three cells it crossed were empty.
            for (var week = 0; week < 6; week++)
                for (var column = 0; column < 7; column++)
                    Assert.True (Ink (bitmap, calendar.GetCellBounds (week, column), background) > 0,
                                 $"no day drawn in cell ({week},{column})");
        }

        [Fact]
        public void Seven_day_of_week_headers_are_drawn ()
        {
            using var calendar = Calendar ();
            using var bitmap = Render (calendar);
            var background = Background (bitmap);

            for (var column = 0; column < 7; column++)
                Assert.True (Ink (bitmap, calendar.GetDayHeaderBounds (column), background) > 0,
                             $"no day-of-week header drawn in column {column}");
        }

        [Fact]
        public void The_title_names_the_displayed_month ()
        {
            using var calendar = Calendar ();
            using var bitmap = Render (calendar);
            var background = Background (bitmap);

            Assert.True (Ink (bitmap, calendar.Geometry.Title, background) > 0);
        }

        [Theory]
        [InlineData (Day.Default)]      // Sunday
        [InlineData (Day.Monday)]
        [InlineData (Day.Wednesday)]
        [InlineData (Day.Saturday)]
        public void The_first_of_the_month_sits_in_the_column_its_weekday_implies (Day firstDayOfWeek)
        {
            // April, not the March fixture: 1 March 2026 is a Sunday, so under the default
            // (Sunday-first) week it lands in column 0 -- the answer any broken column calculation
            // also gives. 1 April 2026 is a Wednesday, so every case here is a non-zero column.
            using var calendar = Calendar (c => {
                c.FirstDayOfWeek = firstDayOfWeek;
                c.SetDate (new DateTime (2026, 4, 14));
            });

            var first = new DateTime (2026, 4, 1);
            var cell = calendar.GetDateCellBounds (first);
            var geometry = calendar.Geometry;
            var column = (cell.Left - geometry.Grid.Left) / geometry.CellWidth;

            var expected = (((int)first.DayOfWeek - (int)calendar.FirstDayOfWeekAsDayOfWeek) + 7) % 7;

            Assert.Equal (expected, column);
            Assert.Equal (0, (cell.Top - geometry.Grid.Top) / geometry.CellHeight);   // and in the first week row
        }

        [Fact]
        public void The_grid_and_the_padded_display_range_describe_the_same_days ()
        {
            // Monday-first, so the padding is six real days of February rather than none: with the
            // Sunday-first default the March fixture needs no leading days at all and the equality
            // below would hold even for a grid that never padded.
            using var calendar = Calendar (c => c.FirstDayOfWeek = Day.Monday);

            var padded = calendar.GetDisplayRange (visible: false);

            Assert.Equal (padded.Start, calendar.GetDateAt (0, 0));

            // Every day the control says it is showing has a cell to be drawn in.
            for (var date = padded.Start; date <= padded.End; date = date.AddDays (1))
                Assert.False (calendar.GetDateCellBounds (date).IsEmpty, $"{date:d} has no cell");
        }

        [Fact]
        public void ShowWeekNumbers_adds_a_leading_column_and_narrows_the_day_columns ()
        {
            using var plain = Calendar ();
            using var numbered = Calendar (c => c.ShowWeekNumbers = true);

            Assert.True (plain.GetWeekNumberBounds (0).IsEmpty);
            Assert.False (numbered.GetWeekNumberBounds (0).IsEmpty);

            // Eight columns instead of seven: the day grid starts one cell in and each cell is narrower.
            Assert.True (numbered.Geometry.Grid.Left > plain.Geometry.Grid.Left);
            Assert.True (numbered.Geometry.CellWidth < plain.Geometry.CellWidth);
            Assert.Equal (numbered.Geometry.CellWidth, numbered.Geometry.Grid.Left - numbered.Geometry.WeekNumberColumn.Left);

            using var bitmap = Render (numbered);

            Assert.True (Ink (bitmap, numbered.GetWeekNumberBounds (0), Background (bitmap)) > 0,
                         "the week-number column is laid out but nothing is drawn in it");
        }

        [Fact]
        public void ShowToday_puts_a_strip_at_the_foot_and_ShowToday_false_takes_it_away ()
        {
            using var with = Calendar ();
            using var without = Calendar (c => c.ShowToday = false);

            Assert.True (with.ShowToday);
            Assert.False (with.Geometry.TodayBand.IsEmpty);
            Assert.True (without.Geometry.TodayBand.IsEmpty);

            using var bitmap = Render (with);

            Assert.True (Ink (bitmap, with.Geometry.TodayBand, Background (bitmap)) > 0);
        }

        [Fact]
        public void The_selected_day_is_filled_differently_from_an_unselected_one ()
        {
            using var calendar = Calendar (c => c.SetDate (new DateTime (2026, 3, 10)));
            using var bitmap = Render (calendar);

            var selected = Fill (bitmap, calendar.GetDateCellBounds (new DateTime (2026, 3, 10)));
            var neighbour = Fill (bitmap, calendar.GetDateCellBounds (new DateTime (2026, 3, 17)));

            Assert.NotEqual (neighbour, selected);
        }

        [Fact]
        public void A_day_outside_the_displayed_month_is_drawn_in_the_trailing_colour ()
        {
            // TrailingForeColor was stored and read by nothing (SMP-46). Setting it to something
            // nothing else in the control uses proves the renderer consults it.
            using var calendar = Calendar (c => c.TrailingForeColor = Color.FromArgb (255, 255, 0, 255));
            using var bitmap = Render (calendar);

            var trailing = calendar.GetDateAt (5, 6);   // last cell: always a day of the next month here

            Assert.NotEqual (March.Month, trailing.Month);

            var magenta = new SKColor (255, 0, 255);
            var cell = calendar.GetDateCellBounds (trailing);
            var found = false;

            for (var x = cell.Left; x < cell.Right && !found; x++)
                for (var y = cell.Top; y < cell.Bottom && !found; y++)
                    found = bitmap.GetPixel (x, y) == magenta;

            Assert.True (found, "TrailingForeColor is not used for adjacent-month days");
        }

        // ---------------- mouse: selection

        // None of these is the 14th: the fixture already has that day selected, so clicking it would
        // assert a state the click never had to produce.
        [Theory]
        [InlineData (1)]
        [InlineData (20)]
        [InlineData (31)]
        public void Clicking_a_day_cell_selects_that_date (int day)
        {
            using var calendar = Calendar ();
            var date = new DateTime (March.Year, March.Month, day);

            calendar.ClickAt (CentreOf (calendar, date));

            Assert.Equal (date, calendar.SelectionStart);
            Assert.Equal (date, calendar.SelectionEnd);
        }

        [Fact]
        public void Clicking_a_day_raises_DateChanged_then_DateSelected ()
        {
            using var calendar = Calendar ();
            var order = new List<string> ();
            calendar.DateChanged += (_, _) => order.Add ("changed");
            calendar.DateSelected += (_, _) => order.Add ("selected");

            calendar.ClickAt (CentreOf (calendar, new DateTime (2026, 3, 20)));

            Assert.Equal (new[] { "changed", "selected" }, order);
        }

        [Fact]
        public void DateSelected_keeps_the_handlers_it_is_given ()
        {
            // The event was `add { } remove { }`: both handlers below were accepted and dropped, so
            // nothing ran. Adding two and removing one proves a real backing field rather than a
            // single stored delegate.
            using var calendar = Calendar ();
            var kept = 0;
            var removed = 0;
            DateRangeEventHandler keeper = (_, _) => kept++;
            DateRangeEventHandler goner = (_, _) => removed++;

            calendar.DateSelected += keeper;
            calendar.DateSelected += goner;
            calendar.DateSelected -= goner;

            calendar.ClickAt (CentreOf (calendar, new DateTime (2026, 3, 5)));

            Assert.Equal (1, kept);
            Assert.Equal (0, removed);
        }

        [Fact]
        public void Dragging_across_days_extends_the_selection_and_commits_once ()
        {
            using var calendar = Calendar ();
            var changed = 0;
            var selected = 0;
            calendar.DateChanged += (_, _) => changed++;
            calendar.DateSelected += (_, _) => selected++;

            calendar.Press (CentreOf (calendar, new DateTime (2026, 3, 10)));
            calendar.Drag (CentreOf (calendar, new DateTime (2026, 3, 11)));
            calendar.Drag (CentreOf (calendar, new DateTime (2026, 3, 12)));
            calendar.Release (CentreOf (calendar, new DateTime (2026, 3, 12)));

            Assert.Equal (new DateTime (2026, 3, 10), calendar.SelectionStart);
            Assert.Equal (new DateTime (2026, 3, 12), calendar.SelectionEnd);
            Assert.Equal (3, changed);      // per day crossed
            Assert.Equal (1, selected);     // once, on release
        }

        [Fact]
        public void Dragging_backwards_moves_the_start_and_leaves_the_anchor_as_the_end ()
        {
            using var calendar = Calendar ();

            calendar.Press (CentreOf (calendar, new DateTime (2026, 3, 20)));
            calendar.Drag (CentreOf (calendar, new DateTime (2026, 3, 17)));
            calendar.Release (CentreOf (calendar, new DateTime (2026, 3, 17)));

            Assert.Equal (new DateTime (2026, 3, 17), calendar.SelectionStart);
            Assert.Equal (new DateTime (2026, 3, 20), calendar.SelectionEnd);
        }

        [Fact]
        public void A_drag_is_trimmed_by_MaxSelectionCount_without_moving_the_anchor ()
        {
            using var calendar = Calendar (c => c.MaxSelectionCount = 3);

            calendar.Press (CentreOf (calendar, new DateTime (2026, 3, 10)));
            calendar.Drag (CentreOf (calendar, new DateTime (2026, 3, 20)));

            // The pressed day stays put and the dragged end is the one clamped; three days, not eleven.
            Assert.Equal (new DateTime (2026, 3, 10), calendar.SelectionStart);
            Assert.Equal (new DateTime (2026, 3, 12), calendar.SelectionEnd);
        }

        [Fact]
        public void Clicking_a_trailing_day_selects_it_and_brings_its_month_into_view ()
        {
            using var calendar = Calendar ();
            var trailing = calendar.GetDateAt (5, 6);

            Assert.NotEqual (March.Month, trailing.Month);

            calendar.ClickAt (CentreOf (calendar, trailing));

            Assert.Equal (trailing, calendar.SelectionStart);
            Assert.Equal (trailing.Month, calendar.GetDisplayRange (visible: true).Start.Month);
        }

        [Fact]
        public void Clicking_the_next_month_arrow_pages_the_view_and_leaves_the_selection_alone ()
        {
            using var calendar = Calendar ();
            var before = calendar.SelectionStart;

            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.NextButton));

            Assert.Equal (before, calendar.SelectionStart);
            Assert.Equal (new DateTime (2026, 4, 1), calendar.GetDisplayRange (visible: true).Start);
        }

        [Fact]
        public void Clicking_the_previous_month_arrow_pages_the_other_way ()
        {
            using var calendar = Calendar ();

            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.PrevButton));

            Assert.Equal (new DateTime (2026, 2, 1), calendar.GetDisplayRange (visible: true).Start);
            Assert.Equal (March.Date, calendar.SelectionStart);
        }

        [Fact]
        public void ScrollChange_sets_how_far_an_arrow_moves ()
        {
            // ScrollChange was stored and read by nothing (SMP-46); zero means "one screenful".
            using var calendar = Calendar (c => c.ScrollChange = 3);

            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.NextButton));

            Assert.Equal (new DateTime (2026, 6, 1), calendar.GetDisplayRange (visible: true).Start);
        }

        [Fact]
        public void Clicking_the_today_strip_selects_today ()
        {
            using var calendar = Calendar (c => c.TodayDate = new DateTime (2026, 5, 4));
            var selected = 0;
            calendar.DateSelected += (_, _) => selected++;

            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.TodayBand));

            Assert.Equal (new DateTime (2026, 5, 4), calendar.SelectionStart);
            Assert.Equal (1, selected);
        }

        [Fact]
        public void Clicking_a_week_number_selects_that_week ()
        {
            using var calendar = Calendar (c => c.ShowWeekNumbers = true);

            calendar.ClickAt (CentreOf (calendar, calendar.GetWeekNumberBounds (2)));

            Assert.Equal (calendar.GetDateAt (2, 0), calendar.SelectionStart);
            Assert.Equal (calendar.GetDateAt (2, 6), calendar.SelectionEnd);
        }

        // GUARD, not proof: no previous version could fail this. Before the slice the control had no
        // mouse handling at all, so a click on the header changed nothing then either. It is here
        // because the obvious way to write OnMouseDown -- fall through to "the point is in the body,
        // select whatever HitTest returned" -- would select a date from the header row.
        [Fact]
        public void Clicking_the_day_header_selects_nothing ()
        {
            using var calendar = Calendar ();
            var before = calendar.SelectionStart;

            calendar.ClickAt (CentreOf (calendar, calendar.GetDayHeaderBounds (3)));

            Assert.Equal (before, calendar.SelectionStart);
        }

        // ---------------- HitTest (SMP-43)

        [Fact]
        public void HitTest_reports_the_date_under_the_point_not_the_selection ()
        {
            using var calendar = Calendar ();

            foreach (var day in new[] { 2, 9, 23 }) {
                var date = new DateTime (March.Year, March.Month, day);
                var hit = calendar.HitTest (CentreOf (calendar, date));

                Assert.Equal (MonthCalendar.HitArea.Date, hit.HitArea);
                Assert.Equal (date, hit.Time);
            }
        }

        [Fact]
        public void HitTest_tells_the_adjacent_month_days_apart_from_the_displayed_ones ()
        {
            using var calendar = Calendar (c => c.FirstDayOfWeek = Day.Monday);

            // 1 March 2026 is a Sunday, so a Monday-first grid opens with six days of February.
            var leading = calendar.GetDateAt (0, 0);
            var trailing = calendar.GetDateAt (5, 6);

            Assert.Equal (MonthCalendar.HitArea.PrevMonthDate, calendar.HitTest (CentreOf (calendar, leading)).HitArea);
            Assert.Equal (MonthCalendar.HitArea.NextMonthDate, calendar.HitTest (CentreOf (calendar, trailing)).HitArea);
        }

        [Fact]
        public void HitTest_names_the_day_header_and_the_week_number_column ()
        {
            using var calendar = Calendar (c => c.ShowWeekNumbers = true);

            Assert.Equal (MonthCalendar.HitArea.DayOfWeek,
                          calendar.HitTest (CentreOf (calendar, calendar.GetDayHeaderBounds (0))).HitArea);
            Assert.Equal (MonthCalendar.HitArea.WeekNumbers,
                          calendar.HitTest (CentreOf (calendar, calendar.GetWeekNumberBounds (1))).HitArea);
        }

        // ---------------- keyboard

        [Theory]
        [InlineData (Keys.Left, -1)]
        [InlineData (Keys.Right, 1)]
        [InlineData (Keys.Up, -7)]
        [InlineData (Keys.Down, 7)]
        public void The_arrow_keys_move_the_selection_by_a_day_or_a_week (Keys key, int days)
        {
            using var calendar = Calendar ();

            calendar.Key (key);

            Assert.Equal (March.AddDays (days), calendar.SelectionStart);
            Assert.Equal (March.AddDays (days), calendar.SelectionEnd);
        }

        [Fact]
        public void PageUp_and_PageDown_move_a_month ()
        {
            using var calendar = Calendar ();

            calendar.Key (Keys.PageDown);
            Assert.Equal (new DateTime (2026, 4, 14), calendar.SelectionStart);

            calendar.Key (Keys.PageUp);
            Assert.Equal (March.Date, calendar.SelectionStart);
        }

        [Fact]
        public void Home_and_End_go_to_the_ends_of_the_month ()
        {
            using var calendar = Calendar ();

            calendar.Key (Keys.Home);
            Assert.Equal (new DateTime (2026, 3, 1), calendar.SelectionStart);

            calendar.Key (Keys.End);
            Assert.Equal (new DateTime (2026, 3, 31), calendar.SelectionStart);
        }

        [Fact]
        public void Shift_plus_an_arrow_extends_the_range_from_the_anchor ()
        {
            using var calendar = Calendar ();

            calendar.Key (Keys.Right | Keys.Shift);
            calendar.Key (Keys.Right | Keys.Shift);

            Assert.Equal (March.Date, calendar.SelectionStart);
            Assert.Equal (March.AddDays (2), calendar.SelectionEnd);
        }

        [Fact]
        public void Keyboard_navigation_raises_DateChanged_and_DateSelected ()
        {
            using var calendar = Calendar ();
            var order = new List<string> ();
            calendar.DateChanged += (_, _) => order.Add ("changed");
            calendar.DateSelected += (_, _) => order.Add ("selected");

            calendar.Key (Keys.Down);

            Assert.Equal (new[] { "changed", "selected" }, order);
        }

        [Fact]
        public void A_navigation_key_is_marked_handled_and_an_unrelated_one_is_not ()
        {
            using var calendar = Calendar ();

            // Handled is what stops the key travelling on to the form's dialog/mnemonic handling, so a
            // calendar that eats an arrow key must say so, and must not eat anything else.
            Assert.True (calendar.Key (Keys.Right).Handled);
            Assert.False (calendar.Key (Keys.A).Handled);
        }

        [Fact]
        public void Navigating_off_the_displayed_month_scrolls_the_view_to_follow ()
        {
            using var calendar = Calendar (c => c.SetDate (new DateTime (2026, 3, 31)));

            // Page forward first, so the view is pinned somewhere the selection is not: without that
            // the displayed month simply tracks SelectionStart and the assertion proves nothing.
            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.NextButton));
            Assert.Equal (new DateTime (2026, 4, 1), calendar.GetDisplayRange (visible: true).Start);

            calendar.Key (Keys.Left);

            Assert.Equal (new DateTime (2026, 3, 30), calendar.SelectionStart);
            Assert.Equal (new DateTime (2026, 3, 1), calendar.GetDisplayRange (visible: true).Start);
        }

        [Fact]
        public void Extending_the_selection_past_the_month_scrolls_to_the_end_being_moved ()
        {
            using var calendar = Calendar (c => c.SetDate (new DateTime (2026, 3, 31)));

            // Pin the view on March explicitly (there and back), so the displayed month can no longer
            // drift with SelectionStart -- which does not move during a shift-extend.
            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.NextButton));
            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.PrevButton));
            Assert.Equal (new DateTime (2026, 3, 1), calendar.GetDisplayRange (visible: true).Start);

            calendar.Key (Keys.Right | Keys.Shift);

            Assert.Equal (new DateTime (2026, 3, 31), calendar.SelectionStart);
            Assert.Equal (new DateTime (2026, 4, 1), calendar.SelectionEnd);
            Assert.Equal (new DateTime (2026, 4, 1), calendar.GetDisplayRange (visible: true).Start);
        }

        // GUARD, not proof: no previous version could fail this. There was no keyboard handling at all
        // before the slice, so the selection could not walk past MaxDate either. It guards the clamp
        // in the new navigation path, which is easy to lose -- SelectionStart's own setter validates
        // against the RAW min/max fields (SMP-45), not the effective ones, so nothing below the
        // gesture would stop it.
        [Fact]
        public void Keyboard_navigation_stops_at_MaxDate ()
        {
            using var calendar = Calendar (c => {
                c.MaxDate = new DateTime (2026, 3, 20);
                c.SetDate (new DateTime (2026, 3, 20));
            });

            calendar.Key (Keys.Right);

            Assert.Equal (new DateTime (2026, 3, 20), calendar.SelectionStart);
        }

        // ---------------- the selection follows a programmatic change

        [Fact]
        public void SetDate_brings_the_month_it_selected_into_view ()
        {
            using var calendar = Calendar ();

            calendar.ClickAt (CentreOf (calendar, calendar.Geometry.NextButton));
            Assert.Equal (new DateTime (2026, 4, 1), calendar.GetDisplayRange (visible: true).Start);

            calendar.SetDate (new DateTime (2026, 9, 4));

            Assert.Equal (new DateTime (2026, 9, 1), calendar.GetDisplayRange (visible: true).Start);
        }

        // ---------------- the same click at whatever the display scaling is

        [Fact]
        public void A_click_on_a_computed_cell_centre_selects_that_date_at_any_scaling ()
        {
            // Hosted on a real (headless) window, so Control.Scaling reflects MF_HEADLESS_SCALE and
            // the logical/device conversion in HitTest is actually exercised. Comparing the two spaces
            // directly picks the cell at index/scale, which is how the equivalent ListBox and TreeView
            // bugs presented.
            HeadlessRenderer.Use ();

            using var form = new Form { UseSystemDecorations = true, Width = 320, Height = 320 };
            var calendar = new TestCalendar { Left = 0, Top = 0, Width = 220, Height = 162 };
            form.Controls.Add (calendar);
            calendar.SetDate (March);
            HeadlessRenderer.CapturePng (form, 320, 320);

            var date = new DateTime (2026, 3, 18);

            calendar.ClickAt (CentreOf (calendar, date));

            Assert.Equal (date, calendar.SelectionStart);
        }
    }
}
