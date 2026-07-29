using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
// See GraphicsPathTests for why these are aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class BlendTests
{
    [Fact]
    public void Blend_DefaultConstructor_HasOneSlot()
    {
        var blend = new Blend();

        Assert.Single(blend.Factors);
        Assert.Single(blend.Positions);
        Assert.Equal(0f, blend.Factors[0]);
    }

    [Fact]
    public void Blend_CountConstructor_SizesBothArrays()
    {
        var blend = new Blend(4);

        Assert.Equal(4, blend.Factors.Length);
        Assert.Equal(4, blend.Positions.Length);
    }

    [Fact]
    public void Blend_NegativeCount_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new Blend(-1));

    [Fact]
    public void ColorBlend_CountConstructor_SizesBothArrays()
    {
        var blend = new ColorBlend(3);

        Assert.Equal(3, blend.Colors.Length);
        Assert.Equal(3, blend.Positions.Length);
    }

    [Fact]
    public void ColorBlend_NegativeCount_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ColorBlend(-1));
}

public class GradientBlendShapeTests
{
    private static LinearGradientBrush MakeBrush()
        => new LinearGradientBrush(new RectangleF(0, 0, 100, 10), Color.Red, Color.Blue);

    [Fact]
    public void SetBlendTriangularShape_PeaksAtTheFocus()
    {
        using var brush = MakeBrush();
        brush.SetBlendTriangularShape(0.5f);

        var blend = brush.Blend!;
        Assert.Equal(new[] { 0f, 0.5f, 1f }, blend.Positions);
        Assert.Equal(new[] { 0f, 1f, 0f }, blend.Factors);
    }

    [Fact]
    public void SetBlendTriangularShape_HonoursScale()
    {
        using var brush = MakeBrush();
        brush.SetBlendTriangularShape(0.25f, 0.6f);

        var blend = brush.Blend!;
        Assert.Equal(0.25f, blend.Positions[1]);
        Assert.Equal(0.6f, blend.Factors[1]);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void SetBlendTriangularShape_DegenerateFocus_ProducesAStraightRamp(float focus)
    {
        using var brush = MakeBrush();
        brush.SetBlendTriangularShape(focus);

        var blend = brush.Blend!;
        Assert.Equal(2, blend.Factors.Length);
        Assert.Equal(new[] { 0f, 1f }, blend.Positions);
    }

    [Fact]
    public void SetBlendTriangularShape_ExpandsIntoColorStops()
    {
        using var brush = MakeBrush();
        brush.SetBlendTriangularShape(0.5f);

        var colors = brush.InterpolationColors!;
        Assert.Equal(3, colors.Colors.Length);
        Assert.Equal(Color.Red.ToArgb(), colors.Colors[0].ToArgb());   // factor 0 => start color
        Assert.Equal(Color.Blue.ToArgb(), colors.Colors[1].ToArgb());  // factor 1 => end color
        Assert.Equal(Color.Red.ToArgb(), colors.Colors[2].ToArgb());
    }

    [Fact]
    public void SetSigmaBellShape_StartsAndEndsAtZeroAndPeaksAtTheFocus()
    {
        using var brush = MakeBrush();
        brush.SetSigmaBellShape(0.5f);

        var blend = brush.Blend!;
        Assert.Equal(0f, blend.Factors[0]);
        Assert.Equal(0f, blend.Factors[^1]);
        Assert.Equal(0f, blend.Positions[0]);
        Assert.Equal(1f, blend.Positions[^1]);
        Assert.Equal(1f, blend.Factors.Max(), 2);
    }

    [Fact]
    public void SetSigmaBellShape_RisesMonotonicallyToTheFocus()
    {
        using var brush = MakeBrush();
        brush.SetSigmaBellShape(0.5f);

        var blend = brush.Blend!;
        var peak = Array.IndexOf(blend.Factors, blend.Factors.Max());

        for (var i = 1; i <= peak; i++)
            Assert.True(blend.Factors[i] >= blend.Factors[i - 1], $"factor dropped at {i}");
        for (var i = peak + 1; i < blend.Factors.Length; i++)
            Assert.True(blend.Factors[i] <= blend.Factors[i - 1], $"factor rose at {i}");
    }

    [Fact]
    public void SetSigmaBellShape_IsGentlerThanTriangularNearTheEnds()
    {
        using var brush = MakeBrush();
        brush.SetSigmaBellShape(0.5f);

        var sigma = brush.Blend!;
        var index = sigma.Factors.Length / 10;

        // A tenth of the way along, the triangular ramp is already at ~0.2 (twice the position,
        // since it peaks at 0.5). The Gaussian leaves the endpoint with near-zero slope, so it is
        // still far below that.
        var linear = 2f * sigma.Positions[index];
        Assert.True(sigma.Factors[index] < linear / 2f,
            $"expected a gentler start than {linear}, got {sigma.Factors[index]}");
    }

    [Fact]
    public void SetSigmaBellShape_HonoursScale()
    {
        using var brush = MakeBrush();
        brush.SetSigmaBellShape(0.5f, 0.4f);

        Assert.Equal(0.4f, brush.Blend!.Factors.Max(), 2);
    }

    [Fact]
    public void SettingBlend_ReplacesTheInterpolationColorRamp()
    {
        using var brush = MakeBrush();
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { Color.Green, Color.Yellow },
            Positions = new[] { 0f, 1f },
        };

        brush.SetBlendTriangularShape(0.5f);

        Assert.Equal(3, brush.InterpolationColors!.Colors.Length);
        Assert.Equal(Color.Red.ToArgb(), brush.InterpolationColors.Colors[0].ToArgb());
    }

    [Fact]
    public void SettingInterpolationColors_ClearsTheBlend()
    {
        using var brush = MakeBrush();
        brush.SetBlendTriangularShape(0.5f);

        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { Color.Green, Color.Yellow },
            Positions = new[] { 0f, 1f },
        };

        Assert.Null(brush.Blend);
        Assert.Equal(2, brush.InterpolationColors!.Colors.Length);
    }

    [Fact]
    public void InterpolationColors_WithoutPositions_ReportsEvenSpacing()
    {
        using var brush = MakeBrush();
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { Color.Red, Color.Green, Color.Blue },
            Positions = Array.Empty<float>(),
        };

        Assert.Equal(new[] { 0f, 0.5f, 1f }, brush.InterpolationColors!.Positions);
    }

    [Fact]
    public void InterpolationPositions_SharesStorageWithInterpolationColors()
    {
        using var brush = MakeBrush();
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { Color.Red, Color.Green, Color.Blue },
            Positions = new[] { 0f, 0.25f, 1f },
        };

        Assert.Equal(new[] { 0f, 0.25f, 1f }, brush.InterpolationPositions);
    }

    [Fact]
    public void BlendedBrush_ProducesAUsablePaint()
    {
        using var brush = MakeBrush();
        brush.SetSigmaBellShape(0.5f);

        using var paint = brush.CreatePaint();
        Assert.NotNull(paint.Shader);
    }

    [Fact]
    public void TriangularBrush_RendersTheEndColorAtTheFocus()
    {
        using var bitmap = new SkiaSharp.SKBitmap(101, 5, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.White);

        using var brush = new LinearGradientBrush(new RectangleF(0, 0, 100, 5), Color.Red, Color.Blue);
        brush.SetBlendTriangularShape(0.5f);
        using var paint = brush.CreatePaint();
        canvas.DrawRect(new SkiaSharp.SKRect(0, 0, 101, 5), paint);
        canvas.Flush();

        var middle = bitmap.GetPixel(50, 2);
        var left = bitmap.GetPixel(0, 2);

        Assert.True(middle.Blue > middle.Red, "the focus should be near the ending (blue) color");
        Assert.True(left.Red > left.Blue, "the start should be near the starting (red) color");
    }

    [Fact]
    public void PathGradientBrush_SupportsTheSameBlendShapes()
    {
        using var brush = new PathGradientBrush(new[]
        {
            new PointF(0, 0), new PointF(20, 0), new PointF(20, 20), new PointF(0, 20),
        })
        {
            CenterColor = Color.Red,
            SurroundColors = new[] { Color.Blue },
        };

        brush.SetBlendTriangularShape(0.5f);

        Assert.NotNull(brush.Blend);
        Assert.Equal(3, brush.InterpolationColors!.Colors.Length);

        using var paint = brush.CreatePaint();
        Assert.NotNull(paint.Shader);
    }

    [Fact]
    public void PathGradientBrush_InterpolationColors_RoundTrip()
    {
        using var brush = new PathGradientBrush(new[] { new PointF(0, 0), new PointF(10, 10) });
        brush.InterpolationColors = new ColorBlend
        {
            Colors = new[] { Color.Red, Color.Lime, Color.Blue },
            Positions = new[] { 0f, 0.5f, 1f },
        };

        Assert.Equal(3, brush.InterpolationColors!.Colors.Length);
        Assert.Null(brush.Blend);
    }
}
