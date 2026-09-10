using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // docs/theming.md is the document a designer -- human or coding assistant -- reads to write a theme,
    // so its reference tables must describe exactly what the parser accepts. The tables are generated
    // from ThemeCssReference between marker comments; this gate fails when the code and the document
    // drift. Regenerate with MAJORSILENCE_WRITE_THEMING_DOC=1.
    public class ThemeCssDocTests : IDisposable
    {
        private const string ReferenceBegin = "<!-- BEGIN GENERATED: reference (ThemeCssReference.ToMarkdown) -->";
        private const string ReferenceEnd = "<!-- END GENERATED: reference -->";
        private const string LightBegin = "<!-- BEGIN GENERATED: light-theme (Theme.ExportCss) -->";
        private const string LightEnd = "<!-- END GENERATED: light-theme -->";

        public ThemeCssDocTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static string LocateDoc ()
        {
            var dir = new DirectoryInfo (AppContext.BaseDirectory);

            while (dir is not null) {
                var candidate = Path.Combine (dir.FullName, "docs", "theming.md");
                if (File.Exists (candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException ("docs/theming.md not found above " + AppContext.BaseDirectory);
        }

        private static string Section (string text, string begin, string end)
        {
            var start = text.IndexOf (begin, StringComparison.Ordinal);
            var stop = text.IndexOf (end, StringComparison.Ordinal);

            Assert.True (start >= 0, $"marker missing: {begin}");
            Assert.True (stop > start, $"marker missing or out of order: {end}");

            return text.Substring (start + begin.Length, stop - start - begin.Length).Trim ('\r', '\n');
        }

        private static string Replace (string text, string begin, string end, string body)
        {
            var start = text.IndexOf (begin, StringComparison.Ordinal) + begin.Length;
            var stop = text.IndexOf (end, StringComparison.Ordinal);

            return text.Substring (0, start) + "\n" + body.TrimEnd ('\r', '\n') + "\n" + text.Substring (stop);
        }

        private static string Normalize (string s) => s.Replace ("\r\n", "\n").Trim ();

        private static string LightThemeBlock ()
        {
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
            var css = Theme.ExportCss ("MyTheme", "Light").TrimEnd ();

            // ExportCss writes the RESOLVED family of the default UI font, which is whatever the OS
            // matched -- Helvetica on macOS, DejaVu Sans on Linux, Segoe UI Emoji on Windows -- so the
            // committed document would differ per machine. Replace the two font lines with the portable
            // spelling a theme author should write.
            css = System.Text.RegularExpressions.Regex.Replace (css, @"--ui-font: ""[^""]*"";", "--ui-font: \"Segoe UI\", \"Noto Sans\", sans-serif;");
            css = System.Text.RegularExpressions.Regex.Replace (css, @"--ui-font-bold: ""[^""]*"";", "--ui-font-bold: \"Segoe UI Semibold\", \"Segoe UI\", \"Noto Sans\", sans-serif;");

            return "```css\n" + css + "\n```";
        }

        [Fact]
        public void ReferenceTables_MatchTheCode ()
        {
            var path = LocateDoc ();
            var text = File.ReadAllText (path);

            var reference = ThemeCssReference.ToMarkdown ();
            var light = LightThemeBlock ();

            if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_THEMING_DOC") == "1") {
                text = Replace (text, ReferenceBegin, ReferenceEnd, reference);
                text = Replace (text, LightBegin, LightEnd, light);
                File.WriteAllText (path, text);
                return;
            }

            Assert.Equal (Normalize (reference), Normalize (Section (text, ReferenceBegin, ReferenceEnd)));
            Assert.Equal (Normalize (light), Normalize (Section (text, LightBegin, LightEnd)));
        }

        [Fact]
        public void EveryCodeExampleInTheDoc_Parses ()
        {
            // The prose examples are what people copy. Each ```css block must parse without errors,
            // except blocks explicitly marked as showing a mistake (```css invalid).
            var text = File.ReadAllText (LocateDoc ()).Replace ("\r\n", "\n");
            var blocks = 0;

            for (var index = text.IndexOf ("```css", StringComparison.Ordinal); index >= 0; index = text.IndexOf ("```css", index + 6, StringComparison.Ordinal)) {
                var lineEnd = text.IndexOf ('\n', index);
                var fence = text.Substring (index, lineEnd - index);
                var end = text.IndexOf ("\n```", lineEnd, StringComparison.Ordinal);
                var css = text.Substring (lineEnd + 1, end - lineEnd - 1);

                if (fence.Contains ("invalid"))
                    continue;

                var sheet = ThemeStyleSheet.Parse (css);
                Assert.False (sheet.HasErrors, $"example starting at offset {index} has errors:\n{string.Join ("\n", sheet.Diagnostics)}\n---\n{css}");
                blocks++;
            }

            Assert.True (blocks >= 5, $"expected several css examples, found {blocks}");
        }

        [Fact]
        public void EveryDocumentedUnsupportedFeature_ProducesAnError ()
        {
            // The "what is not supported" table quotes the input; keep the claim honest.
            var text = File.ReadAllText (LocateDoc ()).Replace ("\r\n", "\n");
            var start = text.IndexOf ("<!-- BEGIN UNSUPPORTED -->", StringComparison.Ordinal);
            var stop = text.IndexOf ("<!-- END UNSUPPORTED -->", StringComparison.Ordinal);
            Assert.True (start >= 0 && stop > start, "unsupported-features markers missing");

            var rows = text.Substring (start, stop - start).Split ('\n')
                .Where (l => l.StartsWith ("| `", StringComparison.Ordinal))
                .Select (l => l.Split ('|')[1].Trim ().Trim ('`'))
                .ToList ();

            Assert.True (rows.Count >= 8, $"expected the unsupported table to have rows, found {rows.Count}");

            foreach (var css in rows)
                Assert.True (ThemeStyleSheet.Parse (css).HasErrors, $"documented as unsupported but parsed cleanly: {css}");
        }
    }
}
