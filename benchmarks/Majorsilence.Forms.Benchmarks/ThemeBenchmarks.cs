using BenchmarkDotNet.Attributes;
using SkiaSharp;

namespace Majorsilence.Forms.Benchmarks;

// Theme values are read on every paint of every control (colours, font sizes), and a CSS theme is parsed and
// applied when an application starts or switches theme.
[MemoryDiagnoser]
public class ThemeBenchmarks
{
    private string css = "";

    [GlobalSetup]
    public void Setup () => css = Theme.ExportCss ();

    // Ten reads, the kind a control's OnPaint makes.
    [Benchmark (OperationsPerInvoke = 10)]
    [BenchmarkCategory (AllocationGate.Category)]
    public uint ReadValues ()
    {
        var total = (uint)Theme.FontSize + (uint)Theme.ItemFontSize;
        total += (uint)Theme.BackgroundColor + (uint)Theme.ForegroundColor + (uint)Theme.AccentColor;
        total += (uint)Theme.BorderLowColor + (uint)Theme.ControlLowColor + (uint)Theme.ControlMidColor;
        total += (uint)Theme.ForegroundDisabledColor + (uint)Theme.TextSelectionBackgroundColor;
        return total;
    }

    [Benchmark]
    [BenchmarkCategory (AllocationGate.Category)]
    public int ParseCss () => ThemeStyleSheet.Parse (css).RuleCount;

    // Not gated: applying a theme raises theme-changed and clears caches, whose cost depends on what else is alive.
    [Benchmark]
    public void LoadFromCss () => Theme.LoadFromCss (css);
}
