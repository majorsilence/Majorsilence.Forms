using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Graphics.Transform used to be a stub: the getter handed back a fresh identity matrix and the
    // setter did nothing, as did MultiplyTransform. Nothing threw, so ported drawing code that used the
    // standard save/modify/restore idiom -- read Transform, apply a translation, draw, put the old one
    // back -- simply drew everything at the untransformed origin.
    //
    // The second half of these tests covers a subtler interaction: the clip is emulated with Skia
    // save/restore frames, and Skia's restore pops the MATRIX along with the clip. So replacing a clip
    // silently discarded the world transform. That combination is what an SVG renderer does on every
    // element, and it collapsed each translated <g> group onto the same spot.
    public class GraphicsWorldTransformTests
    {
        private const int Size = 60;

        private static SKBitmap Paint (System.Action<Graphics> draw)
        {
            var bitmap = new SKBitmap (Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);

            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Clear (SKColors.White);
                draw (new Graphics (canvas));
                canvas.Flush ();
            }

            return bitmap;
        }

        private static void FillMarker (Graphics g)
            => g.FillRectangle (new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), new Rectangle (0, 0, 10, 10));

        private static bool IsBlack (SKBitmap bitmap, int x, int y)
            => bitmap.GetPixel (x, y).Red < 64;

        [Fact]
        public void Assigning_Transform_moves_subsequent_drawing ()
        {
            using var matrix = new Matrix ();
            matrix.Translate (20, 20);

            using var bitmap = Paint (g => {
                g.Transform = matrix;
                FillMarker (g);
            });

            Assert.True (IsBlack (bitmap, 25, 25), "the marker should have moved to the translated origin");
            Assert.False (IsBlack (bitmap, 5, 5), "nothing should remain at the untransformed origin");
        }

        [Fact]
        public void Reading_Transform_reports_what_was_applied ()
        {
            using var bitmap = new SKBitmap (Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas (bitmap);
            var g = new Graphics (canvas);

            g.TranslateTransform (12, 34);
            using var read = g.Transform;

            Assert.Equal (12f, read.OffsetX, 3);
            Assert.Equal (34f, read.OffsetY, 3);
        }

        [Fact]
        public void Transform_round_trips_through_save_and_restore ()
        {
            using var bitmap = Paint (g => {
                using var saved = g.Transform;
                g.TranslateTransform (40, 40);
                g.Transform = saved;
                FillMarker (g);
            });

            // Restoring the captured transform must put drawing back at the original origin, not leave
            // the translation in place.
            Assert.True (IsBlack (bitmap, 5, 5));
            Assert.False (IsBlack (bitmap, 45, 45));
        }

        [Fact]
        public void MultiplyTransform_composes_onto_the_current_transform ()
        {
            using var extra = new Matrix ();
            extra.Translate (10, 10);

            using var bitmap = Paint (g => {
                g.TranslateTransform (10, 10);
                g.MultiplyTransform (extra);
                FillMarker (g);
            });

            Assert.True (IsBlack (bitmap, 25, 25), "the two translations should add up");
        }

        [Fact]
        public void ResetTransform_returns_to_the_untransformed_origin ()
        {
            using var bitmap = Paint (g => {
                g.TranslateTransform (30, 30);
                g.ResetTransform ();
                FillMarker (g);
            });

            Assert.True (IsBlack (bitmap, 5, 5));
        }

        [Fact]
        public void Replacing_a_clip_does_not_discard_the_transform ()
        {
            // Two SetClip calls are needed: the first only arms the emulation's baseline, the second is
            // the one that unwinds to it -- and used to pop the matrix on the way.
            var wideOpen = new RectangleF (-100, -100, 400, 400);

            using var bitmap = Paint (g => {
                g.SetClip (wideOpen);
                g.TranslateTransform (20, 20);
                g.SetClip (wideOpen);
                FillMarker (g);
            });

            Assert.True (IsBlack (bitmap, 25, 25), "the translation must survive the clip replacement");
            Assert.False (IsBlack (bitmap, 5, 5));
        }

        [Fact]
        public void ResetClip_does_not_discard_the_transform ()
        {
            using var bitmap = Paint (g => {
                g.SetClip (new RectangleF (-100, -100, 400, 400));
                g.TranslateTransform (20, 20);
                g.ResetClip ();
                FillMarker (g);
            });

            Assert.True (IsBlack (bitmap, 25, 25));
        }

        [Fact]
        public void A_clip_still_clips ()
        {
            // The matrix carry-over must not turn the clip into a no-op.
            using var bitmap = Paint (g => {
                g.SetClip (new RectangleF (0, 0, 4, 4));
                g.FillRectangle (new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), new Rectangle (0, 0, 40, 40));
            });

            Assert.True (IsBlack (bitmap, 1, 1), "inside the clip should be painted");
            Assert.False (IsBlack (bitmap, 20, 20), "outside the clip should not be");
        }

        [Fact]
        public void Parallelogram_DrawImage_honours_an_alpha_scaling_ColorMatrix ()
        {
            // This is how GDI+ callers composite a layer at partial opacity. The attributes used to be
            // dropped, so a fully transparent layer drew as a solid block of its own colour -- which is
            // exactly how an SVG `opacity="0"` spacer rect became a black square.
            using var source = new Majorsilence.Forms.Drawing.Bitmap (10, 10);

            using (var painter = Graphics.FromImage (source))
                painter.FillRectangle (new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), new Rectangle (0, 0, 10, 10));

            using var attributes = new ImageAttributes ();
            attributes.SetColorMatrix (new ColorMatrix { Matrix33 = 0f });

            PointF[] destination = [new PointF (0, 0), new PointF (10, 0), new PointF (0, 10)];

            using var bitmap = Paint (g => g.DrawImage (source, destination,
                new RectangleF (0, 0, 10, 10), Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, attributes));

            Assert.False (IsBlack (bitmap, 5, 5), "a zero-alpha color matrix must draw nothing");
        }

        [Fact]
        public void Parallelogram_DrawImage_without_attributes_still_draws ()
        {
            using var source = new Majorsilence.Forms.Drawing.Bitmap (10, 10);

            using (var painter = Graphics.FromImage (source))
                painter.FillRectangle (new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), new Rectangle (0, 0, 10, 10));

            PointF[] destination = [new PointF (0, 0), new PointF (10, 0), new PointF (0, 10)];

            using var bitmap = Paint (g => g.DrawImage (source, destination));

            Assert.True (IsBlack (bitmap, 5, 5));
        }
    }
}
