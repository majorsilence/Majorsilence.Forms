using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace Majorsilence.Drawing
{
    /// <summary>
    /// Converts <see cref="Color"/> values to and from strings. Cross-platform replacement for
    /// System.Drawing.ColorConverter. Accepts a known color name, a <c>#RRGGBB</c>/<c>#AARRGGBB</c>
    /// hex string, or comma-separated <c>R, G, B</c> / <c>A, R, G, B</c> components.
    /// </summary>
    public class ColorConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType) =>
            destinationType == typeof (string) || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string text)
                return base.ConvertFrom (context, culture, value);

            var s = text.Trim ();
            if (s.Length == 0)
                return Color.Empty;

            if (s.StartsWith ('#'))
            {
                var hex = s[1..];
                if (uint.TryParse (hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                {
                    // #RRGGBB is fully opaque; #AARRGGBB carries its own alpha.
                    if (hex.Length <= 6)
                        argb |= 0xFF000000;
                    return Color.FromArgb ((int)argb);
                }
            }

            if (s.Contains (','))
            {
                var parts = s.Split (',');
                var nums = new int[parts.Length];
                var ok = true;
                for (var i = 0; i < parts.Length && ok; i++)
                    ok = int.TryParse (parts[i].Trim (), out nums[i]);
                if (ok)
                {
                    return nums.Length switch {
                        3 => Color.FromArgb (nums[0], nums[1], nums[2]),
                        4 => Color.FromArgb (nums[0], nums[1], nums[2], nums[3]),
                        _ => Color.Empty,
                    };
                }
            }

            var named = Color.FromName (s);
            return named.IsKnownColor || named.A != 0 ? named : Color.Empty;
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof (string) && value is Color color)
            {
                if (color.IsEmpty)
                    return string.Empty;
                if (color.IsNamedColor)
                    return color.Name;
                return color.A < 255
                    ? $"{color.A}, {color.R}, {color.G}, {color.B}"
                    : $"{color.R}, {color.G}, {color.B}";
            }
            return base.ConvertTo (context, culture, value, destinationType);
        }
    }
}
