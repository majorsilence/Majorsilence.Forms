using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A single-line TextBox centres its text vertically, and its caret has to sit on the same line. An empty box has
    // no laid-out text to measure (its height is 0), so the centring used the whole box height as the slack and put
    // the line's top at the box's middle: the caret hung from the vertical centre, down towards the lower left.
    [Collection ("Headless")]
    public class TextBoxEmptyCaretTests
    {
        public TextBoxEmptyCaretTests () => HeadlessRenderer.Use ();

        [Theory]
        [InlineData (48)]
        [InlineData (30)]
        [InlineData (80)]
        public void TheCaretOfAnEmptyBox_IsOnTheSameLineAsTheCaretOfABoxWithText (int height)
        {
            using var empty = new TextBox { Size = new Size (200, height) };
            using var withText = new TextBox { Size = new Size (200, height), Text = " " };

            // The caret before the first character of each.
            var emptyCaret = empty.GetPositionFromCharIndex (0);
            var textCaret = withText.GetPositionFromCharIndex (0);

            Assert.Equal (textCaret.Y, emptyCaret.Y);
        }

        [Fact]
        public void TheCaretOfAnEmptyBox_IsAsTallAsTheCaretOfABoxWithText ()
        {
            using var empty = new TextBox { Size = new Size (200, 48) };
            using var withText = new TextBox { Size = new Size (200, 48), Text = " " };

            var emptyCaret = TextMeasurer.GetCursorLocation (empty.document.GetTextBlock (), empty.TextOrigin, 0, empty.CurrentFontSize, empty.document.EmptyLineHeight);
            var textCaret = TextMeasurer.GetCursorLocation (withText.document.GetTextBlock (), withText.TextOrigin, 0, withText.CurrentFontSize, withText.document.EmptyLineHeight);

            Assert.Equal (textCaret.Height, emptyCaret.Height);
        }
    }
}
