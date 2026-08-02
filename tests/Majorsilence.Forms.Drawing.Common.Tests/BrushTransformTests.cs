using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;
// See GraphicsPathTests for why this is aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;
using PointF = System.Drawing.PointF;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Covers the transform and clone surface added to the brush family in Phase 3 of docs/gdi-gap-plan.md.
///
/// As in <see cref="TextureBrushTests"/>, the transform cases render real pixels rather than only
/// round-tripping the matrix: a stored-but-unapplied transform is the failure mode these phases exist to
/// remove, and it is invisible to a property-only assertion.
/// </summary>
public class BrushTransformTests
{
    // Renders the brush over an 8x8 surface and returns the pixels.
    private static SKColor[,] Render (Brush brush, int size = 8)
    {
        using var surface = new SKBitmap (size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas (surface))
        using (var paint = brush.CreatePaint ())
        {
            canvas.Clear (SKColors.Black);
            canvas.DrawRect (new SKRect (0, 0, size, size), paint);
        }

        var pixels = new SKColor[size, size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[x, y] = surface.GetPixel (x, y);
        return pixels;
    }

    private static LinearGradientBrush NewGradient () =>
        new (new RectangleF (0, 0, 8, 0), Color.Red, Color.Lime);

    // ---- LinearGradientBrush ----

    [Fact]
    public void LinearGradientBrush_exposes_its_rectangle_and_colors ()
    {
        using var brush = new LinearGradientBrush (new RectangleF (2, 3, 20, 10), Color.Red, Color.Blue);

        Assert.Equal (new RectangleF (2, 3, 20, 10), brush.Rectangle);
        Assert.Equal (Color.Red, brush.LinearColors[0]);
        Assert.Equal (Color.Blue, brush.LinearColors[1]);
    }

    [Fact]
    public void LinearGradientBrush_LinearColors_setter_replaces_both_endpoints ()
    {
        using var brush = NewGradient ();
        brush.LinearColors = [Color.Blue, Color.Yellow];

        Assert.Equal (Color.Blue, brush.LinearColors[0]);
        Assert.Equal (Color.Yellow, brush.LinearColors[1]);
    }

    [Fact]
    public void LinearGradientBrush_LinearColors_setter_ignores_short_arrays ()
    {
        // GDI+ requires two colors; a one-element array must not half-apply.
        using var brush = NewGradient ();
        brush.LinearColors = [Color.Blue];

        Assert.Equal (Color.Red, brush.LinearColors[0]);
        Assert.Equal (Color.Lime, brush.LinearColors[1]);
    }

    [Fact]
    public void LinearGradientBrush_transform_actually_moves_the_gradient ()
    {
        using var brush = NewGradient ();
        var before = Render (brush);

        // Shift the ramp a long way left; the left edge should end up further along the gradient.
        brush.TranslateTransform (-4f, 0f);
        var after = Render (brush);

        Assert.NotEqual (before[0, 0], after[0, 0]);
    }

    [Fact]
    public void LinearGradientBrush_ResetTransform_restores_the_original_rendering ()
    {
        using var brush = NewGradient ();
        var original = Render (brush);

        brush.TranslateTransform (-4f, 0f);
        brush.ResetTransform ();

        Assert.True (brush.Transform.IsIdentity);
        Assert.Equal (original[0, 0], Render (brush)[0, 0]);
    }

    [Fact]
    public void LinearGradientBrush_Transform_property_round_trips_a_copy ()
    {
        using var brush = NewGradient ();
        using var matrix = new Matrix ();
        matrix.Translate (7f, 0f);

        brush.Transform = matrix;
        matrix.Translate (100f, 0f);   // must not leak into the brush

        Assert.Equal (7f, brush.Transform.OffsetX, 3);

        brush.Transform = null!;
        Assert.True (brush.Transform.IsIdentity);
    }

    [Fact]
    public void LinearGradientBrush_Clone_is_independent ()
    {
        using var brush = NewGradient ();
        brush.WrapMode = WrapMode.TileFlipX;
        brush.TranslateTransform (3f, 0f);

        using var clone = brush.Clone ();

        Assert.Equal (WrapMode.TileFlipX, clone.WrapMode);
        Assert.Equal (3f, clone.Transform.OffsetX, 3);
        Assert.Equal (brush.LinearColors[0], clone.LinearColors[0]);

        clone.TranslateTransform (50f, 0f);
        Assert.Equal (3f, brush.Transform.OffsetX, 3);
    }

    // ---- PathGradientBrush ----

    [Fact]
    public void PathGradientBrush_exposes_its_bounds_and_clones_independently ()
    {
        using var path = new GraphicsPath ();
        path.AddRectangle (new RectangleF (0, 0, 10, 10));
        using var brush = new PathGradientBrush (path) {
            CenterColor = Color.Red,
            SurroundColors = [Color.Blue],
            FocusScales = new PointF (0.5f, 0.5f),
            WrapMode = WrapMode.Clamp,
        };
        brush.ScaleTransform (2f, 2f);

        using var clone = brush.Clone ();

        Assert.Equal (new RectangleF (0, 0, 10, 10), clone.Rectangle);
        Assert.Equal (Color.Red, clone.CenterColor);
        Assert.Equal (new PointF (0.5f, 0.5f), clone.FocusScales);
        Assert.Equal (2f, clone.Transform.Elements[0], 3);

        clone.ScaleTransform (5f, 5f);
        Assert.Equal (2f, brush.Transform.Elements[0], 3);
    }

    // ---- Clone across the family ----

    [Fact]
    public void SolidBrush_Clone_copies_color_and_is_independent ()
    {
        using var brush = new SolidBrush (Color.Red);
        var clone = (SolidBrush)brush.Clone ();

        Assert.Equal (Color.Red, clone.Color);
        clone.Color = Color.Blue;
        Assert.Equal (Color.Red, brush.Color);
    }

    [Fact]
    public void HatchBrush_Clone_copies_style_and_colors ()
    {
        using var brush = new HatchBrush (HatchStyle.Cross, Color.Red, Color.Blue);
        var clone = brush.Clone ();

        Assert.Equal (HatchStyle.Cross, clone.HatchStyle);
        Assert.Equal (Color.Red, clone.ForegroundColor);
        Assert.Equal (Color.Blue, clone.BackgroundColor);
    }

    [Fact]
    public void Clone_is_reachable_through_the_Brush_base_for_every_brush_type ()
    {
        // System.Drawing declares Clone on Brush itself; code that holds a Brush must be able to call it.
        using var texture = new Bitmap (2, 2);
        var brushes = new Brush[] {
            new SolidBrush (Color.Red),
            new HatchBrush (HatchStyle.Cross, Color.Red),
            new LinearGradientBrush (new RectangleF (0, 0, 4, 0), Color.Red, Color.Lime),
            new TextureBrush (texture),
        };

        foreach (var brush in brushes) {
            var clone = brush.Clone ();
            Assert.NotNull (clone);
            Assert.NotSame (brush, clone);
            Assert.IsType (brush.GetType (), clone);
            brush.Dispose ();
        }
    }
}
