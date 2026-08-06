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
            ArgumentNullException.ThrowIfNull (stream);

            using var buffer = new MemoryStream ();
            stream.CopyTo (buffer);
            return buffer.ToArray ();
        }
    }

    /// <summary>Stand-in for <c>System.Drawing.Bitmap</c> (see file remarks).</summary>
    [TypeConverter (typeof (ImageConverter))]
    public sealed class Bitmap : Image
    {
        /// <summary>Creates a stand-in holding every byte of <paramref name="stream"/>.</summary>
        public Bitmap (Stream stream) : base (stream) { }
    }

    /// <summary>Stand-in for <c>System.Drawing.Icon</c> (see file remarks).</summary>
    [TypeConverter (typeof (ImageConverter))]
    public sealed class Icon : Image
    {
        /// <summary>Creates a stand-in holding every byte of <paramref name="stream"/>.</summary>
        public Icon (Stream stream) : base (stream) { }
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
}
