using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // XML-defined custom themes. A theme is an XML document whose root <Theme> element carries a
    // name (and an optional base) and whose child elements are named after the public Theme
    // properties below. Registered themes are stored as parsed documents and applied on demand,
    // layered on top of an optional base theme (a BuiltInTheme or another registered theme).
    //
    // Example:
    //   <Theme name="Ocean" base="Dark">
    //     <BackgroundColor>#FF0A1929</BackgroundColor>
    //     <AccentColor>30,144,255</AccentColor>
    //     <FontSize>15</FontSize>
    //     <UIFont family="Segoe UI" />
    //   </Theme>
    public static partial class Theme
    {
        // Registered themes keyed by name, stored as their parsed root <Theme> element so they can
        // be re-applied any number of times (and re-used as a base by other themes).
        private static readonly Dictionary<string, XElement> registered_themes = new (StringComparer.OrdinalIgnoreCase);

        // The set of color-valued properties that may appear as XML elements. Matches the SKColor
        // properties declared in Theme.cs; the keys double as the dictionary keys used internally.
        private static readonly HashSet<string> color_properties = new (StringComparer.Ordinal) {
            nameof (AccentColor), nameof (AccentColor2), nameof (BackgroundColor),
            nameof (BorderLowColor), nameof (BorderMidColor), nameof (BorderHighColor),
            nameof (ControlLowColor), nameof (ControlMidColor), nameof (ControlMidHighColor),
            nameof (ControlHighColor), nameof (ControlVeryHighColor),
            nameof (ControlHighlightLowColor), nameof (ControlHighlightMidColor), nameof (ControlHighlightHighColor),
            nameof (ForegroundColor), nameof (ForegroundColorOnAccent), nameof (ForegroundDisabledColor),
            nameof (TextSelectionBackgroundColor), nameof (WarningHighlightColor)
        };

        private static readonly HashSet<string> int_properties = new (StringComparer.Ordinal) {
            nameof (FontSize), nameof (ItemFontSize)
        };

        private static readonly HashSet<string> font_properties = new (StringComparer.Ordinal) {
            nameof (UIFont), nameof (UIFontBold)
        };

        /// <summary>
        /// The names of all themes registered with <see cref="RegisterTheme"/>.
        /// </summary>
        public static IReadOnlyCollection<string> RegisteredThemes {
            get {
                lock (_lock)
                    return new List<string> (registered_themes.Keys);
            }
        }

        /// <summary>
        /// Returns whether a theme with the given name has been registered.
        /// </summary>
        public static bool IsThemeRegistered (string name)
        {
            if (string.IsNullOrEmpty (name))
                return false;

            lock (_lock)
                return registered_themes.ContainsKey (name);
        }

        /// <summary>
        /// Registers a custom theme defined as XML so it can later be applied by name with
        /// <see cref="ApplyTheme"/>. The root element must carry a <c>name</c> attribute; a theme
        /// registered under an existing name replaces it. Returns the theme's name.
        /// </summary>
        /// <param name="xml">The theme definition as an XML string.</param>
        public static string RegisterTheme (string xml)
        {
            if (string.IsNullOrWhiteSpace (xml))
                throw new ArgumentException ("Theme XML cannot be null or empty.", nameof (xml));

            return RegisterThemeElement (ParseDocument (XDocument.Parse (xml)));
        }

        /// <summary>
        /// Registers a custom theme by reading its XML definition from a file. Returns the theme's name.
        /// </summary>
        public static string RegisterThemeFromFile (string path)
        {
            if (string.IsNullOrWhiteSpace (path))
                throw new ArgumentException ("Path cannot be null or empty.", nameof (path));

            return RegisterThemeElement (ParseDocument (XDocument.Load (path)));
        }

        /// <summary>
        /// Registers a custom theme by reading its XML definition from a stream. Returns the theme's name.
        /// </summary>
        public static string RegisterThemeFromStream (Stream stream)
        {
            ArgumentNullException.ThrowIfNull (stream);

            return RegisterThemeElement (ParseDocument (XDocument.Load (stream)));
        }

        /// <summary>
        /// Removes a previously registered theme. Returns whether a theme was removed.
        /// </summary>
        public static bool UnregisterTheme (string name)
        {
            if (string.IsNullOrEmpty (name))
                return false;

            lock (_lock)
                return registered_themes.Remove (name);
        }

        /// <summary>
        /// Applies a theme previously registered with <see cref="RegisterTheme"/>, raising
        /// <see cref="ThemeChanged"/> once.
        /// </summary>
        public static void ApplyTheme (string name)
        {
            if (string.IsNullOrEmpty (name))
                throw new ArgumentException ("Theme name cannot be null or empty.", nameof (name));

            XElement element;

            lock (_lock) {
                if (!registered_themes.TryGetValue (name, out element!))
                    throw new ArgumentException ($"No theme is registered with the name '{name}'.", nameof (name));
            }

            BeginUpdate ();
            try {
                ApplyThemeElement (element, new HashSet<string> (StringComparer.OrdinalIgnoreCase));
                RaiseThemeChanged ();
            } finally {
                EndUpdate ();
            }
        }

        /// <summary>
        /// Parses an XML theme definition and applies it immediately without registering it,
        /// raising <see cref="ThemeChanged"/> once. The <c>name</c> attribute is optional here.
        /// </summary>
        public static void LoadFromXml (string xml)
        {
            if (string.IsNullOrWhiteSpace (xml))
                throw new ArgumentException ("Theme XML cannot be null or empty.", nameof (xml));

            ApplyParsedTheme (ReadThemeRoot (XDocument.Parse (xml)));
        }

        /// <summary>
        /// Reads an XML theme definition from a file and applies it immediately without registering it.
        /// </summary>
        public static void LoadFromFile (string path)
        {
            if (string.IsNullOrWhiteSpace (path))
                throw new ArgumentException ("Path cannot be null or empty.", nameof (path));

            ApplyParsedTheme (ReadThemeRoot (XDocument.Load (path)));
        }

        // --- internals -------------------------------------------------------------------------

        // Validates the document and returns the root <Theme> element, requiring a name.
        private static XElement ParseDocument (XDocument document)
        {
            var root = ReadThemeRoot (document);

            if (string.IsNullOrWhiteSpace ((string?) root.Attribute ("name")))
                throw new ThemeXmlException ("A registered theme must declare a 'name' attribute on its root <Theme> element.");

            return root;
        }

        private static XElement ReadThemeRoot (XDocument document)
        {
            var root = document.Root
                ?? throw new ThemeXmlException ("The theme document is empty.");

            if (!string.Equals (root.Name.LocalName, "Theme", StringComparison.Ordinal))
                throw new ThemeXmlException ($"The root element must be <Theme>, but was <{root.Name.LocalName}>.");

            return root;
        }

        private static string RegisterThemeElement (XElement root)
        {
            var name = ((string?) root.Attribute ("name"))!.Trim ();

            // Validate up front so registration never stores a document that would fail to apply.
            ValidateThemeElement (root);

            lock (_lock)
                registered_themes[name] = root;

            return name;
        }

        private static void ApplyParsedTheme (XElement root)
        {
            BeginUpdate ();
            try {
                ApplyThemeElement (root, new HashSet<string> (StringComparer.OrdinalIgnoreCase));
                RaiseThemeChanged ();
            } finally {
                EndUpdate ();
            }
        }

        // Applies the base (if any) and then each property element. The visiting set guards against
        // cycles in base chains between registered themes.
        private static void ApplyThemeElement (XElement root, HashSet<string> visiting)
        {
            var baseName = ((string?) root.Attribute ("base"))?.Trim ();

            if (!string.IsNullOrEmpty (baseName))
                ApplyBase (baseName!, visiting);

            foreach (var element in root.Elements ())
                ApplyPropertyElement (element);
        }

        private static void ApplyBase (string baseName, HashSet<string> visiting)
        {
            // A built-in theme name always wins as a base and resets to its defaults first.
            if (Enum.TryParse<BuiltInTheme> (baseName, ignoreCase: true, out var builtIn)) {
                SetBuiltInTheme (builtIn);
                return;
            }

            XElement? baseElement;

            lock (_lock)
                registered_themes.TryGetValue (baseName, out baseElement);

            if (baseElement is null)
                throw new ThemeXmlException ($"Base theme '{baseName}' is not a built-in theme or a registered theme.");

            var name = ((string?) baseElement.Attribute ("name"))?.Trim () ?? baseName;

            if (!visiting.Add (name))
                throw new ThemeXmlException ($"Cyclic theme inheritance detected at '{name}'.");

            ApplyThemeElement (baseElement, visiting);
            visiting.Remove (name);
        }

        // Validates that every property element is recognized and parseable, without mutating Theme.
        private static void ValidateThemeElement (XElement root)
        {
            foreach (var element in root.Elements ()) {
                var key = element.Name.LocalName;

                if (color_properties.Contains (key))
                    ParseColor (key, element.Value);
                else if (int_properties.Contains (key))
                    ParseInt (key, element.Value);
                else if (font_properties.Contains (key))
                    ParseFont (key, element);
                else
                    throw new ThemeXmlException ($"Unknown theme property '<{key}>'.");
            }
        }

        private static void ApplyPropertyElement (XElement element)
        {
            var key = element.Name.LocalName;

            if (color_properties.Contains (key))
                values[key] = ParseColor (key, element.Value);
            else if (int_properties.Contains (key))
                values[key] = ParseInt (key, element.Value);
            else if (font_properties.Contains (key))
                values[key] = ParseFont (key, element);
            else
                throw new ThemeXmlException ($"Unknown theme property '<{key}>'.");
        }

        // Accepts "#AARRGGBB"/"#RRGGBB" (with or without '#') and comma-separated "r,g,b" / "r,g,b,a".
        private static SKColor ParseColor (string property, string raw)
        {
            var text = raw?.Trim () ?? string.Empty;

            if (text.Length == 0)
                throw new ThemeXmlException ($"Theme property '<{property}>' has no color value.");

            if (text.Contains (',')) {
                var parts = text.Split (',');

                if ((parts.Length == 3 || parts.Length == 4)
                    && byte.TryParse (parts[0].Trim (), out var r)
                    && byte.TryParse (parts[1].Trim (), out var g)
                    && byte.TryParse (parts[2].Trim (), out var b)) {
                    var a = (byte) 255;
                    if (parts.Length == 4 && !byte.TryParse (parts[3].Trim (), out a))
                        throw new ThemeXmlException ($"'{text}' is not a valid color for '<{property}>'.");
                    return new SKColor (r, g, b, a);
                }

                throw new ThemeXmlException ($"'{text}' is not a valid color for '<{property}>'.");
            }

            if (SKColor.TryParse (text, out var parsed))
                return parsed;

            throw new ThemeXmlException ($"'{text}' is not a valid color for '<{property}>'.");
        }

        private static int ParseInt (string property, string raw)
        {
            if (int.TryParse (raw?.Trim (), out var value))
                return value;

            throw new ThemeXmlException ($"'{raw}' is not a valid integer for '<{property}>'.");
        }

        // A font is described by a family name (element text or a 'family' attribute) plus optional
        // weight/width/slant attributes. UIFontBold defaults to a bold weight when none is given.
        private static SKTypeface ParseFont (string property, XElement element)
        {
            var family = ((string?) element.Attribute ("family"))?.Trim ();

            if (string.IsNullOrEmpty (family))
                family = element.Value?.Trim ();

            if (string.IsNullOrEmpty (family))
                throw new ThemeXmlException ($"Theme property '<{property}>' must specify a font family.");

            var defaultWeight = string.Equals (property, nameof (UIFontBold), StringComparison.Ordinal)
                ? SKFontStyleWeight.Bold
                : SKFontStyleWeight.Normal;

            var weight = ParseEnumAttribute (element, "weight", defaultWeight, property);
            var width = ParseEnumAttribute (element, "width", SKFontStyleWidth.Normal, property);
            var slant = ParseEnumAttribute (element, "slant", SKFontStyleSlant.Upright, property);

            return SKTypeface.FromFamilyName (family, weight, width, slant);
        }

        private static T ParseEnumAttribute<T> (XElement element, string attribute, T fallback, string property)
            where T : struct, Enum
        {
            var raw = ((string?) element.Attribute (attribute))?.Trim ();

            if (string.IsNullOrEmpty (raw))
                return fallback;

            if (Enum.TryParse<T> (raw, ignoreCase: true, out var value))
                return value;

            throw new ThemeXmlException ($"'{raw}' is not a valid {typeof (T).Name} for the '{attribute}' attribute of '<{property}>'.");
        }
    }

    /// <summary>
    /// The exception thrown when an XML theme definition is malformed or references unknown values.
    /// </summary>
    public sealed class ThemeXmlException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeXmlException"/> class.
        /// </summary>
        public ThemeXmlException (string message) : base (message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeXmlException"/> class.
        /// </summary>
        public ThemeXmlException (string message, Exception innerException) : base (message, innerException) { }
    }
}
