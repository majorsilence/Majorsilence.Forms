using System.Drawing;
using BenchmarkDotNet.Attributes;

namespace Majorsilence.Forms.Benchmarks;

// Scrolling a bound grid one row at a time and repainting, the per-frame cost of a user dragging the scroll bar
// through a large table.
[MemoryDiagnoser]
public class DataGridViewScrollBenchmarks
{
    private const int Rows = 2000;

    private Form form = null!;
    private DataGridView grid = null!;
    private int row;

    [GlobalSetup]
    public void Setup ()
    {
        form = new Form { ClientSize = new Size (900, 700) };
        grid = new DataGridView { Dock = DockStyle.Fill };
        for (var c = 0; c < 6; c++)
            grid.Columns.Add ($"c{c}", $"Column {c}");
        for (var r = 0; r < Rows; r++)
            grid.Rows.Add ($"Row {r}", r * 3, $"Customer {r % 97}", r * 1.25, r % 2 == 0 ? "Open" : "Closed", $"Note {r}");
        form.Controls.Add (grid);
        form.Show ();
        Frame.Render (form);
    }

    [GlobalCleanup]
    public void Cleanup () => form.Close ();

    [Benchmark]
    public void ScrollOneRowAndRepaint ()
    {
        row = (row + 1) % (Rows - 40);
        grid.FirstDisplayedScrollingRowIndex = row;
        Frame.Render (form);
    }
}
