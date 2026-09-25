using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, seventeenth chunk: more empty-bodied public methods -- the ISupportInitialize pairs
// that were meant to batch a designer's property assignments, and four overloads that did nothing
// while their siblings worked.
[Collection ("Headless")]
public class W6InitAndOverloadTests
{
    // ── ISupportInitialize ──────────────────────────────────────────────────────────────────────────

    private sealed class CountingTrackBar : TrackBar
    {
        internal int Layouts;

        protected override void OnLayout (LayoutEventArgs e)
        {
            Layouts++;
            base.OnLayout (e);
        }
    }

    private sealed class CountingSpinner : NumericUpDown
    {
        internal int Layouts;

        protected override void OnLayout (LayoutEventArgs e)
        {
            Layouts++;
            base.OnLayout (e);
        }
    }

    [Fact]
    public void BeginInit_holds_the_layout_a_batch_of_properties_would_cause ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var bar = new CountingTrackBar { Width = 200, Height = 40 };
        form.Controls.Add (bar);
        form.Show ();

        bar.Layouts = 0;

        bar.BeginInit ();
        bar.Width = 180;
        bar.Height = 50;
        bar.Minimum = 5;
        bar.Maximum = 50;

        // Nothing has been laid out yet: the designer's assignments are one batch.
        Assert.Equal (0, bar.Layouts);

        bar.EndInit ();

        Assert.True (bar.Layouts > 0, "EndInit should lay the control out once");
    }

    [Fact]
    public void The_spinner_pair_batches_the_same_way ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var spinner = new CountingSpinner { Width = 120, Height = 24 };
        form.Controls.Add (spinner);
        form.Show ();

        spinner.Layouts = 0;

        spinner.BeginInit ();
        spinner.Width = 100;
        spinner.Height = 30;
        Assert.Equal (0, spinner.Layouts);

        spinner.EndInit ();
        Assert.True (spinner.Layouts > 0, "EndInit should lay the control out once");
    }

    [Fact]
    public void A_status_bar_panel_defers_its_bars_layout ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        var bar = new StatusBar { Width = 300, Height = 24 };
        var panel = new StatusBarPanel { Text = "ready" };
        bar.Panels.Add (panel);
        form.Controls.Add (bar);
        form.Show ();

        // A panel is not a control, so what the pair tracks is the batch itself.
        Assert.False (panel.Initialising);

        panel.BeginInit ();
        Assert.True (panel.Initialising);

        panel.Text = "working";
        panel.Width = 120;

        panel.EndInit ();
        Assert.False (panel.Initialising);
        Assert.Equal ("working", panel.Text);
    }

    // ── overloads that did nothing ──────────────────────────────────────────────────────────────────

    // Flush forwards to the canvas, which is the right implementation for a GPU-backed surface. This
    // test cannot PROVE it: a raster surface has its pixels the moment they are drawn, so neutralizing
    // Flush changes nothing observable here. It pins the forwarding and the argument handling, and the
    // claim is recorded as forwarded rather than verified.
    [Fact]
    public void Graphics_Flush_forwards_to_the_canvas_and_leaves_the_drawing_intact ()
    {
        using var surface = new SKBitmap (20, 20, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new Majorsilence.Forms.Drawing.Bitmap (surface);
        using var graphics = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
        using var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.Red);

        graphics.FillRectangle (brush, 0, 0, 20, 20);

        // Both overloads reach the canvas and neither throws; the drawing is intact afterwards.
        graphics.Flush ();
        graphics.Flush (Majorsilence.Forms.Drawing.Drawing2D.FlushIntention.Sync);

        Assert.Equal (SKColors.Red, surface.GetPixel (10, 10));

        // A Graphics with no canvas behind it still answers both calls.
        using var detached = new Majorsilence.Forms.Drawing.Graphics ();
        detached.Flush ();
        detached.Flush (Majorsilence.Forms.Drawing.Drawing2D.FlushIntention.Flush);
    }

    [Fact]
    public void TextRenderer_draws_into_a_canvas_as_well_as_into_paint_args ()
    {
        using var surface = new SKBitmap (120, 30, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas (surface);
        canvas.Clear (SKColors.White);

        TextRenderer.DrawText (canvas, "hello", Theme.UIFont, new Rectangle (0, 0, 120, 30), SKColors.Black);
        canvas.Flush ();

        var ink = 0;

        for (var y = 0; y < surface.Height; y++)
            for (var x = 0; x < surface.Width; x++)
                if (surface.GetPixel (x, y) != SKColors.White)
                    ink++;

        Assert.True (ink > 0, "the canvas overload drew nothing");
        Assert.Throws<ArgumentNullException> (() => TextRenderer.DrawText ((SKCanvas) null!, "x", Theme.UIFont, Rectangle.Empty, SKColors.Black));
    }

    [Fact]
    public void A_tip_set_on_a_window_lands_on_the_control_that_fills_it ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (300, 200) };
        form.Show ();

        using var tip = new ToolTip ();
        tip.SetToolTip (form, "the window's tip");

        // The tip is registered against the window's content, so the pointer over it finds one.
        Assert.Equal ("the window's tip", tip.GetToolTip (form.ContentRoot));
    }
}
