using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="Cursor.Position"/> must report where the pointer actually is, because
/// <see cref="Control.MousePosition"/> reads through to it.
/// </summary>
/// <remarks>
/// It was a stored property nothing ever assigned, so it always read (0, 0). Any control that
/// hit-tests the pointer without being handed a MouseEventArgs -- the WinForms
/// <c>HitTest (PointToClient (Control.MousePosition))</c> idiom -- therefore tested the top-left corner
/// of the screen and matched nothing. In a docking library that is how a tab strip decides which tab was
/// clicked, so document tabs could not be switched at all: every click resolved to "no tab".
/// </remarks>
[Collection ("Headless")]
public class CursorPositionTrackingTests
{
    [Fact]
    public void A_click_updates_Cursor_Position ()
    {
        HeadlessRenderer.Use ();
        Cursor.Position = Point.Empty;

        using var form = new Form { Width = 300, Height = 200 };
        var panel = new Panel { Left = 10, Top = 10, Width = 200, Height = 100 };
        form.Controls.Add (panel);

        HeadlessRenderer.CapturePng (form, 300, 200);   // lay out first
        HeadlessRenderer.Click (form, 60, 70);

        Assert.NotEqual (Point.Empty, Cursor.Position);
    }

    [Fact]
    public void A_move_updates_Cursor_Position ()
    {
        HeadlessRenderer.Use ();
        Cursor.Position = Point.Empty;

        using var form = new Form { Width = 300, Height = 200 };
        HeadlessRenderer.CapturePng (form, 300, 200);
        HeadlessRenderer.MouseMove (form, 123, 45);

        Assert.NotEqual (Point.Empty, Cursor.Position);
    }

    [Fact]
    public void Control_MousePosition_reads_through_to_Cursor_Position ()
    {
        Cursor.Position = new Point (321, 234);
        Assert.Equal (new Point (321, 234), Control.MousePosition);
    }

    [Fact]
    public void The_tracked_position_follows_the_pointer_between_clicks ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 400, Height = 300 };
        HeadlessRenderer.CapturePng (form, 400, 300);

        HeadlessRenderer.Click (form, 40, 40);
        var first = Cursor.Position;

        HeadlessRenderer.Click (form, 200, 150);
        var second = Cursor.Position;

        // Two different points must not report the same position -- a stale value is what broke
        // hit-testing in the first place.
        Assert.NotEqual (first, second);
    }

    [Fact]
    public void PointToClient_of_the_tracked_position_lands_inside_the_clicked_control ()
    {
        // The whole idiom, end to end: click a child, then resolve Control.MousePosition back into that
        // child's client coordinates and confirm it hits.
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 400, Height = 300 };
        var panel = new Panel { Left = 50, Top = 40, Width = 200, Height = 100 };
        form.Controls.Add (panel);

        HeadlessRenderer.CapturePng (form, 400, 300);
        HeadlessRenderer.Click (form, 100, 80);

        var local = panel.PointToClient (Control.MousePosition);

        Assert.True (panel.ClientRectangle.Contains (local),
            $"{local} should be inside the panel's client rectangle {panel.ClientRectangle}");
    }
}
