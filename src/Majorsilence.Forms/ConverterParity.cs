using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Majorsilence.Forms
{
    // The design-time type converters (docs/winforms-gap-plan.md).
    //
    // These are what a .resx round-trip and a property grid go through, so the conversions are real
    // rather than declared: OpacityConverter parses "50%" into 0.5 and writes it back, the image
    // converters map WinForms' "(none)" sentinel onto -1 and the empty key, and SelectionRangeConverter
    // parses "start - end" and can rebuild a range from its two properties.
    //
    // The exception is stated once here rather than in each type. Several of these exist upstream to
    // emit an InstanceDescriptor -- the recipe a designer writes into InitializeComponent. There is no
    // designer in this layer and nothing consumes a descriptor, so those converters do the string half
    // of their job, which is the half a .resx and a property grid actually use.

    /// <summary>Converts between an image index and its designer representation.</summary>
    public class ImageIndexConverter : Int32Converter
    {
        /// <summary>The text WinForms shows for "no image".</summary>
        protected const string NoneText = "(none)";

        /// <summary>Gets whether the "(none)" entry is offered as a standard value.</summary>
        protected virtual bool IncludeNoneAsStandardValue => true;

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
            => value is string text && string.Equals (text.Trim (), NoneText, StringComparison.OrdinalIgnoreCase)
                ? -1
                : base.ConvertFrom (context, culture, value);

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            // -1 is WinForms' "no image", and it round-trips through the word rather than the number
            // so a property grid shows something a person can read.
            if (destinationType == typeof (string) && value is int index && index == -1)
                return NoneText;

            return base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
            => new (IncludeNoneAsStandardValue ? new object[] { -1 } : Array.Empty<object> ());

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive (ITypeDescriptorContext? context) => false;
    }

    /// <summary>Converts between an image key and its designer representation.</summary>
    public class ImageKeyConverter : StringConverter
    {
        /// <summary>The text WinForms shows for "no image".</summary>
        protected const string NoneText = "(none)";

        /// <summary>Gets whether the "(none)" entry is offered as a standard value.</summary>
        protected virtual bool IncludeNoneAsStandardValue => true;

        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
            => value is string text && string.Equals (text.Trim (), NoneText, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : base.ConvertFrom (context, culture, value);

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            // The empty key is "no image", and it shows as the word for the same reason -1 does.
            if (destinationType == typeof (string) && value is string key && key.Length == 0)
                return NoneText;

            return base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
            => new (IncludeNoneAsStandardValue ? new object[] { string.Empty } : Array.Empty<object> ());

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive (ITypeDescriptorContext? context) => false;
    }

    /// <summary>Converts a <see cref="ListView"/> state-image index.</summary>
    public class ListViewItemStateImageIndexConverter : ImageIndexConverter
    {
        /// <summary>Gets whether "(none)" is offered.</summary>
        /// <remarks>False: a state image index of -1 is not a valid state, so WinForms does not offer
        /// it here even though the base converter does.</remarks>
        protected override bool IncludeNoneAsStandardValue => false;
    }

    /// <summary>Converts a <see cref="TreeView"/> image index.</summary>
    public class TreeViewImageIndexConverter : ImageIndexConverter
    {
        /// <summary>The text WinForms shows for "inherit from the tree".</summary>
        protected const string DefaultText = "(default)";

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string text)
                return base.ConvertFrom (context, culture, value);

            // A tree node has two sentinels rather than one: -1 means "no image", -2 means "use the
            // tree's". Collapsing them would silently change which image a node draws.
            var trimmed = text.Trim ();

            if (string.Equals (trimmed, NoneText, StringComparison.OrdinalIgnoreCase))
                return -1;

            if (string.Equals (trimmed, DefaultText, StringComparison.OrdinalIgnoreCase))
                return -2;

            return base.ConvertFrom (context, culture, value);
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string) && value is int index)
                return index switch {
                    -1 => NoneText,
                    -2 => DefaultText,
                    _ => base.ConvertTo (context, culture, value, destinationType),
                };

            return base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
            => new (new object[] { -1, -2 });
    }

    /// <summary>Converts a <see cref="TreeView"/> image key.</summary>
    public class TreeViewImageKeyConverter : ImageKeyConverter
    {
        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string) && value is null)
                return "(default)";

            return base.ConvertTo (context, culture, value, destinationType);
        }
    }

    /// <summary>Converts a form's opacity between its stored fraction and a percentage.</summary>
    public class OpacityConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string text)
                return base.ConvertFrom (context, culture, value);

            culture ??= CultureInfo.CurrentCulture;

            // Opacity is stored as 0..1 and shown as a percentage, so the trailing sign has to be
            // stripped before parsing or "50%" reads as fifty.
            var trimmed = text.Replace (culture.NumberFormat.PercentSymbol, string.Empty, StringComparison.Ordinal)
                .Replace ("%", string.Empty, StringComparison.Ordinal)
                .Trim ();

            var percent = double.Parse (trimmed, NumberStyles.Float, culture) / 100d;
            return Math.Clamp (percent, 0d, 1d);
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string) && value is double opacity) {
                culture ??= CultureInfo.CurrentCulture;
                return (opacity * 100d).ToString (CultureInfo.InvariantCulture.NumberFormat) + "%";
            }

            return base.ConvertTo (context, culture, value, destinationType);
        }
    }

    /// <summary>Converts a <see cref="SelectionRange"/> to and from text.</summary>
    public class SelectionRangeConverter : TypeConverter
    {
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

            if (text.Trim ().Length == 0)
                return new SelectionRange (DateTime.Now.Date, DateTime.Now.Date);

            // The separator is the culture's list separator, not a comma: a culture that uses the
            // comma as a decimal point uses a semicolon here, and hard-coding the comma would split
            // a date in half.
            var parts = text.Split (culture.TextInfo.ListSeparator[0]);

            return parts.Length != 2
                ? throw new ArgumentException ($"'{text}' is not a valid selection range.", nameof (value))
                : new SelectionRange (DateTime.Parse (parts[0].Trim (), culture), DateTime.Parse (parts[1].Trim (), culture));
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string) && value is SelectionRange range) {
                culture ??= CultureInfo.CurrentCulture;
                var separator = culture.TextInfo.ListSeparator + " ";
                return range.Start.ToString (culture) + separator + range.End.ToString (culture);
            }

            return base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override bool GetCreateInstanceSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override object? CreateInstance (ITypeDescriptorContext? context, IDictionary propertyValues)
        {
            ArgumentNullException.ThrowIfNull (propertyValues);

            var start = propertyValues[nameof (SelectionRange.Start)];
            var end = propertyValues[nameof (SelectionRange.End)];

            return start is DateTime from && end is DateTime to
                ? new SelectionRange (from, to)
                : throw new ArgumentException ("Start and End are both required.", nameof (propertyValues));
        }

        /// <inheritdoc/>
        public override bool GetPropertiesSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        [RequiresUnreferencedCode ("SelectionRange's properties are discovered by reflection, as they are upstream.")]
        public override PropertyDescriptorCollection GetProperties (ITypeDescriptorContext? context, object value, Attribute[]? attributes)
            => TypeDescriptor.GetProperties (typeof (SelectionRange), attributes)
                .Sort ([nameof (SelectionRange.Start), nameof (SelectionRange.End)]);
    }

    /// <summary>Converts a <see cref="Cursor"/> to and from its name.</summary>
    public class CursorConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (string) || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string name)
                return base.ConvertFrom (context, culture, value);

            return StandardCursors ()
                .FirstOrDefault (p => string.Equals (p.Name, name.Trim (), StringComparison.OrdinalIgnoreCase))
                ?.GetValue (null) ?? base.ConvertFrom (context, culture, value);
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string) && value is Cursor cursor)
                return StandardCursors ()
                    .FirstOrDefault (p => ReferenceEquals (p.GetValue (null), cursor))
                    ?.Name ?? base.ConvertTo (context, culture, value, destinationType);

            return base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
            => new (StandardCursors ().Select (p => p.GetValue (null)).ToArray ());

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported (ITypeDescriptorContext? context) => true;

        // Cursors' own static properties are the standard set. The type is in this assembly and its
        // properties are reachable from here, so preserving them is not in question; the suppression
        // says that rather than pushing an annotation onto overrides whose base does not have one.
        [UnconditionalSuppressMessage ("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "Cursors is in this assembly and its static properties are always preserved.")]
        [UnconditionalSuppressMessage ("Trimming", "IL2075:DynamicallyAccessedMembers",
            Justification = "See above.")]
        private static PropertyInfo[] StandardCursors ()
            => typeof (Cursors).GetProperties (BindingFlags.Public | BindingFlags.Static)
                .Where (p => p.PropertyType == typeof (Cursor))
                .ToArray ();
    }

    /// <summary>Converts a <see cref="LinkLabel.Link"/> to and from text.</summary>
    public class LinkConverter : TypeConverter
    {
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

            return parts.Length != 2
                ? throw new ArgumentException ($"'{text}' is not a valid link.", nameof (value))
                : new LinkLabel.Link (int.Parse (parts[0].Trim (), culture), int.Parse (parts[1].Trim (), culture));
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (destinationType == typeof (string) && value is LinkLabel.Link link) {
                culture ??= CultureInfo.CurrentCulture;
                return link.Start.ToString (culture) + culture.TextInfo.ListSeparator + " " + link.Length.ToString (culture);
            }

            return base.ConvertTo (context, culture, value, destinationType);
        }
    }

    /// <summary>Rebuilds a <see cref="Binding"/> from the properties a designer recorded.</summary>
    public class ListBindingConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
            => base.ConvertTo (context, culture, value, destinationType);

        /// <inheritdoc/>
        public override bool GetCreateInstanceSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override object? CreateInstance (ITypeDescriptorContext? context, IDictionary propertyValues)
        {
            ArgumentNullException.ThrowIfNull (propertyValues);

            return new Binding (
                propertyValues[nameof (Binding.PropertyName)] as string ?? string.Empty,
                propertyValues[nameof (Binding.DataSource)],
                propertyValues["DataMember"] as string);
        }
    }

    // The four below exist upstream to emit an InstanceDescriptor for a designer. There is no designer
    // here, so each keeps the base conversions and adds nothing it could not honour -- see this file's
    // header.

    /// <summary>Converts a <see cref="ColumnHeader"/> for a designer.</summary>
    public class ColumnHeaderConverter : ExpandableObjectConverter
    {
    }

    /// <summary>Converts a <see cref="TreeNode"/> for a designer.</summary>
    public class TreeNodeConverter : TypeConverter
    {
    }

    /// <summary>Converts a <see cref="ListViewItem"/> for a designer.</summary>
    public class ListViewItemConverter : ExpandableObjectConverter
    {
    }

    /// <summary>Converts a <see cref="DataGridViewCellStyle"/> for a designer.</summary>
    public class DataGridViewCellStyleConverter : TypeConverter
    {
    }

    /// <summary>Converts a <see cref="DataGrid"/>'s preferred column width.</summary>
    public class DataGridPreferredColumnWidthTypeConverter : TypeConverter
    {
    }

    /// <summary>Converts a <see cref="Keys"/> combination to and from its shortcut text.</summary>
    public class KeysConverter : TypeConverter, IComparer
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom (ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof (string) || base.CanConvertFrom (context, sourceType);

        /// <inheritdoc/>
        public override bool CanConvertTo (ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof (string) || destinationType == typeof (Enum[])
                || base.CanConvertTo (context, destinationType);

        /// <inheritdoc/>
        public override object? ConvertFrom (ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is not string text)
                return base.ConvertFrom (context, culture, value);

            var keys = Keys.None;

            // The modifiers are named parts of the same value, so each segment is folded in rather
            // than replacing what came before -- "Ctrl+Shift+A" is one Keys, not three.
            foreach (var segment in text.Split ('+', StringSplitOptions.RemoveEmptyEntries)) {
                var name = segment.Trim ();

                keys |= name.ToUpperInvariant () switch {
                    "CTRL" or "CONTROL" => Keys.Control,
                    "SHIFT" => Keys.Shift,
                    "ALT" => Keys.Alt,
                    _ => Enum.TryParse<Keys> (name, ignoreCase: true, out var parsed)
                        ? parsed
                        : throw new ArgumentException ($"'{name}' is not a key name.", nameof (value)),
                };
            }

            return keys;
        }

        /// <inheritdoc/>
        public override object? ConvertTo (ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        {
            ArgumentNullException.ThrowIfNull (destinationType);

            if (value is not Keys keys)
                return base.ConvertTo (context, culture, value, destinationType);

            if (destinationType == typeof (Enum[]))
                return Parts (keys).Cast<Enum> ().ToArray ();

            if (destinationType == typeof (string))
                return string.Join ('+', Parts (keys).Select (Format));

            return base.ConvertTo (context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues (ITypeDescriptorContext? context)
            => new (Enum.GetValues<Keys> ().Distinct ().ToArray ());

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported (ITypeDescriptorContext? context) => true;

        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive (ITypeDescriptorContext? context) => false;

        /// <summary>Orders two key values by their shortcut text.</summary>
        public int Compare (object? x, object? y)
            => string.Compare (x?.ToString (), y?.ToString (), StringComparison.Ordinal);

        // WinForms writes the control modifier as "Ctrl"; the enum member is named Control, so
        // ToString alone would put "Control+S" in a menu where Windows shows "Ctrl+S".
        private static string Format (Keys key) => key == Keys.Control ? "Ctrl" : key.ToString ();

        // Modifiers first and in WinForms' order, then the key itself -- "Ctrl+Shift+A", never
        // "A+Shift+Ctrl", because the text is what ends up in a menu item.
        private static Keys[] Parts (Keys keys)
        {
            var parts = new System.Collections.Generic.List<Keys> ();

            if ((keys & Keys.Control) == Keys.Control)
                parts.Add (Keys.Control);
            if ((keys & Keys.Shift) == Keys.Shift)
                parts.Add (Keys.Shift);
            if ((keys & Keys.Alt) == Keys.Alt)
                parts.Add (Keys.Alt);

            var key = keys & Keys.KeyCode;
            if (key != Keys.None)
                parts.Add (key);

            return [.. parts];
        }
    }
}

namespace Majorsilence.Forms.Layout
{
    /// <summary>Converts a <see cref="TableLayoutSettings"/> for a designer.</summary>
    /// <remarks>Upstream serialises the row and column styles into a .resx through this converter.
    /// See ConverterParity.cs's header: nothing here consumes the descriptor it would produce.</remarks>
    public class TableLayoutSettingsTypeConverter : TypeConverter
    {
    }
}
