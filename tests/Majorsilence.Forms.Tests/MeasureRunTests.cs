using System;
using Xunit;
using RectangleF = System.Drawing.RectangleF;
using CharacterRange = Majorsilence.Forms.Drawing.CharacterRange;
using MSStringFormat = Majorsilence.Forms.Drawing.StringFormat;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// <c>MeasureRunWidth</c> and <c>MeasureRunSpans</c> exist so a caller that measures many single-line strings
    /// (a report engine, once per cell) does not pay for a Region per range. They must give the numbers
    /// <c>MeasureCharacterRanges</c> gives, so these tests compare the two over a corpus, at several resolutions.
    /// </summary>
    public class MeasureRunTests
    {
        private static Graphics At (float dpi)
        {
            var bitmap = new Bitmap (800, 200);
            bitmap.SetResolution (dpi, dpi);
            return Graphics.FromImage (bitmap);
        }

        private static RectangleF[] ViaRegions (Graphics g, string text, Font font, CharacterRange[] ranges)
        {
            var format = new MSStringFormat ();
            format.SetMeasurableCharacterRanges (ranges);
            var regions = g.MeasureCharacterRanges (text, font, new RectangleF (0, 0, float.MaxValue, float.MaxValue), format);
            var bounds = new RectangleF[regions.Length];
            for (var i = 0; i < regions.Length; i++)
                bounds[i] = regions[i].GetBounds (g);
            return bounds;
        }

        private static readonly string[] Texts =
        {
            "Hello", "Quarterly results for the region", "  leading and trailing  ", "a", "WWWW iiii", "Total: 1,234.50",
            "café crème", "Dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor",
        };

        [Theory]
        [InlineData (96f)]
        [InlineData (300f)]
        [InlineData (72f)]
        public void Whole_text_width_matches_MeasureCharacterRanges (float dpi)
        {
            using var g = At (dpi);
            foreach (var family in new[] { "Arial", "Times New Roman", "Courier New" }) {
                using var font = new Font (family, 11f);
                foreach (var text in Texts) {
                    var viaRegions = ViaRegions (g, text, font, new[] { new CharacterRange (0, text.Length) })[0];
                    Assert.Equal (viaRegions.Width, g.MeasureRunWidth (text, font));
                }
            }
        }

        [Theory]
        [InlineData (96f)]
        [InlineData (300f)]
        public void Word_ranges_match_MeasureCharacterRanges (float dpi)
        {
            using var g = At (dpi);
            using var font = new Font ("Arial", 10.5f);
            foreach (var text in Texts) {
                // one range per word, the way a report engine asks
                var ranges = new System.Collections.Generic.List<CharacterRange> ();
                var wordStart = 0;
                for (var i = 0; i <= text.Length; i++) {
                    if (i == text.Length || text[i] == ' ') {
                        if (i > wordStart)
                            ranges.Add (new CharacterRange (wordStart, i - wordStart));
                        wordStart = i + 1;
                    }
                }

                var expected = ViaRegions (g, text, font, ranges.ToArray ());
                var actual = new (float Left, float Right)[ranges.Count];
                g.MeasureRunSpans (text, font, ranges.ToArray (), actual);

                for (var i = 0; i < ranges.Count; i++) {
                    Assert.Equal (expected[i].Left, actual[i].Left);
                    Assert.Equal (expected[i].Right, actual[i].Right);
                }
            }
        }

        [Fact]
        public void Empty_and_out_of_range_ranges_give_zero ()
        {
            using var g = At (96f);
            using var font = new Font ("Arial", 12f);
            var ranges = new[] { new CharacterRange (0, 0), new CharacterRange (50, 3), new CharacterRange (-5, 2) };
            var actual = new (float Left, float Right)[3];

            g.MeasureRunSpans ("abc", font, ranges, actual);

            Assert.All (actual, a => Assert.Equal ((0f, 0f), a));
            Assert.Equal (0f, g.MeasureRunWidth ("", font));
        }

        [Fact]
        public void A_result_that_is_too_short_throws ()
        {
            using var g = At (96f);
            using var font = new Font ("Arial", 12f);
            Assert.Throws<ArgumentException> (() =>
                g.MeasureRunSpans ("abc", font, new[] { new CharacterRange (0, 1), new CharacterRange (1, 1) }, new (float, float)[1]));
        }
    }
}
