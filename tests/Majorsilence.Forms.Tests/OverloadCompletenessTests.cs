using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;

using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using PointF = System.Drawing.PointF;
using Rectangle = System.Drawing.Rectangle;
using RectangleF = System.Drawing.RectangleF;
using SizeF = System.Drawing.SizeF;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Phase 7 of docs/gdi-gap-plan.md: the overload shapes GDI+ exposes that this layer was missing.
    ///
    /// These are compile-shape tests by design — every member delegates to an implementation already
    /// covered elsewhere, so re-asserting the behavior would be duplication. What was actually broken is
    /// that the call did not compile, which is what a migration hits first and what these pin down. The
    /// integer overloads matter most: <c>*.Designer.cs</c> emits integer literals, so their absence
    /// broke exactly the generated files a migration cannot hand-edit.
    /// </summary>
    public class OverloadCompletenessTests
    {
        private static Bitmap NewSurface (int size = 40) => new (size, size);

        [Fact]
        public void Graphics_integer_shape_overloads_are_callable ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var pen = new Pen (Color.Red);
            using var brush = new SolidBrush (Color.Blue);

            // Every one of these is an integer shape a designer file would emit.
            g.DrawArc (pen, 1, 1, 10, 10, 0, 90);
            g.DrawEllipse (pen, 1, 1, 10, 10);
            g.DrawPie (pen, 1, 1, 10, 10, 0, 90);
            g.DrawBezier (pen, 1f, 1f, 2f, 2f, 3f, 3f, 4f, 4f);
            g.FillEllipse (brush, 1, 1, 10, 10);
            g.FillPie (brush, 1, 1, 10, 10, 0, 90);

            Assert.True (surface.GetPixel (5, 5).A > 0, "something should have been drawn");
        }

        [Fact]
        public void Graphics_rectangle_and_fillmode_overloads_are_callable ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var pen = new Pen (Color.Red);
            using var brush = new SolidBrush (Color.Blue);
            PointF[] curve = [new (2, 2), new (20, 6), new (18, 20), new (3, 16)];
            Point[] curveInts = [new (2, 2), new (20, 6), new (18, 20), new (3, 16)];

            g.DrawArc (pen, new RectangleF (1, 1, 10, 10), 0, 90);
            g.DrawPie (pen, new RectangleF (1, 1, 10, 10), 0, 90);
            g.FillPolygon (brush, curve, FillMode.Winding);
            g.FillPolygon (brush, curveInts, FillMode.Winding);
            g.DrawClosedCurve (pen, curveInts, 0.5f, FillMode.Alternate);
            g.FillClosedCurve (brush, curveInts, FillMode.Alternate);
            g.FillClosedCurve (brush, curveInts, FillMode.Alternate, 0.5f);
            g.DrawCurve (pen, curve, 0.5f);
            g.DrawCurve (pen, curve, 0, 2);
            g.DrawCurve (pen, curveInts, 0.5f);
            g.DrawCurve (pen, curveInts, 0, 2, 0.5f);

            Assert.True (surface.GetPixel (10, 10).A > 0);
        }

        [Fact]
        public void Graphics_image_overloads_are_callable ()
        {
            using var source = new Bitmap (10, 10);
            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 10; x++)
                    source.SetPixel (x, y, Color.Red);

            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            g.DrawImage (source, 1f, 1f);
            g.DrawImage (source, new PointF (2, 2));
            g.DrawImage (source, new Point (3, 3));
            g.DrawImage (source, 0, 0, new Rectangle (0, 0, 5, 5), GraphicsUnit.Pixel);
            g.DrawImage (source, new Rectangle (0, 0, 8, 8), 0, 0, 5, 5, GraphicsUnit.Pixel);
            g.DrawImage (source, new Rectangle (0, 0, 8, 8), 0f, 0f, 5f, 5f, GraphicsUnit.Pixel);
            g.DrawImageUnscaled (source, new Point (1, 1));
            g.DrawImageUnscaled (source, 1, 1, 5, 5);

            Assert.True (surface.GetPixel (4, 4).A > 0);
        }

        [Fact]
        public void DrawImage_into_a_parallelogram_applies_the_implied_transform ()
        {
            // The three-point overload is not just a bounding-box draw: a mirrored destination has to
            // actually flip the image.
            using var source = new Bitmap (10, 10);
            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 10; x++)
                    source.SetPixel (x, y, x < 5 ? Color.Red : Color.Blue);

            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            // Upper-left, upper-right, lower-left — with left/right swapped, so the halves mirror.
            g.DrawImage (source, [new PointF (10, 0), new PointF (0, 0), new PointF (10, 10)]);

            Assert.Equal (Color.Blue.ToArgb (), surface.GetPixel (2, 5).ToArgb ());
            Assert.Equal (Color.Red.ToArgb (), surface.GetPixel (8, 5).ToArgb ());
        }

        [Fact]
        public void Graphics_clip_and_visibility_overloads_are_callable ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var region = new Region (new Rectangle (0, 0, 20, 20));
            using var path = new GraphicsPath ();
            path.AddRectangle (new Rectangle (0, 0, 20, 20));

            g.SetClip (new Rectangle (0, 0, 30, 30), CombineMode.Replace);
            g.SetClip (new RectangleF (0, 0, 30, 30), CombineMode.Replace);
            g.SetClip (path, CombineMode.Replace);
            g.SetClip (region, CombineMode.Replace);
            g.IntersectClip (region);

            using var other = Graphics.FromImage (surface);
            g.SetClip (other);
            g.SetClip (other, CombineMode.Replace);

            Assert.True (g.IsVisible (new PointF (1, 1)));
            Assert.True (g.IsVisible (1f, 1f));
            Assert.True (g.IsVisible (1, 1));
            Assert.True (g.IsVisible (1f, 1f, 2f, 2f));
            Assert.True (g.IsVisible (1, 1, 2, 2));
        }

        [Fact]
        public void Graphics_transform_and_measure_overloads_are_callable ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var font = new Font ("Arial", 10f);

            g.ScaleTransform (1f, 1f, MatrixOrder.Prepend);
            g.TranslateTransform (0f, 0f, MatrixOrder.Append);

            var sized = g.MeasureString ("hello", font, 100);
            var atOrigin = g.MeasureString ("hello", font, new PointF (5, 5), null);
            var detailed = g.MeasureString ("hello", font, new SizeF (100, 50), null, out var chars, out var lines);

            Assert.True (sized.Width > 0);
            Assert.True (atOrigin.Width > 0);
            Assert.True (detailed.Width > 0);
            Assert.Equal (5, chars);
            Assert.True (lines >= 1);
        }

        [Fact]
        public void Region_graphics_taking_overloads_bind_a_Graphics_argument ()
        {
            // The whole point of the object?-typed parameter: this is what migrated code writes.
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var region = new Region (new Rectangle (0, 0, 20, 20));

            Assert.True (region.IsVisible (new PointF (5, 5), g));
            Assert.True (region.IsVisible (new Point (5, 5), g));
            Assert.True (region.IsVisible (5f, 5f, g));
            Assert.True (region.IsVisible (5, 5, g));
            Assert.True (region.IsVisible (new RectangleF (1, 1, 4, 4), g));
            Assert.True (region.IsVisible (new Rectangle (1, 1, 4, 4), g));
            Assert.True (region.IsVisible (1f, 1f, 4f, 4f, g));
            Assert.True (region.IsVisible (1, 1, 4, 4, g));
            Assert.False (region.IsEmpty (g));
            Assert.False (region.IsInfinite (g));
            Assert.Equal (new RectangleF (0, 0, 20, 20), region.GetBounds (g));
        }

        [Fact]
        public void GraphicsPath_graphics_taking_overloads_bind_a_Graphics_argument ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var path = new GraphicsPath ();
            path.AddRectangle (new Rectangle (0, 0, 20, 20));
            using var pen = new Pen (Color.Black, 4f);

            Assert.True (path.IsVisible (new PointF (5, 5), g));
            Assert.True (path.IsVisible (new Point (5, 5), g));
            Assert.True (path.IsVisible (5f, 5f, g));
            Assert.True (path.IsVisible (5, 5, g));
            Assert.True (path.IsOutlineVisible (new PointF (0, 10), pen, g));
            Assert.True (path.IsOutlineVisible (new Point (0, 10), pen, g));
            Assert.True (path.IsOutlineVisible (0f, 10f, pen, g));
            Assert.True (path.IsOutlineVisible (0, 10, pen, g));
        }

        [Fact]
        public void GraphicsPath_integer_overloads_produce_the_same_geometry_as_the_float_ones ()
        {
            using var ints = new GraphicsPath ();
            using var floats = new GraphicsPath ();

            ints.AddLine (0, 0, 10, 10);
            ints.AddEllipse (0, 0, 20, 20);
            ints.AddArc (0, 0, 20, 20, 0f, 90f);
            floats.AddLine (0f, 0f, 10f, 10f);
            floats.AddEllipse (0f, 0f, 20f, 20f);
            floats.AddArc (0f, 0f, 20f, 20f, 0f, 90f);

            Assert.Equal (floats.PointCount, ints.PointCount);
            Assert.Equal (floats.GetBounds (), ints.GetBounds ());
        }

        [Fact]
        public void GraphicsPath_widen_and_bounds_overloads_are_callable ()
        {
            using var path = new GraphicsPath ();
            path.AddLine (0, 10, 40, 10);
            using var pen = new Pen (Color.Black, 6f);
            using var matrix = new Matrix ();

            var plain = path.GetBounds ();
            var withPen = path.GetBounds (null, pen);
            Assert.True (withPen.Height > plain.Height, "the pen width should widen the reported bounds");

            path.Widen (pen, matrix);
            Assert.True (path.IsVisible (20f, 10f), "a widened line becomes a hit-testable area");

            using var second = new GraphicsPath ();
            second.AddLine (0, 10, 40, 10);
            second.Widen (pen, matrix, 0.5f);
            Assert.True (second.PointCount > 0);
        }

        [Fact]
        public void Region_GetRegionScans_returns_the_covering_rectangles ()
        {
            using var region = new Region (new Rectangle (5, 5, 10, 10));

            var scans = region.GetRegionScans (null);

            Assert.NotEmpty (scans);
            Assert.Equal (new RectangleF (5, 5, 10, 10), scans[0]);

            using var scaled = new Matrix ();
            scaled.Scale (2f, 2f);
            Assert.Equal (new RectangleF (10, 10, 20, 20), region.GetRegionScans (scaled)[0]);
        }

        [Fact]
        public void Bitmap_and_Matrix_overloads_are_callable ()
        {
            using var source = new Bitmap (20, 20);
            source.SetPixel (5, 5, Color.Red);

            using var cropped = source.Clone (new Rectangle (0, 0, 10, 10), Majorsilence.Forms.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Assert.Equal (10, cropped.Width);
            Assert.Equal (Color.Red.ToArgb (), cropped.GetPixel (5, 5).ToArgb ());

            using var croppedF = source.Clone (new RectangleF (0, 0, 10, 10), Majorsilence.Forms.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Assert.Equal (10, croppedF.Width);

            using var matrix = new Matrix ();
            matrix.Scale (2f, 2f);
            var vectors = new[] { new Point (3, 4) };
            matrix.TransformVectors (vectors);
            Assert.Equal (new Point (6, 8), vectors[0]);
        }

        [Fact]
        public void Font_GetHeight_accepts_a_Graphics ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var font = new Font ("Arial", 12f);

            Assert.Equal (font.GetHeight (), font.GetHeight (g), 3);
        }
    }
}
