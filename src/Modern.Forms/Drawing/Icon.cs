using System;
using System.IO;
using SkiaSharp;

namespace Modern.Drawing
{
    /// <summary>
    /// Represents a Windows icon, backed by SkiaSharp pixel data. Cross-platform replacement for
    /// <c>System.Drawing.Icon</c>.
    /// </summary>
    public sealed class Icon : IDisposable, ICloneable
    {
        private SKBitmap? backing;

        /// <summary>Initializes a new icon from the specified file.</summary>
        public Icon (string fileName)
        {
            try { backing = SKBitmap.Decode (fileName); } catch { backing = null; }
        }

        /// <summary>Initializes a new icon from the specified stream.</summary>
        public Icon (Stream stream)
        {
            try { backing = SKBitmap.Decode (stream); } catch { backing = null; }
        }

        /// <summary>Initializes a new icon from an existing icon, scaled to the specified size.</summary>
        public Icon (Icon original, System.Drawing.Size size) : this (original, size.Width, size.Height) { }

        /// <summary>Initializes a new icon from an existing icon, scaled to the specified dimensions.</summary>
        public Icon (Icon original, int width, int height)
        {
            var source = original?.backing;
            if (source is null)
                return;

            backing = source.Resize (new SKImageInfo (Math.Max (1, width), Math.Max (1, height)), SKFilterQuality.High);
        }

        // Wraps an existing SKBitmap.
        internal Icon (SKBitmap? bitmap) => backing = bitmap;

        /// <summary>Gets the width of the icon.</summary>
        public int Width => backing?.Width ?? 0;

        /// <summary>Gets the height of the icon.</summary>
        public int Height => backing?.Height ?? 0;

        /// <summary>Gets the size of the icon.</summary>
        public System.Drawing.Size Size => new System.Drawing.Size (Width, Height);

        /// <summary>Gets the handle for this icon. Returns IntPtr.Zero in Modern.Drawing.</summary>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Gets the backing SkiaSharp bitmap (for renderer use).</summary>
        internal SKBitmap? GetSKBitmap () => backing;

        /// <summary>Converts this icon to a <see cref="Bitmap"/>.</summary>
        public Bitmap ToBitmap () => new Bitmap (backing?.Copy ());

        /// <summary>Saves this icon to the specified stream.</summary>
        public void Save (Stream outputStream)
        {
            if (backing is null)
                return;

            using var image = SKImage.FromBitmap (backing);
            using var data = image.Encode (SKEncodedImageFormat.Png, 100);
            data.SaveTo (outputStream);
        }

        /// <summary>Creates an exact copy of this icon.</summary>
        public object Clone () => new Icon (backing?.Copy ());

        /// <inheritdoc/>
        public void Dispose ()
        {
            backing?.Dispose ();
            backing = null;
        }
    }
}
