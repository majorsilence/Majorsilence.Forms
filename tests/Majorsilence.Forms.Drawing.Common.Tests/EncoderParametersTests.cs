using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class EncoderParametersTests
{
    // Regression: EncoderParameters(int count) used `new List<EncoderParameter>(count)`, which sets
    // capacity, not Count -- so Param came back empty and the WinForms idiom
    // `p.Param[0] = new EncoderParameter(...)` threw IndexOutOfRange. It broke every chart / TIF /
    // image encode in Majorsilence Reporting's engine on the SkiaSharp path.
    [Fact]
    public void CountConstructor_GivesAnArrayOfThatLength_AssignableByIndex()
    {
        var p = new EncoderParameters(2);

        Assert.Equal(2, p.Param.Length);

        p.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
        p.Param[1] = new EncoderParameter(Encoder.ColorDepth, 24L);

        Assert.Same(Encoder.Quality, p.Param[0].Encoder);
        Assert.Equal(2, p.GetParameters().Length);
    }

    [Fact]
    public void GetParameters_SkipsUnassignedSlots()
    {
        var p = new EncoderParameters(3);
        p.Param[1] = new EncoderParameter(Encoder.Quality, 100L);

        var filled = p.GetParameters();

        Assert.Single(filled);
        Assert.Same(Encoder.Quality, filled[0].Encoder);
    }

    [Fact]
    public void Add_GrowsParam()
    {
        var p = new EncoderParameters();
        Assert.Empty(p.Param);

        p.Add(new EncoderParameter(Encoder.Quality, 80L));
        p.Add(new EncoderParameter(Encoder.ColorDepth, 8L));

        Assert.Equal(2, p.Param.Length);
        Assert.Equal(2, p.GetParameters().Length);
    }
}
