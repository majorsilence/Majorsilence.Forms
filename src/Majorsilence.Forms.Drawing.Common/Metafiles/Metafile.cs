using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>A Windows or enhanced metafile.</summary>
    /// <remarks>
    /// <para><b>Reading and rendering work.</b> EMF and WMF are published record formats (MS-EMF,
    /// MS-WMF), so this type parses them and replays the records onto a Skia canvas itself rather
    /// than asking GDI to do it. A metafile loaded from a file, a stream or the clipboard renders
    /// anywhere an <see cref="Image"/> does -- a PictureBox, <c>DrawImage</c>, a printed page --
    /// because it rasterises into the same backing bitmap every other image uses.</para>
    /// <para>Because a metafile is vector data, it re-rasterises when asked for a size it has not
    /// drawn at yet, so scaling one up stays sharp instead of enlarging pixels.</para>
    /// <para><b>Recording does not.</b> Creating a metafile by drawing into it needs the drawing API
    /// to emit records instead of Skia calls; those constructors throw. EMF+ records, which travel
    /// inside EMF comment records, are skipped -- rendering the EMF half is what a downlevel GDI
    /// renderer does with a dual metafile, so the picture is right rather than absent.</para>
    /// </remarks>
    public sealed class Metafile : Image
    {
        private readonly MetafileHeader header;
        private readonly List<Metafiles.MetafileRecord> records = [];
        private readonly bool isWmf;
        private SkiaSharp.SKSizeI rasterisedAt;

        /// <summary>Initializes a new instance of the <see cref="Metafile"/> class from a file.</summary>
        public Metafile (string filename)
        {
            Guard.ThrowIfNull (filename);

            var bytes = File.ReadAllBytes (filename);
            header = GetMetafileHeader (new MemoryStream (bytes));
            records = Parse (bytes, header, out isWmf);
            Rasterise (DefaultSize);
        }

        /// <summary>Initializes a new instance of the <see cref="Metafile"/> class from a stream.</summary>
        public Metafile (Stream stream)
        {
            Guard.ThrowIfNull (stream);

            using var buffer = new MemoryStream ();
            stream.CopyTo (buffer);
            var bytes = buffer.ToArray ();

            header = GetMetafileHeader (new MemoryStream (bytes));
            records = Parse (bytes, header, out isWmf);
            Rasterise (DefaultSize);
        }

        /// <summary>Gets how many records were recognised but not drawn.</summary>
        /// <remarks>Zero for a metafile this layer fully understands. A non-zero count is how a file
        /// using records outside the supported set makes itself visible, rather than quietly
        /// rendering a partial picture.</remarks>
        public int UnsupportedRecordCount { get; private set; }

        /// <summary>Gets the number of records read from the metafile.</summary>
        public int RecordCount => records.Count;

        // The size to rasterise at before anyone asks for a specific one. The recorded bounds are in
        // the metafile's own units, which for a placeable WMF can be 1440 to the inch -- so a bare
        // bounds would allocate a bitmap thousands of pixels across for a small picture.
        private System.Drawing.Size DefaultSize
        {
            get {
                var bounds = header.Bounds;

                if (bounds.Width <= 0 || bounds.Height <= 0)
                    return new System.Drawing.Size (1, 1);

                var dpi = header.DpiX > 0 ? header.DpiX : 96f;
                var scale = isWmf && dpi > 96f ? 96f / dpi : 1f;

                return new System.Drawing.Size (
                    MathCompat.Clamp ((int) Math.Round (bounds.Width * scale), 1, 8192),
                    MathCompat.Clamp ((int) Math.Round (bounds.Height * scale), 1, 8192));
            }
        }

        /// <summary>Renders the metafile at the given pixel size, if it is not already.</summary>
        /// <remarks>Internal because callers ask for a size by drawing at one; <c>GetSKBitmap</c> is
        /// what every draw path already goes through, so this hooks in there rather than adding a
        /// second way to draw an image.</remarks>
        internal override void PrepareForDraw (int width, int height) => EnsureRasterised (width, height);

        /// <inheritdoc cref="PrepareForDraw"/>
        internal void EnsureRasterised (int width, int height)
        {
            if (width <= 0 || height <= 0 || records.Count == 0)
                return;

            var wanted = new SkiaSharp.SKSizeI (MathCompat.Clamp (width, 1, 8192), MathCompat.Clamp (height, 1, 8192));

            if (wanted == rasterisedAt)
                return;

            Rasterise (new System.Drawing.Size (wanted.Width, wanted.Height));
        }

        private void Rasterise (System.Drawing.Size size)
        {
            var bitmap = new SkiaSharp.SKBitmap (size.Width, size.Height,
                SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

            using (var canvas = new SkiaSharp.SKCanvas (bitmap)) {
                canvas.Clear (SkiaSharp.SKColors.Transparent);

                // Map the metafile's recorded bounds onto the bitmap, so the picture fills whatever
                // size was asked for regardless of the units it was authored in.
                var bounds = header.Bounds;
                var transform = bounds.Width > 0 && bounds.Height > 0
                    ? SkiaSharp.SKMatrix.CreateScale ((float) size.Width / bounds.Width, (float) size.Height / bounds.Height)
                        .PreConcat (SkiaSharp.SKMatrix.CreateTranslation (-bounds.X, -bounds.Y))
                    : SkiaSharp.SKMatrix.Identity;

                var extent = new SkiaSharp.SKSize (
                    bounds.Width > 0 ? bounds.Width : size.Width,
                    bounds.Height > 0 ? bounds.Height : size.Height);

                if (isWmf) {
                    var player = new Metafiles.WmfPlayer (canvas, transform, extent);
                    player.Play (records);
                    UnsupportedRecordCount = player.SkippedRecords;
                } else {
                    var player = new Metafiles.EmfPlayer (canvas, transform, extent);
                    player.Play (records);
                    UnsupportedRecordCount = player.SkippedRecords;
                }
            }

            backing?.Dispose ();
            backing = bitmap;
            rasterisedAt = new SkiaSharp.SKSizeI (size.Width, size.Height);
            RawFormat = isWmf ? ImageFormat.Wmf : ImageFormat.Emf;
        }

        private static List<Metafiles.MetafileRecord> Parse (byte[] bytes, MetafileHeader header, out bool isWmf)
        {
            isWmf = header.IsWmf ();

            if (isWmf)
                return Metafiles.WmfReader.Read (bytes, out _, out _);

            return header.IsEmfOrEmfPlus () ? Metafiles.EmfReader.Read (bytes) : [];
        }

        /// <summary>Initializes a new instance of the <see cref="Metafile"/> class from a metafile handle.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the remarks on <see cref="Metafile"/>.</exception>
        public Metafile (IntPtr henhmetafile, bool deleteEmf) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,bool)"/>
        public Metafile (IntPtr hmetafile, WmfPlaceableFileHeader wmfHeader) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,bool)"/>
        public Metafile (IntPtr hmetafile, WmfPlaceableFileHeader wmfHeader, bool deleteWmf) => throw NoRecording ();

        /// <summary>Initializes a new instance that records drawing against a device context.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the remarks on <see cref="Metafile"/>.</exception>
        public Metafile (IntPtr referenceHdc, EmfType emfType) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, Rectangle frameRect) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, RectangleF frameRect) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit, EmfType type)
            => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit, EmfType type)
            => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit, EmfType type,
            string? description) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, EmfType type) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, Rectangle frameRect) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, RectangleF frameRect) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit)
            => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit)
            => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit,
            EmfType type) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit,
            EmfType type) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit,
            string? description) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit,
            string? desc) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (string fileName, IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit,
            EmfType type, string? description) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, EmfType type) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, Rectangle frameRect) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, RectangleF frameRect) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit)
            => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit)
            => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, Rectangle frameRect, MetafileFrameUnit frameUnit,
            EmfType type) => throw NoRecording ();

        /// <inheritdoc cref="Metafile(IntPtr,EmfType)"/>
        public Metafile (Stream stream, IntPtr referenceHdc, RectangleF frameRect, MetafileFrameUnit frameUnit,
            EmfType type) => throw NoRecording ();

        /// <summary>Returns this metafile's header.</summary>
        public MetafileHeader GetMetafileHeader () => header;

        /// <summary>Reads a metafile's header from a file.</summary>
        public static MetafileHeader GetMetafileHeader (string fileName)
        {
            Guard.ThrowIfNull (fileName);

            using var stream = File.OpenRead (fileName);
            return GetMetafileHeader (stream);
        }

        /// <summary>Reads a metafile's header from a stream.</summary>
        /// <remarks>Real parsing, not a stub: the EMF and placeable-WMF headers are fixed little-endian
        /// layouts, so the kind, size, bounds and resolution all come out of the bytes.</remarks>
        public static MetafileHeader GetMetafileHeader (Stream stream)
        {
            Guard.ThrowIfNull (stream);

            var bytes = new byte[88];
            var read = stream.Read (bytes, 0, bytes.Length);
            var result = new MetafileHeader ();

            if (read < 4)
                return result;

            var signature = BitConverter.ToUInt32 (bytes, 0);

            // 0x9AC6CDD7 is the placeable-WMF magic; EMF starts with record type 1 (EMR_HEADER) and
            // carries "  EMF" at offset 40.
            if (signature == 0x9AC6CDD7 && read >= 22) {
                result.Type = MetafileType.WmfPlaceable;
                result.Version = 0x300;
                result.WmfHeader = new MetaHeader {
                    Type = BitConverter.ToInt16 (bytes, 22),
                };

                var inch = BitConverter.ToInt16 (bytes, 18);
                result.DpiX = result.DpiY = inch == 0 ? 96f : inch;
                result.LogicalDpiX = result.LogicalDpiY = (int) result.DpiX;
                result.Bounds = new Rectangle (
                    BitConverter.ToInt16 (bytes, 6),
                    BitConverter.ToInt16 (bytes, 8),
                    BitConverter.ToInt16 (bytes, 10) - BitConverter.ToInt16 (bytes, 6),
                    BitConverter.ToInt16 (bytes, 12) - BitConverter.ToInt16 (bytes, 8));

                return result;
            }

            if (signature == 1 && read >= 88 && BitConverter.ToUInt32 (bytes, 40) == 0x464D4520) {
                result.Type = MetafileType.Emf;
                result.MetafileSize = BitConverter.ToInt32 (bytes, 48);
                result.Version = BitConverter.ToInt32 (bytes, 44);
                result.Bounds = new Rectangle (
                    BitConverter.ToInt32 (bytes, 8),
                    BitConverter.ToInt32 (bytes, 12),
                    BitConverter.ToInt32 (bytes, 16) - BitConverter.ToInt32 (bytes, 8),
                    BitConverter.ToInt32 (bytes, 20) - BitConverter.ToInt32 (bytes, 12));

                // Device pixels over device millimetres, times 25.4, is dots per inch.
                var pixels = BitConverter.ToInt32 (bytes, 72);
                var millimetres = BitConverter.ToInt32 (bytes, 80);
                result.DpiX = result.DpiY = millimetres == 0 ? 96f : pixels * 25.4f / millimetres;
                result.LogicalDpiX = result.LogicalDpiY = (int) result.DpiX;

                return result;
            }

            result.Type = MetafileType.Invalid;
            return result;
        }

        /// <inheritdoc cref="GetMetafileHeader(string)"/>
        /// <exception cref="PlatformNotSupportedException">Always. See the remarks on <see cref="Metafile"/>.</exception>
        public static MetafileHeader GetMetafileHeader (IntPtr henhmetafile) => throw NoRecording ();

        /// <inheritdoc cref="GetMetafileHeader(IntPtr)"/>
        public static MetafileHeader GetMetafileHeader (IntPtr hmetafile, WmfPlaceableFileHeader wmfHeader)
            => throw NoRecording ();

        /// <summary>Returns a handle to this metafile.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the remarks on <see cref="Metafile"/>.</exception>
        public IntPtr GetHenhmetafile () => throw NoRecording ();

        /// <summary>Plays a single metafile record.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the remarks on <see cref="Metafile"/>.</exception>
        public void PlayRecord (EmfPlusRecordType recordType, int flags, int dataSize, byte[] data)
            => throw NoRecording ();

        private static PlatformNotSupportedException NoRecording () => new (
            "Recording and playing back EMF/WMF metafiles is a Windows GDI facility with no Skia equivalent. "
            + "Reading a metafile's header (Metafile.GetMetafileHeader) does work.");
    }
}
