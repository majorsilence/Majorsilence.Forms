using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
// See GraphicsPathTests for why these are aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Covers the GraphicsPath, FontFamily and StringFormat surface added in Phase 5 of
/// docs/gdi-gap-plan.md — most importantly <c>AddString</c>, which turns text into real glyph geometry.
/// </summary>
public class GraphicsPathPhase5Tests
{
    // ---- AddString ----

    [Fact]
    public void AddString_produces_real_glyph_geometry ()
    {
        using var path = new GraphicsPath ();
        using var family = new FontFamily ("Arial");

        path.AddString ("Hg", family, (int)FontStyle.Regular, 48f, new PointF (10, 20), null);

        Assert.True (path.PointCount > 0, "the glyph outline should contribute points");
        var bounds = path.GetBounds ();
        Assert.True (bounds.Width > 0 && bounds.Height > 0, $"outline should have real extents, got {bounds}");

        // Curves, not a polygon: a text outline must contain Bezier point types.
        Assert.Contains (path.PathTypes, t => (t & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier);
    }

    [Fact]
    public void AddString_lays_text_out_from_the_top_left_like_GDI_plus ()
    {
        // Skia draws from the baseline; GDI+ from the top-left. The outline must therefore sit below
        // the origin, not straddle it.
        using var path = new GraphicsPath ();
        using var family = new FontFamily ("Arial");

        path.AddString ("H", family, (int)FontStyle.Regular, 40f, new PointF (0, 100), null);

        var bounds = path.GetBounds ();
        Assert.True (bounds.Top >= 99f, $"outline should start at or below the origin y, got top={bounds.Top}");
    }

    [Fact]
    public void AddString_scales_with_em_size ()
    {
        using var family = new FontFamily ("Arial");
        using var small = new GraphicsPath ();
        using var large = new GraphicsPath ();

        small.AddString ("W", family, (int)FontStyle.Regular, 20f, new PointF (0, 0), null);
        large.AddString ("W", family, (int)FontStyle.Regular, 60f, new PointF (0, 0), null);

        Assert.True (large.GetBounds ().Width > small.GetBounds ().Width * 2f,
            "tripling the em size should roughly triple the outline width");
    }

    [Fact]
    public void AddString_ignores_empty_input ()
    {
        using var path = new GraphicsPath ();
        using var family = new FontFamily ("Arial");

        path.AddString ("", family, (int)FontStyle.Regular, 20f, new PointF (0, 0), null);
        path.AddString ("x", family, (int)FontStyle.Regular, 0f, new PointF (0, 0), null);

        Assert.Equal (0, path.PointCount);
    }

    // ---- Flatten / Reverse / Clone ----

    [Fact]
    public void Flatten_replaces_curves_with_line_segments ()
    {
        using var path = new GraphicsPath ();
        path.AddEllipse (new RectangleF (0, 0, 100, 60));
        Assert.Contains (path.PathTypes, t => (t & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier);

        path.Flatten ();

        Assert.DoesNotContain (path.PathTypes, t => (t & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier);
        Assert.True (path.PointCount > 4, "flattening should produce many line segments");
    }

    [Fact]
    public void Flatten_with_a_tighter_flatness_produces_more_segments ()
    {
        using var coarse = new GraphicsPath ();
        using var fine = new GraphicsPath ();
        coarse.AddEllipse (new RectangleF (0, 0, 100, 100));
        fine.AddEllipse (new RectangleF (0, 0, 100, 100));

        coarse.Flatten (null, 1.0f);
        fine.Flatten (null, 0.05f);

        Assert.True (fine.PointCount > coarse.PointCount,
            $"finer flatness should yield more points ({fine.PointCount} vs {coarse.PointCount})");
    }

    [Fact]
    public void Flatten_keeps_the_shape_within_its_bounds ()
    {
        using var path = new GraphicsPath ();
        path.AddEllipse (new RectangleF (10, 20, 100, 60));
        var before = path.GetBounds ();

        path.Flatten ();
        var after = path.GetBounds ();

        // Flattening interpolates along the curve, so it may inscribe slightly but must not grow.
        Assert.True (after.Left >= before.Left - 1f && after.Right <= before.Right + 1f, $"{after} vs {before}");
        Assert.True (after.Top >= before.Top - 1f && after.Bottom <= before.Bottom + 1f, $"{after} vs {before}");
    }

    [Fact]
    public void Reverse_flips_the_point_order ()
    {
        using var path = new GraphicsPath ();
        path.AddLine (0, 0, 10, 0);
        path.AddLine (10, 0, 10, 10);
        var first = path.PathPoints[0];
        var last = path.PathPoints[^1];

        path.Reverse ();

        Assert.Equal (last, path.PathPoints[0]);
        Assert.Equal (first, path.PathPoints[^1]);
    }

    [Fact]
    public void Clone_is_independent_of_the_original ()
    {
        using var path = new GraphicsPath (FillMode.Winding);
        path.AddRectangle (new RectangleF (0, 0, 10, 10));

        using var clone = path.Clone ();
        Assert.Equal (FillMode.Winding, clone.FillMode);
        Assert.Equal (path.PointCount, clone.PointCount);

        clone.AddRectangle (new RectangleF (50, 50, 10, 10));
        Assert.NotEqual (path.PointCount, clone.PointCount);
    }

    // ---- Point/type inspection ----

    [Fact]
    public void GetLastPoint_returns_the_final_point ()
    {
        using var path = new GraphicsPath ();
        path.AddLine (0, 0, 42, 24);

        Assert.Equal (new PointF (42, 24), path.GetLastPoint ());
    }

    [Fact]
    public void PathData_agrees_with_PathPoints_and_PathTypes ()
    {
        using var path = new GraphicsPath ();
        path.AddRectangle (new RectangleF (0, 0, 10, 10));

        var data = path.PathData;

        Assert.Equal (path.PathTypes.Length, data.Types!.Length);
        Assert.Equal (data.Points!.Length, data.Types.Length);
        Assert.Equal ((byte)PathPointType.Start, (byte)(data.Types[0] & (byte)PathPointType.PathTypeMask));
    }

    [Fact]
    public void SetMarkers_flags_the_current_end_of_the_path ()
    {
        using var path = new GraphicsPath ();
        path.AddLine (0, 0, 10, 0);
        path.SetMarkers ();
        path.AddLine (10, 0, 10, 10);

        var marked = path.PathTypes.Count (t => (t & (byte)PathPointType.PathMarker) != 0);
        Assert.Equal (1, marked);

        path.ClearMarkers ();
        Assert.DoesNotContain (path.PathTypes, t => (t & (byte)PathPointType.PathMarker) != 0);
    }

    // ---- Hit testing ----

    [Fact]
    public void IsOutlineVisible_hits_the_stroke_but_not_the_interior ()
    {
        using var path = new GraphicsPath ();
        path.AddRectangle (new RectangleF (0, 0, 100, 100));
        using var pen = new Pen (Color.Black, 6f);

        Assert.True (path.IsOutlineVisible (new PointF (0, 50), pen), "a point on the left edge is on the outline");
        Assert.False (path.IsOutlineVisible (new PointF (50, 50), pen), "the interior is not the outline");
        Assert.False (path.IsOutlineVisible (new PointF (200, 200), pen), "a far-away point is not on the outline");
    }

    // ---- AddPie / AddClosedCurve / Warp ----

    [Fact]
    public void AddPie_produces_a_closed_wedge_within_the_bounding_ellipse ()
    {
        using var path = new GraphicsPath ();
        path.AddPie (0, 0, 100, 100, 0, 90);

        var bounds = path.GetBounds ();
        Assert.True (path.PointCount > 0);
        // A 0..90 degree wedge occupies the lower-right quadrant plus the center.
        Assert.True (bounds.Right <= 101f && bounds.Bottom <= 101f, $"unexpected bounds {bounds}");
    }

    [Fact]
    public void AddClosedCurve_closes_the_figure ()
    {
        using var open = new GraphicsPath ();
        using var closed = new GraphicsPath ();
        PointF[] points = [new (0, 0), new (50, 20), new (100, 0)];

        open.AddCurve (points);
        closed.AddClosedCurve (points);

        Assert.Contains (closed.PathTypes, t => (t & (byte)PathPointType.CloseSubpath) != 0);
        Assert.DoesNotContain (open.PathTypes, t => (t & (byte)PathPointType.CloseSubpath) != 0);
    }

    [Fact]
    public void Warp_maps_the_path_onto_the_destination_quad ()
    {
        using var path = new GraphicsPath ();
        path.AddRectangle (new RectangleF (0, 0, 100, 100));

        // Map the unit source rect onto a quad shifted right and down by 200.
        path.Warp ([new PointF (200, 200), new PointF (300, 200), new PointF (200, 300), new PointF (300, 300)],
            new RectangleF (0, 0, 100, 100));

        var bounds = path.GetBounds ();
        Assert.True (bounds.Left >= 199f && bounds.Top >= 199f, $"warped path should move to the destination, got {bounds}");
        Assert.True (bounds.Right <= 301f && bounds.Bottom <= 301f, $"got {bounds}");
    }

    // ---- FontFamily metrics ----

    [Fact]
    public void FontFamily_metrics_are_real_values_not_placeholders ()
    {
        using var family = new FontFamily ("Arial");

        var em = family.GetEmHeight (FontStyle.Regular);
        var ascent = family.GetCellAscent (FontStyle.Regular);
        var descent = family.GetCellDescent (FontStyle.Regular);
        var lineSpacing = family.GetLineSpacing (FontStyle.Regular);

        Assert.True (em > 0);
        Assert.True (ascent > 0, $"ascent should be positive, got {ascent}");
        Assert.True (descent > 0, $"descent should be positive, got {descent}");
        // Line spacing includes ascent + descent (+ leading), so it is at least their sum.
        Assert.True (lineSpacing >= ascent + descent - 1,
            $"line spacing {lineSpacing} should cover ascent {ascent} + descent {descent}");
        // Ascent normally dominates descent for Latin faces.
        Assert.True (ascent > descent);
    }

    [Fact]
    public void GetFamilies_returns_the_installed_families ()
        => Assert.Equal (FontFamily.Families.Length, FontFamily.GetFamilies (null).Length);

    // ---- StringFormat ----

    [Fact]
    public void Tab_stops_round_trip ()
    {
        var format = new StringFormat ();
        format.SetTabStops (12f, [40f, 80f, 120f]);

        var stops = format.GetTabStops (out var firstOffset);

        Assert.Equal (12f, firstOffset);
        Assert.Equal ([40f, 80f, 120f], stops);

        // The getter must hand back a copy.
        stops[0] = 999f;
        Assert.Equal (40f, format.GetTabStops (out _)[0]);
    }

    [Fact]
    public void Digit_substitution_round_trips ()
    {
        var format = new StringFormat ();
        Assert.Equal (StringDigitSubstitute.User, format.DigitSubstitutionMethod);

        format.SetDigitSubstitution (0x0401, StringDigitSubstitute.National);

        Assert.Equal (0x0401, format.DigitSubstitutionLanguage);
        Assert.Equal (StringDigitSubstitute.National, format.DigitSubstitutionMethod);
    }
}
