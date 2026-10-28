using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadCalendar's AllowMultipleView, MultiViewRows and MultiViewColumns, and the way Telerik's calendar lays its months
// over its bounds rather than sizing itself to them (UI.for.WinForms 2026.2.520).
[Collection ("Headless")]
public class TelerikCalendarMultiViewTests
{
    private static RadCalendar Calendar (int width = 216, int height = 715)
    {
        HeadlessRenderer.Use ();

        var calendar = new RadCalendar { Size = new Size (width, height), SelectionStart = new DateTime (2026, 3, 15), SelectionEnd = new DateTime (2026, 3, 15) };
        var form = new Form { Width = 900, Height = 900 };
        form.Controls.Add (calendar);

        return calendar;
    }

    [Fact]
    public void A_calendar_shows_one_month_however_big_it_is ()
    {
        var calendar = Calendar ();

        using (calendar.FindForm ()) {
            Assert.False (calendar.AllowMultipleView);
            Assert.Equal ((1, 1), (calendar.MultiViewRows, calendar.MultiViewColumns));
            Assert.Equal (new Size (1, 1), calendar.CalendarDimensions);
            Assert.Equal (new Size (216, 715), calendar.Size);

            // The month fills the box.
            Assert.True (calendar.GetMonthGeometry (0).Block.Height > calendar.DeviceClientRectangle.Height * 3 / 4);
        }
    }

    [Fact]
    public void Rows_set_before_multiple_view_is_allowed_are_ignored ()
    {
        var calendar = Calendar ();

        using (calendar.FindForm ()) {
            calendar.MultiViewRows = 3;
            calendar.MultiViewColumns = 2;
            Assert.Equal ((1, 1), (calendar.MultiViewRows, calendar.MultiViewColumns));

            calendar.AllowMultipleView = true;
            Assert.Equal ((1, 1), (calendar.MultiViewRows, calendar.MultiViewColumns));
            Assert.Equal (new Size (1, 1), calendar.CalendarDimensions);
        }
    }

    [Fact]
    public void A_multiple_view_lays_its_months_over_the_calendar_as_it_is ()
    {
        var calendar = Calendar ();

        using (calendar.FindForm ()) {
            // The order the designer sets them in.
            calendar.AllowMultipleView = true;
            calendar.MultiViewRows = 3;

            Assert.Equal (new Size (1, 3), calendar.CalendarDimensions);
            Assert.Equal (new Size (216, 715), calendar.Size);

            var months = Enumerable.Range (0, 3).Select (calendar.GetMonthGeometry).ToList ();
            Assert.Equal ([new DateTime (2026, 3, 1), new DateTime (2026, 4, 1), new DateTime (2026, 5, 1)], months.Select (m => m.Month));
            Assert.True (months[0].Block.Bottom <= months[1].Block.Top && months[1].Block.Bottom <= months[2].Block.Top);
            Assert.True (months[2].Block.Bottom <= calendar.DeviceClientRectangle.Bottom);

            calendar.MultiViewColumns = 2;
            Assert.Equal (new Size (2, 3), calendar.CalendarDimensions);
            Assert.Equal (new DateTime (2026, 4, 1), calendar.GetMonthGeometry (1).Month);
        }
    }

    [Fact]
    public void There_is_no_twelve_month_cap ()
    {
        var calendar = Calendar (900, 900);

        using (calendar.FindForm ()) {
            calendar.AllowMultipleView = true;
            calendar.MultiViewRows = 5;
            calendar.MultiViewColumns = 4;

            Assert.Equal (new Size (4, 5), calendar.CalendarDimensions);
            Assert.Equal (20, calendar.MonthsShown);
        }
    }

    [Fact]
    public void Resizing_keeps_the_months_and_the_months_keep_the_size ()
    {
        var calendar = Calendar ();

        using (calendar.FindForm ()) {
            calendar.AllowMultipleView = true;
            calendar.MultiViewRows = 2;

            calendar.Size = new Size (500, 300);

            Assert.Equal (new Size (500, 300), calendar.Size);
            Assert.Equal (2, calendar.MultiViewRows);
            Assert.Equal (new Size (1, 2), calendar.CalendarDimensions);

            calendar.ShowWeekNumbers = true;
            Assert.Equal (new Size (500, 300), calendar.Size);
        }
    }

    [Fact]
    public void A_count_below_one_is_kept_but_shows_one ()
    {
        var calendar = Calendar ();

        using (calendar.FindForm ()) {
            calendar.AllowMultipleView = true;
            calendar.MultiViewRows = 0;

            Assert.Equal (0, calendar.MultiViewRows);
            Assert.Equal (new Size (1, 1), calendar.CalendarDimensions);
        }
    }

    [Fact]
    public void Turning_multiple_view_off_goes_back_to_one_month ()
    {
        var calendar = Calendar ();

        using (calendar.FindForm ()) {
            calendar.AllowMultipleView = true;
            calendar.MultiViewRows = 3;
            calendar.MultiViewColumns = 2;

            calendar.AllowMultipleView = false;

            Assert.Equal ((1, 1), (calendar.MultiViewRows, calendar.MultiViewColumns));
            Assert.Equal (new Size (1, 1), calendar.CalendarDimensions);
        }
    }

    [Fact]
    public void A_plain_month_calendar_still_sizes_to_its_months ()
    {
        HeadlessRenderer.Use ();

        using var calendar = new MonthCalendar ();
        var single = calendar.Size;

        calendar.CalendarDimensions = new Size (1, 2);

        Assert.True (calendar.Height > single.Height);
    }
}
