using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A multiline TextBox's scroll bars could not be used to reach the end of the text -- and for text
    // shorter than two viewports they could not move at all. UpdateScrollBars set Maximum to the
    // overflow (content minus viewport) and LargeChange to the viewport, but a user-driven scroll stops
    // at Maximum - LargeChange + 1 (upstream's rule, ScrollAndSpinArithmeticTests), which for that
    // pairing is `overflow - viewport + 1`: zero or negative until the content is more than twice the
    // viewport, and always one viewport short of the end. Found in the Theme Studio's CSS editor,
    // whose ~60 lines in a ~40-line viewport had a bar that did nothing. The convention ListBox and
    // TreeView already follow is Maximum = content - 1 with LargeChange = viewport, so the reachable
    // range is exactly the overflow.
    [Collection ("Headless")]
    public class TextBoxScrollBarRangeTests
    {
        // Enough lines to overflow the box by roughly half a viewport -- the shape that used to
        // scroll nothing at all -- but not two viewports, which used to scroll partially.
        private static TextBox ShortOverflow ()
        {
            HeadlessRenderer.Use ();

            var box = new TextBox {
                Width = 200, Height = 120, Multiline = true, WordWrap = false, ScrollBars = ScrollBars.Both,
                Text = string.Join ("\n", Enumerable.Range (1, 14).Select (i => $"line {i} " + new string ('x', 60))),
            };

            PaintSurface.Render (box).Dispose ();   // lays the text out and sizes the bars
            return box;
        }

        private static void ClickBottomArrow (TextBox box)
        {
            var bar = box.VerticalScrollBar;
            bar.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, bar.Width / 2, bar.Height - 2, 0));
            bar.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, bar.Width / 2, bar.Height - 2, 0));
        }

        private static void ClickRightArrow (TextBox box)
        {
            var bar = box.HorizontalScrollBar;
            bar.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, bar.Width - 2, bar.Height / 2, 0));
            bar.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, bar.Width - 2, bar.Height / 2, 0));
        }

        [Fact]
        public void The_vertical_bar_can_scroll_text_shorter_than_two_viewports ()
        {
            using var box = ShortOverflow ();
            Assert.True (box.VerticalScrollBar.Enabled);
            var firstLineBefore = box.GetPositionFromCharIndex (0).Y;

            ClickBottomArrow (box);

            Assert.True (box.VerticalScrollBar.Value > 0, "the bottom arrow did not move the bar");
            Assert.True (box.GetPositionFromCharIndex (0).Y < firstLineBefore, "the text did not scroll with the bar");
        }

        [Fact]
        public void The_vertical_bar_reaches_the_last_line ()
        {
            using var box = ShortOverflow ();

            for (var i = 0; i < 100; i++)
                ClickBottomArrow (box);

            var lastLine = box.GetPositionFromCharIndex (box.Text.Length - 1);
            var viewportBottom = box.PaddedClientRectangle.Bottom;

            Assert.True (lastLine.Y < viewportBottom, $"the last line sits at y={lastLine.Y}, below the viewport bottom {viewportBottom}");
            Assert.True (box.GetPositionFromCharIndex (0).Y < 0, "the first line should have scrolled out of view");
        }

        [Fact]
        public void The_wheel_scrolls_the_same_range ()
        {
            using var box = ShortOverflow ();

            for (var i = 0; i < 100; i++)
                box.RaiseMouseWheel (new MouseEventArgs (MouseButtons.None, 0, 50, 50, -120));

            Assert.True (box.GetPositionFromCharIndex (box.Text.Length - 1).Y < box.PaddedClientRectangle.Bottom);
            Assert.True (box.VerticalScrollBar.Value > 0);
        }

        [Fact]
        public void The_horizontal_bar_reaches_the_end_of_the_longest_line ()
        {
            using var box = ShortOverflow ();
            Assert.True (box.HorizontalScrollBar.Enabled);

            for (var i = 0; i < 200; i++)
                ClickRightArrow (box);

            var endOfLine = box.GetPositionFromCharIndex (box.Text.IndexOf ('\n', StringComparison.Ordinal) - 1);

            Assert.True (box.HorizontalScrollBar.Value > 0, "the right arrow did not move the bar");
            Assert.True (endOfLine.X < box.PaddedClientRectangle.Right, $"the end of the first line sits at x={endOfLine.X}, past the viewport right {box.PaddedClientRectangle.Right}");
        }

        [Fact]
        public void A_bar_never_scrolls_past_the_content ()
        {
            // The other half of the convention: the last page of content starts at the reachable
            // maximum, so no blank space is shown below the text.
            using var box = ShortOverflow ();

            for (var i = 0; i < 100; i++)
                ClickBottomArrow (box);

            var lastLine = box.GetPositionFromCharIndex (box.Text.Length - 1);
            var lineHeight = box.GetPositionFromCharIndex (box.Text.IndexOf ('\n', StringComparison.Ordinal) + 1).Y - box.GetPositionFromCharIndex (0).Y;

            Assert.True (lastLine.Y + lineHeight >= box.PaddedClientRectangle.Bottom - lineHeight,
                $"the last line (y={lastLine.Y}) sits a whole line above the viewport bottom {box.PaddedClientRectangle.Bottom}: blank space was scrolled into view");
        }
    }
}
