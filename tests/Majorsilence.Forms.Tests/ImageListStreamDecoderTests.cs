using System.Collections.Generic;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="ImageListStreamDecoder"/> turns a designer <c>ImageList.ImageStream</c> payload into one
/// bitmap per image — including the pixels.
/// </summary>
/// <remarks>
/// The colour strip inside an image-list stream carries a BITMAPFILEHEADER whose <c>bfSize</c> is the
/// offset of the pixel bits, not the length of the file (1078 == 14 + 40 + a 256-entry palette, for the
/// 8bpp strip a designer ImageList produces). Slicing the bitmap at <c>bfSize</c> therefore handed
/// SkiaSharp headers with no pixel data behind them: every frame decoded at the right size and fully
/// transparent, so an ImageList reported the correct Count and ImageSize while a toolbar bound to it drew
/// nothing at all.
/// </remarks>
public class ImageListStreamDecoderTests
{
    private const int Cx = 4;
    private const int Cy = 4;

    [Fact]
    public void Decodes_one_frame_per_image ()
    {
        var frames = ImageListStreamDecoder.Decode (BuildStream (bfSizeIsPixelOffset: true));

        Assert.Equal (2, frames.Count);
        Assert.All (frames, f => Assert.Equal (new SKSizeI (Cx, Cy), new SKSizeI (f.Width, f.Height)));
    }

    [Fact]
    public void Frames_carry_their_pixels_when_bfSize_is_the_pixel_offset_rather_than_the_file_length ()
    {
        var frames = ImageListStreamDecoder.Decode (BuildStream (bfSizeIsPixelOffset: true));

        Assert.Equal (2, frames.Count);
        Assert.Equal (SKColors.Red, frames[0].GetPixel (1, 1));
        Assert.Equal (SKColors.Blue, frames[1].GetPixel (1, 1));
    }

    [Fact]
    public void Frames_carry_their_pixels_when_bfSize_is_the_file_length ()
    {
        // The well-formed case still has to work: the fix reads the extent out of the headers instead of
        // trusting bfSize either way.
        var frames = ImageListStreamDecoder.Decode (BuildStream (bfSizeIsPixelOffset: false));

        Assert.Equal (2, frames.Count);
        Assert.Equal (SKColors.Red, frames[0].GetPixel (1, 1));
        Assert.Equal (SKColors.Blue, frames[1].GetPixel (1, 1));
    }

    [Fact]
    public void No_frame_comes_back_fully_transparent ()
    {
        var frames = ImageListStreamDecoder.Decode (BuildStream (bfSizeIsPixelOffset: true));

        Assert.NotEmpty (frames);
        Assert.All (frames, frame => {
            var opaque = 0;
            for (var y = 0; y < frame.Height; y++)
                for (var x = 0; x < frame.Width; x++)
                    if (frame.GetPixel (x, y).Alpha > 0)
                        opaque++;

            Assert.True (opaque > 0, "a frame decoded fully transparent");
        });
    }

    [Fact]
    public void Malformed_input_yields_no_frames_rather_than_throwing ()
    {
        Assert.Empty (ImageListStreamDecoder.Decode (new byte[] { 1, 2, 3, 4 }));
    }

    // A minimal image-list stream: "MSFt", then an RLE'd body of a 28-byte ILHEAD followed by a colour
    // BMP holding both frames side by side (no mask, so ILC_MASK stays clear).
    private static byte[] BuildStream (bool bfSizeIsPixelOffset)
    {
        var body = new List<byte> ();

        var head = new byte[28];
        head[0] = (byte)'I';
        head[1] = (byte)'L';
        WriteU16 (head, 4, 2);      // cCurImage
        WriteU16 (head, 10, Cx);
        WriteU16 (head, 12, Cy);
        WriteU16 (head, 20, 0);     // flags — no mask
        body.AddRange (head);
        body.AddRange (BuildBmp (bfSizeIsPixelOffset));

        // The stream's compression is a flat run of (count, value) pairs; a run of one per byte is valid
        // and keeps the fixture readable.
        var stream = new List<byte> ("MSFt"u8.ToArray ());
        foreach (var b in body) {
            stream.Add (1);
            stream.Add (b);
        }

        return stream.ToArray ();
    }

    // 24bpp, bottom-up, two Cx-wide colour bands: red then blue.
    private static byte[] BuildBmp (bool bfSizeIsPixelOffset)
    {
        const int Width = Cx * 2;
        const int Height = Cy;
        const int OffBits = 54;

        var rowBytes = ((Width * 24 + 31) / 32) * 4;
        var pixels = new byte[rowBytes * Height];

        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++) {
                var o = y * rowBytes + x * 3;
                if (x < Cx) {
                    pixels[o + 2] = 255;    // B, G, R -- red
                } else {
                    pixels[o] = 255;        // blue
                }
            }

        var bmp = new byte[OffBits + pixels.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        // The whole point of the fixture: the writer behind a real image-list stream puts the pixel offset
        // here instead of the file length.
        WriteU32 (bmp, 2, bfSizeIsPixelOffset ? OffBits : bmp.Length);
        WriteU32 (bmp, 10, OffBits);
        WriteU32 (bmp, 14, 40);         // BITMAPINFOHEADER
        WriteU32 (bmp, 18, Width);
        WriteU32 (bmp, 22, Height);
        WriteU16 (bmp, 26, 1);          // planes
        WriteU16 (bmp, 28, 24);         // bit count
        WriteU32 (bmp, 30, 0);          // BI_RGB
        pixels.CopyTo (bmp, OffBits);

        return bmp;
    }

    private static void WriteU16 (byte[] b, int o, int v)
    {
        b[o] = (byte)(v & 0xFF);
        b[o + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteU32 (byte[] b, int o, int v)
    {
        b[o] = (byte)(v & 0xFF);
        b[o + 1] = (byte)((v >> 8) & 0xFF);
        b[o + 2] = (byte)((v >> 16) & 0xFF);
        b[o + 3] = (byte)((v >> 24) & 0xFF);
    }
}
