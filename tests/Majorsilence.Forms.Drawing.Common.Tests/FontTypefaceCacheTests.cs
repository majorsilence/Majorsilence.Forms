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
