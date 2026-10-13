using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Tests
{
    // TextMeasurer hands the same cached TextBlock to every caller with the same text, font and colour, and
    // RichTextKit builds each run's SKTextBlob lazily, on the block's first Paint. A report engine draws on
    // several threads at once (one bitmap per thread), so two threads can paint one block for the first time
    // together. This checks that doing so neither throws nor draws anything different from a block painted
    // on one thread (performance plan F6(d)).
    [Collection ("Headless")]
    public class TextBlockConcurrentPaintTests
    {
        private const int Threads = 8;
        private const int Rounds = 40;
        private static readonly Rectangle bounds = new (0, 0, 300, 60);

        [Fact]
        public void A_cached_block_painted_for_the_first_time_on_many_threads_draws_the_same_pixels ()
        {
            HeadlessRenderer.Use ();
            var typeface = SKTypeface.Default;
            var errors = new ConcurrentBag<Exception> ();
            var mismatches = new ConcurrentBag<string> ();

            for (var round = 0; round < Rounds; round++) {
                // New text every round, so the block is laid out once and its blobs are built by whichever
                // threads get to Paint first.
                var text = $"Round {round}: The quick brown fox jumps over the lazy dog";
                var drawn = new byte[Threads][];

                using (var start = new Barrier (Threads)) {
                    Parallel.For (0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, t => {
                        try {
                            start.SignalAndWait ();
                            drawn[t] = Render (text, typeface);
                        } catch (Exception ex) {
                            errors.Add (ex);
                        }
                    });
                }

                // The reference comes from a fresh block painted on this thread alone.
                TextMeasurer.ClearTextBlockCache ();
                var expected = Render (text, typeface);

                for (var t = 0; t < Threads; t++) {
                    if (drawn[t] is not null && !drawn[t].AsSpan ().SequenceEqual (expected))
                        mismatches.Add ($"round {round}, thread {t}");
                }
            }

            Assert.Empty (errors);
            Assert.Empty (mismatches);
        }

        private static byte[] Render (string text, SKTypeface typeface)
        {
            using var bitmap = new SKBitmap (bounds.Width, bounds.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Clear (SKColors.White);
                SkiaTextExtensions.DrawText (canvas, text, typeface, 14, bounds, SKColors.Black, ContentAlignment.TopLeft,
                    -1, -1, null, null, false, null);
            }
            return bitmap.Bytes;
        }
    }
}
