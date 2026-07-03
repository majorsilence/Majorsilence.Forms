using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>
    /// Specifies the file format of an image. Cross-platform replacement for
    /// <c>System.Drawing.Imaging.ImageFormat</c>.
    /// </summary>
    public sealed class ImageFormat
    {
        internal ImageFormat (string name, SKEncodedImageFormat skFormat)
        {
            Name = name;
            SKFormat = skFormat;
        }

        /// <summary>Gets the name of this image format.</summary>
        public string Name { get; }

        internal SKEncodedImageFormat SKFormat { get; }

        /// <summary>Gets the bitmap (BMP) image format.</summary>
        public static ImageFormat Bmp { get; } = new ImageFormat ("Bmp", SKEncodedImageFormat.Bmp);

        /// <summary>Gets the PNG image format.</summary>
        public static ImageFormat Png { get; } = new ImageFormat ("Png", SKEncodedImageFormat.Png);

        /// <summary>Gets the JPEG image format.</summary>
        public static ImageFormat Jpeg { get; } = new ImageFormat ("Jpeg", SKEncodedImageFormat.Jpeg);

        /// <summary>Gets the GIF image format.</summary>
        public static ImageFormat Gif { get; } = new ImageFormat ("Gif", SKEncodedImageFormat.Gif);

        /// <summary>Gets the W3C PNG image format (alias of <see cref="Png"/>).</summary>
        public static ImageFormat MemoryBmp { get; } = new ImageFormat ("MemoryBmp", SKEncodedImageFormat.Bmp);

        /// <summary>Gets the Windows icon image format (encoded as PNG).</summary>
        public static ImageFormat Icon { get; } = new ImageFormat ("Icon", SKEncodedImageFormat.Ico);

        /// <summary>Gets the TIFF image format.</summary>
        public static ImageFormat Tiff { get; } = new ImageFormat ("Tiff", SKEncodedImageFormat.Png);

        /// <summary>Gets the WMF image format (encoded as PNG).</summary>
        public static ImageFormat Wmf { get; } = new ImageFormat ("Wmf", SKEncodedImageFormat.Png);

        /// <summary>Gets the EMF image format (encoded as PNG).</summary>
        public static ImageFormat Emf { get; } = new ImageFormat ("Emf", SKEncodedImageFormat.Png);

        /// <summary>Gets the EXIF image format (encoded as JPEG).</summary>
        public static ImageFormat Exif { get; } = new ImageFormat ("Exif", SKEncodedImageFormat.Jpeg);

        internal SKEncodedImageFormat ToSKEncodedImageFormat () => SKFormat;

        internal static ImageFormat FromFileName (string filename)
        {
            var ext = System.IO.Path.GetExtension (filename)?.ToLowerInvariant ();
            return ext switch {
                ".jpg" or ".jpeg" => Jpeg,
                ".gif" => Gif,
                ".bmp" => Bmp,
                ".ico" => Icon,
                ".tif" or ".tiff" => Tiff,
                _ => Png
            };
        }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>
    /// Specifies the pixel format of an image. Majorsilence.Forms.Drawing always stores 32bpp ARGB; the other
    /// members are provided for source compatibility.
    /// </summary>
    public enum PixelFormat
    {
        /// <summary>The pixel format is undefined.</summary>
        Undefined = 0,
        /// <summary>16 bits per pixel, 555 RGB.</summary>
        Format16bppRgb555 = 135173,
        /// <summary>16 bits per pixel, 565 RGB.</summary>
        Format16bppRgb565 = 135174,
        /// <summary>24 bits per pixel, RGB.</summary>
        Format24bppRgb = 137224,
        /// <summary>32 bits per pixel, RGB.</summary>
        Format32bppRgb = 139273,
        /// <summary>32 bits per pixel, ARGB.</summary>
        Format32bppArgb = 2498570,
        /// <summary>32 bits per pixel, premultiplied ARGB.</summary>
        Format32bppPArgb = 925707,
        /// <summary>8 bits per pixel, indexed.</summary>
        Format8bppIndexed = 198659
    }

    /// <summary>Specifies the access mode used when locking bitmap bits. Stub in Majorsilence.Forms.Drawing.</summary>
    public enum ImageLockMode
    {
        /// <summary>Read-only access.</summary>
        ReadOnly = 1,
        /// <summary>Write-only access.</summary>
        WriteOnly = 2,
        /// <summary>Read and write access.</summary>
        ReadWrite = 3,
        /// <summary>The buffer is user-allocated.</summary>
        UserInputBuffer = 4
    }

    /// <summary>
    /// Identifies an image encoder parameter (e.g., quality or compression type).
    /// Cross-platform replacement for <c>System.Drawing.Imaging.Encoder</c>.
    /// </summary>
    public sealed class Encoder
    {
        /// <summary>Encoder parameter for image quality (0–100).</summary>
        public static readonly Encoder Quality = new Encoder ("Quality");

        /// <summary>Encoder parameter for compression type.</summary>
        public static readonly Encoder Compression = new Encoder ("Compression");

        /// <summary>Gets the name of this encoder parameter.</summary>
        public string ParameterName { get; }

        private Encoder (string name) => ParameterName = name;
    }

    /// <summary>
    /// Represents a single parameter passed to an image encoder.
    /// Cross-platform replacement for <c>System.Drawing.Imaging.EncoderParameter</c>.
    /// </summary>
    public sealed class EncoderParameter : IDisposable
    {
        /// <summary>Gets the encoder this parameter is for.</summary>
        public Encoder Encoder { get; }

        /// <summary>Gets the parameter value.</summary>
        public object Value { get; }

        /// <summary>Initializes a new EncoderParameter with the given encoder and value.</summary>
        public EncoderParameter (Encoder encoder, object value)
        {
            ArgumentNullException.ThrowIfNull (encoder);
            Encoder = encoder;
            Value = value;
        }

        /// <summary>Initializes a new EncoderParameter with a long integer value.</summary>
        public EncoderParameter (Encoder encoder, long value) : this (encoder, (object)value) { }

        /// <inheritdoc/>
        public void Dispose () { }
    }

    /// <summary>
    /// A collection of <see cref="EncoderParameter"/> objects passed to an image encoder.
    /// Cross-platform replacement for <c>System.Drawing.Imaging.EncoderParameters</c>.
    /// </summary>
    public sealed class EncoderParameters : IDisposable
    {
        private readonly List<EncoderParameter> _list;

        /// <summary>Initializes an empty EncoderParameters collection.</summary>
        public EncoderParameters () => _list = new List<EncoderParameter> ();

        /// <summary>Initializes an EncoderParameters collection with the specified capacity.</summary>
        public EncoderParameters (int count) => _list = new List<EncoderParameter> (count);

        /// <summary>Gets or sets the parameter array (for WinForms compat — setting replaces all entries).</summary>
        public EncoderParameter[] Param {
            get => _list.ToArray ();
            set {
                _list.Clear ();
                if (value is not null)
                    _list.AddRange (value);
            }
        }

        /// <summary>Adds an encoder parameter to the collection.</summary>
        public void Add (EncoderParameter param)
        {
            if (param is not null) _list.Add (param);
        }

        /// <summary>Returns all encoder parameters as an array.</summary>
        public EncoderParameter[] GetParameters () => _list.ToArray ();

        /// <inheritdoc/>
        public void Dispose () { }
    }

    /// <summary>
    /// Describes an image codec (encoder/decoder). Cross-platform replacement for
    /// <c>System.Drawing.Imaging.ImageCodecInfo</c>.
    /// </summary>
    public sealed class ImageCodecInfo
    {
        /// <summary>Gets or sets the unique identifier for this codec.</summary>
        public Guid Clsid { get; set; }

        /// <summary>Gets or sets the MIME type string (e.g., "image/jpeg").</summary>
        public string MimeType { get; set; } = string.Empty;

        /// <summary>Gets or sets the image format this codec handles.</summary>
        public ImageFormat? Format { get; set; }

        /// <summary>Gets or sets the human-readable format description.</summary>
        public string FormatDescription { get; set; } = string.Empty;

        /// <summary>Returns a list of available image encoders.</summary>
        public static ImageCodecInfo[] GetImageEncoders () => new ImageCodecInfo[] {
            new ImageCodecInfo { Clsid = new Guid ("557cf400-1a04-11d3-9a73-0000f81ef32e"), MimeType = "image/bmp",  Format = ImageFormat.Bmp,  FormatDescription = "BMP"  },
            new ImageCodecInfo { Clsid = new Guid ("557cf401-1a04-11d3-9a73-0000f81ef32e"), MimeType = "image/jpeg", Format = ImageFormat.Jpeg, FormatDescription = "JPEG" },
            new ImageCodecInfo { Clsid = new Guid ("557cf402-1a04-11d3-9a73-0000f81ef32e"), MimeType = "image/gif",  Format = ImageFormat.Gif,  FormatDescription = "GIF"  },
            new ImageCodecInfo { Clsid = new Guid ("557cf403-1a04-11d3-9a73-0000f81ef32e"), MimeType = "image/tiff", Format = ImageFormat.Tiff, FormatDescription = "TIFF" },
            new ImageCodecInfo { Clsid = new Guid ("557cf406-1a04-11d3-9a73-0000f81ef32e"), MimeType = "image/png",  Format = ImageFormat.Png,  FormatDescription = "PNG"  },
        };

        /// <summary>Returns a list of available image decoders.</summary>
        public static ImageCodecInfo[] GetImageDecoders () => GetImageEncoders ();
    }

    /// <summary>
    /// Writes a multi-page little-endian TIFF file. Each page is written by <see cref="WritePage"/>
    /// and the stream is finalized by calling <see cref="Finish"/> or <see cref="Dispose"/>.
    /// </summary>
    public sealed class TiffWriter : IDisposable
    {
        private readonly BinaryWriter _w;
        private bool _disposed;
        private long _pendingIfdOffsetPos;

        /// <summary>Initializes a new TiffWriter that writes to the specified stream.</summary>
        public TiffWriter (Stream stream)
        {
            ArgumentNullException.ThrowIfNull (stream);
            _w = new BinaryWriter (stream, System.Text.Encoding.ASCII, leaveOpen: true);
            _w.Write ((byte)'I');  // little-endian marker
            _w.Write ((byte)'I');
            _w.Write ((ushort)42); // TIFF magic
            _pendingIfdOffsetPos = stream.Position;
            _w.Write ((uint)0);    // placeholder: offset to first IFD
        }

        /// <summary>Appends one page to the TIFF.</summary>
        /// <param name="bitmap">Source bitmap.</param>
        /// <param name="color">True for 24-bit RGB output; false for 1-bit bitonal.</param>
        /// <param name="dpiX">Horizontal resolution in dots per inch.</param>
        /// <param name="dpiY">Vertical resolution in dots per inch.</param>
        public void WritePage (SKBitmap bitmap, bool color, float dpiX, float dpiY)
        {
            ArgumentNullException.ThrowIfNull (bitmap);

            int width = bitmap.Width;
            int height = bitmap.Height;

            byte[] imageData;
            int samplesPerPixel;
            int bitsPerSample;
            int photometric;

            if (color)
            {
                samplesPerPixel = 3;
                bitsPerSample   = 8;
                photometric     = 2; // RGB
                imageData = new byte[width * height * 3];
                int idx = 0;
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var px = bitmap.GetPixel (x, y);
                        imageData[idx++] = px.Red;
                        imageData[idx++] = px.Green;
                        imageData[idx++] = px.Blue;
                    }
            }
            else
            {
                samplesPerPixel = 1;
                bitsPerSample   = 1;
                photometric     = 0; // WhiteIsZero
                int rowBytes = (width + 7) / 8;
                imageData = new byte[rowBytes * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var px = bitmap.GetPixel (x, y);
                        float lum = px.Red * 0.299f + px.Green * 0.587f + px.Blue * 0.114f;
                        if (lum < 128f) // black → 1 in WhiteIsZero
                            imageData[y * rowBytes + x / 8] |= (byte)(0x80 >> (x % 8));
                    }
            }

            var stream = _w.BaseStream;

            // --- write image data ---
            long imageDataOffset = stream.Position;
            _w.Write (imageData);

            // --- write BitsPerSample extra data for RGB [8,8,8] ---
            long bpsOffset = 0;
            if (color)
            {
                bpsOffset = stream.Position;
                _w.Write ((ushort)8);
                _w.Write ((ushort)8);
                _w.Write ((ushort)8);
            }

            // --- write XResolution and YResolution as RATIONAL (numerator/denominator LONGs) ---
            long xResOffset = stream.Position;
            _w.Write ((uint)Math.Max (1, (uint)MathF.Round (dpiX)));
            _w.Write ((uint)1);
            long yResOffset = stream.Position;
            _w.Write ((uint)Math.Max (1, (uint)MathF.Round (dpiY)));
            _w.Write ((uint)1);

            // --- patch the pending IFD offset to point here ---
            long ifdPosition = stream.Position;
            stream.Seek (_pendingIfdOffsetPos, SeekOrigin.Begin);
            _w.Write ((uint)ifdPosition);
            stream.Seek (ifdPosition, SeekOrigin.Begin);

            // --- build IFD entries (must be sorted ascending by tag) ---
            // type codes: 3=SHORT (uint16), 4=LONG (uint32), 5=RATIONAL (two uint32)
            var entries = new (ushort tag, ushort type, uint count, uint value)[]
            {
                (254, 4, 1, 2u),                         // NewSubfileType: multi-page
                (256, 4, 1, (uint)width),                // ImageWidth
                (257, 4, 1, (uint)height),               // ImageLength
                color
                    ? ((ushort)258, (ushort)3, 3u, (uint)bpsOffset)  // BitsPerSample → offset
                    : ((ushort)258, (ushort)3, 1u, (uint)bitsPerSample), // BitsPerSample = 1
                (259, 3, 1, 1u),                         // Compression: none
                (262, 3, 1, (uint)photometric),          // PhotometricInterpretation
                (273, 4, 1, (uint)imageDataOffset),      // StripOffsets
                (277, 3, 1, (uint)samplesPerPixel),      // SamplesPerPixel
                (278, 4, 1, (uint)height),               // RowsPerStrip
                (279, 4, 1, (uint)imageData.Length),     // StripByteCounts
                (282, 5, 1, (uint)xResOffset),           // XResolution
                (283, 5, 1, (uint)yResOffset),           // YResolution
                (296, 3, 1, 2u),                         // ResolutionUnit: inch
            };

            _w.Write ((ushort)entries.Length);
            foreach (var e in entries)
            {
                _w.Write (e.tag);
                _w.Write (e.type);
                _w.Write (e.count);
                _w.Write (e.value);
            }

            // save position of "next IFD" pointer so the next page can patch it
            _pendingIfdOffsetPos = stream.Position;
            _w.Write ((uint)0); // next IFD = none (patched by next WritePage if any)
        }

        /// <summary>Flushes and finalizes the TIFF stream.</summary>
        public void Finish () => _w.Flush ();

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed) return;
            _disposed = true;
            Finish ();
            _w.Dispose ();
        }
    }
}
