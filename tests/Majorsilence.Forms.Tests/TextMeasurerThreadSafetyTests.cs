using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TextMeasurer keeps laid-out text in one static cache. A report engine measures text on whatever
    // thread renders the report, so the cache has to survive concurrent use -- including the point
    // where it fills up and is cleared. Before the cache was locked, this threw "Operations that change
    // non-concurrent collections must have exclusive access".
    [Collection ("Headless")]
    public class TextMeasurerThreadSafetyTests
    {
        [Fact]
        public void MeasureText_FromManyThreads_KeepsTheCacheIntact ()
        {
            HeadlessRenderer.Use ();
            var typeface = SKTypeface.Default;
            var errors = new ConcurrentBag<Exception> ();

            // 16 x 600 distinct strings is well past the cache limit, so it is cleared several times.
            Parallel.For (0, 16, t => {
                try {
                    for (var i = 0; i < 600; i++) {
                        var size = TextMeasurer.MeasureText ($"text {t} {i}", typeface, 12, new Size (300, 100));
                        Assert.True (size.Width > 0);
                    }
                } catch (Exception ex) {
                    errors.Add (ex);
                }
            });

            Assert.Empty (errors);
        }

        [Fact]
        public void MeasureText_SameText_GivesTheSameSizeOnEveryThread ()
        {
            HeadlessRenderer.Use ();
            var typeface = SKTypeface.Default;
            var expected = TextMeasurer.MeasureText ("The quick brown fox", typeface, 12, new Size (200, 100));
            var wrong = new ConcurrentBag<SKSize> ();

            Parallel.For (0, 16, _ => {
                for (var i = 0; i < 200; i++) {
                    var size = TextMeasurer.MeasureText ("The quick brown fox", typeface, 12, new Size (200, 100));
                    if (size != expected)
                        wrong.Add (size);
                }
            });

            Assert.Empty (wrong);
        }
    }
}
