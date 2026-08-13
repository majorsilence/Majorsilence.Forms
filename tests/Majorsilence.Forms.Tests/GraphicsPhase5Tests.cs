using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;

// Bitmap/Font/Image come from GlobalDrawingAliases.cs. The rest are ambiguous against the referenced
// real System.Drawing.Common, so they are pinned here.
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using PointF = System.Drawing.PointF;
using Rectangle = System.Drawing.Rectangle;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the <c>Graphics</c> members added in Phase 5 of docs/gdi-gap-plan.md, plus two fidelity
    /// fixes made in the same pass: <c>DrawPath</c> and <c>FillPath</c> previously replayed the path as
    /// a polyline built from <c>PathPoints</c>, which discarded every curve and every pen setting.
    /// </summary>
    public class GraphicsPhase5Tests
    {
        private static Bitmap NewSurface (int size = 60) => new (size, size);

        private static int CountPixels (Bitmap bitmap, Func<Color, bool> predicate)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (predicate (bitmap.GetPixel (x, y)))
                        count++;
            return count;
        }

        // ---- DrawPath / FillPath fidelity ----

        [Fact]
        public void DrawPath_honors_the_pen_dash_pattern ()
        {
            // The old hand-rolled SKPaint ignored DashStyle entirely, so a dashed pen drew a solid line.
            using var solidSurface = NewSurface ();
            using var dashedSurface = NewSurface ();

            foreach (var (surface, dashed) in new[] { (solidSurface, false), (dashedSurface, true) }) {
                using var g = Graphics.FromImage (surface);
                using var pen = new Pen (Color.Red, 3f);
                if (dashed)
                    pen.DashStyle = DashStyle.Dash;
                using var path = new GraphicsPath ();
                path.AddLine (2, 30, 58, 30);
                g.DrawPath (pen, path);
            }

            var solid = CountPixels (solidSurface, c => c.A > 0);
            var dashes = CountPixels (dashedSurface, c => c.A > 0);

            Assert.True (dashes > 0, "the dashed path should still draw something");
            Assert.True (dashes < solid, $"a dashed pen should paint fewer pixels than a solid one ({dashes} vs {solid})");
        }

        [Fact]
        public void FillPath_honors_the_paths_fill_mode ()
        {
            // Alternate vs Winding differ only where a path self-intersects, which the old code could
            // not express: it forced a single default fill type. This must be a true pentagram (five
            // outer points joined in skip-one order); the more obvious ten-point outer/inner star is a
            // plain concave polygon that never crosses itself, so both modes would agree.
            PointF[] star = [
                new (30, 4), new (45.3f, 51), new (5.3f, 22), new (54.7f, 22), new (14.7f, 51),
            ];

            int FilledCount (FillMode mode)
            {
                using var surface = NewSurface ();
                using var g = Graphics.FromImage (surface);
                using var path = new GraphicsPath (mode);
                path.AddPolygon (star);
                using var brush = new SolidBrush (Color.Red);
                g.FillPath (brush, path);
                return CountPixels (surface, c => c.A > 0);
            }

            var alternate = FilledCount (FillMode.Alternate);
            var winding = FilledCount (FillMode.Winding);

            Assert.True (alternate > 0 && winding > 0);
            Assert.True (winding > alternate,
                $"winding fills the star's center that alternate leaves hollow ({winding} vs {alternate})");
        }

        [Fact]
        public void FillPath_preserves_curves_rather_than_chording_them ()
        {
            // A polyline through the ellipse's Bezier control points bulges outside the true curve, so
            // the corner pixel is a clean discriminator: it must stay empty for a real ellipse.
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var path = new GraphicsPath ();
            path.AddEllipse (new RectangleF (2, 2, 56, 56));
            using var brush = new SolidBrush (Color.Red);

            g.FillPath (brush, path);

            Assert.True (surface.GetPixel (30, 30).A > 0, "the ellipse center should be filled");
            Assert.Equal (0, surface.GetPixel (3, 3).A);
        }

        [Fact]
        public void A_text_outline_can_be_filled_as_geometry ()
        {
            // The point of AddString: text becomes a path you can fill with any brush.
            using var surface = NewSurface (120);
            using var g = Graphics.FromImage (surface);
            using var family = new FontFamily ("Arial");
            using var path = new GraphicsPath ();
            path.AddString ("ABC", family, (int)FontStyle.Bold, 40f, new PointF (4, 10), null);
            using var brush = new SolidBrush (Color.Red);

            g.FillPath (brush, path);

            Assert.True (CountPixels (surface, c => c.A > 0) > 50, "filling the glyph outline should paint pixels");
        }

        // ---- Region / curve drawing ----

        [Fact]
        public void FillRegion_fills_the_region ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            using var region = new Region (new Rectangle (10, 10, 20, 20));
            using var brush = new SolidBrush (Color.Red);

            g.FillRegion (brush, region);

            Assert.True (surface.GetPixel (20, 20).A > 0, "inside the region should be filled");
            Assert.Equal (0, surface.GetPixel (50, 50).A);
        }

        [Fact]
        public void FillClosedCurve_and_DrawClosedCurve_paint ()
        {
            PointF[] points = [new (10, 10), new (50, 15), new (45, 50), new (12, 45)];

            using var filled = NewSurface ();
            using (var g = Graphics.FromImage (filled)) {
                using var brush = new SolidBrush (Color.Red);
                g.FillClosedCurve (brush, points);
            }

            using var outlined = NewSurface ();
            using (var g = Graphics.FromImage (outlined)) {
                using var pen = new Pen (Color.Blue, 2f);
                g.DrawClosedCurve (pen, points);
            }

            Assert.True (CountPixels (filled, c => c.A > 0) > 100, "the closed curve should fill an area");
            Assert.True (CountPixels (outlined, c => c.A > 0) > 0, "the closed curve should draw an outline");
            Assert.True (CountPixels (filled, c => c.A > 0) > CountPixels (outlined, c => c.A > 0),
                "a filled shape covers more than its outline");
        }

        [Fact]
        public void DrawImageUnscaledAndClipped_clips_without_scaling ()
        {
            using var source = new Bitmap (40, 40);
            for (var y = 0; y < 40; y++)
                for (var x = 0; x < 40; x++)
                    source.SetPixel (x, y, Color.Red);

            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            g.DrawImageUnscaledAndClipped (source, new Rectangle (0, 0, 10, 10));

            Assert.True (surface.GetPixel (5, 5).A > 0, "inside the clip should be drawn");
            Assert.Equal (0, surface.GetPixel (20, 20).A);   // clipped away, not scaled down
        }

        // ---- Clip / transform / color ----

        [Fact]
        public void IsClipEmpty_reports_an_empty_clip ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            Assert.False (g.IsClipEmpty);

            g.SetClip (new Rectangle (0, 0, 0, 0));
            Assert.True (g.IsClipEmpty);
        }

        [Fact]
        public void SetClip_replaces_the_current_clip_rather_than_intersecting_it ()
        {
            // Skia's clip only ever narrows, so a SetClip built on ClipRect could never widen again:
            // the first narrow clip in a paint silently discarded everything drawn after it.
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 10, 10));
            g.SetClip (new Rectangle (0, 0, 60, 60));
            g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (40, 40, 10, 10));

            Assert.True (surface.GetPixel (45, 45).A > 0, "drawing after widening the clip should appear");
        }

        [Fact]
        public void ResetClip_lifts_a_previously_applied_clip ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 10, 10));
            g.ResetClip ();
            g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (40, 40, 10, 10));

            Assert.True (surface.GetPixel (45, 45).A > 0, "drawing after ResetClip should appear");
        }

        [Fact]
        public void IntersectClip_narrows_the_clip_instead_of_replacing_it ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 30, 30));
            g.IntersectClip (new Rectangle (20, 20, 30, 30));
            g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (0, 0, 60, 60));

            Assert.True (surface.GetPixel (25, 25).A > 0, "the overlap of both rectangles should be drawn");
            Assert.Equal (0, surface.GetPixel (5, 5).A);      // only in the first rectangle
            Assert.Equal (0, surface.GetPixel (45, 45).A);    // only in the second
        }

        [Fact]
        public void ClipBounds_reports_the_current_clip_on_a_bitmap_backed_Graphics ()
        {
            // ClipBounds used to report the control's bounds, and Empty when there was no control --
            // which is the double-buffered case. Custom painting saves and restores the clip through
            // this property, so an empty answer clipped the rest of the paint away.
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            Assert.Equal (new RectangleF (0, 0, 60, 60), g.ClipBounds);

            g.SetClip (new Rectangle (10, 10, 20, 20));
            Assert.Equal (new RectangleF (10, 10, 20, 20), g.ClipBounds);
        }

        [Fact]
        public void The_save_and_restore_clip_idiom_round_trips ()
        {
            // var last = g.ClipBounds; g.SetClip (part); ...; g.SetClip (last);
            // This is how RibbonWinForms -- and custom painting generally -- scopes a clip.
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            var last = g.ClipBounds;
            g.SetClip (new Rectangle (0, 0, 10, 10));
            g.SetClip (last);
            g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (40, 40, 10, 10));

            Assert.True (surface.GetPixel (45, 45).A > 0, "restoring the saved clip should restore drawing");
        }

        [Fact]
        public void TranslateClip_moves_the_clip_without_moving_what_is_drawn ()
        {
            // It used to translate the canvas, which shifted every later drawing operation as well.
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            g.SetClip (new Rectangle (0, 0, 20, 20));
            g.TranslateClip (20, 20);
            g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (0, 0, 60, 60));

            Assert.True (surface.GetPixel (25, 25).A > 0, "the moved clip should let the fill through");
            Assert.Equal (0, surface.GetPixel (5, 5).A);   // where the clip used to be
        }

        [Fact]
        public void GetNearestColor_returns_the_color_unchanged_on_a_32bpp_surface ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            var color = Color.FromArgb (200, 12, 34, 56);

            Assert.Equal (color, g.GetNearestColor (color));
        }

        [Fact]
        public void TransformPoints_applies_the_canvas_transform_going_to_device_space ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            g.TranslateTransform (10f, 20f);

            var points = new[] { new PointF (1f, 2f) };
            g.TransformPoints (CoordinateSpace.Device, CoordinateSpace.World, points);

            Assert.Equal (11f, points[0].X, 3);
            Assert.Equal (22f, points[0].Y, 3);
        }

        [Fact]
        public void TransformPoints_is_the_identity_between_world_and_page ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);
            g.TranslateTransform (10f, 20f);

            var points = new[] { new Point (1, 2) };
            g.TransformPoints (CoordinateSpace.Page, CoordinateSpace.World, points);

            Assert.Equal (new Point (1, 2), points[0]);
        }

        [Fact]
        public void RenderingOrigin_and_TextContrast_round_trip ()
        {
            using var surface = NewSurface ();
            using var g = Graphics.FromImage (surface);

            Assert.Equal (4, g.TextContrast);   // GDI+'s default

            g.RenderingOrigin = new Point (3, 4);
            g.TextContrast = 9;
            g.Flush ();                          // must not throw

            Assert.Equal (new Point (3, 4), g.RenderingOrigin);
            Assert.Equal (9, g.TextContrast);
        }
    }
}
