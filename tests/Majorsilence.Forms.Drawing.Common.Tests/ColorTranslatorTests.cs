using Majorsilence.Forms.Drawing;

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

    // 8-char hex is #AARRGGBB (alpha first), matching System.Drawing.ColorTranslator -- the type this
    // class replaces. (ToHtml never emits this form: like upstream it drops alpha -- GFX-40.)
    // The original Reporting test asserted the CSS #RRGGBBAA order against its own Color type; that
    // convention was dropped along with that type when the drawing projects were consolidated.
    [Theory]
    [InlineData("#80FF0000", 255,   0,   0, 128)]
    [InlineData("#7F123456",  18,  52,  86, 127)]
    public void FromHtml_EightCharHex_ParsesAsAaRrGgBb(string html, int r, int g, int b, int a)
    {
        var color = ColorTranslator.FromHtml(html);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.Equal(a, color.A);
    }

    // Measured from System.Drawing.ColorTranslator.FromHtml on .NET 10. Without a '#', six digits are a DECIMAL
    // number through the colour converter: "123456" is 0x0001E240, alpha 0 -- not #123456.
    [Fact]
    public void FromHtml_DigitsWithoutHash_AreADecimalNumber()
    {
        var color = ColorTranslator.FromHtml("123456");
        Assert.Equal((0, 1, 226, 64), (color.A, color.R, color.G, color.B));
    }

    [Fact]
    public void FromHtml_HexWithoutHash_IsNotAColour()
    {
        Assert.Throws<ArgumentException>(() => ColorTranslator.FromHtml("FF5733"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FromHtml_NullEmptyOrBlank_IsEmpty(string? html)
    {
        Assert.True(ColorTranslator.FromHtml(html!).IsEmpty);
    }

    [Theory]
    [InlineData("buttonface", "Control")]
    [InlineData("ButtonFace", "Control")]
    [InlineData("infotext", "InfoText")]
    [InlineData("infobackground", "Info")]
    [InlineData("threedhighlight", "ControlLight")]
    public void FromHtml_SystemColourKeyword_IsThatSystemColour(string html, string name)
    {
        Assert.Equal(System.Drawing.Color.FromName(name), ColorTranslator.FromHtml(html));
    }

    [Theory]
    [InlineData("LightGrey")]
    [InlineData("lightgrey")]
    public void FromHtml_LightGrey_IsLightGray(string html)
    {
        Assert.Equal(System.Drawing.Color.LightGray, ColorTranslator.FromHtml(html));
    }

    [Fact]
    public void FromHtml_NameInAnyCase_IsTheKnownColour()
    {
        Assert.Equal(System.Drawing.Color.Red, ColorTranslator.FromHtml("red"));
    }

    [Fact]
    public void FromHtml_InvalidHexDigit_IsAFormatException()
    {
        Assert.Throws<FormatException>(() => ColorTranslator.FromHtml("#GGGGGG"));
    }

    [Fact]
    public void FromHtml_UnknownName_IsAnArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ColorTranslator.FromHtml("notacolour"));
    }
}
