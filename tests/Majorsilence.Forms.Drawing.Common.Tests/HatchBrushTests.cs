using Majorsilence.Drawing;
using Majorsilence.Drawing.Drawing2D;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class HatchBrushTests
{
    [Fact]
    public void Constructor_StoresColors()
    {
        using var brush = new HatchBrush(HatchStyle.Horizontal, Color.Red, Color.White);
        Assert.Equal(Color.Red,   brush.ForegroundColor);
        Assert.Equal(Color.White, brush.BackgroundColor);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var brush = new HatchBrush(HatchStyle.Cross, Color.Black, Color.White);
        brush.Dispose(); // must not throw
    }

    // Verify that all HatchStyle variants construct without throwing.
    // The switch in CreateHatchPaint has a default case so unknown values also work.
    [Theory]
    [InlineData(HatchStyle.Horizontal)]
    [InlineData(HatchStyle.Vertical)]
    [InlineData(HatchStyle.ForwardDiagonal)]
    [InlineData(HatchStyle.BackwardDiagonal)]
    [InlineData(HatchStyle.Cross)]
    [InlineData(HatchStyle.DiagonalCross)]
    [InlineData(HatchStyle.Percent05)]
    [InlineData(HatchStyle.Percent10)]
    [InlineData(HatchStyle.Percent20)]
    [InlineData(HatchStyle.Percent25)]
    [InlineData(HatchStyle.Percent30)]
    [InlineData(HatchStyle.Percent40)]
    [InlineData(HatchStyle.Percent50)]
    [InlineData(HatchStyle.Percent60)]
    [InlineData(HatchStyle.Percent70)]
    [InlineData(HatchStyle.Percent75)]
    [InlineData(HatchStyle.Percent80)]
    [InlineData(HatchStyle.Percent90)]
    [InlineData(HatchStyle.LightHorizontal)]
    [InlineData(HatchStyle.NarrowHorizontal)]
    [InlineData(HatchStyle.DarkHorizontal)]
    [InlineData(HatchStyle.LightVertical)]
    [InlineData(HatchStyle.NarrowVertical)]
    [InlineData(HatchStyle.DarkVertical)]
    [InlineData(HatchStyle.LightDownwardDiagonal)]
    [InlineData(HatchStyle.DarkDownwardDiagonal)]
    [InlineData(HatchStyle.WideDownwardDiagonal)]
    [InlineData(HatchStyle.LightUpwardDiagonal)]
    [InlineData(HatchStyle.DarkUpwardDiagonal)]
    [InlineData(HatchStyle.WideUpwardDiagonal)]
    [InlineData(HatchStyle.LargeCheckerBoard)]
    [InlineData(HatchStyle.SmallCheckerBoard)]
    [InlineData(HatchStyle.SmallGrid)]
    [InlineData(HatchStyle.Trellis)]
    [InlineData(HatchStyle.OutlinedDiamond)]
    [InlineData(HatchStyle.SolidDiamond)]
    [InlineData(HatchStyle.DottedGrid)]
    [InlineData(HatchStyle.ZigZag)]
    [InlineData(HatchStyle.Wave)]
    [InlineData(HatchStyle.DashedHorizontal)]
    [InlineData(HatchStyle.DashedVertical)]
    [InlineData(HatchStyle.DashedDownwardDiagonal)]
    [InlineData(HatchStyle.DashedUpwardDiagonal)]
    public void Constructor_AllHatchStyles_DoesNotThrow(HatchStyle style)
    {
        using var brush = new HatchBrush(style, Color.Black, Color.White);
        Assert.Equal(Color.Black, brush.ForegroundColor);
        Assert.Equal(Color.White, brush.BackgroundColor);
    }

    [Fact]
    public void Constructor_SemiTransparentColors_DoesNotThrow()
    {
        var fore = new Color(255, 0, 0, 128);
        var back = new Color(0, 0, 255, 64);
        using var brush = new HatchBrush(HatchStyle.DiagonalCross, fore, back);
        Assert.Equal(fore, brush.ForegroundColor);
        Assert.Equal(back, brush.BackgroundColor);
    }
}
