using Majorsilence.Drawing;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class ColorTranslatorTests
{
    // Migrated from Reporting/ReportTests/MajorsilenceDarawingColorTranslatorTests.cs
    [Theory]
    [InlineData("Bisque", 255, 228, 196)]
    [InlineData("Red",    255,   0,   0)]
    [InlineData("White",  255, 255, 255)]
    [InlineData("Black",    0,   0,   0)]
    [InlineData("Blue",     0,   0, 255)]
    [InlineData("Green",    0, 128,   0)]
    public void FromHtml_NamedColor_ReturnsExpectedRgb(string name, int r, int g, int b)
    {
        var color = ColorTranslator.FromHtml(name);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    // 3-char hex: each digit doubled (#F00 → #FF0000)
    [Theory]
    [InlineData("#F00", 255,   0,   0)]
    [InlineData("#0F0",   0, 255,   0)]
    [InlineData("#00F",   0,   0, 255)]
    [InlineData("#FFF", 255, 255, 255)]
    [InlineData("#000",   0,   0,   0)]
    public void FromHtml_ThreeCharHex_ExpandsEachDigit(string html, int r, int g, int b)
    {
        var color = ColorTranslator.FromHtml(html);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.Equal(255, color.A);
    }

    // 6-char hex: #RRGGBB, fully opaque
    [Theory]
    [InlineData("#FF0000", 255,   0,   0)]
    [InlineData("#00FF00",   0, 255,   0)]
    [InlineData("#0000FF",   0,   0, 255)]
    [InlineData("#FF5733", 255,  87,  51)]
    [InlineData("#123456",  18,  52,  86)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("#000000",   0,   0,   0)]
    public void FromHtml_SixCharHex_ReturnsExpectedRgbFullyOpaque(string html, int r, int g, int b)
    {
        var color = ColorTranslator.FromHtml(html);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.Equal(255, color.A);
    }

    // 8-char hex: #RRGGBBAA — migrated from Reporting test (original used Majorsilence.Drawing.Color)
    [Theory]
    [InlineData("#80FF0000", 128, 255,   0,   0)]
    [InlineData("#7F123456", 127,  18,  52,  86)]
    public void FromHtml_EightCharHex_ParsesAsRrGgBbAa(string html, int r, int g, int b, int a)
    {
        var color = ColorTranslator.FromHtml(html);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.Equal(a, color.A);
    }

    [Fact]
    public void FromHtml_WithoutLeadingHash_SameAsWithHash()
    {
        var withHash    = ColorTranslator.FromHtml("#FF5733");
        var withoutHash = ColorTranslator.FromHtml("FF5733");
        Assert.Equal(withHash.R, withoutHash.R);
        Assert.Equal(withHash.G, withoutHash.G);
        Assert.Equal(withHash.B, withoutHash.B);
    }

    [Fact]
    public void FromHtml_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ColorTranslator.FromHtml(string.Empty));
    }

    [Fact]
    public void FromHtml_NullString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ColorTranslator.FromHtml(null!));
    }
}
