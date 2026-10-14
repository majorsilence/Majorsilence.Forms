using System;
using System.Collections.Generic;
using SkiaSharp;
using Xunit;
using MSGraphics = Majorsilence.Forms.Drawing.Graphics;
using MSPen = Majorsilence.Forms.Drawing.Pen;

namespace Majorsilence.Forms.Tests
{
    // Horizontal and vertical lines are filled as the rectangle they cover instead of stroked (performance plan F15):
    // Skia strokes anything wider than a hairline by building and filling an outline path, four times the cost. The
    // pixels must be the same. The oracle is the stroke itself, drawn the old way, compared byte for byte.
    public class LineAsRectParityTests
    {
        private static readonly float[] widths = [1f, 2f, 3f, 0.5f, 2.5f];

        private static IEnumerable<(float X1, float Y1, float X2, float Y2)> Lines ()
        {
            var rnd = new Random (7);
            var coords = new List<float> ();
            for (var v = 0f; v <= 5f; v += 0.125f)
                coords.Add (v);
            coords.AddRange ([0.1f, 0.33f, 0.49f, 0.51f, 2.999f, 3.001f, 4.4999f, 4.5001f]);

            foreach (var c in coords) {
                for (var k = 0; k < 4; k++) {
                    var a = coords[rnd.Next (coords.Count)] + 2;
                    var b = k == 3 ? a : coords[rnd.Next (coords.Count)] + 2 + k * 3;   // k == 3: zero length
                    yield return (c + 2, a, c + 2, b);
                    yield return (a, c + 2, b, c + 2);
                }
            }
        }

        private static readonly (float Sx, float Sy, float Tx, float Ty)[] transforms = [
            (1, 1, 0, 0), (1.25f, 1.25f, 0.3f, 0.6f), (1.5f, 1.5f, -2.7f, 0.25f), (2, 2, 0.5f, 1), (0.75f, 0.75f, 0, 0),
            (3, 3, 0.125f, 0), (-2, 1, 60, 0), (1.75f, 1.25f, 0, 0.5f),
        ];

        [Fact]
        public void A_canvas_line_fills_the_pixels_its_stroke_did ()
        {
            using var expected = new SKBitmap (64, 64);
            using var actual = new SKBitmap (64, 64);
            using var stroke = new SKPaint { Color = new SKColor (20, 40, 200, 128) };
            var color = new SKColor (20, 40, 200, 128);

            foreach (var (sx, sy, tx, ty) in transforms)
                foreach (var thickness in new[] { 1, 2, 3 })
                    foreach (var (x1, y1, x2, y2) in Lines ()) {
                        expected.Erase (SKColors.White);
                        actual.Erase (SKColors.White);
                        using (var canvas = Prepared (expected, sx, sy, tx, ty)) {
                            stroke.StrokeWidth = thickness;
                            canvas.DrawLine (x1, y1, x2, y2, stroke);
                        }
                        using (var canvas = Prepared (actual, sx, sy, tx, ty))
                            canvas.DrawLine (x1, y1, x2, y2, color, thickness);

                        if (!expected.Bytes.AsSpan ().SequenceEqual (actual.Bytes))
                            Assert.Fail ($"scale ({sx}, {sy}) offset ({tx}, {ty}) width {thickness}: ({x1}, {y1})-({x2}, {y2})");
                    }
        }

        [Fact]
        public void A_Graphics_line_with_a_plain_pen_draws_what_a_stroked_one_does ()
        {
            using var expected = new Drawing.Bitmap (64, 64);
            using var actual = new Drawing.Bitmap (64, 64);

            foreach (var (sx, sy, tx, ty) in transforms)
                foreach (var width in widths)
                    foreach (var (x1, y1, x2, y2) in Lines ()) {
                        // A round join puts the pen off the fast path; a join changes nothing on one segment.
                        using var stroked = new MSPen (System.Drawing.Color.FromArgb (128, 20, 40, 200), width) { LineJoin = Drawing.Drawing2D.LineJoin.Round };
                        using var plain = new MSPen (System.Drawing.Color.FromArgb (128, 20, 40, 200), width);

                        Draw (expected, stroked, sx, sy, tx, ty, x1, y1, x2, y2);
                        Draw (actual, plain, sx, sy, tx, ty, x1, y1, x2, y2);

                        if (!Pixels (expected).AsSpan ().SequenceEqual (Pixels (actual)))
                            Assert.Fail ($"scale ({sx}, {sy}) offset ({tx}, {ty}) pen {width}: ({x1}, {y1})-({x2}, {y2})");
                    }
        }

        // A rotated canvas and a diagonal line keep the stroke; this pins that they still draw.
        [Fact]
        public void A_rotated_canvas_and_a_diagonal_line_still_draw ()
        {
            using var bitmap = new SKBitmap (64, 64);
            bitmap.Erase (SKColors.White);
            using (var canvas = new SKCanvas (bitmap)) {
                canvas.RotateDegrees (30, 32, 32);
                canvas.DrawLine (10, 32, 54, 32, SKColors.Black, 2);
                canvas.ResetMatrix ();
                canvas.DrawLine (5, 5, 20, 40, SKColors.Black, 1);
            }

            Assert.Equal (SKColors.Black, bitmap.GetPixel (32, 32));
            Assert.NotEqual (SKColors.White, bitmap.GetPixel (12, 22));
        }

        private static SKCanvas Prepared (SKBitmap bitmap, float sx, float sy, float tx, float ty)
        {
            var canvas = new SKCanvas (bitmap);
            canvas.ClipRect (new SKRect (5.5f, 3, 40, 37.25f));
            canvas.Translate (tx, ty);
            canvas.Scale (sx, sy);
            return canvas;
        }

        private static void Draw (Drawing.Bitmap bitmap, MSPen pen, float sx, float sy, float tx, float ty, float x1, float y1, float x2, float y2)
        {
            using var g = MSGraphics.FromImage (bitmap);
            g.Clear (System.Drawing.Color.White);
            g.TranslateTransform (tx, ty);
            g.ScaleTransform (sx, sy);
            g.DrawLine (pen, x1, y1, x2, y2);
        }

        private static byte[] Pixels (Drawing.Bitmap bitmap)
        {
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            var i = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    pixels[i++] = c.A; pixels[i++] = c.R; pixels[i++] = c.G; pixels[i++] = c.B;
                }
            return pixels;
        }
    }
}
