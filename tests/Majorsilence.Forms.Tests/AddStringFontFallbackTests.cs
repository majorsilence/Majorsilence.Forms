using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GraphicsPath.AddString took the glyph outlines from a single SKFont, which renders any codepoint
    // its typeface lacks as tofu -- so CJK and emoji came out as a row of identical boxes.
    //
    // This is not an obscure corner: a control library that renders all its text as filled glyph
    // outlines (for sharper anti-aliasing than DrawString gives) routes EVERY label through AddString.
    // With that mode on, its whole UI was boxes for any non-Latin script, while the same page with the
    // mode off rendered correctly -- because DrawString already had fallback and this path did not.
    // The two paths silently disagreeing is the thing these tests exist to prevent.
    public class AddStringFontFallbackTests
    {
        private const string Chinese = "图表类型销售趋势";
        private const string Latin = "Chart Type";

        private static SKBitmap Fill (string text, string family = "Segoe UI")
        {
            var bitmap = new SKBitmap (260, 40, SKColorType.Bgra8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Clear (SKColors.White);

                using var path = new GraphicsPath ();
                using var fontFamily = new Majorsilence.Forms.Drawing.FontFamily (family);
                path.AddString (text, fontFamily, (int)Majorsilence.Forms.Drawing.FontStyle.Regular, 16f, new PointF (2, 2), null);

                new Graphics (canvas).FillPath (new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), path);
                canvas.Flush ();
            }

            return bitmap;
        }

        private static int InkPixels (SKBitmap bitmap)
        {
            var ink = 0;

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y).Red < 200)
                        ink++;

            return ink;
        }

        // Ink per column. Tofu is the same box repeated, so the profile is highly repetitive; real
        // glyphs differ from one another.
        private static int DistinctColumnProfiles (SKBitmap bitmap)
        {
            var seen = new System.Collections.Generic.HashSet<int> ();

            for (var x = 0; x < bitmap.Width; x++) {
                var column = 0;

                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y).Red < 200)
                        column++;

                if (column > 0)
                    seen.Add (column);
            }

            return seen.Count;
        }

        [Fact]
        public void CJK_outlines_are_drawn_at_all ()
        {
            using var bitmap = Fill (Chinese);

            Assert.True (InkPixels (bitmap) > 0, "no glyph outlines were added for CJK text");
        }

        [Fact]
        public void CJK_outlines_are_not_a_row_of_identical_boxes ()
        {
            using var bitmap = Fill (Chinese);

            // Eight distinct ideographs cannot collapse to the two or three column profiles a repeated
            // rectangle produces.
            Assert.True (DistinctColumnProfiles (bitmap) > 6,
                $"only {DistinctColumnProfiles (bitmap)} distinct column profiles -- looks like tofu");
        }

        [Fact]
        public void Latin_outlines_still_work ()
        {
            using var bitmap = Fill (Latin);

            Assert.True (InkPixels (bitmap) > 0);
            Assert.True (DistinctColumnProfiles (bitmap) > 3);
        }

        [Fact]
        public void Mixed_scripts_produce_more_ink_than_either_half ()
        {
            using var latin = Fill (Latin);
            using var chinese = Fill (Chinese);
            using var mixed = Fill (Latin + Chinese);

            // Runs are split by which face covers them; a bug in the splitting or in advancing between
            // runs would drop one side or overprint them on top of each other.
            var mixedInk = InkPixels (mixed);
            Assert.True (mixedInk > InkPixels (latin), "the CJK run was dropped");
            Assert.True (mixedInk > InkPixels (chinese), "the Latin run was dropped");
        }
    }
}
