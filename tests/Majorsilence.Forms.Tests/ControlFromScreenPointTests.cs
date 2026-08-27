using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="Control.FromScreenPoint"/> answers "which control is under this point", the question
/// <c>Control.FromChildHandle (WindowFromPoint (pt))</c> asks in ported WinForms code.
/// </summary>
/// <remarks>
/// Both halves of that idiom are necessarily null here (there are no window handles in this layer), so
/// ported code asking what is under the cursor got nothing back and had to fall through to whatever its
/// last resort was. In DockPanelSuite that resort is "float it": a document tab dragged inside its own
/// strip was torn out into a float window that was never shown, so the tab simply vanished.
///
/// The point is in the space <see cref="Control.MousePosition"/> reports, which measures from the
/// window origin rather than the client area.
/// </remarks>
[Collection ("Headless")]
public class ControlFromScreenPointTests
{
    private static (Form Form, Panel Outer, Panel Inner) Nested ()
    {
        HeadlessRenderer.Use ();

        var form = new Form { Width = 400, Height = 300 };
        var outer = new Panel { Left = 20, Top = 20, Width = 300, Height = 200 };
        var inner = new Panel { Left = 10, Top = 10, Width = 100, Height = 50 };

        outer.Controls.Add (inner);
        form.Controls.Add (outer);
        form.Show ();
        HeadlessRenderer.CapturePng (form, 400, 300);

        return (form, outer, inner);
    }

    [Fact]
    public void Finds_the_innermost_control_under_the_point ()
    {
        var (form, _, inner) = Nested ();

        using (form) {
            var point = inner.PointToScreen (new Point (5, 5));

            Assert.Same (inner, Control.FromScreenPoint (point));
        }
    }

    [Fact]
    public void Finds_the_outer_control_where_the_inner_one_does_not_reach ()
    {
        var (form, outer, _) = Nested ();

        using (form) {
            // Inside outer, past inner's 100x50 box.
            var point = outer.PointToScreen (new Point (200, 150));

            Assert.Same (outer, Control.FromScreenPoint (point));
        }
    }

    [Fact]
    public void Round_trips_with_the_position_the_pointer_actually_reports ()
    {
        // The way callers really use it: hit-test whatever Control.MousePosition currently says, which
        // is the pairing that has to line up -- a mismatch of even a title bar's height misses a tab
        // strip completely.
        var (form, _, inner) = Nested ();

        using (form) {
            HeadlessInput.MouseMove (form, WindowPoint.In (inner, 20, 20));

            Assert.Same (inner, Control.FromScreenPoint (Control.MousePosition));
        }
    }

    [Fact]
    public void Returns_null_for_a_point_outside_every_window ()
    {
        var (form, _, _) = Nested ();

        using (form) {
            Assert.Null (Control.FromScreenPoint (new Point (-10_000, -10_000)));
        }
    }

    [Fact]
    public void A_disabled_window_covering_the_point_is_ignored ()
    {
        // A disabled window cannot receive mouse input, so it is never what is under the cursor. This is
        // load-bearing rather than pedantic: a drag overlay is exactly such a window (DockPanelSuite's
        // DragForm sets Enabled = false and answers WM_NCHITTEST with HTTRANSPARENT), it covers the
        // whole screen, and it is opened last -- so counted, every hit test during a drag returned the
        // overlay the drag itself had just put up, never the pane underneath that the drop needs.
        var (form, _, inner) = Nested ();

        using var overlay = new Form { Width = 800, Height = 600, Enabled = false };
        overlay.Show ();

        using (form) {
            var point = inner.PointToScreen (new Point (5, 5));

            Assert.Same (inner, Control.FromScreenPoint (point));

            overlay.Close ();
        }
    }

    [Fact]
    public void Skips_a_hidden_control_and_reports_what_is_behind_it ()
    {
        var (form, outer, inner) = Nested ();

        using (form) {
            inner.Visible = false;

            var point = inner.PointToScreen (new Point (5, 5));

            Assert.Same (outer, Control.FromScreenPoint (point));
        }
    }
}
