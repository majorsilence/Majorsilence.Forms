using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the tool strips WinForms' default
    // ToolStripProfessionalRenderer draws under Windows 11, measured from a module window's toolbar: shaded down from #FCFCFC
    // to #F8F8F8 over its first 12px, idle buttons showing the shading through, a grip of white 2x2 squares each overlaid a
    // pixel up and left by black ones, a #F5 > #F2 > #A0 edging down its right side, corner pixels left to the panel, and the
    // ToolStripPanel round it shaded #F0F0F0 to #FCFCFC across its parent. The strip and its buttons were the theme's flat
    // #F0F0F0, with a grey dotted grip.
    [Collection ("Headless")]
    public class ToolStripUpstreamPaintTests
    {
        private static T WithStrip<T> (System.Func<SKBitmap, ToolStripPanel, ToolStrip, int, T> check)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (800, 200) };
                var panel = new ToolStripPanel { Dock = DockStyle.Top };
                var strip = new ToolStrip ();
                strip.Items.Add (new ToolStripButton ("Close") { AutoSize = false, Size = new Size (60, 50) });
                panel.Join (strip);
                form.Controls.Add (panel);
                form.Show ();
                strip.AutoSize = false;
                strip.Size = new Size (200, 60);
                HeadlessRenderer.CapturePng (form, 800, 200);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 800, 200));
                var px = (int) System.Math.Round (bitmap.Width / 800f);
                return check (bitmap, panel, strip, px);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static SKColor Grey (byte v) => new SKColor (v, v, v);

        [Fact]
        public void A_tool_strip_is_shaded_down_through_its_idle_buttons ()
            => WithStrip ((bitmap, panel, strip, px) => {
                var origin = new Point (strip.Left * px, strip.Top * px);
                var button = strip.Items[0].Bounds;
                var x = origin.X + (button.Left + button.Width / 2) * px;
                var width = 12 * px;

                Assert.Equal (Grey (0xFC), bitmap.GetPixel (x, origin.Y));
                Assert.Equal (Grey (0xF8), bitmap.GetPixel (x, origin.Y + width));
                Assert.Equal (Grey (0xF8), bitmap.GetPixel (x, origin.Y + strip.Height * px / 2));
                return 0;
            });

        [Fact]
        public void A_tool_strip_s_grip_is_white_squares_overlaid_by_black_ones ()
            => WithStrip ((bitmap, panel, strip, px) => {
                var grip = new Rectangle (strip.Left * px + strip.GripRectangle.Left * px, strip.Top * px + strip.GripRectangle.Top * px,
                    strip.GripRectangle.Width * px, strip.GripRectangle.Height * px);
                var x = grip.Left + grip.Width / 2;
                var y = grip.Top + 4 * px + 1;

                // The light square's lower right pixel shows; the dark one covers the rest, a pixel up and left.
                Assert.Equal (SKColors.White, bitmap.GetPixel (x + 1, y + 1));
                Assert.Equal (SKColors.Black, bitmap.GetPixel (x - 1, y - 1));
                Assert.Equal (SKColors.Black, bitmap.GetPixel (x, y));
                Assert.Equal (SKColors.Black, bitmap.GetPixel (x - 1, y + 3));
                return 0;
            });

        [Fact]
        public void A_tool_strip_is_edged_on_the_right_and_leaves_its_corners_to_the_panel ()
            => WithStrip ((bitmap, panel, strip, px) => {
                var left = strip.Left * px;
                var top = strip.Top * px;
                var right = left + strip.Width * px - 1;
                var middle = top + strip.Height * px / 2;

                Assert.False (strip.OverflowButton.Visible);
                Assert.Equal (Grey (0xF2), bitmap.GetPixel (right, middle));

                // The top left pixel and the top right three show the panel's shading, not the strip's #FCFCFC.
                Assert.NotEqual (Grey (0xFC), bitmap.GetPixel (left, top));
                Assert.Equal (bitmap.GetPixel (left - 1, top), bitmap.GetPixel (left, top));
                Assert.Equal (bitmap.GetPixel (right + 1, top), bitmap.GetPixel (right, top));
                Assert.Equal (bitmap.GetPixel (right + 1, top + 2), bitmap.GetPixel (right, top + 2));

                // Beside them, the curve's shadow pixels.
                Assert.Equal (Grey (0xF8), bitmap.GetPixel (left + 1, top));
                return 0;
            });

        [Fact]
        public void A_tool_strip_panel_is_shaded_left_to_right ()
            => WithStrip ((bitmap, panel, strip, px) => {
                var y = (strip.Top + strip.Height / 2) * px;
                var near = bitmap.GetPixel (1, y);
                var far = bitmap.GetPixel (bitmap.Width - 2, y);

                Assert.InRange (near.Red, 0xF0, 0xF1);
                Assert.InRange (far.Red, 0xFB, 0xFC);
                return 0;
            });
    }
}
