using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms' UpDownBase owns an up/down buttons control and an edit box, adding the BUTTONS first so
    // Controls[0] is the buttons. That ordering is the documented way to theme one of these: code hooks
    // Controls[0].Paint to draw its own arrows and calls Controls[0].PointToClient to hit-test them. This
    // control drew itself with no children, so the idiom threw before the control existed.
    public class NumericUpDownChildControlTests
    {
        [Fact]
        public void The_first_child_is_the_up_down_buttons ()
        {
            HeadlessRenderer.Use ();

            using var spinner = new NumericUpDown ();

            Assert.NotEmpty (spinner.Controls);
            Assert.NotNull (spinner.Controls[0]);
        }

        [Fact]
        public void The_buttons_child_covers_the_button_strip_and_tracks_resizes ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            var spinner = new NumericUpDown { Bounds = new Rectangle (0, 0, 120, 24) };
            form.Controls.Add (spinner);
            form.Show ();

            var buttons = spinner.Controls[0];

            Assert.Equal (24, buttons.Height);
            Assert.Equal (120, buttons.Right);   // flush with the control's right edge

            spinner.Size = new Size (200, 40);
            form.PerformLayout ();

            // Derived from the control's size, so a resize has to move it -- a stored rectangle would
            // leave the buttons behind wherever they were first laid out.
            Assert.Equal (40, buttons.Height);
            Assert.Equal (200, buttons.Right);
        }

        [Fact]
        public void A_Paint_handler_on_the_buttons_child_is_raised ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            var spinner = new NumericUpDown { Bounds = new Rectangle (0, 0, 120, 24) };
            form.Controls.Add (spinner);

            var painted = 0;
            spinner.Controls[0].Paint += (_, _) => painted++;

            form.Show ();
            HeadlessRenderer.CapturePng (form);

            Assert.True (painted > 0, "The themer's Paint handler on Controls[0] never ran.");
        }

        [Fact]
        public void Clicking_the_buttons_still_changes_the_value ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            var spinner = new NumericUpDown { Bounds = new Rectangle (0, 0, 120, 24), Value = 5 };
            form.Controls.Add (spinner);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            // The child now covers the strip, so the click lands on IT rather than on the spinner -- the
            // forwarding is what keeps the buttons working, and is the thing most likely to break.
            var buttons = spinner.Controls[0];
            HeadlessRenderer.Click (form, buttons.Left + buttons.Width / 2, 4);

            Assert.Equal (6m, spinner.Value);

            HeadlessRenderer.Click (form, buttons.Left + buttons.Width / 2, 20);

            Assert.Equal (5m, spinner.Value);
        }
    }
}
