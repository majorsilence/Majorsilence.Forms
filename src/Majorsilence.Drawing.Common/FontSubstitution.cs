using SkiaSharp;

namespace Majorsilence.Drawing
{
    internal static class FontSubstitution
    {
        // Maps common Windows/macOS font names to ordered cross-platform alternatives.
        // Alternatives are tried in order; first one whose FamilyName round-trips correctly wins.
        private static readonly Dictionary<string, string[]> Table = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial"]                = ["Liberation Sans", "DejaVu Sans", "Helvetica", "FreeSans", "Noto Sans"],
            ["Times New Roman"]      = ["Liberation Serif", "DejaVu Serif", "FreeSerif", "Noto Serif"],
            ["Courier New"]          = ["Liberation Mono", "DejaVu Sans Mono", "FreeMono", "Noto Sans Mono", "Menlo"],
            ["Comic Sans MS"]        = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Impact"]               = ["DejaVu Sans Condensed", "DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Tahoma"]               = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Verdana"]              = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Georgia"]              = ["DejaVu Serif", "Liberation Serif", "Noto Serif"],
            ["Trebuchet MS"]         = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Calibri"]              = ["Carlito", "DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Cambria"]              = ["Caladea", "DejaVu Serif", "Liberation Serif", "Noto Serif"],
            ["Helvetica"]            = ["Liberation Sans", "DejaVu Sans", "Arial", "Noto Sans"],
            ["Palatino Linotype"]    = ["FreeSerif", "Noto Serif", "DejaVu Serif"],
            ["Book Antiqua"]         = ["FreeSerif", "Noto Serif", "DejaVu Serif"],
            ["Century Gothic"]       = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Lucida Console"]       = ["DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono"],
            ["Lucida Sans Unicode"]  = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Microsoft Sans Serif"] = ["Liberation Sans", "DejaVu Sans", "Noto Sans"],
            ["MS Sans Serif"]        = ["Liberation Sans", "DejaVu Sans", "Noto Sans"],
            ["Wingdings"]            = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Symbol"]               = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
        };

        // Fonts loaded from embedded resources, keyed by (FamilyName, SKFontStyle).
        private static readonly Dictionary<(string, SKFontStyle), SKTypeface> _embedded;
        // Held to prevent GC of the underlying native buffers.
        private static readonly List<SKData> _embeddedData = [];

        static FontSubstitution()
        {
            _embedded = LoadEmbeddedFonts();
        }

        private static Dictionary<(string, SKFontStyle), SKTypeface> LoadEmbeddedFonts()
        {
            var result = new Dictionary<(string, SKFontStyle), SKTypeface>();
            var assembly = typeof(FontSubstitution).Assembly;

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
                    !resourceName.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) continue;

                    // SKData.Create reads the stream; we keep the SKData alive so the
                    // native buffer remains valid for the lifetime of the typeface.
                    var data = SKData.Create(stream);
                    _embeddedData.Add(data);

                    var typeface = SKTypeface.FromData(data);
                    if (typeface == null) continue;

                    result[(typeface.FamilyName, StyleOf(typeface))] = typeface;
                }
                catch { /* skip any malformed resource */ }
            }

            return result;
        }

        /// <summary>
        /// Resolves a font family name to an SKTypeface, trying cross-platform substitutes
        /// and finally bundled embedded fonts when the requested font is not installed.
        /// </summary>
        public static SKTypeface Resolve(string familyName, SKFontStyle style)
        {
            // 1. Try system font — if it actually matched, use it.
            var typeface = SKTypeface.FromFamilyName(familyName, style);
            if (IsMatch(typeface, familyName))
                return typeface!;

            // 2. Walk the substitution table: prefer system-installed, then embedded.
            if (Table.TryGetValue(familyName, out var alternatives))
            {
                foreach (var alt in alternatives)
                {
                    var sys = SKTypeface.FromFamilyName(alt, style);
                    if (IsMatch(sys, alt))
                        return sys!;

                    var emb = GetEmbedded(alt, style);
                    if (emb != null)
                        return emb;
                }
            }

            // 3. Try an embedded version of the originally requested family.
            var direct = GetEmbedded(familyName, style);
            if (direct != null)
                return direct;

            // 4. Accept whatever the OS gave us (its own substitute) or fall back to default.
            return typeface ?? SKTypeface.Default;
        }

        private static SKTypeface? GetEmbedded(string familyName, SKFontStyle style)
        {
            if (_embedded.TryGetValue((familyName, style), out var exact))
                return exact;
            // If the exact weight/slant isn't embedded, fall back to Regular so at least
            // the right family is used (SkiaSharp will synthesize bold/italic as needed).
            if (_embedded.TryGetValue((familyName, SKFontStyle.Normal), out var regular))
                return regular;
            return null;
        }

        private static SKFontStyle StyleOf(SKTypeface typeface)
        {
            if (typeface.IsBold && typeface.IsItalic) return SKFontStyle.BoldItalic;
            if (typeface.IsBold) return SKFontStyle.Bold;
            if (typeface.IsItalic) return SKFontStyle.Italic;
            return SKFontStyle.Normal;
        }

        private static bool IsMatch(SKTypeface? typeface, string requestedFamily) =>
            typeface != null &&
            string.Equals(typeface.FamilyName, requestedFamily.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
