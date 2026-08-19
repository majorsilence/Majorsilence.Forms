using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    // Win32 handle interop (docs/gdi-gap-plan.md).
    //
    // These members were absent, which meant migrated code that touched one did not compile at all --
    // and a file that does not compile blocks the other ninety per cent of it that would have worked.
    // They exist now, and each one falls into exactly one of three cases, stated here once:
    //
    //   * A meaningful answer exists without Win32 -- LOGFONT is a data layout, not an API call, and
    //     a region is a set of rectangles this layer already has. Those are implemented for real.
    //   * A handle would have to be *produced*. Those throw PlatformNotSupportedException. Returning
    //     IntPtr.Zero would be worse than absence: the caller hands it to DeleteObject or SelectObject
    //     and corrupts silently, where a throw names the problem at the line that caused it.
    //   * A handle would have to be *read*. Those throw for the same reason -- there is nothing behind
    //     the pointer to read, so any object returned would be a blank picture masquerading as data.
    //
    // Releasing a handle is the exception to the last two: you can only reach a release with a handle
    // you never obtained, so those are no-ops rather than throws.

    public partial class Bitmap
    {
        /// <summary>Creates a bitmap from a Windows icon handle.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Bitmap FromHicon (IntPtr hicon)
            => throw new PlatformNotSupportedException (
                "Bitmap.FromHicon needs a Win32 HICON, which has no meaning outside Windows GDI. Load the icon from a file or stream instead.");

        /// <summary>Creates a bitmap from a Win32 module resource.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Bitmap FromResource (IntPtr hinstance, string bitmapName)
            => throw new PlatformNotSupportedException (
                "Bitmap.FromResource reads a Win32 module resource. Use a .NET resource, or Bitmap(Stream), instead.");

        /// <summary>Creates a GDI bitmap handle for this bitmap.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public IntPtr GetHbitmap ()
            => throw new PlatformNotSupportedException (
                "Bitmap.GetHbitmap would have to return a Win32 HBITMAP the caller then deletes. There is no GDI object behind this bitmap to hand out.");

        /// <inheritdoc cref="GetHbitmap()"/>
        public IntPtr GetHbitmap (Color background) => GetHbitmap ();
    }

    public partial class Image
    {
        /// <summary>Creates an image from a GDI bitmap handle.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Bitmap FromHbitmap (IntPtr hbitmap)
            => throw new PlatformNotSupportedException (
                "Image.FromHbitmap needs a Win32 HBITMAP, which has no meaning outside Windows GDI. Load the image from a file or stream instead.");

        /// <inheritdoc cref="FromHbitmap(IntPtr)"/>
        public static Bitmap FromHbitmap (IntPtr hbitmap, IntPtr hpalette) => FromHbitmap (hbitmap);
    }

    public partial class Icon
    {
        /// <summary>Creates an icon from a Windows icon handle.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Icon FromHandle (IntPtr handle)
            => throw new PlatformNotSupportedException (
                "Icon.FromHandle needs a Win32 HICON, which has no meaning outside Windows GDI. Load the icon from a file or stream instead.");

        /// <summary>Returns the icon the shell associates with a file, or null.</summary>
        /// <remarks>Null. The file-to-icon association lives in the Windows shell, and null is an
        /// outcome upstream already returns for a path it cannot resolve -- so a caller that checks
        /// the result behaves correctly here rather than seeing a wrong icon.</remarks>
        public static Icon? ExtractAssociatedIcon (string filePath)
        {
            Guard.ThrowIfNull (filePath);
            return null;
        }
    }

    public partial class Font
    {
        /// <summary>Creates a font from a device context's currently selected font.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Font FromHdc (IntPtr hdc)
            => throw new PlatformNotSupportedException (
                "Font.FromHdc reads the font selected into a Win32 device context. There is no HDC here to read from.");

        /// <summary>Creates a font from a GDI font handle.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Font FromHfont (IntPtr hfont)
            => throw new PlatformNotSupportedException (
                "Font.FromHfont needs a Win32 HFONT, which has no meaning outside Windows GDI. Construct the Font from a family name and size instead.");

        /// <summary>Creates a GDI font handle for this font.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public IntPtr ToHfont ()
            => throw new PlatformNotSupportedException (
                "Font.ToHfont would have to return a Win32 HFONT the caller then deletes. There is no GDI object behind this font to hand out.");

        /// <summary>Creates a font from a LOGFONT structure.</summary>
        /// <remarks>Implemented for real: a LOGFONT is a data layout, not an API call. The fields are
        /// read by name off whatever object the caller passes, so any LOGFONT declaration works --
        /// there is no single blessed struct type to require.</remarks>
        public static Font FromLogFont (object lf)
        {
            Guard.ThrowIfNull (lf);

            var name = ReadLogFont<string> (lf, "lfFaceName") ?? "Segoe UI";

            // lfHeight is negative for a character height and positive for a cell height; both give
            // the same font, so its magnitude is what matters.
            var height = Math.Abs (Convert.ToSingle (ReadLogFont<object> (lf, "lfHeight") ?? 0f,
                System.Globalization.CultureInfo.InvariantCulture));

            if (height == 0f)
                height = 12f;

            var style = FontStyle.Regular;

            if (Convert.ToInt32 (ReadLogFont<object> (lf, "lfWeight") ?? 0, System.Globalization.CultureInfo.InvariantCulture) >= 700)
                style |= FontStyle.Bold;
            if (Convert.ToInt32 (ReadLogFont<object> (lf, "lfItalic") ?? 0, System.Globalization.CultureInfo.InvariantCulture) != 0)
                style |= FontStyle.Italic;
            if (Convert.ToInt32 (ReadLogFont<object> (lf, "lfUnderline") ?? 0, System.Globalization.CultureInfo.InvariantCulture) != 0)
                style |= FontStyle.Underline;
            if (Convert.ToInt32 (ReadLogFont<object> (lf, "lfStrikeOut") ?? 0, System.Globalization.CultureInfo.InvariantCulture) != 0)
                style |= FontStyle.Strikeout;

            return new Font (name, height, style, GraphicsUnit.Pixel);
        }

        /// <inheritdoc cref="FromLogFont(object)"/>
        public static Font FromLogFont (object lf, IntPtr hdc) => FromLogFont (lf);

        /// <summary>Fills a LOGFONT structure from this font.</summary>
        /// <inheritdoc cref="FromLogFont(object)" path="/remarks"/>
        public void ToLogFont (object logFont)
        {
            Guard.ThrowIfNull (logFont);

            // Negative, matching GDI's convention for "this is the character height, not the cell
            // height" -- the sign is what tells a consumer which of the two it was given.
            WriteLogFont (logFont, "lfHeight", -(int) Math.Round (Unit == GraphicsUnit.Pixel ? Size : Size * 96f / 72f));
            WriteLogFont (logFont, "lfWeight", Style.HasFlag (FontStyle.Bold) ? 700 : 400);
            WriteLogFont (logFont, "lfItalic", (byte) (Style.HasFlag (FontStyle.Italic) ? 1 : 0));
            WriteLogFont (logFont, "lfUnderline", (byte) (Style.HasFlag (FontStyle.Underline) ? 1 : 0));
            WriteLogFont (logFont, "lfStrikeOut", (byte) (Style.HasFlag (FontStyle.Strikeout) ? 1 : 0));
            WriteLogFont (logFont, "lfCharSet", GdiCharSet);
            WriteLogFont (logFont, "lfFaceName", Name);
        }

        /// <inheritdoc cref="ToLogFont(object)"/>
        public void ToLogFont (object logFont, object? graphics) => ToLogFont (logFont);

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "The LOGFONT type comes from the caller, which is what keeps its fields alive.")]
        private static T? ReadLogFont<T> (object lf, string name)
        {
            var type = lf.GetType ();

            object? raw = type.GetField (name, BindingFlags.Public | BindingFlags.Instance)?.GetValue (lf)
                ?? type.GetProperty (name, BindingFlags.Public | BindingFlags.Instance)?.GetValue (lf);

            return raw is T value ? value : default;
        }

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "The LOGFONT type comes from the caller, which is what keeps its fields alive.")]
        private static void WriteLogFont (object logFont, string name, object value)
        {
            var type = logFont.GetType ();

            if (type.GetField (name, BindingFlags.Public | BindingFlags.Instance) is { } field) {
                field.SetValue (logFont, Convert.ChangeType (value, field.FieldType,
                    System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            if (type.GetProperty (name, BindingFlags.Public | BindingFlags.Instance) is { CanWrite: true } property)
                property.SetValue (logFont, Convert.ChangeType (value, property.PropertyType,
                    System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public partial class Region
    {
        /// <summary>Initializes a new instance of the <see cref="Region"/> class from serialized data.</summary>
        public Region (RegionData rgnData) : this ()
        {
            Guard.ThrowIfNull (rgnData);
            MakeEmpty ();

            foreach (var rect in RegionData.Decode (rgnData.Data))
                Union (rect);
        }

        /// <summary>Returns the region's data in a form <see cref="Region(RegionData)"/> reads back.</summary>
        /// <remarks>Implemented for real, but the bytes are this layer's own encoding of the region's
        /// rectangles rather than GDI+'s. They round-trip exactly through <see cref="Region(RegionData)"/>,
        /// which is what cloning a region through its data needs; they are not something to hand to
        /// Win32, which is the one thing the GDI+ layout would have bought.</remarks>
        public RegionData GetRegionData () => new () { Data = RegionData.Encode (GetRegionScans (null)) };

        /// <summary>Creates a region from a GDI region handle.</summary>
        /// <exception cref="PlatformNotSupportedException">Always. See the note in HandleInterop.cs.</exception>
        public static Region FromHrgn (IntPtr hrgn)
            => throw new PlatformNotSupportedException (
                "Region.FromHrgn needs a Win32 HRGN, which has no meaning outside Windows GDI. Construct the Region from a rectangle or path instead.");

        /// <summary>Creates a GDI region handle for this region.</summary>
        /// <remarks>Returns <see cref="IntPtr.Zero"/>: there is no GDI object behind this region to hand
        /// out. It used to throw, but the callers seen in practice hand the value straight to a Win32
        /// invalidation call and then delete it -- chrome bookkeeping with a natural neutral value --
        /// so per the stub policy a null handle beats an exception on a code path that otherwise works.</remarks>
        public IntPtr GetHrgn (object? g) => IntPtr.Zero;

        /// <summary>Releases a region handle obtained from <see cref="GetHrgn"/>.</summary>
        /// <remarks>A no-op: <see cref="GetHrgn"/> never returns, so there is no handle to release.
        /// It is a no-op rather than a throw so that a caller's cleanup path -- often a finally
        /// block -- does not mask the exception that actually stopped them.</remarks>
        public void ReleaseHrgn (IntPtr regionHandle) { }
    }

    /// <summary>A region's interior, in a form that round-trips through <see cref="Region"/>.</summary>
    public sealed class RegionData
    {
        /// <summary>Gets or sets the encoded region.</summary>
        public byte[] Data { get; set; } = [];

        // Four floats per rectangle, little-endian, no header: the region is a rectangle list, and a
        // header would only carry a version this layer has no second version to distinguish.
        internal static byte[] Encode (RectangleF[] rects)
        {
            var bytes = new byte[rects.Length * 16];

            for (var i = 0; i < rects.Length; i++) {
                BitConverterCompat.TryWriteBytes (bytes.AsSpan (i * 16), rects[i].X);
                BitConverterCompat.TryWriteBytes (bytes.AsSpan ((i * 16) + 4), rects[i].Y);
                BitConverterCompat.TryWriteBytes (bytes.AsSpan ((i * 16) + 8), rects[i].Width);
                BitConverterCompat.TryWriteBytes (bytes.AsSpan ((i * 16) + 12), rects[i].Height);
            }

            return bytes;
        }

        internal static RectangleF[] Decode (byte[]? data)
        {
            if (data is null || data.Length < 16)
                return [];

            var rects = new RectangleF[data.Length / 16];

            for (var i = 0; i < rects.Length; i++) {
                rects[i] = new RectangleF (
                    BitConverter.ToSingle (data, i * 16),
                    BitConverter.ToSingle (data, (i * 16) + 4),
                    BitConverter.ToSingle (data, (i * 16) + 8),
                    BitConverter.ToSingle (data, (i * 16) + 12));
            }

            return rects;
        }
    }

    /// <summary>Marks an assembly as carrying its own high-DPI bitmap variants.</summary>
    [AttributeUsage (AttributeTargets.Assembly)]
    public sealed class BitmapSuffixInSameAssemblyAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="BitmapSuffixInSameAssemblyAttribute"/> class.</summary>
        public BitmapSuffixInSameAssemblyAttribute () { }
    }

    /// <summary>Marks an assembly as carrying its high-DPI bitmap variants in a satellite assembly.</summary>
    [AttributeUsage (AttributeTargets.Assembly)]
    public sealed class BitmapSuffixInSatelliteAssemblyAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="BitmapSuffixInSatelliteAssemblyAttribute"/> class.</summary>
        public BitmapSuffixInSatelliteAssemblyAttribute () { }
    }

    /// <summary>Names the bitmap a designer shows for a component in its toolbox.</summary>
    /// <remarks>The image really is loaded, from the declaring type's assembly manifest resources --
    /// which is where the designer puts it. Nothing here has a toolbox to show it in, but a component
    /// carrying the attribute can still be asked for its image, and gets one.</remarks>
    [AttributeUsage (AttributeTargets.Class)]
    public class ToolboxBitmapAttribute : Attribute
    {
        private readonly string? file;
        private readonly Type? declaring;
        private readonly string? resourceName;

        /// <summary>Initializes a new instance naming a bitmap file.</summary>
        public ToolboxBitmapAttribute (string imageFile) => file = imageFile;

        /// <summary>Initializes a new instance naming a type whose resources hold the bitmap.</summary>
        public ToolboxBitmapAttribute (Type t) => declaring = t;

        /// <summary>Initializes a new instance naming a type and the resource within it.</summary>
        public ToolboxBitmapAttribute (Type t, string name)
        {
            declaring = t;
            resourceName = name;
        }

        /// <summary>The attribute applied when a component names no bitmap.</summary>
        public static readonly ToolboxBitmapAttribute Default = new (string.Empty);

        /// <summary>Returns the small image for a component instance.</summary>
        public Image? GetImage (object? component) => GetImage (component, large: false);

        /// <inheritdoc cref="GetImage(object)"/>
        public Image? GetImage (object? component, bool large)
            => component is null ? null : GetImage (component.GetType (), large);

        /// <summary>Returns the small image for a type.</summary>
        public Image? GetImage (Type? type) => GetImage (type, large: false);

        /// <inheritdoc cref="GetImage(Type)"/>
        public Image? GetImage (Type? type, bool large) => GetImage (type, resourceName, large);

        /// <inheritdoc cref="GetImage(Type)"/>
        public Image? GetImage (Type? type, string? imgName, bool large)
        {
            if (!string.IsNullOrEmpty (file) && File.Exists (file))
                return Resize (new Bitmap (file!), large);

            var owner = declaring ?? type;

            return owner is null ? null : GetImageFromResource (owner, imgName ?? owner.Name, large);
        }

        /// <summary>Loads a named bitmap out of a type's assembly resources.</summary>
        public static Image? GetImageFromResource (Type t, string? imageName, bool large)
        {
            Guard.ThrowIfNull (t);

            var assembly = t.Assembly;
            var name = imageName ?? t.Name;

            // The designer writes the resource as "Namespace.Name.bmp"; accept the bare name too,
            // because a hand-written attribute usually gives just that.
            var match = assembly.GetManifestResourceNames ()
                .FirstOrDefault (r => r.EndsWith (name, StringComparison.OrdinalIgnoreCase)
                    || r.EndsWith (name + ".bmp", StringComparison.OrdinalIgnoreCase)
                    || r.EndsWith (name + ".png", StringComparison.OrdinalIgnoreCase));

            if (match is null)
                return null;

            using var stream = assembly.GetManifestResourceStream (match);

            return stream is null ? null : Resize (new Bitmap (stream), large);
        }

        // The designer's two sizes; upstream doubles the small one rather than carrying two images.
        private static Bitmap Resize (Bitmap source, bool large)
            => large ? new Bitmap (source, new Size (32, 32)) : source;
    }
}
