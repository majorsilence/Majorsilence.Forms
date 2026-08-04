using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;
// See GraphicsPathTests for why this is aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;
using PointF = System.Drawing.PointF;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Covers the Pen and Matrix members added in Phase 3 of docs/gdi-gap-plan.md.
/// </summary>
public class PenAndMatrixTests
{
    // ---- Pen ----

    [Fact]
    public void Pen_from_a_solid_brush_takes_its_color ()
    {
        using var brush = new SolidBrush (Color.Red);
        using var pen = new Pen (brush, 2f);

        Assert.Equal (Color.Red, pen.Color);
        Assert.Equal (PenType.SolidColor, pen.PenType);
    }

    [Theory]
    [InlineData (typeof (HatchBrush), PenType.HatchFill)]
    [InlineData (typeof (LinearGradientBrush), PenType.LinearGradient)]
    [InlineData (typeof (PathGradientBrush), PenType.PathGradient)]
    public void PenType_reflects_the_brush_it_strokes_with (Type brushType, PenType expected)
    {
        Brush brush = brushType == typeof (HatchBrush)
            ? new HatchBrush (HatchStyle.Cross, Color.Red)
            : brushType == typeof (LinearGradientBrush)
                ? new LinearGradientBrush (new RectangleF (0, 0, 4, 0), Color.Red, Color.Lime)
                : new PathGradientBrush ([new PointF (0, 0), new PointF (4, 0), new PointF (4, 4)]);

        using var pen = new Pen (brush, 1f);
        Assert.Equal (expected, pen.PenType);
        brush.Dispose ();
    }

    [Fact]
    public void A_gradient_brush_pen_strokes_with_that_gradient_rather_than_a_flat_color ()
    {
        // The whole point of Pen.Brush: before it, a non-solid brush collapsed to Color.Black.
        using var brush = new LinearGradientBrush (new RectangleF (0, 0, 16, 0), Color.Red, Color.Lime);
        using var pen = new Pen (brush, 4f);
        using var paint = pen.CreatePaint ();

        Assert.NotNull (paint.Shader);
        Assert.Equal (SKPaintStyle.Stroke, paint.Style);
    }

    [Fact]
    public void A_solid_brush_pen_uses_its_color_and_no_shader ()
    {
        using var brush = new SolidBrush (Color.Red);
        using var pen = new Pen (brush, 4f);
        using var paint = pen.CreatePaint ();

        Assert.Null (paint.Shader);
        Assert.Equal (new SKColor (255, 0, 0, 255), paint.Color);
    }

    [Fact]
    public void SetLineCap_sets_all_three_caps ()
    {
        using var pen = new Pen (Color.Red);
        pen.SetLineCap (LineCap.Round, LineCap.Square, DashCap.Triangle);

        Assert.Equal (LineCap.Round, pen.StartCap);
        Assert.Equal (LineCap.Square, pen.EndCap);
        Assert.Equal (DashCap.Triangle, pen.DashCap);
    }

    [Fact]
    public void Pen_transform_round_trips_and_resets ()
    {
        using var pen = new Pen (Color.Red);
        pen.TranslateTransform (5f, 9f);

        Assert.Equal (5f, pen.Transform.OffsetX, 3);
        Assert.Equal (9f, pen.Transform.OffsetY, 3);

        pen.ResetTransform ();
        Assert.True (pen.Transform.IsIdentity);
    }

    [Fact]
    public void Pen_Clone_carries_the_Phase3_state ()
    {
        using var brush = new HatchBrush (HatchStyle.Cross, Color.Red);
        using var pen = new Pen (brush, 3f) {
            DashCap = DashCap.Round,
            CompoundArray = [0f, 0.5f],
        };

        using var clone = pen.Clone ();

        Assert.Equal (DashCap.Round, clone.DashCap);
        Assert.Equal (PenType.HatchFill, clone.PenType);
        Assert.Equal ([0f, 0.5f], clone.CompoundArray!);

        // The array must be copied, not shared.
        clone.CompoundArray![0] = 0.25f;
        Assert.Equal (0f, pen.CompoundArray![0]);
    }

    // ---- Matrix ----

    [Fact]
    public void OffsetX_and_OffsetY_expose_the_translation_components ()
    {
        using var matrix = new Matrix ();
        matrix.Translate (12f, -7f);

        Assert.Equal (12f, matrix.OffsetX, 3);
        Assert.Equal (-7f, matrix.OffsetY, 3);
        // They must agree with the element array's dx/dy slots.
        Assert.Equal (matrix.Elements[4], matrix.OffsetX, 3);
        Assert.Equal (matrix.Elements[5], matrix.OffsetY, 3);
    }

    [Fact]
    public void Shear_skews_the_matrix ()
    {
        using var matrix = new Matrix ();
        matrix.Shear (2f, 0f);

        var points = new[] { new PointF (0f, 1f) };
        matrix.TransformPoints (points);

        // A horizontal shear of 2 moves a point at y=1 across by 2.
        Assert.Equal (2f, points[0].X, 3);
        Assert.Equal (1f, points[0].Y, 3);
    }

    [Fact]
    public void VectorTransformPoints_ignores_translation_but_TransformPoints_does_not ()
    {
        // This is the entire distinction between transforming a position and a direction.
        using var matrix = new Matrix ();
        matrix.Translate (100f, 100f);
        matrix.Scale (2f, 2f);

        var asPosition = new[] { new PointF (3f, 4f) };
        var asVector = new[] { new PointF (3f, 4f) };

        matrix.TransformPoints (asPosition);
        matrix.VectorTransformPoints (asVector);

        Assert.Equal (106f, asPosition[0].X, 3);
        Assert.Equal (108f, asPosition[0].Y, 3);
        Assert.Equal (6f, asVector[0].X, 3);
        Assert.Equal (8f, asVector[0].Y, 3);
    }
}
