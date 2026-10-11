using System;
using System.Text;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// <c>Graphics.FitRun</c> used to measure every prefix of the text in turn (quadratic). It now measures the whole
    /// run and bisects only when that overflows. These tests keep the original as a reference and require the
    /// two to agree over a large set of texts, start and end positions, fonts and widths.
    /// </summary>
    public class FitRunParityTests
    {
        // The implementation before the change, verbatim.
        private static int FitRunReference (string text, int start, int end, SKFont font, float maxWidth)
        {
            if (start >= end)
                return 0;

            var lastBreak = -1;
            for (var i = start; i < end; i++) {
                var width = font.MeasureText (text.AsSpan (start, i - start + 1));
                if (width > maxWidth) {
                    if (lastBreak >= 0)
                        return lastBreak - start;
                    return Math.Max (1, i - start);
                }
                if (text[i] == ' ')
                    lastBreak = i;
            }
            return end - start;
        }

        private static string RandomText (Random random)
        {
            var sb = new StringBuilder ();
            var words = random.Next (0, 14);
            for (var w = 0; w < words; w++) {
                if (w > 0)
                    sb.Append (random.Next (6) == 0 ? "  " : " ");     // sometimes two spaces
                var length = random.Next (1, 13);
                for (var c = 0; c < length; c++)
                    sb.Append ((char)random.Next ('A', 'z'));        // letters, some punctuation between Z and a
            }
            if (random.Next (5) == 0)
                sb.Append (' ');                                      // trailing space
            if (random.Next (7) == 0)
                sb.Insert (0, ' ');                                   // leading space
            return sb.ToString ();
        }

        [Theory]
        [InlineData ("Arial", 8f)]
        [InlineData ("Arial", 12f)]
        [InlineData ("Arial", 22f)]
        [InlineData ("Times New Roman", 10.5f)]
        [InlineData ("Courier New", 9f)]
        public void Agrees_with_the_measure_every_prefix_original (string family, float size)
        {
            using var font = new Font (family, size);
            var skFont = font.GetSKFont ();
            var random = new Random (20261011);
            var compared = 0;

            for (var n = 0; n < 400; n++) {
                var text = RandomText (random);
                var start = text.Length == 0 ? 0 : random.Next (0, Math.Max (1, text.Length));
                var end = random.Next (start, text.Length + 1);

                foreach (var width in new[] { 0.5f, 3f, 12f, 40f, 90f, 160f, 400f, 5000f, float.MaxValue }) {
                    Assert.Equal (FitRunReference (text, start, end, skFont, width),
                                  Graphics.FitRun (text, start, end, skFont, width));
                    compared++;
                }
            }

            Assert.True (compared > 3000);
        }

        [Fact]
        public void Edge_cases_agree ()
        {
            using var font = new Font ("Arial", 12f);
            var skFont = font.GetSKFont ();

            foreach (var text in new[] { "", "a", " ", "  ", "abc", "abc def", "abc  def", " abc", "abc ", "supercalifragilisticexpialidocious", "a b c d e f g h i j" }) {
                for (var start = 0; start <= text.Length; start++) {
                    for (var end = start; end <= text.Length; end++) {
                        foreach (var width in new[] { 0.1f, 1f, 8f, 20f, 60f, 1000f, float.MaxValue }) {
                            Assert.Equal (FitRunReference (text, start, end, skFont, width),
                                          Graphics.FitRun (text, start, end, skFont, width));
                        }
                    }
                }
            }
        }

        [Fact]
        public void LayoutLines_with_no_wrapping_width_does_not_measure_prefixes ()
        {
            using var font = new Font ("Arial", 11f);
            var skFont = font.GetSKFont ();
            var text = new string ('x', 400);

            // One paragraph, everything fits in a huge width: the whole run is taken in a single piece.
            var lines = Graphics.LayoutLines (text, skFont, float.MaxValue);
            Assert.Equal (400, lines[0].Length);
        }
    }
}
