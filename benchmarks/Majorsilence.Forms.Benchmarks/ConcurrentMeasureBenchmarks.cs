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

    // Text past U+00FF: Polish, Czech, Greek and Cyrillic cells, and typographic punctuation.
    private static readonly string[] extendedTexts = [
        "Zażółć gęślą jaźń, łódź",
        "Příliš žluťoučký kůň úpěl",
        "Τριμηνιαία αποτελέσματα",
        "Квартальные результаты — “итого”",
    ];

    // More distinct strings than TextMeasurer's two cache generations hold (2000), cycled through, so every
    // measurement misses the cache and lays the text out: what a report full of different values does.
    private const int DistinctTexts = 5000;
    private static readonly string[] distinct = MakeDistinct ();
    private int[] cursors = null!;

    private static string[] MakeDistinct ()
    {
        var all = new string[DistinctTexts];
        for (var i = 0; i < all.Length; i++)
            all[i] = $"{texts[i & 3]} {i}";
        return all;
    }

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
        cursors = new int[Threads * 16];   // one per thread, a cache line apart
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
    public void MeasureRunWidthExtended () => Run ((g, font, _, text) => g.MeasureRunWidth (extendedTexts[text.Length & 3], font));

    [Benchmark]
    public void MeasureCharacterRanges () => Run ((g, font, format, text) => {
        format.SetMeasurableCharacterRanges ([new CharacterRange (0, text.Length)]);
        foreach (var region in g.MeasureCharacterRanges (text, font, layout, format))
            region.Dispose ();
    });

    [Benchmark]
    public void MeasureStringDistinct () => RunDistinct ((g, font, text) => g.MeasureString (text, font));

    [Benchmark]
    public void DrawStringDistinct () => RunDistinct ((g, font, text) => g.DrawString (text, font, Brushes.Black, 0, 0));

    private void RunDistinct (Action<Graphics, Font, string> measure)
    {
        Parallel.For (0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, t => {
            var g = graphics[t];
            var font = fonts[t];
            ref var cursor = ref cursors[t * 16];
            for (var i = 0; i < OpsPerThread; i++) {
                // Threads start a share of the cycle apart, so they do not lay out each other's strings.
                measure (g, font, distinct[(cursor + t * (DistinctTexts / 8)) % DistinctTexts]);
                cursor++;
            }
        });
    }

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
