using System;
using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Imaging;
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
    /// Covers plan item W5.18: the fidelity a <c>Graphics</c> stroke, clip or image draw used to throw
    /// away. Every test here renders into an off-screen <c>Bitmap</c> through a real
    /// <c>Graphics.FromImage</c> (no control and no window server involved) and asserts a
    /// relationship between two renders (dashed vs solid, clipped vs unclipped, attributes vs none)
    /// rather than a pixel count, so the assertions survive a rasteriser change.
    /// </summary>
    public class PenAndClipFidelityTests
    {
        private const int Size = 100;

        private static Bitmap NewSurface (int size = Size)
        {
            var surface = new Bitmap (size, size);
            // The surface is the measuring instrument: a zero-sized one would make every "nothing was
            // painted here" assertion below pass without drawing anything at all.
            Assert.Equal (size, surface.Width);
            Assert.Equal (size, surface.Height);
            return surface;
        }

        private static int Inked (Bitmap bitmap)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y).A > 0)
                        count++;
            return count;
        }

        /// <summary>Whether any pixel in column <paramref name="x"/>, rows <paramref name="from"/>..<paramref name="to"/>, is painted.</summary>
        private static bool ColumnInked (Bitmap bitmap, int x, int from, int to)
        {
            for (var y = from; y <= to; y++)
                if (bitmap.GetPixel (x, y).A > 0)
                    return true;
            return false;
        }

        private static Bitmap Render (Action<Graphics> draw, int size = Size)
        {
            var surface = NewSurface (size);
            using (var g = Graphics.FromImage (surface))
                draw (g);
            return surface;
        }

        // ---- GFX-23: every simple stroke call honours the whole pen ----

        [Fact]
        public void DrawLine_with_a_dashed_pen_leaves_gaps_where_a_solid_pen_leaves_none ()
        {
            // The honest statement of "the dash applies": the stroked span is interrupted. A pixel
            // count would just pin today's dash period.
            using var solid = Render (g => {
                using var pen = new Pen (Color.Red, 3f);
                g.DrawLine (pen, 5, 50, 95, 50);
            });
            using var dashed = Render (g => {
                using var pen = new Pen (Color.Red, 3f) { DashStyle = DashStyle.Dash };
                g.DrawLine (pen, 5, 50, 95, 50);
            });

            var solidColumns = Enumerable.Range (5, 90).Where (x => ColumnInked (solid, x, 45, 55)).ToList ();
            var dashedColumns = Enumerable.Range (5, 90).Where (x => ColumnInked (dashed, x, 45, 55)).ToList ();

            Assert.Equal (90, solidColumns.Count);      // a solid pen paints the whole span
            Assert.NotEmpty (dashedColumns);            // the dashed pen still paints
            Assert.True (dashedColumns.Count < solidColumns.Count,
                $"a dashed line must leave gaps along the span ({dashedColumns.Count} of 90 columns inked)");
        }

        public static TheoryData<string> StrokeCalls () => new () {
            "DrawLine", "DrawLines", "DrawRectangle", "DrawRectangles", "DrawEllipse", "DrawArc",
            "DrawPie", "DrawBezier", "DrawBeziers", "DrawCurve", "DrawPolygon",
            // GUARD, not proof: DrawPath is the one stroke call that already built its paint from the
            // pen, so no previous version of this file could fail this case -- it is here to keep the
            // sweep honest, i.e. to catch a future "optimisation" that routes DrawPath back through a
            // partial paint. Every other case in this list fails against the pre-W5.18 code.
            "DrawPath",
        };

        [Theory]
        [MemberData (nameof (StrokeCalls))]
        public void Every_stroke_call_honours_the_dash_not_just_DrawPath (string call)
        {
            // GFX-23 was that DrawPath -- the one method built on Pen.CreatePaint -- honoured the pen
            // while the other fourteen call sites went through a pooled paint carrying colour, style and
            // width only. Sweeping every call site is the point: fixing one and leaving the rest is how
            // the two paths drifted apart in the first place.
            static void Stroke (Graphics g, Pen pen, string call)
            {
                PointF[] line = [new (5, 20), new (40, 20), new (75, 20), new (95, 20)];
                switch (call) {
                case "DrawLine": g.DrawLine (pen, 5, 50, 95, 50); break;
                case "DrawLines": g.DrawLines (pen, line); break;
                case "DrawRectangle": g.DrawRectangle (pen, new Rectangle (5, 5, 90, 90)); break;
                case "DrawRectangles": g.DrawRectangles (pen, [new Rectangle (5, 5, 90, 90)]); break;
                case "DrawEllipse": g.DrawEllipse (pen, new Rectangle (5, 5, 90, 90)); break;
                case "DrawArc": g.DrawArc (pen, new Rectangle (5, 5, 90, 90), 0f, 270f); break;
                case "DrawPie": g.DrawPie (pen, new Rectangle (5, 5, 90, 90), 0f, 270f); break;
                case "DrawBezier": g.DrawBezier (pen, new PointF (5, 90), new PointF (5, 5),
                    new PointF (95, 5), new PointF (95, 90)); break;
                case "DrawBeziers": g.DrawBeziers (pen, [new PointF (5, 90), new PointF (5, 5),
                    new PointF (95, 5), new PointF (95, 90)]); break;
                case "DrawCurve": g.DrawCurve (pen, line); break;
                case "DrawPolygon": g.DrawPolygon (pen, [new PointF (5, 5), new PointF (95, 5),
                    new PointF (50, 95)]); break;
                case "DrawPath": {
                    using var path = new GraphicsPath ();
                    path.AddEllipse (5, 5, 90, 90);
                    g.DrawPath (pen, path);
                    break;
                }
                default: throw new ArgumentOutOfRangeException (nameof (call), call, "unknown stroke call");
                }
            }

            using var solidSurface = Render (g => {
                using var pen = new Pen (Color.Red, 3f);
                Stroke (g, pen, call);
            });
            using var dashedSurface = Render (g => {
                using var pen = new Pen (Color.Red, 3f) { DashStyle = DashStyle.Dot };
                Stroke (g, pen, call);
            });

            var solid = Inked (solidSurface);
            var dashed = Inked (dashedSurface);

            Assert.True (solid > 0, $"{call} painted nothing with a solid pen -- the test measures nothing");
            Assert.True (dashed > 0, $"{call} painted nothing with a dotted pen");
            Assert.True (dashed < solid, $"{call} ignored the pen's dash ({dashed} pixels vs {solid} solid)");
        }

        [Fact]
        public void A_polyline_takes_the_pens_line_join_at_its_vertices ()
        {
            // A mitred join runs to a point beyond the vertex; a round one stops a half-width away. So
            // the tip zone above the apex is painted by one and not the other -- which also proves the
            // polyline is stroked as one path, since a per-segment DrawLine has no joins to shape.
            PointF[] v = [new (10, 60), new (50, 12), new (90, 60)];

            using var mitred = Render (g => {
                using var pen = new Pen (Color.Red, 12f) { LineJoin = LineJoin.Miter };
                g.DrawLines (pen, v);
            });
            using var rounded = Render (g => {
                using var pen = new Pen (Color.Red, 12f) { LineJoin = LineJoin.Round };
                g.DrawLines (pen, v);
            });

            var mitredTip = CountRows (mitred, 0, 4);
            var roundedTip = CountRows (rounded, 0, 4);

            Assert.True (Inked (rounded) > 0, "the round-joined polyline painted nothing");
            Assert.True (mitredTip > 0, "a mitred join should run past the vertex into the tip zone");
            Assert.Equal (0, roundedTip);
        }

        [Fact]
        public void MiterLimit_bevels_a_join_the_limit_forbids ()
        {
            // Same sharp apex, both mitred: with the default limit of 10 the spike is kept, with a limit
            // of 1 it must be cut back to a bevel. Skia's own default StrokeMiter is 4, so a pooled
            // paint that never assigned it answered "spike" for both.
            PointF[] v = [new (10, 60), new (50, 12), new (90, 60)];

            using var generous = Render (g => {
                using var pen = new Pen (Color.Red, 12f) { MiterLimit = 10f };
                g.DrawLines (pen, v);
            });
            using var strict = Render (g => {
                using var pen = new Pen (Color.Red, 12f) { MiterLimit = 1f };
                g.DrawLines (pen, v);
            });

            Assert.True (Inked (strict) > 0, "the bevelled polyline painted nothing");
            Assert.True (CountRows (generous, 0, 4) > 0, "MiterLimit 10 should keep the spike");
            Assert.Equal (0, CountRows (strict, 0, 4));
        }

        [Fact]
        public void A_pen_built_from_a_gradient_brush_strokes_with_the_gradient ()
        {
            using var flat = Render (g => {
                using var pen = new Pen (Color.Red, 10f);
                g.DrawLine (pen, 5, 50, 95, 50);
            });
            using var gradient = Render (g => {
                using var brush = new LinearGradientBrush (
                    new RectangleF (0, 0, Size, Size), Color.Red, Color.Blue);
                using var pen = new Pen (brush, 10f);
                g.DrawLine (pen, 5, 50, 95, 50);
            });

            Assert.Single (DistinctColors (flat));
            Assert.True (DistinctColors (gradient).Count > 2,
                "a gradient-brush pen should stroke with the brush's shader, not with one flat colour");
        }

        // ---- GFX-07: SmoothingMode.Default does not antialias ----

        [Fact]
        public void A_fill_is_hard_edged_by_default_and_soft_only_when_asked ()
        {
            PointF[] triangle = [new (10, 10), new (90, 30), new (30, 90)];

            using var byDefault = Render (g => {
                using var brush = new SolidBrush (Color.Red);
                g.FillPolygon (brush, triangle);
            });
            using var antialiased = Render (g => {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush (Color.Red);
                g.FillPolygon (brush, triangle);
            });

            Assert.True (Inked (byDefault) > 0, "the default-mode fill painted nothing");
            Assert.Equal (0, PartiallyTransparent (byDefault));
            Assert.True (PartiallyTransparent (antialiased) > 0,
                "SmoothingMode.AntiAlias should soften the diagonal edges");
        }

        [Fact]
        public void HighSpeed_smoothing_does_not_antialias_but_HighQuality_does ()
        {
            // The old test was `SmoothingMode != None`, and the enum's values are
            // Default=0, HighSpeed=1, HighQuality=2, None=3, AntiAlias=4 -- so everything except None
            // antialiased, including the two modes GDI+ maps to hard edges.
            PointF[] triangle = [new (10, 10), new (90, 30), new (30, 90)];

            using var highSpeed = Render (g => {
                g.SmoothingMode = SmoothingMode.HighSpeed;
                using var brush = new SolidBrush (Color.Red);
                g.FillPolygon (brush, triangle);
            });
            using var highQuality = Render (g => {
                g.SmoothingMode = SmoothingMode.HighQuality;
                using var brush = new SolidBrush (Color.Red);
                g.FillPolygon (brush, triangle);
            });

            Assert.True (Inked (highSpeed) > 0, "the HighSpeed fill painted nothing");
            Assert.Equal (0, PartiallyTransparent (highSpeed));
            Assert.True (PartiallyTransparent (highQuality) > 0, "HighQuality should antialias");
        }

        [Fact]
        public void A_stroke_follows_SmoothingMode_the_same_way_a_fill_does ()
        {
            // The stroke paint never set IsAntialias at all, so SmoothingMode had the opposite failure
            // on the stroke side: a diagonal outline stayed hard however high the quality was set.
            using var byDefault = Render (g => {
                using var pen = new Pen (Color.Red, 3f);
                g.DrawLine (pen, 10, 10, 90, 60);
            });
            using var antialiased = Render (g => {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen (Color.Red, 3f);
                g.DrawLine (pen, 10, 10, 90, 60);
            });

            Assert.True (Inked (byDefault) > 0, "the default-mode stroke painted nothing");
            Assert.Equal (0, PartiallyTransparent (byDefault));
            Assert.True (PartiallyTransparent (antialiased) > 0,
                "an antialiased diagonal stroke should have soft edges");
        }

        // ---- GFX-12: IntersectClip(Region) can only narrow ----

        [Fact]
        public void IntersectClip_with_a_region_narrows_and_never_widens ()
        {
            using var surface = Render (g => {
                g.SetClip (new Rectangle (0, 0, 20, 20));
                using var wider = new Region (new Rectangle (0, 0, Size, Size));
                g.IntersectClip (wider);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (10, 10).A > 0, "inside both clips should be painted");
            Assert.Equal (0, surface.GetPixel (50, 50).A);      // outside the first clip: it must survive
        }

        // ---- GFX-13: clips keep their shape instead of collapsing to a bounding rectangle ----

        [Fact]
        public void SetClip_with_an_elliptical_region_clips_to_the_ellipse_not_its_bounds ()
        {
            using var path = new GraphicsPath ();
            path.AddEllipse (0, 0, Size, Size);

            using var surface = Render (g => {
                using var region = new Region (path);
                g.SetClip (region);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (50, 50).A > 0, "the middle of the ellipse should be painted");
            Assert.Equal (0, surface.GetPixel (2, 2).A);        // a corner of the bounding box
        }

        [Fact]
        public void ExcludeClip_with_an_L_shaped_region_excludes_only_the_region ()
        {
            // The bounding box of an L is the whole square, so the old code blanked everything -- which
            // is exactly what "punch a hole for the child control" transparent painting relies on not
            // happening.
            using var surface = Render (g => {
                using var shape = new Region (new Rectangle (0, 0, 40, Size));      // left column
                shape.Union (new Rectangle (0, 60, Size, 40));                      // bottom row
                g.ExcludeClip (shape);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (80, 20).A > 0,
                "the quadrant outside the L, but inside its bounding box, must still be painted");
            Assert.Equal (0, surface.GetPixel (20, 20).A);      // inside the L's column
            Assert.Equal (0, surface.GetPixel (80, 80).A);      // inside the L's row
        }

        [Fact]
        public void The_Clip_property_round_trips_a_non_rectangular_clip ()
        {
            // var saved = g.Clip; g.SetClip(part); ...; g.Clip = saved;
            // The getter used to hand back a rectangle, so the restore silently widened the clip to the
            // bounding box and later drawing leaked into the corners.
            using var path = new GraphicsPath ();
            path.AddEllipse (0, 0, Size, Size);

            using var surface = Render (g => {
                using var region = new Region (path);
                g.SetClip (region);

                var saved = g.Clip;
                g.SetClip (new Rectangle (0, 0, 10, 10));
                g.Clip = saved;

                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (50, 50).A > 0, "the restored clip should let the middle through");
            Assert.Equal (0, surface.GetPixel (2, 2).A);        // still outside the ellipse
        }

        [Fact]
        public void SetClip_from_another_Graphics_carries_the_shape_across ()
        {
            using var path = new GraphicsPath ();
            path.AddEllipse (0, 0, Size, Size);
            using var other = NewSurface ();
            using var sourceGraphics = Graphics.FromImage (other);
            using var region = new Region (path);
            sourceGraphics.SetClip (region);

            using var surface = Render (g => {
                g.SetClip (sourceGraphics);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (50, 50).A > 0, "the copied clip should let the middle through");
            Assert.Equal (0, surface.GetPixel (2, 2).A);
        }

        [Fact]
        public void SetClip_honours_the_widening_combine_modes ()
        {
            // Union, Xor and Complement widen, which no Skia clip operation can do, so they used to
            // fall through to Replace -- a different shape whenever the current clip was not already
            // inside the incoming one.
            using var surface = Render (g => {
                g.SetClip (new Rectangle (0, 0, 20, 20));
                g.SetClip (new Rectangle (80, 80, 20, 20), CombineMode.Union);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (10, 10).A > 0, "the original clip must survive a Union");
            Assert.True (surface.GetPixel (90, 90).A > 0, "the unioned rectangle must be painted too");
            Assert.Equal (0, surface.GetPixel (50, 50).A);      // in neither rectangle
        }

        [Fact]
        public void TranslateClip_moves_a_non_rectangular_clip_without_squaring_it ()
        {
            using var path = new GraphicsPath ();
            path.AddEllipse (0, 0, 50, 50);

            using var surface = Render (g => {
                using var region = new Region (path);
                g.SetClip (region);
                g.TranslateClip (50, 50);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (75, 75).A > 0, "the moved ellipse should let its middle through");
            Assert.Equal (0, surface.GetPixel (25, 25).A);      // where the clip used to be
            Assert.Equal (0, surface.GetPixel (52, 52).A);      // a corner of the moved bounding box
        }

        // ---- GFX-24: SetClip(GraphicsPath) clips to the outline, not the control points ----

        [Fact]
        public void SetClip_with_a_path_clips_to_the_curve_not_to_its_control_points ()
        {
            // A circle's Bezier control points sit outside the circle, so a clip replayed as a polyline
            // through them bulges past the outline: (12,12) is outside the circle but inside that
            // polygon, which makes it the pixel that tells the two apart.
            using var path = new GraphicsPath ();
            path.AddEllipse (0, 0, Size, Size);

            using var surface = Render (g => {
                g.SetClip (path);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (50, 50).A > 0, "the middle of the circle should be painted");
            Assert.True (surface.GetPixel (8, 50).A > 0, "just inside the circle's left edge should be painted");
            Assert.Equal (0, surface.GetPixel (12, 12).A);
        }

        [Fact]
        public void SetClip_with_a_path_honours_the_combine_mode ()
        {
            using var left = new GraphicsPath ();
            left.AddRectangle (new Rectangle (0, 0, 40, Size));

            using var surface = Render (g => {
                g.SetClip (new Rectangle (0, 0, Size, 40));
                g.SetClip (left, CombineMode.Intersect);
                using var brush = new SolidBrush (Color.Red);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            Assert.True (surface.GetPixel (20, 20).A > 0, "the intersection should be painted");
            Assert.Equal (0, surface.GetPixel (80, 20).A);      // only in the rectangle clip
            Assert.Equal (0, surface.GetPixel (20, 80).A);      // only in the path clip
        }

        // ---- GFX-15: DrawImage's callback overloads keep the ImageAttributes ----

        [Fact]
        public void DrawImage_with_ImageAttributes_and_a_callback_applies_the_attributes ()
        {
            using var source = new Bitmap (4, 4);
            using (var sg = Graphics.FromImage (source)) {
                using var brush = new SolidBrush (Color.Red);
                sg.FillRectangle (brush, new Rectangle (0, 0, 4, 4));
            }

            var matrix = new ColorMatrix ();
            matrix[3, 3] = 0f;                  // scale alpha to zero: the image must vanish
            using var attributes = new ImageAttributes ();
            attributes.SetColorMatrix (matrix);

            using var withAttributes = Render (g => g.DrawImage (source, new Rectangle (0, 0, 40, 40),
                0, 0, 4, 4, GraphicsUnit.Pixel, attributes, null), 40);
            using var withoutAttributes = Render (g => g.DrawImage (source, new Rectangle (0, 0, 40, 40),
                0, 0, 4, 4, GraphicsUnit.Pixel, null, null), 40);

            Assert.True (Inked (withoutAttributes) > 0,
                "the same call without attributes must paint, or the test proves nothing");
            Assert.Equal (0, Inked (withAttributes));
        }

        // ---- GFX-08 (in part): CompositingMode.SourceCopy replaces rather than blends ----

        [Fact]
        public void CompositingMode_SourceCopy_replaces_the_destination_instead_of_blending ()
        {
            var translucentRed = Color.FromArgb (128, 255, 0, 0);

            static Bitmap Paint (CompositingMode mode, Color color) => Render (g => {
                using var white = new SolidBrush (Color.White);
                g.FillRectangle (white, new Rectangle (0, 0, Size, Size));
                g.CompositingMode = mode;
                using var brush = new SolidBrush (color);
                g.FillRectangle (brush, new Rectangle (0, 0, Size, Size));
            });

            using var blended = Paint (CompositingMode.SourceOver, translucentRed);
            using var copied = Paint (CompositingMode.SourceCopy, translucentRed);

            var over = blended.GetPixel (50, 50);
            var copy = copied.GetPixel (50, 50);

            Assert.Equal (255, over.A);                     // blended onto opaque white
            Assert.True (over.G > 0, "SourceOver should leave some of the white showing through");
            Assert.Equal (128, copy.A);                     // the source's own alpha replaced it
            Assert.Equal (0, copy.G);
        }

        // ---- helpers whose failure would be silent ----

        private static int CountRows (Bitmap bitmap, int from, int to)
        {
            var count = 0;
            for (var y = from; y <= to; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y).A > 0)
                        count++;
            return count;
        }

        private static int PartiallyTransparent (Bitmap bitmap)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var a = bitmap.GetPixel (x, y).A;
                    if (a is > 0 and < 255)
                        count++;
                }
            return count;
        }

        private static HashSet<Color> DistinctColors (Bitmap bitmap)
        {
            var colors = new HashSet<Color> ();
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.A > 0)
                        colors.Add (c);
                }
            return colors;
        }
    }
}
