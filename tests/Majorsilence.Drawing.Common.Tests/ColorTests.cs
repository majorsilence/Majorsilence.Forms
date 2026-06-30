using Majorsilence.Drawing;

namespace Majorsilence.Drawing.Common.Tests;

public class ColorTests
{
    [Fact]
    public void Constructor_RgbOnly_DefaultsAlphaTo255()
    {
        var c = new Color(10, 20, 30);
        Assert.Equal(10, c.R);
        Assert.Equal(20, c.G);
        Assert.Equal(30, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void Constructor_WithAlpha_SetsAllComponents()
    {
        var c = new Color(10, 20, 30, 128);
        Assert.Equal(10,  c.R);
        Assert.Equal(20,  c.G);
        Assert.Equal(30,  c.B);
        Assert.Equal(128, c.A);
    }

    [Fact]
    public void Empty_AllComponentsZero()
    {
        var e = Color.Empty;
        Assert.Equal(0, e.R);
        Assert.Equal(0, e.G);
        Assert.Equal(0, e.B);
        Assert.Equal(0, e.A);
    }

    [Fact]
    public void IsEmpty_EmptyColor_ReturnsTrue()
    {
        Assert.True(Color.Empty.IsEmpty);
    }

    [Fact]
    public void IsEmpty_NonZeroColor_ReturnsFalse()
    {
        Assert.False(Color.Red.IsEmpty);
        Assert.False(Color.Black.IsEmpty);
    }

    [Theory]
    [InlineData("Red",   255,   0,   0)]
    [InlineData("Blue",    0,   0, 255)]
    [InlineData("White", 255, 255, 255)]
    [InlineData("Black",   0,   0,   0)]
    [InlineData("Bisque", 255, 228, 196)]
    [InlineData("Green",   0, 128,   0)]
    public void FromName_KnownColor_ReturnsExpectedRgb(string name, int r, int g, int b)
    {
        var c = Color.FromName(name);
        Assert.Equal(r, c.R);
        Assert.Equal(g, c.G);
        Assert.Equal(b, c.B);
    }

    [Fact]
    public void FromName_UnknownColor_ReturnsEmpty()
    {
        Assert.True(Color.FromName("NotARealColor").IsEmpty);
    }

    [Fact]
    public void FromName_CaseInsensitive()
    {
        var lower = Color.FromName("red");
        var upper = Color.FromName("RED");
        Assert.Equal(lower.R, upper.R);
        Assert.Equal(lower.G, upper.G);
        Assert.Equal(lower.B, upper.B);
    }

    [Fact]
    public void FromArgb_SetsAllComponents()
    {
        var c = Color.FromArgb(128, 255, 0, 0);
        Assert.Equal(255, c.R);
        Assert.Equal(0,   c.G);
        Assert.Equal(0,   c.B);
        Assert.Equal(128, c.A);
    }

    [Fact]
    public void EqualityOperator_SameValues_Equal()
    {
        var a = new Color(100, 200, 50, 128);
        var b = new Color(100, 200, 50, 128);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void EqualityOperator_DifferentBlue_NotEqual()
    {
        var a = new Color(100, 200, 50);
        var b = new Color(100, 200, 51);
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void EqualityOperator_DifferentAlpha_NotEqual()
    {
        var a = new Color(255, 0, 0, 255);
        var b = new Color(255, 0, 0, 128);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void StaticColor_Red_HasExpectedValues()
    {
        Assert.Equal(255, Color.Red.R);
        Assert.Equal(0,   Color.Red.G);
        Assert.Equal(0,   Color.Red.B);
        Assert.Equal(255, Color.Red.A);
    }

    [Fact]
    public void StaticColor_Black_IsOpaqueBlack()
    {
        Assert.Equal(0,   Color.Black.R);
        Assert.Equal(0,   Color.Black.G);
        Assert.Equal(0,   Color.Black.B);
        Assert.Equal(255, Color.Black.A);
    }

    [Fact]
    public void StaticColor_Transparent_HasZeroAlpha()
    {
        Assert.Equal(0, Color.Transparent.A);
        Assert.True(Color.Transparent.IsEmpty);
    }
}
