using BenchmarkDotNet.Attributes;

namespace Majorsilence.Forms.Benchmarks;

// Times a single layout pass over a container with a varying number of direct children, to put a
// number on the suspected O(n^2) pattern in Layout/DockAndAnchorLayout.cs and Layout/TableLayout.cs:
// both iterate IArrangedElement.Children with "for (...) { ... children.ElementAt(i) ... }",
// where Children resolves through ControlCollection.GetAllControls's lazy LINQ Concat -- so
// Count()/ElementAt(i) inside a loop re-enumerates that Concat every iteration. If that theory is
// right, the per-child cost should visibly grow with ChildCount rather than stay flat.
[MemoryDiagnoser]
public class LayoutBenchmarks
{
    [Params (10, 100, 1000, 5000, 10000)]
    public int ChildCount { get; set; }

    private Panel dockedContainer = null!;
    private Panel anchoredContainer = null!;

    [GlobalSetup]
    public void Setup ()
    {
        dockedContainer = new Panel { Width = 2000, Height = 2000 };
        dockedContainer.SuspendLayout ();

        for (var i = 0; i < ChildCount; i++)
            dockedContainer.Controls.Add (new Panel { Dock = DockStyle.Top, Height = 20 });

        dockedContainer.ResumeLayout (false);

        anchoredContainer = new Panel { Width = 2000, Height = 2000 };
        anchoredContainer.SuspendLayout ();

        for (var i = 0; i < ChildCount; i++) {
            anchoredContainer.Controls.Add (new Panel {
                Left = i % 200,
                Top = (i / 200) * 20,
                Width = 100,
                Height = 18,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            });
        }

        anchoredContainer.ResumeLayout (false);
    }

    // A single docked-layout pass over ChildCount direct children.
    [Benchmark]
    public void DockedLayoutPass () => dockedContainer.PerformLayout ();

    // A single anchored-layout pass over ChildCount direct children, triggered by a resize (the
    // resize is what actually needs anchors recomputed; PerformLayout alone can no-op if nothing
    // is marked dirty).
    [Benchmark]
    public void AnchoredLayoutPass ()
    {
        anchoredContainer.Width = anchoredContainer.Width == 2000 ? 2010 : 2000;
        anchoredContainer.PerformLayout ();
    }
}
