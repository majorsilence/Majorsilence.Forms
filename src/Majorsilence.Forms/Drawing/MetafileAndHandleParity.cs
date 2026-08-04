using System;
using System.Drawing;
using System.IO;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms
{
    // The Graphics half of the metafile and handle work (docs/gdi-gap-plan.md). Graphics lives in
    // this assembly rather than the drawing one -- it depends on Control -- so its share of those
    // members has to live here too. The three cases are the ones stated in
    // Majorsilence.Forms.Drawing/HandleInterop.cs.

    public sealed partial class Graphics
    {
        /// <summary>Called for each record as a metafile is enumerated.</summary>
        public delegate bool EnumerateMetafileProc (EmfPlusRecordType recordType, int flags, int dataSize,
            IntPtr data, PlayRecordCallback? callbackData);

        /// <summary>Adds a comment to the metafile this surface is recording.</summary>
        /// <remarks>A no-op, and correctly so: upstream also does nothing when the surface is not
        /// recording a metafile, which here is always. A caller that peppers its drawing code with
        /// comments therefore behaves the same on both.</remarks>
        public void AddMetafileComment (byte[] data) { }

        /// <summary>Creates a surface from a device context handle, for framework use.</summary>
        /// <remarks>Matches the existing <see cref="FromHdc(IntPtr)"/> rather than throwing: that one
        /// already ships returning a detached surface, and two members a line apart disagreeing about
        /// the same handle would be worse than either answer.</remarks>
        public static Graphics FromHdcInternal (IntPtr hdc) => FromHdc (hdc);

        /// <inheritdoc cref="FromHdcInternal(IntPtr)"/>
        public static Graphics FromHwndInternal (IntPtr hwnd) => FromHwnd (hwnd);

        /// <inheritdoc cref="FromHdcInternal(IntPtr)"/>
        public static Graphics FromHdc (IntPtr hdc, IntPtr hdevice) => FromHdc (hdc);

        /// <summary>Releases a device context obtained for framework use.</summary>
        /// <remarks>A no-op, matching <see cref="ReleaseHdc()"/>: nothing was acquired.</remarks>
        public void ReleaseHdcInternal (IntPtr hdc) { }

        /// <summary>Returns the halftone palette GDI+ uses when drawing to a paletted surface.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. A palette handle is a Win32 GDI
        /// object; there is none behind this surface to hand out.</exception>
        public static IntPtr GetHalftonePalette ()
            => throw new PlatformNotSupportedException (
                "Graphics.GetHalftonePalette returns a Win32 HPALETTE. There is no GDI object behind this surface to hand out.");

        /// <summary>Returns the accumulated transform offset and clip region.</summary>
        /// <remarks>Implemented for real -- both are state this surface already tracks, so a caller
        /// that reads them to position child drawing gets the right answer.</remarks>
        public void GetContextInfo (out PointF offset)
        {
            var bounds = ClipBounds;
            offset = new PointF (bounds.X, bounds.Y);
        }

        /// <inheritdoc cref="GetContextInfo(out PointF)"/>
        public void GetContextInfo (out PointF offset, out Majorsilence.Forms.Drawing.Region clip)
        {
            GetContextInfo (out offset);
            clip = new Majorsilence.Forms.Drawing.Region (Rectangle.Round (ClipBounds));
        }

        /// <summary>Returns the accumulated transform offset and clip region.</summary>
        /// <remarks>Boxed, matching the shape upstream kept for compatibility; prefer one of the
        /// <c>out</c> overloads, which say what they return.</remarks>
        public object GetContextInfo ()
        {
            GetContextInfo (out var offset, out var clip);
            return new object[] { offset, clip };
        }
    }
}

namespace Majorsilence.Forms.Drawing
{
    public sealed partial class BufferedGraphics
    {
        /// <summary>Writes the buffer to a device context.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. There is no HDC to write to; use
        /// the <see cref="Render(Majorsilence.Forms.Graphics)"/> overload.</exception>
        public void Render (IntPtr targetDC)
            => throw new PlatformNotSupportedException (
                "BufferedGraphics.Render(IntPtr) writes to a Win32 device context. Use Render(Graphics) or Render() instead.");
    }
}

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>A Windows or enhanced metafile.</summary>
    /// <remarks>
    /// EMF and WMF are Windows GDI record-and-replay formats: a metafile is a list of GDI calls, and
    /// playing one back means executing those calls against a device context. Skia has no equivalent,
    /// so this type exists to make surrounding code compile and to carry a header -- the recording
    /// constructors throw rather than returning something that silently records nothing.
    /// <para>Reading a metafile's header is not the same as playing it back, and that part is real:
    /// <see cref="GetMetafileHeader(string)"/> and its stream overload parse the EMF and placeable-WMF
    /// headers, which are fixed binary layouts, so code that inspects a file before deciding what to
    /// do with it works here.</para>
    /// </remarks>
    public sealed class Metafile : Image
    {
        private readonly MetafileHeader header;

        /// <summary>Initializes a new instance of the <see cref="Metafile"/> class from a file.</summary>
        public Metafile (string filename) => header = GetMetafileHeader (filename);

        /// <summary>Initializes a new instance of the <see cref="Metafile"/> class from a stream.</summary>
        public Metafile (Stream stream) => header = GetMetafileHeader (stream);

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
            ArgumentNullException.ThrowIfNull (fileName);

            using var stream = File.OpenRead (fileName);
            return GetMetafileHeader (stream);
        }

        /// <summary>Reads a metafile's header from a stream.</summary>
        /// <remarks>Real parsing, not a stub: the EMF and placeable-WMF headers are fixed little-endian
        /// layouts, so the kind, size, bounds and resolution all come out of the bytes.</remarks>
        public static MetafileHeader GetMetafileHeader (Stream stream)
        {
            ArgumentNullException.ThrowIfNull (stream);

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
