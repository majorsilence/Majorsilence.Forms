using Majorsilence.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class ImageCodecInfoTests
{
    [Fact]
    public void GetImageEncoders_ReturnsFiveEntries()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.Equal(5, encoders.Length);
    }

    [Fact]
    public void GetImageEncoders_ContainsBmpMimeType()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.Contains(encoders, e => e.MimeType == "image/bmp");
    }

    [Fact]
    public void GetImageEncoders_ContainsJpegMimeType()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.Contains(encoders, e => e.MimeType == "image/jpeg");
    }

    [Fact]
    public void GetImageEncoders_ContainsPngMimeType()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.Contains(encoders, e => e.MimeType == "image/png");
    }

    [Fact]
    public void GetImageEncoders_ContainsGifMimeType()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.Contains(encoders, e => e.MimeType == "image/gif");
    }

    [Fact]
    public void GetImageEncoders_ContainsTiffMimeType()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.Contains(encoders, e => e.MimeType == "image/tiff");
    }

    [Fact]
    public void GetImageEncoders_AllHaveNonEmptyMimeType()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        Assert.All(encoders, e => Assert.False(string.IsNullOrEmpty(e.MimeType)));
    }

    [Fact]
    public void GetImageEncoders_TiffEntry_HasTiffFormat()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        var tiff = Array.Find(encoders, e => e.MimeType == "image/tiff");
        Assert.NotNull(tiff);
        Assert.Equal(ImageFormat.Tiff, tiff!.Format);
    }
}
