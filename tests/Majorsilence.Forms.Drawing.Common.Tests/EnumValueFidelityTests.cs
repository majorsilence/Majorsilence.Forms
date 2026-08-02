using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Pins the numeric values of enums shared with <c>System.Drawing</c>, from Phase 2 of
/// docs/gdi-gap-plan.md.
///
/// These values are API, not implementation detail: designer-generated code and <c>.resx</c> resources
/// persist them as raw integers, so renumbering one silently corrupts data on round-trip rather than
/// breaking the build. `tools/Majorsilence.Forms.GdiDiff` now checks the whole set mechanically against
/// upstream on every CI run; this file guards the specific values that were found *wrong* and fixed, so
/// a regression names itself instead of showing up as an anonymous VALUE line.
/// </summary>
public class EnumValueFidelityTests
{
    // StringFormatFlags.DirectionRightToLeft and DirectionVertical were transposed. This is the most
    // consequential of the fixes: both are honored at layout time, so the swap turned right-to-left
    // text into vertical text.
    [Fact]
    public void StringFormatFlags_direction_values_are_not_transposed ()
    {
        Assert.Equal (1, (int)StringFormatFlags.DirectionRightToLeft);
        Assert.Equal (2, (int)StringFormatFlags.DirectionVertical);
    }

    [Theory]
    [InlineData (StringFormatFlags.FitBlackBox, 4)]
    [InlineData (StringFormatFlags.DisplayFormatControl, 32)]
    [InlineData (StringFormatFlags.NoFontFallback, 1024)]
    [InlineData (StringFormatFlags.MeasureTrailingSpaces, 2048)]
    [InlineData (StringFormatFlags.NoWrap, 4096)]
    public void StringFormatFlags_values_match_GDI_plus (StringFormatFlags flag, int expected)
        => Assert.Equal (expected, (int)flag);

    // RotateFlipType's second half are aliases of the first half in GDI+, not distinct values.
    [Fact]
    public void RotateFlipType_aliases_share_values_with_their_equivalents ()
    {
        Assert.Equal ((int)RotateFlipType.RotateNoneFlipNone, (int)RotateFlipType.Rotate180FlipXY);
        Assert.Equal ((int)RotateFlipType.Rotate90FlipNone, (int)RotateFlipType.Rotate270FlipXY);
        Assert.Equal ((int)RotateFlipType.Rotate180FlipNone, (int)RotateFlipType.RotateNoneFlipXY);
        Assert.Equal ((int)RotateFlipType.RotateNoneFlipX, (int)RotateFlipType.Rotate180FlipY);
        Assert.Equal ((int)RotateFlipType.Rotate180FlipX, (int)RotateFlipType.RotateNoneFlipY);
    }

    [Theory]
    [InlineData (PixelFormat.Format24bppRgb, 137224)]
    [InlineData (PixelFormat.Format32bppArgb, 2498570)]
    [InlineData (PixelFormat.Format1bppIndexed, 196865)]
    [InlineData (PixelFormat.Format16bppGrayScale, 1052676)]
    public void PixelFormat_values_match_GDI_plus (PixelFormat format, int expected)
        => Assert.Equal (expected, (int)format);

    [Theory]
    [InlineData (LineCap.ArrowAnchor, 20)]
    [InlineData (LineCap.Custom, 255)]
    [InlineData (LineCap.AnchorMask, 240)]
    public void LineCap_anchor_values_match_GDI_plus (LineCap cap, int expected)
        => Assert.Equal (expected, (int)cap);

    [Theory]
    [InlineData (DashCap.Flat, 0)]
    [InlineData (DashCap.Round, 2)]
    [InlineData (DashCap.Triangle, 3)]
    public void DashCap_values_match_GDI_plus (DashCap cap, int expected)
        => Assert.Equal (expected, (int)cap);

    [Fact]
    public void Invalid_sentinels_are_negative_one ()
    {
        Assert.Equal (-1, (int)SmoothingMode.Invalid);
        Assert.Equal (-1, (int)PixelOffsetMode.Invalid);
        Assert.Equal (-1, (int)QualityMode.Invalid);
    }
}
