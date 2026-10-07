using System;
using System.IO;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Represents a Windows icon, backed by SkiaSharp pixel data. Cross-platform replacement for
    /// <c>System.Drawing.Icon</c>.
    /// </summary>
    /// <remarks>
    /// An icon loaded from ICO data keeps those bytes, as upstream's <c>Icon</c> keeps its
    /// <c>_iconData</c>: <see cref="Save"/> writes them back verbatim (GFX-33) and
    /// <see cref="Icon(Icon, int, int)"/> selects the embedded frame from them (GFX-34).
    /// </remarks>
    public sealed partial class Icon : IDisposable, ICloneable
    {
        private SKBitmap? backing;

        // The ICO container this icon was loaded from, or null when it was built from a bitmap.
        private byte[]? iconData;

        /// <summary>Initializes a new icon from the specified file.</summary>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="ArgumentException">The file is not an image Skia can decode.</exception>
        /// <remarks>
        /// GFX-35: every exception used to be swallowed, so a mistyped path or a corrupt file produced
        /// an invisible zero-sized icon and the failure surfaced much later as a blank title bar. Upstream
        /// throws, and so does this: the file system's own exception for a missing file, and
        /// <see cref="ArgumentException"/> for data that is not an icon.
        /// </remarks>
        public Icon (string fileName)
        {
            Guard.ThrowIfNull (fileName);
            Load (File.ReadAllBytes (fileName));
        }

        /// <summary>Initializes a new icon from the specified stream.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="ArgumentException">The stream is not an image Skia can decode.</exception>
        /// <inheritdoc cref="Icon(string)" path="/remarks"/>
        public Icon (Stream stream)
        {
            Guard.ThrowIfNull (stream);

            using var buffer = new MemoryStream ();
            stream.CopyTo (buffer);
            Load (buffer.ToArray ());
        }

        private void Load (byte[] data)
        {
            SKBitmap? decoded = null;
            try {
                decoded = data.Length == 0 ? null : SKBitmap.Decode (data);
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
                decoded = null;
            }

            backing = decoded ?? throw new ArgumentException ("The data is not a valid icon or image.");

            if (IcoDirectory.IsIco (data))
                iconData = data;
        }

        /// <summary>Initializes a new icon from an existing icon, scaled to the specified size.</summary>
        public Icon (Icon original, System.Drawing.Size size) : this (original, size.Width, size.Height) { }

        /// <summary>Initializes a new icon from an existing icon, at the specified dimensions.</summary>
        /// <remarks>
        /// GFX-34: upstream picks the embedded ICO frame that best matches the request and uses that
        /// frame's own pixels -- the hand-tuned 16x16 a form's small title-bar icon is meant to show.
        /// This rescaled the one frame the decoder had already chosen (usually the largest), so small
        /// icons came out as mushy downsamples. The frame is selected from the retained ICO data now; it
        /// is rescaled only when no frame has the requested size, or when there is no ICO data.
        /// </remarks>
        public Icon (Icon original, int width, int height)
        {
            width = Math.Max (1, width);
            height = Math.Max (1, height);

            if (original?.iconData is { } data) {
                var frame = IcoDirectory.DecodeBest (data, width, height);
                if (frame is not null) {
                    iconData = data;
                    backing = frame.Width == width && frame.Height == height
                        ? frame
                        : Resize (frame, width, height, dispose: true);
                    return;
                }
            }

            var source = original?.backing;
            if (source is not null)
                backing = Resize (source, width, height, dispose: false);
        }

        private static SKBitmap Resize (SKBitmap source, int width, int height, bool dispose)
        {
            var resized = source.Resize (new SKImageInfo (width, height), new SKSamplingOptions (SKCubicResampler.Mitchell)) ?? source.Copy ();
            if (dispose)
                source.Dispose ();
            return resized;
        }

        // Wraps an existing SKBitmap.
        internal Icon (SKBitmap? bitmap) => backing = bitmap;

        /// <summary>Gets the width of the icon.</summary>
        public int Width => backing?.Width ?? 0;

        /// <summary>Gets the height of the icon.</summary>
        public int Height => backing?.Height ?? 0;

        /// <summary>Gets the size of the icon.</summary>
        public System.Drawing.Size Size => new System.Drawing.Size (Width, Height);

        /// <summary>Gets the handle for this icon. Returns IntPtr.Zero in Majorsilence.Forms.Drawing.</summary>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Gets the backing SkiaSharp bitmap (for renderer use).</summary>
        internal SKBitmap? GetSKBitmap () => backing;

        /// <summary>Converts this icon to a <see cref="Bitmap"/>.</summary>
        public Bitmap ToBitmap () => new Bitmap (backing?.Copy ());

        /// <summary>Saves this icon to the specified stream, in ICO format.</summary>
        /// <remarks>
        /// GFX-33: this wrote a PNG, so a file saved with a .ico name was rejected by the shell and by
        /// installers, and every embedded size but one was lost. Upstream writes the original ICO bytes
        /// back verbatim; an icon that was not loaded from ICO data (one built from a bitmap) is written
        /// as a single-frame ICO holding a PNG image, the form Windows Vista and later read.
        /// </remarks>
        public void Save (Stream outputStream)
        {
            Guard.ThrowIfNull (outputStream);

            if (iconData is not null) {
                outputStream.Write (iconData, 0, iconData.Length);
                return;
            }

            if (backing is null)
                return;

            using var image = SKImage.FromBitmap (backing);
            using var png = image.Encode (SKEncodedImageFormat.Png, 100);
            var bytes = IcoDirectory.SingleFrame (png.ToArray (), backing.Width, backing.Height);
            outputStream.Write (bytes, 0, bytes.Length);
        }

        /// <summary>Creates an exact copy of this icon.</summary>
        public object Clone () => new Icon (backing?.Copy ()) { iconData = iconData };

        /// <inheritdoc/>
        public void Dispose ()
        {
            backing?.Dispose ();
            backing = null;
        }
    }

    /// <summary>The ICO container: a 6-byte header and one 16-byte directory entry per frame.</summary>
    internal static class IcoDirectory
    {
        private const int HeaderSize = 6;
        private const int EntrySize = 16;

        internal static bool IsIco (byte[] data)
            => data.Length >= HeaderSize && data[0] == 0 && data[1] == 0 && data[2] == 1 && data[3] == 0
               && Count (data) > 0 && data.Length >= HeaderSize + Count (data) * EntrySize;

        private static int Count (byte[] data) => data[4] | (data[5] << 8);

        // A width or height byte of 0 means 256.
        private static int Dimension (byte value) => value == 0 ? 256 : value;

        /// <summary>
        /// Decodes the frame that best fits <paramref name="width"/> x <paramref name="height"/>: an exact
        /// size with the most colour bits, else the smallest frame larger than asked (scaling down keeps
        /// detail), else the largest.
        /// </summary>
        internal static SKBitmap? DecodeBest (byte[] data, int width, int height)
        {
            if (!IsIco (data))
                return null;

            var best = -1;
            var bestScore = long.MinValue;

            for (var i = 0; i < Count (data); i++) {
                var entry = HeaderSize + i * EntrySize;
                int w = Dimension (data[entry]), h = Dimension (data[entry + 1]);
                var bits = data[entry + 6] | (data[entry + 7] << 8);

                long score = w == width && h == height ? 3_000_000L
                    : w >= width && h >= height ? 2_000_000L - (w * h)
                    : 1_000_000L + (w * h);
                score = score * 64 + bits;

                if (score > bestScore) {
                    bestScore = score;
                    best = i;
                }
            }

            if (best < 0)
                return null;

            var at = HeaderSize + best * EntrySize;
            var length = BitConverter.ToInt32 (data, at + 8);
            var offset = BitConverter.ToInt32 (data, at + 12);
            if (offset < 0 || length <= 0 || offset + (long)length > data.Length)
                return null;

            // Rewrap the one frame as a single-entry ICO: the codec then decodes exactly it, PNG or DIB.
            var single = new byte[HeaderSize + EntrySize + length];
            Array.Copy (data, 0, single, 0, 4);
            single[4] = 1;
            Array.Copy (data, at, single, HeaderSize, 12);
            BitConverter.GetBytes (HeaderSize + EntrySize).CopyTo (single, HeaderSize + 12);
            Array.Copy (data, offset, single, HeaderSize + EntrySize, length);

            try {
                return SKBitmap.Decode (single);
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
                return null;
            }
        }

        /// <summary>A one-frame ICO holding <paramref name="png"/>.</summary>
        internal static byte[] SingleFrame (byte[] png, int width, int height)
        {
            var bytes = new byte[HeaderSize + EntrySize + png.Length];
            bytes[2] = 1;
            bytes[4] = 1;
            bytes[HeaderSize] = (byte)(width >= 256 ? 0 : width);
            bytes[HeaderSize + 1] = (byte)(height >= 256 ? 0 : height);
            bytes[HeaderSize + 4] = 1;     // planes
            bytes[HeaderSize + 6] = 32;    // bits per pixel
            BitConverter.GetBytes (png.Length).CopyTo (bytes, HeaderSize + 8);
            BitConverter.GetBytes (HeaderSize + EntrySize).CopyTo (bytes, HeaderSize + 12);
            png.CopyTo (bytes, HeaderSize + EntrySize);
            return bytes;
        }
    }
}
