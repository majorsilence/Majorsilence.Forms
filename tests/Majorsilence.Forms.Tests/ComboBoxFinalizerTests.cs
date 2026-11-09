using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// A ComboBox reached by the finalizer (Control's ~Control calls Dispose (false) on the GC's thread) must leave its drop-down
// window alone: closing it there touched the UI from the wrong thread, which Avalonia refuses with "The calling thread
// cannot access this object because a different thread owns it", and the unhandled exception ended the process.
[Collection ("Headless")]
public class ComboBoxFinalizerTests
{
    private sealed class FinalizedComboBox : ComboBox
    {
        public void RunFinalizerDispose () => Dispose (false);
    }

    [Fact]
    public void The_finalizer_does_not_close_the_drop_down ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form ();
        var combo = new FinalizedComboBox ();
        combo.Items.AddRange (new object[] { "A", "B" });
        form.Controls.Add (combo);
        form.Show ();
        combo.DroppedDown = true;
        Assert.True (combo.DroppedDown);

        combo.RunFinalizerDispose ();

        Assert.True (combo.DroppedDown, "the finalizer path closed the drop-down window");
        combo.DroppedDown = false;
    }
}
