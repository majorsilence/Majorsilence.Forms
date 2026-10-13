using System;
using System.Collections.Generic;
using Xunit;
using CharacterRange = Majorsilence.Forms.Drawing.CharacterRange;
using MSRegion = Majorsilence.Forms.Drawing.Region;
using MSStringFormat = Majorsilence.Forms.Drawing.StringFormat;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Tests
{
    // MeasureCharacterRanges used to build each range's region as the empty region unioned with one rectangle per line;
    // it now starts from the first rectangle (performance plan F5). The region must be the same: the old construction
    // is kept here as the oracle and compared on bounds, emptiness and scans, for single- and multi-line ranges, at
    // whole-pixel and fractional origins (an integral region and a path take different routes through Region).
    public class MeasureCharacterRangesRegionParityTests
    {
        private static readonly string[] texts = [
            "Customer number 4821",
            "Quarterly results for the northern region, adjusted for seasonality and one-off items",
            "First paragraph\nSecond, rather longer paragraph that wraps\r\nthird",
            "a",
            "  spaced  out  ",
        ];

        [Theory]
        [InlineData (0f, 0f, 0f)]           // no wrapping, integral origin
        [InlineData (0f, 0f, 120f)]         // wrapping
        [InlineData (0.3f, 0.7f, 0f)]       // fractional origin
        [InlineData (10.25f, 3.5f, 90f)]    // fractional and wrapping
        public void Regions_match_the_empty_region_unioned_with_each_line (float x, float y, float width)
        {
            using var bitmap = new Bitmap (800, 400);
            using var g = Graphics.FromImage (bitmap);   // 96 DPI: the Font's own SKFont is the one measured with

            foreach (var family in new[] { "Arial", "Times New Roman" }) {
                using var font = new Font (family, 11f);
                var runFont = font.GetRunFont (font.GetSKFont ());
                var metrics = font.GetSKFont ().Metrics;
                var lineHeight = metrics.Descent - metrics.Ascent;
                var layout = new RectangleF (x, y, width, 400);

                foreach (var text in texts) {
                    var ranges = Ranges (text);
                    var format = new MSStringFormat ();
                    format.SetMeasurableCharacterRanges (ranges);
                    var actual = g.MeasureCharacterRanges (text, font, layout, format);
                    var lines = Graphics.LayoutLines (text, runFont, layout.Width);

                    for (var i = 0; i < ranges.Length; i++) {
                        using var expected = new MSRegion ();
                        expected.MakeEmpty ();
                        var rangeStart = ranges[i].First;
                        var rangeEnd = rangeStart + ranges[i].Length;
                        foreach (var (start, length, index) in lines) {
                            var segStart = Math.Max (rangeStart, start);
                            var segEnd = Math.Min (rangeEnd, start + length);
                            if (segEnd <= segStart)
                                continue;
                            var left = layout.X + runFont.Measure (text.AsSpan (start, segStart - start));
                            var segWidth = runFont.Measure (text.AsSpan (segStart, segEnd - segStart));
                            expected.Union (new RectangleF (left, layout.Y + index * lineHeight, segWidth, lineHeight));
                        }

                        var what = $"{family} \"{text}\" range {ranges[i].First}+{ranges[i].Length} in {layout}";
                        Assert.True (expected.GetBounds (g) == actual[i].GetBounds (g), $"{what}: bounds {actual[i].GetBounds (g)}, expected {expected.GetBounds (g)}");
                        Assert.True (expected.IsEmpty (g) == actual[i].IsEmpty (g), $"{what}: emptiness");
                        Assert.Equal (expected.GetRegionScans (null), actual[i].GetRegionScans (null));
                        actual[i].Dispose ();
                    }
                }
            }
        }

        // MeasureCharacterRanges lays lines out into a list kept per thread; threads measuring at once must each get
        // their own answer.
        [Fact]
        public void Measuring_on_many_threads_at_once_gives_each_the_same_regions ()
        {
            var layout = new RectangleF (0, 0, 120, 400);
            var expected = new Dictionary<string, RectangleF[]> ();
            using (var bitmap = new Bitmap (400, 400))
            using (var g = Graphics.FromImage (bitmap))
            using (var font = new Font ("Arial", 11f)) {
                foreach (var text in texts)
                    expected[text] = Bounds (g, font, text, layout);
            }

            var wrong = new System.Collections.Concurrent.ConcurrentBag<string> ();
            System.Threading.Tasks.Parallel.For (0, 8, t => {
                using var bitmap = new Bitmap (400, 400);
                using var g = Graphics.FromImage (bitmap);
                using var font = new Font ("Arial", 11f);
                for (var i = 0; i < 200; i++) {
                    var text = texts[(t + i) % texts.Length];
                    if (!Bounds (g, font, text, layout).AsSpan ().SequenceEqual (expected[text]))
                        wrong.Add (text);
                }
            });

            Assert.Empty (wrong);
        }

        private static RectangleF[] Bounds (Graphics g, Font font, string text, RectangleF layout)
        {
            var format = new MSStringFormat ();
            format.SetMeasurableCharacterRanges (Ranges (text));
            var regions = g.MeasureCharacterRanges (text, font, layout, format);
            var bounds = new RectangleF[regions.Length];
            for (var i = 0; i < regions.Length; i++) {
                bounds[i] = regions[i].GetBounds (g);
                regions[i].Dispose ();
            }
            return bounds;
        }

        // The whole text, each word, each character pair, and ranges that are empty or fall outside the text.
        private static CharacterRange[] Ranges (string text)
        {
            var ranges = new List<CharacterRange> { new (0, text.Length), new (0, 0), new (text.Length, 3), new (-2, 1) };
            var start = 0;
            for (var i = 0; i <= text.Length; i++) {
                if (i == text.Length || text[i] == ' ' || text[i] == '\n') {
                    if (i > start)
                        ranges.Add (new CharacterRange (start, i - start));
                    start = i + 1;
                }
            }
            for (var i = 0; i + 2 <= text.Length; i += 7)
                ranges.Add (new CharacterRange (i, 2));
            return ranges.ToArray ();
        }
    }
}
