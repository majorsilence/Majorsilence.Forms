using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// An application can claim a caption drag for itself: <see cref="WindowBase.CaptionDragStarting"/> is
/// raised before the window starts moving, and cancelling it leaves the window where it is.
/// </summary>
/// <remarks>
/// This is the portable stand-in for intercepting <c>WM_NCLBUTTONDOWN</c> over <c>HTCAPTION</c> — how
/// WinForms code takes over a title-bar drag. DockPanelSuite's FloatWindow does exactly that so dragging
/// a floating document re-docks it; with no equivalent here the press went straight to the window move
/// and the docking library never learned a drag had begun, which made a floated document impossible to
/// dock back by hand.
///
/// It only applies to a caption this library draws. A window on the operating system's title bar
/// (<see cref="Form.UseSystemDecorations"/>) never sees the press — the OS moves it — which is why a
/// window wanting this has to own its caption.
/// </remarks>
[Collection ("Headless")]
public class CaptionDragTests
{
    private static Form CaptionedForm ()
    {
        HeadlessRenderer.Use ();

        var form = new Form { Width = 400, Height = 300, UseSystemDecorations = false };
        form.Show ();
        HeadlessRenderer.CapturePng (form, 400, 300);

        return form;
    }

    private static void PressTheCaption (Form form)
    {
        // Through the real input path, so this exercises the same routing a click does.
        var bar = form.TitleBar;
        HeadlessRenderer.MouseDown (form, bar.Width / 2, bar.Height / 2);
    }

    [Fact]
    public void Dragging_the_caption_raises_CaptionDragStarting ()
    {
        using var form = CaptionedForm ();

        var raised = 0;
        form.CaptionDragStarting += (_, _) => raised++;

        PressTheCaption (form);

        Assert.Equal (1, raised);
    }

    [Fact]
    public void An_unclaimed_caption_drag_moves_the_window ()
    {
        using var form = CaptionedForm ();
        HeadlessRenderer.MoveDragCount = 0;

        PressTheCaption (form);

        Assert.Equal (1, HeadlessRenderer.MoveDragCount);
    }

    [Fact]
    public void A_claimed_caption_drag_leaves_the_window_alone ()
    {
        using var form = CaptionedForm ();
        HeadlessRenderer.MoveDragCount = 0;

        form.CaptionDragStarting += (_, e) => e.Cancel = true;

        PressTheCaption (form);

        Assert.Equal (0, HeadlessRenderer.MoveDragCount);
    }

    [Fact]
    public void The_drag_reports_where_in_the_caption_it_started ()
    {
        using var form = CaptionedForm ();

        Point? where = null;
        form.CaptionDragStarting += (_, e) => where = e.Location;

        PressTheCaption (form);

        Assert.NotNull (where);
        Assert.Equal (form.TitleBar.Width / 2, where!.Value.X);
    }
}
