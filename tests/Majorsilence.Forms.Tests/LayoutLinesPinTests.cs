using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Pins what <c>Graphics.LayoutLines</c> does today for text with more than one paragraph. This records current
    /// behaviour, it does not endorse it: with a positive width every paragraph is followed by an empty line (the
    /// wrapping loop runs once more after the paragraph is used up and adds a zero-length line), so later paragraphs
    /// sit lower than they do when the width is 0. If that is ever changed deliberately, this test is the place that
    /// says what changed.
    /// </summary>
    public class LayoutLinesPinTests
    {
        [Fact]
        public void Wrapping_path_adds_an_empty_line_after_each_paragraph ()
        {
            using var font = new Font ("Arial", 12f);
            var skFont = font.GetSKFont ();

            var wrapped = Graphics.LayoutLines ("a\nb", skFont, 1000f);
            var unwrapped = Graphics.LayoutLines ("a\nb", skFont, 0f);

            // no wrapping width: one line per paragraph, indexes 0 and 1
            Assert.Equal (new[] { (0, 1, 0), (2, 1, 1) }, unwrapped);

            // positive width: each paragraph is followed by an empty line, so "b" is on line index 2, not 1
            Assert.Equal (new[] { (0, 1, 0), (1, 0, 1), (2, 1, 2), (3, 0, 3) }, wrapped);
        }

        [Fact]
        public void A_single_paragraph_has_one_content_line_on_both_paths ()
        {
            using var font = new Font ("Arial", 12f);
            var skFont = font.GetSKFont ();

            Assert.Equal ((0, 5, 0), Graphics.LayoutLines ("hello", skFont, 0f)[0]);
            Assert.Equal ((0, 5, 0), Graphics.LayoutLines ("hello", skFont, 1000f)[0]);
        }
    }
}
