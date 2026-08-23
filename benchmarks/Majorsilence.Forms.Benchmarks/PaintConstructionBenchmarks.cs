using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Majorsilence.Forms.Benchmarks;

// Isolates the cost of building an SKPaint from the cost of the Skia draw call it feeds, to put a
// ceiling on what pooling SKPaint instances (instead of allocating fresh ones in Pen.CreatePaint /
// Brush.CreatePaint / the inlined "new SKPaint{}" sites in Graphics.cs) could possibly save. If
// construction is a small fraction of a real draw call's cost, pooling ~40 call sites for a
// "fresh, caller-owned" API isn't worth the risk; if it's a large fraction, it is.
[MemoryDiagnoser]
public class PaintConstructionBenchmarks
{
    private const int Iterations = 1000;

    private SKPaint reusablePaint = null!;
    private SKBitmap bitmap = null!;
    private SKCanvas canvas = null!;

    [GlobalSetup]
    public void Setup ()
    {
        reusablePaint = new SKPaint { Color = SKColors.CornflowerBlue, Style = SKPaintStyle.Fill, IsAntialias = true };
        bitmap = new SKBitmap (512, 512);
        canvas = new SKCanvas (bitmap);
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        reusablePaint.Dispose ();
        canvas.Dispose ();
        bitmap.Dispose ();
    }

    // Matches SolidBrush.CreatePaint's shape exactly -- the most common CreatePaint call site.
    [Benchmark (Baseline = true)]
    public void AllocateFreshPaint ()
    {
        for (var i = 0; i < Iterations; i++) {
            using var paint = new SKPaint { Color = SKColors.CornflowerBlue, Style = SKPaintStyle.Fill, IsAntialias = true };
        }
    }

    // What a pool would give a caller instead: the same paint object, fields reassigned each time.
    [Benchmark]
    public void ReconfigurePooledPaint ()
    {
        for (var i = 0; i < Iterations; i++) {
            reusablePaint.Color = SKColors.CornflowerBlue;
            reusablePaint.Style = SKPaintStyle.Fill;
            reusablePaint.IsAntialias = true;
        }
    }

    // The actual Skia draw call a fresh paint feeds -- the thing pooling would NOT speed up. Compare
    // this against AllocateFreshPaint to see what fraction of a real FillRect is paint construction.
    [Benchmark]
    public void DrawRectWithFreshPaint ()
    {
        for (var i = 0; i < Iterations; i++) {
            using var paint = new SKPaint { Color = SKColors.CornflowerBlue, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRect (i % 512, (i * 2) % 512, 4, 4, paint);
        }
    }

    [Benchmark]
    public void DrawRectWithPooledPaint ()
    {
        for (var i = 0; i < Iterations; i++) {
            reusablePaint.Color = SKColors.CornflowerBlue;
            reusablePaint.Style = SKPaintStyle.Fill;
            reusablePaint.IsAntialias = true;
            canvas.DrawRect (i % 512, (i * 2) % 512, 4, 4, reusablePaint);
        }
    }
}
