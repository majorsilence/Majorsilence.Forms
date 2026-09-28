using SkiaSharp;

namespace Majorsilence.Forms.Drawing
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

            // Merged in from the Majorsilence.Forms-side substitution table during the drawing
            // consolidation: CJK and a few legacy Windows families this table was missing.
            ["Arial Narrow"]         = ["Liberation Sans Narrow", "FreeSans", "DejaVu Sans Condensed"],
            ["Times"]                = ["Liberation Serif", "FreeSerif", "DejaVu Serif"],
            ["Courier"]              = ["Liberation Mono", "FreeMono", "DejaVu Sans Mono"],
            ["Marlett"]              = ["DejaVu Sans", "Liberation Sans", "Noto Sans"],
            ["Arial Unicode MS"]     = ["Noto Sans", "DejaVu Sans", "Liberation Sans"],
            ["MS Gothic"]            = ["Noto Sans CJK JP", "VL Gothic", "Noto Sans"],
            ["MS Mincho"]            = ["Noto Serif CJK JP", "IPAMincho", "Noto Serif"],
            ["SimSun"]               = ["Noto Serif CJK SC", "WenQuanYi Bitmap Song", "Noto Serif"],
            ["SimHei"]               = ["Noto Sans CJK SC", "WenQuanYi Zen Hei", "Noto Sans"],
            ["Microsoft YaHei"]      = ["Noto Sans CJK SC", "WenQuanYi Micro Hei", "Noto Sans"],
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
    
        // Resolved fallback faces, keyed by codepoint. Font matching is a real lookup through the
        // platform's font manager, and text is laid out on every paint -- doing it per character per
        // frame is far too slow to leave uncached.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SKTypeface?> fallbackByCodepoint = new ();

        // Same, but for VARIATION SELECTOR-16 (below): a character asking specifically for its emoji
        // presentation needs a face found through the "give me the emoji one" hint, kept apart from the
        // plain fallback cache above because the two can legitimately disagree for the same codepoint
        // (a warning sign's plain-text face and its emoji face are two different fonts).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SKTypeface?> emojiFallbackByCodepoint = new ();

        private const char EmojiPresentation = '️'; // VARIATION SELECTOR-16: draw the preceding character as an emoji.
        private const char TextPresentation = '︎';  // VARIATION SELECTOR-15: draw the preceding character as plain text.

        // Each platform's own colour emoji face, tried by name before falling back to a hinted font-manager
        // search. SKFontManager.Default.MatchCharacter (null, ["und-Zsye"], codepoint) -- the "give me the
        // emoji one" hint -- works as documented through fontconfig on Linux, but CoreText on macOS silently
        // ignores the und-Zsye script subtag and returns its ordinary text-fallback face instead (confirmed
        // against a real macOS CI runner: it resolved "Hiragino Sans", a CJK text font, while "Apple Color
        // Emoji" sat installed and untried in the same font list). Naming the face directly is exactly the
        // approach FontSubstitution.Table above already takes for the same reason -- a platform API that
        // does not reliably answer "which face has X" is worked around with a known-name list instead.
        private static readonly string[] KnownEmojiFontFamilies = [
            "Apple Color Emoji",  // macOS, iOS
            "Segoe UI Emoji",     // Windows
            "Noto Color Emoji",   // Linux, Android
        ];

        /// <summary>
        /// Splits <paramref name="text"/> into the longest possible runs that a single typeface can
        /// actually render, substituting a face that has the glyph wherever
        /// <paramref name="primary"/> does not.
        /// </summary>
        /// <remarks>
        /// Needed by every path that works one <see cref="SKFont"/> at a time -- glyph outlines in
        /// particular. A bare SKFont renders any codepoint its typeface lacks as tofu, so a string
        /// mixing scripts (or any CJK/emoji text drawn with a Latin UI font) came out as a row of
        /// boxes. Whitespace stays in the current run rather than forcing a split, which keeps runs
        /// long and the advance widths consistent with how the text was measured.
        ///
        /// A character followed by VARIATION SELECTOR-16 (U+FE0F) asks explicitly for its emoji
        /// presentation, which a plain UI font commonly cannot draw even when it has some glyph for the
        /// bare character -- WARNING SIGN (U+26A0) is exactly this: most text fonts include its
        /// monochrome triangle, so <paramref name="primary"/> looked like it already covered "⚠" and the
        /// following, invisible U+FE0F was resolved on its own, achieving nothing. The selector is
        /// resolved together with the character it modifies, through a font-manager query that asks for
        /// the emoji-presentation face specifically, and both codepoints stay in one run either way, so
        /// the selector is never measured as a glyph of its own (found and filed as #271/#281).
        /// </remarks>
        public static List<(string Text, SKTypeface Typeface)> SplitByCoverage (string text, SKTypeface primary)
        {
            var runs = new List<(string, SKTypeface)> ();

            if (string.IsNullOrEmpty (text) || primary is null)
                return runs;

            var builder = new System.Text.StringBuilder ();
            var current = primary;

            // Walked by codepoint rather than by char: a surrogate pair is one glyph, and asking
            // whether a typeface covers half of one is meaningless. Hand-rolled because this assembly
            // also targets netstandard2.0, where System.Text.Rune does not exist.
            for (var i = 0; i < text.Length;) {
                var isPair = char.IsHighSurrogate (text[i]) && i + 1 < text.Length && char.IsLowSurrogate (text[i + 1]);
                var codepoint = isPair ? char.ConvertToUtf32 (text[i], text[i + 1]) : text[i];
                var length = isPair ? 2 : 1;

                // A following presentation selector is consumed here, with its base character, rather than
                // walked to on the next iteration: on its own it is invisible and covered by nothing in
                // particular, and it must land in the same run as the character it modifies regardless.
                var selectorAt = i + length;
                var hasSelector = selectorAt < text.Length && (text[selectorAt] == EmojiPresentation || text[selectorAt] == TextPresentation);
                var wantsEmoji = hasSelector && text[selectorAt] == EmojiPresentation;

                var face = wantsEmoji ? EmojiCovering (codepoint, primary) : Covering (codepoint, primary);

                if (hasSelector)
                    length += 1;

                // Whitespace and anything the run's face already covers extend the current run.
                if (builder.Length > 0 && !ReferenceEquals (face, current)
                    && !(length == 1 && char.IsWhiteSpace (text[i]))) {
                    runs.Add ((builder.ToString (), current));
                    builder.Clear ();
                    current = face;
                } else if (builder.Length == 0) {
                    current = face;
                }

                builder.Append (text, i, length);
                i += length;
            }

            if (builder.Length > 0)
                runs.Add ((builder.ToString (), current));

            return runs;
        }

        // VARIATION SELECTOR-16 asks for the emoji presentation explicitly, so this looks for one even when
        // primary already has some (typically plain-text) glyph for the bare codepoint -- unlike Covering,
        // which only looks elsewhere once primary has nothing at all.
        private static SKTypeface EmojiCovering (int codepoint, SKTypeface primary)
        {
            var emoji = emojiFallbackByCodepoint.GetOrAdd (codepoint, FindEmojiFace);

            // No emoji-capable face on the system either: fall back to the ordinary rule rather than lose
            // the character, the same way Covering falls back to primary when nothing at all covers it.
            return emoji ?? Covering (codepoint, primary);
        }

        private static SKTypeface? FindEmojiFace (int codepoint)
        {
            foreach (var name in KnownEmojiFontFamilies) {
                var typeface = SKTypeface.FromFamilyName (name);
                if (!string.Equals (typeface.FamilyName, name, StringComparison.OrdinalIgnoreCase))
                    continue; // FromFamilyName fell back to something else -- this name isn't really installed
                if (typeface.ContainsGlyph (codepoint))
                    return typeface;
            }

            // None of the known names are installed (a Linux box with a different emoji font, say): fall
            // back to the hinted search, which is at least correct wherever the hint is honoured.
            return SKFontManager.Default.MatchCharacter (null, ["und-Zsye"], codepoint);
        }

        private static SKTypeface Covering (int codepoint, SKTypeface primary)
        {
            if (primary.ContainsGlyph (codepoint))
                return primary;

            var fallback = fallbackByCodepoint.GetOrAdd (codepoint,
                cp => SKFontManager.Default.MatchCharacter (cp));

            // No face on the system has it either: keep the primary so the caller still advances by a
            // sensible width rather than dropping the character.
            return fallback ?? primary;
        }
}
}
