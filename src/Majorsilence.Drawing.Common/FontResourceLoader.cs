namespace Majorsilence.Drawing
{
    /// <summary>
    /// Extracts the embedded fallback fonts to a temporary directory so that
    /// renderers needing file-system font paths (e.g. the iTextSharp PDF renderer)
    /// can locate them without requiring the fonts to be installed on the host.
    /// Also provides CSS font-family stack helpers for HTML renderers.
    /// </summary>
    public static class FontResourceLoader
    {
        // Maps known Windows/macOS font names → ordered cross-platform alternatives,
        // mirroring the substitution table in FontSubstitution so we can build CSS stacks
        // without exposing SkiaSharp types.
        private static readonly Dictionary<string, string[]> _cssTable = new(StringComparer.OrdinalIgnoreCase)
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

        private static readonly HashSet<string> _serifFonts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Times New Roman", "Liberation Serif", "DejaVu Serif", "Georgia",
            "Palatino Linotype", "Book Antiqua", "FreeSerif", "Noto Serif",
            "Caladea", "Cambria",
        };

        private static readonly HashSet<string> _monoFonts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Courier New", "Liberation Mono", "DejaVu Sans Mono", "FreeMono",
            "Noto Sans Mono", "Lucida Console", "Menlo", "Consolas",
        };

        /// <summary>
        /// Returns a CSS font-family value that starts with <paramref name="fontFamily"/> and
        /// appends cross-platform alternatives from the substitution table plus a generic
        /// CSS family keyword (serif / monospace / sans-serif) as a final browser fallback.
        /// </summary>
        public static string GetCssFontStack(string fontFamily)
        {
            var primary = fontFamily.Split(',')[0].Trim().Trim('\'').Trim('"');

            var parts = new List<string> { QuoteCss(primary) };

            if (_cssTable.TryGetValue(primary, out var alternatives))
                foreach (var alt in alternatives)
                    parts.Add(QuoteCss(alt));

            parts.Add(
                _serifFonts.Contains(primary) ? "serif" :
                _monoFonts.Contains(primary)  ? "monospace" :
                "sans-serif"
            );

            return string.Join(", ", parts);
        }

        private static string QuoteCss(string name) =>
            name.Contains(' ') ? $"'{name}'" : name;

        private static string? _extractedDir;

#if NET10_OR_GREATER
        private static readonly Lock _lock = new();
#else
        private static readonly object _lock = new();
#endif

        /// <summary>
        /// Returns a directory path containing all bundled .ttf files.
        /// Fonts are extracted from the assembly on the first call; subsequent
        /// calls return the cached path immediately.
        /// </summary>
        public static string GetFontDirectory()
        {
            if (_extractedDir != null)
                return _extractedDir;

            lock (_lock)
            {
                if (_extractedDir != null)
                    return _extractedDir;

                var dir = Path.Combine(Path.GetTempPath(), "majorsilence-fonts");
                Directory.CreateDirectory(dir);
                ExtractAll(dir);
                _extractedDir = dir;
                return dir;
            }
        }

        private static void ExtractAll(string dir)
        {
            var assembly = typeof(FontResourceLoader).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Resource name format: Majorsilence.Drawing.Common.Fonts.LiberationSans-Regular.ttf
                // Reconstruct the filename from the last two dot-separated segments.
                var parts = name.Split('.');
                if (parts.Length < 2) continue;
#if NET8_0_OR_GREATER
                var filename = parts[^2] + "." + parts[^1]; // e.g. "LiberationSans-Regular.ttf"
#else
                var filename = parts[parts.Length - 2] + "." + parts[parts.Length - 1]; // e.g. "LiberationSans-Regular.ttf"
#endif

                var dest = Path.Combine(dir, filename);
                if (File.Exists(dest)) continue;

                try
                {
                    using var stream = assembly.GetManifestResourceStream(name);
                    if (stream == null) continue;
                    using var file = File.Create(dest);
                    stream.CopyTo(file);
                }
                catch { /* skip any unreadable resource */ }
            }
        }
    }
}
