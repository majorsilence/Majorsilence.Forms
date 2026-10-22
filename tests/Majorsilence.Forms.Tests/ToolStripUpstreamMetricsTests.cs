using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) lays a ToolStrip out as WinForms
    // does, measured from it: a (0, 0, 1, 0) padding, a 5px grip in its 2px margin, 6px separators, a (0, 1, 0, 2)
    // margin round each button, the items over the strip's whole height, and 2px kept after the last item. A 150 x 64
    // button after a separator is at (15, 1) on a 168 x 67 strip; it was at (14, 0),
    // 63px tall, with the items taking 164 x 63.
    [Collection ("Headless")]
    public class ToolStripUpstreamMetricsTests
    {
        private static (Form form, ToolStrip strip, ToolStripSeparator separator, ToolStripButton button) Strip (bool chooseFont)
        {
            if (chooseFont)
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 200) };
            var strip = new ToolStrip { Dock = DockStyle.None, Location = new Point (0, 0) };
            var separator = new ToolStripSeparator ();
            var button = new ToolStripButton ("Close") { AutoSize = false, Size = new Size (150, 64) };
            strip.Items.Add (separator);
            strip.Items.Add (button);
            form.Controls.Add (strip);
            form.Show ();
            strip.Size = strip.ItemsPreferredSize ();
            HeadlessRenderer.CapturePng (form, 600, 200);   // a strip lays its items out when it paints

            return (form, strip, separator, button);
        }

        [Fact]
        public void An_app_wide_font_gives_a_tool_strip_upstream_s_layout ()
        {
            try {
                var (form, strip, separator, button) = Strip (true);

                using (form) {
                    Assert.Equal (new Padding (0, 0, 1, 0), strip.Padding);
                    Assert.Equal (new Rectangle (2, 0, 5, strip.Height), strip.GripRectangle);
                    Assert.Equal (new Padding (0, 1, 0, 2), button.Margin);
                    Assert.Equal (Padding.Empty, separator.Margin);

                    // The extent a ToolStripPanel row gives an unstretched strip.
                    Assert.Equal (new Size (168, 67), strip.ItemsPreferredSize ());
                    Assert.Equal (new Rectangle (9, 0, 6, 67), separator.Bounds);
                    Assert.Equal (new Rectangle (15, 1, 150, 64), button.Bounds);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Without_a_chosen_font_a_tool_strip_keeps_the_theme_layout ()
        {
            var (form, strip, _, button) = Strip (false);

            using (form) {
                Assert.Equal (Padding.Empty, strip.Padding);
                Assert.Equal (Padding.Empty, button.Margin);
                Assert.Equal (new Rectangle (0, 0, 6, strip.Height), strip.GripRectangle);
            }
        }

        // An assigned margin stands, as upstream's does.
        [Fact]
        public void An_assigned_item_margin_is_kept ()
        {
            try {
                var (form, _, _, button) = Strip (true);

                using (form) {
                    button.Margin = new Padding (4);
                    Assert.Equal (new Padding (4), button.Margin);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
