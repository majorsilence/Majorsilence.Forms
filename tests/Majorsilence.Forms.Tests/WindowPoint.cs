using System.Drawing;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Converts a control's own coordinates into the window-space coordinates the headless input and
/// capture helpers take.
/// </summary>
/// <remarks>
/// <para>
/// <c>HeadlessRenderer.Click</c>, <c>MouseDown</c> and the captured bitmap are all in WINDOW space —
/// measured from the top-left of the window, caption included. A control's <c>Left</c>/<c>Top</c> are
/// in its parent's client space. Those two coincided for a control added straight to a form only while
/// the form's client area started at the window origin, which stopped being true when the title bar
/// moved out of the client area (finding FRM-06): a child at (20, 20) is now 20px below the caption,
/// as it is in WinForms.
/// </para>
/// <para>
/// A test that hard-codes the sum of a control's own offsets is therefore both wrong off macOS and
/// silently right on it — the platform CI does not run. Walking the parent chain is correct on every
/// platform and stays correct if anything is ever inserted into or removed from it.
/// </para>
/// </remarks>
internal static class WindowPoint
{
    /// <summary>The window-space point at <paramref name="offset"/> within <paramref name="control"/>.</summary>
    internal static Point In (Control control, Point offset)
    {
        var x = offset.X;
        var y = offset.Y;

        for (var c = control; c is not null; c = c.Parent) {
            x += c.Left;
            y += c.Top;
        }

        return new Point (x, y);
    }

    /// <inheritdoc cref="In(Control, Point)"/>
    internal static Point In (Control control, int x, int y) => In (control, new Point (x, y));

    /// <summary>The window-space point at the centre of <paramref name="control"/>.</summary>
    internal static Point CentreOf (Control control)
        => In (control, control.Width / 2, control.Height / 2);

    /// <summary>
    /// <see cref="In(Control, int, int)"/> converted to DEVICE pixels, for the entry points that take
    /// them.
    /// </summary>
    /// <remarks>
    /// <c>WindowBase.HandleLongPress</c>, <c>HandlePinch</c>, <c>HandleSwipe</c> and
    /// <c>HandleScrollGesture</c> are the backend-facing callbacks and take device pixels, the way a
    /// real backend delivers them. <c>HeadlessRenderer</c>'s mouse helpers convert for you and take
    /// logical; these do not. Identical while the display factor is 1, which is what lets the mistake
    /// hide until MF_HEADLESS_SCALE=2 runs.
    /// </remarks>
    internal static Point DeviceIn (Control control, int x, int y)
    {
        var logical = In (control, x, y);
        var scale = control.Scaling;

        return new Point ((int) (logical.X * scale), (int) (logical.Y * scale));
    }
}

/// <summary>
/// The headless input helpers, taking a <see cref="Point"/> so a call site can pass what
/// <see cref="WindowPoint"/> computed without unpacking it.
/// </summary>
internal static class HeadlessInput
{
    internal static void Click (WindowBase w, Point p)
        => Majorsilence.Forms.Headless.HeadlessRenderer.Click (w, p.X, p.Y);

    internal static void MouseDown (WindowBase w, Point p)
        => Majorsilence.Forms.Headless.HeadlessRenderer.MouseDown (w, p.X, p.Y);

    internal static void MouseUp (WindowBase w, Point p)
        => Majorsilence.Forms.Headless.HeadlessRenderer.MouseUp (w, p.X, p.Y);

    internal static void MouseMove (WindowBase w, Point p)
        => Majorsilence.Forms.Headless.HeadlessRenderer.MouseMove (w, p.X, p.Y);
}
