using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regression: designer-generated InitializeComponent resizes a container while its layout is
    // suspended -- SuspendLayout, add children (container still at its default size), assign the real
    // Size, ResumeLayout(false) -- and nothing re-snapshotted the anchored children afterwards. Their
    // captured distance-from-edges still referred to the default size, so the first real layout
    // stretched every one of them from a rectangle the container never had: a bottom-right button
    // landed outside the container entirely and a top-left checkbox was pushed hundreds of pixels
    // down. Found on a search form whose controls outside its grid were all displaced or invisible.
    [Collection ("Headless")]
    public class AnchorResizeWhileSuspendedTests
    {
        [Fact]
        public void Children_added_before_a_suspended_resize_keep_their_designed_positions ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var panel = new Panel ();
            form.Controls.Add (panel);

            // The order below is the order the designer emits, and it matters: the children are added
            // while the panel is still at its default size, and the panel's real size arrives after.
            panel.SuspendLayout ();

            var topLeft = new CheckBox {
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Location = new Point (10, 6),
                Size = new Size (80, 20)
            };
            var bottomRight = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point (741, 40),
                Size = new Size (90, 27)
            };
            panel.Controls.Add (topLeft);
            panel.Controls.Add (bottomRight);

            panel.Size = new Size (851, 238);
            panel.ResumeLayout (false);
            panel.PerformLayout ();

            Assert.Equal (new Point (10, 6), topLeft.Location);
            Assert.Equal (new Point (741, 40), bottomRight.Location);
        }

        [Fact]
        public void A_suspended_resize_still_moves_anchored_children_on_later_resizes ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var panel = new Panel ();
            form.Controls.Add (panel);

            panel.SuspendLayout ();
            var bottomRight = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point (741, 40),
                Size = new Size (90, 27)
            };
            panel.Controls.Add (bottomRight);
            panel.Size = new Size (851, 238);
            panel.ResumeLayout (false);
            panel.PerformLayout ();

            // Repairing the snapshot must not freeze it: the gaps captured at 851x238 are the ones a
            // subsequent resize has to preserve.
            var rightGap = panel.Width - bottomRight.Bounds.Right;
            var bottomGap = panel.Height - bottomRight.Bounds.Bottom;

            panel.Size = new Size (1000, 400);
            panel.PerformLayout ();

            Assert.Equal (rightGap, panel.Width - bottomRight.Bounds.Right);
            Assert.Equal (bottomGap, panel.Height - bottomRight.Bounds.Bottom);
        }

        // A form whose base class sets a ClientSize in its constructor, and whose own designer code
        // then sets the real one inside SuspendLayout before adding its controls. Measured on WinForms
        // (.NET 10): the controls keep their designed bounds, as they do with no base size at all.
        // Suspending the form held its client area at the base size, so anchored controls took their
        // distances from 800x450 and grew by the difference when the layout resumed.
        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void A_form_resized_while_suspended_anchors_its_controls_to_the_new_client_size (bool baseSize)
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            if (baseSize)
                form.ClientSize = new Size (800, 450);

            var group = new GroupBox {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point (18, 35),
                Size = new Size (1036, 444)
            };
            var save = new Button {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point (879, 483),
                Size = new Size (75, 29)
            };

            form.SuspendLayout ();
            form.ClientSize = new Size (1071, 516);
            form.Controls.Add (group);
            form.Controls.Add (save);
            form.ResumeLayout (false);
            form.PerformLayout ();
            form.Show ();
            Application.DoEvents ();

            Assert.Equal (new Rectangle (18, 35, 1036, 444), group.Bounds);
            Assert.Equal (new Rectangle (879, 483, 75, 29), save.Bounds);
            form.Close ();
        }
    }
}
