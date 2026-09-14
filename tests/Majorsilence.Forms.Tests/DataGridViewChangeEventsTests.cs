using Xunit;

namespace Majorsilence.Forms.Tests;

// W6.1, the dead-event sweep (RC-5), DataGridView slice: ColumnWidthChanged, RowHeightChanged,
// ColumnSortModeChanged, ColumnHeadersHeightChanged, RowHeadersWidthChanged and
// AutoSizeColumnModeChanged were all `{ add { } remove { } }` -- declared for source compat and
// discarded every handler -- even though each already has an obvious trigger point (the property
// setter that changes the value they describe). Backed each with a field, gave the grid an On*
// hook the way ColumnAdded/RowsAdded already have (see DataGridViewCollectionEventTests), and
// called it from that setter.
//
// Left out of this sweep: RowStateChanged and CellStateChanged. Selected -- the state change that
// matters most -- flips through DataGridView.SetRowSelected/SetCellSelected for a single click or
// assignment, but every batch selection path (Shift-click ranges, SelectAll, ClearSelection) writes
// DataGridViewRow/Cell.SetSelectedCore directly and bypasses that choke point deliberately, so the
// two events would fire for a single selection change and silently not fire for a range or a clear.
// Wiring that correctly is its own item, not a one-line addition to an existing setter.
public class DataGridViewChangeEventsTests
{
    sealed class ProbeGrid : DataGridView
    {
        public int ColumnWidthChangedCount;
        public int RowHeightChangedCount;
        public int ColumnSortModeChangedCount;
        public int ColumnHeadersHeightChangedCount;
        public int RowHeadersWidthChangedCount;
        public int AutoSizeColumnModeChangedCount;
        public DataGridViewAutoSizeColumnModeEventArgs? LastAutoSizeColumnModeChanged;

        protected override void OnColumnWidthChanged (DataGridViewColumnEventArgs e)
        {
            ColumnWidthChangedCount++;
            base.OnColumnWidthChanged (e);
        }

        protected override void OnRowHeightChanged (DataGridViewRowEventArgs e)
        {
            RowHeightChangedCount++;
            base.OnRowHeightChanged (e);
        }

        protected override void OnColumnSortModeChanged (DataGridViewColumnEventArgs e)
        {
            ColumnSortModeChangedCount++;
            base.OnColumnSortModeChanged (e);
        }

        protected override void OnColumnHeadersHeightChanged (System.EventArgs e)
        {
            ColumnHeadersHeightChangedCount++;
            base.OnColumnHeadersHeightChanged (e);
        }

        protected override void OnRowHeadersWidthChanged (System.EventArgs e)
        {
            RowHeadersWidthChangedCount++;
            base.OnRowHeadersWidthChanged (e);
        }

        protected override void OnAutoSizeColumnModeChanged (DataGridViewAutoSizeColumnModeEventArgs e)
        {
            AutoSizeColumnModeChangedCount++;
            LastAutoSizeColumnModeChanged = e;
            base.OnAutoSizeColumnModeChanged (e);
        }
    }

    [Fact]
    public void ColumnWidth_Set_RaisesTheHookAndTheEvent ()
    {
        using var grid = new ProbeGrid ();
        var column = new DataGridViewColumn { Name = "a", Width = 100 };
        grid.Columns.Add (column);
        var raised = 0;
        DataGridViewColumn? seen = null;
        grid.ColumnWidthChanged += (s, e) => { raised++; seen = e.Column; };

        column.Width = 150;

        Assert.Equal (1, grid.ColumnWidthChangedCount);
        Assert.Equal (1, raised);
        Assert.Same (column, seen);

        // Setting the same value again must not re-fire.
        column.Width = 150;
        Assert.Equal (1, grid.ColumnWidthChangedCount);
    }

    [Fact]
    public void ColumnWidth_SetOnADetachedColumn_DoesNotThrow ()
    {
        var column = new DataGridViewColumn { Name = "a", Width = 100 };

        column.Width = 150;

        Assert.Equal (150, column.Width);
    }

    [Fact]
    public void RowHeight_Set_RaisesTheHookAndTheEvent ()
    {
        using var grid = new ProbeGrid ();
        grid.Columns.Add (new DataGridViewColumn { Name = "a" });
        var row = grid.Rows[grid.Rows.Add ()];
        var raised = 0;
        DataGridViewRow? seen = null;
        grid.RowHeightChanged += (s, e) => { raised++; seen = e.Row; };

        row.Height = 40;

        Assert.Equal (1, grid.RowHeightChangedCount);
        Assert.Equal (1, raised);
        Assert.Same (row, seen);

        row.Height = 40;
        Assert.Equal (1, grid.RowHeightChangedCount);
    }

    [Fact]
    public void ColumnSortMode_Set_RaisesTheHookAndTheEvent ()
    {
        using var grid = new ProbeGrid ();
        var column = new DataGridViewColumn { Name = "a" };
        grid.Columns.Add (column);
        var raised = 0;
        grid.ColumnSortModeChanged += (s, e) => raised++;

        Assert.Equal (DataGridViewColumnSortMode.Automatic, column.SortMode);
        column.SortMode = DataGridViewColumnSortMode.NotSortable;

        Assert.Equal (DataGridViewColumnSortMode.NotSortable, column.SortMode);
        Assert.Equal (1, grid.ColumnSortModeChangedCount);
        Assert.Equal (1, raised);

        column.SortMode = DataGridViewColumnSortMode.NotSortable;
        Assert.Equal (1, grid.ColumnSortModeChangedCount);
    }

    [Fact]
    public void ColumnHeadersHeight_Set_RaisesTheHookAndTheEvent ()
    {
        using var grid = new ProbeGrid ();
        var raised = 0;
        grid.ColumnHeadersHeightChanged += (s, e) => raised++;

        grid.ColumnHeadersHeight = 40;

        Assert.Equal (1, grid.ColumnHeadersHeightChangedCount);
        Assert.Equal (1, raised);

        grid.ColumnHeadersHeight = 40;
        Assert.Equal (1, grid.ColumnHeadersHeightChangedCount);
    }

    [Fact]
    public void RowHeadersWidth_Set_RaisesTheHookAndTheEvent ()
    {
        using var grid = new ProbeGrid ();
        var raised = 0;
        grid.RowHeadersWidthChanged += (s, e) => raised++;

        grid.RowHeadersWidth = 60;

        Assert.Equal (1, grid.RowHeadersWidthChangedCount);
        Assert.Equal (1, raised);

        grid.RowHeadersWidth = 60;
        Assert.Equal (1, grid.RowHeadersWidthChangedCount);
    }

    [Fact]
    public void AutoSizeColumnMode_Set_RaisesTheHookWithThePreviousMode ()
    {
        using var grid = new ProbeGrid ();
        var column = new DataGridViewColumn { Name = "a" };
        grid.Columns.Add (column);
        var raised = 0;
        grid.AutoSizeColumnModeChanged += (s, e) => raised++;

        Assert.Equal (DataGridViewAutoSizeColumnMode.NotSet, column.AutoSizeMode);
        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        Assert.Equal (1, grid.AutoSizeColumnModeChangedCount);
        Assert.Equal (1, raised);
        Assert.NotNull (grid.LastAutoSizeColumnModeChanged);
        Assert.Same (column, grid.LastAutoSizeColumnModeChanged!.Column);
        Assert.Equal (DataGridViewAutoSizeColumnMode.NotSet, grid.LastAutoSizeColumnModeChanged!.PreviousMode);

        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        Assert.Equal (1, grid.AutoSizeColumnModeChangedCount);
    }
}
