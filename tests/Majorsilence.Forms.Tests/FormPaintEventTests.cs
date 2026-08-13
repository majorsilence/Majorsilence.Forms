using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// In WinForms, Form derives from Control, so `form.Paint += handler` is a supported -- and in ported
// game/custom-rendering code, very common -- way to draw on a form. Majorsilence.Forms' Form derives
// from WindowBase, which is NOT a Control and so inherits none of Control's paint pipeline, so the
// Paint event WindowBase declares has to be raised by its own paint pass. It previously was not: the
// event existed, `form.Paint += handler` compiled, and the handler silently never ran.
public class FormPaintEventTests
{
    private static readonly SKColor HandlerFill = new SKColor (255, 0, 0);

    [Fact]
    public void PaintEvent_IsRaisedDuringTheWindowPaintPass ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Size = new System.Drawing.Size (200, 150) };
        var raised = 0;
        form.Paint += (s, e) => raised++;

        form.Show ();
        HeadlessRenderer.CapturePng (form);

        Assert.True (raised > 0, "Form.Paint was never raised");
    }

    [Fact]
    public void PaintEvent_DrawingReachesTheSurface ()
    {
        // Raising the event is not enough on its own -- whatever the handler draws has to survive the
        // rest of the pass. The client area's background is painted after the window's OnPaint, so a
        // handler invoked too early would be silently erased before anything reached the screen.
        HeadlessRenderer.Use ();

        using var form = new Form { Size = new System.Drawing.Size (200, 150) };
        form.Paint += (s, e) => e.Canvas.Clear (HandlerFill);

        form.Show ();
        var png = HeadlessRenderer.CapturePng (form);
        using var bmp = SKBitmap.Decode (png);

        Assert.Equal (HandlerFill, bmp.GetPixel (bmp.Width / 2, bmp.Height / 2));
    }

    [Fact]
    public void PaintEvent_DoesNotSuppressChildControls ()
    {
        // WinForms z-order: a child control's HWND occludes the parent's client-area painting, so a
        // Form.Paint handler must draw UNDER its controls, never over them.
        HeadlessRenderer.Use ();

        using var form = new Form { Size = new System.Drawing.Size (240, 160) };
        form.Paint += (s, e) => e.Canvas.Clear (HandlerFill);

        var button = new Button { Text = "Child", Left = 40, Top = 40, Width = 120, Height = 50 };
        form.Controls.Add (button);

        form.Show ();
        var png = HeadlessRenderer.CapturePng (form);
        using var bmp = SKBitmap.Decode (png);

        // Assert the handler's fill actually landed somewhere first. Without it, "the button is not
        // red" would also hold when Paint never ran at all, and the test would pass vacuously.
        Assert.Equal (HandlerFill, bmp.GetPixel (button.Left + button.Width + 20, button.Top));

        Assert.NotEqual (HandlerFill, bmp.GetPixel (button.Left + button.Width / 2,
                                                    button.Top + button.Height / 2));
    }
}
