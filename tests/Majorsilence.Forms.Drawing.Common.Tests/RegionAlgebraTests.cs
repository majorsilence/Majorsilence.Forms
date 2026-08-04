using System.Drawing;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Covers the region algebra added in Phase 1 of docs/gdi-gap-plan.md. Before it, Region exposed only
/// Union/Intersect/Exclude taking a RectangleF — no Region/GraphicsPath/Rectangle overloads, and no
/// Xor/Complement/Translate/Transform/IsInfinite at all — so real region algebra could not be expressed.
///
/// These assert computed geometry (via IsVisible/GetBounds), not merely that the members exist.
/// </summary>
public class RegionAlgebraTests
{
    // Two 20x20 squares that touch edge-to-edge at x=20, so overlap tests are unambiguous.
    private static Region Left () => new (new Rectangle (0, 0, 20, 20));
    private static Region Right () => new (new Rectangle (10, 0, 20, 20));

    [Fact]
    public void Union_with_region_covers_both_operands ()
    {
        using var region = Left ();
        using var other = Right ();
        region.Union (other);

        Assert.True (region.IsVisible (new Point (5, 5)));    // only in the left square
        Assert.True (region.IsVisible (new Point (25, 5)));   // only in the right square
        Assert.Equal (new RectangleF (0, 0, 30, 20), region.GetBounds ());
    }

    [Fact]
    public void Intersect_with_region_keeps_only_the_overlap ()
    {
        using var region = Left ();
        using var other = Right ();
        region.Intersect (other);

        Assert.False (region.IsVisible (new Point (5, 5)));    // left-only is gone
        Assert.False (region.IsVisible (new Point (25, 5)));   // right-only is gone
        Assert.True (region.IsVisible (new Point (15, 5)));    // the shared strip remains
        Assert.Equal (new RectangleF (10, 0, 10, 20), region.GetBounds ());
    }

    [Fact]
    public void Exclude_removes_the_other_region ()
    {
        using var region = Left ();
        using var other = Right ();
        region.Exclude (other);

        Assert.True (region.IsVisible (new Point (5, 5)));
        Assert.False (region.IsVisible (new Point (15, 5)));   // the overlap was cut away
        Assert.Equal (new RectangleF (0, 0, 10, 20), region.GetBounds ());
    }

    [Fact]
    public void Xor_keeps_what_is_in_exactly_one_operand ()
    {
        using var region = Left ();
        using var other = Right ();
        region.Xor (other);

        Assert.True (region.IsVisible (new Point (5, 5)));     // left-only kept
        Assert.True (region.IsVisible (new Point (25, 5)));    // right-only kept
        Assert.False (region.IsVisible (new Point (15, 5)));   // shared strip dropped
    }

    [Fact]
    public void Complement_keeps_the_part_of_the_other_region_not_in_this_one ()
    {
        using var region = Left ();
        using var other = Right ();
        region.Complement (other);

        Assert.False (region.IsVisible (new Point (5, 5)));    // this-only is discarded
        Assert.False (region.IsVisible (new Point (15, 5)));   // shared strip is discarded
        Assert.True (region.IsVisible (new Point (25, 5)));    // other-only survives
        Assert.Equal (new RectangleF (20, 0, 10, 20), region.GetBounds ());
    }

    [Fact]
    public void Union_with_graphics_path_uses_the_path_interior ()
    {
        using var region = new Region (new Rectangle (0, 0, 10, 10));
        using var path = new GraphicsPath ();
        path.AddRectangle (new Rectangle (50, 50, 10, 10));

        region.Union (path);

        Assert.True (region.IsVisible (new Point (5, 5)));
        Assert.True (region.IsVisible (new Point (55, 55)));
        Assert.Equal (new RectangleF (0, 0, 60, 60), region.GetBounds ());
    }

    [Fact]
    public void Intersect_with_graphics_path_on_a_fresh_infinite_region_yields_the_path ()
    {
        // Regression guard for the infinite-region case: SKRegion.Op(SKPath, ...) rasterizes against the
        // region's own bounds, so intersecting a path into a brand-new (infinite) region has to go
        // through an explicitly clipped region or it collapses to empty.
        using var region = new Region ();
        using var path = new GraphicsPath ();
        path.AddRectangle (new Rectangle (5, 5, 10, 10));

        region.Intersect (path);

        Assert.True (region.IsVisible (new Point (10, 10)));
        Assert.False (region.IsVisible (new Point (100, 100)));
        Assert.Equal (new RectangleF (5, 5, 10, 10), region.GetBounds ());
    }

    [Fact]
    public void Translate_offsets_the_region ()
    {
        using var region = new Region (new Rectangle (0, 0, 10, 10));
        region.Translate (100, 50);

        Assert.False (region.IsVisible (new Point (5, 5)));
        Assert.True (region.IsVisible (new Point (105, 55)));
        Assert.Equal (new RectangleF (100, 50, 10, 10), region.GetBounds ());
    }

    [Fact]
    public void Transform_applies_the_matrix ()
    {
        using var region = new Region (new Rectangle (0, 0, 10, 10));
        using var matrix = new Matrix ();
        matrix.Scale (2f, 3f);

        region.Transform (matrix);

        Assert.Equal (new RectangleF (0, 0, 20, 30), region.GetBounds ());
    }

    [Fact]
    public void IsInfinite_is_true_only_for_an_unbounded_region ()
    {
        using var fresh = new Region ();
        Assert.True (fresh.IsInfinite ());

        using var bounded = new Region (new Rectangle (0, 0, 10, 10));
        Assert.False (bounded.IsInfinite ());

        bounded.MakeInfinite ();
        Assert.True (bounded.IsInfinite ());

        using var emptied = new Region ();
        emptied.MakeEmpty ();
        Assert.False (emptied.IsInfinite ());
    }

    [Theory]
    [InlineData (CombineMode.Replace, 0)]
    [InlineData (CombineMode.Intersect, 1)]
    [InlineData (CombineMode.Union, 2)]
    [InlineData (CombineMode.Xor, 3)]
    [InlineData (CombineMode.Exclude, 4)]
    [InlineData (CombineMode.Complement, 5)]
    public void CombineMode_values_match_GDI_plus (CombineMode mode, int expected)
    {
        // Designer-serialized code persists these as raw integers, so the numeric values are API.
        Assert.Equal (expected, (int)mode);
    }
}
