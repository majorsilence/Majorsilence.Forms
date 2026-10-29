using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadCalendar's single-date selection, as measured on Telerik UI.for.WinForms 2026.2.520: setting SelectedDate raises
// SelectionChanging then SelectionChanged, even when the date is already selected; SelectedDates holds it; clearing
// SelectedDates raises nothing; and a date the user picks is announced the same way.
[Collection ("Headless")]
public class TelerikCalendarSelectionTests
{
    private sealed class TestCalendar : RadCalendar
    {
        internal void ClickAt (Point p)
        {
            OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, 0));
            OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, 0));
        }
    }

    private static TestCalendar Calendar ()
    {
        HeadlessRenderer.Use ();
        var calendar = new TestCalendar { Width = 220, Height = 162 };
        calendar.SetDate (new DateTime (2026, 3, 14));
        return calendar;
    }

    private static Point CentreOf (MonthCalendar calendar, DateTime date)
    {
        var cell = calendar.GetDateCellBounds (date);
        Assert.False (cell.IsEmpty);
        return new Point (calendar.DeviceToLogicalUnits (cell.Left + (cell.Width / 2)), calendar.DeviceToLogicalUnits (cell.Top + (cell.Height / 2)));
    }

    [Fact]
    public void Setting_SelectedDate_raises_SelectionChanging_then_SelectionChanged ()
    {
        using var calendar = Calendar ();
        var order = new List<string> ();
        calendar.SelectionChanging += (_, e) => order.Add ("changing:" + e.Dates[0].Day);
        calendar.SelectionChanged += (_, _) => order.Add ("changed");

        Assert.Empty (calendar.SelectedDates);

        calendar.SelectedDate = new DateTime (2026, 3, 4);
        Assert.Equal (new[] { "changing:4", "changed" }, order);
        Assert.Equal (new DateTime (2026, 3, 4), calendar.SelectedDate);
        Assert.Equal (new[] { new DateTime (2026, 3, 4) }, calendar.SelectedDates);

        // The same date again is announced again, as Telerik's is.
        calendar.SelectedDate = new DateTime (2026, 3, 4);
        Assert.Equal (4, order.Count);

        // Clearing the selection raises nothing.
        calendar.SelectedDates.Clear ();
        Assert.Empty (calendar.SelectedDates);
        Assert.Equal (4, order.Count);
    }

    [Fact]
    public void A_cancelled_SelectionChanging_keeps_the_selection ()
    {
        using var calendar = Calendar ();
        calendar.SelectedDate = new DateTime (2026, 3, 4);

        var changed = 0;
        calendar.SelectionChanging += (_, e) => e.Cancel = true;
        calendar.SelectionChanged += (_, _) => changed++;

        calendar.SelectedDate = new DateTime (2026, 3, 9);
        Assert.Equal (new DateTime (2026, 3, 4), calendar.SelectedDate);

        calendar.ClickAt (CentreOf (calendar, new DateTime (2026, 3, 20)));
        Assert.Equal (new DateTime (2026, 3, 4), calendar.SelectedDate);
        Assert.Equal (0, changed);
    }

    [Fact]
    public void A_date_the_user_picks_raises_SelectionChanged ()
    {
        using var calendar = Calendar ();
        var changed = 0;
        calendar.SelectionChanged += (_, _) => changed++;

        calendar.ClickAt (CentreOf (calendar, new DateTime (2026, 3, 20)));

        Assert.Equal (1, changed);
        Assert.Equal (new DateTime (2026, 3, 20), calendar.SelectedDate);
        Assert.Single (calendar.SelectedDates);
    }
}
