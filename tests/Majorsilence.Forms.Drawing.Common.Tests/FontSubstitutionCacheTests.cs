using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // FontSubstitution.Resolve asked the platform font manager for the family and each substitute on every call,
    // about 8 ms each on Linux, and FontFamily's metrics, IsStyleAvailable and GraphicsPath.AddString all go
    // through it: a report placing a baseline per line of text paid 17 ms a line (FontBenchmarks). Answers are
    // now kept per (family, style), and FontFamily's design-unit metrics per (family, bold/italic).
    public class FontSubstitutionCacheTests
    {
        [Theory]
        [InlineData ("Arial")]
        [InlineData ("Times New Roman")]
        [InlineData ("No Such Family 1234")]
        public void Resolving_the_same_family_and_style_again_gives_the_same_typeface (string family)
        {
            var first = FontSubstitution.Resolve (family, SKFontStyle.Bold);

            Assert.Same (first, FontSubstitution.Resolve (family, SKFontStyle.Bold));
            Assert.Same (first, FontSubstitution.Resolve (family, new SKFontStyle (SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)));
        }

        [Fact]
        public void A_repeated_lookup_does_not_ask_the_font_manager_again ()
        {
            var family = new FontFamily ("Times New Roman");
            family.GetLineSpacing (FontStyle.Bold);
            family.IsStyleAvailable (FontStyle.Italic);
            FontSubstitution.Resolve ("Courier New", SKFontStyle.Normal);
            var before = FontSubstitution.UncachedResolves;

            for (var i = 0; i < 50; i++) {
                family.IsStyleAvailable (FontStyle.Italic);
                FontSubstitution.Resolve ("Courier New", SKFontStyle.Normal);
                FontSubstitution.Resolve ("Times New Roman", SKFontStyle.Bold);
            }

            Assert.Equal (before, FontSubstitution.UncachedResolves);
        }

        [Fact]
        public void Different_styles_are_kept_apart ()
        {
            var regular = FontSubstitution.Resolve ("Arial", SKFontStyle.Normal);
            var italic = FontSubstitution.Resolve ("Arial", SKFontStyle.Italic);

            Assert.False (regular.IsItalic);
            Assert.NotSame (regular, italic);
        }

        [Theory]
        [InlineData ("Arial", FontStyle.Regular)]
        [InlineData ("Arial", FontStyle.Bold | FontStyle.Italic)]
        [InlineData ("Courier New", FontStyle.Italic)]
        [InlineData ("No Such Family 1234", FontStyle.Regular)]
        public void Family_metrics_are_those_of_the_resolved_face (string name, FontStyle style)
        {
            var typeface = FontSubstitution.Resolve (name, new SKFontStyle (
                (style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright));
            using var font = new SKFont (typeface, 2048);
            var expected = font.Metrics;
            var family = new FontFamily (name);

            for (var i = 0; i < 2; i++) {   // the second time from the cache
                Assert.Equal ((int)Math.Round (-expected.Ascent), family.GetCellAscent (style));
                Assert.Equal ((int)Math.Round (expected.Descent), family.GetCellDescent (style));
                Assert.Equal ((int)Math.Round (-expected.Ascent + expected.Descent + expected.Leading), family.GetLineSpacing (style));
            }
        }

        [Fact]
        public void Underline_and_strikeout_do_not_change_family_metrics ()
        {
            var family = new FontFamily ("Arial");

            Assert.Equal (family.GetLineSpacing (FontStyle.Bold), family.GetLineSpacing (FontStyle.Bold | FontStyle.Underline | FontStyle.Strikeout));
        }

        [Fact]
        public void Resolve_from_many_threads_agrees ()
        {
            var families = new[] { "Arial", "Times New Roman", "Courier New", "DejaVu Sans", "No Such Family 1234" };
            var seen = new ConcurrentDictionary<string, SKTypeface> ();
            var errors = new ConcurrentBag<string> ();

            Parallel.For (0, 16, t => {
                for (var i = 0; i < 200; i++) {
                    var family = families[(t + i) % families.Length];
                    var typeface = FontSubstitution.Resolve (family, SKFontStyle.Normal);
                    if (!ReferenceEquals (seen.GetOrAdd (family, typeface), typeface))
                        errors.Add (family);
                }
            });

            Assert.Empty (errors);
        }

        [Fact]
        public void A_Font_hands_out_one_FontFamily ()
        {
            using var font = new Font ("Arial", 10f);

            Assert.Same (font.FontFamily, font.FontFamily);
            Assert.Equal ("Arial", font.FontFamily.Name);
        }
    }
}
