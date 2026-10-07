using System;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Drawing.Imaging;
using Xunit;
using MFD = Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.Tests;

// GFX-31: Image.PixelFormat was the constant Format32bppArgb and Bitmap(int, int, PixelFormat) threw the
// format away, so code that branches on the format always took the 32bpp-with-alpha branch.
// GFX-32: Image.Clone copied the pixels and nothing else, so "clone, modify, save" reset the resolution,
// the raw format and the EXIF items.
public class ImageFormatAndCloneTests
{
    [Theory]
    [InlineData (PixelFormat.Format24bppRgb, false)]
    [InlineData (PixelFormat.Format32bppRgb, false)]
    [InlineData (PixelFormat.Format32bppArgb, true)]
    [InlineData (PixelFormat.Format32bppPArgb, true)]
    public void A_bitmap_reports_the_format_it_was_created_in (PixelFormat format, bool alpha)
    {
        using var bitmap = new MFD.Bitmap (4, 4, format);

        Assert.Equal (format, bitmap.PixelFormat);
        Assert.Equal (alpha, MFD.Image.IsAlphaPixelFormat (bitmap.PixelFormat));
        Assert.Equal (alpha, (bitmap.Flags & (int)ImageFlags.HasAlpha) != 0);
    }

    [Fact]
    public void A_format_without_alpha_keeps_no_alpha ()
    {
        // A 24bpp surface has nowhere to store alpha: GDI+ drops it, and reads the pixel back opaque.
        using var bitmap = new MFD.Bitmap (2, 2, PixelFormat.Format24bppRgb);

        Assert.Equal (255, bitmap.GetPixel (0, 0).A);
        Assert.Equal (Color.Black.ToArgb (), bitmap.GetPixel (0, 0).ToArgb ());
    }

    [Fact]
    public void A_blank_bitmap_is_32bpp_ARGB_and_transparent ()
    {
        using var bitmap = new MFD.Bitmap (2, 2);

        Assert.Equal (PixelFormat.Format32bppArgb, bitmap.PixelFormat);
        Assert.Equal (0, bitmap.GetPixel (1, 1).A);
    }

    [Fact]
    public void A_decoded_image_without_alpha_reports_24bpp ()
    {
        // GDI+ reports a JPEG (or an RGB PNG) as Format24bppRgb; the format comes from the surface here.
        using var source = new MFD.Bitmap (3, 3, PixelFormat.Format24bppRgb);
        using var jpeg = new MemoryStream ();
        source.Save (jpeg, ImageFormat.Jpeg);
        jpeg.Position = 0;

        using var decoded = new MFD.Bitmap (jpeg);

        Assert.Equal (PixelFormat.Format24bppRgb, decoded.PixelFormat);
    }

    [Fact]
    public void Graphics_cannot_draw_into_an_indexed_bitmap ()
    {
        // Upstream's Graphics.FromImage refuses an indexed image; reachable now that the format is kept.
        using var bitmap = new MFD.Bitmap (4, 4, PixelFormat.Format8bppIndexed);

        Assert.Equal (PixelFormat.Format8bppIndexed, bitmap.PixelFormat);
        Assert.Throws<ArgumentException> (() => MFD.Graphics.FromImage (bitmap));
    }

    [Fact]
    public void A_flag_is_not_a_pixel_format ()
        => Assert.Throws<ArgumentException> (() => new MFD.Bitmap (4, 4, PixelFormat.Indexed));

    [Fact]
    public void Clone_keeps_resolution_raw_format_pixel_format_and_metadata ()
    {
        using var source = new MFD.Bitmap (4, 4, PixelFormat.Format24bppRgb);
        using var jpeg = new MemoryStream ();
        source.Save (jpeg, ImageFormat.Jpeg);
        jpeg.Position = 0;

        using var original = new MFD.Bitmap (jpeg);
        original.SetResolution (300, 300);
        Assert.Equal (ImageFormat.Jpeg, original.RawFormat);   // read off the decoded container
        original.SetPropertyItem (PropertyItem.Create (0x010E, 2, [(byte)'h', (byte)'i', 0]));

        using var clone = (MFD.Bitmap)original.Clone ();

        Assert.Equal (300f, clone.HorizontalResolution);
        Assert.Equal (300f, clone.VerticalResolution);
        Assert.Equal (ImageFormat.Jpeg, clone.RawFormat);
        Assert.Equal (original.PixelFormat, clone.PixelFormat);
        Assert.Equal ([0x010E], clone.PropertyIdList);

        // A copy, not shared state: changing the clone's metadata leaves the original's alone.
        clone.GetPropertyItem (0x010E).Value![0] = (byte)'x';
        Assert.Equal ((byte)'h', original.GetPropertyItem (0x010E).Value![0]);
    }

    [Fact]
    public void Clone_does_not_copy_the_Tag ()
    {
        // Tag is a managed field upstream's Clone does not copy (it clones the native image only). A
        // guard: the old implementation did not copy it either.
        using var original = new MFD.Bitmap (2, 2) { Tag = "mine" };
        using var clone = (MFD.Bitmap)original.Clone ();

        Assert.Null (clone.Tag);
    }
}
