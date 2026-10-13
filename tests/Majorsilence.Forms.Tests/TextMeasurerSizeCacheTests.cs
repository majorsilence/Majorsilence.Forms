using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    // Performance round 3. MeasureText keeps only the size it measured, not the TextBlock: holding every measured
    // string's layout made the garbage collector promote it, a third of the time of a report measuring new values.
    // The caches are lock-free, and a cached Graphics.MeasureString no longer copies text that has spaces only
    // between words.
    [Collection ("Headless")]
    public class TextMeasurerSizeCacheTests
    {
        private static readonly string[] texts = [
            "Customer number 4821",
            "Quarterly results for the northern region, adjusted for seasonality",
            "Two\nlines",
            "Ends with a space ",
            "x",
        ];

        [Fact]
        public void Measuring_keeps_the_size_and_not_the_layout ()
        {
            HeadlessRenderer.Use ();
            TextMeasurer.ClearTextBlockCache ();

            for (var i = 0; i < 20; i++)
                TextMeasurer.MeasureText ($"Value {i}", SKTypeface.Default, 12);

            Assert.Equal (0, TextMeasurer.CachedLayoutCount);
            Assert.Equal (20, TextMeasurer.CachedBlockCount);
        }

        [Theory]
        [InlineData (1000, 1000)]
        [InlineData (60, 1000)]
        [InlineData (int.MaxValue, int.MaxValue)]
        public void A_measurement_is_the_size_of_the_block_for_the_same_text (int width, int height)
        {
            HeadlessRenderer.Use ();
            var size = new Size (width, height);

            foreach (var text in texts) {
                TextMeasurer.ClearTextBlockCache ();
                var block = TextMeasurer.CreateTextBlock (text, SKTypeface.Default, 12, size);

                for (var i = 0; i < 2; i++) {   // the second time from the cache
                    var measured = TextMeasurer.MeasureText (text, SKTypeface.Default, 12, size);
                    Assert.Equal (block.MeasuredWidth, measured.Width);
                    Assert.Equal (block.MeasuredHeight, measured.Height);
                }
            }
        }

        [Fact]
        public void Threads_measuring_at_once_each_get_the_single_threaded_answer ()
        {
            HeadlessRenderer.Use ();
            TextMeasurer.ClearTextBlockCache ();

            // Enough distinct strings to swap generations several times while the threads run.
            var all = new List<string> ();
            for (var i = 0; i < 3000; i++)
                all.Add ($"{texts[i % texts.Length]} {i}");

            var expected = new Dictionary<string, SKSize> ();
            foreach (var text in all)
                expected[text] = TextMeasurer.MeasureText (text, SKTypeface.Default, 12);
            TextMeasurer.ClearTextBlockCache ();

            var wrong = new ConcurrentBag<string> ();
            Parallel.For (0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, t => {
                for (var i = 0; i < all.Count; i++) {
                    var text = all[(i * 7 + t * 401) % all.Count];
                    if (TextMeasurer.MeasureText (text, SKTypeface.Default, 12) != expected[text])
                        wrong.Add (text);
                }
            });

            Assert.Empty (wrong);
            Assert.InRange (TextMeasurer.CachedBlockCount, 1, 2000 + 8);
        }

        [Theory]
        [InlineData ("Customer number 4821")]
        [InlineData ("a  b")]
        [InlineData ("trailing ")]
        [InlineData ("line one \nline two")]
        [InlineData ("line one  \r\nline two  ")]
        [InlineData (" leading")]
        [InlineData ("a much longer first line     \nb")]
        public void MeasureString_leaves_out_only_the_spaces_that_end_a_line (string text)
        {
            HeadlessRenderer.Use ();
            using var bitmap = new Drawing.Bitmap (10, 10);
            using var g = Drawing.Graphics.FromImage (bitmap);
            using var font = new Drawing.Font ("Arial", 10f);

            var trimmed = System.Text.RegularExpressions.Regex.Replace (text, " +(?=\r|\n|$)", "");

            Assert.Equal (g.MeasureString (trimmed, font), g.MeasureString (text, font));
        }

        [Fact]
        public void A_cached_MeasureString_of_words_allocates_nothing ()
        {
            HeadlessRenderer.Use ();
            using var bitmap = new Drawing.Bitmap (10, 10);
            using var g = Drawing.Graphics.FromImage (bitmap);
            using var font = new Drawing.Font ("Arial", 10f);
            const string text = "Quarterly results for the northern region";

            for (var i = 0; i < 1000; i++)
                g.MeasureString (text, font);

            var before = GC.GetAllocatedBytesForCurrentThread ();
            for (var i = 0; i < 100; i++)
                g.MeasureString (text, font);

            Assert.Equal (0, GC.GetAllocatedBytesForCurrentThread () - before);
        }
    }
}
