using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #690: AppendText (and the ScrollToCaret that goes with it) laid the whole document out on every call, so a log
    // window that appended a line at a time paid for every line already in it each time -- quadratic. The scroll is
    // now resolved when something reads the scroll position, so a run of appends costs one layout.
    // These count layouts rather than time them: a timing threshold is a flaky test, a layout count is the mechanism.
    public class TextBoxLazyScrollTests
    {
        private static TextBox Log (bool rich = false)
        {
            TextBox box = rich ? new RichTextBox () : new TextBox ();

            box.Multiline = true;
            box.Size = new Size (400, 100);
            box.GetPositionFromCharIndex (0);   // lay the empty box out once, so the counts below start from a known place

            return box;
        }

        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void A_run_of_appends_lays_the_text_out_once_not_once_each (bool rich)
        {
            using var box = Log (rich);
            var before = box.document.LayoutCount;

            for (var i = 0; i < 300; i++)
                box.AppendText ($"line {i} some log text here and more\r\n");

            Assert.True (box.document.LayoutCount - before <= 1, $"{box.document.LayoutCount - before} layouts for 300 appends");
        }

        [Fact]
        public void The_idiom_of_AppendText_then_ScrollToCaret_is_no_dearer ()
        {
            using var box = Log ();
            var before = box.document.LayoutCount;

            for (var i = 0; i < 300; i++) {
                box.AppendText ($"line {i}\r\n");
                box.ScrollToCaret ();
            }

            Assert.True (box.document.LayoutCount - before <= 1, $"{box.document.LayoutCount - before} layouts for 300 appends");
        }

        [Fact]
        public void The_new_text_is_in_view_once_the_scroll_position_is_read ()
        {
            using var box = Log ();

            for (var i = 0; i < 300; i++)
                box.AppendText ($"line {i}\r\n");

            // The caret is after the last line break, on an empty last line: it has to be inside the 100px box.
            var caret = box.GetPositionFromCharIndex (box.TextLength);

            Assert.InRange (caret.Y, 0, box.ClientSize.Height);
        }

        [Fact]
        public void The_scroll_is_announced_when_it_is_resolved ()
        {
            using var box = Log ();
            var scrolled = 0;
            box.VScroll += (_, _) => scrolled++;

            for (var i = 0; i < 300; i++)
                box.AppendText ($"line {i}\r\n");

            box.GetPositionFromCharIndex (box.TextLength);

            Assert.True (scrolled >= 1);
        }

        [Fact]
        public void A_scroll_asked_for_before_the_caret_moved_on_goes_where_the_caret_was ()
        {
            using var box = Log ();

            for (var i = 0; i < 300; i++)
                box.AppendText ($"line {i}\r\n");

            // The view went to the end when AppendText asked; moving the caret away afterwards does not take it back.
            box.SelectionStart = 0;

            var top = box.GetPositionFromCharIndex (0);
            var end = box.GetPositionFromCharIndex (box.TextLength);

            Assert.True (top.Y < 0, $"line 0 at {top.Y}");
            Assert.InRange (end.Y, 0, box.ClientSize.Height);
        }

        [Fact]
        public void ScrollToCaret_brings_a_caret_moved_up_back_into_view ()
        {
            using var box = Log ();

            for (var i = 0; i < 300; i++)
                box.AppendText ($"line {i}\r\n");

            box.SelectionStart = 0;
            box.ScrollToCaret ();

            Assert.InRange (box.GetPositionFromCharIndex (0).Y, 0, box.ClientSize.Height);
        }
    }
}
