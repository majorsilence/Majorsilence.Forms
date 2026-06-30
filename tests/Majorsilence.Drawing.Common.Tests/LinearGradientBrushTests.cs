using Majorsilence.Drawing;
using Majorsilence.Drawing.Drawing2D;

namespace Majorsilence.Drawing.Common.Tests;

public class LinearGradientBrushTests
{
    [Fact]
    public void Constructor_SingleColor_DoesNotThrow()
    {
        using var brush = new LinearGradientBrush(Color.Red);
        Assert.False(brush.GammaCorrection);
    }

    [Fact]
    public void Constructor_TwoPoints_DoesNotThrow()
    {
        using var brush = new LinearGradientBrush(
            new PointF(0f, 0f), new PointF(100f, 0f),
            Color.Red, Color.Blue);
        Assert.NotNull(brush);
    }

    [Fact]
    public void Constructor_IntPoints_DoesNotThrow()
    {
        using var brush = new LinearGradientBrush(
            new Point(0, 0), new Point(100, 0),
            Color.Red, Color.Blue);
        Assert.NotNull(brush);
    }

    [Theory]
    [InlineData(LinearGradientMode.Horizontal)]
    [InlineData(LinearGradientMode.Vertical)]
    [InlineData(LinearGradientMode.ForwardDiagonal)]
    [InlineData(LinearGradientMode.BackwardDiagonal)]
    public void Constructor_WithMode_DoesNotThrow(LinearGradientMode mode)
    {
        using var brush = new LinearGradientBrush(
            new RectangleF(0f, 0f, 100f, 100f),
            Color.Red, Color.Blue, mode);
        Assert.NotNull(brush);
    }

    [Fact]
    public void Constructor_WithAngle_DoesNotThrow()
    {
        using var brush = new LinearGradientBrush(
            new RectangleF(0f, 0f, 200f, 100f),
            Color.Green, Color.Yellow, 45f);
        Assert.NotNull(brush);
    }

    [Fact]
    public void Constructor_IntRectWithMode_DoesNotThrow()
    {
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, 100, 100),
            Color.Red, Color.Blue, LinearGradientMode.Vertical);
        Assert.NotNull(brush);
    }

    [Fact]
    public void Constructor_IntRectWithAngle_DoesNotThrow()
    {
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, 100, 100),
            Color.Red, Color.Blue, 90f);
        Assert.NotNull(brush);
    }

    [Fact]
    public void GammaCorrection_RoundTrips()
    {
        using var brush = new LinearGradientBrush(Color.Red);
        brush.GammaCorrection = true;
        Assert.True(brush.GammaCorrection);
        brush.GammaCorrection = false;
        Assert.False(brush.GammaCorrection);
    }

    [Fact]
    public void InterpolationColors_SetValidArray_RoundTrips()
    {
        using var brush = new LinearGradientBrush(
            new PointF(0f, 0f), new PointF(100f, 0f),
            Color.Red, Color.Blue);

        var colors = new[] { Color.Red, Color.Green, Color.Blue };
        brush.InterpolationColors = colors;

        Assert.Equal(3, brush.InterpolationColors!.Length);
    }

    [Fact]
    public void InterpolationPositions_SetValidArray_RoundTrips()
    {
        using var brush = new LinearGradientBrush(
            new PointF(0f, 0f), new PointF(100f, 0f),
            Color.Red, Color.Blue);

        brush.InterpolationPositions = new[] { 0f, 0.5f, 1f };
        Assert.Equal(3, brush.InterpolationPositions!.Length);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var brush = new LinearGradientBrush(Color.Red);
        brush.Dispose();
    }
}
