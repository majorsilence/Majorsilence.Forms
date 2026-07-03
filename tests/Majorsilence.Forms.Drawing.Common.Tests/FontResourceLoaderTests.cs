using Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class FontResourceLoaderTests
{
    [Fact]
    public void GetCssFontStack_Arial_ContainsSansSerifGeneric()
    {
        var stack = FontResourceLoader.GetCssFontStack("Arial");
        Assert.EndsWith("sans-serif", stack);
    }

    [Fact]
    public void GetCssFontStack_Arial_StartsWithArial()
    {
        var stack = FontResourceLoader.GetCssFontStack("Arial");
        Assert.StartsWith("Arial", stack);
    }

    [Fact]
    public void GetCssFontStack_Arial_IncludesLiberationSans()
    {
        var stack = FontResourceLoader.GetCssFontStack("Arial");
        Assert.Contains("Liberation Sans", stack);
    }

    [Fact]
    public void GetCssFontStack_TimesNewRoman_UsesSerifGeneric()
    {
        var stack = FontResourceLoader.GetCssFontStack("Times New Roman");
        Assert.EndsWith("serif", stack);
        Assert.DoesNotContain("sans-serif", stack);
    }

    [Fact]
    public void GetCssFontStack_TimesNewRoman_IncludesLiberationSerif()
    {
        var stack = FontResourceLoader.GetCssFontStack("Times New Roman");
        Assert.Contains("Liberation Serif", stack);
    }

    [Fact]
    public void GetCssFontStack_CourierNew_UsesMonospaceGeneric()
    {
        var stack = FontResourceLoader.GetCssFontStack("Courier New");
        Assert.EndsWith("monospace", stack);
    }

    [Fact]
    public void GetCssFontStack_UnknownFont_ReturnsFontPlusSansSerif()
    {
        var stack = FontResourceLoader.GetCssFontStack("MyCustomFont");
        Assert.StartsWith("MyCustomFont", stack);
        Assert.EndsWith("sans-serif", stack);
    }

    [Fact]
    public void GetCssFontStack_MultiWordName_IsQuotedInOutput()
    {
        // "Liberation Sans" has a space, so must appear quoted in the CSS stack
        var stack = FontResourceLoader.GetCssFontStack("Arial");
        Assert.Contains("'Liberation Sans'", stack);
    }

    [Fact]
    public void GetCssFontStack_CssFontFamilyString_UsesFirstFamily()
    {
        // When given a full CSS stack, only the first family should be used as primary
        var stack = FontResourceLoader.GetCssFontStack("Arial, Helvetica, sans-serif");
        Assert.StartsWith("Arial", stack);
    }

    [Fact]
    public void GetFontDirectory_ReturnsExistingDirectory()
    {
        var dir = FontResourceLoader.GetFontDirectory();
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void GetFontDirectory_CalledTwice_ReturnsSamePath()
    {
        var first  = FontResourceLoader.GetFontDirectory();
        var second = FontResourceLoader.GetFontDirectory();
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetFontDirectory_ContainsTtfFiles()
    {
        var dir  = FontResourceLoader.GetFontDirectory();
        var ttfs = Directory.GetFiles(dir, "*.ttf");
        Assert.True(ttfs.Length > 0, "Font directory should contain at least one .ttf file");
    }
}
