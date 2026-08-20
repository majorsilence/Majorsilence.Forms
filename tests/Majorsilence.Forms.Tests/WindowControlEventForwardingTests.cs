using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Form is not a Control here, so the Control events a WinForms Form inherits do not come for free --
    // `form.MouseClick += ...` did not even compile. They now forward from WindowBase to the root
    // ControlAdapter, which is the window's client surface. Compiling is only half of it: a forward that
    // subscribes to the wrong object compiles and never fires, so these check they actually arrive.
    public class WindowControlEventForwardingTests
    {
        [Fact]
        public void MouseClick_on_the_client_area_reaches_a_Form_handler ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            form.Show ();

            var clicks = 0;
            form.MouseClick += (_, _) => clicks++;

            HeadlessRenderer.Click (form, 150, 100);

            Assert.Equal (1, clicks);
        }

        [Fact]
        public void ControlAdded_reaches_a_Form_handler ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            form.Show ();

            Control? added = null;
            form.ControlAdded += (_, e) => added = e.Control;

            var panel = new Panel ();
            form.Controls.Add (panel);

            Assert.Same (panel, added);
        }

        [Fact]
        public void BackColorChanged_reaches_a_Form_handler ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            form.Show ();

            var changed = 0;
            form.BackColorChanged += (_, _) => changed++;

            form.Controls.Owner.BackColor = Color.Firebrick;

            Assert.Equal (1, changed);
        }
    }
}
