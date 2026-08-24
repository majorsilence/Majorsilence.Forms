using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Graphics.DrawString -- the GDI+-shaped API ported code calls -- used to draw straight to
    // SKCanvas.DrawText with a single SKFont, which does NO font fallback: any codepoint the chosen
    // typeface lacks rendered as tofu. Meanwhile MeasureString goes through RichTextKit, which DOES fall
    // back, so a CJK or emoji string was laid out at the right size and then drawn as a row of boxes.
    // Found with a Chinese control library, where every label was tofu.
    //
    // It now routes through the same RichTextKit path, and these tests pin that -- because the two sides
    // silently disagreeing is exactly how the bug arose in the first place.
    public class DrawStringFontFallbackTests
    {
        private const string Chinese = "通用布局导航";

        private static SKBitmap Draw (System.Action<Graphics> paint)
        {
            var bitmap = new SKBitmap (260, 60, SKColorType.Bgra8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Clear (SKColors.White);
                paint (new Graphics (canvas));
                canvas.Flush ();
            }

            return bitmap;
        }

        private static int InkPixels (SKBitmap bitmap)
        {
            var ink = 0;

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);

                    if (c.Red < 200 || c.Green < 200 || c.Blue < 200)
                        ink++;
                }

            return ink;
        }

        [Fact]
        public void DrawString_and_the_canvas_text_path_agree_for_CJK ()
        {
            HeadlessRenderer.Use ();

            var font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 12f);
            using var viaDrawString = Draw (g => g.DrawString (Chinese, font,
                new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), 4f, 4f));

            // The library's own renderers draw through this extension, which has always had fallback.
            // DrawString producing a different picture is precisely the regression to catch.
            using var viaCanvas = Draw (g => { });
            using (var canvas = new SKCanvas (viaCanvas)) {
                canvas.Clear (SKColors.White);
                canvas.DrawText (Chinese, font.GetSKTypeface (), (int)System.Math.Round (font.PixelSize),
                    new Rectangle (4, 4, 1 << 20, 1 << 20), SKColors.Black, ContentAlignment.TopLeft);
                canvas.Flush ();
            }

            Assert.Equal (InkPixels (viaCanvas), InkPixels (viaDrawString));
        }

        [Fact]
        public void CJK_text_is_not_drawn_as_identical_boxes ()
        {
            HeadlessRenderer.Use ();

            // Tofu is the SAME box for every missing codepoint, so a run of DIFFERENT characters rendered
            // as tofu repeats horizontally. Real glyphs differ from each other, so the column-ink profile
            // of distinct characters cannot be perfectly periodic.
            var font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 14f);
            using var bitmap = Draw (g => g.DrawString (Chinese, font,
                new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), 2f, 2f));

            Assert.True (InkPixels (bitmap) > 0, "Nothing was drawn at all.");

            var columns = new int[bitmap.Width];

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++) {
                    var c = bitmap.GetPixel (x, y);

                    if (c.Red < 200 || c.Green < 200 || c.Blue < 200)
                        columns[x]++;
                }

            var distinct = new System.Collections.Generic.HashSet<int> (columns);

            // Six identical boxes would give a handful of repeated column heights; real glyphs give many.
            Assert.True (distinct.Count > 6,
                $"Only {distinct.Count} distinct column ink counts -- the text looks like repeated boxes.");
        }

        [Fact]
        public void A_gradient_brush_still_draws_something ()
        {
            HeadlessRenderer.Use ();

            // Non-solid brushes keep the direct path (there is no single colour to hand RichTextKit), so
            // this guards that the fallback branch did not break them.
            var font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 12f);
            using var bitmap = Draw (g => g.DrawString ("Hello", font,
                new Majorsilence.Forms.Drawing.Drawing2D.LinearGradientBrush (
                    new Rectangle (0, 0, 100, 20), Color.Red, Color.Blue, 0f), 4f, 4f));

            Assert.True (InkPixels (bitmap) > 0, "A gradient-brushed string drew nothing.");
        }
    }
}
