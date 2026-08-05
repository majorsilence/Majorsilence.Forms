using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing
{
    // The design-time type converters (docs/gdi-gap-plan.md).
    //
    // These are what turns a .resx entry or a designer-serialised property into a live Font, Image,
    // Icon or ImageFormat and back again. They were listed as non-goals on the grounds of being
    // "design-time", but nothing in them is Windows-specific: it is string and byte-array work, and
    // a converter that parses but does not format loses the value on the next save. Every test for
    // these is therefore a round-trip rather than a one-way conversion.

    /// <summary>Converts a <see cref="Font"/> to and from other representations.</summary>
    public class FontConverter : TypeConverter
    {
        /// <summary>Initializes a new instance of the <see cref="FontConverter"/> class.</summary>
        public FontConverter () { }

        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (string) || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        /// <remarks>Parses the designer's own format: "Segoe UI, 9pt, style=Bold, Italic". The family
        /// may itself contain the list separator, so the size is found first and everything before it
        /// taken as the family -- which is how a font called "Arial, Bold" survives the round trip.</remarks>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string text)
                return base.ConvertFrom (context, culture, value);

            text = text.Trim ();

            if (text.Length == 0)
                return null;

            culture ??= CultureInfo.CurrentCulture;
            var separator = culture.TextInfo.ListSeparator;
            var parts = text.Split (separator[0]).Select (p => p.Trim ()).ToArray ();

            var size = 8.25f;
            var unit = GraphicsUnit.Point;
            var style = FontStyle.Regular;

            // The style clause runs to the end of the string -- "style=Bold, Italic" is one clause
            // split across two segments, not a style followed by part of a family name.
            var styleAt = Array.FindIndex (parts, p => p.StartsWith ("style=", StringComparison.OrdinalIgnoreCase));

            if (styleAt >= 0) {
                var names = string.Join (',', parts.Skip (styleAt)).AsSpan (6).ToString ();

                foreach (var name in names.Split ('|', ',')) {
                    if (Enum.TryParse<FontStyle> (name.Trim (), ignoreCase: true, out var parsed))
                        style |= parsed;
                }
            }

            // Everything up to the size is the family: a font really can be called "Arial, Bold",
            // so the first separator is not a reliable boundary.
            var end = styleAt >= 0 ? styleAt : parts.Length;
            var familyEnd = end;

            for (var i = 1; i < end; i++) {
                if (!TryParseSize (parts[i], culture, out var parsedSize, out var parsedUnit))
                    continue;

                size = parsedSize;
                unit = parsedUnit;
                familyEnd = i;
                break;
            }

            return new Font (string.Join (separator + " ", parts.Take (familyEnd)), size, style, unit);
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture,
            object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType != typeof (string) || value is not Font font)
                return base.ConvertTo (context, culture, value, destinationType);

            culture ??= CultureInfo.CurrentCulture;
            var separator = culture.TextInfo.ListSeparator + " ";

            var text = font.Name + separator + font.Size.ToString (culture) + UnitSuffix (font.Unit);

            if (font.Style != FontStyle.Regular)
                text += separator + "style=" + string.Join (separator, StyleNames (font.Style));

            return text;
        }

        /// <inheritdoc/>
        public override bool GetCreateInstanceSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override object? CreateInstance (ITypeDescriptorContext? context, IDictionary propertyValues)
        {
            ArgumentNullException.ThrowIfNull (propertyValues);

            var name = propertyValues["Name"] as string ?? "Segoe UI";
            var size = propertyValues["Size"] is float s ? s : 8.25f;
            var unit = propertyValues["Unit"] is GraphicsUnit u ? u : GraphicsUnit.Point;
            var style = FontStyle.Regular;

            if (propertyValues["Bold"] is true)
                style |= FontStyle.Bold;
            if (propertyValues["Italic"] is true)
                style |= FontStyle.Italic;
            if (propertyValues["Underline"] is true)
                style |= FontStyle.Underline;
            if (propertyValues["Strikeout"] is true)
                style |= FontStyle.Strikeout;

            return new Font (name, size, style, unit);
        }

        /// <inheritdoc/>
        public override bool GetPropertiesSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The properties of Font may be trimmed.")]
        public override PropertyDescriptorCollection GetProperties (ITypeDescriptorContext? context,
            object value, Attribute[]? attributes)
            => TypeDescriptor.GetProperties (typeof (Font), attributes);

        private static bool TryParseSize (string segment, CultureInfo culture, out float size, out GraphicsUnit unit)
        {
            unit = GraphicsUnit.Point;

            foreach (var (suffix, candidate) in UnitSuffixes) {
                if (!segment.EndsWith (suffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (float.TryParse (segment[..^suffix.Length].Trim (), NumberStyles.Float, culture, out size)) {
                    unit = candidate;
                    return true;
                }
            }

            return float.TryParse (segment, NumberStyles.Float, culture, out size);
        }

        // Longest first: "mm" would otherwise never be reached past "m", and "pt" past "p".
        private static readonly (string Suffix, GraphicsUnit Unit)[] UnitSuffixes = [
            ("world", GraphicsUnit.World),
            ("display", GraphicsUnit.Display),
            ("px", GraphicsUnit.Pixel),
            ("pt", GraphicsUnit.Point),
            ("in", GraphicsUnit.Inch),
            ("doc", GraphicsUnit.Document),
            ("mm", GraphicsUnit.Millimeter),
        ];

        private static string UnitSuffix (GraphicsUnit unit) => unit switch {
            GraphicsUnit.World => "world",
            GraphicsUnit.Display => "display",
            GraphicsUnit.Pixel => "px",
            GraphicsUnit.Point => "pt",
            GraphicsUnit.Inch => "in",
            GraphicsUnit.Document => "doc",
            GraphicsUnit.Millimeter => "mm",
            _ => "pt",
        };

        private static string[] StyleNames (FontStyle style)
        {
            var names = new System.Collections.Generic.List<string> ();

            if (style.HasFlag (FontStyle.Bold))
                names.Add (nameof (FontStyle.Bold));
            if (style.HasFlag (FontStyle.Italic))
                names.Add (nameof (FontStyle.Italic));
            if (style.HasFlag (FontStyle.Underline))
                names.Add (nameof (FontStyle.Underline));
            if (style.HasFlag (FontStyle.Strikeout))
                names.Add (nameof (FontStyle.Strikeout));

            return [.. names];
        }

        /// <summary>Converts a font family name to and from other representations.</summary>
        public sealed class FontNameConverter : TypeConverter, IDisposable
        {
            /// <summary>Initializes a new instance of the <see cref="FontNameConverter"/> class.</summary>
            public FontNameConverter () { }

            /// <inheritdoc/>
            public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
                => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

            /// <inheritdoc/>
            public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
                => value is string text ? text.Trim () : base.ConvertFrom (context, culture, value);

            /// <inheritdoc/>
            public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
                => new (FontFamily.Families.Select (f => f.Name).ToArray ());

            /// <inheritdoc/>
            public override bool GetStandardValuesSupported (ITypeDescriptorContext? context) => true;

            /// <inheritdoc/>
            /// <remarks>False: a family the host has but this list does not is still a usable name,
            /// so the designer must accept typed-in values.</remarks>
            public override bool GetStandardValuesExclusive (ITypeDescriptorContext? context) => false;

            /// <summary>Releases the resources held by this converter.</summary>
            /// <remarks>Nothing to release. Upstream caches an enumerated font collection here; this
            /// one reads the installed families on demand.</remarks>
            public void Dispose () { }
        }

        /// <summary>Converts a <see cref="GraphicsUnit"/> to and from other representations.</summary>
        public class FontUnitConverter : EnumConverter
        {
            /// <summary>Initializes a new instance of the <see cref="FontUnitConverter"/> class.</summary>
            public FontUnitConverter () : base (typeof (GraphicsUnit)) { }

            /// <inheritdoc/>
            /// <remarks>World and Display are omitted, as upstream omits them: neither is meaningful
            /// as a font size unit, so offering them in a designer drop-down is a trap.</remarks>
            public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
                => new (new object[] {
                    GraphicsUnit.Point, GraphicsUnit.Pixel, GraphicsUnit.Inch,
                    GraphicsUnit.Document, GraphicsUnit.Millimeter,
                });
        }
    }

    /// <summary>Converts an <see cref="ImageFormat"/> to and from other representations.</summary>
    public class ImageFormatConverter : TypeConverter
    {
        /// <summary>Initializes a new instance of the <see cref="ImageFormatConverter"/> class.</summary>
        public ImageFormatConverter () { }

        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (string) || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string text)
                return base.ConvertFrom (context, culture, value);

            return KnownFormats.FirstOrDefault (f => string.Equals (f.Name, text.Trim (), StringComparison.OrdinalIgnoreCase))
                ?? throw new FormatException ($"'{text}' is not a known image format.");
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture,
            object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            return destinationType == typeof (string) && value is ImageFormat format
                ? format.Name
                : base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
            => new (KnownFormats);

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported (ITypeDescriptorContext? context) => true;

        private static ImageFormat[] KnownFormats => [
            ImageFormat.MemoryBmp, ImageFormat.Bmp, ImageFormat.Emf, ImageFormat.Wmf,
            ImageFormat.Gif, ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.Tiff,
            ImageFormat.Exif, ImageFormat.Icon, ImageFormat.Webp, ImageFormat.Heif,
        ];
    }

    /// <summary>Converts an <see cref="Image"/> to and from other representations.</summary>
    public class ImageConverter : TypeConverter
    {
        /// <summary>Initializes a new instance of the <see cref="ImageConverter"/> class.</summary>
        public ImageConverter () { }

        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (byte[]) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (byte[]) || destinationType == typeof (string)
                || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        /// <remarks>The byte array is the encoded file, which is what a .resx stores -- so an image
        /// read out of a resource comes back as the same picture, not as raw pixels the caller would
        /// have to know the layout of.</remarks>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not byte[] bytes)
                return base.ConvertFrom (context, culture, value);

            using var stream = new MemoryStream (bytes);
            return new Bitmap (stream);
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture,
            object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string))
                return value is Image image ? $"{image.Width}x{image.Height}" : "(none)";

            if (destinationType != typeof (byte[]))
                return base.ConvertTo (context, culture, value, destinationType);

            if (value is not Image source)
                return Array.Empty<byte> ();

            using var stream = new MemoryStream ();
            source.Save (stream, ImageFormat.Png);
            return stream.ToArray ();
        }

        /// <inheritdoc/>
        public override bool GetPropertiesSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("The properties of Image may be trimmed.")]
        public override PropertyDescriptorCollection GetProperties (ITypeDescriptorContext? context,
            object value, Attribute[]? attributes)
            => TypeDescriptor.GetProperties (typeof (Image), attributes);
    }

    /// <summary>Converts an <see cref="Icon"/> to and from other representations.</summary>
    public class IconConverter : ExpandableObjectConverter
    {
        /// <summary>Initializes a new instance of the <see cref="IconConverter"/> class.</summary>
        public IconConverter () { }

        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (byte[]) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (byte[]) || destinationType == typeof (Image)
                || destinationType == typeof (string) || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not byte[] bytes)
                return base.ConvertFrom (context, culture, value);

            using var stream = new MemoryStream (bytes);
            return new Icon (stream);
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture,
            object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string))
                return value is Icon icon ? $"{icon.Width}x{icon.Height}" : "(none)";

            if (destinationType == typeof (Image))
                return (value as Icon)?.ToBitmap ();

            if (destinationType != typeof (byte[]))
                return base.ConvertTo (context, culture, value, destinationType);

            if (value is not Icon source)
                return Array.Empty<byte> ();

            using var stream = new MemoryStream ();
            source.Save (stream);
            return stream.ToArray ();
        }
    }
}
