using System;
using System.Drawing;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>
    /// Describes the pixel buffer handed out by <see cref="Bitmap.LockBits(Rectangle, ImageLockMode, PixelFormat)"/>.
    /// Cross-platform replacement for <c>System.Drawing.Imaging.BitmapData</c>.
    /// </summary>
    /// <remarks>
    /// Unlike GDI+, which can hand back a pointer straight into the bitmap's own memory, this
    /// implementation always hands back a freshly-allocated buffer laid out in the requested
    /// <see cref="PixelFormat"/> and copies it back into the SkiaSharp bitmap on
    /// <see cref="Bitmap.UnlockBits(BitmapData)"/> (unless the lock was
    /// <see cref="ImageLockMode.ReadOnly"/>). That keeps the documented GDI+ semantics exact --
    /// notably <see cref="PixelFormat.Format32bppArgb"/> being straight (non-premultiplied) alpha,
    /// where the SkiaSharp backing store is premultiplied -- at the cost of one copy per lock, which
    /// is still far cheaper than the per-pixel <c>GetPixel</c>/<c>SetPixel</c> path this API exists
    /// to replace.
    /// </remarks>
    public sealed class BitmapData
    {
        /// <summary>Gets or sets the address of the first pixel of the locked region.</summary>
        public IntPtr Scan0 { get; set; }

        /// <summary>Gets or sets the byte offset between the start of one scan line and the next.</summary>
        public int Stride { get; set; }

        /// <summary>Gets or sets the pixel width of the locked region.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the pixel height of the locked region.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the pixel format of the locked region.</summary>
        public PixelFormat PixelFormat { get; set; } = PixelFormat.Format32bppArgb;

        /// <summary>Gets or sets a reserved value. Unused by Majorsilence.Forms.Drawing.</summary>
        public int Reserved { get; set; }

        // The region of the owning bitmap this buffer maps to, and how it was locked.
        internal Rectangle LockedRegion { get; set; }
        internal ImageLockMode LockMode { get; set; }
        internal int BufferLength { get; set; }
        internal object? Owner { get; set; }
    }

    /// <summary>
    /// Marshals between a SkiaSharp <see cref="SKBitmap"/> and a GDI+-shaped, format-specific
    /// pixel buffer. Shared by <c>Bitmap.LockBits</c>/<c>UnlockBits</c>.
    /// </summary>
    internal static class BitmapDataMarshal
    {
        /// <summary>
        /// Returns the bytes per pixel of a GDI+ pixel format. Formats Majorsilence.Forms.Drawing
        /// does not lay out natively are widened to 32bpp ARGB (the backing store's own depth).
        /// </summary>
        public static int BytesPerPixel (PixelFormat format) => format == PixelFormat.Format24bppRgb ? 3 : 4;

        /// <summary>
        /// Normalizes a requested pixel format to one this implementation lays out directly. The
        /// sub-24bpp formats (16bpp RGB variants, 8bpp indexed) have no SkiaSharp backing-store
        /// equivalent here -- the backing bitmap is always 32bpp -- so they widen to 32bpp ARGB
        /// rather than silently producing a buffer the caller would misread.
        /// </summary>
        public static PixelFormat Normalize (PixelFormat format) => format switch {
            PixelFormat.Format24bppRgb => PixelFormat.Format24bppRgb,
            PixelFormat.Format32bppRgb => PixelFormat.Format32bppRgb,
            PixelFormat.Format32bppPArgb => PixelFormat.Format32bppPArgb,
            _ => PixelFormat.Format32bppArgb
        };

        /// <summary>GDI+ pads every scan line to a 4-byte boundary.</summary>
        public static int StrideFor (int width, PixelFormat format)
            => ((width * BytesPerPixel (format)) + 3) & ~3;

        /// <summary>Copies <paramref name="region"/> of <paramref name="bitmap"/> into a newly allocated native buffer.</summary>
        public static IntPtr CopyOut (SKBitmap bitmap, Rectangle region, PixelFormat format, int stride, out int byteCount)
        {
            var bpp = BytesPerPixel (format);
            byteCount = stride * region.Height;
            var managed = new byte[byteCount];

            // One interop hop for the whole bitmap; SKBitmap.Pixels normalizes color type and
            // un-premultiplies, which is what the GDI+ ARGB formats expect.
            var pixels = bitmap.Pixels;
            var bmpWidth = bitmap.Width;

            for (var y = 0; y < region.Height; y++) {
                var rowStart = y * stride;
                var srcRow = (region.Y + y) * bmpWidth + region.X;
                for (var x = 0; x < region.Width; x++) {
                    var c = pixels[srcRow + x];
                    var p = rowStart + x * bpp;
                    switch (format) {
                        case PixelFormat.Format24bppRgb:
                            managed[p] = c.Blue; managed[p + 1] = c.Green; managed[p + 2] = c.Red;
                            break;
                        case PixelFormat.Format32bppRgb:
                            managed[p] = c.Blue; managed[p + 1] = c.Green; managed[p + 2] = c.Red; managed[p + 3] = 255;
                            break;
                        case PixelFormat.Format32bppPArgb:
                            managed[p] = Premultiply (c.Blue, c.Alpha);
                            managed[p + 1] = Premultiply (c.Green, c.Alpha);
                            managed[p + 2] = Premultiply (c.Red, c.Alpha);
                            managed[p + 3] = c.Alpha;
                            break;
                        default: // Format32bppArgb -- straight alpha, BGRA byte order.
                            managed[p] = c.Blue; managed[p + 1] = c.Green; managed[p + 2] = c.Red; managed[p + 3] = c.Alpha;
                            break;
                    }
                }
            }

            var buffer = Marshal.AllocHGlobal (byteCount);
            Marshal.Copy (managed, 0, buffer, byteCount);
            return buffer;
        }

        /// <summary>Copies a native buffer back into <paramref name="region"/> of <paramref name="bitmap"/>.</summary>
        public static void CopyIn (SKBitmap bitmap, IntPtr buffer, int byteCount, Rectangle region, PixelFormat format, int stride)
        {
            var bpp = BytesPerPixel (format);
            var managed = new byte[byteCount];
            Marshal.Copy (buffer, managed, 0, byteCount);

            var pixels = bitmap.Pixels;
            var bmpWidth = bitmap.Width;

            for (var y = 0; y < region.Height; y++) {
                var rowStart = y * stride;
                var destRow = (region.Y + y) * bmpWidth + region.X;
                for (var x = 0; x < region.Width; x++) {
                    var p = rowStart + x * bpp;
                    var c = format switch {
                        PixelFormat.Format24bppRgb or PixelFormat.Format32bppRgb
                            => new SKColor (managed[p + 2], managed[p + 1], managed[p], 255),
                        PixelFormat.Format32bppPArgb
                            => new SKColor (
                                Unpremultiply (managed[p + 2], managed[p + 3]),
                                Unpremultiply (managed[p + 1], managed[p + 3]),
                                Unpremultiply (managed[p], managed[p + 3]),
                                managed[p + 3]),
                        _ => new SKColor (managed[p + 2], managed[p + 1], managed[p], managed[p + 3])
                    };
                    pixels[destRow + x] = c;
                }
            }

            bitmap.Pixels = pixels;
        }

        private static byte Premultiply (byte channel, byte alpha) => (byte)((channel * alpha + 127) / 255);

        private static byte Unpremultiply (byte channel, byte alpha)
            => alpha == 0 ? (byte)0 : (byte)Math.Min (255, (channel * 255 + alpha / 2) / alpha);
    }
}
