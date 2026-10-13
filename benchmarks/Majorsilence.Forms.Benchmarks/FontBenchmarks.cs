using BenchmarkDotNet.Attributes;
using Majorsilence.Forms.Drawing.Drawing2D;
using MSFontFamily = Majorsilence.Forms.Drawing.FontFamily;
using MSFontStyle = Majorsilence.Forms.Drawing.FontStyle;
using RectangleF = System.Drawing.RectangleF;

namespace Majorsilence.Forms.Benchmarks;

// Font and font-family lookups, warm (every face has been seen before), the way a report engine makes them for
// each text box: a new Font, the family's line spacing and ascent to place the baseline, and sometimes a path.
// FontFamily metrics and GraphicsPath.AddString resolve the family through FontSubstitution, which asks the
// platform font manager (fontconfig on Linux) for the family and each substitute.
[MemoryDiagnoser]
[BenchmarkCategory (AllocationGate.Category)]
public class FontBenchmarks
{
    private readonly MSFontFamily arial = new ("Arial");
    private readonly MSFontFamily missing = new ("No Such Family 1234");

    [GlobalSetup]
    public void Setup ()
    {
        // Warm everything once, so this measures the steady state rather than the first lookup.
        NewFont ();
        NewFontUnknownFamily ();
        FamilyMetrics ();
        FamilyMetricsUnknownFamily ();
        IsStyleAvailable ();
        PathAddString ();
    }

    [Benchmark]
    public float NewFont ()
    {
        using var font = new Font ("Arial", 10f);
        return font.GetSKFont ().Size;
    }

    [Benchmark]
    public float NewFontUnknownFamily ()
    {
        using var font = new Font ("No Such Family 1234", 10f);
        return font.GetSKFont ().Size;
    }

    [Benchmark]
    public int FamilyMetrics () => arial.GetLineSpacing (MSFontStyle.Regular) + arial.GetCellAscent (MSFontStyle.Regular);

    [Benchmark]
    public int FamilyMetricsUnknownFamily () => missing.GetLineSpacing (MSFontStyle.Regular) + missing.GetCellAscent (MSFontStyle.Regular);

    [Benchmark]
    public bool IsStyleAvailable () => arial.IsStyleAvailable (MSFontStyle.Bold);

    [Benchmark]
    public int PathAddString ()
    {
        using var path = new GraphicsPath ();
        path.AddString ("Total", arial, (int)MSFontStyle.Regular, 12f, new RectangleF (0, 0, 200, 40), null);
        return path.PointCount;
    }
}
