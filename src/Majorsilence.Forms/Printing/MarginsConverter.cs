using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace Majorsilence.Forms.Printing
{
    /// <summary>Converts a <see cref="Margins"/> to and from other representations.</summary>
    public class MarginsConverter : ExpandableObjectConverter
    {
        /// <summary>Initializes a new instance of the <see cref="MarginsConverter"/> class.</summary>
        public MarginsConverter () { }

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

            culture ??= CultureInfo.CurrentCulture;
            var parts = text.Split (culture.TextInfo.ListSeparator[0]);

            if (parts.Length != 4)
                throw new ArgumentException ($"Cannot convert \"{text}\" to Margins.", nameof (value));

            // Left, Right, Top, Bottom -- the order Margins' own constructor takes, not the
            // clockwise order a CSS-shaped guess would use.
            return new Margins (
                int.Parse (parts[0].Trim (), culture),
                int.Parse (parts[1].Trim (), culture),
                int.Parse (parts[2].Trim (), culture),
                int.Parse (parts[3].Trim (), culture));
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture,
            object? value, Type destinationType)
        {
            Guard.ThrowIfNull (destinationType);

            if (destinationType != typeof (string) || value is not Margins margins)
                return base.ConvertTo (context, culture, value, destinationType);

            culture ??= CultureInfo.CurrentCulture;
            var separator = culture.TextInfo.ListSeparator + " ";

            return string.Join (separator, margins.Left, margins.Right, margins.Top, margins.Bottom);
        }

        /// <inheritdoc/>
        public override bool GetCreateInstanceSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override object? CreateInstance (ITypeDescriptorContext? context, IDictionary propertyValues)
        {
            Guard.ThrowIfNull (propertyValues);

            return new Margins (
                propertyValues["Left"] is int left ? left : 100,
                propertyValues["Right"] is int right ? right : 100,
                propertyValues["Top"] is int top ? top : 100,
                propertyValues["Bottom"] is int bottom ? bottom : 100);
        }
    }
}
