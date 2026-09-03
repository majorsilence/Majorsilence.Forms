using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.12 (findings TXT-02 P0, TXT-35): every text-mutating verb assigned Text, and that setter is
    // DEFINED to reset the caret to 0, clear the undo buffer and drop Modified. So the commonest line
    // in any logging UI -- log.AppendText (line + Environment.NewLine) -- scrolled to the oldest line
    // on every append, and a following ScrollToCaret went to the top too, because the caret was at 0.
    //
    // RichTextBox made it worse by shadowing AppendText and SelectedText with `new`, so the SAME
    // object behaved differently depending on the static type of the reference it was called through.
    [Collection ("Headless")]
    public class AppendTextRoutingTests
    {
        // Small enough that 50 lines cannot fit: the scroll position is the whole point here.
        private static TextBox Log (int lines = 50)
        {
            HeadlessRenderer.Use ();
            var box = new TextBox { Width = 100, Height = 40, Multiline = true };
            box.Text = string.Join ("\n", Enumerable.Range (0, lines).Select (i => $"line {i}"));

            return box;
        }

        private static RichTextBox RichLog (int lines = 50)
        {
            HeadlessRenderer.Use ();
            var box = new RichTextBox { Width = 100, Height = 40, Multiline = true };
            box.Text = string.Join ("\n", Enumerable.Range (0, lines).Select (i => $"line {i}"));

            return box;
        }

        [Fact]
        public void AppendText_leaves_the_caret_after_the_appended_text ()
        {
            using var box = Log ();

            box.AppendText ("\nappended");

            Assert.Equal (box.TextLength, box.SelectionStart);
            Assert.EndsWith ("appended", box.Text);
        }

        [Fact]
        public void AppendText_brings_the_new_text_into_view_instead_of_the_top ()
        {
            // The finding's own test. Before the fix the caret went to 0, so the view scrolled to the
            // first line and the appended text sat far below the bottom edge.
            using var box = Log ();

            box.AppendText ("\nappended");

            var last = box.GetPositionFromCharIndex (box.TextLength - 1);

            Assert.InRange (last.Y, box.ClientRectangle.Top, box.ClientRectangle.Bottom);
        }

        [Fact]
        public void AppendText_keeps_the_undo_buffer ()
        {
            using var box = Log ();
            box.Select (box.TextLength, 0);
            box.RaiseKeyPress (new KeyPressEventArgs ('z'));   // something to undo
            Assert.True (box.CanUndo);

            box.AppendText ("\nappended");

            // The Text setter called ClearUndo, so an append used to throw the user's history away.
            Assert.True (box.CanUndo);
        }

        [Fact]
        public void AppendText_marks_the_control_modified ()
        {
            // A correction to the finding, which suggests asserting Modified is *unchanged*: upstream
            // appends with EM_REPLACESEL, and that sets the edit control's modify flag. What was wrong
            // before is the direction -- routing through the Text setter forced Modified to FALSE, so
            // an append made a dirty document look clean.
            using var box = Log ();
            Assert.False (box.Modified);

            box.AppendText ("\nappended");

            Assert.True (box.Modified);
        }

        [Fact]
        public void AppendText_applies_to_a_read_only_box_and_ignores_MaxLength ()
        {
            // GUARD, not proof: the Text setter this replaced ignored ReadOnly and MaxLength as well
            // (the document's Text setter has no such check), so no previous version fails this. It
            // pins the behaviour deliberately: upstream brackets its EM_REPLACESEL with
            // EM_LIMITTEXT 0, because an append is not user input -- a read-only log window the app
            // writes to is the ordinary case, not an edge case.
            using var box = Log (lines: 2);
            box.MaxLength = 3;
            box.ReadOnly = true;

            box.AppendText ("\nappended");

            Assert.EndsWith ("appended", box.Text);
        }

        [Fact]
        public void AppendText_raises_TextChanged_once ()
        {
            // GUARD, not proof: the old path raised once too. It pins that routing through the document
            // did not turn one append into a delete-plus-insert pair of notifications.
            using var box = Log ();
            var changes = 0;
            box.TextChanged += (_, _) => changes++;

            box.AppendText ("\nappended");

            Assert.Equal (1, changes);
        }

        [Fact]
        public void An_append_through_a_base_reference_behaves_the_same_as_through_the_derived_one ()
        {
            // TXT-02's other half: RichTextBox shadowed AppendText with `new`, so which implementation
            // ran depended on the static type of the reference, not on the object.
            using var derived = RichLog ();
            using var basic = RichLog ();
            TextBoxBase asBase = basic;

            derived.AppendText ("\nappended");
            asBase.AppendText ("\nappended");

            Assert.Equal (derived.Text, basic.Text);
            Assert.Equal (derived.SelectionStart, basic.SelectionStart);
            Assert.Equal (derived.TextLength, derived.SelectionStart);
        }

        [Fact]
        public void Replacing_a_selection_in_a_RichTextBox_does_not_scroll_to_the_top ()
        {
            // TXT-35. The invariant is that the scroll position does not MOVE -- a replace is not a
            // reason to jump anywhere -- so the box is scrolled to the bottom first and the first
            // line's position is used to observe where the view sits.
            using var box = RichLog (lines: 100);
            box.Select (box.TextLength - 1, 1);
            box.ScrollToCaret ();

            var viewBefore = box.GetPositionFromCharIndex (0).Y;
            Assert.True (viewBefore < box.ClientRectangle.Top, "the box should be scrolled down");

            box.SelectedText = "x";

            Assert.Equal (box.TextLength, box.SelectionStart);
            Assert.Equal (viewBefore, box.GetPositionFromCharIndex (0).Y);
        }

        [Fact]
        public void SelectedText_behaves_the_same_through_either_reference ()
        {
            // Comparing the resulting text and caret is NOT enough: the shadow rebuilt the same string
            // and then patched the caret to the same index, so those two agreed while everything around
            // them differed. Undo and Modified are where the divergence showed.
            using var derived = RichLog (lines: 4);
            using var basic = RichLog (lines: 4);
            TextBox asBase = basic;

            derived.Select (2, 3);
            derived.SelectedText = "ZZ";

            asBase.Select (2, 3);
            asBase.SelectedText = "ZZ";

            Assert.Equal (derived.Text, basic.Text);
            Assert.Equal (derived.SelectionStart, basic.SelectionStart);
            Assert.Equal (derived.CanUndo, basic.CanUndo);
            Assert.Equal (derived.Modified, basic.Modified);

            // And the replace is undoable through either reference, where the shadow cleared undo.
            Assert.True (derived.CanUndo);
            Assert.True (derived.Modified);
        }

        [Fact]
        public void Replacing_a_selection_is_a_single_undo_step ()
        {
            // GUARD, not proof: TextBox.SelectedText already went through the document, so this passed
            // before. It pins the invariant ReplaceRange has to preserve -- a replace is a delete plus
            // an insert, and Win32 reverses it in a single Undo.
            using var box = Log (lines: 3);
            var before = box.Text;
            box.Select (0, 4);

            box.SelectedText = "REPLACED";
            Assert.NotEqual (before, box.Text);

            box.Undo ();

            Assert.Equal (before, box.Text);
        }

        [Fact]
        public void The_selection_setter_still_refuses_a_read_only_box ()
        {
            // GUARD, not proof: nothing here changed for this path. It pins that ReplaceRange's
            // ignoreLimits escape hatch was given to AppendText only -- a user-driven replace on a
            // read-only box must still do nothing.
            using var box = Log (lines: 2);
            var before = box.Text;
            box.ReadOnly = true;
            box.Select (0, 4);

            box.SelectedText = "REPLACED";

            Assert.Equal (before, box.Text);
        }

        [Fact]
        public void Appending_nothing_changes_nothing ()
        {
            // GUARD, not proof: an empty or null append is a no-op, and must not raise TextChanged or
            // move the caret just because the append path now writes through the document.
            using var box = Log (lines: 2);
            box.Select (0, 0);
            var changes = 0;
            box.TextChanged += (_, _) => changes++;

            box.AppendText (string.Empty);

            Assert.Equal (0, changes);
            Assert.Equal (0, box.SelectionStart);
        }
    }
}
