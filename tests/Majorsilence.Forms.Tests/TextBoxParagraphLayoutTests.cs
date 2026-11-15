using System;
using System.Drawing;
using System.Linq;
using SkiaSharp;
using Topten.RichTextKit;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #690: a text box lays its text out a paragraph at a time, so that an edit lays out only the paragraphs it touches. The
    // paragraphs have to add up to what one block over the whole text gives -- the box used to be one block, and every caret,
    // click, line query and pixel of it has to come out the same. These compare the two (TextBoxDocument.GetTextBlock still
    // builds the whole-text block), and count paragraphs laid out for what an edit costs.
    public class TextBoxParagraphLayoutTests
    {
        private static TextBox Box (string text, int width = 400, int height = 300, bool rich = false)
        {
            TextBox box = rich ? new RichTextBox () : new TextBox ();

            box.Multiline = true;
            box.Size = new Size (width, height);
            box.Text = text;

            return box;
        }

        private static string Lines (int count) => string.Join ("\r\n", Enumerable.Range (0, count).Select (i => $"line {i} some log text here and more of it"));

        // Texts that cut into paragraphs in different ways: each one has to lay out as the whole block does. (Right-to-left text is
        // the exception: a paragraph takes its reading order from its own first strong character, where the one block took the
        // whole text's from its first.)
        public static TheoryData<string, int> Texts => new () {
            { "aa\r\nbb\r\ncc\r\ndd", 400 },
            { "aa\r\nbb\r\ncc\r\ndd\r\n", 400 },                // ends in a line break: no line after it
            { "a\r\n\r\n\r\nb\r\n\r\n", 400 },                  // blank lines are lines
            { "\r\n", 400 },
            { "a\nb\nc", 400 },
            { "one\rtwo\r\nthree\rfour", 400 },                // a lone CR breaks a line inside its paragraph
            { "a\U0001F600b\r\nc\U0001F600\U0001F600d\r\ne", 400 },
            { "x" , 400 },
            { "a long paragraph that wraps over several lines in a narrow box, then another paragraph\r\nthat wraps too, and a short one\r\nok", 90 },
            { Lines (40), 120 },                                // every paragraph wraps
            { "tab\tseparated\r\n\tindented\r\nback to start", 400 },
        };

        private static void AssertSameAsOneBlock (TextBox box)
        {
            var document = box.document;
            var laid_out = document.GetLayout ();
            var whole = document.GetTextBlock ();
            var map = laid_out.IndexMap;

            Assert.Equal (whole.Lines.Count, laid_out.LineCount);
            Assert.Equal (whole.MeasuredHeight, laid_out.MeasuredHeight, 0.05);
            Assert.Equal (whole.MeasuredWidth, laid_out.MeasuredWidth, 0.05);

            var stops = whole.CaretIndicies;

            // Every line starts where it did.
            for (var line = 0; line < whole.Lines.Count; line++)
                Assert.Equal (whole.Lines[line].Start, laid_out.GetLineStart (line));

            Assert.Equal (-1, laid_out.GetLineStart (whole.Lines.Count));

            // Every code point: its caret, its line, and the step from it either way.
            var total = map.ToCodePoint (document.Text.Length);

            for (var code_point = 0; code_point <= total; code_point++) {
                var expected = whole.GetCaretInfo (new CaretPosition (code_point));
                var actual = laid_out.GetCaretInfo (code_point);

                Assert.True (Math.Abs (expected.CaretRectangle.Left - actual.CaretRectangle.Left) < 0.05
                             && Math.Abs (expected.CaretRectangle.Top - actual.CaretRectangle.Top) < 0.05
                             && Math.Abs (expected.CaretRectangle.Height - actual.CaretRectangle.Height) < 0.05,
                             $"caret at {code_point}: {expected.CaretRectangle} / {actual.CaretRectangle}");
                Assert.True (Math.Abs (expected.CaretXCoord - actual.CaretXCoord) < 0.05, $"caret x at {code_point}");
                Assert.Equal (expected.LineIndex, actual.LineIndex);
                Assert.Equal (Reference.Step (stops, code_point, 1), laid_out.StepCaret (code_point, 1));
                Assert.Equal (Reference.Step (stops, code_point, -1), laid_out.StepCaret (code_point, -1));
            }

            Assert.Equal (stops[0], laid_out.FirstStop);
            Assert.Equal (stops[stops.Count - 1], laid_out.LastStop);

            // Hit tests down each line, past the ends and above and below the text.
            var ys = whole.Lines.SelectMany (l => new[] { l.YCoord + 1, l.YCoord + l.Height / 2, l.YCoord + l.Height - 1 }).Concat (new[] { -20f, whole.MeasuredHeight + 20 });

            foreach (var y in ys)
                foreach (var x in new[] { -5f, 0f, 3f, 17f, 60f, 200f, 5000f })
                    Assert.True (whole.HitTest (x, y).ClosestCodePointIndex == laid_out.HitTest (x, y), $"hit test at {x}, {y}");
        }

        // What the old step did, over the block's own list of stops.
        private static class Reference
        {
            internal static int Step (System.Collections.Generic.IReadOnlyList<int> stops, int code_point, int step)
            {
                var at = -1;

                for (var i = 0; i < stops.Count && stops[i] <= code_point; i++)
                    at = i;

                var target = step < 0 ? (at >= 0 && stops[at] == code_point ? at - 1 : at) : at + 1;

                return stops[Math.Max (0, Math.Min (target, stops.Count - 1))];
            }
        }

        [Theory]
        [MemberData (nameof (Texts))]
        public void The_paragraphs_add_up_to_one_block (string text, int width)
        {
            using var box = Box (text, width);

            AssertSameAsOneBlock (box);
        }

        [Fact]
        public void The_paragraphs_add_up_to_one_block_for_rich_text_in_a_window_scale ()
        {
            using var box = Box (Lines (30), 200, rich: true);

            AssertSameAsOneBlock (box);
        }

        [Fact]
        public void A_colorizer_is_laid_out_over_the_paragraphs_as_over_the_whole_text ()
        {
            using var box = Box (Lines (30), 150);

            // A span that crosses paragraphs, one that is bold (so it is wider) and one at the very end.
            box.Colorizer = text => new[] {
                new TextSpanStyle (3, 80, SKColors.Red),
                new TextSpanStyle (120, 30, SKColors.Blue, Bold: true),
                new TextSpanStyle (text.Length - 4, 4, SKColors.Green, Bold: true),
            };

            AssertSameAsOneBlock (box);
        }

        [Fact]
        public void The_paragraphs_still_add_up_after_edits ()
        {
            using var box = Box (Lines (20), 150);

            AssertSameAsOneBlock (box);

            box.SelectionStart = 30;
            box.SelectedText = "inserted\r\nlines\r\n";      // splits a paragraph
            AssertSameAsOneBlock (box);

            box.Select (box.Text.IndexOf ("line 9", StringComparison.Ordinal) - 2, 4);
            box.SelectedText = string.Empty;                   // joins two
            AssertSameAsOneBlock (box);

            box.AppendText ("tail with no break");
            AssertSameAsOneBlock (box);

            box.Width = 90;                                    // everything wraps differently
            AssertSameAsOneBlock (box);
        }

        [Fact]
        public void An_edit_inside_a_paragraph_lays_out_that_paragraph_only ()
        {
            using var box = Box (Lines (2000), 600, 400);
            var document = box.document;

            box.GetPositionFromCharIndex (0);

            var before = document.ParagraphsLaidOut;
            box.SelectionStart = box.Text.IndexOf ("line 1000 ", StringComparison.Ordinal) + 5;
            box.SelectedText = "x";
            box.GetPositionFromCharIndex (0);

            Assert.Equal (1, document.ParagraphsLaidOut - before);

            // A paragraph split in two, and two joined into one.
            before = document.ParagraphsLaidOut;
            box.SelectedText = "\r\n";
            box.GetPositionFromCharIndex (0);
            Assert.Equal (2, document.ParagraphsLaidOut - before);

            before = document.ParagraphsLaidOut;
            box.Select (box.SelectionStart - 2, 2);
            box.SelectedText = string.Empty;
            box.GetPositionFromCharIndex (0);
            Assert.Equal (1, document.ParagraphsLaidOut - before);
        }

        [Fact]
        public void A_log_that_is_read_between_appends_lays_out_each_line_once ()
        {
            using var box = Box (string.Empty, 400, 100);
            var document = box.document;

            box.GetPositionFromCharIndex (0);

            var before = document.ParagraphsLaidOut;

            // The caret is read after each append, so the layout is rebuilt each time: it used to lay out every line so far.
            for (var i = 0; i < 400; i++) {
                box.AppendText ($"line {i} some log text here and more\r\n");
                box.GetPositionFromCharIndex (box.TextLength);
            }

            Assert.InRange (document.ParagraphsLaidOut - before, 400, 800);
        }

        [Fact]
        public void An_edit_in_a_colorized_box_lays_out_the_paragraphs_whose_spans_changed ()
        {
            using var box = Box (Lines (300), 600, 400);
            var document = box.document;

            // "/*" starts a comment that runs to the end: the paragraphs after it change colour, the ones before it do not.
            box.Colorizer = text => {
                var start = text.IndexOf ("/*", StringComparison.Ordinal);

                return start < 0 ? Array.Empty<TextSpanStyle> () : new[] { new TextSpanStyle (start, text.Length - start, SKColors.Green) };
            };
            box.GetPositionFromCharIndex (0);

            var before = document.ParagraphsLaidOut;
            box.SelectionStart = box.Text.IndexOf ("line 200 ", StringComparison.Ordinal);
            box.SelectedText = "/*";
            box.GetPositionFromCharIndex (0);

            // The paragraph with the "/*" and the 99 after it.
            Assert.Equal (100, document.ParagraphsLaidOut - before);

            AssertSameAsOneBlock (box);
        }

        [Fact]
        public void An_unrelated_edit_in_a_colorized_box_lays_out_one_paragraph ()
        {
            using var box = Box (Lines (300), 600, 400);
            var document = box.document;

            box.Colorizer = text => {
                var spans = new System.Collections.Generic.List<TextSpanStyle> ();

                for (var at = text.IndexOf ("some", StringComparison.Ordinal); at >= 0; at = text.IndexOf ("some", at + 4, StringComparison.Ordinal))
                    spans.Add (new TextSpanStyle (at, 4, SKColors.Red));

                return spans;
            };
            box.GetPositionFromCharIndex (0);

            var before = document.ParagraphsLaidOut;
            box.SelectionStart = box.Text.IndexOf ("line 150 ", StringComparison.Ordinal) + 3;
            box.SelectedText = "x";
            box.GetPositionFromCharIndex (0);

            Assert.Equal (1, document.ParagraphsLaidOut - before);
        }

        [Fact]
        public void Changing_what_the_text_is_laid_out_in_lays_every_paragraph_out_again ()
        {
            using var box = Box (Lines (50), 400);
            var document = box.document;

            box.GetPositionFromCharIndex (0);

            var before = document.ParagraphsLaidOut;
            box.Font = new Font (box.Font.FontFamily, box.Font.Size * 2);
            box.GetPositionFromCharIndex (0);

            Assert.True (document.ParagraphsLaidOut - before >= 50);
        }

        [Fact]
        public void The_whole_block_is_rebuilt_after_a_change ()
        {
            using var box = Box (Lines (50), 400);
            var document = box.document;
            var before = document.GetTextBlock ();

            Assert.Same (before, document.GetTextBlock ());

            box.AppendText ("\r\nmore");

            Assert.NotSame (before, document.GetTextBlock ());
            Assert.Equal (51, document.GetTextBlock ().Lines.Count);
        }

        // The paint: the paragraphs in view, each with the part of the selection that falls in it, drawn as the one block was.
        [Theory]
        [InlineData (0, 0, false)]
        [InlineData (10, 70, false)]
        [InlineData (70, 10, false)]
        [InlineData (0, 400, false)]
        [InlineData (10, 70, true)]
        public void The_paint_is_the_one_blocks (int from, int to, bool colorized)
        {
            using var box = Box (Lines (12), 250, 160);

            if (colorized)
                box.Colorizer = text => new[] { new TextSpanStyle (5, 60, SKColors.Red, Bold: true), new TextSpanStyle (100, 20, SKColors.Blue) };

            if (from != to)
                box.document.Select (Math.Min (from, to), Math.Abs (to - from));

            var document = box.document;
            var selection = document.GetTextSelection ();

            using var one = new SKBitmap (250, 160);
            using var many = new SKBitmap (250, 160);

            using (var canvas = new SKCanvas (one)) {
                canvas.Clear (SKColors.White);
                canvas.DrawTextBlock (document.GetTextBlock (), new Point (4, 3), selection);
            }

            using (var canvas = new SKCanvas (many)) {
                canvas.Clear (SKColors.White);
                document.GetLayout ().Paint (canvas, new Point (4, 3), selection);
            }

            var different = 0;

            for (var y = 0; y < 160; y++)
                for (var x = 0; x < 250; x++)
                    if (one.GetPixel (x, y) != many.GetPixel (x, y))
                        different++;

            Assert.True (different == 0, $"{different} pixels differ");
        }
    }
}
