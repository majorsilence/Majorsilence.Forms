using BenchmarkDotNet.Attributes;
using SkiaSharp;
using CharacterRange = Majorsilence.Forms.Drawing.CharacterRange;
using MSStringFormat = Majorsilence.Forms.Drawing.StringFormat;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Benchmarks;

// Text measurement on several threads at once, one Graphics and Font per thread, the way Majorsilence.Reporting
// renders reports on a server. Every thread makes the same 500 measurements, so a time that stays flat as
// Threads grows means the threads do not get in each other's way (a shared lock or a contended native handle
// table shows up as a rise).
//
// Not in the allocation gate: Parallel.For's own allocations depend on the thread pool.
[MemoryDiagnoser]
public class ConcurrentMeasureBenchmarks
{
    private const int OpsPerThread = 500;

    private static readonly string[] texts = [
        "Customer number 4821",
        "Quarterly results for the northern region",
        "Adjusted for seasonality and one-off items",
        "Total",
    ];

    private Bitmap[] bitmaps = null!;
    private Graphics[] graphics = null!;
    private Font[] fonts = null!;
    private MSStringFormat[] formats = null!;
    private SKFont[] skFonts = null!;
    private readonly RectangleF layout = new (0, 0, float.MaxValue, float.MaxValue);

    [Params (1, 4, 8)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        bitmaps = new Bitmap[Threads];
        graphics = new Graphics[Threads];
        fonts = new Font[Threads];
        formats = new MSStringFormat[Threads];
        skFonts = new SKFont[Threads];
        for (var t = 0; t < Threads; t++) {
            bitmaps[t] = new Bitmap (400, 100);
            graphics[t] = Graphics.FromImage (bitmaps[t]);
            fonts[t] = new Font ("Arial", 10f);
            formats[t] = new MSStringFormat ();
            skFonts[t] = new SKFont (SKTypeface.FromFamilyName ("Arial"), 13.333f);
        }
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        for (var t = 0; t < Threads; t++) {
            graphics[t].Dispose ();
            bitmaps[t].Dispose ();
            fonts[t].Dispose ();
            skFonts[t].Dispose ();
        }
    }

    // The floor: SkiaSharp alone, an SKFont per thread, none of this library's code.
    [Benchmark (Baseline = true)]
    public void RawSkiaMeasureText ()
    {
        Parallel.For (0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, t => {
            var font = skFonts[t];
            for (var i = 0; i < OpsPerThread; i++)
                font.MeasureText (texts[i & 3].AsSpan ());
        });
    }

    [Benchmark]
    public void MeasureString () => Run ((g, font, _, text) => g.MeasureString (text, font));

    [Benchmark]
    public void MeasureRunWidth () => Run ((g, font, _, text) => g.MeasureRunWidth (text, font));

    [Benchmark]
    public void MeasureCharacterRanges () => Run ((g, font, format, text) => {
        format.SetMeasurableCharacterRanges ([new CharacterRange (0, text.Length)]);
        foreach (var region in g.MeasureCharacterRanges (text, font, layout, format))
            region.Dispose ();
    });

    private void Run (Action<Graphics, Font, MSStringFormat, string> measure)
    {
        Parallel.For (0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, t => {
            var g = graphics[t];
            var font = fonts[t];
            var format = formats[t];
            for (var i = 0; i < OpsPerThread; i++)
                measure (g, font, format, texts[i & 3]);
        });
    }
}
