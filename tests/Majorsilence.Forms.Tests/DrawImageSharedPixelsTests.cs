using System;
using SkiaSharp;
using Xunit;
using MSBitmap = Majorsilence.Forms.Drawing.Bitmap;
using MSGraphics = Majorsilence.Forms.Drawing.Graphics;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Tests
{
    // On a Graphics made by FromImage, DrawImage reads the source bitmap's pixels in place instead of copying them first
    // (performance plan F18). The pixels drawn must be the same: the oracle is the copying route, a Graphics over a plain
    // canvas, compared byte for byte.
    public class DrawImageSharedPixelsTests
    {
        private static MSBitmap Source ()
        {
            var bitmap = new MSBitmap (37, 23);
            for (var y = 0; y < 23; y++)
                for (var x = 0; x < 37; x++)
                    bitmap.SetPixel (x, y, System.Drawing.Color.FromArgb (40 + x * 5, x * 7, y * 11, (x * y) % 255));
            return bitmap;
        }

        [Theory]
        [InlineData (Drawing.Drawing2D.InterpolationMode.Default)]
        [InlineData (Drawing.Drawing2D.InterpolationMode.NearestNeighbor)]
        [InlineData (Drawing.Drawing2D.InterpolationMode.HighQualityBicubic)]
        public void Drawing_from_shared_pixels_matches_drawing_from_a_copy (Drawing.Drawing2D.InterpolationMode mode)
        {
            using var source = Source ();
            using var shared = new MSBitmap (120, 90);
            using var copied = new MSBitmap (120, 90);

            using (var g = MSGraphics.FromImage (shared))
                Draw (g, source, mode);

            using (var canvas = new SKCanvas (copied.GetSKBitmap ()!))
            using (var g = new MSGraphics (canvas))
                Draw (g, source, mode);

            Assert.True (shared.GetSKBitmap ()!.Bytes.AsSpan ().SequenceEqual (copied.GetSKBitmap ()!.Bytes));
        }

        private static void Draw (MSGraphics g, MSBitmap source, Drawing.Drawing2D.InterpolationMode mode)
        {
            g.Clear (System.Drawing.Color.White);
            g.InterpolationMode = mode;
            g.DrawImage (source, 3, 4);
            g.DrawImage (source, new Rectangle (10, 30, 80, 50));
            g.DrawImage (source, new Rectangle (50, 2, 30, 20), new Rectangle (5, 3, 20, 15), Drawing.GraphicsUnit.Pixel);

            // A source rectangle past the image takes the tiled route.
            using var attributes = new Drawing.Imaging.ImageAttributes ();
            attributes.SetWrapMode (Drawing.Drawing2D.WrapMode.TileFlipXY);
            g.DrawImage (source, new Rectangle (0, 60, 110, 30), -10, -5, 70, 40, Drawing.GraphicsUnit.Pixel, attributes);
        }

        // Drawing an image onto itself still reads the pixels from before the draw, through every overload that now
        // reads an image's bitmap in place.
        [Fact]
        public void An_image_drawn_onto_itself_reads_its_old_pixels ()
        {
            var draws = new (string Name, Action<MSGraphics, MSBitmap> Draw)[] {
                ("at a point", (g, i) => g.DrawImage (i, 0, 1)),
                ("into a rectangle", (g, i) => g.DrawImage (i, new Rectangle (0, 1, i.Width, i.Height))),
                ("a source rectangle", (g, i) => g.DrawImage (i, new System.Drawing.RectangleF (0, 1, i.Width, i.Height), new System.Drawing.RectangleF (0, 0, i.Width, i.Height), Drawing.GraphicsUnit.Pixel)),
            };

            foreach (var (name, draw) in draws) {
                using var image = Source ();
                using var before = image.GetSKBitmap ()!.Copy ();

                using (var g = MSGraphics.FromImage (image))
                    draw (g, image);

                using var expected = before.Copy ();
                using (var canvas = new SKCanvas (expected))
                    canvas.DrawBitmap (before, 0, 1);

                Assert.True (expected.Bytes.AsSpan ().SequenceEqual (image.GetSKBitmap ()!.Bytes), name);
            }
        }

        // Against Skia directly, as DrawImage drew before: a copy of the bitmap, then an image of that, linear sampling.
        [Theory]
        [InlineData (Drawing.Imaging.PixelFormat.Format32bppArgb)]
        [InlineData (Drawing.Imaging.PixelFormat.Format32bppPArgb)]
        [InlineData (Drawing.Imaging.PixelFormat.Format24bppRgb)]
        [InlineData (Drawing.Imaging.PixelFormat.Format16bppRgb565)]
        public void Both_routes_draw_what_a_copied_image_drew (Drawing.Imaging.PixelFormat format)
        {
            using var source = new MSBitmap (37, 23, format);
            for (var y = 0; y < 23; y++)
                for (var x = 0; x < 37; x++)
                    source.SetPixel (x, y, System.Drawing.Color.FromArgb (40 + x * 5, x * 7, y * 11, (x * y) % 255));

            using var fromImage = new MSBitmap (120, 90);
            // The same pixel layout as the bitmaps compared with: a bare SKBitmap takes the platform's colour type.
            using var expected = new SKBitmap (fromImage.GetSKBitmap ()!.Info);
            expected.Erase (SKColors.White);
            using (var canvas = new SKCanvas (expected))
            using (var copy = source.GetSKBitmap ()!.Copy ())
            using (var image = SKImage.FromBitmap (copy))
                canvas.DrawImage (image, new SKRect (0, 0, 37, 23), new SKRect (10, 30, 90, 80), new SKSamplingOptions (SKFilterMode.Linear, SKMipmapMode.None));

            using (var g = MSGraphics.FromImage (fromImage)) {
                g.Clear (System.Drawing.Color.White);
                g.DrawImage (source, new Rectangle (10, 30, 80, 50));
            }

            using var onCanvas = new MSBitmap (120, 90);
            using (var canvas = new SKCanvas (onCanvas.GetSKBitmap ()!))
            using (var g = new MSGraphics (canvas)) {
                g.Clear (System.Drawing.Color.White);
                g.DrawImage (source, new Rectangle (10, 30, 80, 50));
            }

            Assert.True (expected.Bytes.AsSpan ().SequenceEqual (fromImage.GetSKBitmap ()!.Bytes), "FromImage");
            Assert.True (expected.Bytes.AsSpan ().SequenceEqual (onCanvas.GetSKBitmap ()!.Bytes), "canvas");
        }

        // The source image is left as it was.
        [Fact]
        public void Drawing_an_image_leaves_it_unchanged ()
        {
            using var source = Source ();
            using var before = source.GetSKBitmap ()!.Copy ();
            using var target = new MSBitmap (120, 90);

            using (var g = MSGraphics.FromImage (target))
                Draw (g, source, Drawing.Drawing2D.InterpolationMode.Default);

            Assert.True (before.Bytes.AsSpan ().SequenceEqual (source.GetSKBitmap ()!.Bytes));
        }
    }
}
