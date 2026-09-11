using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // The parsed pieces of a declaration's value, after the tokenizer and before a property gives them
    // meaning. A var() reference to a theme token survives as a CssTokenRef so the compiled rule can
    // read the token's *current* value every time it is applied.
    internal abstract class CssComponent
    {
        protected CssComponent (int line, int column, string raw)
        {
            Line = line;
            Column = column;
            Raw = raw;
        }

        public int Line { get; }
        public int Column { get; }
        public string Raw { get; }

        public override string ToString () => Raw;
    }

    internal sealed class CssIdent : CssComponent
    {
        public CssIdent (string name, int line, int column) : base (line, column, name) => Name = name;
        public string Name { get; }
    }

    internal sealed class CssHash : CssComponent
    {
        public CssHash (string name, int line, int column) : base (line, column, "#" + name) => Name = name;
        public string Name { get; }
    }

    internal sealed class CssString : CssComponent
    {
        public CssString (string value, int line, int column, string raw) : base (line, column, raw) => Value = value;
        public string Value { get; }
    }

    internal sealed class CssNumber : CssComponent
    {
        public CssNumber (double value, string unit, int line, int column, string raw) : base (line, column, raw)
        {
            Value = value;
            Unit = unit;
        }

        public double Value { get; }
        public string Unit { get; }
        public bool IsPercentage => Unit == "%";
    }

    internal sealed class CssFunction : CssComponent
    {
        public CssFunction (string name, List<List<CssComponent>> arguments, int line, int column, string raw) : base (line, column, raw)
        {
            Name = name;
            Arguments = arguments;
        }

        public string Name { get; }

        /// <summary>The comma-separated argument groups; each group is the components between commas.</summary>
        public List<List<CssComponent>> Arguments { get; }
    }

    internal sealed class CssDelim : CssComponent
    {
        public CssDelim (char value, int line, int column) : base (line, column, value.ToString ()) => Value = value;
        public char Value { get; }
    }

    internal sealed class CssTokenRef : CssComponent
    {
        public CssTokenRef (ThemeCssToken token, int line, int column) : base (line, column, $"var({token.Name})") => Token = token;
        public ThemeCssToken Token { get; }
    }

    // Value interpretation shared by the ':root' token declarations and the control-rule properties:
    // colors, pixel lengths, font families/weights/slants. Every TryParse* returns a *thunk* rather than
    // a value because a var(--token) reference must track the token as the theme changes later.
    internal static class ThemeCssValues
    {
        public const string ColorSyntaxHelp =
            "Use #rgb, #rrggbb, #rrggbbaa, rgb(r, g, b), rgba(r, g, b, a), hsl(h, s%, l%), a CSS color name, 'transparent', or var(--token).";

        public const string LengthSyntaxHelp = "Lengths are whole pixels: write e.g. '14px' or '14'.";

        private static readonly ConcurrentDictionary<string, SKTypeface> typefaces = new (StringComparer.OrdinalIgnoreCase);

        // ---- colors ------------------------------------------------------------------------------

        public static bool TryParseColor (List<CssComponent> components, out Func<SKColor> color, out string? error)
        {
            color = null!;
            error = null;

            if (components.Count != 1) {
                error = $"'{Join (components)}' is not a valid color. {ColorSyntaxHelp}";
                return false;
            }

            var component = components[0];

            switch (component) {
                case CssHash hash:
                    if (TryParseHexColor (hash.Name, out var hex)) {
                        color = Constant (hex);
                        return true;
                    }
                    error = $"'#{hash.Name}' is not a valid hex color: expected 3, 4, 6 or 8 hex digits (#rgb, #rgba, #rrggbb or #rrggbbaa -- note the alpha comes LAST, unlike the #AARRGGBB form used by XML themes).";
                    return false;

                case CssIdent ident:
                    if (string.Equals (ident.Name, "transparent", StringComparison.OrdinalIgnoreCase)) {
                        color = Constant (SKColors.Transparent);
                        return true;
                    }
                    if (NamedColors.TryGetValue (ident.Name, out var named)) {
                        color = Constant (named);
                        return true;
                    }
                    error = $"'{ident.Name}' is not a CSS color name. {ColorSyntaxHelp}"
                        + Suggest (ident.Name, NamedColors.Keys);
                    return false;

                case CssFunction fn:
                    return TryParseColorFunction (fn, out color, out error);

                case CssTokenRef tokenRef:
                    if (tokenRef.Token.Kind != ThemeCssValueKind.Color) {
                        error = $"'{tokenRef.Token.Name}' is a {DescribeKind (tokenRef.Token.Kind)} token, not a color, so it cannot be used where a color is expected.";
                        return false;
                    }
                    var token = tokenRef.Token;
                    color = () => (SKColor) Theme.GetTokenValue (token);
                    return true;

                default:
                    error = $"'{component.Raw}' is not a valid color. {ColorSyntaxHelp}";
                    return false;
            }
        }

        private static bool TryParseColorFunction (CssFunction fn, out Func<SKColor> color, out string? error)
        {
            color = null!;
            error = null;

            var name = fn.Name.ToLowerInvariant ();

            if (name is not ("rgb" or "rgba" or "hsl" or "hsla")) {
                error = name == "var"
                    ? $"'{fn.Raw}' could not be resolved."
                    : $"'{fn.Name}()' is not a supported color function. {ColorSyntaxHelp}";
                return false;
            }

            // Accept both the legacy comma form rgb(1, 2, 3, 0.5) and the modern space form rgb(1 2 3 / 0.5).
            var numbers = new List<CssNumber> ();

            foreach (var group in fn.Arguments)
                foreach (var component in group) {
                    if (component is CssNumber n)
                        numbers.Add (n);
                    else if (component is CssDelim { Value: '/' })
                        continue;
                    else {
                        error = $"'{fn.Raw}' is not a valid color: '{component.Raw}' is not a number. Example: {(name.StartsWith ("hsl", StringComparison.Ordinal) ? "hsl(210, 60%, 50%)" : "rgb(42, 138, 208)")}.";
                        return false;
                    }
                }

            if (numbers.Count is not (3 or 4)) {
                error = $"'{fn.Raw}' is not a valid color: expected 3 components plus an optional alpha, got {numbers.Count}.";
                return false;
            }

            byte alpha = 255;

            if (numbers.Count == 4)
                alpha = numbers[3].IsPercentage ? ClampByte (numbers[3].Value * 2.55) : ClampByte (numbers[3].Value * 255);

            if (name.StartsWith ("rgb", StringComparison.Ordinal)) {
                var r = numbers[0].IsPercentage ? ClampByte (numbers[0].Value * 2.55) : ClampByte (numbers[0].Value);
                var g = numbers[1].IsPercentage ? ClampByte (numbers[1].Value * 2.55) : ClampByte (numbers[1].Value);
                var b = numbers[2].IsPercentage ? ClampByte (numbers[2].Value * 2.55) : ClampByte (numbers[2].Value);
                color = Constant (new SKColor (r, g, b, alpha));
                return true;
            }

            var hue = (float) (((numbers[0].Value % 360) + 360) % 360);
            var sat = (float) MathCompat.Clamp (numbers[1].Value, 0, 100);
            var light = (float) MathCompat.Clamp (numbers[2].Value, 0, 100);
            color = Constant (SKColor.FromHsl (hue, sat, light, alpha));
            return true;
        }

        private static byte ClampByte (double value) => (byte) MathCompat.Clamp (Math.Round (value), 0, 255);

        public static bool TryParseHexColor (string hex, out SKColor color)
        {
            color = SKColor.Empty;

            if (hex.Length is not (3 or 4 or 6 or 8) || !hex.All (Uri.IsHexDigit))
                return false;

            if (hex.Length <= 4) {
                var sb = new StringBuilder ();
                foreach (var c in hex)
                    sb.Append (c).Append (c);
                hex = sb.ToString ();
            }

            var r = HexByte (hex, 0);
            var g = HexByte (hex, 2);
            var b = HexByte (hex, 4);
            var a = hex.Length == 8 ? HexByte (hex, 6) : (byte) 255;

            color = new SKColor (r, g, b, a);
            return true;
        }

        // Two hex digits at `index`; the caller has already checked every character is a hex digit.
        private static byte HexByte (string hex, int index)
            => (byte) ((Uri.FromHex (hex[index]) << 4) | Uri.FromHex (hex[index + 1]));

        /// <summary>Formats a color the way the stylesheet would spell it: #rrggbb, or #rrggbbaa when translucent.</summary>
        public static string FormatColor (SKColor color)
            => color.Alpha == 255
                ? $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}"
                : $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}{color.Alpha:x2}";

        // ---- lengths -----------------------------------------------------------------------------

        public static bool TryParseLength (List<CssComponent> components, out Func<int> length, out string? error)
        {
            length = null!;
            error = null;

            if (components.Count != 1) {
                error = $"'{Join (components)}' is not a valid length. {LengthSyntaxHelp}";
                return false;
            }

            switch (components[0]) {
                case CssNumber number:
                    if (number.Unit.Length > 0 && !string.Equals (number.Unit, "px", StringComparison.OrdinalIgnoreCase)) {
                        error = $"Unsupported unit '{number.Unit}' in '{number.Raw}'. {LengthSyntaxHelp} Points, em, rem and percentages are not supported because every Majorsilence.Forms style size is a pixel value.";
                        return false;
                    }
                    if (number.Value < 0) {
                        error = $"'{number.Raw}' is negative; lengths must be zero or positive.";
                        return false;
                    }
                    var value = (int) Math.Round (number.Value);
                    length = () => value;
                    return true;

                case CssTokenRef tokenRef:
                    if (tokenRef.Token.Kind != ThemeCssValueKind.Length) {
                        error = $"'{tokenRef.Token.Name}' is a {DescribeKind (tokenRef.Token.Kind)} token, not a length, so it cannot be used where a length is expected.";
                        return false;
                    }
                    var token = tokenRef.Token;
                    length = () => (int) Theme.GetTokenValue (token);
                    return true;

                default:
                    error = $"'{components[0].Raw}' is not a valid length. {LengthSyntaxHelp}";
                    return false;
            }
        }

        // ---- fonts -------------------------------------------------------------------------------

        /// <summary>
        /// Parses a font-family list: quoted strings and/or bare identifiers (multi-word unquoted names
        /// are joined with a space), separated by commas. A var() of a font token contributes that
        /// token's family at apply time.
        /// </summary>
        public static bool TryParseFontFamilies (List<CssComponent> components, out Func<IReadOnlyList<string>> families, out string? error)
        {
            families = null!;
            error = null;

            var parts = new List<Func<string>> ();
            var pending = new List<string> ();

            void Flush ()
            {
                if (pending.Count > 0) {
                    var name = string.Join (" ", pending);
                    parts.Add (() => name);
                    pending.Clear ();
                }
            }

            foreach (var component in components) {
                switch (component) {
                    case CssString s:
                        Flush ();
                        var value = s.Value;
                        parts.Add (() => value);
                        break;
                    case CssIdent ident:
                        pending.Add (ident.Name);
                        break;
                    case CssDelim { Value: ',' }:
                        Flush ();
                        break;
                    case CssTokenRef tokenRef when tokenRef.Token.Kind == ThemeCssValueKind.FontFamily:
                        Flush ();
                        var token = tokenRef.Token;
                        parts.Add (() => ((SKTypeface) Theme.GetTokenValue (token)).FamilyName);
                        break;
                    default:
                        error = $"'{component.Raw}' is not a valid font family. Write a quoted name like \"Segoe UI\", a bare name like Arial, or a list: \"Inter\", \"Segoe UI\", sans-serif.";
                        return false;
                }
            }

            Flush ();

            if (parts.Count == 0) {
                error = "A font family is required, e.g. font-family: \"Segoe UI\", sans-serif;";
                return false;
            }

            families = () => parts.Select (p => p ()).ToList ();
            return true;
        }

        public static bool TryParseFontWeight (List<CssComponent> components, out SKFontStyleWeight weight, out string? error)
        {
            weight = SKFontStyleWeight.Normal;
            error = null;

            if (components.Count == 1) {
                if (components[0] is CssIdent ident) {
                    switch (ident.Name.ToLowerInvariant ()) {
                        case "normal": weight = SKFontStyleWeight.Normal; return true;
                        case "bold": weight = SKFontStyleWeight.Bold; return true;
                        case "lighter": weight = SKFontStyleWeight.Light; return true;
                        case "bolder": weight = SKFontStyleWeight.ExtraBold; return true;
                    }
                } else if (components[0] is CssNumber { Unit: "" } number
                           && number.Value >= 100 && number.Value <= 900 && Math.Abs (number.Value % 100) < 0.001) {
                    weight = (SKFontStyleWeight) (int) number.Value;
                    return true;
                }
            }

            error = $"'{Join (components)}' is not a valid font-weight. Use normal, bold, lighter, bolder, or a multiple of 100 from 100 to 900.";
            return false;
        }

        public static bool TryParseFontStyle (List<CssComponent> components, out SKFontStyleSlant slant, out string? error)
        {
            slant = SKFontStyleSlant.Upright;
            error = null;

            if (components.Count == 1 && components[0] is CssIdent ident) {
                switch (ident.Name.ToLowerInvariant ()) {
                    case "normal": slant = SKFontStyleSlant.Upright; return true;
                    case "italic": slant = SKFontStyleSlant.Italic; return true;
                    case "oblique": slant = SKFontStyleSlant.Oblique; return true;
                }
            }

            error = $"'{Join (components)}' is not a valid font-style. Use normal, italic or oblique.";
            return false;
        }

        /// <summary>
        /// Resolves a family list to a typeface the way a browser does: the first family the system
        /// actually has wins; if none match, Skia's fallback for the first name is used so text still
        /// draws. Cached, because compiled rules re-resolve on every theme change.
        /// </summary>
        public static SKTypeface GetTypeface (IReadOnlyList<string> families, SKFontStyleWeight weight, SKFontStyleSlant slant)
        {
            var key = string.Join ("|", families) + "|" + (int) weight + "|" + (int) slant;

            return typefaces.GetOrAdd (key, _ => {
                SKTypeface? first = null;

                foreach (var family in families) {
                    var typeface = SKTypeface.FromFamilyName (family, weight, SKFontStyleWidth.Normal, slant);

                    if (typeface is null)
                        continue;

                    first ??= typeface;

                    if (string.Equals (typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                        return typeface;
                }

                return first ?? SKTypeface.FromFamilyName (families[0], weight, SKFontStyleWidth.Normal, slant) ?? SKTypeface.Default;
            });
        }

        // ---- helpers -----------------------------------------------------------------------------

        private static Func<SKColor> Constant (SKColor color) => () => color;

        public static string Join (IEnumerable<CssComponent> components) => string.Join (" ", components.Select (c => c.Raw));

        public static string DescribeKind (ThemeCssValueKind kind) => kind switch {
            ThemeCssValueKind.Color => "color",
            ThemeCssValueKind.Length => "length",
            ThemeCssValueKind.FontWeight => "font-weight",
            ThemeCssValueKind.FontStyle => "font-style",
            _ => "font-family"
        };

        /// <summary>
        /// Returns " Did you mean 'x'?" for the closest candidate within a small edit distance, or an
        /// empty string. Typos are the most common failure in hand- or model-written CSS, and a name
        /// suggestion turns a "why does nothing happen" into a one-character fix.
        /// </summary>
        public static string Suggest (string name, IEnumerable<string> candidates)
        {
            var best = FindClosest (name, candidates);
            return best is null ? string.Empty : $" Did you mean '{best}'?";
        }

        public static string? FindClosest (string name, IEnumerable<string> candidates)
        {
            string? best = null;
            var bestDistance = int.MaxValue;
            // Up to half the characters may differ: 'heading' -> 'header' (3 edits of 7) is the kind of
            // near miss a suggestion exists for, and the candidate lists are short and distinct enough
            // that a looser bound does not produce misleading matches.
            var limit = Math.Max (2, (name.Length + 1) / 2);

            foreach (var candidate in candidates) {
                var lower = name.ToLowerInvariant ();
                var candidateLower = candidate.ToLowerInvariant ();

                // A truncated or over-long spelling ('background' for 'background-color') is as common
                // as a typo and scores badly on edit distance, so treat a prefix match as nearly exact.
                var prefix = Math.Min (lower.Length, candidateLower.Length) >= 4
                    && (candidateLower.StartsWith (lower, StringComparison.Ordinal) || lower.StartsWith (candidateLower, StringComparison.Ordinal));
                var distance = prefix ? 1 : Levenshtein (lower, candidateLower);

                if (distance < bestDistance && distance <= limit) {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static int Levenshtein (string a, string b)
        {
            var costs = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
                costs[j] = j;

            for (var i = 1; i <= a.Length; i++) {
                var previous = costs[0];
                costs[0] = i;

                for (var j = 1; j <= b.Length; j++) {
                    var current = costs[j];
                    costs[j] = Math.Min (Math.Min (costs[j] + 1, costs[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                    previous = current;
                }
            }

            return costs[b.Length];
        }

        // The CSS Color Module Level 4 named colors (148 including rebeccapurple; the gray/grey pairs
        // are both present). Kept as a table rather than reflected off SKColors so the set is exactly
        // the documented CSS one and trimming/AOT never has to preserve SKColors' fields.
        public static readonly IReadOnlyDictionary<string, SKColor> NamedColors = BuildNamedColors ();

        private static Dictionary<string, SKColor> BuildNamedColors ()
        {
            var table = new Dictionary<string, SKColor> (StringComparer.OrdinalIgnoreCase);

            void Add (string name, uint rgb) => table[name] = new SKColor ((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);

            Add ("aliceblue", 0xf0f8ff); Add ("antiquewhite", 0xfaebd7); Add ("aqua", 0x00ffff); Add ("aquamarine", 0x7fffd4);
            Add ("azure", 0xf0ffff); Add ("beige", 0xf5f5dc); Add ("bisque", 0xffe4c4); Add ("black", 0x000000);
            Add ("blanchedalmond", 0xffebcd); Add ("blue", 0x0000ff); Add ("blueviolet", 0x8a2be2); Add ("brown", 0xa52a2a);
            Add ("burlywood", 0xdeb887); Add ("cadetblue", 0x5f9ea0); Add ("chartreuse", 0x7fff00); Add ("chocolate", 0xd2691e);
            Add ("coral", 0xff7f50); Add ("cornflowerblue", 0x6495ed); Add ("cornsilk", 0xfff8dc); Add ("crimson", 0xdc143c);
            Add ("cyan", 0x00ffff); Add ("darkblue", 0x00008b); Add ("darkcyan", 0x008b8b); Add ("darkgoldenrod", 0xb8860b);
            Add ("darkgray", 0xa9a9a9); Add ("darkgreen", 0x006400); Add ("darkgrey", 0xa9a9a9); Add ("darkkhaki", 0xbdb76b);
            Add ("darkmagenta", 0x8b008b); Add ("darkolivegreen", 0x556b2f); Add ("darkorange", 0xff8c00); Add ("darkorchid", 0x9932cc);
            Add ("darkred", 0x8b0000); Add ("darksalmon", 0xe9967a); Add ("darkseagreen", 0x8fbc8f); Add ("darkslateblue", 0x483d8b);
            Add ("darkslategray", 0x2f4f4f); Add ("darkslategrey", 0x2f4f4f); Add ("darkturquoise", 0x00ced1); Add ("darkviolet", 0x9400d3);
            Add ("deeppink", 0xff1493); Add ("deepskyblue", 0x00bfff); Add ("dimgray", 0x696969); Add ("dimgrey", 0x696969);
            Add ("dodgerblue", 0x1e90ff); Add ("firebrick", 0xb22222); Add ("floralwhite", 0xfffaf0); Add ("forestgreen", 0x228b22);
            Add ("fuchsia", 0xff00ff); Add ("gainsboro", 0xdcdcdc); Add ("ghostwhite", 0xf8f8ff); Add ("gold", 0xffd700);
            Add ("goldenrod", 0xdaa520); Add ("gray", 0x808080); Add ("green", 0x008000); Add ("greenyellow", 0xadff2f);
            Add ("grey", 0x808080); Add ("honeydew", 0xf0fff0); Add ("hotpink", 0xff69b4); Add ("indianred", 0xcd5c5c);
            Add ("indigo", 0x4b0082); Add ("ivory", 0xfffff0); Add ("khaki", 0xf0e68c); Add ("lavender", 0xe6e6fa);
            Add ("lavenderblush", 0xfff0f5); Add ("lawngreen", 0x7cfc00); Add ("lemonchiffon", 0xfffacd); Add ("lightblue", 0xadd8e6);
            Add ("lightcoral", 0xf08080); Add ("lightcyan", 0xe0ffff); Add ("lightgoldenrodyellow", 0xfafad2); Add ("lightgray", 0xd3d3d3);
            Add ("lightgreen", 0x90ee90); Add ("lightgrey", 0xd3d3d3); Add ("lightpink", 0xffb6c1); Add ("lightsalmon", 0xffa07a);
            Add ("lightseagreen", 0x20b2aa); Add ("lightskyblue", 0x87cefa); Add ("lightslategray", 0x778899); Add ("lightslategrey", 0x778899);
            Add ("lightsteelblue", 0xb0c4de); Add ("lightyellow", 0xffffe0); Add ("lime", 0x00ff00); Add ("limegreen", 0x32cd32);
            Add ("linen", 0xfaf0e6); Add ("magenta", 0xff00ff); Add ("maroon", 0x800000); Add ("mediumaquamarine", 0x66cdaa);
            Add ("mediumblue", 0x0000cd); Add ("mediumorchid", 0xba55d3); Add ("mediumpurple", 0x9370db); Add ("mediumseagreen", 0x3cb371);
            Add ("mediumslateblue", 0x7b68ee); Add ("mediumspringgreen", 0x00fa9a); Add ("mediumturquoise", 0x48d1cc); Add ("mediumvioletred", 0xc71585);
            Add ("midnightblue", 0x191970); Add ("mintcream", 0xf5fffa); Add ("mistyrose", 0xffe4e1); Add ("moccasin", 0xffe4b5);
            Add ("navajowhite", 0xffdead); Add ("navy", 0x000080); Add ("oldlace", 0xfdf5e6); Add ("olive", 0x808000);
            Add ("olivedrab", 0x6b8e23); Add ("orange", 0xffa500); Add ("orangered", 0xff4500); Add ("orchid", 0xda70d6);
            Add ("palegoldenrod", 0xeee8aa); Add ("palegreen", 0x98fb98); Add ("paleturquoise", 0xafeeee); Add ("palevioletred", 0xdb7093);
            Add ("papayawhip", 0xffefd5); Add ("peachpuff", 0xffdab9); Add ("peru", 0xcd853f); Add ("pink", 0xffc0cb);
            Add ("plum", 0xdda0dd); Add ("powderblue", 0xb0e0e6); Add ("purple", 0x800080); Add ("rebeccapurple", 0x663399);
            Add ("red", 0xff0000); Add ("rosybrown", 0xbc8f8f); Add ("royalblue", 0x4169e1); Add ("saddlebrown", 0x8b4513);
            Add ("salmon", 0xfa8072); Add ("sandybrown", 0xf4a460); Add ("seagreen", 0x2e8b57); Add ("seashell", 0xfff5ee);
            Add ("sienna", 0xa0522d); Add ("silver", 0xc0c0c0); Add ("skyblue", 0x87ceeb); Add ("slateblue", 0x6a5acd);
            Add ("slategray", 0x708090); Add ("slategrey", 0x708090); Add ("snow", 0xfffafa); Add ("springgreen", 0x00ff7f);
            Add ("steelblue", 0x4682b4); Add ("tan", 0xd2b48c); Add ("teal", 0x008080); Add ("thistle", 0xd8bfd8);
            Add ("tomato", 0xff6347); Add ("turquoise", 0x40e0d0); Add ("violet", 0xee82ee); Add ("wheat", 0xf5deb3);
            Add ("white", 0xffffff); Add ("whitesmoke", 0xf5f5f5); Add ("yellow", 0xffff00); Add ("yellowgreen", 0x9acd32);

            return table;
        }
    }
}
