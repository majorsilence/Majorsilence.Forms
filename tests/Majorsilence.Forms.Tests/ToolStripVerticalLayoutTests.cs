using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported app's ToolStrip and MenuStrip docked Left or Right run down the form, as upstream's do. Measured with a WinForms
    // probe (Microsoft Sans Serif 12pt as the app font): a ToolStrip of "Button0", "Btn1" with an image, a separator, an
    // image-only button and a label "Lbl" docked left on a 300 x 200 panel is 54 x 200, Orientation Vertical, its items at
    // y 10, 32, 54, 61 and 84 and 51px wide; a MenuStrip of "File0", "Edit", a separator and "Help" is 50 x 200, its items
    // from (6, 2), 19px tall and 37 wide. MF laid both out in a row across the top of a strip as wide as that row.
    [Collection ("Headless")]
    public class ToolStripVerticalLayoutTests
    {
        private static void Ported () => Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

        private static (Form form, T strip) Show<T> (T strip, Action<T> properties, int height = 200) where T : ToolStrip
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
            var panel = new Panel { Size = new Size (300, height) };

            form.Controls.Add (panel);
            panel.SuspendLayout ();
            strip.SuspendLayout ();
            properties (strip);
            panel.Controls.Add (strip);
            strip.ResumeLayout (false);
            strip.PerformLayout ();
            panel.ResumeLayout (false);
            panel.PerformLayout ();

            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 300);

            return (form, strip);
        }

        private static ToolStrip Tools ()
        {
            var image = new Majorsilence.Forms.Drawing.Bitmap (16, 16);
            var strip = new ToolStrip ();

            foreach (var item in new ToolStripItem[] {
                new ToolStripButton ("Button0"), new ToolStripButton ("Btn1", image), new ToolStripSeparator (),
                new ToolStripButton ("", image), new ToolStripLabel ("Lbl") })
                strip.Items.Add (item);

            return strip;
        }

        private static MenuStrip Menu ()
        {
            var strip = new MenuStrip ();

            foreach (var item in new ToolStripItem[] {
                new ToolStripMenuItem ("File0"), new ToolStripMenuItem ("Edit"), new ToolStripSeparator (), new ToolStripMenuItem ("Help") })
                ((ToolStrip) strip).Items.Add (item);

            return strip;
        }

        [Theory]
        [InlineData (DockStyle.Left)]
        [InlineData (DockStyle.Right)]
        public void A_tool_strip_docked_to_a_side_stacks_its_items (DockStyle dock)
        {
            Rectangle bounds;
            Rectangle[] items;
            Orientation orientation;
            ToolStripLayoutStyle style;
            Size preferred;

            try {
                Ported ();
                var (form, strip) = Show (Tools (), s => s.Dock = dock);

                using (form) {
                    bounds = strip.Bounds;
                    orientation = strip.Orientation;
                    style = strip.LayoutStyle;
                    preferred = strip.GetPreferredSize (Size.Empty);
                    items = strip.Items.Cast<ToolStripItem> ().Select (i => i.Bounds).ToArray ();
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (Orientation.Vertical, orientation);
            Assert.Equal (ToolStripLayoutStyle.VerticalStackWithOverflow, style);
            Assert.Equal (200, bounds.Height);
            Assert.Equal (dock == DockStyle.Left ? 0 : 300 - bounds.Width, bounds.X);
            Assert.True (bounds.Width < 80, $"width {bounds.Width}");

            // Below the 9px grip band, each item its own height in its margin (a button's 1 above and 2 below, a separator's
            // none): 19, 20, 6, 20 and 15 tall. The strip prefers them stacked, 2px after the last.
            Assert.Equal (new[] { 10, 32, 54, 61, 84 }, items.Select (i => i.Y));
            Assert.Equal (new[] { 19, 20, 6, 20, 15 }, items.Select (i => i.Height));
            Assert.Equal (103, preferred.Height);

            // All one width: the strip's, less its 1px right padding twice and a pixel, as upstream's split stack pads them.
            Assert.All (items, i => Assert.Equal ((0, bounds.Width - 3), (i.X, i.Width)));
        }

        [Fact]
        public void A_menu_strip_docked_left_stacks_its_items ()
        {
            Rectangle bounds;
            Rectangle[] items;
            Size preferred;

            try {
                Ported ();
                var (form, strip) = Show (Menu (), s => s.Dock = DockStyle.Left);

                using (form) {
                    bounds = strip.Bounds;
                    preferred = strip.GetPreferredSize (Size.Empty);
                    items = ((ToolStrip) strip).Items.Cast<ToolStripItem> ().Select (i => i.Bounds).ToArray ();
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (200, bounds.Height);

            // Inside the (6, 2, 0, 2) padding: three items a line of Segoe UI 9pt in its 2px border, and a 6px separator.
            Assert.Equal (new[] { 2, 21, 40, 46 }, items.Select (i => i.Y));
            Assert.Equal (69, preferred.Height);
            Assert.All (items, i => Assert.Equal ((6, bounds.Width - 13), (i.X, i.Width)));

            if (OperatingSystem.IsWindows ())
                Assert.Equal (new[] { 19, 19, 6, 19 }, items.Select (i => i.Height));
        }

        // A strip with AutoSize off pads its items to its display rectangle less a pixel instead.
        [Fact]
        public void A_fixed_width_vertical_strip_pads_its_items_to_its_width ()
        {
            Rectangle[] items;

            try {
                Ported ();
                var (form, strip) = Show (Tools (), s => { s.Dock = DockStyle.Left; s.AutoSize = false; s.Width = 60; });

                using (form)
                    items = strip.Items.Cast<ToolStripItem> ().Select (i => i.Bounds).ToArray ();
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.All (items, i => Assert.Equal (58, i.Width));
        }

        // StackWithOverflow follows the docked edge back to a row.
        [Fact]
        public void A_strip_docked_back_to_the_top_runs_across_again ()
        {
            Orientation orientation;
            int height;

            try {
                Ported ();
                var (form, strip) = Show (Tools (), s => { s.Dock = DockStyle.Left; s.Dock = DockStyle.Top; });

                using (form) {
                    orientation = strip.Orientation;
                    height = strip.Height;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (Orientation.Horizontal, orientation);
            Assert.Equal (25, height);
        }

        [Fact]
        public void Without_a_chosen_font_a_docked_left_strip_keeps_its_row ()
        {
            var (form, strip) = Show (Tools (), s => s.Dock = DockStyle.Left);

            using (form) {
                Assert.Equal (ToolStripLayoutStyle.HorizontalStackWithOverflow, strip.LayoutStyle);
                Assert.Equal (Orientation.Horizontal, strip.Orientation);
            }
        }
    }
}
