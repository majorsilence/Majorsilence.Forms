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
        internal ImageFormat (string name, SKEncodedImageFormat skFormat, string guid)
        {
            Name = name;
            SKFormat = skFormat;
            Guid = new Guid (guid);
        }

        /// <summary>
        /// Gets the GUID identifying this format. These are GDI+'s own format GUIDs, so a value read
        /// from designer-serialized or persisted data still compares equal.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>Gets the name of this image format.</summary>
        public string Name { get; }

        internal SKEncodedImageFormat SKFormat { get; }

        /// <summary>Gets the bitmap (BMP) image format.</summary>
        public static ImageFormat Bmp { get; } = new ImageFormat ("Bmp", SKEncodedImageFormat.Bmp, "b96b3cab-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the PNG image format.</summary>
        public static ImageFormat Png { get; } = new ImageFormat ("Png", SKEncodedImageFormat.Png, "b96b3caf-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the JPEG image format.</summary>
        public static ImageFormat Jpeg { get; } = new ImageFormat ("Jpeg", SKEncodedImageFormat.Jpeg, "b96b3cae-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the GIF image format.</summary>
        public static ImageFormat Gif { get; } = new ImageFormat ("Gif", SKEncodedImageFormat.Gif, "b96b3cb0-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the W3C PNG image format (alias of <see cref="Png"/>).</summary>
        public static ImageFormat MemoryBmp { get; } = new ImageFormat ("MemoryBmp", SKEncodedImageFormat.Bmp, "b96b3caa-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the Windows icon image format (encoded as PNG).</summary>
        public static ImageFormat Icon { get; } = new ImageFormat ("Icon", SKEncodedImageFormat.Ico, "b96b3cb5-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the TIFF image format.</summary>
        public static ImageFormat Tiff { get; } = new ImageFormat ("Tiff", SKEncodedImageFormat.Png, "b96b3cb1-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the WMF image format (encoded as PNG).</summary>
        public static ImageFormat Wmf { get; } = new ImageFormat ("Wmf", SKEncodedImageFormat.Png, "b96b3cad-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the EMF image format (encoded as PNG).</summary>
        public static ImageFormat Emf { get; } = new ImageFormat ("Emf", SKEncodedImageFormat.Png, "b96b3cac-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>Gets the EXIF image format (encoded as JPEG).</summary>
        public static ImageFormat Exif { get; } = new ImageFormat ("Exif", SKEncodedImageFormat.Jpeg, "b96b3cb2-0728-11d3-9d7b-0000f81ef32e");

        /// <summary>
        /// Gets the WebP image format, which SkiaSharp encodes and decodes natively.
        /// </summary>
        /// <remarks>
        /// GDI+ predates WebP and has no GUID for it, so the value here is this project's own stable
        /// identifier rather than a Microsoft one. It will not match a GUID obtained from
        /// System.Drawing, because System.Drawing has none to match.
        /// </remarks>
        public static ImageFormat Webp { get; } = new ImageFormat ("Webp", SKEncodedImageFormat.Webp, "1b7cfaf4-713f-473c-bbcd-6137425faeaf");

        /// <summary>Gets the HEIF image format. Decode support depends on the platform's Skia build.</summary>
        /// <remarks>As with <see cref="Webp"/>, the GUID is this project's own; GDI+ defines none.</remarks>
        public static ImageFormat Heif { get; } = new ImageFormat ("Heif", SKEncodedImageFormat.Heif, "9d1b3a2c-6f47-4e64-9c2a-0c7f5b3a8d21");

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
        Format8bppIndexed = 198659,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Indexed.</summary>
        Indexed = 65536,
        /// <summary>Gdi.</summary>
        Gdi = 131072,
        /// <summary>Alpha.</summary>
        Alpha = 262144,
        /// <summary>P alpha.</summary>
        PAlpha = 524288,
        /// <summary>Extended.</summary>
        Extended = 1048576,
        /// <summary>Canonical.</summary>
        Canonical = 2097152,
        /// <summary>Dont care.</summary>
        DontCare = 0,
        /// <summary>Format 1 bpp indexed.</summary>
        Format1bppIndexed = 196865,
        /// <summary>Format 4 bpp indexed.</summary>
        Format4bppIndexed = 197634,
        /// <summary>Format 16 bpp gray scale.</summary>
        Format16bppGrayScale = 1052676,
        /// <summary>Format 16 bpp argb 1555.</summary>
        Format16bppArgb1555 = 397319,
        /// <summary>Format 48 bpp rgb.</summary>
        Format48bppRgb = 1060876,
        /// <summary>Format 64 bpp argb.</summary>
        Format64bppArgb = 3424269,
        /// <summary>Format 64 bpp p argb.</summary>
        Format64bppPArgb = 1851406,
        /// <summary>Max.</summary>
        Max = 15,
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
        public static readonly Encoder Quality = new Encoder ("Quality", "1d5be4b5-fa4a-452d-9cdd-5db35105e7eb");

        /// <summary>Encoder parameter for compression type.</summary>
        public static readonly Encoder Compression = new Encoder ("Compression", "e09d739d-ccd4-44ee-8eba-3fbf8be4fc58");

        /// <summary>Encoder parameter for color depth, in bits per pixel.</summary>
        public static readonly Encoder ColorDepth = new Encoder ("ColorDepth", "66087055-ad66-4c7c-9a18-38a2310b8337");

        /// <summary>Encoder parameter for the scan method (interlaced or progressive).</summary>
        public static readonly Encoder ScanMethod = new Encoder ("ScanMethod", "3a4e2661-3109-4e56-8536-42c156e7dcfa");

        /// <summary>Encoder parameter for the version of the format to write.</summary>
        public static readonly Encoder Version = new Encoder ("Version", "24d18c76-814a-41a4-bf53-1c219cccf797");

        /// <summary>Encoder parameter for the rendering method.</summary>
        public static readonly Encoder RenderMethod = new Encoder ("RenderMethod", "6d42c53a-229a-4825-8bb7-5c99e2b9a8b8");

        /// <summary>Encoder parameter for a geometric transformation applied while encoding.</summary>
        public static readonly Encoder Transformation = new Encoder ("Transformation", "8d0eb2d1-a58e-4ea8-aa14-108074b7b6f9");

        /// <summary>Encoder parameter for the JPEG luminance quantization table.</summary>
        public static readonly Encoder LuminanceTable = new Encoder ("LuminanceTable", "edb33bce-0266-4a77-b904-27216099e717");

        /// <summary>Encoder parameter for the JPEG chrominance quantization table.</summary>
        public static readonly Encoder ChrominanceTable = new Encoder ("ChrominanceTable", "f2e455dc-09b3-4316-8260-676ada32481c");

        /// <summary>Encoder parameter controlling multi-frame save behavior.</summary>
        public static readonly Encoder SaveFlag = new Encoder ("SaveFlag", "292266fc-ac40-47bf-8cfc-a85b89a655de");

        /// <summary>Encoder parameter for the color space to encode in.</summary>
        public static readonly Encoder ColorSpace = new Encoder ("ColorSpace", "ae7a62a0-ee2c-49d8-9d07-1ba8a927596e");

        /// <summary>Encoder parameter selecting CMYK output.</summary>
        public static readonly Encoder SaveAsCmyk = new Encoder ("SaveAsCmyk", "a219bbc9-0a9d-4005-a3ee-3a421b8bb06c");

        /// <summary>Encoder parameter identifying the image items to encode.</summary>
        public static readonly Encoder ImageItems = new Encoder ("ImageItems", "63875e13-1f1d-45ab-9195-a29b6066a650");

        /// <summary>Gets the name of this encoder parameter.</summary>
        public string ParameterName { get; }

        /// <summary>
        /// Gets the GUID identifying this encoder parameter. These are the GDI+ category GUIDs, so a
        /// parameter built here is recognizable to code that compares against System.Drawing's values.
        /// </summary>
        public Guid Guid { get; }

        private Encoder (string name, string guid)
        {
            ParameterName = name;
            Guid = new Guid (guid);
        }
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

        /// <summary>Gets the data type of <see cref="Value"/>.</summary>
        public EncoderParameterValueType Type => ValueType;

        /// <summary>Gets the data type of <see cref="Value"/>, inferred from the value itself.</summary>
        public EncoderParameterValueType ValueType => Value switch {
            byte => EncoderParameterValueType.ValueTypeByte,
            short or ushort => EncoderParameterValueType.ValueTypeShort,
            int or uint or long or ulong => EncoderParameterValueType.ValueTypeLong,
            string => EncoderParameterValueType.ValueTypeAscii,
            byte[] => EncoderParameterValueType.ValueTypeByte,
            _ => EncoderParameterValueType.ValueTypeUndefined,
        };

        /// <summary>Gets the number of values held by this parameter.</summary>
        public int NumberOfValues => Value is Array array ? array.Length : 1;

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

        /// <summary>Gets or sets the descriptive name of this codec.</summary>
        public string CodecName { get; set; } = string.Empty;

        /// <summary>Gets or sets the semicolon-separated file extensions this codec handles.</summary>
        public string FilenameExtension { get; set; } = string.Empty;

        /// <summary>Gets or sets the GUID of the image format this codec handles.</summary>
        public Guid FormatID { get; set; }

        /// <summary>Gets or sets the codec's capability flags.</summary>
        public ImageCodecFlags Flags { get; set; }

        /// <summary>
        /// Gets or sets the name of the DLL implementing this codec. Always empty: these codecs are
        /// SkiaSharp, not separate GDI+ codec DLLs.
        /// </summary>
        public string DllName { get; set; } = string.Empty;

        /// <summary>Gets or sets the codec version.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Gets or sets the byte patterns that identify this format.</summary>
        public byte[][]? SignaturePatterns { get; set; }

        /// <summary>Gets or sets the masks applied to <see cref="SignaturePatterns"/> when matching.</summary>
        public byte[][]? SignatureMasks { get; set; }

        private static ImageCodecInfo Describe (string clsid, string mime, ImageFormat format, string name,
            string extensions, ImageCodecFlags flags, byte[][]? patterns, byte[][]? masks) =>
            new () {
                Clsid = new Guid (clsid),
                MimeType = mime,
                Format = format,
                FormatID = format.Guid,
                FormatDescription = name,
                CodecName = $"Built-in SkiaSharp {name} Codec",
                FilenameExtension = extensions,
                Flags = flags,
                SignaturePatterns = patterns,
                SignatureMasks = masks,
            };

        // Skia both reads and writes these.
        private const ImageCodecFlags ReadWrite =
            ImageCodecFlags.Encoder | ImageCodecFlags.Decoder | ImageCodecFlags.SupportBitmap | ImageCodecFlags.Builtin;

        /// <summary>Returns the available image encoders.</summary>
        public static ImageCodecInfo[] GetImageEncoders () => [
            Describe ("557cf400-1a04-11d3-9a73-0000f81ef32e", "image/bmp", ImageFormat.Bmp, "BMP", "*.BMP;*.DIB;*.RLE",
                ReadWrite, [[0x42, 0x4D]], [[0xFF, 0xFF]]),
            Describe ("557cf401-1a04-11d3-9a73-0000f81ef32e", "image/jpeg", ImageFormat.Jpeg, "JPEG", "*.JPG;*.JPEG;*.JPE;*.JFIF",
                ReadWrite, [[0xFF, 0xD8]], [[0xFF, 0xFF]]),
            Describe ("557cf402-1a04-11d3-9a73-0000f81ef32e", "image/gif", ImageFormat.Gif, "GIF", "*.GIF",
                ReadWrite, [[0x47, 0x49, 0x46]], [[0xFF, 0xFF, 0xFF]]),
            Describe ("557cf406-1a04-11d3-9a73-0000f81ef32e", "image/png", ImageFormat.Png, "PNG", "*.PNG",
                ReadWrite, [[0x89, 0x50, 0x4E, 0x47]], [[0xFF, 0xFF, 0xFF, 0xFF]]),
            Describe ("1b7cfaf4-713f-473c-bbcd-6137425faeaf", "image/webp", ImageFormat.Webp, "WebP", "*.WEBP",
                ReadWrite, [[0x52, 0x49, 0x46, 0x46]], [[0xFF, 0xFF, 0xFF, 0xFF]]),
            // TIFF is written by the multi-page writer in this file rather than by Skia, which decodes
            // but does not encode it.
            Describe ("557cf403-1a04-11d3-9a73-0000f81ef32e", "image/tiff", ImageFormat.Tiff, "TIFF", "*.TIF;*.TIFF",
                ReadWrite, [[0x49, 0x49], [0x4D, 0x4D]], [[0xFF, 0xFF], [0xFF, 0xFF]]),
        ];

        /// <summary>
        /// Returns the available image decoders. A superset of the encoders: Skia decodes several
        /// formats it will not write, so this is deliberately not the same list.
        /// </summary>
        public static ImageCodecInfo[] GetImageDecoders () => [
            .. GetImageEncoders (),
            Describe ("c2b0d0d1-9a3a-4d1b-9b3a-6f0a2f4a1f5e", "image/x-icon", ImageFormat.Icon, "ICO", "*.ICO",
                ImageCodecFlags.Decoder | ImageCodecFlags.SupportBitmap | ImageCodecFlags.Builtin,
                [[0x00, 0x00, 0x01, 0x00]], [[0xFF, 0xFF, 0xFF, 0xFF]]),
        ];
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
        private int _pageIndex;

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
            ObjectDisposedException.ThrowIf (_disposed, this);

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
                // PageNumber: two SHORTs packed into the value field (little-endian, low word first):
                // this page's zero-based index, and 0 for "total page count unknown" -- the count is
                // not knowable while streaming pages out.
                (297, 3, 2, (uint)_pageIndex),           // PageNumber
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
            _pageIndex++;
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
