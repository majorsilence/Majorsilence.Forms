using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    // Inside the namespace, so Avalonia types win over the Majorsilence.Forms ones of the same name.
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;

    // The support matrix is the contract: every selector, part and property the grammar accepts must
    // have an explicit answer for Avalonia, every Fluent resource it writes must exist in the real theme
    // with the type the applier writes, and docs/theming-avalonia.md must show exactly that answer.
    public class AvaloniaThemeSupportTests
    {
        private static readonly string[] Longhands = MF.ThemeCssReference.PropertyNames.Where (p => p != "border").ToArray ();

        [Fact]
        public void EverySelector_HasAMapping ()
        {
            foreach (var selector in MF.ThemeCssReference.Selectors)
                Assert.NotNull (AvaloniaThemeSupport.FindMapping (selector.Name));
        }

        [Fact]
        public void EveryMappedSelector_AnswersEveryLonghand ()
        {
            foreach (var selector in MF.ThemeCssReference.Selectors) {
                var mapping = AvaloniaThemeSupport.FindMapping (selector.Name)!;
                if (mapping.AvaloniaTypes is null)
                    continue;

                foreach (var property in Longhands) {
                    Assert.True (AvaloniaThemeSupport.Find (selector.Name, null, false, property) is not null,
                        $"no support row for {selector.Name} {{ {property} }}");

                    if (selector.SupportsHover)
                        Assert.True (AvaloniaThemeSupport.Find (selector.Name, null, true, property) is not null,
                            $"no support row for {selector.Name}:hover {{ {property} }}");
                }
            }
        }

        [Fact]
        public void EveryPart_AnswersEveryPropertyItAccepts ()
        {
            foreach (var (selector, part) in MF.ThemeCssReference.Parts) {
                var mapping = AvaloniaThemeSupport.FindMapping (selector.Name)!;
                if (mapping.AvaloniaTypes is null)
                    continue;

                foreach (var property in part.Properties) {
                    Assert.True (AvaloniaThemeSupport.Find (selector.Name, part.Name, false, property) is not null,
                        $"no support row for {selector.Name}::{part.Name} {{ {property} }}");

                    if (part.SupportsHover)
                        Assert.True (AvaloniaThemeSupport.Find (selector.Name, part.Name, true, property) is not null,
                            $"no support row for {selector.Name}::{part.Name}:hover {{ {property} }}");
                }
            }
        }

        [Fact]
        public void NoRow_NamesAnUnknownSelectorPartOrProperty ()
        {
            foreach (var entry in AvaloniaThemeSupport.Entries) {
                var selector = MF.ThemeCssReference.FindSelector (entry.Selector);
                Assert.NotNull (selector);
                Assert.Contains (entry.Property, Longhands);

                if (entry.Part is not null) {
                    var part = selector!.FindPart (entry.Part);
                    Assert.NotNull (part);
                    Assert.True (part!.Accepts (entry.Property), $"{entry} names a property the part does not accept");
                    if (entry.Hover)
                        Assert.True (part.SupportsHover, $"{entry} is a hover row on a part without :hover");
                } else if (entry.Hover) {
                    Assert.True (selector!.SupportsHover, $"{entry} is a hover row on a selector without :hover");
                }
            }
        }

        [Fact]
        public void SupportedRows_WriteSomething_AndUnsupportedRowsWriteNothing ()
        {
            foreach (var entry in AvaloniaThemeSupport.Entries) {
                var writes = entry.ResourceKeys.Count + entry.StyleSelectors.Count;
                if (entry.Level == AvaloniaThemeSupportLevel.Unsupported)
                    Assert.True (writes == 0, $"{entry} is unsupported but writes {writes} target(s)");
                else
                    Assert.True (writes > 0, $"{entry} is supported but writes nothing");
            }
        }

        [Fact]
        public void SupportProperty_IsTheMatrix ()
        {
            Assert.Same (AvaloniaThemeSupport.Entries, AvaloniaCssTheme.Support);
        }

        // ---- the real Fluent theme -----------------------------------------------------------------

        // Every key the matrix (and the token pass) writes must already exist in Fluent or the DataGrid
        // theme, as the same type -- a misspelt key or a Color written where a brush is read would
        // otherwise be a silent no-op, the one thing the subset promises never to do.
        [Fact]
        public Task EveryResourceKey_ExistsInTheFluentThemesWithTheSameType () => Headless.Run (() => {
            var themes = Application.Current!.Styles;
            var targets = AvaloniaThemeSupport.Entries
                .SelectMany (e => e.Targets.OfType<ResourceTarget> ())
                .Select (t => (t.Key, t.Kind))
                .Concat (new[] {
                    ("SystemAccentColor", ResourceKind.Color), ("SystemAccentColorLight1", ResourceKind.Color),
                    ("SystemAccentColorLight2", ResourceKind.Color), ("SystemAccentColorLight3", ResourceKind.Color),
                    ("SystemAccentColorDark1", ResourceKind.Color), ("SystemAccentColorDark2", ResourceKind.Color),
                    ("SystemAccentColorDark3", ResourceKind.Color), ("TextControlSelectionHighlightColor", ResourceKind.Brush),
                    ("ControlContentThemeFontSize", ResourceKind.Double),
                })
                .Distinct ()
                .ToList ();

            var problems = new List<string> ();

            foreach (var (key, kind) in targets) {
                if (!themes.TryGetResource (key, ThemeVariant.Light, out var value)) {
                    problems.Add ($"{key}: not a Fluent/DataGrid resource");
                    continue;
                }

                var ok = kind switch {
                    ResourceKind.Brush => value is IBrush,
                    ResourceKind.Color => value is Color,
                    ResourceKind.Double => value is double,
                    _ => false
                };

                if (!ok)
                    problems.Add ($"{key}: Fluent declares {value?.GetType ().Name ?? "null"}, the matrix writes {kind}");
            }

            Assert.True (themes.TryGetResource ("ContentControlThemeFontFamily", ThemeVariant.Light, out var family) && family is FontFamily,
                "ContentControlThemeFontFamily is not a FontFamily resource");
            Assert.True (problems.Count == 0, string.Join ("\n", problems));
        });

        // ---- the document -------------------------------------------------------------------------

        private const string Begin = "<!-- BEGIN GENERATED: avalonia-support (AvaloniaThemeSupport.ToMarkdown) -->";
        private const string End = "<!-- END GENERATED: avalonia-support -->";

        private static string LocateDoc ()
        {
            var dir = new DirectoryInfo (AppContext.BaseDirectory);

            while (dir is not null) {
                var candidate = Path.Combine (dir.FullName, "docs", "theming-avalonia.md");
                if (File.Exists (candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException ("docs/theming-avalonia.md not found above " + AppContext.BaseDirectory);
        }

        private static string Normalize (string s) => s.Replace ("\r\n", "\n").Trim ();

        [Fact]
        public void SupportTables_MatchTheCode ()
        {
            // Regenerate with MAJORSILENCE_WRITE_THEMING_AVALONIA_DOC=1.
            var path = LocateDoc ();
            var text = File.ReadAllText (path);
            var generated = AvaloniaThemeSupport.ToMarkdown ();

            var start = text.IndexOf (Begin, StringComparison.Ordinal);
            var stop = text.IndexOf (End, StringComparison.Ordinal);
            Assert.True (start >= 0, $"marker missing: {Begin}");
            Assert.True (stop > start, $"marker missing or out of order: {End}");

            if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_THEMING_AVALONIA_DOC") == "1") {
                File.WriteAllText (path, text.Substring (0, start + Begin.Length) + "\n" + generated.TrimEnd ('\r', '\n') + "\n" + text.Substring (stop));
                return;
            }

            var section = text.Substring (start + Begin.Length, stop - start - Begin.Length);
            Assert.Equal (Normalize (generated), Normalize (section));
        }
    }
}
