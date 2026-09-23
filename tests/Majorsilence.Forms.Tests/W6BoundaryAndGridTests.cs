using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// W6 mechanisms, fifth chunk: the Application.ThreadException boundary around input dispatch, the
/// binding managers' DataError, Form.ResizeBegin/ResizeEnd, and the DataGridView's context-menu-needed,
/// cell-style-content-changed and virtual-mode events.
/// </summary>
public class W6BoundaryAndGridTests
{
    // ── Application.ThreadException ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_throwing_click_handler_reaches_ThreadException_and_propagates_without_a_handler ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (200, 200) };
        form.MouseDown += (_, _) => throw new InvalidOperationException ("boom");
        form.Show ();

        // Device-space window coordinates, well inside the client area under either chrome.
        var x = form.LogicalToDeviceUnits (100);
        var y = form.LogicalToDeviceUnits (150);

        // No handler: the exception unwinds to the caller, as before.
        Assert.Throws<InvalidOperationException> (() => {
            form.HandlePointerPressed (MouseButtons.Left, x, y, Keys.None);
            form.HandlePointerReleased (MouseButtons.Left, x, y, Keys.None);
        });

        Exception? seen = null;
        void Handler (object sender, System.Threading.ThreadExceptionEventArgs e) => seen = e.Exception;
        Application.ThreadException += Handler;

        try {
            form.HandlePointerPressed (MouseButtons.Left, x, y, Keys.None);
            form.HandlePointerReleased (MouseButtons.Left, x, y, Keys.None);
            Assert.IsType<InvalidOperationException> (seen);

            // ThrowException stands the boundary aside even with a handler attached.
            seen = null;
            Application.SetUnhandledExceptionMode (UnhandledExceptionMode.ThrowException);
            Assert.Throws<InvalidOperationException> (() => {
                form.HandlePointerPressed (MouseButtons.Left, x, y, Keys.None);
                form.HandlePointerReleased (MouseButtons.Left, x, y, Keys.None);
            });
            Assert.Null (seen);
        } finally {
            Application.SetUnhandledExceptionMode (UnhandledExceptionMode.Automatic);
            Application.ThreadException -= Handler;
        }
    }

    // ── BindingManagerBase / BindingSource.DataError ────────────────────────────────────────────────

    private sealed class Record : IEditableObject
    {
        public string Name { get; set; } = "a";
        public void BeginEdit () { }
        public void CancelEdit () { }
        public void EndEdit () => throw new InvalidOperationException ("commit refused");
    }

    [Fact]
    public void A_failing_commit_reaches_DataError_on_the_manager_and_the_source_or_propagates ()
    {
        using var source = new BindingSource (new List<Record> { new () }, string.Empty);

        // No handler anywhere: EndEdit throws, as it did before.
        Assert.Throws<InvalidOperationException> (source.EndEdit);

        Exception? manager_saw = null;
        Exception? source_saw = null;
        source.CurrencyManager.DataError += (_, e) => manager_saw = e.Exception;
        source.DataError += (_, e) => source_saw = e.Exception;

        source.EndEdit ();

        Assert.IsType<InvalidOperationException> (manager_saw);
        Assert.Same (manager_saw, source_saw);
    }

    // ── Form.ResizeBegin / ResizeEnd ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_resize_or_move_drag_brackets_ResizeBegin_and_ResizeEnd ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (200, 200) };
        var begun = 0;
        var ended = 0;
        form.ResizeBegin += (_, _) => begun++;
        form.ResizeEnd += (_, _) => ended++;

        form.BeginResizeDrag (Backends.WindowEdge.East);
        Assert.Equal ((1, 0), (begun, ended));

        // The release that ends the drag; a second release with no drag active is nothing.
        form.HandlePointerReleased (MouseButtons.Left, 5, 5, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, 5, 5, Keys.None);
        Assert.Equal ((1, 1), (begun, ended));

        // A caption move is a size-move gesture too, as upstream's WM_ENTERSIZEMOVE is.
        form.BeginMoveDrag ();
        form.BeginMoveDrag ();
        form.HandlePointerReleased (MouseButtons.Left, 5, 5, Keys.None);
        Assert.Equal ((2, 2), (begun, ended));

        // A programmatic size change is not a user drag.
        form.Size = new Size (300, 300);
        Assert.Equal ((2, 2), (begun, ended));
    }

    // ── DataGridView ────────────────────────────────────────────────────────────────────────────────

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;
    }

    private static DataGridView BoundGrid ()
    {
        HeadlessRenderer.Use ();
        var grid = new DataGridView { Size = new Size (300, 200) };
        grid.Columns.Add (new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name" });
        grid.DataSource = new List<Row> { new () { Name = "one" }, new () { Name = "two" } };
        return grid;
    }

    [Fact]
    public void ContextMenuStripNeeded_supplies_the_cell_and_row_menus_of_a_bound_grid ()
    {
        using var grid = BoundGrid ();
        using var cell_menu = new ContextMenuStrip ();
        using var row_menu = new ContextMenuStrip ();
        var cell_asked = new List<(int column, int row)> ();
        var row_asked = new List<int> ();

        grid.CellContextMenuStripNeeded += (_, e) => { cell_asked.Add ((e.ColumnIndex, e.RowIndex)); e.ContextMenuStrip = cell_menu; };
        grid.RowContextMenuStripNeeded += (_, e) => { row_asked.Add (e.RowIndex); e.ContextMenuStrip = row_menu; };

        Assert.Same (cell_menu, grid.Rows[1].Cells[0].GetInheritedContextMenuStrip (1));
        Assert.Equal ([(0, 1)], cell_asked);

        Assert.Same (row_menu, grid.Rows[0].GetContextMenuStrip (0));
        Assert.Equal ([0], row_asked);
    }

    [Fact]
    public void ContextMenuStripNeeded_is_not_asked_of_an_unbound_grid_and_the_stored_menu_wins_by_default ()
    {
        HeadlessRenderer.Use ();
        using var grid = new DataGridView { Size = new Size (300, 200) };
        grid.Columns.Add ("a", "A");
        grid.Rows.Add ("x");
        using var stored = new ContextMenuStrip ();
        grid.Rows[0].Cells[0].ContextMenuStrip = stored;
        var asked = 0;
        grid.CellContextMenuStripNeeded += (_, _) => asked++;

        Assert.Same (stored, grid.Rows[0].Cells[0].GetInheritedContextMenuStrip (0));
        Assert.Equal (0, asked);
    }

    [Fact]
    public void Changing_a_column_or_row_default_cell_style_raises_CellStyleContentChanged_with_its_scope ()
    {
        HeadlessRenderer.Use ();
        using var grid = new DataGridView { Size = new Size (300, 200) };
        grid.Columns.Add ("a", "A");
        grid.Rows.Add ("x");
        var seen = new List<(DataGridViewCellStyle style, DataGridViewCellStyleScopes scope)> ();
        grid.CellStyleContentChanged += (_, e) => seen.Add ((e.CellStyle, e.CellStyleScope));

        grid.Columns[0].DefaultCellStyle.BackColor = Color.Red;
        grid.Columns[0].DefaultCellStyle.BackColor = Color.Red;   // unchanged: nothing
        grid.Rows[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        Assert.Equal (2, seen.Count);
        Assert.Same (grid.Columns[0].DefaultCellStyle, seen[0].style);
        Assert.Equal (DataGridViewCellStyleScopes.Column, seen[0].scope);
        Assert.Equal (DataGridViewCellStyleScopes.Row, seen[1].scope);
        Assert.Equal (DataGridViewCellStyleScopes.Column, grid.Columns[0].DefaultCellStyle.Scope);
    }

    private static DataGridView VirtualGrid (out Form form)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (400, 300) };
        var grid = new DataGridView { Size = new Size (300, 200), VirtualMode = true };
        grid.Columns.Add ("a", "A");
        grid.Rows.Add ();
        grid.Rows.Add ();
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    [Fact]
    public void An_unbound_virtual_grid_reads_values_through_CellValueNeeded_and_writes_through_CellValuePushed ()
    {
        var grid = VirtualGrid (out var form);
        using var _ = form;
        var needed = new List<(int column, int row)> ();
        var pushed = new List<(int row, object? value)> ();
        grid.CellValueNeeded += (_, e) => { needed.Add ((e.ColumnIndex, e.RowIndex)); e.Value = $"v{e.RowIndex}"; };
        grid.CellValuePushed += (_, e) => pushed.Add ((e.RowIndex, e.Value));

        Assert.Equal ("v1", grid.Rows[1].Cells[0].Value);
        Assert.Equal ("v0", grid.Rows[0].Cells[0].FormattedValue);
        Assert.Contains ((0, 1), needed);

        var changed = 0;
        grid.CellValueChanged += (_, _) => changed++;
        grid.Rows[1].Cells[0].Value = "typed";

        Assert.Equal ([(1, "typed")], pushed);
        Assert.Equal (1, changed);
        Assert.Equal ("v1", grid.Rows[1].Cells[0].Value);   // nothing was stored: the application owns the data
    }

    [Fact]
    public void An_unbound_virtual_grid_asks_RowHeightInfoNeeded_and_offers_RowHeightInfoPushed ()
    {
        var grid = VirtualGrid (out var form);
        using var _ = form;
        var pushed = new List<int> ();
        grid.RowHeightInfoNeeded += (_, e) => e.Height = 44;
        grid.RowHeightInfoPushed += (_, e) => { pushed.Add (e.Height); e.Handled = true; };

        Assert.Equal (44, grid.Rows[0].Height);

        grid.Rows[0].Height = 60;
        Assert.Equal ([60], pushed);
        Assert.Equal (44, grid.Rows[0].Height);   // Handled: the row kept nothing of its own
    }

    [Fact]
    public void Cancelling_an_edit_in_virtual_mode_raises_CancelRowEdit_and_a_commit_pushes ()
    {
        var grid = VirtualGrid (out var form);
        using var _ = form;
        var cancelled = 0;
        var pushed = new List<object?> ();
        grid.CellValueNeeded += (_, e) => e.Value = "start";
        grid.CellValuePushed += (_, e) => pushed.Add (e.Value);
        grid.CancelRowEdit += (_, _) => cancelled++;

        grid.MoveCurrentCell (0, 0);
        Assert.True (grid.BeginEdit (true));
        grid.CancelEdit ();
        Assert.Equal (1, cancelled);

        Assert.True (grid.BeginEdit (true));
        ((TextBox)grid.EditingControl!).Text = "edited";
        Assert.True (grid.EndEdit ());
        Assert.Equal (["edited"], pushed);
    }
}
