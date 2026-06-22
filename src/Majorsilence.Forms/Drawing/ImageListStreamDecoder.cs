using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Decodes the byte[] payload of a WinForms <c>ImageListStreamer</c> (the value stored under
    /// <c>imageList.ImageStream</c> in a <c>.resx</c>) into individual frames — cross-platform, with no
    /// Windows/comctl32 dependency.
    ///
    /// The payload is the comctl32 image-list stream WinForms wrote, run-length compressed behind a
    /// <c>"MSFt"</c> signature:
    /// <list type="number">
    ///   <item><c>"MSFt"</c> + an RLE body of <c>(count, value)</c> byte pairs;</item>
    ///   <item>which decompresses to a 28-byte <c>ILHEAD</c> (<c>"IL"</c> magic, image count, frame
    ///         <c>cx</c>/<c>cy</c>, flags, …);</item>
    ///   <item>followed by a colour <c>BMP</c> laid out as a grid of frames, and — when the list has a
    ///         mask (<c>ILC_MASK</c>) — a 1-bpp mask <c>BMP</c> in the same grid.</item>
    /// </list>
    /// SkiaSharp decodes the embedded BMPs; the mask supplies per-pixel transparency.
    /// </summary>
    internal static class ImageListStreamDecoder
    {
        private const int IlHeadSize = 28;
        private const int IlcMask = 0x0001;

        /// <summary>
        /// Decodes the streamer payload into one <see cref="SKBitmap"/> per image (each <c>cx</c>×<c>cy</c>).
        /// Returns an empty list if the payload is malformed or in an unsupported variant.
        /// </summary>
        public static IReadOnlyList<SKBitmap> Decode (byte[] data)
        {
            try { return DecodeCore (data); }
            catch { return Array.Empty<SKBitmap> (); }
        }

        private static IReadOnlyList<SKBitmap> DecodeCore (byte[] data)
        {
            var msft = IndexOf (data, "MSFt"u8, 0);
            if (msft < 0)
                return Array.Empty<SKBitmap> ();

            var raw = RunLengthDecompress (data, msft + 4);
            if (raw.Length < IlHeadSize || raw[0] != (byte)'I' || raw[1] != (byte)'L')
                return Array.Empty<SKBitmap> ();

            int count = ReadU16 (raw, 4);     // cCurImage — only this many frames are valid
            int cx = ReadU16 (raw, 10);
            int cy = ReadU16 (raw, 12);
            int flags = ReadU16 (raw, 20);
            if (count <= 0 || cx <= 0 || cy <= 0)
                return Array.Empty<SKBitmap> ();

            var colorStart = IndexOf (raw, "BM"u8, IlHeadSize);
            if (colorStart < 0)
                return Array.Empty<SKBitmap> ();

            using var color = DecodeBmp (raw, colorStart, out var colorEnd);
            if (color is null)
                return Array.Empty<SKBitmap> ();

            // The mask BMP (if any) follows the colour BMP.
            SKBitmap? mask = null;
            if ((flags & IlcMask) != 0)
            {
                var maskStart = IndexOf (raw, "BM"u8, colorEnd);
                if (maskStart < 0)
                    maskStart = IndexOf (raw, "BM"u8, colorStart + 2);
                if (maskStart >= 0)
                    mask = DecodeBmp (raw, maskStart, out _);
            }

            try
            {
                return SplitFrames (color, mask, count, cx, cy);
            }
            finally
            {
                mask?.Dispose ();
            }
        }

        // Each frame is a cx×cy tile of the grid (columns = stripWidth / cx), read row-major. Where the
        // mask is set (white => transparent in comctl32), the frame pixel's alpha is cleared.
        private static List<SKBitmap> SplitFrames (SKBitmap color, SKBitmap? mask, int count, int cx, int cy)
        {
            var columns = Math.Max (1, color.Width / cx);
            var frames = new List<SKBitmap> (count);

            for (var index = 0; index < count; index++)
            {
                var ox = (index % columns) * cx;
                var oy = (index / columns) * cy;
                if (ox + cx > color.Width || oy + cy > color.Height)
                    break;

                var frame = new SKBitmap (cx, cy, SKColorType.Bgra8888, SKAlphaType.Premul);
                for (var y = 0; y < cy; y++)
                    for (var x = 0; x < cx; x++)
                    {
                        var px = color.GetPixel (ox + x, oy + y);
                        if (mask is not null && oy + y < mask.Height && ox + x < mask.Width)
                        {
                            var m = mask.GetPixel (ox + x, oy + y);
                            if (m.Red > 127)                       // white mask pixel => transparent
                                px = SKColors.Transparent;
                        }
                        frame.SetPixel (x, y, px);
                    }
                frames.Add (frame);
            }

            return frames;
        }

        // WinForms ImageListStreamer compression: a flat sequence of (count, value) byte pairs.
        private static byte[] RunLengthDecompress (byte[] data, int start)
        {
            var output = new List<byte> (data.Length * 2);
            for (var i = start; i + 1 < data.Length; i += 2)
            {
                var count = data[i];
                var value = data[i + 1];
                for (var n = 0; n < count; n++)
                    output.Add (value);
            }
            return output.ToArray ();
        }

        // Decodes the BMP that begins at <paramref name="start"/>, returning the byte just past it in
        // <paramref name="end"/> so the caller can find the next (mask) BMP.
        private static SKBitmap? DecodeBmp (byte[] raw, int start, out int end)
        {
            end = raw.Length;
            if (start + 6 > raw.Length)
                return null;

            // BITMAPFILEHEADER.bfSize (bytes 2..5) — trust it only if it lands on a sane boundary.
            var bfSize = (int)ReadU32 (raw, start + 2);
            end = bfSize > 0 && start + bfSize <= raw.Length ? start + bfSize : raw.Length;

            var length = end - start;
            var bmp = new byte[length];
            Array.Copy (raw, start, bmp, 0, length);
            return SKBitmap.Decode (bmp);
        }

        private static int IndexOf (byte[] haystack, ReadOnlySpan<byte> needle, int start)
        {
            for (var i = Math.Max (0, start); i <= haystack.Length - needle.Length; i++)
            {
                var match = true;
                for (var j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match)
                    return i;
            }
            return -1;
        }

        private static int ReadU16 (byte[] b, int o) => b[o] | (b[o + 1] << 8);
        private static uint ReadU32 (byte[] b, int o) =>
            (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }
}
