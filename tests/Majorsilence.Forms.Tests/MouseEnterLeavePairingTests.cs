using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// EVT-14: enter and leave come in pairs per control, because in WinForms every control is its own window.
// Moving from a parent's own area onto a child leaves the parent; moving back enters it again. Here the
// parent got no MouseLeave onto a child but a fresh MouseEnter coming back, so its enters piled up; and a
// control entered with the pointer already over a child entered that child twice, because the entry was
// not recorded and the move that followed entered it again.
[Collection ("Headless")]
public class MouseEnterLeavePairingTests
{
    private static (Form form, Panel panel, Label child) Scene ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 400, Height = 300 };
        var panel = new Panel { Left = 100, Top = 50, Width = 200, Height = 200 };
        var child = new Label { Left = 20, Top = 20, Width = 60, Height = 60 };
        panel.Controls.Add (child);
        form.Controls.Add (panel);
        form.Show ();
        return (form, panel, child);
    }

    [Fact]
    public void Crossing_onto_a_child_and_back_leaves_and_reenters_the_parent ()
    {
        var (form, panel, child) = Scene ();

        using (form) {
            var p = panel.GetPositionInForm ();
            using var recorder = EventRecorder.For (panel, "MouseEnter", "MouseLeave").Also (child, "child", "MouseEnter", "MouseLeave");

            HeadlessRenderer.MouseMove (form, 5, 5);                     // outside the panel
            HeadlessRenderer.MouseMove (form, p.X + 150, p.Y + 150);     // the panel's own area
            HeadlessRenderer.MouseMove (form, p.X + 160, p.Y + 160);     // still there: no second enter
            HeadlessRenderer.MouseMove (form, p.X + 40, p.Y + 40);       // onto the child
            HeadlessRenderer.MouseMove (form, p.X + 150, p.Y + 150);     // back to the panel
            HeadlessRenderer.MouseMove (form, 5, 5);                     // out

            recorder.AssertSequence ("MouseEnter", "MouseLeave", "child.MouseEnter", "child.MouseLeave", "MouseEnter", "MouseLeave");
        }
    }

    [Fact]
    public void Entering_straight_onto_a_child_enters_it_once_and_not_the_parent ()
    {
        var (form, panel, child) = Scene ();

        using (form) {
            var p = panel.GetPositionInForm ();
            using var recorder = EventRecorder.For (panel, "MouseEnter", "MouseLeave").Also (child, "child", "MouseEnter", "MouseLeave");

            HeadlessRenderer.MouseMove (form, 5, 5);
            HeadlessRenderer.MouseMove (form, p.X + 40, p.Y + 40);       // over the child, from outside the panel
            HeadlessRenderer.MouseMove (form, p.X + 45, p.Y + 45);
            HeadlessRenderer.MouseMove (form, 5, 5);

            recorder.AssertSequence ("child.MouseEnter", "child.MouseLeave");
        }
    }

    [Fact]
    public void A_child_entered_with_no_move_after_it_still_gets_its_leave ()
    {
        // The window can deliver an enter and then a leave with no move between (the pointer flicks
        // across). The child the enter descended into has to be the one the leave reaches.
        var (form, panel, child) = Scene ();

        using (form) {
            using var recorder = EventRecorder.For (child, "MouseEnter", "MouseLeave");

            panel.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 40, 40, 0));
            panel.RaiseMouseLeave (System.EventArgs.Empty);

            recorder.AssertSequence ("MouseEnter", "MouseLeave");
        }
    }
}
