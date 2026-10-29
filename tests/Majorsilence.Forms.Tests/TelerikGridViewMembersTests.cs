using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadGridView view members, against Telerik UI.for.WinForms 2026.2.520: BeginEdit() opens the editor (false on a
// read-only column); both scroll states default to AutoHide and the grid's forwards to the template; the column
// chooser lists in column order by default (ColumnChooserSortOrder None); AllowRowReorder is off by default; a grand
// summary row is a GridViewSummaryRowInfo with a cell per column holding the aggregate, Index -1, and can be current.
[Collection ("Headless")]
public class TelerikGridViewMembersTests
{
    private static RadGridView Unbound (out Form form, params string[] names)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 360, Height = 260 };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 120 });
        grid.Columns.Add (new GridViewTextBoxColumn ("Code") { HeaderText = "Code", Width = 80 });

        foreach (var name in names) {
            grid.Rows.Add ();
            grid.Rows[grid.Rows.Count - 1].Cells["Name"].Value = name;
        }

        form = new Form { Width = 460, Height = 360 };
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    private static string Order (RadGridView grid) => string.Join (",", grid.Rows.Select (r => r.Cells["Name"].Value));

    [Fact]
    public void BeginEdit_opens_the_editor_on_the_current_cell_and_refuses_a_read_only_one ()
    {
        var grid = Unbound (out var form, "Alpha", "Bravo");

        using (form) {
            ((DataGridView) grid).CurrentCell = ((DataGridView) grid).Rows[1].Cells[0];

            Assert.True (grid.BeginEdit ());
            Assert.True (grid.IsInEditMode);
            ((DataGridView) grid).EndEdit ();

            grid.Columns["Name"]!.ReadOnly = true;
            Assert.False (grid.BeginEdit ());
            Assert.False (grid.IsInEditMode);
        }
    }

    [Fact]
    public void AlwaysHide_takes_a_scroll_bar_away ()
    {
        var grid = Unbound (out var form, Enumerable.Range (0, 40).Select (i => "Row " + i).ToArray ());

        using (form) {
            Assert.Equal (ScrollState.AutoHide, grid.HorizontalScrollState);
            Assert.Equal (ScrollState.AutoHide, grid.VerticalScrollState);
            PaintSurface.Render (grid).Dispose ();
            Assert.True (((DataGridView) grid).VerticalScrollBarVisible);

            grid.VerticalScrollState = ScrollState.AlwaysHide;
            PaintSurface.Render (grid).Dispose ();
            Assert.Equal (ScrollState.AlwaysHide, grid.MasterTemplate.VerticalScrollState);
            Assert.False (((DataGridView) grid).VerticalScrollBarVisible);

            grid.HorizontalScrollState = ScrollState.AlwaysHide;
            Assert.Equal (ScrollState.AlwaysHide, grid.MasterTemplate.HorizontalScrollState);
            Assert.Equal (ScrollBars.None, ((DataGridView) grid).ScrollBars);

            grid.VerticalScrollState = ScrollState.AutoHide;
            PaintSurface.Render (grid).Dispose ();
            Assert.True (((DataGridView) grid).VerticalScrollBarVisible);
        }
    }

    [Fact]
    public void The_column_chooser_lists_the_columns_in_the_order_ColumnChooserSortOrder_asks ()
    {
        var grid = Unbound (out var form);

        using (form) {
            grid.Columns.Add (new GridViewTextBoxColumn ("Amount") { HeaderText = "Amount" });
            ((GridViewColumn) grid.Columns["Code"]!).VisibleInColumnChooser = false;

            string Listed () => string.Join (",", RadGridColumnChooser.Columns (grid).Select (c => c.HeaderText));

            Assert.Equal (RadSortOrder.None, grid.ColumnChooserSortOrder);
            Assert.Equal ("Name,Amount", Listed ());

            grid.ColumnChooserSortOrder = RadSortOrder.Ascending;
            Assert.Equal ("Amount,Name", Listed ());

            grid.ColumnChooserSortOrder = RadSortOrder.Descending;
            Assert.Equal ("Name,Amount", Listed ());
        }
    }

    [Fact]
    public void A_dragged_row_takes_the_place_of_the_row_it_is_dropped_on ()
    {
        var grid = Unbound (out var form, "A", "B", "C");

        using (form) {
            // Read-only, so a click on the current cell opens no editor to take the next press.
            grid.ReadOnly = true;
            PaintSurface.Render (grid).Dispose ();
            Assert.False (grid.AllowRowReorder);

            var core = (DataGridView) grid;
            Point Centre (int row)
            {
                var r = core.GetCellBounds (row, 0);
                return new Point (core.DeviceToLogicalUnits (r.Left + r.Width / 2), core.DeviceToLogicalUnits (r.Top + r.Height / 2));
            }

            void Drag (int from, int to)
            {
                var a = Centre (from);
                var b = Centre (to);
                grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, a.X, a.Y, 0));
                grid.RaiseMouseMove (new MouseEventArgs (MouseButtons.Left, 0, b.X, b.Y, 0));
                grid.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, b.X, b.Y, 0));
                PaintSurface.Render (grid).Dispose ();
            }

            // Off: a drag moves nothing.
            Drag (0, 2);
            Assert.Equal ("A,B,C", Order (grid));

            grid.AllowRowReorder = true;
            Drag (0, 2);
            Assert.Equal ("B,C,A", Order (grid));

            Drag (2, 0);
            Assert.Equal ("A,B,C", Order (grid));

            // Sorted rows keep the sort's order.
            grid.SortDescriptors.Add (new SortDescriptor ("Name", System.ComponentModel.ListSortDirection.Descending));
            Drag (0, 2);
            Assert.Equal ("C,B,A", Order (grid));
        }
    }

    [Fact]
    public void A_grand_summary_row_holds_the_aggregate_in_its_cells_and_can_be_current ()
    {
        HeadlessRenderer.Use ();
        var table = new DataTable ();
        table.Columns.Add ("Name", typeof (string));
        table.Columns.Add ("Amount", typeof (int));
        table.Rows.Add ("Bravo", 2);
        table.Rows.Add ("Alpha", 1);
        table.Rows.Add ("Charlie", 3);

        using var form = new Form { Width = 460, Height = 300 };
        var grid = new RadGridView { Width = 360, Height = 200, DataSource = table };
        form.Controls.Add (grid);
        form.Show ();

        grid.MasterTemplate.SummaryRowsBottom.Add (new GridViewSummaryRowItem (new GridViewSummaryItem ("Amount", "{0}", GridAggregateFunction.Sum)));

        var summary = Assert.Single (grid.MasterView.SummaryRows);
        Assert.IsType<GridViewSummaryRowInfo> (summary);
        Assert.Equal (6.0, Convert.ToDouble (summary.Cells["Amount"].Value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null (summary.Cells["Name"].Value);
        Assert.Equal (-1, ((GridViewSummaryRowInfo) summary).Index);
        Assert.Equal (3, grid.Rows.Count);

        grid.CurrentRow = summary;
        Assert.IsType<GridViewSummaryRowInfo> (grid.CurrentRow);
    }
}
