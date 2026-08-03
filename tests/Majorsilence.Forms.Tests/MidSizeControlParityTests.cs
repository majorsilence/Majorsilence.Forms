using System;
using Xunit;

using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the mid-size control parity pass (docs/winforms-gap-plan.md).
    ///
    /// Weighted towards MonthCalendar, which is the half that genuinely computes: the three bolded
    /// date sets mean three different recurrence rules, <c>GetDisplayRange</c> has to include the
    /// greyed-out days of adjacent months when asked, and <c>SetCalendarDimensions</c> caps the total
    /// at twelve months the way WinForms does.
    /// </summary>
    public class MidSizeControlParityTests
    {
        [Fact]
        public void The_three_bolded_date_sets_mean_three_different_rules ()
        {
            using var calendar = new MonthCalendar ();

            calendar.AddBoldedDate (new DateTime (2026, 3, 14));
            calendar.AddMonthlyBoldedDate (new DateTime (2026, 1, 1));
            calendar.AddAnnuallyBoldedDate (new DateTime (2026, 12, 25));

            // The plain one is that day only.
            Assert.True (calendar.IsBoldedDate (new DateTime (2026, 3, 14)));
            Assert.False (calendar.IsBoldedDate (new DateTime (2027, 3, 14)));

            // The monthly one recurs on that day of every month.
            Assert.True (calendar.IsBoldedDate (new DateTime (2026, 7, 1)));
            Assert.True (calendar.IsBoldedDate (new DateTime (2030, 2, 1)));

            // The annual one recurs on that month and day of every year.
            Assert.True (calendar.IsBoldedDate (new DateTime (2031, 12, 25)));
            Assert.False (calendar.IsBoldedDate (new DateTime (2031, 12, 24)));
        }

        [Fact]
        public void Removing_a_bolded_date_removes_only_its_own_rule ()
        {
            using var calendar = new MonthCalendar ();
            calendar.AddBoldedDate (new DateTime (2026, 6, 5));
            calendar.AddMonthlyBoldedDate (new DateTime (2026, 6, 5));

            calendar.RemoveBoldedDate (new DateTime (2026, 6, 5));

            // The monthly rule still matches that day.
            Assert.True (calendar.IsBoldedDate (new DateTime (2026, 6, 5)));

            calendar.RemoveMonthlyBoldedDate (new DateTime (2026, 6, 5));
            Assert.False (calendar.IsBoldedDate (new DateTime (2026, 6, 5)));
        }

        [Fact]
        public void UpdateBoldedDates_publishes_the_lists_to_the_array_properties ()
        {
            using var calendar = new MonthCalendar ();
            calendar.AddBoldedDate (new DateTime (2026, 3, 14));
            calendar.AddAnnuallyBoldedDate (new DateTime (2026, 12, 25));

            calendar.UpdateBoldedDates ();

            Assert.Single (calendar.BoldedDates);
            Assert.Single (calendar.AnnuallyBoldedDates);
            Assert.Empty (calendar.MonthlyBoldedDates);
        }

        [Fact]
        public void RemoveAll_clears_only_the_set_it_names ()
        {
            using var calendar = new MonthCalendar ();
            calendar.AddBoldedDate (new DateTime (2026, 3, 14));
            calendar.AddMonthlyBoldedDate (new DateTime (2026, 1, 8));

            calendar.RemoveAllBoldedDates ();

            Assert.False (calendar.IsBoldedDate (new DateTime (2026, 3, 14)));
            Assert.True (calendar.IsBoldedDate (new DateTime (2026, 5, 8)));
        }

        [Fact]
        public void SetCalendarDimensions_caps_the_total_at_twelve_months ()
        {
            using var calendar = new MonthCalendar ();

            calendar.SetCalendarDimensions (5, 5);

            var dimensions = calendar.CalendarDimensions;
            Assert.True (dimensions.Width * dimensions.Height <= 12);

            calendar.SetCalendarDimensions (2, 3);
            Assert.Equal (new Size (2, 3), calendar.CalendarDimensions);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetCalendarDimensions (0, 1));
        }

        [Fact]
        public void GetDisplayRange_covers_the_months_being_shown ()
        {
            using var calendar = new MonthCalendar { SelectionStart = new DateTime (2026, 3, 14) };
            calendar.SetCalendarDimensions (2, 1);

            var visible = calendar.GetDisplayRange (visible: true);

            Assert.Equal (new DateTime (2026, 3, 1), visible.Start);
            Assert.Equal (new DateTime (2026, 4, 30), visible.End);   // two months
        }

        [Fact]
        public void GetDisplayRange_includes_the_adjacent_days_when_asked ()
        {
            using var calendar = new MonthCalendar { SelectionStart = new DateTime (2026, 3, 14) };
            calendar.SetCalendarDimensions (1, 1);

            var visible = calendar.GetDisplayRange (visible: true);
            var full = calendar.GetDisplayRange (visible: false);

            Assert.True (full.Start <= visible.Start);
            Assert.True (full.End >= visible.End);

            // The padded range always starts and ends on a week boundary.
            Assert.Equal (DayOfWeek.Sunday, full.Start.DayOfWeek);
            Assert.Equal (DayOfWeek.Saturday, full.End.DayOfWeek);
        }

        [Fact]
        public void HitTest_reports_nowhere_outside_the_control ()
        {
            using var calendar = new MonthCalendar { Size = new Size (200, 160) };

            var outside = calendar.HitTest (5000, 5000);

            Assert.Equal (MonthCalendar.HitArea.Nowhere, outside.HitArea);
            Assert.Equal (new Point (5000, 5000), outside.Point);
        }

        [Fact]
        public void HitTest_distinguishes_the_title_from_its_scroll_arrows ()
        {
            using var calendar = new MonthCalendar { Size = new Size (200, 160) };

            Assert.Equal (MonthCalendar.HitArea.PrevMonthButton, calendar.HitTest (1, 1).HitArea);
            Assert.Equal (MonthCalendar.HitArea.NextMonthButton, calendar.HitTest (199, 1).HitArea);
            Assert.Equal (MonthCalendar.HitArea.TitleMonth, calendar.HitTest (100, 1).HitArea);
        }

        [Fact]
        public void SingleMonthSize_divides_the_control_by_its_dimensions ()
        {
            using var calendar = new MonthCalendar { Size = new Size (200, 160) };
            calendar.SetCalendarDimensions (2, 2);

            Assert.Equal (new Size (100, 80), calendar.SingleMonthSize);
        }

        [Fact]
        public void RightToLeftLayout_notifies_once_per_change ()
        {
            using var calendar = new MonthCalendar ();
            var raised = 0;
            calendar.RightToLeftLayoutChanged += (_, _) => raised++;

            calendar.RightToLeftLayout = true;
            calendar.RightToLeftLayout = true;

            Assert.True (calendar.RightToLeftLayout);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void TreeView_VisibleCount_divides_the_client_area_by_the_item_height ()
        {
            using var tree = new TreeView { Size = new Size (200, 200), ItemHeight = 20 };

            Assert.True (tree.VisibleCount > 0);
            Assert.Equal (tree.ClientRectangle.Height / 20, tree.VisibleCount);
        }

        [Fact]
        public void TreeView_HitTest_reports_nothing_outside_the_control ()
        {
            using var tree = new TreeView { Size = new Size (120, 80) };

            var hit = tree.HitTest (new Point (5000, 5000));

            Assert.Null (hit.Node);
            Assert.Equal (TreeViewHitTestLocations.None, hit.Location);
        }

        [Fact]
        public void MaskedTextBox_ValidateText_converts_or_reports_null ()
        {
            var box = new MaskedTextBox { ValidatingType = typeof (int), Text = "42" };

            Assert.Equal (42, box.ValidateText ());

            box.Text = "not a number";
            Assert.Null (box.ValidateText ());       // reported, not thrown

            var untyped = new MaskedTextBox { Text = "42" };
            Assert.Null (untyped.ValidateText ());   // no ValidatingType to convert to
        }

        [Fact]
        public void RichTextBox_reports_no_redo_because_there_is_no_undo_stack ()
        {
            using var rich = new RichTextBox { Text = "edited" };

            Assert.False (rich.CanRedo);
            Assert.Equal (string.Empty, rich.RedoActionName);
            Assert.Equal (string.Empty, rich.UndoActionName);

            rich.Redo ();                            // must not throw
        }

        [Fact]
        public void RichTextBox_SelectionType_reports_whether_anything_is_selected ()
        {
            using var rich = new RichTextBox { Text = "hello world" };

            Assert.Equal (RichTextBoxSelectionTypes.Empty, rich.SelectionType);

            rich.Select (0, 5);

            Assert.Equal (RichTextBoxSelectionTypes.Text, rich.SelectionType);
            Assert.Equal ("hello", rich.SelectedRtf);
        }

        [Fact]
        public void AccessibleObject_HitTest_answers_from_its_own_bounds ()
        {
            var control = new Button { Text = "Save" };
            control.SetBounds (0, 0, 80, 24);
            var accessible = new Control.ControlAccessibleObject (control);

            Assert.Same (accessible, accessible.HitTest (10, 10));
            Assert.Null (accessible.HitTest (5000, 5000));
        }

        [Fact]
        public void AccessibleObject_reports_that_no_help_topic_exists ()
        {
            var accessible = new AccessibleObject ();

            Assert.Equal (-1, accessible.GetHelpTopic (out var fileName));
            Assert.Null (fileName);
        }

        [Fact]
        public void AccessibleObject_reports_that_a_notification_was_not_delivered ()
        {
            // False is how WinForms says the announcement did not happen, so a caller that checks
            // can fall back to its own messaging.
            var accessible = new AccessibleObject ();

            Assert.False (accessible.RaiseAutomationNotification (
                AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.All, "Saved"));
            Assert.False (accessible.RaiseLiveRegionChanged ());
        }

        [Fact]
        public void PropertyGrid_cannot_show_commands_without_a_designer_host ()
        {
            using var grid = new PropertyGrid ();

            Assert.False (grid.CanShowCommands);
            Assert.False (grid.CommandsVisible);
            Assert.Empty (grid.PropertyTabs);
            Assert.Null (grid.SelectedTab);
        }

        [Fact]
        public void WebBrowser_IsBusy_follows_the_ready_state ()
        {
            using var browser = new WebBrowser ();

            Assert.False (browser.IsBusy);

            browser.Navigate ("https://example.invalid/");

            Assert.True (browser.IsBusy);
        }

        [Fact]
        public void WebBrowser_reports_an_unknown_encryption_level ()
        {
            // Anything else would be a security claim this layer cannot back.
            using var browser = new WebBrowser ();

            Assert.Equal (WebBrowserEncryptionLevel.Unknown, browser.EncryptionLevel);
            Assert.Null (browser.Document);
        }
    }
}
