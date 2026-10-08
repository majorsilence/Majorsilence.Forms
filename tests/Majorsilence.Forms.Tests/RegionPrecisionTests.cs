using System;
using System.Diagnostics;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;
using MFRegion = Majorsilence.Forms.Drawing.Region;

namespace Majorsilence.Forms.Tests;

// GFX-44: Region is float-precision, as GDI+'s is (dotnet/winforms
// src/System.Drawing.Common/src/System/Drawing/Region.cs: GdipCreateRegionRect, GdipTranslateRegion with
// float offsets, GdipIsInfiniteRegion). It used to be an integer SKRegion, so every fractional rectangle
// and offset was rounded and "infinite" was guessed from the bounds.
[Collection ("Headless")]
public class RegionPrecisionTests
{
    [Fact]
    public void Translating_by_0_4_twice_moves_0_8 ()
    {
        // The finding's own case: each rounded offset was 0, so the region never moved.
        using var r = new MFRegion (new Rectangle (0, 0, 10, 10));

        r.Translate (0.4f, 0.4f);
        r.Translate (0.4f, 0.4f);

        var bounds = r.GetBounds ();
        Assert.Equal (0.8f, bounds.X, 4);
        Assert.Equal (0.8f, bounds.Y, 4);
        Assert.Equal (10f, bounds.Width, 4);
    }

    [Fact]
    public void A_fractional_rectangle_keeps_its_edges ()
    {
        using var r = new MFRegion (new RectangleF (1.25f, 2.5f, 3.5f, 4.75f));

        Assert.Equal (new RectangleF (1.25f, 2.5f, 3.5f, 4.75f), r.GetBounds ());
    }

    [Fact]
    public void A_sub_pixel_region_is_not_empty ()
    {
        // Rectangle.Round turned a half-pixel separator into nothing.
        using var r = new MFRegion (new RectangleF (10f, 10f, 0.5f, 0.5f));

        Assert.False (r.IsEmpty ());
        Assert.Equal (0.5f, r.GetBounds ().Width, 4);
    }

    [Fact]
    public void Transform_scales_exactly ()
    {
        // A quarter of 10 is 2.5; rasterising the scaled path gave a whole number.
        using var r = new MFRegion (new Rectangle (0, 0, 10, 10));
        using var m = new Matrix ();
        m.Scale (0.25f, 0.25f);

        r.Transform (m);

        Assert.Equal (new RectangleF (0, 0, 2.5f, 2.5f), r.GetBounds ());
    }

    [Fact]
    public void Infinite_is_a_flag_not_a_size ()
    {
        using var infinite = new MFRegion ();
        Assert.True (infinite.IsInfinite ());

        // A union keeps it infinite; anything that bounds it does not.
        infinite.Union (new Rectangle (0, 0, 5, 5));
        Assert.True (infinite.IsInfinite ());

        using var excluded = new MFRegion ();
        excluded.Exclude (new RectangleF (10.5f, 10.5f, 5f, 5f));
        Assert.False (excluded.IsInfinite ());
        Assert.True (excluded.IsVisible (0, 0));
        Assert.True (excluded.IsVisible (1_000_000, -1_000_000));
        Assert.False (excluded.IsVisible (12, 12));

        // The bounds heuristic called any region reaching ±2^28 infinite, however it was made.
        const int Huge = 1 << 28;
        using var big = new MFRegion (new Rectangle (-Huge, -Huge, 2 * Huge, 2 * Huge));
        Assert.False (big.IsInfinite ());

        big.MakeInfinite ();
        Assert.True (big.IsInfinite ());

        using var clone = big.Clone ();
        Assert.True (clone.IsInfinite ());

        clone.Intersect (new RectangleF (0.5f, 0.5f, 1f, 1f));
        Assert.False (clone.IsInfinite ());
        Assert.Equal (new RectangleF (0.5f, 0.5f, 1f, 1f), clone.GetBounds ());
    }

    // A = [0, 10.5) x [0, 10), B = [5.25, 15.25) x [0, 10): fractional, so these go through SKPath.Op.
    private static MFRegion A () => new (new RectangleF (0f, 0f, 10.5f, 10f));
    private static RectangleF B => new (5.25f, 0f, 10f, 10f);

    [Fact]
    public void Union_and_intersect_of_fractional_rectangles ()
    {
        using var union = A ();
        union.Union (B);
        Assert.Equal (new RectangleF (0f, 0f, 15.25f, 10f), union.GetBounds ());

        using var intersect = A ();
        intersect.Intersect (B);
        Assert.Equal (new RectangleF (5.25f, 0f, 5.25f, 10f), intersect.GetBounds ());
    }

    [Fact]
    public void Exclude_complement_and_xor_of_fractional_rectangles ()
    {
        using var exclude = A ();
        exclude.Exclude (B);
        Assert.Equal (new RectangleF (0f, 0f, 5.25f, 10f), exclude.GetBounds ());

        // Complement is "the argument minus this region".
        using var complement = A ();
        complement.Complement (B);
        Assert.Equal (new RectangleF (10.5f, 0f, 4.75f, 10f), complement.GetBounds ());

        using var xor = A ();
        xor.Xor (B);
        Assert.Equal (new RectangleF (0f, 0f, 15.25f, 10f), xor.GetBounds ());
        Assert.True (xor.IsVisible (2, 5));
        Assert.False (xor.IsVisible (7, 5));
        Assert.True (xor.IsVisible (12, 5));
    }

    [Fact]
    public void Clipping_to_a_fractional_region_masks_the_pixels_its_edges_cover ()
    {
        // [0, 4) translated by 0.4 twice is [0.8, 4.8): pixel centres 1.5 to 4.5 are inside, so columns
        // 1-4. Rounded, it stayed at columns 0-3. Antialiasing is on for the Graphics, and the clip must
        // still be hard-edged -- column 4 is 80% covered and must be painted in full, as GDI+ does.
        using var region = new MFRegion (new Rectangle (0, 0, 4, 10));
        region.Translate (0.4f, 0f);
        region.Translate (0.4f, 0f);

        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using (var g = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap)) {
            g.Clear (Color.White);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip (region);
            g.FillRectangle (Majorsilence.Forms.Drawing.Brushes.Black, 0, 0, 10, 10);
        }

        var white = Color.White.ToArgb ();
        var black = Color.Black.ToArgb ();
        Assert.Equal (white, bitmap.GetPixel (0, 5).ToArgb ());
        Assert.Equal (black, bitmap.GetPixel (1, 5).ToArgb ());
        Assert.Equal (black, bitmap.GetPixel (4, 5).ToArgb ());
        Assert.Equal (white, bitmap.GetPixel (5, 5).ToArgb ());
    }

    [Fact]
    public void A_region_clip_follows_the_world_transform ()
    {
        // GDI+ regions are in world coordinates, so SetClip (Region) goes through the Graphics transform
        // like every other drawing call. ClipRegion took the scanlines as device pixels: under a 2x scale a
        // 2.5-wide region clipped 2 device pixels instead of 5.
        using var region = new MFRegion (new RectangleF (0f, 0f, 2.5f, 5f));

        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using (var g = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap)) {
            g.Clear (Color.White);
            g.ScaleTransform (2f, 2f);
            g.SetClip (region);
            g.FillRectangle (Majorsilence.Forms.Drawing.Brushes.Black, 0, 0, 5, 5);
        }

        Assert.Equal (Color.Black.ToArgb (), bitmap.GetPixel (4, 5).ToArgb ());
        Assert.Equal (Color.White.ToArgb (), bitmap.GetPixel (5, 5).ToArgb ());
    }

    [Fact]
    public void Graphics_Clip_round_trips_a_fractional_region ()
    {
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (20, 20);
        using var g = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
        using var region = new MFRegion (new RectangleF (2.5f, 3.25f, 6f, 4.5f));

        g.SetClip (region);

        using var clip = g.Clip;
        Assert.Equal (new RectangleF (2.5f, 3.25f, 6f, 4.5f), clip.GetBounds ());
    }

    [Fact]
    public void A_many_rectangle_integer_region_stays_on_the_scanline_fast_path ()
    {
        // Invalidation-style regions are hundreds of whole-pixel rectangles. They must not pay for path
        // geometry: the internal flag is the mechanism check; the timing bound is a coarse guard only.
        var watch = Stopwatch.StartNew ();

        using var region = new MFRegion ();
        region.MakeEmpty ();
        for (var i = 0; i < 500; i++)
            region.Union (new Rectangle ((i % 25) * 12, (i / 25) * 12, 10, 10));

        using var other = new MFRegion (new Rectangle (5, 5, 200, 200));
        region.Intersect (other);
        region.Exclude (new Rectangle (50, 50, 20, 20));
        region.Xor (new RectangleF (0f, 0f, 30f, 30f));
        region.Translate (3, 4);
        region.Translate (2f, 1f);

        watch.Stop ();

        Assert.False (region.IsShaped);
        Assert.False (region.IsEmpty ());
        Assert.True (region.GetRegionScans (null).Length > 100);
        Assert.True (watch.ElapsedMilliseconds < 5000, $"500-rect region took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void A_fractional_operand_moves_the_region_to_a_path ()
    {
        // The other side of the fast path, so the flag above means something.
        using var region = new MFRegion (new Rectangle (0, 0, 10, 10));
        Assert.False (region.IsShaped);

        region.Union (new RectangleF (0f, 0f, 10.5f, 1f));
        Assert.True (region.IsShaped);
    }

    [Fact]
    public void Infinite_minus_a_fractional_shape_rasterises_quickly ()
    {
        // The derived scanlines of "everything but a small hole" must not walk the ±2^28 extent row by
        // row (half a billion of them, seconds of work): only the hole is rasterised.
        var before = MFRegion.RasterisedRows;

        using var region = new MFRegion ();
        region.Exclude (new RectangleF (10.5f, 10.5f, 5f, 5f));
        var scans = region.GetRegionScans (null);

        Assert.True (MFRegion.RasterisedRows - before < 1000, $"rasterised {MFRegion.RasterisedRows - before} rows");
        Assert.True (scans.Length >= 4);
        Assert.True (region.IsVisible (9, 9));
        Assert.False (region.IsVisible (12, 12));
        Assert.False (region.IsVisible (14, 14));
        Assert.True (region.IsVisible (16, 16));
    }
}
