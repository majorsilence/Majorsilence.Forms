using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;
using Topten.RichTextKit;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #683: setting a long Text on a multiline TextBox or RichTextBox took time quadratic in its length (800,000
    // characters, two minutes), because of how RichTextKit builds and breaks one long run. TextBlockAppender cuts long
    // text into runs it lays out linearly; these pin that mechanism.
    public class LongTextLayoutTests
    {
        private static string Xml (int length, string lineEnd = "\r\n")
        {
            var sb = new StringBuilder ();

            for (var i = 0; sb.Length < length; i++)
                sb.Append ("    <Textbox Name=\"T").Append (i).Append ("\"><Value>abc</Value><Style><FontSize>9pt</FontSize></Style></Textbox>").Append (lineEnd);

            return sb.ToString (0, length);
        }

        // One span per tag, as an XML colorizer gives: thousands of runs.
        private static IEnumerable<TextSpanStyle> Tags (string text)
        {
            for (var i = text.IndexOf ('<'); i >= 0; i = text.IndexOf ('<', i + 1)) {
                var end = text.IndexOf ('>', i);

                if (end < 0)
                    yield break;

                yield return new TextSpanStyle (i, end - i + 1, SKColors.Blue);
                i = end;
            }
        }

        [Fact]
        public void NoTwoParagraphsOfALongDocumentCanShareAShapingCall ()
        {
            // RichTextKit shapes adjacent runs with equal shaping properties as one font run, then splits that run at
            // every line, shifting everything after each split. A run per paragraph whose letter spacing differs
            // from its neighbours' keeps each paragraph to its own font run, whatever the font.
            var text = Xml (20_000, "\n");
            using var box = new TextBox { Multiline = true, Text = text };

            var runs = box.document.GetTextBlock ().StyleRuns;
            var paragraphs = text.Count (c => c == '\n') + 1;

            Assert.True (runs.Count >= paragraphs, $"{runs.Count} style runs for {paragraphs} paragraphs");

            for (var i = 1; i < runs.Count; i++) {
                if (text.IndexOf ('\n', runs[i - 1].Start, runs[i - 1].Length) >= 0)
                    Assert.NotEqual (runs[i - 1].Style.LetterSpacing, runs[i].Style.LetterSpacing);
            }
        }

        [Fact]
        public void EachLineOfALongDocumentLaysOutAsItWouldAlone ()
        {
            var text = Xml (12_000, "\n");
            var font = Theme.UIFont;
            var unbounded = new System.Drawing.Size (int.MaxValue, int.MaxValue);
            var block = TextMeasurer.CreateTextBlock (text, font, 13, unbounded, TextAlignment.Left, SKColors.Black);
            var lines = text.Split ('\n');

            Assert.Equal (lines.Length, block.Lines.Count);

            for (var i = 0; i < lines.Length - 1; i += 13) {
                var alone = TextMeasurer.CreateTextBlock (lines[i], font, 13, unbounded, TextAlignment.Left, SKColors.Black);
                var start = block.Lines[i].Start;

                foreach (var offset in new[] { 5, 40, lines[i].Length }) {
                    var expected = alone.GetCaretInfo (new CaretPosition (offset)).CaretRectangle;
                    var actual = block.GetCaretInfo (new CaretPosition (start + offset)).CaretRectangle;

                    Assert.Equal (expected.Left, actual.Left, 0.01f);
                    Assert.Equal (expected.Height, actual.Height, 0.01f);
                }
            }
        }

        [Fact]
        public void ColorizedLayoutAllocatesInProportionToTheText ()
        {
            // RichTextKit grows its buffers to exactly the length asked for, so every run appended, and every
            // shaping call, copied everything before it: four times the text was sixteen times the bytes.
            var small = AllocatedLayingOut (16_000);
            var large = AllocatedLayingOut (64_000);

            Assert.True (large < small * 6, $"16k chars: {small:N0} bytes; 64k chars: {large:N0} bytes");
        }

        private static long AllocatedLayingOut (int length)
        {
            var text = Xml (length);
            using var box = new TextBox { Multiline = true, Text = text };

            box.Colorizer = Tags;

            var before = GC.GetAllocatedBytesForCurrentThread ();
            _ = box.document.GetTextBlock ().Lines;

            return GC.GetAllocatedBytesForCurrentThread () - before;
        }

        [Fact]
        public void EditingATextBoxKeepsNoLayoutInTheSharedCache ()
        {
            // The document keeps its own block. In the shared cache, every edit's whole-document layout was a new
            // entry kept alive: 18 MB a keystroke in a 400,000-character document.
            using var box = new TextBox { Multiline = true, Text = Xml (8_000) };
            _ = box.document.GetTextBlock ().Lines;

            var before = TextMeasurer.CachedLayoutCount;

            box.SelectionStart = 4_000;

            for (var i = 0; i < 10; i++) {
                box.document.InsertText ("x");
                _ = box.document.GetTextBlock ().Lines;
            }

            Assert.Equal (before, TextMeasurer.CachedLayoutCount);
        }

        [Fact]
        public void RichTextKitsShapingBuffersCanBeSizedUpFront ()
        {
            // They are private to TextBlock and reached by name. If a RichTextKit upgrade moves them, layout still
            // works, but every segment's shaping call copies all the glyphs before it again: quadratic once more.
            Assert.True (TextBlockAppender.ShapingBuffersFound,
                "RichTextKit's TextBlock._textShapingBuffers (a TextShaper.ResultBufferSet) or one of its buffers moved. " +
                "On RichTextKit 0.6.171 or later, delete the reflection hack in TextBlockAppender (ShapingBuffers and " +
                "PresizedTextBlock) and this test; only on an older one update the names.");
        }

        [Fact]
        public void ShortTextIsAppendedExactlyAsGiven ()
        {
            // Every label and button: one run, the style it was given, nothing segmented.
            var text = "first line\nsecond line\nthird";
            var block = TextBlockAppender.CreateBlock (text.Length);
            var style = new Style ();

            new TextBlockAppender (block, text).Add (text.AsSpan (), style);

            Assert.Same (style, Assert.Single (block.StyleRuns).Style);
        }

        [Fact]
        public void SegmentsStartAfterLineEndsButNeverInsideACrLf ()
        {
            var text = "ab\r\ncd\ref\ngh";

            Assert.Equal ([4, 7, 10], TextBlockAppender.FindBoundaries (text));
        }

        [Fact]
        public void ALongLineIsCutAfterASpaceAndNotBeforeACombiningMark ()
        {
            var word = new string ('x', TextBlockAppender.MaxSegmentLength);
            var text = word + " \u0301y " + new string ('x', 100) + " z";

            Assert.Equal ([word.Length + 4], TextBlockAppender.FindBoundaries (text));
        }

        [Fact]
        public void FontFallbackAsksThePlatformOncePerCharacter ()
        {
            var inner = new CountingMatcher ();
            var matcher = new CachingCharacterMatcher (inner);

            for (var i = 0; i < 100; i++)
                matcher.MatchCharacter ("Sans", 400, 5, SKFontStyleSlant.Upright, null, '\n');

            matcher.MatchCharacter ("Sans", 700, 5, SKFontStyleSlant.Upright, null, '\n');

            Assert.Equal (2, inner.Calls);
        }

        private sealed class CountingMatcher : ICharacterMatcher
        {
            internal int Calls;

            public SKTypeface? MatchCharacter (string familyName, int weight, int width, SKFontStyleSlant slant, string[]? bcp47, int character)
            {
                Calls++;
                return null;
            }
        }
    }
}
