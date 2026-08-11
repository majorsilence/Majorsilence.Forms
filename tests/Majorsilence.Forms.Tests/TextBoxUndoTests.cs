using Xunit;

namespace Majorsilence.Forms.Tests;

// TextBoxBase.Undo/ClearUndo were empty and CanUndo hard-coded false, so an Edit▸Undo command wired to
// a TextBox did nothing. Semantics follow the Win32 edit control: a single-level buffer that Undo
// toggles, a run of same-kind edits coalescing into one step, and WM_SETTEXT (a programmatic
// Text assignment) resetting the buffer entirely.
//
// Edits are driven through SelectedText because TextBox overrides it to go via TextBoxDocument.
// InsertText -- the same path typing, paste and cut take -- so these exercise the real edit pipeline
// rather than a programmatic Text assignment.
public class TextBoxUndoTests
{
    [Fact]
    public void CanUndo_IsFalseBeforeAnyEdit ()
    {
        using var box = new TextBox ();

        Assert.False (box.CanUndo);
    }

    [Fact]
    public void Undo_RevertsAnEdit ()
    {
        using var box = new TextBox { Text = "hello" };
        box.SelectionStart = 5;
        box.SelectedText = " world";

        Assert.Equal ("hello world", box.Text);
        Assert.True (box.CanUndo);

        box.Undo ();

        Assert.Equal ("hello", box.Text);
    }

    [Fact]
    public void Undo_RevertsAReplaceInOneStep ()
    {
        // Replacing a selection is a delete plus an insert internally; Win32 undoes it in one go.
        using var box = new TextBox { Text = "hello" };
        box.SelectAll ();
        box.SelectedText = "goodbye";

        Assert.Equal ("goodbye", box.Text);

        box.Undo ();

        Assert.Equal ("hello", box.Text);
    }

    [Fact]
    public void Undo_CoalescesARunOfTyping ()
    {
        using var box = new TextBox { Text = "" };
        box.SelectedText = "a";
        box.SelectedText = "b";
        box.SelectedText = "c";

        Assert.Equal ("abc", box.Text);

        // One step for the whole run, not one per character.
        box.Undo ();

        Assert.Equal ("", box.Text);
    }

    [Fact]
    public void Undo_TogglesLikeTheWin32SingleLevelBuffer ()
    {
        using var box = new TextBox { Text = "hello" };
        box.SelectAll ();
        box.SelectedText = "goodbye";

        box.Undo ();
        Assert.Equal ("hello", box.Text);

        // Undo again redoes, rather than being a no-op or walking further back.
        box.Undo ();
        Assert.Equal ("goodbye", box.Text);
    }

    [Fact]
    public void SettingTextProgrammatically_ResetsTheUndoBuffer ()
    {
        using var box = new TextBox { Text = "hello" };
        box.SelectAll ();
        box.SelectedText = "edited";
        Assert.True (box.CanUndo);

        box.Text = "assigned";

        Assert.False (box.CanUndo);
        box.Undo ();
        Assert.Equal ("assigned", box.Text);
    }

    [Fact]
    public void ClearUndo_DiscardsTheBuffer ()
    {
        using var box = new TextBox { Text = "hello" };
        box.SelectAll ();
        box.SelectedText = "edited";

        box.ClearUndo ();

        Assert.False (box.CanUndo);
        box.Undo ();
        Assert.Equal ("edited", box.Text);
    }
}
