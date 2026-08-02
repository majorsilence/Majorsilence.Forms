using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // The bytes this image was decoded from, retained only when they are still needed afterwards:
        // to decode further frames, or to answer metadata queries. A single-frame image with no EXIF
        // drops them, so the common case does not pay to hold its source twice. See RetainSource.
        private protected byte[]? encodedSource;

        private List<PropertyItem>? propertyItems;
        private int frameCount = 1;

        /// <summary>
        /// Records the encoded bytes an image was decoded from, keeping them only if they will still be
        /// useful: a multi-frame image needs them to select another frame, and any image with metadata
        /// needs them to answer property queries.
        /// </summary>
        private protected void RetainSource (byte[]? data)
        {
            if (data is null || data.Length == 0)
                return;

            try {
                using var codec = SKCodec.Create (new SKMemoryStream (data));
                frameCount = Math.Max (1, codec?.FrameCount ?? 1);
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
                frameCount = 1;
            }

            propertyItems = ExifReader.Read (data);
            if (frameCount > 1 || propertyItems.Count > 0)
                encodedSource = data;
        }

        /// <summary>Gets attribute flags for the pixel data of this image.</summary>
        /// <remarks>
        /// Reports the handful of <see cref="ImageFlags"/> that are knowable here: every surface is a
        /// readable, writable, 32bpp color bitmap. GDI+'s colorimetry flags are not modelled.
        /// </remarks>
        public int Flags => (int)(ImageFlags.HasAlpha | ImageFlags.ColorSpaceRgb);

        /// <summary>Gets or sets an arbitrary object associated with this image.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the bounds of this image in the specified unit of measure.</summary>
        /// <param name="pageUnit">
        /// Set to <see cref="GraphicsUnit.Pixel"/> on return: the bounds are always reported in pixels,
        /// which is the unit the whole drawing layer works in.
        /// </param>
        public System.Drawing.RectangleF GetBounds (ref GraphicsUnit pageUnit)
        {
            pageUnit = GraphicsUnit.Pixel;
            return new System.Drawing.RectangleF (0, 0, Width, Height);
        }

        /// <summary>
        /// Gets or sets the color palette associated with this image. See <see cref="ColorPalette"/> for
        /// why assigning one does not re-quantize the pixels.
        /// </summary>
        public ColorPalette Palette { get; set; } = new ColorPalette (0);

        // ---- Frames ----

        /// <summary>
        /// Gets the GUIDs of the dimensions along which this image holds multiple frames — the time
        /// dimension for an animation, otherwise the page dimension.
        /// </summary>
        public Guid[] FrameDimensionsList =>
            frameCount > 1 ? [FrameDimension.Time.Guid] : [FrameDimension.Page.Guid];

        /// <summary>Gets the number of frames this image holds along the specified dimension.</summary>
        public int GetFrameCount (FrameDimension dimension) => frameCount;

        /// <summary>
        /// Selects the frame that subsequent drawing and pixel access will use, decoding it in place.
        /// </summary>
        /// <returns>The zero-based index of the frame now active.</returns>
        /// <remarks>
        /// Backed by a real <c>SKCodec</c> decode of that frame, so animated GIF playback works. An
        /// index outside the frame range, or an image whose source bytes were not retained (a
        /// single-frame image), leaves the current frame in place.
        /// </remarks>
        public int SelectActiveFrame (FrameDimension dimension, int frameIndex)
        {
            if (encodedSource is null || frameIndex < 0 || frameIndex >= frameCount)
                return 0;

            try {
                using var codec = SKCodec.Create (new SKMemoryStream (encodedSource));
                if (codec is null)
                    return 0;

                var info = new SKImageInfo (codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var decoded = new SKBitmap (info);
                var options = new SKCodecOptions (frameIndex);
                if (codec.GetPixels (info, decoded.GetPixels (), options) is SKCodecResult.Success or SKCodecResult.IncompleteInput) {
                    backing?.Dispose ();
                    backing = decoded;
                    return frameIndex;
                }
                decoded.Dispose ();
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
                // A codec that cannot seek leaves the current frame showing.
            }

            return 0;
        }

        /// <summary>
        /// Adds a frame to a multi-frame file being written. Not supported: the Skia encoders here write
        /// a single image per file, so this is a documented no-op rather than a silent partial write.
        /// </summary>
        public void SaveAdd (EncoderParameters? encoderParams) { }

        /// <inheritdoc cref="SaveAdd(EncoderParameters)"/>
        public void SaveAdd (Image image, EncoderParameters? encoderParams) { }

        // ---- Metadata ----

        /// <summary>Gets the IDs of the metadata properties stored in this image.</summary>
        public int[] PropertyIdList => (propertyItems ?? []).Select (p => p.Id).ToArray ();

        /// <summary>Gets all metadata properties stored in this image.</summary>
        public PropertyItem[] PropertyItems => (propertyItems ?? []).ToArray ();

        /// <summary>Gets the metadata property with the specified ID.</summary>
        /// <exception cref="ArgumentException">No property with that ID is present.</exception>
        public PropertyItem GetPropertyItem (int propid)
            => (propertyItems ?? []).FirstOrDefault (p => p.Id == propid)
               ?? throw new ArgumentException ($"The image has no property with id {propid}.", nameof (propid));

        /// <summary>Adds or replaces a metadata property on this image.</summary>
        public void SetPropertyItem (PropertyItem item)
        {
            if (item is null)
                return;
            propertyItems ??= [];
            propertyItems.RemoveAll (p => p.Id == item.Id);
            propertyItems.Add (item);
        }

        /// <summary>Removes the metadata property with the specified ID.</summary>
        public void RemovePropertyItem (int propid) => propertyItems?.RemoveAll (p => p.Id == propid);

        /// <summary>Gets the encoder parameters supported by the encoder for the specified format.</summary>
        public EncoderParameters GetEncoderParameterList (Guid encoder) => new (0);

        // ---- PixelFormat helpers (pure bit math over the enum's encoding) ----

        /// <summary>Returns the color depth, in bits per pixel, of the specified pixel format.</summary>
        public static int GetPixelFormatSize (PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;

        /// <summary>Returns whether the specified pixel format contains alpha information.</summary>
        public static bool IsAlphaPixelFormat (PixelFormat pixfmt) => ((int)pixfmt & (int)PixelFormat.Alpha) != 0;

        /// <summary>Returns whether the specified pixel format is one of the canonical formats.</summary>
        public static bool IsCanonicalPixelFormat (PixelFormat pixfmt) => ((int)pixfmt & (int)PixelFormat.Canonical) != 0;

        /// <summary>Returns whether the specified pixel format is an extended (16-bits-per-channel) format.</summary>
        public static bool IsExtendedPixelFormat (PixelFormat pixfmt) => ((int)pixfmt & (int)PixelFormat.Extended) != 0;

        /// <summary>Callback invoked during <c>GetThumbnailImage</c> to allow cancellation.</summary>
        public delegate bool GetThumbnailImageAbort ();

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
        public static Image FromBytes (byte[] data) => new Bitmap (new MemoryStream (data ?? []));

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
        public virtual void Dispose ()
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
            var data = File.Exists (filename) ? File.ReadAllBytes (filename) : null;
            backing = DecodeOrPlaceholder (data);
            RetainSource (data);
        }

        /// <summary>Initializes a new bitmap from the specified file.</summary>
        public Bitmap (string filename, bool useIcm) : this (filename) { }

        /// <summary>Initializes a new bitmap from the specified stream.</summary>
        public Bitmap (Stream stream)
        {
            byte[]? data = null;
            if (stream is not null) {
                using var buffer = new MemoryStream ();
                stream.CopyTo (buffer);
                data = buffer.ToArray ();
            }
            backing = DecodeOrPlaceholder (data);
            RetainSource (data);
        }

        // SKBitmap.Decode throws (rather than returning null) when it cannot create a codec for the
        // data, so undecodable or truncated bytes must not be allowed to escape as an exception from a
        // constructor: System.Drawing.Bitmap surfaces that as an ArgumentException at most, and callers
        // here have always been able to rely on getting *an* image back.
        private static SKBitmap DecodeOrPlaceholder (byte[]? data)
        {
            if (data is null || data.Length == 0)
                return new SKBitmap (1, 1);
            try {
                return SKBitmap.Decode (data) ?? new SKBitmap (1, 1);
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) {
                return new SKBitmap (1, 1);
            }
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

        // Outstanding LockBits buffer, if any. GDI+ allows only one lock at a time per bitmap.
        private BitmapData? locked;

        /// <summary>
        /// Locks a rectangular region of this bitmap into a contiguous pixel buffer for bulk access,
        /// the cross-platform equivalent of <c>System.Drawing.Bitmap.LockBits</c>. Read or write the
        /// buffer through <see cref="BitmapData.Scan0"/>/<see cref="BitmapData.Stride"/>, then call
        /// <see cref="UnlockBits"/> to release it (and, unless the lock was
        /// <see cref="ImageLockMode.ReadOnly"/>, write the pixels back into the bitmap).
        /// </summary>
        /// <param name="rect">The region of this bitmap to lock.</param>
        /// <param name="flags">Whether the buffer will be read, written, or both.</param>
        /// <param name="format">
        /// The layout to present the pixels in. <see cref="PixelFormat.Format32bppArgb"/>,
        /// <see cref="PixelFormat.Format32bppPArgb"/>, <see cref="PixelFormat.Format32bppRgb"/> and
        /// <see cref="PixelFormat.Format24bppRgb"/> are laid out directly; anything narrower widens
        /// to <see cref="PixelFormat.Format32bppArgb"/> (the backing store is always 32bpp), and the
        /// returned <see cref="BitmapData.PixelFormat"/> reports what was actually produced.
        /// </param>
        public BitmapData LockBits (System.Drawing.Rectangle rect, ImageLockMode flags, PixelFormat format)
        {
            ObjectDisposedException.ThrowIf (backing is null, this);
            if (locked is not null)
                throw new InvalidOperationException ("The bitmap region is already locked. Call UnlockBits first.");

            var region = System.Drawing.Rectangle.Intersect (rect, new System.Drawing.Rectangle (0, 0, Width, Height));
            if (region.Width <= 0 || region.Height <= 0)
                throw new ArgumentException ("The lock rectangle does not intersect the bitmap.", nameof (rect));

            var actual = BitmapDataMarshal.Normalize (format);
            var stride = BitmapDataMarshal.StrideFor (region.Width, actual);
            var scan0 = BitmapDataMarshal.CopyOut (backing, region, actual, stride, out var byteCount);

            locked = new BitmapData {
                Scan0 = scan0,
                Stride = stride,
                Width = region.Width,
                Height = region.Height,
                PixelFormat = actual,
                LockedRegion = region,
                LockMode = flags,
                BufferLength = byteCount,
                Owner = this,
            };
            return locked;
        }

        /// <summary>Locks the whole bitmap. Convenience overload matching System.Drawing.</summary>
        public BitmapData LockBits (System.Drawing.Rectangle rect, ImageLockMode flags)
            => LockBits (rect, flags, PixelFormat.Format32bppArgb);

        /// <summary>
        /// Releases a buffer previously returned by <see cref="LockBits(System.Drawing.Rectangle, ImageLockMode, PixelFormat)"/>,
        /// copying it back into the bitmap unless the lock was <see cref="ImageLockMode.ReadOnly"/>.
        /// </summary>
        public void UnlockBits (BitmapData bitmapData)
        {
            ArgumentNullException.ThrowIfNull (bitmapData);
            if (!ReferenceEquals (bitmapData, locked))
                throw new ArgumentException ("The BitmapData was not produced by a LockBits call on this bitmap.", nameof (bitmapData));

            try {
                if (backing is not null && bitmapData.LockMode != ImageLockMode.ReadOnly && bitmapData.Scan0 != IntPtr.Zero) {
                    BitmapDataMarshal.CopyIn (backing, bitmapData.Scan0, bitmapData.BufferLength,
                        bitmapData.LockedRegion, bitmapData.PixelFormat, bitmapData.Stride);
                }
            } finally {
                ReleaseLock ();
            }
        }

        private void ReleaseLock ()
        {
            if (locked is null)
                return;
            if (locked.Scan0 != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.FreeHGlobal (locked.Scan0);
            locked.Scan0 = IntPtr.Zero;
            locked.Owner = null;
            locked = null;
        }

        /// <summary>
        /// Releases the resources used by this bitmap, including any pixel buffer still held by an
        /// unbalanced <see cref="LockBits(System.Drawing.Rectangle, ImageLockMode, PixelFormat)"/>
        /// (those pixels are discarded, not written back).
        /// </summary>
        public override void Dispose ()
        {
            ReleaseLock ();
            base.Dispose ();
        }
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
