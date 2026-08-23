using System.Drawing;
using BenchmarkDotNet.Attributes;

namespace Majorsilence.Forms.Benchmarks;

// Isolated Graphics-primitive costs, with no Control/Form/layout involved -- Graphics.FromImage
// draws directly onto a real Bitmap's backing SKCanvas, so these are real Skia draw calls, just
// without the control-tree machinery around them. Mirrors what the Game of Life, Fractals, and
// Binary Rain gallery panels do many times per repaint: DrawLine (Fractals' recursive branches),
// FillRectangle (Game of Life's cells), FillPolygon (Fractals' Sierpinski triangles), and
// DrawString (Binary Rain's falling glyphs).
[MemoryDiagnoser]
public class DrawingBenchmarks
{
    private const int CanvasSize = 512;
    private const int Iterations = 1000;

    private Bitmap bitmap = null!;
    private Graphics g = null!;
    private Pen pen = null!;
    private SolidBrush brush = null!;
    private Font font = null!;

    [GlobalSetup]
    public void Setup ()
    {
        bitmap = new Bitmap (CanvasSize, CanvasSize);
        g = Graphics.FromImage (bitmap);
        pen = new Pen (Color.Black, 2);
        brush = new SolidBrush (Color.CornflowerBlue);
        font = new Font ("Arial", 12);
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        g.Dispose ();
        bitmap.Dispose ();
        pen.Dispose ();
    }

    // Graphics.DrawLine inlines "new SKPaint{...}" per call (does not even go through
    // Pen.CreatePaint) -- see src/Majorsilence.Forms/Graphics.cs around DrawLine.
    [Benchmark]
    public void DrawLine ()
    {
        for (var i = 0; i < Iterations; i++)
            g.DrawLine (pen, 0, 0, i % CanvasSize, (i * 3) % CanvasSize);
    }

    // Routes through Brush.CreateFillPaint -- a different allocation path than DrawLine's inline
    // "new SKPaint{}", but still one fresh SKPaint per call.
    [Benchmark]
    public void FillRectangle ()
    {
        for (var i = 0; i < Iterations; i++)
            g.FillRectangle (brush, i % CanvasSize, (i * 2) % CanvasSize, 4, 4);
    }

    // Same shape of per-call cost as the Sierpinski/Koch recursive fills in the Fractals panel.
    [Benchmark]
    public void FillPolygon ()
    {
        var points = new PointF[3];

        for (var i = 0; i < Iterations; i++) {
            var x = i % CanvasSize;
            var y = (i * 2) % CanvasSize;
            points[0] = new PointF (x, y);
            points[1] = new PointF (x + 10, y);
            points[2] = new PointF (x, y + 10);
            g.FillPolygon (brush, points);
        }
    }

    // Graphics.DrawString currently allocates "new SKFont(face, font.PixelSize)" per call even
    // though the Font it was given already lazily caches an identical SKFont instance via
    // Font.GetSKFont() (populated as a side effect of the TypefaceCache.Resolve call directly
    // above it). Binary Rain draws on the order of a thousand glyphs per repaint at full trail
    // length, so this is a real per-frame cost.
    [Benchmark]
    public void DrawString ()
    {
        for (var i = 0; i < Iterations; i++)
            g.DrawString ("01", font, brush, i % CanvasSize, (i * 2) % CanvasSize);
    }
}
