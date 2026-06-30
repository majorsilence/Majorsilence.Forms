using Majorsilence.Drawing.Imaging;
using SkiaSharp;

namespace Majorsilence.Drawing.Common.Tests;

public class TiffWriterTests
{
    // ── TIFF byte-level helpers ────────────────────────────────────────────────

    private static ushort ReadU16(byte[] b, int pos) =>
        (ushort)(b[pos] | (b[pos + 1] << 8));

    private static uint ReadU32(byte[] b, int pos) =>
        (uint)(b[pos] | (b[pos + 1] << 8) | (b[pos + 2] << 16) | (b[pos + 3] << 24));

    // Returns tag→value map for one IFD. For 14-entry IFDs the "next IFD" pointer
    // sits 2 + 14*12 = 170 bytes after the IFD start.
    private static Dictionary<ushort, uint> ReadIfd(byte[] data, uint offset)
    {
        int entryCount = ReadU16(data, (int)offset);
        var tags = new Dictionary<ushort, uint>();
        int pos  = (int)offset + 2;
        for (int i = 0; i < entryCount; i++, pos += 12)
        {
            ushort tag = ReadU16(data, pos);
            uint   val = ReadU32(data, pos + 8);
            tags[tag]  = val;
        }
        return tags;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public void WritePage_Bitonal_StartsWithLittleEndianMagic()
    {
        using var bmp = new SKBitmap(4, 4);
        bmp.Erase(SKColors.White);

        var ms = new MemoryStream();
        using (var writer = new TiffWriter(ms))
        {
            writer.WritePage(bmp, color: false, dpiX: 72, dpiY: 72);
            writer.Finish();
        }

        var data = ms.ToArray();
        Assert.Equal(0x49, data[0]); // 'I'
        Assert.Equal(0x49, data[1]); // 'I'
        Assert.Equal(42u,  ReadU16(data, 2));
        Assert.True(ReadU32(data, 4) > 0, "First IFD offset must be non-zero");
    }

    [Fact]
    public void WritePage_Bitonal_IfdHasCorrectDimensionAndPhotometricTags()
    {
        using var bmp = new SKBitmap(8, 3);
        bmp.Erase(SKColors.Black);

        var ms = new MemoryStream();
        using (var writer = new TiffWriter(ms))
        {
            writer.WritePage(bmp, color: false, dpiX: 96, dpiY: 96);
            writer.Finish();
        }

        var data = ms.ToArray();
        var tags = ReadIfd(data, ReadU32(data, 4));

        Assert.Equal(8u, tags[256]); // ImageWidth
        Assert.Equal(3u, tags[257]); // ImageLength
        Assert.Equal(1u, tags[258]); // BitsPerSample = 1
        Assert.Equal(1u, tags[259]); // Compression = uncompressed
        Assert.Equal(1u, tags[262]); // PhotometricInterpretation = MinIsBlack
        Assert.Equal(1u, tags[277]); // SamplesPerPixel = 1
        Assert.Equal(2u, tags[296]); // ResolutionUnit = inch
    }

    [Fact]
    public void WritePage_Color_IfdHasRgbPhotometricAndThreeSamples()
    {
        using var bmp = new SKBitmap(5, 7);
        bmp.Erase(SKColors.Red);

        var ms = new MemoryStream();
        using (var writer = new TiffWriter(ms))
        {
            writer.WritePage(bmp, color: true, dpiX: 300, dpiY: 300);
            writer.Finish();
        }

        var data = ms.ToArray();
        var tags = ReadIfd(data, ReadU32(data, 4));

        Assert.Equal(5u, tags[256]); // ImageWidth
        Assert.Equal(7u, tags[257]); // ImageLength
        Assert.Equal(1u, tags[259]); // Compression = uncompressed
        Assert.Equal(2u, tags[262]); // PhotometricInterpretation = RGB
        Assert.Equal(3u, tags[277]); // SamplesPerPixel = 3
        Assert.Equal(2u, tags[296]); // ResolutionUnit = inch
    }

    [Fact]
    public void WritePage_Color_StripContainsCorrectRgbBytes()
    {
        // A 1×1 pure red bitmap must produce [0xFF, 0x00, 0x00] at the strip offset.
        using var bmp = new SKBitmap(1, 1);
        bmp.SetPixel(0, 0, SKColors.Red);

        var ms = new MemoryStream();
        using (var writer = new TiffWriter(ms))
        {
            writer.WritePage(bmp, color: true, dpiX: 72, dpiY: 72);
            writer.Finish();
        }

        var data       = ms.ToArray();
        var tags       = ReadIfd(data, ReadU32(data, 4));
        uint stripBase = tags[273]; // StripOffsets

        Assert.Equal(0xFF, data[stripBase]);      // R
        Assert.Equal(0x00, data[stripBase + 1]);  // G
        Assert.Equal(0x00, data[stripBase + 2]);  // B
    }

    [Fact]
    public void WritePage_TwoPages_SecondIfdLinkedFromFirst()
    {
        using var bmp1 = new SKBitmap(2, 2);
        using var bmp2 = new SKBitmap(3, 1);
        bmp1.Erase(SKColors.White);
        bmp2.Erase(SKColors.Black);

        var ms = new MemoryStream();
        using (var writer = new TiffWriter(ms))
        {
            writer.WritePage(bmp1, color: false, dpiX: 72, dpiY: 72);
            writer.WritePage(bmp2, color: false, dpiX: 72, dpiY: 72);
            writer.Finish();
        }

        var data = ms.ToArray();

        // First IFD
        uint ifd1    = ReadU32(data, 4);
        var  tags1   = ReadIfd(data, ifd1);
        Assert.Equal(2u, tags1[256]); // page 1 width

        // "Next IFD" pointer is right after the 14 entries (2 + 14*12 = 170 bytes from IFD start)
        uint ifd2 = ReadU32(data, (int)ifd1 + 2 + 14 * 12);
        Assert.True(ifd2 > 0, "Second IFD offset must be non-zero");

        var tags2 = ReadIfd(data, ifd2);
        Assert.Equal(3u, tags2[256]); // page 2 width
        Assert.Equal(1u, tags2[257]); // page 2 height
    }

    [Fact]
    public void WritePage_AfterDispose_ThrowsObjectDisposedException()
    {
        using var bmp = new SKBitmap(1, 1);
        var ms     = new MemoryStream();
        var writer = new TiffWriter(ms);
        writer.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            writer.WritePage(bmp, color: false, dpiX: 72, dpiY: 72));
    }
}
