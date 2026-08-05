using System;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging.Metafiles
{
    /// <summary>Decodes the device-independent bitmaps embedded in metafile blit records.</summary>
    /// <remarks>
    /// A DIB is a BITMAPINFOHEADER followed by an optional palette and then the pixel rows, packed
    /// bottom-up and padded to a four-byte boundary. Uncompressed 1, 4, 8, 16, 24 and 32 bits per
    /// pixel are handled, which is everything a metafile realistically carries -- a producer that
    /// wanted JPEG or PNG compression would have embedded the file rather than a DIB.
    /// </remarks>
    internal static class DeviceIndependentBitmap
    {
        internal static SKBitmap? Decode (byte[] source, int headerOffset, int headerLength, int bitsOffset, int bitsLength)
        {
            if (headerOffset < 0 || headerLength < 40 || headerOffset + 40 > source.Length)
                return null;

            var width = BitConverter.ToInt32 (source, headerOffset + 4);
            var height = BitConverter.ToInt32 (source, headerOffset + 8);
            var bitCount = BitConverter.ToInt16 (source, headerOffset + 14);
            var compression = BitConverter.ToInt32 (source, headerOffset + 16);
            var paletteEntries = BitConverter.ToInt32 (source, headerOffset + 32);

            // BI_RGB (0) and BI_BITFIELDS (3) both leave the pixels uncompressed; RLE would need a
            // decoder for a case metafiles do not produce.
            if (compression is not (0 or 3) || width <= 0 || width > 20000 || height == 0 || Math.Abs (height) > 20000)
                return null;

            // A negative height means the rows are stored top-down instead of the usual bottom-up.
            var topDown = height < 0;
            height = Math.Abs (height);

            if (bitsOffset < 0 || bitsOffset >= source.Length)
                return null;

            var palette = ReadPalette (source, headerOffset, headerLength, bitCount, paletteEntries);
            var stride = ((width * bitCount + 31) / 32) * 4;
            var available = Math.Min (bitsLength, source.Length - bitsOffset);

            if (available < stride)
                return null;

            var bitmap = new SKBitmap (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var rows = Math.Min (height, available / stride);

            // Built into one buffer and copied in a single call. SKBitmap.SetPixel is a P/Invoke per
            // pixel, which for a megapixel bitmap embedded in a metafile cost seconds rather than
            // milliseconds -- slow enough to look like a hang while a page rendered.
            var destinationStride = bitmap.RowBytes;
            var pixels = new byte[destinationStride * height];

            for (var y = 0; y < rows; y++) {
                var row = bitsOffset + (y * stride);
                var target = (topDown ? y : height - 1 - y) * destinationStride;

                for (var x = 0; x < width; x++) {
                    var colour = ReadPixel (source, row, x, bitCount, palette);
                    var at = target + (x * 4);

                    // Bgra8888, so blue first.
                    pixels[at] = colour.Blue;
                    pixels[at + 1] = colour.Green;
                    pixels[at + 2] = colour.Red;
                    pixels[at + 3] = colour.Alpha;
                }
            }

            var handle = bitmap.GetPixels ();

            if (handle == IntPtr.Zero) {
                bitmap.Dispose ();
                return null;
            }

            System.Runtime.InteropServices.Marshal.Copy (pixels, 0, handle, pixels.Length);
            bitmap.NotifyPixelsChanged ();

            return bitmap;
        }

        private static SKColor[] ReadPalette (byte[] source, int headerOffset, int headerLength, short bitCount, int used)
        {
            if (bitCount > 8)
                return [];

            var count = used > 0 ? used : 1 << bitCount;
            var offset = headerOffset + headerLength;
            var palette = new SKColor[count];

            for (var i = 0; i < count; i++) {
                var at = offset + (i * 4);

                // A truncated palette is common in hand-built metafiles; the remaining entries stay
                // black rather than aborting a picture that is otherwise fine.
                if (at + 3 >= source.Length)
                    break;

                palette[i] = new SKColor (source[at + 2], source[at + 1], source[at], 0xFF);
            }

            return palette;
        }

        private static SKColor ReadPixel (byte[] source, int row, int x, short bitCount, SKColor[] palette)
        {
            switch (bitCount) {
            case 1: {
                var at = row + (x >> 3);
                if (at >= source.Length)
                    return SKColors.Black;
                var index = (source[at] >> (7 - (x & 7))) & 1;
                return index < palette.Length ? palette[index] : SKColors.Black;
            }
            case 4: {
                var at = row + (x >> 1);
                if (at >= source.Length)
                    return SKColors.Black;
                var index = (x & 1) == 0 ? source[at] >> 4 : source[at] & 0x0F;
                return index < palette.Length ? palette[index] : SKColors.Black;
            }
            case 8: {
                var at = row + x;
                if (at >= source.Length)
                    return SKColors.Black;
                var index = source[at];
                return index < palette.Length ? palette[index] : SKColors.Black;
            }
            case 16: {
                var at = row + (x * 2);
                if (at + 1 >= source.Length)
                    return SKColors.Black;
                // Default 5-5-5 layout; the top bit is unused rather than alpha.
                var packed = BitConverter.ToUInt16 (source, at);
                var r = (byte) (((packed >> 10) & 0x1F) * 255 / 31);
                var g = (byte) (((packed >> 5) & 0x1F) * 255 / 31);
                var b = (byte) ((packed & 0x1F) * 255 / 31);
                return new SKColor (r, g, b, 0xFF);
            }
            case 24: {
                var at = row + (x * 3);
                return at + 2 < source.Length
                    ? new SKColor (source[at + 2], source[at + 1], source[at], 0xFF)
                    : SKColors.Black;
            }
            case 32: {
                var at = row + (x * 4);

                if (at + 3 >= source.Length)
                    return SKColors.Black;

                // The fourth byte is alpha only when something set it: a fully zero alpha channel
                // across a 32bpp DIB means "unused", and honouring it would render the blit invisible.
                var alpha = source[at + 3];
                return new SKColor (source[at + 2], source[at + 1], source[at], alpha == 0 ? (byte) 0xFF : alpha);
            }
            default:
                return SKColors.Black;
            }
        }
    }
}
