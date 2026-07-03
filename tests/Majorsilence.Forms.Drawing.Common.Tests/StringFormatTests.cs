using Majorsilence.Drawing;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class StringFormatTests
{
    [Fact]
    public void DefaultConstructor_HasNearAlignmentAndNoTrimming()
    {
        using var sf = new StringFormat();
        Assert.Equal(StringAlignment.Near, sf.Alignment);
        Assert.Equal(StringAlignment.Near, sf.LineAlignment);
        Assert.Equal(StringTrimming.None,  sf.Trimming);
    }

    [Fact]
    public void GenericTypographic_HasExpectedDefaults()
    {
        var sf = StringFormat.GenericTypographic;
        Assert.Equal(StringAlignment.Near, sf.Alignment);
        Assert.Equal(StringAlignment.Near, sf.LineAlignment);
        Assert.Equal(StringTrimming.None,  sf.Trimming);
    }

    [Fact]
    public void Alignment_Setter_RoundTrips()
    {
        using var sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        Assert.Equal(StringAlignment.Center, sf.Alignment);
        sf.Alignment = StringAlignment.Far;
        Assert.Equal(StringAlignment.Far, sf.Alignment);
    }

    [Fact]
    public void LineAlignment_Setter_RoundTrips()
    {
        using var sf = new StringFormat();
        sf.LineAlignment = StringAlignment.Far;
        Assert.Equal(StringAlignment.Far, sf.LineAlignment);
    }

    [Fact]
    public void Trimming_Setter_RoundTrips()
    {
        using var sf = new StringFormat();
        sf.Trimming = StringTrimming.EllipsisCharacter;
        Assert.Equal(StringTrimming.EllipsisCharacter, sf.Trimming);
    }

    [Fact]
    public void FormatFlags_Setter_RoundTrips()
    {
        using var sf = new StringFormat();
        sf.FormatFlags = StringFormatFlags.NoWrap;
        Assert.Equal(StringFormatFlags.NoWrap, sf.FormatFlags);
    }

    [Fact]
    public void SetMeasurableCharacterRanges_StoresRanges()
    {
        using var sf = new StringFormat();
        sf.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, 5), new CharacterRange(10, 3) });
        Assert.Equal(2, sf.MeasurableCharacterRanges!.Length);
    }

    [Fact]
    public void Clone_ReturnsIndependentCopy()
    {
        using var sf    = new StringFormat { Alignment = StringAlignment.Center };
        var       clone = (StringFormat)sf.Clone();
        clone.Alignment = StringAlignment.Far;
        // Original should be unaffected
        Assert.Equal(StringAlignment.Center, sf.Alignment);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sf = new StringFormat();
        sf.Dispose();
    }
}
