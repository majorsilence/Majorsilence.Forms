using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream shows a maximized MDI child without a frame: its icon and minimize/restore/close buttons
    // move into the parent's menu bar and its title into the parent's caption. Here the child kept a full
    // caption bar inside the parent, under the parent's own (ReportDesigner's documents showed both).
    [Collection ("Headless")]
    public class MdiMergedChromeTests
    {
        private static (Form parent, MenuStrip menu, Form child) Shell ()
        {
            HeadlessRenderer.Use ();

            var parent = new Form { Text = "Designer", ClientSize = new Size (900, 600) };
            var menu = new MenuStrip ();
            menu.Items.Add (new ToolStripMenuItem ("&File"));
            menu.Items.Add (new ToolStripMenuItem ("&Help"));
            parent.Controls.Add (menu);
            parent.MainMenuStrip = menu;
            parent.IsMdiContainer = true;
            parent.Show ();

            var child = new Form { Text = "Report.rdl", MdiParent = parent, ClientSize = new Size (300, 200) };
            child.WindowState = FormWindowState.Maximized;
            child.Show ();

            return (parent, menu, child);
        }

        private static MdiControlItem[] Merged (MenuStrip menu) => menu.Items.OfType<MdiControlItem> ().ToArray ();

        [Fact]
        public void A_maximized_child_has_no_frame_and_its_controls_are_in_the_menu_bar ()
        {
            var (parent, menu, child) = Shell ();

            using (parent) {
                var area = parent.MdiClientControl!.DisplayRectangle;
                Assert.Equal (area.Size, child.Size);

                var merged = Merged (menu);
                Assert.Equal (
                    new[] { MdiControlItem.Kind.System, MdiControlItem.Kind.Minimize, MdiControlItem.Kind.Restore, MdiControlItem.Kind.Close },
                    merged.Select (i => i.ControlKind));
                Assert.Same (merged[0], menu.Items[0]);

                // The buttons sit at the trailing edge, close last; the menus keep their places after the icon.
                // (A strip lays its items out when it paints.)
                HeadlessRenderer.CapturePng (parent, 900, 600);
                Assert.Equal (menu.ClientSize.Width, merged[3].Bounds.Right);
                Assert.True (merged[1].Bounds.Left > menu.Items.OfType<ToolStripMenuItem> ().Single (i => i.Text == "&Help").Bounds.Right);

                Assert.Equal ("Designer - [Report.rdl]", parent.DisplayTitle);
            }
        }

        [Fact]
        public void Restoring_the_child_takes_its_controls_back_out ()
        {
            var (parent, menu, child) = Shell ();

            using (parent) {
                Merged (menu).Single (i => i.ControlKind == MdiControlItem.Kind.Restore).PerformClick ();

                Assert.Equal (FormWindowState.Normal, child.WindowState);
                Assert.Empty (Merged (menu));
                Assert.Equal ("Designer", parent.DisplayTitle);

                // The frame draws its caption and border again, around the content.
                var frame = child.MdiHost!.Size;
                Assert.Equal (new Size (2 * MdiChildWindow.FrameBorder, MdiChildWindow.CaptionHeight + 2 * MdiChildWindow.FrameBorder),
                    new Size (frame.Width - child.Size.Width, frame.Height - child.Size.Height));
            }
        }

        [Fact]
        public void Closing_the_child_takes_its_controls_back_out ()
        {
            var (parent, menu, child) = Shell ();

            using (parent) {
                Merged (menu).Single (i => i.ControlKind == MdiControlItem.Kind.Close).PerformClick ();

                Assert.Empty (parent.MdiChildren);
                Assert.Empty (Merged (menu));
                Assert.Equal ("Designer", parent.DisplayTitle);
            }
        }

        [Fact]
        public void Renaming_the_child_renames_the_parent_caption ()
        {
            var (parent, _, child) = Shell ();

            using (parent) {
                child.Text = "Other.rdl";
                Assert.Equal ("Designer - [Other.rdl]", parent.DisplayTitle);
            }
        }

        [Fact]
        public void Without_a_menu_bar_a_maximized_child_keeps_its_caption ()
        {
            // The buttons would have nowhere to go.
            HeadlessRenderer.Use ();
            using var parent = new Form { Text = "Designer", ClientSize = new Size (900, 600), IsMdiContainer = true };
            parent.Show ();

            var child = new Form { Text = "Report.rdl", MdiParent = parent, ClientSize = new Size (300, 200) };
            child.WindowState = FormWindowState.Maximized;
            child.Show ();

            Assert.Equal (parent.MdiClientControl!.DisplayRectangle.Height - MdiChildWindow.CaptionHeight - 2 * MdiChildWindow.FrameBorder, child.Size.Height);
            Assert.Equal ("Designer", parent.DisplayTitle);
        }
    }
}
