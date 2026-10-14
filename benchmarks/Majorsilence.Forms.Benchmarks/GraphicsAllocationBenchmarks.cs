using System.Drawing;
using BenchmarkDotNet.Attributes;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;
using MSRegion = Majorsilence.Forms.Drawing.Region;

namespace Majorsilence.Forms.Benchmarks;

// What a Graphics call allocates beyond the Skia draw itself, for the calls DrawingBenchmarks does not cover: the
// SKColor helpers the controls paint with, table borders, a gradient fill, a clip set from a Region, and a string.
[MemoryDiagnoser]
[BenchmarkCategory (AllocationGate.Category)]
public class GraphicsAllocationBenchmarks
{
    private const int Iterations = 1000;

    private Bitmap bitmap = null!;
    private Graphics g = null!;
    private LinearGradientBrush gradient = null!;
    private MSRegion region = null!;
    private Font font = null!;
    private Pen pen = null!;

    [GlobalSetup]
    public void Setup ()
    {
        bitmap = new Bitmap (512, 512);
        g = Graphics.FromImage (bitmap);
        gradient = new LinearGradientBrush (new Point (0, 0), new Point (100, 0), Color.White, Color.SteelBlue);
        region = new MSRegion (new RectangleF (10.5f, 10.5f, 200, 100));
        font = new Font ("Arial", 10f);
        pen = new Pen (Color.Black, 1f);
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        g.Dispose ();
        bitmap.Dispose ();
        gradient.Dispose ();
        region.Dispose ();
        font.Dispose ();
        pen.Dispose ();
    }

    // The internal SKColor overloads every control's OnPaint uses for backgrounds, borders and separators.
    [Benchmark]
    public void ColorHelpers ()
    {
        for (var i = 0; i < Iterations; i++) {
            var x = i % 400;
            g.FillRectangle (SKColors.CornflowerBlue, new Rectangle (x, x, 20, 10));
            g.DrawRectangle (SKColors.Black, new Rectangle (x, x, 20, 10));
            g.DrawLine (SKColors.Gray, new Point (x, 0), new Point (x, 100));
        }
    }

    // A report table's cell borders and a grid's lines: horizontal and vertical lines with a plain pen.
    [Benchmark]
    public void TableBorders ()
    {
        for (var i = 0; i < Iterations; i++) {
            var x = i % 400;
            g.DrawLine (pen, x, 0, x, 200);
            g.DrawLine (pen, 0, x, 300, x);
        }
    }

    [Benchmark]
    public void GradientFill ()
    {
        for (var i = 0; i < Iterations; i++)
            g.FillRectangle (gradient, i % 400, 0, 50, 20);
    }

    [Benchmark]
    public void SetClipRegion ()
    {
        for (var i = 0; i < Iterations; i++) {
            g.SetClip (region);
            g.ResetClip ();
        }
    }

    [Benchmark]
    public void DrawStringSolid ()
    {
        for (var i = 0; i < Iterations; i++)
            g.DrawString ("Total", font, Brushes.Black, i % 400, 20);
    }
}
