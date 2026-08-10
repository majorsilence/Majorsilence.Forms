using SkiaSharp;

namespace Majorsilence.Forms.Tests;

// Renders a control straight into a bitmap of its own size, so pixel coordinates in a test are
// control coordinates. Going through a Form and HeadlessRenderer would work too, but then every
// assertion has to compensate for the window chrome, and a caption strip full of non-background
// pixels quietly breaks any ink-bounds measurement.
internal static class PaintSurface
{
    /// <summary>
    /// Attaches the control to a form before rendering it. Control.Visible walks up to its parent
    /// and a parentless control reports false, so the child paint pass would skip every child of a
    /// detached parent -- a test about child painting has to own a form even though it renders the
    /// control directly rather than capturing the window.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT call Form.Show: adding to the form's control collection is enough to
    /// make the chain visible, and a shown window keeps the test host alive at the end of the run.
    /// </remarks>
    public static SKBitmap RenderOnForm (Control control, float scaling = 1f)
    {
        var form = new Form { Width = control.Width + 80, Height = control.Height + 80 };
        form.Controls.Add (control);

        return Render (control, scaling);
    }

    public static SKBitmap Render (Control control, float scaling = 1f)
    {
        var info = new SKImageInfo (
            (int)(control.Width * scaling),
            (int)(control.Height * scaling),
            SKImageInfo.PlatformColorType,
            SKAlphaType.Premul);

        var bitmap = new SKBitmap (info);

        using (var canvas = new SKCanvas (bitmap)) {
            var args = new PaintEventArgs (info, canvas, scaling);

            control.RaisePaintBackground (args);
            control.RaisePaint (args);

            canvas.Flush ();
        }

        return bitmap;
    }
}
