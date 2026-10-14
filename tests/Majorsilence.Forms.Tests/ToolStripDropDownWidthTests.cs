using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A drop-down item was measured with 14px for its arrow, but RenderItem takes 20px (16 + a 4px gap)
    // out of the content box for it. The text of every ToolStripDropDownButton was therefore laid out 6px
    // narrower than it was measured, and wrapped: "Sort: Recent" drew as "Sort:" over "Recent", "Theme"
    // as "Them" over "e". Found by Recital (FRAMEWORK_GAPS GAP-008).
    [Collection ("Headless")]
    public class ToolStripDropDownWidthTests
    {
        [Theory]
        [InlineData ("Theme")]
        [InlineData ("Sort: Recent")]
        public void A_drop_down_button_is_measured_with_the_whole_arrow_gutter (string text)
        {
            HeadlessRenderer.Use ();

            var plain = new ToolStripButton (text);
            var drop_down = new ToolStripDropDownButton (text);
            drop_down.DropDownItems.Add (new ToolStripMenuItem ("One"));

            var strip = new ToolStrip { Width = 600, Height = 30 };
            strip.Items.Add (plain);
            strip.Items.Add (drop_down);

            var form = new Form { Width = 700, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();

            try {
                PaintSurface.Render (strip).Dispose ();

                // Same text, same padding: the only difference is the arrow's gutter.
                Assert.Equal (plain.Width + Renderers.ToolBarRenderer.DropDownArrowGutter (16), drop_down.Width);
            } finally {
                form.Close ();
            }
        }
    }
}
