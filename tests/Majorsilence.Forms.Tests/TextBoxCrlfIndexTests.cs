using System.Drawing;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #689: the document keeps its text in UTF-16 indices, with its CRLFs; the RichTextKit block it lays out
    // counts code points, and folds each CRLF (and each surrogate pair) into one. Every place the two met
    // handed an index across unconverted, so a caret, a click or a line query drifted one character per CRLF
    // above it. These compare a CRLF text to the same text with bare LFs, whose indices need no conversion:
    // the two must agree on where each line starts and where each character is drawn.
    public class TextBoxCrlfIndexTests
    {
        private const string Crlf = "aa\r\nbb\r\ncc\r\ndd";
        private const string Lf = "aa\nbb\ncc\ndd";

        private static TextBox Box (string text, bool rich = false)
        {
            TextBox box = rich ? new RichTextBox () : new TextBox ();

            box.Multiline = true;
            box.Size = new Size (400, 300);
            box.Text = text;

            return box;
        }

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void Each_line_starts_where_its_text_does (bool rich)
        {
            using var box = Box (Crlf, rich);

            Assert.Equal (0, box.GetFirstCharIndexFromLine (0));
            Assert.Equal (4, box.GetFirstCharIndexFromLine (1));
            Assert.Equal (8, box.GetFirstCharIndexFromLine (2));
            Assert.Equal (12, box.GetFirstCharIndexFromLine (3));
            Assert.Equal (-1, box.GetFirstCharIndexFromLine (4));
        }

        [Fact]
        public void A_character_is_on_the_line_it_is_in ()
        {
            using var box = Box (Crlf);

            // Both halves of a CRLF are on the line it ends.
            var expected = new[] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3 };

            for (var i = 0; i < expected.Length; i++)
                Assert.True (expected[i] == box.GetLineFromCharIndex (i), $"index {i}");
        }

        [Fact]
        public void A_caret_is_drawn_where_it_would_be_with_bare_line_feeds ()
        {
            using var crlf = Box (Crlf);
            using var lf = Box (Lf);

            // Index in the CRLF text -> the same character's index in the LF text.
            foreach (var (c, l) in new[] { (0, 0), (1, 1), (4, 3), (5, 4), (8, 6), (9, 7), (12, 9), (13, 10), (14, 11) })
                Assert.True (lf.GetPositionFromCharIndex (l) == crlf.GetPositionFromCharIndex (c),
                    $"CRLF index {c}: {crlf.GetPositionFromCharIndex (c)}, LF index {l}: {lf.GetPositionFromCharIndex (l)}");
        }

        [Fact]
        public void A_click_lands_on_the_character_the_caret_was_drawn_at ()
        {
            using var box = Box (Crlf);

            foreach (var index in new[] { 0, 1, 4, 5, 8, 9, 12, 13 })
                Assert.True (index == box.GetCharIndexFromPosition (box.GetPositionFromCharIndex (index)), $"index {index}");
        }

        [Fact]
        public void A_surrogate_pair_is_one_caret_stop ()
        {
            using var box = Box ("a\U0001F600b\U0001F600c");

            // Caret stops are 0, 1, 3, 4, 6, 7: the middle of a pair is not one.
            foreach (var index in new[] { 0, 1, 3, 4, 6, 7 })
                Assert.True (index == box.GetCharIndexFromPosition (box.GetPositionFromCharIndex (index)), $"index {index}");
        }

        [Fact]
        public void The_caret_steps_over_a_CRLF_as_one_character ()
        {
            using var box = Box (Crlf);
            var document = box.document;

            document.SetCursorToCharIndex (1);
            document.MoveCursor (ArrowDirection.Right, select: false, wholeWord: false, end: false);
            Assert.Equal (2, document.CursorIndex);
            document.MoveCursor (ArrowDirection.Right, select: false, wholeWord: false, end: false);
            Assert.Equal (4, document.CursorIndex);   // over the CRLF, not onto its LF
            document.MoveCursor (ArrowDirection.Left, select: false, wholeWord: false, end: false);
            Assert.Equal (2, document.CursorIndex);
        }

        [Fact]
        public void Down_and_Up_keep_the_column_across_CRLF_lines ()
        {
            using var box = Box (Crlf);
            var document = box.document;

            document.SetCursorToCharIndex (1);   // a|a  column 1 on line 0
            document.MoveCursor (ArrowDirection.Down, select: false, wholeWord: false, end: false);
            Assert.Equal (5, document.CursorIndex);
            document.MoveCursor (ArrowDirection.Down, select: false, wholeWord: false, end: false);
            Assert.Equal (9, document.CursorIndex);
            document.MoveCursor (ArrowDirection.Up, select: false, wholeWord: false, end: false);
            Assert.Equal (5, document.CursorIndex);
        }

        [Fact]
        public void Home_and_End_stay_on_the_line_of_a_CRLF_text ()
        {
            using var box = Box (Crlf);
            var document = box.document;

            document.SetCursorToCharIndex (9);   // c|c
            document.MoveCursor (ArrowDirection.Left, select: false, wholeWord: false, end: true);
            Assert.Equal (8, document.CursorIndex);
            document.MoveCursor (ArrowDirection.Right, select: false, wholeWord: false, end: true);
            Assert.Equal (10, document.CursorIndex);   // before the line's CRLF
            document.MoveCursor (ArrowDirection.Right, select: false, wholeWord: true, end: true);   // Ctrl+End
            Assert.Equal (Crlf.Length, document.CursorIndex);
        }

        [Fact]
        public void The_painted_selection_covers_the_characters_selected ()
        {
            using var crlf = Box (Crlf);
            using var lf = Box (Lf);

            // "bb" and the CRLF before it, as indices into each text.
            crlf.document.Select (4, 2);
            lf.document.Select (3, 2);

            var in_crlf = crlf.document.GetTextSelection ();
            var in_lf = lf.document.GetTextSelection ();

            Assert.Equal (in_lf.Start, in_crlf.Start);
            Assert.Equal (in_lf.End, in_crlf.End);
        }

        [Fact]
        public void A_colorizer_span_that_ends_inside_a_CRLF_does_not_change_the_indices ()
        {
            using var plain = Box (Crlf);
            using var colored = Box (Crlf);

            // The first span ends between the CR and the LF.
            colored.Colorizer = _ => new[] { new TextSpanStyle (0, 3, SKColors.Red), new TextSpanStyle (3, 4, SKColors.Blue) };

            for (var line = 0; line < 4; line++)
                Assert.Equal (plain.GetFirstCharIndexFromLine (line), colored.GetFirstCharIndexFromLine (line));

            foreach (var index in new[] { 0, 1, 4, 5, 8, 9, 12, 13, 14 })
                Assert.True (plain.GetPositionFromCharIndex (index) == colored.GetPositionFromCharIndex (index), $"index {index}");
        }

        [Fact]
        public void A_long_CRLF_text_keeps_its_lines ()
        {
            // Past TextBlockAppender's segmenting threshold, which cuts at line ends.
            var text = string.Join ("\r\n", Enumerable.Range (0, 1200).Select (i => "line " + i));
            using var box = Box (text);
            box.Colorizer = t => new[] { new TextSpanStyle (0, 5, SKColors.Red) };

            Assert.Equal (text.IndexOf ("line 1000", System.StringComparison.Ordinal), box.GetFirstCharIndexFromLine (1000));
        }
    }
}
