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
