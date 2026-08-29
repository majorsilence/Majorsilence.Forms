// These live under the "System.Drawing" namespace deliberately: ComponentResourceManager's resolver
// hands this assembly back whenever a compiled .resx resource asks (by assembly-qualified name) for
// "System.Drawing.Bitmap"/"Icon", so Type.GetType's by-name lookup within the returned assembly
// needs to find a type at exactly that namespace+name.
//
// A compiled .resx stores an image one of two ways, and both end up here:
//   * as a stream resource, which the resource reader materializes by calling the declared type's
//     (Stream) constructor -- the shape the SDK writes for an image dragged into a Resources.resx;
//   * as a type-converter resource over a byte[], which it materializes through TypeDescriptor.
// Either way the payload is the original, undecoded file (a PNG, a .ico, ...). These types keep
// exactly those bytes and decode nothing: ComponentResourceManager reads MajorsilenceRawBytes back
// off them and hands it to SkiaSharp, which works on every platform, unlike the GDI+-backed types
// they are standing in for.
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace System.Drawing
{
    /// <summary>Stand-in for <c>System.Drawing.Image</c> (see file remarks).</summary>
    [TypeConverter (typeof (ImageConverter))]
    public class Image
    {
        /// <summary>Creates an empty stand-in.</summary>
        public Image () { }

        /// <summary>Creates a stand-in holding every byte of <paramref name="stream"/>.</summary>
        public Image (Stream stream) => MajorsilenceRawBytes = ReadAll (stream);

        /// <summary>The undecoded bytes of the original image file.</summary>
        public byte[]? MajorsilenceRawBytes { get; set; }

        internal static byte[] ReadAll (Stream stream)
        {
#if NETSTANDARD2_0
            if (stream is null)
                throw new ArgumentNullException (nameof (stream));
#else
            ArgumentNullException.ThrowIfNull (stream);
#endif

            using var buffer = new MemoryStream ();
            stream.CopyTo (buffer);
            return buffer.ToArray ();
        }
    }

    /// <summary>Stand-in for <c>System.Drawing.Bitmap</c> (see file remarks).</summary>
    /// <remarks>
    /// Carries its own converter rather than sharing <see cref="ImageConverter"/>: a converter is
    /// selected by the resource's *declared* type, and one that always returned the base
    /// <see cref="Image"/> could not satisfy an entry declared as a Bitmap — the read failed and the
    /// resource silently came back null.
    /// </remarks>
    [TypeConverter (typeof (BitmapConverter))]
    public sealed class Bitmap : Image
    {
        /// <summary>Creates an empty stand-in.</summary>
        public Bitmap () { }

        /// <summary>Creates a stand-in holding every byte of <paramref name="stream"/>.</summary>
        public Bitmap (Stream stream) : base (stream) { }
    }

    /// <summary>Stand-in for <c>System.Drawing.Icon</c> (see file remarks).</summary>
    /// <remarks>See <see cref="Bitmap"/> for why this has its own converter.</remarks>
    [TypeConverter (typeof (IconConverter))]
    public sealed class Icon : Image
    {
        /// <summary>Creates an empty stand-in.</summary>
        public Icon () { }

        /// <summary>Creates a stand-in holding every byte of <paramref name="stream"/>.</summary>
        public Icon (Stream stream) : base (stream) { }
    }

    /// <summary>Converts a byte[] image payload into a <see cref="Bitmap"/> stand-in.</summary>
    public sealed class BitmapConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (byte[]) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
            => value is byte[] bytes
                ? new Bitmap { MajorsilenceRawBytes = bytes }
                : base.ConvertFrom (context, culture, value);
    }

    /// <summary>Converts a byte[] icon payload into an <see cref="Icon"/> stand-in.</summary>
    public sealed class IconConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (byte[]) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
            => value is byte[] bytes
                ? new Icon { MajorsilenceRawBytes = bytes }
                : base.ConvertFrom (context, culture, value);
    }

    /// <summary>
    /// Converts the byte[] payload of a type-converter-backed image resource into the stand-in types
    /// above, for the resources that are stored that way rather than as a stream.
    /// </summary>
    public sealed class ImageConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (byte[]) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
            => value is byte[] bytes
                ? new Image { MajorsilenceRawBytes = bytes }
                : base.ConvertFrom (context, culture, value);
    }

    /// <summary>
    /// Stand-in for <c>System.Drawing.Font</c> (see file remarks). Unlike the image types, a font is
    /// written into a compiled <c>.resources</c> by its type converter, as the string GDI+ round-trips —
    /// "Arial, 12pt, style=Bold". This keeps that string verbatim; ComponentResourceManager reads it back
    /// off <see cref="MajorsilenceFontSpec"/> and parses it with the same parser it uses for a font written
    /// directly into <c>.resx</c> XML.
    /// </summary>
    [TypeConverter (typeof (FontConverter))]
    public sealed class Font
    {
        /// <summary>Creates an empty stand-in.</summary>
        public Font () { }

        /// <summary>Creates a stand-in carrying the converter string <paramref name="spec"/>.</summary>
        public Font (string spec) => MajorsilenceFontSpec = spec;

        /// <summary>The font's type-converter string, exactly as the resource stored it.</summary>
        public string? MajorsilenceFontSpec { get; set; }

        /// <inheritdoc/>
        public override string ToString () => MajorsilenceFontSpec ?? string.Empty;
    }

    /// <summary>
    /// Converts the string payload of a font resource into <see cref="Font"/>. Also accepts the byte[]
    /// shape, for the same reason <see cref="ImageConverter"/> does.
    /// </summary>
    public sealed class FontConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || sourceType == typeof (byte[]) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
            => value switch {
                string spec => new Font (spec),
                byte[] bytes => new Font (System.Text.Encoding.UTF8.GetString (bytes)),
                _ => base.ConvertFrom (context, culture, value),
            };

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (string) || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
            => destinationType == typeof (string) && value is Font font
                ? font.MajorsilenceFontSpec ?? string.Empty
                : base.ConvertTo (context, culture, value, destinationType);
    }
}
