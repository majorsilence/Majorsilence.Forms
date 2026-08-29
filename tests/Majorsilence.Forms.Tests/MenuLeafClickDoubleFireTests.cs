using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regression: found by running the migrated ReportDesigner on a real Avalonia/X11 desktop. A
    // SINGLE click on File -> "New Report from Database" opened the dialog TWICE, stacked at the same
    // position -- looked like a freeze. dotnet-stack on the live process showed two nested modal
    // loops, the outer entered from MenuBase.OnMouseClick (the menu bar) and the inner from
    // MenuDropDown.OnMouseClick (the drop-down popup): one physical button release was dispatched to
    // BOTH the bar's window and the popup's window -- separate MajorsilenceFormsWindowHosts -- and
    // each routed it through its OnMouseClick to the same leaf item.
    //
    // The bar-routes-a-nested-leaf half of that only happens under the real X11 input stack (a plain
    // headless click on a MenuStrip top-level item just opens its drop down), so these drive the
    // click-dispatch path the duplicated delivery goes through -- RaiseClick -> OnMouseClick -> the
    // leaf's Click -- twice for one release, the way the two windows do, and assert the guard
    // (MenuBase.TryBeginLeafClick / EndLeafClick) collapses it to one.
    public class MenuLeafClickDoubleFireTests
    {
        private sealed class TestableMenuDropDown : MenuDropDown
        {
            public TestableMenuDropDown (MenuItem root) : base (root) { }

            // One physical release, as the window's click routing delivers it.
            public void DeliverRelease (Point p)
                => RaiseClick (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));
        }

        private static (TestableMenuDropDown dropdown, MenuItem leaf) Build ()
        {
            var root = new MenuItem ("File");
            var leaf = new MenuItem ("New");
            root.Items.Add (leaf);
            leaf.SetBounds (0, 0, 160, 24);
            return (new TestableMenuDropDown (root), leaf);
        }

        [Fact]
        public void OneRelease_DeliveredTwice_FiresTheLeafOnce ()
        {
            var (dropdown, leaf) = Build ();
            using (dropdown) {
                var fired = 0;
                leaf.Click += (_, _) => fired++;

                // Same release, delivered by two windows back to back, no fresh press between.
                dropdown.DeliverRelease (new Point (10, 10));
                dropdown.DeliverRelease (new Point (10, 10));

                Assert.Equal (1, fired);
            }
        }

        [Fact]
        public void TwoDeliberateClicks_AreBothHonoured ()
        {
            var (dropdown, leaf) = Build ();
            using (dropdown) {
                var fired = 0;
                leaf.Click += (_, _) => fired++;

                dropdown.DeliverRelease (new Point (10, 10));
                System.Threading.Thread.Sleep (80);   // past the duplicate-collapse window
                dropdown.DeliverRelease (new Point (10, 10));

                Assert.Equal (2, fired);
            }
        }

        [Fact]
        public void SecondDelivery_WhileTheFirstHandlerIsModal_IsSuppressed ()
        {
            HeadlessRenderer.Use ();

            var owner = new Form ();
            owner.Show ();

            var (dropdown, leaf) = Build ();
            using (owner)
            using (dropdown) {
                var opened = 0;
                leaf.Click += (_, _) =>
                {
                    opened++;
                    var dlg = new Form ();
                    var t = dlg.ShowDialogAsync (owner);
                    // The duplicate delivery arrives while this modal handler is on the stack --
                    // exactly what the live modal loop pumped.
                    dropdown.DeliverRelease (new Point (10, 10));
                    dlg.DialogResult = DialogResult.OK;
                    _ = t;
                };

                dropdown.DeliverRelease (new Point (10, 10));

                Assert.Equal (1, opened);
            }
        }

        // The full real-flow open-the-menu-and-click path still fires exactly once headlessly (it
        // always did -- the duplicate is X11-only), kept as a guard on that path.
        [Fact]
        public async Task RealFlow_OpenMenuThenClickLeaf_FiresOnce ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (400, 300) };
            var strip = new MenuStrip ();
            var file = new ToolStripMenuItem { Text = "File" };
            var leaf = new ToolStripMenuItem { Text = "New" };
            file.DropDownItems.Add (leaf);
            strip.Items.Add (file);
            form.Controls.Add (strip);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 300);

            using (form) {
                var fired = 0;
                leaf.Click += (_, _) => fired++;

                var fileAt = WindowPoint.In (strip, file.Bounds.X + 5, file.Bounds.Y + 5);
                HeadlessRenderer.MouseDown (form, fileAt.X, fileAt.Y);
                HeadlessRenderer.MouseUp (form, fileAt.X, fileAt.Y);
                Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

                var popup = Application.ActivePopupWindow;
                Assert.NotNull (popup);
                var b = ((MenuItem)leaf).Bounds;
                HeadlessRenderer.MouseDown (popup!, b.X + b.Width / 2, b.Y + b.Height / 2);
                HeadlessRenderer.MouseUp (popup!, b.X + b.Width / 2, b.Y + b.Height / 2);
                Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

                Assert.Equal (1, fired);
                await Task.CompletedTask;
            }
        }
    }
}
