using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Majorsilence.Forms.Benchmarks;

// The first headless measurement in a new process: what a report server pays once, and what a command-line
// export pays every time. Each launch is a fresh process measuring exactly one call, so this is the cold cost
// (static constructors, the font manager, RichTextKit's mapper), not the steady state.
//
// Not in the allocation gate: a cold call's allocation is mostly one-time static state.
[SimpleJob (RunStrategy.ColdStart, launchCount: 10, warmupCount: 0, iterationCount: 1)]
[MemoryDiagnoser]
public class StartupBenchmarks
{
    [Benchmark]
    public float FirstMeasureString ()
    {
        using var bitmap = new Bitmap (200, 50);
        using var g = Graphics.FromImage (bitmap);
        using var font = new Font ("Arial", 10f);
        return g.MeasureString ("Customer number 4821", font).Width;
    }

    [Benchmark]
    public float FirstMeasureRunWidth ()
    {
        using var bitmap = new Bitmap (200, 50);
        using var g = Graphics.FromImage (bitmap);
        using var font = new Font ("Arial", 10f);
        return g.MeasureRunWidth ("Customer number 4821", font);
    }
}
