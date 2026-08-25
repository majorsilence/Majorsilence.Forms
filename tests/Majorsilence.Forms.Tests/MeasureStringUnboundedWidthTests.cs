using System.Drawing;
using Majorsilence.Forms.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // In GDI+ a layout width of zero means UNBOUNDED, not "wrap at zero pixels". That is not a quirk
    // to be nice about -- Graphics.MeasureString(text, font) is itself specified as passing a layout
    // area of SizeF(0, 0), so libraries that funnel every measurement through one constrained overload
    // pass 0 as their "no limit" value and expect a single line back.
    //
    // Majorsilence.Forms took the 0 literally and handed RichTextKit a 0-wide box, which wrapped to one
    // grapheme per line. The visible result was not a slightly wrong number: auto-sized controls asked
    // for the size of a vertical sliver -- ~1 character wide and a dozen lines tall -- so their captions
    // were clipped to a couple of letters and the controls themselves grew far past the rows meant to
    // contain them, leaving round buttons drawn as domes. Found in a WinForms control library whose
    // whole button page rendered that way.
    public class MeasureStringUnboundedWidthTests
    {
        private const string Sentence = "Primary Button";

        // A Graphics needs a canvas to exist, though measurement never touches it.
        private static Graphics Measurer ()
            => new Graphics (new SKCanvas (new SKBitmap (8, 8, SKColorType.Bgra8888, SKAlphaType.Premul)));

        [Fact]
        public void Zero_width_measures_unbounded_like_the_no_width_overload ()
        {
            var g = Measurer ();
            using var font = new Font ("Arial", 12);

            var unbounded = g.MeasureString (Sentence, font);
            var zeroWidth = g.MeasureString (Sentence, font, 0, (Majorsilence.Forms.Drawing.StringFormat?)null);

            Assert.Equal (unbounded.Width, zeroWidth.Width, 1);
            Assert.Equal (unbounded.Height, zeroWidth.Height, 1);
        }

        [Fact]
        public void Zero_width_does_not_wrap_to_one_grapheme_per_line ()
        {
            var g = Measurer ();
            using var font = new Font ("Arial", 12);

            var measured = g.MeasureString (Sentence, font, 0, (Majorsilence.Forms.Drawing.StringFormat?)null);

            // A sliver is the signature of the bug: wider than tall is enough to rule it out, since
            // one-grapheme-per-line for a 14-character string is many times taller than it is wide.
            Assert.True (measured.Width > measured.Height,
                $"expected a single line, got {measured.Width}x{measured.Height}");
        }

        [Fact]
        public void Zero_layout_area_measures_unbounded ()
        {
            var g = Measurer ();
            using var font = new Font ("Arial", 12);

            var unbounded = g.MeasureString (Sentence, font);
            var empty = g.MeasureString (Sentence, font, SizeF.Empty);

            Assert.Equal (unbounded.Width, empty.Width, 1);
        }

        [Fact]
        public void A_real_width_still_constrains_and_wraps ()
        {
            var g = Measurer ();
            using var font = new Font ("Arial", 12);

            var unbounded = g.MeasureString (Sentence, font);
            var narrow = g.MeasureString (Sentence, font, 40, (Majorsilence.Forms.Drawing.StringFormat?)null);

            Assert.True (narrow.Width < unbounded.Width, "a positive width must still constrain");
            Assert.True (narrow.Height > unbounded.Height, "constrained text must wrap to more lines");
        }
    }
}
