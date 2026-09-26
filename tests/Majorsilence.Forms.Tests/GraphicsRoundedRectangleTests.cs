using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;
using Xunit;
using Pen = Majorsilence.Forms.Drawing.Pen;
using SolidBrush = Majorsilence.Forms.Drawing.SolidBrush;

namespace Majorsilence.Forms.Tests;

// Graphics.FillRoundedRectangle / DrawRoundedRectangle and GraphicsPath.AddRoundedRectangle.
//
// Every pixel probed below is chosen so that one specific wrong implementation turns it: a square rectangle
// (corner pixels filled), a radius that ignores the world transform, corners assigned in the wrong order, or
// per-corner clamping in place of the CSS rule of scaling all radii by one factor. "Something was drawn" is not
// asserted anywhere, because a square rectangle satisfies it.
public class GraphicsRoundedRectangleTests
{
    // A control's OnPaint gets a canvas in DEVICE pixels (each control paints into a back buffer of its scaled size
    // and the parent blits it), so a control that wants to think in logical units has to scale its own graphics.
    // The theory below runs at 1 and 2 to prove the rounded shapes follow that transform rather than being
    // computed against raw canvas pixels.
    private sealed class DrawingControl : Control
    {
        public Action<Graphics>? Draw { get; init; }

        protected override void OnPaintBackground (PaintEventArgs e) => e.Canvas.Clear (SKColors.Transparent);

        protected override void OnPaint (PaintEventArgs e)
        {
            var scale = (float)e.Scaling;
            e.Graphics.ScaleTransform (scale, scale);
            Draw?.Invoke (e.Graphics);
        }
    }

    private static SKBitmap Render (int logicalWidth, int logicalHeight, int scale, Action<Graphics> draw)
        => PaintSurface.Render (new DrawingControl { Width = logicalWidth, Height = logicalHeight, Draw = draw }, scale);

    // Device pixel whose top-left corner is the logical point (x, y).
    private static SKColor At (SKBitmap bitmap, int scale, int x, int y) => bitmap.GetPixel (x * scale, y * scale);

    // Device pixel just inside the logical right/bottom edge.
    private static SKColor Inside (SKBitmap bitmap, int scale, int right, int bottom) => bitmap.GetPixel (right * scale - 1, bottom * scale - 1);

    // Anti-aliasing leaves a faint fringe on an edge that passes near a pixel, so "empty" and "solid" carry a margin
    // instead of being exact. The probes are placed well clear of any edge, so the margin never decides a result.
    private static void AssertClear (SKColor pixel, string where) => Assert.True (pixel.Alpha < 16, $"{where} should be transparent but has alpha {pixel.Alpha}");

    private static void AssertSolid (SKColor pixel, string where) => Assert.True (pixel.Alpha > 240, $"{where} should be solid but has alpha {pixel.Alpha}");

    // ---- Graphics.FillRoundedRectangle ----

    [Theory]
    [InlineData (1)]
    [InlineData (2)]
    public void Fill_UniformRadius_LeavesCornersTransparentAndEdgesFilled (int scale)
    {
        // Rect (10,10)-(50,30), radius 8, so the top-left arc is centred on (18,18).
        using var bitmap = Render (60, 40, scale, g => g.FillRoundedRectangle (new SolidBrush (Color.Red), new RectangleF (10, 10, 40, 20), 8f));

        AssertClear (At (bitmap, scale, 10, 10), "top-left corner");
        AssertClear (bitmap.GetPixel (50 * scale - 1, 10 * scale), "top-right corner");
        AssertClear (bitmap.GetPixel (10 * scale, 30 * scale - 1), "bottom-left corner");
        AssertClear (Inside (bitmap, scale, 50, 30), "bottom-right corner");

        AssertSolid (bitmap.GetPixel (30 * scale, 10 * scale), "top edge midpoint");
        AssertSolid (bitmap.GetPixel (30 * scale, 30 * scale - 1), "bottom edge midpoint");
        AssertSolid (bitmap.GetPixel (10 * scale, 20 * scale), "left edge midpoint");
        AssertSolid (bitmap.GetPixel (50 * scale - 1, 20 * scale), "right edge midpoint");

        // On the corner's diagonal the arc separates the 1-unit square at (11,11), whose nearest point is 8.5 from
        // the arc's centre and so wholly outside the radius of 8, from the one at (14,14), whose farthest point is
        // 5.7 from it and so wholly inside. A square rectangle fills both.
        AssertClear (At (bitmap, scale, 11, 11), "just outside the arc");
        AssertSolid (At (bitmap, scale, 14, 14), "just inside the arc");

        var centre = At (bitmap, scale, 30, 20);
        Assert.Equal (new SKColor (255, 0, 0, 255), centre);
    }

    [Theory]
    [InlineData (1)]
    [InlineData (2)]
    public void Fill_PerCornerRadii_AreAppliedClockwiseFromTopLeft (int scale)
    {
        // Rect (10,10)-(50,50). Top-left and bottom-left square, top-right radius 14, bottom-right radius 6.
        using var bitmap = Render (60, 60, scale, g => g.FillRoundedRectangle (new SolidBrush (Color.Red), new RectangleF (10, 10, 40, 40), 0f, 14f, 6f, 0f));

        AssertSolid (At (bitmap, scale, 10, 10), "top-left (radius 0)");
        AssertSolid (bitmap.GetPixel (10 * scale, 50 * scale - 1), "bottom-left (radius 0)");
        AssertClear (bitmap.GetPixel (50 * scale - 1, 10 * scale), "top-right (radius 14)");
        AssertClear (Inside (bitmap, scale, 50, 50), "bottom-right (radius 6)");

        // (47,12) is 16.3 from the top-right arc's centre (36,24): outside radius 14, but it would be inside a
        // radius of 6. (47,47) is 4.6 from the bottom-right arc's centre (44,44): inside radius 6, but it would be
        // outside a radius of 14. Together they fail if the two radii were swapped or applied in another order.
        AssertClear (At (bitmap, scale, 47, 12), "inside the top-right corner region");
        AssertSolid (At (bitmap, scale, 47, 47), "inside the bottom-right corner region");
    }

    [Theory]
    [InlineData (1)]
    [InlineData (2)]
    public void Fill_RadiiThatDoNotFit_AreScaledTogether_NotClampedIndividually (int scale)
    {
        // Rect (10,10)-(50,30), only the top-left corner asks for a radius, and 40 does not fit a rectangle 20 high.
        // The CSS rule multiplies every radius by min(side / sum of its two radii) = min(40/40, 20/40) = 0.5, so the
        // corner ends up 20. Clamping the corner alone to half the short side would give 10 and fill (16,11).
        using var bitmap = Render (60, 40, scale, g => g.FillRoundedRectangle (new SolidBrush (Color.Red), new RectangleF (10, 10, 40, 20), 40f, 0f, 0f, 0f));

        AssertClear (At (bitmap, scale, 16, 11), "the part of the corner a radius of 20 removes and a radius of 10 would not");

        // The other three radii were zero and stay zero: still square.
        AssertSolid (bitmap.GetPixel (50 * scale - 1, 10 * scale), "top-right stays square");
        AssertSolid (bitmap.GetPixel (10 * scale, 30 * scale - 1), "bottom-left stays square");
        AssertSolid (Inside (bitmap, scale, 50, 30), "bottom-right stays square");
    }

    [Theory]
    [InlineData (1)]
    [InlineData (2)]
    public void Fill_HugeRadius_GivesAPill (int scale)
    {
        using var bitmap = Render (60, 40, scale, g => g.FillRoundedRectangle (new SolidBrush (Color.Red), new RectangleF (10, 10, 40, 20), 1000f));

        // A radius of 10 on a 40x20 rectangle: full semicircular ends, centred on (20,20) and (40,20), and a straight
        // run between them. The probes stay off the very tip of each end, where a 1-unit square is only half covered.
        AssertClear (At (bitmap, scale, 11, 11), "the square corner a pill does not have");
        AssertSolid (At (bitmap, scale, 12, 20), "left end, near its widest");
        AssertSolid (At (bitmap, scale, 47, 20), "right end, near its widest");
        AssertSolid (At (bitmap, scale, 30, 10), "straight top run");
        AssertSolid (At (bitmap, scale, 30, 29), "straight bottom run");
    }

    [Fact]
    public void Fill_ZeroRadius_IsIdenticalToFillRectangle ()
    {
        using var rounded = Render (60, 40, 1, g => g.FillRoundedRectangle (new SolidBrush (Color.Red), new Rectangle (10, 10, 40, 20), 0f));
        using var plain = Render (60, 40, 1, g => g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (10, 10, 40, 20)));

        Assert.True (plain.Bytes.SequenceEqual (rounded.Bytes), "a radius of zero must be a plain rectangle, pixel for pixel");
    }

    [Fact]
    public void Fill_RadiusFollowsTheWorldTransform ()
    {
        // A shape drawn under a 2x world transform at surface scale 1. The radius is a length in the graphics' current
        // units, so it doubles with the rectangle. The theory tests above run this same mechanism through the surface
        // scale; this one isolates the transform.
        using var bitmap = Render (60, 60, 1, g => {
            g.ScaleTransform (2f, 2f);
            g.FillRoundedRectangle (new SolidBrush (Color.Red), new RectangleF (5, 5, 20, 20), 4f);
        });

        // Device rectangle (10,10)-(50,50), device radius 8, arc centre (18,18). The 1-pixel square at (11,11) has its
        // nearest point 8.5 from that centre: wholly outside, so clear. Had the radius stayed 4 device pixels the
        // centre would be (14,14), whose farthest point from that square is 2.8 away: wholly inside, so solid.
        AssertClear (bitmap.GetPixel (11, 11), "just outside a radius of 8 device pixels");
    }

    // ---- Graphics.DrawRoundedRectangle ----

    [Theory]
    [InlineData (1)]
    [InlineData (2)]
    public void Draw_StrokesARoundedOutline_AndLeavesTheInteriorAndCornerClear (int scale)
    {
        // Rect (10,10)-(50,40), radius 10, so the top-left arc is centred on (20,20). A 4-wide stroke covers
        // radii 8 to 12 around that centre, and rows 8 to 12 along the top edge.
        using var bitmap = Render (60, 50, scale, g => g.DrawRoundedRectangle (new Pen (Color.Blue, 4f), new RectangleF (10, 10, 40, 30), 10f));

        AssertSolid (bitmap.GetPixel (30 * scale, 10 * scale), "top edge stroke");
        AssertClear (At (bitmap, scale, 30, 25), "interior (an outline, not a fill)");

        // The arc's 45 degree point is (12.9,12.9), on the stroke. The rectangle's own corner (10,10) is 14.1 from
        // the centre, beyond the stroke's outer radius of 12, so a square outline would have ink there and this does not.
        AssertSolid (At (bitmap, scale, 13, 13), "the arc at 45 degrees");
        AssertClear (At (bitmap, scale, 10, 10), "the square corner a rounded outline does not reach");
    }

    // ---- overloads agree ----

    [Fact]
    public void FillOverloads_DrawTheSameShape ()
    {
        var brush = new SolidBrush (Color.Red);
        using var floatRect = Render (60, 40, 1, g => g.FillRoundedRectangle (brush, new RectangleF (10, 10, 40, 20), 6f));
        using var intRect = Render (60, 40, 1, g => g.FillRoundedRectangle (brush, new Rectangle (10, 10, 40, 20), 6f));
        using var coords = Render (60, 40, 1, g => g.FillRoundedRectangle (brush, 10f, 10f, 40f, 20f, 6f));
        using var fourRadii = Render (60, 40, 1, g => g.FillRoundedRectangle (brush, new RectangleF (10, 10, 40, 20), 6f, 6f, 6f, 6f));
        using var intFourRadii = Render (60, 40, 1, g => g.FillRoundedRectangle (brush, new Rectangle (10, 10, 40, 20), 6f, 6f, 6f, 6f));

        Assert.True (floatRect.Bytes.SequenceEqual (intRect.Bytes), "Rectangle and RectangleF");
        Assert.True (floatRect.Bytes.SequenceEqual (coords.Bytes), "coordinates and RectangleF");
        Assert.True (floatRect.Bytes.SequenceEqual (fourRadii.Bytes), "one radius and four equal radii");
        Assert.True (floatRect.Bytes.SequenceEqual (intFourRadii.Bytes), "Rectangle with four radii");
    }

    [Fact]
    public void DrawOverloads_DrawTheSameShape ()
    {
        var pen = new Pen (Color.Blue, 3f);
        using var floatRect = Render (60, 40, 1, g => g.DrawRoundedRectangle (pen, new RectangleF (10, 10, 40, 20), 6f));
        using var intRect = Render (60, 40, 1, g => g.DrawRoundedRectangle (pen, new Rectangle (10, 10, 40, 20), 6f));
        using var coords = Render (60, 40, 1, g => g.DrawRoundedRectangle (pen, 10f, 10f, 40f, 20f, 6f));
        using var fourRadii = Render (60, 40, 1, g => g.DrawRoundedRectangle (pen, new RectangleF (10, 10, 40, 20), 6f, 6f, 6f, 6f));
        using var intFourRadii = Render (60, 40, 1, g => g.DrawRoundedRectangle (pen, new Rectangle (10, 10, 40, 20), 6f, 6f, 6f, 6f));

        Assert.True (floatRect.Bytes.SequenceEqual (intRect.Bytes), "Rectangle and RectangleF");
        Assert.True (floatRect.Bytes.SequenceEqual (coords.Bytes), "coordinates and RectangleF");
        Assert.True (floatRect.Bytes.SequenceEqual (fourRadii.Bytes), "one radius and four equal radii");
        Assert.True (floatRect.Bytes.SequenceEqual (intFourRadii.Bytes), "Rectangle with four radii");
    }

    // ---- invalid radii ----

    [Theory]
    [InlineData (-1f)]
    [InlineData (float.NaN)]
    [InlineData (float.PositiveInfinity)]
    [InlineData (float.NegativeInfinity)]
    public void BadRadius_Throws_NamingTheOffendingCorner (float bad)
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (50, 50);
        using var g = Graphics.FromImage (target);
        var brush = new SolidBrush (Color.Red);
        var pen = new Pen (Color.Blue);
        var rect = new RectangleF (5, 5, 30, 30);
        string[] names = ["topLeft", "topRight", "bottomRight", "bottomLeft"];

        for (var corner = 0; corner < 4; corner++) {
            var r = new float[4];
            r[corner] = bad;

            Assert.Equal (names[corner], Assert.Throws<ArgumentOutOfRangeException> (() => g.FillRoundedRectangle (brush, rect, r[0], r[1], r[2], r[3])).ParamName);
            Assert.Equal (names[corner], Assert.Throws<ArgumentOutOfRangeException> (() => g.DrawRoundedRectangle (pen, rect, r[0], r[1], r[2], r[3])).ParamName);

            using var path = new GraphicsPath ();
            Assert.Equal (names[corner], Assert.Throws<ArgumentOutOfRangeException> (() => path.AddRoundedRectangle (rect, r[0], r[1], r[2], r[3])).ParamName);
            Assert.Equal (0, path.PointCount);   // a rejected call leaves the path untouched
        }

        // The single-radius forms name the one parameter the caller actually passed, not one of the four corners.
        using var uniformPath = new GraphicsPath ();
        Assert.Equal ("radius", Assert.Throws<ArgumentOutOfRangeException> (() => g.FillRoundedRectangle (brush, rect, bad)).ParamName);
        Assert.Equal ("radius", Assert.Throws<ArgumentOutOfRangeException> (() => g.DrawRoundedRectangle (pen, rect, bad)).ParamName);
        Assert.Equal ("radius", Assert.Throws<ArgumentOutOfRangeException> (() => uniformPath.AddRoundedRectangle (rect, bad)).ParamName);
        Assert.Equal ("radius", Assert.Throws<ArgumentOutOfRangeException> (() => g.FillRoundedRectangle (brush, new Rectangle (5, 5, 30, 30), bad)).ParamName);
        Assert.Equal (0, uniformPath.PointCount);
    }

    // ---- GraphicsPath.AddRoundedRectangle ----

    [Fact]
    public void Path_AddRoundedRectangle_YieldsAClosedFigureWithTheRectanglesBounds ()
    {
        using var path = new GraphicsPath ();
        path.AddRoundedRectangle (new RectangleF (10, 20, 40, 30), 8f);

        var types = path.PathTypes;
        Assert.True (types.Length > 4, "a rounded rectangle is more than four corner points");
        Assert.Equal ((byte)PathPointType.Start, (byte)(types[0] & 0x07));
        Assert.NotEqual (0, types[^1] & (byte)PathPointType.CloseSubpath);

        Assert.Equal (new RectangleF (10, 20, 40, 30), path.GetBounds ());

        Assert.True (path.IsVisible (30f, 35f), "centre is inside");
        Assert.False (path.IsVisible (10.5f, 20.5f), "the square corner is outside the rounded one");
        Assert.True (path.IsVisible (30f, 20.5f), "the middle of the top edge is inside");
    }

    [Fact]
    public void Path_AddRoundedRectangle_ClosesItsFigure_SoTheNextShapeStartsAFreshOne ()
    {
        using var path = new GraphicsPath ();
        path.AddRoundedRectangle (new RectangleF (10, 10, 20, 20), 4f);
        path.AddLine (40f, 40f, 60f, 60f);

        // Counting Start points would not tell: Skia inserts a fresh moveTo after any closed contour by itself, so
        // there are two either way. What differs is WHERE the second figure starts. With the rounded rectangle's
        // figure left "open" AddLine would treat (40,40) as a continuation and the new figure would begin back at
        // the rectangle's own start point instead.
        var data = path.PathData;
        var types = Assert.IsType<byte[]> (data.Types);
        var points = Assert.IsType<PointF[]> (data.Points);
        var lastStart = Array.FindLastIndex (types, t => (t & 0x07) == (byte)PathPointType.Start);
        Assert.Equal (new PointF (40f, 40f), points[lastStart]);
    }

    [Fact]
    public void Path_Overloads_BuildTheSamePoints ()
    {
        using var floatRect = new GraphicsPath ();
        floatRect.AddRoundedRectangle (new RectangleF (10, 10, 40, 20), 6f);
        using var intRect = new GraphicsPath ();
        intRect.AddRoundedRectangle (new Rectangle (10, 10, 40, 20), 6f);
        using var coords = new GraphicsPath ();
        coords.AddRoundedRectangle (10f, 10f, 40f, 20f, 6f);
        using var fourRadii = new GraphicsPath ();
        fourRadii.AddRoundedRectangle (new RectangleF (10, 10, 40, 20), 6f, 6f, 6f, 6f);
        using var intFourRadii = new GraphicsPath ();
        intFourRadii.AddRoundedRectangle (new Rectangle (10, 10, 40, 20), 6f, 6f, 6f, 6f);

        Assert.Equal (floatRect.PathPoints, intRect.PathPoints);
        Assert.Equal (floatRect.PathPoints, coords.PathPoints);
        Assert.Equal (floatRect.PathPoints, fourRadii.PathPoints);
        Assert.Equal (floatRect.PathPoints, intFourRadii.PathPoints);
    }

    [Theory]
    [InlineData (1)]
    [InlineData (2)]
    public void Path_FilledInGraphics_MatchesFillRoundedRectangle (int scale)
    {
        // The path form and the direct form share one definition of the geometry; a fill through each must agree.
        // Both are rasterised by Skia's anti-aliased path filler, so the comparison is exact rather than tolerant.
        var brush = new SolidBrush (Color.Red);
        using var direct = Render (60, 50, scale, g => g.FillRoundedRectangle (brush, new RectangleF (10, 10, 40, 30), 5f, 12f, 3f, 9f));
        using var viaPath = Render (60, 50, scale, g => {
            using var path = new GraphicsPath ();
            path.AddRoundedRectangle (new RectangleF (10, 10, 40, 30), 5f, 12f, 3f, 9f);
            g.FillPath (brush, path);
        });

        Assert.True (direct.Bytes.SequenceEqual (viaPath.Bytes), "GraphicsPath.AddRoundedRectangle and Graphics.FillRoundedRectangle disagree");
    }
}
