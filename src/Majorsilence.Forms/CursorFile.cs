using System;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Reads and writes the Win32 <c>.cur</c> and <c>.ico</c> containers: a 6-byte header (reserved 0,
    /// type 1 = icon or 2 = cursor, frame count) and one 16-byte directory entry per frame. A cursor's
    /// entry carries the frame's hotspot where an icon's carries its plane and bit counts; the frame
    /// data itself is the same DIB (or PNG) in both.
    /// </summary>
    internal static class CursorFile
    {
        private const int HeaderSize = 6;
        private const int EntrySize = 16;
        private const int IconType = 1;
        private const int CursorType = 2;

        // Upstream's message for data OleLoadPicture cannot read (SR.InvalidPictureFormat).
        internal const string InvalidPictureFormat = "Image format is not valid. The image file may be corrupted.";

        /// <summary>
        /// Decodes the frame of a <c>.cur</c> or <c>.ico</c> that best fits the system cursor size, with its
        /// hotspot.
        /// </summary>
        /// <exception cref="ArgumentException">The data is not a cursor or icon, as upstream's
        /// <c>Cursor.LoadPicture</c> throws, naming <paramref name="paramName"/>.</exception>
        /// <remarks>
        /// Upstream loads the picture through OLE, which takes the frame for the system cursor size, then
        /// copies it as a cursor. An icon has no hotspot of its own: copied as a cursor it tracks the
        /// pointer at its centre, as <c>CreateIconIndirect</c> places an icon's hotspot.
        /// </remarks>
        internal static (SKBitmap Image, Point HotSpot) Decode (byte[] data, string paramName)
        {
            var type = data.Length >= HeaderSize && data[0] == 0 && data[1] == 0 && data[3] == 0 ? data[2] : -1;
            var count = data.Length >= HeaderSize ? data[4] | (data[5] << 8) : 0;

            if ((type != IconType && type != CursorType) || count == 0 || data.Length < HeaderSize + count * EntrySize)
                throw new ArgumentException (InvalidPictureFormat, paramName);

            var size = SystemInformation.CursorSize;
            var at = HeaderSize + BestFrame (data, count, size.Width, size.Height) * EntrySize;
            var length = BitConverter.ToInt32 (data, at + 8);
            var offset = BitConverter.ToInt32 (data, at + 12);

            if (offset < HeaderSize || length <= 0 || offset + (long)length > data.Length)
                throw new ArgumentException (InvalidPictureFormat, paramName);

            // Rewrap the one frame as a single-entry icon, so the codec decodes exactly it, PNG or DIB. The
            // codec reads only the entry's size and offset; the cursor's hotspot words are cleared.
            var single = new byte[HeaderSize + EntrySize + length];
            single[2] = IconType;
            single[4] = 1;
            single[HeaderSize] = data[at];
            single[HeaderSize + 1] = data[at + 1];
            BitConverter.GetBytes (length).CopyTo (single, HeaderSize + 8);
            BitConverter.GetBytes (HeaderSize + EntrySize).CopyTo (single, HeaderSize + 12);
            Array.Copy (data, offset, single, HeaderSize + EntrySize, length);

            SKBitmap? image;
            try {
                image = SKBitmap.Decode (single);
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
                image = null;
            }

            if (image is null || image.Width == 0 || image.Height == 0)
                throw new ArgumentException (InvalidPictureFormat, paramName);

            var hotSpot = type == CursorType
                ? new Point (data[at + 4] | (data[at + 5] << 8), data[at + 6] | (data[at + 7] << 8))
                : new Point (image.Width / 2, image.Height / 2);

            return (image, hotSpot);
        }

        // An exact size first (the most colour bits among those), else the smallest frame larger than
        // asked (scaling down keeps detail), else the largest -- the order Drawing.Common's Icon uses.
        private static int BestFrame (byte[] data, int count, int width, int height)
        {
            var best = 0;
            var bestScore = long.MinValue;

            for (var i = 0; i < count; i++) {
                var entry = HeaderSize + i * EntrySize;
                int w = Dimension (data[entry]), h = Dimension (data[entry + 1]);

                long score = w == width && h == height ? 3_000_000L
                    : w >= width && h >= height ? 2_000_000L - (w * h)
                    : 1_000_000L + (w * h);

                if (score > bestScore) {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        // A width or height byte of 0 means 256.
        private static int Dimension (byte value) => value == 0 ? 256 : value;

        /// <summary>
        /// Writes <paramref name="image"/> as a one-frame <c>.cur</c>: a 32-bit DIB with an AND mask, the form
        /// every Windows cursor loader reads (WPF's and WinForms' <c>Cursor (Stream)</c> both take it).
        /// </summary>
        internal static byte[] Encode (SKBitmap image, Point hotSpot)
        {
            int width = Math.Min (image.Width, 256), height = Math.Min (image.Height, 256);
            var maskStride = ((width + 31) / 32) * 4;
            var pixelBytes = width * height * 4;
            var dibSize = 40 + pixelBytes + maskStride * height;

            var bytes = new byte[HeaderSize + EntrySize + dibSize];
            bytes[2] = CursorType;
            bytes[4] = 1;

            var entry = HeaderSize;
            bytes[entry] = (byte)(width >= 256 ? 0 : width);
            bytes[entry + 1] = (byte)(height >= 256 ? 0 : height);
            WriteUInt16 (bytes, entry + 4, Clamp (hotSpot.X, width));
            WriteUInt16 (bytes, entry + 6, Clamp (hotSpot.Y, height));
            BitConverter.GetBytes (dibSize).CopyTo (bytes, entry + 8);
            BitConverter.GetBytes (HeaderSize + EntrySize).CopyTo (bytes, entry + 12);

            // BITMAPINFOHEADER: the height counts the colour rows and the mask rows together.
            var dib = HeaderSize + EntrySize;
            BitConverter.GetBytes (40).CopyTo (bytes, dib);
            BitConverter.GetBytes (width).CopyTo (bytes, dib + 4);
            BitConverter.GetBytes (height * 2).CopyTo (bytes, dib + 8);
            WriteUInt16 (bytes, dib + 12, 1);
            WriteUInt16 (bytes, dib + 14, 32);
            BitConverter.GetBytes (pixelBytes + maskStride * height).CopyTo (bytes, dib + 20);

            var pixels = dib + 40;
            var mask = pixels + pixelBytes;

            // Both planes are stored bottom-up. A fully transparent pixel is also set in the AND mask, for
            // loaders that ignore the alpha channel.
            for (var y = 0; y < height; y++) {
                var row = height - 1 - y;
                for (var x = 0; x < width; x++) {
                    var color = image.GetPixel (x, y);
                    var p = pixels + (row * width + x) * 4;
                    bytes[p] = color.Blue;
                    bytes[p + 1] = color.Green;
                    bytes[p + 2] = color.Red;
                    bytes[p + 3] = color.Alpha;

                    if (color.Alpha == 0)
                        bytes[mask + row * maskStride + x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            return bytes;
        }

        private static int Clamp (int value, int size) => Math.Max (0, Math.Min (value, size - 1));

        private static void WriteUInt16 (byte[] bytes, int at, int value)
        {
            bytes[at] = (byte)value;
            bytes[at + 1] = (byte)(value >> 8);
        }
    }
}
