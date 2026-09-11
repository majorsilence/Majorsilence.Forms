using System;
using System.IO;
using System.Linq;
using Majorsilence.Forms.Theming.WinForms;
using Xunit;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Theming.WinForms.Tests
{
    // The support matrix is the contract: every selector, part and property the grammar accepts must
    // have an explicit answer for WinForms, and docs/theming-winforms.md must show exactly that answer.
    public class WinFormsThemeSupportTests
    {
        private static readonly string[] Longhands = MF.ThemeCssReference.PropertyNames.Where (p => p != "border").ToArray ();

        [Fact]
        public void EverySelector_HasAMapping ()
        {
            foreach (var selector in MF.ThemeCssReference.Selectors)
                Assert.NotNull (WinFormsThemeSupport.FindMapping (selector.Name));
        }

        [Fact]
        public void EveryMappedSelector_AnswersEveryLonghand ()
        {
            foreach (var selector in MF.ThemeCssReference.Selectors) {
                var mapping = WinFormsThemeSupport.FindMapping (selector.Name)!;
                if (mapping.WinFormsTypes is null)
                    continue;

                foreach (var property in Longhands)
                    Assert.True (WinFormsThemeSupport.Find (selector.Name, null, false, property) is not null,
                        $"no support row for {selector.Name} {{ {property} }}");

                if (selector.SupportsHover)
                    foreach (var property in Longhands)
                        Assert.True (WinFormsThemeSupport.Find (selector.Name, null, true, property) is not null,
                            $"no support row for {selector.Name}:hover {{ {property} }}");
            }
        }

        [Fact]
        public void EveryPart_AnswersEveryPropertyItAccepts ()
        {
            foreach (var (selector, part) in MF.ThemeCssReference.Parts) {
                var mapping = WinFormsThemeSupport.FindMapping (selector.Name)!;
                if (mapping.WinFormsTypes is null)
                    continue;

                foreach (var property in part.Properties) {
                    Assert.True (WinFormsThemeSupport.Find (selector.Name, part.Name, false, property) is not null,
                        $"no support row for {selector.Name}::{part.Name} {{ {property} }}");

                    if (part.SupportsHover)
                        Assert.True (WinFormsThemeSupport.Find (selector.Name, part.Name, true, property) is not null,
                            $"no support row for {selector.Name}::{part.Name}:hover {{ {property} }}");
                }
            }
        }

        [Fact]
        public void NoRow_NamesAnUnknownSelectorOrProperty ()
        {
            foreach (var entry in WinFormsThemeSupport.Entries) {
                Assert.NotNull (MF.ThemeCssReference.FindSelector (entry.Selector));
                Assert.Contains (entry.Property, Longhands);
                if (entry.Part is not null)
                    Assert.NotNull (MF.ThemeCssReference.FindSelector (entry.Selector)!.FindPart (entry.Part));
            }
        }

        [Fact]
        public void SupportProperty_IsTheMatrix ()
        {
            Assert.Same (WinFormsThemeSupport.Entries, WinFormsCssTheme.Support);
        }

        // ---- the document -------------------------------------------------------------------------

        private const string Begin = "<!-- BEGIN GENERATED: winforms-support (WinFormsThemeSupport.ToMarkdown) -->";
        private const string End = "<!-- END GENERATED: winforms-support -->";

        private static string LocateDoc ()
        {
            var dir = new DirectoryInfo (AppContext.BaseDirectory);

            while (dir is not null) {
                var candidate = Path.Combine (dir.FullName, "docs", "theming-winforms.md");
                if (File.Exists (candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException ("docs/theming-winforms.md not found above " + AppContext.BaseDirectory);
        }

        private static string Normalize (string s) => s.Replace ("\r\n", "\n").Trim ();

        [Fact]
        public void SupportTables_MatchTheCode ()
        {
            // Regenerate with MAJORSILENCE_WRITE_THEMING_WINFORMS_DOC=1.
            var path = LocateDoc ();
            var text = File.ReadAllText (path);
            var generated = WinFormsThemeSupport.ToMarkdown ();

            var start = text.IndexOf (Begin, StringComparison.Ordinal);
            var stop = text.IndexOf (End, StringComparison.Ordinal);
            Assert.True (start >= 0, $"marker missing: {Begin}");
            Assert.True (stop > start, $"marker missing or out of order: {End}");

            if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_THEMING_WINFORMS_DOC") == "1") {
                File.WriteAllText (path, text.Substring (0, start + Begin.Length) + "\n" + generated.TrimEnd ('\r', '\n') + "\n" + text.Substring (stop));
                return;
            }

            var section = text.Substring (start + Begin.Length, stop - start - Begin.Length);
            Assert.Equal (Normalize (generated), Normalize (section));
        }
    }
}
