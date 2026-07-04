using System;
using System.IO;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Cross-platform, SkiaSharp-backed replacement for <c>System.Drawing.Image</c>. Holds decoded
    /// pixel data as an <see cref="SKBitmap"/> so it works identically on Windows, macOS and Linux
    /// (unlike System.Drawing.Common, which requires GDI+ and throws on non-Windows platforms).
    /// </summary>
    public abstract class Image : IDisposable, ICloneable
    {
        // Backing pixel store. Owned by this Image; disposed with it.
        private protected SKBitmap? backing;

        /// <summary>Gets the width, in pixels, of this image.</summary>
        public int Width => backing?.Width ?? 0;

        /// <summary>Gets the height, in pixels, of this image.</summary>
        public int Height => backing?.Height ?? 0;

        /// <summary>Gets the size, in pixels, of this image.</summary>
        public System.Drawing.Size Size => new System.Drawing.Size (Width, Height);

        /// <summary>Gets the width and height of this image.</summary>
        public System.Drawing.SizeF PhysicalDimension => new System.Drawing.SizeF (Width, Height);

        /// <summary>Gets the pixel format of this image. Always 32bpp ARGB in Majorsilence.Forms.Drawing.</summary>
        public PixelFormat PixelFormat => PixelFormat.Format32bppArgb;

        /// <summary>Gets the file format of this image.</summary>
        public ImageFormat RawFormat { get; internal set; } = ImageFormat.Png;

        /// <summary>Gets the horizontal resolution, in DPI, of this image.</summary>
        public float HorizontalResolution { get; internal set; } = 96f;

        /// <summary>Gets the vertical resolution, in DPI, of this image.</summary>
        public float VerticalResolution { get; internal set; } = 96f;

        /// <summary>Gets the backing SkiaSharp bitmap (for renderer use).</summary>
        internal SKBitmap? GetSKBitmap () => backing;

        /// <summary>Loads an image from the specified file.</summary>
        public static Image FromFile (string filename) => new Bitmap (filename);

        /// <summary>Loads an image from the specified file.</summary>
        public static Image FromFile (string filename, bool useEmbeddedColorManagement) => new Bitmap (filename);

        /// <summary>Loads an image from the specified data stream.</summary>
        public static Image FromStream (Stream stream) => new Bitmap (stream);

        /// <summary>Loads an image from the specified data stream.</summary>
        public static Image FromStream (Stream stream, bool useEmbeddedColorManagement) => new Bitmap (stream);

        /// <summary>Loads an image from the specified data stream.</summary>
        public static Image FromStream (Stream stream, bool useEmbeddedColorManagement, bool validateImageData) => new Bitmap (stream);

        /// <summary>Creates an Image from a byte array of encoded image data.</summary>
        public static Image FromBytes (byte[] data) => new Bitmap (SKBitmap.Decode (data));

        /// <summary>Saves this image to the specified file, inferring the format from the extension.</summary>
        public void Save (string filename) => Save (filename, ImageFormat.FromFileName (filename));

        /// <summary>Saves this image to the specified file in the specified format.</summary>
        public void Save (string filename, ImageFormat format)
        {
            using var stream = File.Create (filename);
            Save (stream, format);
        }

        /// <summary>Saves this image to the specified stream in the specified format.</summary>
        public void Save (Stream stream, ImageFormat format) => Save (stream, format, 100);

        /// <summary>
        /// Saves this image to the specified stream using the format and quality described by an
        /// ImageCodecInfo/EncoderParameters pair -- WinForms compatibility for code that picks a
        /// codec via ImageCodecInfo.GetImageEncoders() and sets Encoder.Quality explicitly.
        /// </summary>
        public void Save (Stream stream, ImageCodecInfo? codec, EncoderParameters? encoderParams)
        {
            var format = codec?.Format ?? ImageFormat.Png;
            var quality = 100;
            if (encoderParams is not null) {
                foreach (var p in encoderParams.GetParameters ()) {
                    if (ReferenceEquals (p.Encoder, Encoder.Quality) && p.Value is long q)
                        quality = (int)q;
                }
            }
            Save (stream, format, quality);
        }

        private void Save (Stream stream, ImageFormat format, int quality)
        {
            if (backing is null)
                return;

            using var image = SKImage.FromBitmap (backing);
            using var data = image.Encode (format.ToSKEncodedImageFormat (), quality);
            data.SaveTo (stream);
        }

        /// <summary>Returns a thumbnail of this image at the requested size.</summary>
        public Image GetThumbnailImage (int thumbWidth, int thumbHeight, Func<bool>? callback = null, IntPtr callbackData = default)
            => new Bitmap (this, thumbWidth, thumbHeight);

        /// <summary>Rotates and/or flips this image.</summary>
        public void RotateFlip (RotateFlipType rotateFlipType)
        {
            if (backing is null || rotateFlipType == RotateFlipType.RotateNoneFlipNone)
                return;

            backing = SkiaImageOps.RotateFlip (backing, rotateFlipType);
        }

        /// <summary>Creates an exact copy of this image.</summary>
        public object Clone () => new Bitmap (backing?.Copy ());

        /// <summary>Releases the resources used by this image.</summary>
        public void Dispose ()
        {
            backing?.Dispose ();
            backing = null;
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>
    /// Cross-platform, SkiaSharp-backed replacement for <c>System.Drawing.Bitmap</c>.
    /// </summary>
    public sealed class Bitmap : Image
    {
        /// <summary>Initializes a new bitmap from the specified file.</summary>
        public Bitmap (string filename)
        {
            backing = SKBitmap.Decode (filename) ?? new SKBitmap (1, 1);
        }

        /// <summary>Initializes a new bitmap from the specified file.</summary>
        public Bitmap (string filename, bool useIcm) : this (filename) { }

        /// <summary>Initializes a new bitmap from the specified stream.</summary>
        public Bitmap (Stream stream)
        {
            backing = SKBitmap.Decode (stream) ?? new SKBitmap (1, 1);
        }

        /// <summary>Initializes a new bitmap from the specified stream.</summary>
        public Bitmap (Stream stream, bool useIcm) : this (stream) { }

        /// <summary>Initializes a new blank bitmap with the specified dimensions.</summary>
        public Bitmap (int width, int height)
        {
            backing = new SKBitmap (Math.Max (1, width), Math.Max (1, height), SKColorType.Bgra8888, SKAlphaType.Premul);
        }

        /// <summary>Initializes a new blank bitmap with the specified dimensions and pixel format.</summary>
        public Bitmap (int width, int height, PixelFormat format) : this (width, height) { }

        /// <summary>Initializes a new bitmap as a copy of an existing image.</summary>
        public Bitmap (Image original)
        {
            backing = original?.GetSKBitmap ()?.Copy () ?? new SKBitmap (1, 1);
        }

        /// <summary>Initializes a new bitmap by resizing an existing image to the specified size.</summary>
        public Bitmap (Image original, System.Drawing.Size size) : this (original, size.Width, size.Height) { }

        /// <summary>Initializes a new bitmap by resizing an existing image to the specified dimensions.</summary>
        public Bitmap (Image original, int width, int height)
        {
            var source = original?.GetSKBitmap ();
            width = Math.Max (1, width);
            height = Math.Max (1, height);

            if (source is null) {
                backing = new SKBitmap (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                return;
            }

            backing = source.Resize (new SKImageInfo (width, height), new SKSamplingOptions (SKCubicResampler.Mitchell)) ?? source.Copy ();
        }

        // Wraps an existing SKBitmap (takes ownership). Used by conversion helpers.
        internal Bitmap (SKBitmap? bitmap)
        {
            backing = bitmap ?? new SKBitmap (1, 1);
        }

        /// <summary>Gets the color of the specified pixel.</summary>
        public System.Drawing.Color GetPixel (int x, int y)
        {
            if (backing is null)
                return System.Drawing.Color.Empty;

            var c = backing.GetPixel (x, y);
            return System.Drawing.Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue);
        }

        /// <summary>Sets the color of the specified pixel.</summary>
        public void SetPixel (int x, int y, System.Drawing.Color color)
            => backing?.SetPixel (x, y, new SKColor (color.R, color.G, color.B, color.A));

        /// <summary>Makes the default transparent color transparent. No-op in Majorsilence.Forms.Drawing.</summary>
        public void MakeTransparent () { }

        /// <summary>Makes the specified color transparent. No-op in Majorsilence.Forms.Drawing.</summary>
        public void MakeTransparent (System.Drawing.Color transparentColor) { }

        /// <summary>Sets the resolution for this bitmap.</summary>
        public void SetResolution (float xDpi, float yDpi)
        {
            HorizontalResolution = xDpi;
            VerticalResolution = yDpi;
        }

        /// <summary>Returns a GDI icon handle. Returns IntPtr.Zero in Majorsilence.Forms.Drawing.</summary>
        public IntPtr GetHicon () => IntPtr.Zero;
    }

    // Skia helpers for image operations that need a fresh bitmap.
    internal static class SkiaImageOps
    {
        public static SKBitmap RotateFlip (SKBitmap source, RotateFlipType type)
        {
            var rotate90 = type is RotateFlipType.Rotate90FlipNone or RotateFlipType.Rotate270FlipNone
                or RotateFlipType.Rotate90FlipX or RotateFlipType.Rotate270FlipX;

            var dest = rotate90
                ? new SKBitmap (source.Height, source.Width, source.ColorType, source.AlphaType)
                : new SKBitmap (source.Width, source.Height, source.ColorType, source.AlphaType);

            using var surface = new SKCanvas (dest);
            var degrees = type switch {
                RotateFlipType.Rotate90FlipNone or RotateFlipType.Rotate90FlipX => 90,
                RotateFlipType.Rotate180FlipNone or RotateFlipType.Rotate180FlipX => 180,
                RotateFlipType.Rotate270FlipNone or RotateFlipType.Rotate270FlipX => 270,
                _ => 0
            };

            surface.Translate (dest.Width / 2f, dest.Height / 2f);
            surface.RotateDegrees (degrees);
            surface.Translate (-source.Width / 2f, -source.Height / 2f);
            surface.DrawBitmap (source, 0, 0);

            return dest;
        }
    }
}
