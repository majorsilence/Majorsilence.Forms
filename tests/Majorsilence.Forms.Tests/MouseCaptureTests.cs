using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Mouse capture is exclusive and belongs to the pointer: one control holds it at a time, and while it
/// does, every mouse event is that control's — including events over a different window.
/// </summary>
/// <remarks>
/// Capture used to be an independent flag on a control <em>and all of its ancestors</em>, with no single
/// holder, so handing capture over did nothing: the previous holder kept its flag and mouse routing kept
/// finding it. That is exactly how WinForms code takes over a drag one of its children started — the
/// child captures on mouse-down, then the library assigns capture to a form. In DockPanelSuite that form
/// is the document being dragged, a separate window hosted inside the main one, so both halves were
/// needed: the tab strip had to lose capture, and the main window had to stop hit-testing and route to
/// the new holder. Without them the strip kept receiving moves and restarted the drag on every one,
/// stacking up a full-screen drag-outline window per mouse move until the app looked hung behind them.
/// </remarks>
[Collection ("Headless")]
public class MouseCaptureTests
{
    [Fact]
    public void Taking_capture_releases_the_previous_holder ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var first = new Panel { Left = 0, Top = 0, Width = 100, Height = 50 };
        var second = new Panel { Left = 100, Top = 0, Width = 100, Height = 50 };
        form.Controls.Add (first);
        form.Controls.Add (second);

        first.Capture = true;
        second.Capture = true;

        Assert.False (first.Capture);
        Assert.True (second.Capture);

        second.Capture = false;
    }

    [Fact]
    public void The_previous_holder_is_told_it_lost_capture ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var first = new Panel ();
        var second = new Panel ();
        form.Controls.Add (first);
        form.Controls.Add (second);

        var lost = 0;
        first.MouseCaptureChanged += (_, _) => lost++;

        first.Capture = true;       // one change: gained
        second.Capture = true;      // one change: lost

        Assert.Equal (2, lost);

        second.Capture = false;
    }

    [Fact]
    public void A_parent_reports_capture_while_a_child_holds_it ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var parent = new Panel { Width = 200, Height = 100 };
        var child = new Panel { Width = 50, Height = 20 };
        form.Controls.Add (parent);
        parent.Controls.Add (child);

        child.Capture = true;

        Assert.True (parent.Capture);

        child.Capture = false;
        Assert.False (parent.Capture);
    }

    [Fact]
    public void Releasing_capture_clears_the_holder ()
    {
        using var form = new Form { Width = 300, Height = 200 };
        var panel = new Panel ();
        form.Controls.Add (panel);

        panel.Capture = true;
        panel.Capture = false;

        Assert.False (panel.Capture);
        Assert.Null (Control.CaptureHolder);
    }

    [Fact]
    public void A_move_goes_to_the_capturing_control_rather_than_the_one_under_the_pointer ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 400, Height = 300 };
        var capturing = new Panel { Left = 0, Top = 0, Width = 50, Height = 50 };
        var underPointer = new Panel { Left = 100, Top = 100, Width = 100, Height = 100 };
        form.Controls.Add (capturing);
        form.Controls.Add (underPointer);

        HeadlessRenderer.CapturePng (form, 400, 300);

        var toCapturing = 0;
        var toOther = 0;
        capturing.MouseMove += (_, _) => toCapturing++;
        underPointer.MouseMove += (_, _) => toOther++;

        capturing.Capture = true;
        HeadlessRenderer.MouseMove (form, 150, 150, MouseButtons.Left);

        Assert.Equal (1, toCapturing);
        Assert.Equal (0, toOther);

        capturing.Capture = false;
    }

    // Builds the DockPanelSuite shape: a document form hosted inside a host window, so the drag's owner
    // is a separate WindowBase that the host window's own dispatch cannot reach.
    private static (Form Host, Panel UnderPointer, Form Hosted, Panel Capturing) HostedDocument ()
    {
        HeadlessRenderer.Use ();

        var host = new Form { Width = 400, Height = 300 };
        var underPointer = new Panel { Left = 0, Top = 0, Width = 300, Height = 200 };
        host.Controls.Add (underPointer);

        var hosted = new Form { Width = 200, Height = 200 };
        var capturing = new Panel { Left = 0, Top = 0, Width = 150, Height = 150 };
        hosted.Controls.Add (capturing);
        underPointer.Controls.Add (hosted);

        HeadlessRenderer.CapturePng (host, 400, 300);

        return (host, underPointer, hosted, capturing);
    }

    [Fact]
    public void A_move_over_the_host_window_goes_to_a_control_capturing_in_a_hosted_form ()
    {
        var (host, underPointer, hosted, capturing) = HostedDocument ();

        using (host) {
            var toCapturing = 0;
            var toOther = 0;
            capturing.MouseMove += (_, _) => toCapturing++;
            underPointer.MouseMove += (_, _) => toOther++;

            capturing.Capture = true;
            HeadlessRenderer.MouseMove (host, 50, 50, MouseButtons.Left);

            Assert.Equal (0, toOther);
            Assert.Equal (1, toCapturing);

            capturing.Capture = false;
            hosted.Close ();
        }
    }

    [Fact]
    public void Normal_hit_testing_resumes_once_capture_is_released ()
    {
        var (host, underPointer, hosted, capturing) = HostedDocument ();

        using (host) {
            var toOther = 0;
            underPointer.MouseMove += (_, _) => toOther++;

            capturing.Capture = true;
            HeadlessRenderer.MouseMove (host, 50, 50, MouseButtons.Left);
            capturing.Capture = false;

            HeadlessRenderer.MouseMove (host, 60, 60, MouseButtons.Left);

            Assert.Equal (1, toOther);

            hosted.Close ();
        }
    }

    [Fact]
    public void An_unrelated_top_level_window_does_not_steal_another_windows_input ()
    {
        // The deliberate limit of the routing above: capture reaches into a form hosted inside this
        // window, but never across unrelated top-level windows. WinForms capture is per-thread and would
        // hold across both, but honouring that would let one window's unreleased capture swallow input to
        // every other window in the process -- a worse failure than the one being fixed.
        HeadlessRenderer.Use ();

        using var pointerWindow = new Form { Width = 400, Height = 300 };
        var underPointer = new Panel { Left = 0, Top = 0, Width = 300, Height = 200 };
        pointerWindow.Controls.Add (underPointer);

        using var other = new Form { Width = 200, Height = 200 };
        var capturing = new Panel { Width = 150, Height = 150 };
        other.Controls.Add (capturing);

        HeadlessRenderer.CapturePng (pointerWindow, 400, 300);
        HeadlessRenderer.CapturePng (other, 200, 200);

        var toOther = 0;
        underPointer.MouseMove += (_, _) => toOther++;

        capturing.Capture = true;
        HeadlessRenderer.MouseMove (pointerWindow, 50, 50, MouseButtons.Left);

        Assert.Equal (1, toOther);

        capturing.Capture = false;
    }

    [Fact]
    public void A_window_taking_capture_takes_it_from_a_control_in_another_window ()
    {
        // BeginDrag's exact move: the control captured on mouse-down, then the library assigns capture to
        // a form. The control must stop being the holder, or it keeps receiving the drag's moves.
        using var pointerWindow = new Form { Width = 400, Height = 300 };
        var strip = new Panel { Width = 300, Height = 20 };
        pointerWindow.Controls.Add (strip);

        using var document = new Form { Width = 200, Height = 200 };

        strip.Capture = true;
        document.Capture = true;

        Assert.False (strip.Capture);
        Assert.True (document.Capture);

        document.Capture = false;
    }
}
