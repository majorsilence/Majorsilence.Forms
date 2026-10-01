using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// EVT-37 / CTL-10: application paint code draws in LOGICAL units, the units Width, Height, ClientRectangle
// and MouseEventArgs are in. The canvas handed to OnPaint was device pixels, so the ordinary WinForms
// `e.Graphics.DrawRectangle (pen, 0, 0, Width - 1, Height - 1)` framed only the top-left quarter of a
// control at 200%; ClientRectangle and ClientSize were device pixels too, the one exception in the bounds
// family. Every test here paints or measures at an explicit scale of 2, so none depends on the gate shape.
[Collection ("Headless")]
public sealed class LogicalPaintingTests : IDisposable
{
    private const int Scale = 2;
    private readonly double original_ui_scale = Application.UiScale;

    public LogicalPaintingTests () => HeadlessRenderer.Use ();

    public void Dispose () => Application.UiScale = original_ui_scale;

    private sealed class Framed : Control
    {
        public Rectangle? Clip;

        protected override void OnPaintBackground (PaintEventArgs e) => e.Canvas.Clear (SKColors.White);

        protected override void OnPaint (PaintEventArgs e)
        {
            Clip = e.ClipRectangle;
            using var pen = new Majorsilence.Forms.Drawing.Pen (Color.Red, 1);
            e.Graphics.DrawRectangle (pen, 0, 0, Width - 1, Height - 1);
        }
    }

    private static bool IsRed (SKColor c) => c.Red > 200 && c.Green < 80 && c.Blue < 80;

    [Fact]
    public void An_OnPaint_frame_in_Width_and_Height_frames_the_whole_control ()
    {
        var control = new Framed { Width = 100, Height = 40 };
        using var bitmap = PaintSurface.RenderOnForm (control, Scale);

        // The frame's far edges land on the far edges of the device bitmap, not halfway across it.
        // A 1-unit pen centred on logical Width - 1 is two device pixels centred on 2 * (Width - 1).
        Assert.True (IsRed (bitmap.GetPixel (bitmap.Width - 2, bitmap.Height / 2)), "the right edge of the frame should be at the right of the control");
        Assert.True (IsRed (bitmap.GetPixel (bitmap.Width / 2, bitmap.Height - 2)), "the bottom edge should be at the bottom");
        Assert.False (IsRed (bitmap.GetPixel (bitmap.Width / 2 - 1, bitmap.Height / 2 - 1)), "a device-pixel canvas would put the frame's corner in the middle");
    }

    [Fact]
    public void ClipRectangle_is_logical_inside_OnPaint ()
    {
        var control = new Framed { Width = 100, Height = 40 };
        PaintSurface.RenderOnForm (control, Scale).Dispose ();

        Assert.Equal (new Rectangle (0, 0, 100, 40), control.Clip);
    }

    [Fact]
    public void A_Paint_handler_draws_in_logical_units_and_a_transform_it_leaves_does_not_reach_the_children ()
    {
        var parent = new Panel { Width = 100, Height = 60, BackColor = Color.White };
        var child = new Panel { Left = 50, Top = 30, Width = 50, Height = 30, BackColor = Color.Blue };
        parent.Controls.Add (child);

        parent.Paint += (_, e) => {
            e.Graphics.FillRectangle (Majorsilence.Forms.Drawing.Brushes.Red, 0, 0, 10, 10);
            e.Graphics.ScaleTransform (3, 3);   // left on the canvas
        };

        // A real 2x window rather than an explicit render scale: children composite at their own scale.
        Application.UiScale = Scale;
        using var form = new Form { Width = 500, Height = 400 };
        form.Controls.Add (parent);
        form.Show ();
        // UiScale multiplies into the backend's own scale, so under the scale-2 gate this is 4.
        var scale = (int) parent.Scaling;
        Assert.True (scale >= Scale);
        using var bitmap = PaintSurface.Render (parent);

        Assert.True (IsRed (bitmap.GetPixel (10 * scale - 1, 10 * scale - 1)), "a 10-unit square is 10 * scale device pixels wide");
        Assert.False (IsRed (bitmap.GetPixel (10 * scale + 1, 10 * scale + 1)));

        // The child is composited where its logical bounds say, unaffected by the handler's transform.
        var inChild = bitmap.GetPixel (50 * scale + 10, 30 * scale + 10);
        Assert.True (inChild.Blue > 200 && inChild.Red < 80, $"the child should be at its own position, got {inChild}");
    }

    // A subclass that draws after base.OnPaint: the built-in renderer underneath still lays out in device
    // pixels, and the subclass's own drawing is logical.
    private sealed class DecoratedButton : Button
    {
        public bool Decorate { get; init; }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (Decorate)
                e.Graphics.FillRectangle (Majorsilence.Forms.Drawing.Brushes.Red, Width - 6, 0, 6, 6);
        }
    }

    [Fact]
    public void A_built_in_renderer_under_a_subclass_still_draws_in_device_pixels ()
    {
        using var plain = PaintSurface.RenderOnForm (new DecoratedButton { Width = 120, Height = 40, Text = "Save" }, Scale);
        using var decorated = PaintSurface.RenderOnForm (new DecoratedButton { Width = 120, Height = 40, Text = "Save", Decorate = true }, Scale);

        // Identical away from the decoration: the base button rendered the same either way.
        for (var x = 0; x < plain.Width - 6 * Scale; x += 3)
            for (var y = 6 * Scale; y < plain.Height; y += 3)
                Assert.Equal (plain.GetPixel (x, y), decorated.GetPixel (x, y));

        // And the decoration sits in the top-right 6x6 LOGICAL units -- 12 device pixels.
        Assert.True (IsRed (decorated.GetPixel (decorated.Width - 2, 2)));
        Assert.True (IsRed (decorated.GetPixel (decorated.Width - 6 * Scale + 1, 6 * Scale - 2)));
    }

    [Fact]
    public void ClientRectangle_and_ClientSize_are_in_the_same_units_as_Width_and_Height ()
    {
        Application.UiScale = Scale;
        var panel = new Panel { Width = 200, Height = 100, BorderStyle = BorderStyle.None };
        using var form = new Form { Width = 500, Height = 400 };
        form.Controls.Add (panel);
        form.Show ();

        Assert.True (panel.ScaleFactor.Width >= 2);   // really scaled (UiScale multiplies into the backend's own)
        Assert.Equal (new Rectangle (0, 0, 200, 100), panel.ClientRectangle);
        Assert.Equal (new Size (200, 100), panel.ClientSize);

        // And the setter: asking for a client size gives exactly that inside the border.
        panel.BorderStyle = BorderStyle.FixedSingle;
        panel.ClientSize = new Size (150, 80);
        Assert.Equal (new Size (150, 80), panel.ClientSize);
        Assert.Equal (panel.Width - panel.ClientSize.Width, panel.Height - panel.ClientSize.Height);   // a symmetric border
    }

    [Fact]
    public void A_forms_own_Paint_handler_draws_in_logical_units ()
    {
        // The window renders itself through RenderFrame, driven here at an explicit 2x -- the headless
        // capture uses the backend's own scale, which UiScale does not reach.
        using var form = new Form { Width = 300, Height = 200, FormBorderStyle = FormBorderStyle.None, BackColor = Color.White };
        form.Paint += (_, e) => e.Graphics.FillRectangle (Majorsilence.Forms.Drawing.Brushes.Red, 0, 0, 20, 20);
        form.Show ();

        using var bitmap = new SKBitmap (300 * Scale, 200 * Scale);
        using (var canvas = new SKCanvas (bitmap))
            form.RenderFrame (canvas, bitmap.Width, bitmap.Height, Scale);

        Assert.True (IsRed (bitmap.GetPixel (20 * Scale - 2, 20 * Scale - 2)), "a 20-unit square is 40 device pixels wide at 2x");
        Assert.False (IsRed (bitmap.GetPixel (20 * Scale + 2, 20 * Scale + 2)));
    }

    [Theory]
    [InlineData (typeof (Button))]
    [InlineData (typeof (Panel))]
    public void Built_in_chrome_still_reaches_the_far_edges (System.Type type)
    {
        // The library's own background, border and renderers are laid out in device pixels and drawn in
        // a device scope inside application paint code. Drawn in the logical scope instead they would be
        // scaled a second time, and the far border would land off the canvas: the left and right edges
        // (and top and bottom) would stop matching.
        var control = (Control) System.Activator.CreateInstance (type)!;
        control.Width = 120;
        control.Height = 40;
        if (control is Panel panel)
            panel.BorderStyle = BorderStyle.FixedSingle;

        // A real scaled window, so the control's device-pixel geometry uses the same scale as the canvas.
        Application.UiScale = Scale;
        using var form = new Form { Width = 500, Height = 400 };
        form.Controls.Add (control);
        form.Show ();
        Assert.True (control.Scaling >= Scale);

        using var bitmap = PaintSurface.Render (control);
        var midY = bitmap.Height / 2;
        var midX = bitmap.Width / 3;

        Assert.Equal (bitmap.GetPixel (0, midY), bitmap.GetPixel (bitmap.Width - 1, midY));
        Assert.Equal (bitmap.GetPixel (midX, 0), bitmap.GetPixel (midX, bitmap.Height - 1));
        Assert.NotEqual (bitmap.GetPixel (0, midY), bitmap.GetPixel (midX, midY));   // the edge is a border, not just fill
    }

    [Fact]
    public void A_renderers_caption_stays_centred ()
    {
        // The renderer (not the background) draws a button's caption, centred in device pixels. Run in
        // the logical scope, it would be scaled again and the caption would sit at or past the right edge.
        Application.UiScale = Scale;
        var button = new Button { Width = 160, Height = 40, Text = "WWWW" };
        using var form = new Form { Width = 500, Height = 400 };
        form.Controls.Add (button);
        form.Show ();

        using var bitmap = PaintSurface.Render (button);
        long sum = 0, count = 0;
        for (var y = 2; y < bitmap.Height - 2; y++)
            for (var x = 2; x < bitmap.Width - 2; x++)
                if (bitmap.GetPixel (x, y) is { Red: < 100, Green: < 100, Blue: < 100 }) {
                    sum += x;
                    count++;
                }

        Assert.True (count > 0, "the caption should have been drawn");
        var centre = (double) sum / count / bitmap.Width;
        Assert.InRange (centre, 0.4, 0.6);
    }
}
