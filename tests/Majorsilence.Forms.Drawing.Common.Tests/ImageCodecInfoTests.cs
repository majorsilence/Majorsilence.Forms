using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class ImageCodecInfoTests
{
    // Was "ReturnsFiveEntries". A hardcoded count breaks every time a format is added (WebP, in Phase 4
    // of docs/gdi-gap-plan.md) without saying anything the per-format tests below don't already cover.
    // What actually matters is that the set is complete and each codec appears once.
    [Fact]
    public void GetImageEncoders_ReturnsEachExpectedFormatExactlyOnce()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        var mimeTypes = encoders.Select(e => e.MimeType).ToArray();

        Assert.Equal(mimeTypes.Length, mimeTypes.Distinct().Count());
        Assert.All(
            new[] { "image/bmp", "image/jpeg", "image/gif", "image/png", "image/tiff" },
            expected => Assert.Single(encoders, e => e.MimeType == expected));
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
