using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The TextBlock cache keeps text that is in use while a stream of strings measured once passes through it
    // (performance plan F6(b)). It used to empty itself completely every 2000 new strings, so a grid's headers or
    // a report's repeated values were laid out again each time the stream filled it.
    [Collection ("Headless")]
    public class TextMeasurerCacheEvictionTests
    {
        [Fact]
        public void Text_in_use_keeps_its_block_through_a_stream_of_new_strings ()
        {
            HeadlessRenderer.Use ();
            TextMeasurer.ClearTextBlockCache ();
            var typeface = SKTypeface.Default;
            var size = new Size (300, 100);

            var first = TextMeasurer.CreateTextBlock ("Column header", typeface, 12, size);

            for (var i = 0; i < 6000; i++) {
                TextMeasurer.CreateTextBlock ($"Row value {i}", typeface, 12, size);

                if (i % 500 == 0)
                    Assert.Same (first, TextMeasurer.CreateTextBlock ("Column header", typeface, 12, size));
            }
        }

        [Fact]
        public void The_cache_stays_bounded ()
        {
            HeadlessRenderer.Use ();
            TextMeasurer.ClearTextBlockCache ();
            var typeface = SKTypeface.Default;

            for (var i = 0; i < 5000; i++) {
                TextMeasurer.MeasureText ($"Value {i}", typeface, 12);
                Assert.InRange (TextMeasurer.CachedBlockCount, 1, 2000);
            }
        }

        [Fact]
        public void Text_not_used_again_is_dropped ()
        {
            HeadlessRenderer.Use ();
            TextMeasurer.ClearTextBlockCache ();
            var typeface = SKTypeface.Default;
            var size = new Size (300, 100);

            var once = TextMeasurer.CreateTextBlock ("Seen once", typeface, 12, size);
            for (var i = 0; i < 2500; i++)
                TextMeasurer.CreateTextBlock ($"Row value {i}", typeface, 12, size);

            Assert.NotSame (once, TextMeasurer.CreateTextBlock ("Seen once", typeface, 12, size));
        }

        [Theory]
        [InlineData ("one\ntwo")]
        [InlineData ("one\rtwo")]
        [InlineData ("one\n\rtwo")]
        public void A_single_line_block_shows_line_breaks_as_a_mark (string text)
        {
            HeadlessRenderer.Use ();
            var block = TextMeasurer.CreateTextBlock (text, SKTypeface.Default, 12, new Size (300, 30), maxLines: 1);
            var marked = TextMeasurer.CreateTextBlock ("one»two", SKTypeface.Default, 12, new Size (300, 30), maxLines: 1);

            Assert.Equal (1, block.Lines.Count);
            Assert.Equal (marked.MeasuredWidth, block.MeasuredWidth);
        }
    }
}
