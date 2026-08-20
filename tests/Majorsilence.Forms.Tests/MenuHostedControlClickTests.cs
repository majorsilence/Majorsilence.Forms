using System.Drawing;
using System.Reflection;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ToolStripControlHost puts a real control inside a menu popup, and mouse-down routes to the deepest
    // child -- so the hosted control ran Control.RaiseMouseDown's menu-closing check, was not a MenuBase,
    // and closed the very menu it was sitting in. Choosing a colour button in a menu dismissed the menu
    // instead of dropping its palette.
    public class MenuHostedControlClickTests
    {
        private static (Form form, ToolStripMenuItem menu, Control hosted) BuildMenuWithHostedControl ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (400, 300) };
            var strip = new MenuStrip ();
            var tools = new ToolStripMenuItem { Text = "Tools" };
            var hosted = new Button { Text = "Color", Size = new Size (90, 22) };
            tools.DropDownItems.Add (new ToolStripMenuItem { Text = "Customize" });
            tools.DropDownItems.Add (new ToolStripControlHost (hosted));
            strip.Items.Add (tools);
            form.Controls.Add (strip);

            form.Show ();
            HeadlessRenderer.CapturePng (form);   // lays the strip's items out
            ((MenuItem)tools).Selected = true;    // open the menu, as a click does

            // What MenuBase.Activate does when the strip is really clicked. Without it the whole
            // dismiss-on-press path is inert (ClosePopups deactivates Application.ActiveMenu and nothing
            // else), so a test that skips it cannot tell the fix from the bug in either direction.
            Application.ActiveMenu = strip;

            return (form, tools, hosted);
        }

        [Fact]
        public void Pressing_a_control_hosted_in_a_menu_does_not_dismiss_that_menu ()
        {
            var (form, tools, hosted) = BuildMenuWithHostedControl ();

            using (form) {
                Assert.True (tools.IsDropDownOpened, "The menu never opened, so the test proves nothing.");
                Assert.NotNull (hosted.Parent);   // the host parents it into the MenuDropDown

                // Straight at the hosted control, which is where the deepest-child routing lands.
                hosted.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1,
                    hosted.Width / 2, hosted.Height / 2, 0));

                Assert.True (tools.IsDropDownOpened,
                    "Pressing the hosted control closed the menu it lives in, so the control never got to act.");
            }
        }

        [Fact]
        public void Pressing_a_control_outside_the_menu_still_dismisses_it ()
        {
            var (form, tools, _) = BuildMenuWithHostedControl ();

            using (form) {
                // The behaviour the old check existed to provide has to survive: a press anywhere that is
                // not part of the open menu still dismisses it.
                var elsewhere = new Button { Bounds = new Rectangle (10, 200, 80, 24) };
                form.Controls.Add (elsewhere);

                elsewhere.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 5, 5, 0));

                Assert.False (tools.IsDropDownOpened,
                    "A press outside the menu no longer dismisses it.");
            }
        }
    }
}
