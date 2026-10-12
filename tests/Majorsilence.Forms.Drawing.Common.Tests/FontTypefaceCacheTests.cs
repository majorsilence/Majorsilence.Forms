using Majorsilence.Forms.Drawing;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // SKTypeface.FromFamilyName is a platform font-manager query (fontconfig on Linux), a few
    // milliseconds per call even warm. Report rendering builds a fresh Font per text run and hits it
    // once each, so a page of text was dozens of round-trips (and a disposable SKTypeface each) on
    // every repaint -- the "slight delay after every edit" on the ReportDesigner design surface.
    // Font now resolves system typefaces through a process-wide cache keyed by (family, weight,
    // slant), and never disposes a cached (shared) typeface.
    public class FontTypefaceCacheTests
    {
        [Fact]
        public void Two_fonts_with_the_same_family_and_style_share_one_typeface ()
        {
            using var a = new Font ("Arial", 10f);
            using var b = new Font ("Arial", 24f, bold: false);   // different size, same face

            Assert.Same (a.GetSKTypeface (), b.GetSKTypeface ());
        }

        [Fact]
        public void Bold_and_regular_do_not_share_a_typeface ()
        {
            using var regular = new Font ("Arial", 10f);
            using var bold = new Font ("Arial", 10f, bold: true);

            Assert.NotSame (regular.GetSKTypeface (), bold.GetSKTypeface ());
        }

        // On a platform with no fonts of its own the cache must resolve through FontSubstitution
        // (substitute table, then the bundled fonts), not ask the platform font manager directly. Where
        // that manager answers every request with one face (browser-wasm only has "Noto Mono"), the
        // direct query drew every font in that face and lost bold. Anywhere else the platform's own
        // answer stands, as it always did. Either way the Font gets what ResolveWhereNoSystemFonts picks.
        [Theory]
        [InlineData ("Arial", false)]
        [InlineData ("Arial", true)]
        [InlineData ("Times New Roman", false)]
        [InlineData ("Times New Roman", true)]
        [InlineData ("Courier New", true)]
        [InlineData ("Calibri", false)]
        public void Font_resolves_through_the_substitution_only_where_the_platform_has_no_fonts (string family, bool bold)
        {
            var style = bold ? SkiaSharp.SKFontStyle.Bold : SkiaSharp.SKFontStyle.Normal;
            var expected = FontSubstitution.ResolveWhereNoSystemFonts (family, style);

            using var font = new Font (family, 10f, bold: bold);

            Assert.Equal (expected.FamilyName, font.GetSKTypeface ().FamilyName);
            Assert.Equal (expected.FontWeight, font.GetSKTypeface ().FontWeight);
        }

        // The bundled fonts are keyed by shared SKFontStyle instances and SKFontStyle compares by
        // reference, so a style built from a weight (what the cache and Font use) used to miss the exact
        // bold face and get the Regular one instead.
        [Theory]
        [InlineData ("Liberation Sans")]
        [InlineData ("Carlito")]
        public void Bundled_bold_is_found_for_a_style_built_from_a_weight (string bundledFamily)
        {
            var built = new SkiaSharp.SKFontStyle (SkiaSharp.SKFontStyleWeight.Bold, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright);

            var face = FontSubstitution.Resolve (bundledFamily, built);

            Assert.True (face.FontWeight >= (int) SkiaSharp.SKFontStyleWeight.SemiBold, $"{face.FamilyName} resolved at weight {face.FontWeight}");
        }

        // Majorsilence.Forms asks for "sans-serif" as its UI family off Windows. Where the platform has
        // real fonts it maps that name itself and the theme (and the ambient default font) depend on the
        // answer being a family the system really has; only a platform with no fonts (browser-wasm) gets
        // a bundled face.
        [Theory]
        [InlineData ("sans-serif")]
        [InlineData ("serif")]
        [InlineData ("monospace")]
        [InlineData ("Segoe UI")]
        public void Generic_and_shell_family_names_use_the_platform_when_it_has_fonts_else_a_bundled_face (string family)
        {
            using var font = new Font (family, 10f);
            var resolved = font.GetSKTypeface ();

            Assert.False (string.IsNullOrEmpty (resolved.FamilyName));

            if (FontSubstitution.PlatformHasFonts) {
                using var platform = SkiaSharp.SKTypeface.FromFamilyName (family, SkiaSharp.SKFontStyle.Normal);
                Assert.Equal (platform.FamilyName, resolved.FamilyName);
            } else {
                Assert.NotEqual ("Noto Mono", resolved.FamilyName);
            }
        }

        [Fact]
        public void Disposing_a_font_does_not_dispose_the_shared_typeface ()
        {
            var first = new Font ("Arial", 10f);
            var face = first.GetSKTypeface ();
            first.Dispose ();

            using var second = new Font ("Arial", 10f);

            // Still the same instance, and still usable (a disposed SKTypeface throws on access).
            Assert.Same (face, second.GetSKTypeface ());
            Assert.False (string.IsNullOrEmpty (second.GetSKTypeface ().FamilyName));
        }
    }
}
