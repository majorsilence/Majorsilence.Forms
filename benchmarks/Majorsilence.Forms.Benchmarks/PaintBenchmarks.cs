using System.Drawing;
using BenchmarkDotNet.Attributes;
using ControlGallery.Panels;

namespace Majorsilence.Forms.Benchmarks;

// End-to-end repaint cost for the four drawing-heavy gallery panels, through the real
// Control/Form/paint pipeline (per-control back-buffer caching, PaintChildren compositing) rather
// than isolated Graphics calls -- this is what a continuously-animating panel actually pays per
// tick, since each panel's Timer invalidates and repaints on every frame. Invalidate() is called
// before every render because rendering only re-runs OnPaint for controls whose NeedsPaint flag
// is set (Control.cs's per-control back-buffer cache) -- otherwise a repeat render would just
// re-blit the previous frame's cached buffers, measuring almost nothing. Frames are rendered, not
// PNG-encoded (see Frame).
[MemoryDiagnoser]
public class PaintBenchmarks
{
    private Form gameOfLifeForm = null!;
    private GameOfLifePanel gameOfLifePanel = null!;

    private Form fractalsForm = null!;
    private FractalsPanel fractalsPanel = null!;

    private Form binaryRainForm = null!;
    private BinaryRainPanel binaryRainPanel = null!;

    private Form sortingForm = null!;
    private SortingVisualizerPanel sortingPanel = null!;

    [GlobalSetup]
    public void Setup ()
    {
        (gameOfLifeForm, gameOfLifePanel) = Build (new GameOfLifePanel ());
        (fractalsForm, fractalsPanel) = Build (new FractalsPanel ());
        (binaryRainForm, binaryRainPanel) = Build (new BinaryRainPanel ());
        (sortingForm, sortingPanel) = Build (new SortingVisualizerPanel ());
    }

    private static (Form, T) Build<T> (T panel) where T : Panel
    {
        var form = new Form { ClientSize = new Size (900, 700) };
        panel.Dock = DockStyle.Fill;
        form.Controls.Add (panel);
        form.Show ();
        return (form, panel);
    }

    // Cell-grid fill: hundreds of Graphics.FillRectangle calls per repaint.
    [Benchmark]
    public void GameOfLifeRepaint ()
    {
        gameOfLifePanel.Invalidate ();
        Frame.Render (gameOfLifeForm);
    }

    // Whichever fractal is currently selected (Mandelbrot by default -- a full Bitmap.SetPixel
    // sweep, the heaviest of the four panels).
    [Benchmark]
    public void FractalsRepaint ()
    {
        fractalsPanel.Invalidate ();
        Frame.Render (fractalsForm);
    }

    // On the order of a thousand Graphics.DrawString calls per repaint.
    [Benchmark]
    public void BinaryRainRepaint ()
    {
        binaryRainPanel.Invalidate ();
        Frame.Render (binaryRainForm);
    }

    // 64 Graphics.FillRectangle calls per repaint -- the lightest of the four, included as a
    // low-end reference point for the other three.
    [Benchmark]
    public void SortingVisualizerRepaint ()
    {
        sortingPanel.Invalidate ();
        Frame.Render (sortingForm);
    }
}
