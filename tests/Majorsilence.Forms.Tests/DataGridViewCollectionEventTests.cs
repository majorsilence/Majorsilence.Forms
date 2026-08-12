using System;
using Xunit;

namespace Majorsilence.Forms.Tests;

// ColumnAdded/ColumnRemoved were declared `{ add { } remove { } }`, which discarded the handler
// outright, and RowsAdded/RowsRemoved had backing fields that nothing ever invoked -- so all four
// events existed and none could fire. There were also no On* hooks to override, which is how this was
// found: a real grid library (AdvancedDataGridView) overrides them to swap in its own header cells.
public class DataGridViewCollectionEventTests
{
    sealed class ProbeGrid : DataGridView
    {
        public int ColumnAddedCount;
        public int ColumnRemovedCount;
        public int RowsAddedCount;
        public int RowsRemovedCount;
        public DataGridViewColumn? LastRemovedColumn;

        protected override void OnColumnAdded (DataGridViewColumnEventArgs e)
        {
            ColumnAddedCount++;
            base.OnColumnAdded (e);
        }

        protected override void OnColumnRemoved (DataGridViewColumnEventArgs e)
        {
            ColumnRemovedCount++;
            LastRemovedColumn = e.Column;
            base.OnColumnRemoved (e);
        }

        protected override void OnRowsAdded (DataGridViewRowsAddedEventArgs e)
        {
            RowsAddedCount++;
            base.OnRowsAdded (e);
        }

        protected override void OnRowsRemoved (DataGridViewRowsRemovedEventArgs e)
        {
            RowsRemovedCount++;
            base.OnRowsRemoved (e);
        }
    }

    [Fact]
    public void AddingAColumn_RaisesTheHookAndTheEvent ()
    {
        using var grid = new ProbeGrid ();
        var raised = 0;
        grid.ColumnAdded += (s, e) => raised++;

        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "a" });

        Assert.Equal (1, grid.ColumnAddedCount);
        Assert.Equal (1, raised);
    }

    [Fact]
    public void RemovingAColumn_ReportsWhichColumnWent ()
    {
        using var grid = new ProbeGrid ();
        var column = new DataGridViewTextBoxColumn { Name = "target" };
        grid.Columns.Add (column);

        grid.Columns.Remove (column);

        Assert.Equal (1, grid.ColumnRemovedCount);
        Assert.Same (column, grid.LastRemovedColumn);
    }

    [Fact]
    public void ClearingColumns_RaisesOncePerColumn ()
    {
        using var grid = new ProbeGrid ();
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "a" });
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "b" });

        grid.Columns.Clear ();

        Assert.Equal (2, grid.ColumnRemovedCount);
    }

    [Fact]
    public void AddingAndRemovingRows_RaisesTheHooks ()
    {
        using var grid = new ProbeGrid ();
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "a" });

        grid.Rows.Add (new DataGridViewRow ());
        Assert.Equal (1, grid.RowsAddedCount);

        grid.Rows.RemoveAt (0);
        Assert.Equal (1, grid.RowsRemovedCount);
    }

    [Fact]
    public void ClearingRows_ReportsTheWholeRangeOnce ()
    {
        using var grid = new ProbeGrid ();
        grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "a" });
        grid.Rows.Add (new DataGridViewRow ());
        grid.Rows.Add (new DataGridViewRow ());

        var before = grid.RowsRemovedCount;
        grid.Rows.Clear ();

        // One event covering the cleared range, not one per row.
        Assert.Equal (before + 1, grid.RowsRemovedCount);
    }
}
