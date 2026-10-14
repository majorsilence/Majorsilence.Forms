using System.Drawing;
using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Majorsilence.Forms.Benchmarks;

// TextMeasurer's TextBlock cache, the way a report or a big grid uses it: a working set of strings measured
// again and again (column headers, repeated values) while a stream of strings seen once (row values) passes
// through. A cache that empties itself wholesale when it fills loses the working set every time the stream
// fills it; one that evicts by age keeps it.
[MemoryDiagnoser]
public class TextMeasurerCacheBenchmarks
{
    private const int HotSetSize = 1000;
    private const int OpsPerInvoke = 4000;

    private SKTypeface typeface = null!;
    private string[] hot = null!;
    private string[] stream = null!;
    private int next;

    [GlobalSetup]
    public void Setup ()
    {
        typeface = SKTypeface.Default;
        hot = Enumerable.Range (0, HotSetSize).Select (i => $"Header {i}").ToArray ();
        // Plenty of distinct strings so the stream never repeats within a run.
        stream = Enumerable.Range (0, 1_000_000).Select (i => $"Row value {i}").ToArray ();
        foreach (var text in hot)
            TextMeasurer.MeasureText (text, typeface, 12);
    }

    // Every call a cache hit: the cost of building the key and looking it up.
    [Benchmark (OperationsPerInvoke = OpsPerInvoke)]
    [BenchmarkCategory (AllocationGate.Category)]
    public float Hit ()
    {
        var total = 0f;
        for (var i = 0; i < OpsPerInvoke; i++)
            total += TextMeasurer.MeasureText (hot[i % 16], typeface, 12).Width;
        return total;
    }

    // The single-line path DrawText takes for a Label or a cell: the same hit, plus the newline handling.
    [Benchmark (OperationsPerInvoke = OpsPerInvoke)]
    [BenchmarkCategory (AllocationGate.Category)]
    public float HitSingleLine ()
    {
        var total = 0f;
        for (var i = 0; i < OpsPerInvoke; i++)
            total += TextMeasurer.CreateTextBlock (hot[i % 16], typeface, 12, new Size (300, 30), maxLines: 1).MeasuredWidth;
        return total;
    }

    // Three hot measurements for every new string: whether the hot set survives the stream. Not gated: what it
    // allocates depends on how far through the stream a run has got.
    [Benchmark (OperationsPerInvoke = OpsPerInvoke)]
    public float HotSetWithChurn ()
    {
        var total = 0f;
        for (var i = 0; i < OpsPerInvoke; i++) {
            var text = (i & 3) == 3 ? stream[next++ % stream.Length] : hot[(i * 7) % HotSetSize];
            total += TextMeasurer.MeasureText (text, typeface, 12).Width;
        }
        return total;
    }
}
