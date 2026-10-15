using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // ToolStripItem.ForeColor and ToolStrip.ForeColor were stored, documented as "read by the strip
    // renderers", and read by nothing unless a ToolStripRenderer was installed: item text always took the
    // built-in item style's near-black. A strip themed dark therefore drew dark text on a dark strip.
    // Found by Recital's dark reading themes (FRAMEWORK_GAPS GAP-009).
    [Collection ("Headless")]
    public class ToolStripItemForeColorTests
    {
        private static int CountNear (SKBitmap bitmap, SKColor color)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var p = bitmap.GetPixel (x, y);
                    if (Math.Abs (p.Red - color.Red) < 40 && Math.Abs (p.Green - color.Green) < 40 && Math.Abs (p.Blue - color.Blue) < 40)
                        count++;
                }
            return count;
        }

        private static int RedTextPixels (Action<ToolStrip, ToolStripButton> colour)
        {
            HeadlessRenderer.Use ();

            var button = new ToolStripButton ("Contents and more");
            var strip = new ToolStrip { Width = 300, Height = 30, BackColor = Color.White };
            strip.Items.Add (button);
            colour (strip, button);

            var form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();

            try {
                using var bitmap = PaintSurface.Render (strip);
                return CountNear (bitmap, SKColors.Red);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_items_own_ForeColor_colours_its_text ()
            => Assert.True (RedTextPixels ((_, button) => button.ForeColor = Color.Red) > 20);

        [Fact]
        public void The_strips_ForeColor_colours_item_text ()
            => Assert.True (RedTextPixels ((strip, _) => strip.ForeColor = Color.Red) > 20);

        [Fact]
        public void Without_a_ForeColor_the_default_style_still_applies ()
            => Assert.Equal (0, RedTextPixels ((_, _) => { }));
    }
}
