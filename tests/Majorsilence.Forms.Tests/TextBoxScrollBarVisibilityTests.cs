using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Which scroll bars a TextBox shows, as upstream's: measured (.NET 10, Windows 11) from the native edit's
    // WS_HSCROLL/WS_VSCROLL styles for every Multiline/WordWrap/ScrollBars combination, with text that fits and text
    // that overflows both ways. A single-line box has none; a multiline one has the vertical bar ScrollBars asks for,
    // and the horizontal one only with WordWrap off; the text does not change either. Here the bars followed
    // ScrollBars alone, so a word-wrapping Comments box set to ScrollBars.Both drew a horizontal bar upstream's never
    // has, and a single-line box could show one.
    public class TextBoxScrollBarVisibilityTests
    {
        public static TheoryData<bool, bool, ScrollBars, bool, bool> Measured => new () {
            // multiline, word wrap, scroll bars -> horizontal, vertical
            { true, true, ScrollBars.None, false, false },
            { true, true, ScrollBars.Vertical, false, true },
            { true, true, ScrollBars.Horizontal, false, false },
            { true, true, ScrollBars.Both, false, true },
            { true, false, ScrollBars.None, false, false },
            { true, false, ScrollBars.Vertical, false, true },
            { true, false, ScrollBars.Horizontal, true, false },
            { true, false, ScrollBars.Both, true, true },
            { false, true, ScrollBars.Vertical, false, false },
            { false, true, ScrollBars.Horizontal, false, false },
            { false, true, ScrollBars.Both, false, false },
            { false, false, ScrollBars.Both, false, false },
        };

        [Theory]
        [MemberData (nameof (Measured))]
        public void A_text_box_shows_upstream_s_scroll_bars (bool multiline, bool wordWrap, ScrollBars bars, bool horizontal, bool vertical)
        {
            using var box = new TextBox { Multiline = multiline, WordWrap = wordWrap, ScrollBars = bars, Text = "short" };

            Assert.Equal (horizontal, box.HorizontalScrollBar.Visible);
            Assert.Equal (vertical, box.VerticalScrollBar.Visible);
        }

        [Fact]
        public void The_bars_follow_word_wrap_and_multiline_set_after_scroll_bars ()
        {
            using var box = new TextBox { ScrollBars = ScrollBars.Both };
            Assert.False (box.HorizontalScrollBar.Visible);
            Assert.False (box.VerticalScrollBar.Visible);

            box.Multiline = true;
            Assert.False (box.HorizontalScrollBar.Visible);
            Assert.True (box.VerticalScrollBar.Visible);

            box.WordWrap = false;
            Assert.True (box.HorizontalScrollBar.Visible);

            box.WordWrap = true;
            Assert.False (box.HorizontalScrollBar.Visible);

            box.Multiline = false;
            Assert.False (box.VerticalScrollBar.Visible);
        }
    }
}
