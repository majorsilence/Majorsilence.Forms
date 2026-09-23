using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Characterisation tests for layout/positioning defects seen on the Avalonia backend that do not
    // occur on real WinForms. Each asserts the invariant the upstream control guarantees, so a failure
    // here names the defect rather than describing a screenshot.
    public class MacQaLayoutTests
    {
        private static Form ShowFormAt (int x, int y, int w = 600, int h = 400)
        {
            HeadlessRenderer.Use ();
            var form = new Form { Size = new Size (w, h), StartPosition = FormStartPosition.Manual };
            form.Show ();
            form.Location = new Point (x, y);
            return form;
        }

        // Showing a form that is already shown must do nothing. It used to run the whole first-show
        // path again and give one form a SECOND window surface: input goes to the newer one on top
        // while the controls keep painting into the first, so the top window looks like an empty
        // shadow of the one beneath it and typing into it appears down there. Application code calls
        // Show twice quite ordinarily -- a factory that shows the form plus a configure callback that
        // also calls Show -- and upstream that is harmless.
        [Fact]
        public void Showing_an_already_shown_form_does_not_raise_a_second_window ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (300, 200) };
            form.Show ();
            form.Show ();
            form.Show ();

            var host = (Majorsilence.Forms.Headless.HeadlessWindowHost) form.Backend;
            Assert.Equal (1, host.ShowCount);
        }

        // Hiding and showing again is a real show, not a repeat: the guard must key on current
        // visibility rather than "has ever been shown".
        [Fact]
        public void Hiding_then_showing_again_shows_the_window_again ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new Size (300, 200) };
            form.Show ();
            form.Hide ();
            form.Show ();

            var host = (Majorsilence.Forms.Headless.HeadlessWindowHost) form.Backend;
            Assert.Equal (2, host.ShowCount);
        }

        // The calendar must open anchored to the field: its left edge on the field's left edge, its top
        // on the field's bottom. Detached placement (the popup landing near the screen origin, or at a
        // position unrelated to the field) is the reported defect.
        [Fact]
        public void DateTimePicker_drop_down_opens_directly_under_the_field ()
        {
            using var form = ShowFormAt (300, 200);
            var picker = new DateTimePicker { Location = new Point (40, 60), Size = new Size (120, 22) };
            form.Controls.Add (picker);

            picker.DroppedDown = true;

            var popup = Application.ActivePopupWindow;
            Assert.NotNull (popup);

            var expected = form.PointToScreen (new Point (picker.Left, picker.Bottom));
            Assert.Equal (expected, popup!.Location);
        }

        // Same guarantee for a field inside a container: a GroupBox is what the reported forms use, and
        // the popup must account for the container's offset, not just the field's own Location.
        [Fact]
        public void DateTimePicker_drop_down_accounts_for_its_container_offset ()
        {
            using var form = ShowFormAt (300, 200);
            var group = new GroupBox { Location = new Point (25, 35), Size = new Size (300, 150), Text = "Dates" };
            form.Controls.Add (group);

            var picker = new DateTimePicker { Location = new Point (15, 40), Size = new Size (120, 22) };
            group.Controls.Add (picker);

            picker.DroppedDown = true;

            var popup = Application.ActivePopupWindow;
            Assert.NotNull (popup);

            var expected = form.PointToScreen (
                new Point (group.Left + picker.Left, group.Top + picker.Bottom));
            Assert.Equal (expected, popup!.Location);
        }

        // A GroupBox's caption occupies the top of its own border, so its client area must start BELOW
        // the caption. A child placed at the top of the client area overlapping the caption/border is
        // the reported defect ("Select Levy Types" drawing through its own frame).
        [Fact]
        public void GroupBox_client_area_starts_below_its_caption ()
        {
            using var form = ShowFormAt (0, 0);
            var group = new GroupBox { Location = new Point (10, 10), Size = new Size (200, 120), Text = "Select Levy Types" };
            form.Controls.Add (group);

            var client = group.DisplayRectangle;

            Assert.True (client.Y > 0, $"client area starts at Y={client.Y}; it must clear the caption");
            Assert.True (client.Height < group.Height, "client area must be shorter than the box");
        }

        // A control added to a GroupBox is positioned relative to that box's client area. Its position
        // in the form is the box's own position plus its client origin plus the child's Location --
        // which is what a drop-down, a hit test and a native overlay all rely on.
        [Fact]
        public void A_child_of_a_GroupBox_reports_its_position_through_the_container ()
        {
            using var form = ShowFormAt (0, 0);
            var group = new GroupBox { Location = new Point (30, 40), Size = new Size (200, 120) };
            form.Controls.Add (group);

            var child = new TextBox { Location = new Point (12, 18), Size = new Size (80, 22) };
            group.Controls.Add (child);

            var expected = form.PointToScreen (new Point (group.Left + child.Left, group.Top + child.Top));
            Assert.Equal (expected, child.PointToScreen (Point.Empty));
        }
    }
}
