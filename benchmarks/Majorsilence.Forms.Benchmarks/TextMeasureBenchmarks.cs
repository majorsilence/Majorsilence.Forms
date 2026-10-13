using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using CharacterRange = Majorsilence.Forms.Drawing.CharacterRange;
using MSStringFormat = Majorsilence.Forms.Drawing.StringFormat;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Benchmarks;

// Headless text measurement, the way a report engine uses it: once per table cell, to learn the width of a
// single line, and sometimes the start and end of every word. MeasureCharacterRanges lays the text out and builds
// a Region per range; MeasureRunWidth / MeasureRunSpans make the same two SKFont.MeasureText calls without that.
[MemoryDiagnoser]
[BenchmarkCategory (AllocationGate.Category)]
public class TextMeasureBenchmarks
{
    private Bitmap bitmap = null!;
    private Graphics g = null!;
    private Font font = null!;
    private MSStringFormat wholeFormat = null!;
    private MSStringFormat wordsFormat = null!;
    private CharacterRange[] words = null!;
    private (float Left, float Right)[] spans = null!;
    private readonly RectangleF layout = new (0, 0, float.MaxValue, float.MaxValue);

    [Params ("Customer number 4821", "Quarterly results for the northern region, adjusted for seasonality and one-off items")]
    public string Text { get; set; } = "";

    [Params (96f, 300f)]
    public float Dpi { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        bitmap = new Bitmap (800, 200);
        bitmap.SetResolution (Dpi, Dpi);
        g = Graphics.FromImage (bitmap);
        font = new Font ("Arial", 10f);

        wholeFormat = new MSStringFormat ();
        wholeFormat.SetMeasurableCharacterRanges (new[] { new CharacterRange (0, Text.Length) });

        var list = new List<CharacterRange> ();
        var start = 0;
        for (var i = 0; i <= Text.Length; i++) {
            if (i == Text.Length || Text[i] == ' ') {
                if (i > start)
                    list.Add (new CharacterRange (start, i - start));
                start = i + 1;
            }
        }
        words = list.ToArray ();
        wordsFormat = new MSStringFormat ();
        wordsFormat.SetMeasurableCharacterRanges (words);
        spans = new (float, float)[words.Length];
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        g.Dispose ();
        bitmap.Dispose ();
        font.Dispose ();
    }

    [Benchmark (Baseline = true)]
    public float WidthViaRegions ()
    {
        var regions = g.MeasureCharacterRanges (Text, font, layout, wholeFormat);
        var width = regions[0].GetBounds (g).Width;
        regions[0].Dispose ();
        return width;
    }

    [Benchmark]
    public float WidthViaMeasureRunWidth () => g.MeasureRunWidth (Text, font);

    [Benchmark]
    public float WordsViaRegions ()
    {
        var regions = g.MeasureCharacterRanges (Text, font, layout, wordsFormat);
        var total = 0f;
        foreach (var region in regions) {
            total += region.GetBounds (g).Right;
            region.Dispose ();
        }
        return total;
    }

    [Benchmark]
    public float WordsViaMeasureRunSpans ()
    {
        g.MeasureRunSpans (Text, font, words, spans);
        var total = 0f;
        foreach (var span in spans)
            total += span.Right;
        return total;
    }

    [Benchmark]
    public float MeasureStringForComparison () => g.MeasureString (Text, font).Width;
}
