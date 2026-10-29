using System;
using System.Data;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Row-level RadGridView members Telerik code uses that the compat grid did not have: AddNewRowPosition,
// AllowRowResize, a row's own Height and its ViewTemplate, GridViewTemplate.MasterTemplate, and a data
// column's NullValue. Expectations measured on UI.for.WinForms 2026.2.520.
[Collection ("Headless")]
public class TelerikGridRowMembersTests
{
    private static DataTable Table ()
    {
        var table = new DataTable ();
        table.Columns.Add ("Name", typeof (string));
        table.Columns.Add ("Amount", typeof (int));
        table.Columns.Add ("When", typeof (DateTime));
        table.Rows.Add ("Bravo", 2, new DateTime (2024, 1, 1));
        table.Rows.Add (DBNull.Value, DBNull.Value, DBNull.Value);
        table.Rows.Add ("Charlie", 3, new DateTime (2023, 1, 1));
        return table;
    }

    private static RadGridView Grid (out Form form)
    {
        HeadlessRenderer.Use ();

        var grid = new RadGridView { Width = 420, Height = 260, DataSource = Table () };

        form = new Form { Width = 520, Height = 360 };
        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.Render (grid).Dispose ();
        return grid;
    }

    private static int NewRowIndex (RadGridView grid)
    {
        var rows = ((DataGridView) grid).Rows.Cast<DataGridViewRow> ().ToList ();
        return rows.FindIndex (r => r.Tag is GridNewRow);
    }

    [Fact]
    public void AddNewRowPosition_puts_the_new_row_above_or_below_the_rows ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.AllowAddNewRow = true;
            var count = ((DataGridView) grid).Rows.Count;

            grid.AddNewRowPosition = SystemRowPosition.Top;
            Assert.Equal (0, NewRowIndex (grid));
            Assert.Equal ("Bravo", grid.Rows[0].Cells["Name"].Value);

            grid.AddNewRowPosition = SystemRowPosition.Bottom;
            Assert.Equal (count - 1, NewRowIndex (grid));

            // The grid's own setting is its master template's, as Telerik's is.
            grid.MasterTemplate.AddNewRowPosition = SystemRowPosition.Top;
            Assert.Equal (SystemRowPosition.Top, grid.AddNewRowPosition);
            Assert.Equal (0, NewRowIndex (grid));
        }
    }

    [Fact]
    public void A_designer_written_position_of_false_is_Top ()
    {
        // VB designer code writes CType(False, SystemRowPosition).
        Assert.Equal (SystemRowPosition.Top, (SystemRowPosition) 0);
        Assert.Equal (1, (int) SystemRowPosition.Bottom);
    }

    [Fact]
    public void AllowRowResize_is_the_grids_row_resize_switch ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.True (grid.AllowRowResize);
            Assert.True (grid.MasterTemplate.AllowRowResize);

            grid.AllowRowResize = false;
            Assert.False (((DataGridView) grid).AllowUserToResizeRows);
            Assert.False (grid.MasterTemplate.AllowRowResize);

            grid.MasterTemplate.AllowRowResize = true;
            Assert.True (((DataGridView) grid).AllowUserToResizeRows);
            Assert.True (grid.AllowRowResize);
        }
    }

    [Fact]
    public void A_rows_own_height_reads_back_and_sizes_only_that_row ()
    {
        var grid = Grid (out var form);

        using (form) {
            var dgv = (DataGridView) grid;
            var standard = dgv.Rows[1].Height;

            Assert.Equal (-1, grid.Rows[0].Height);

            grid.Rows[0].Height = 80;
            Assert.Equal (80, grid.Rows[0].Height);
            Assert.Equal (80, dgv.Rows[0].Height);
            Assert.Equal (-1, grid.Rows[1].Height);
            Assert.Equal (standard, dgv.Rows[1].Height);

            // Drawn at no less than five, read back as set.
            grid.Rows[0].Height = 3;
            Assert.Equal (3, grid.Rows[0].Height);
            Assert.Equal (5, dgv.Rows[0].Height);

            // -1 and zero draw it at the grid's row height again.
            grid.Rows[0].Height = -1;
            Assert.Equal (-1, grid.Rows[0].Height);
            Assert.Equal (standard, dgv.Rows[0].Height);

            grid.Rows[0].Height = 0;
            Assert.Equal (0, grid.Rows[0].Height);
            Assert.Equal (standard, dgv.Rows[0].Height);
        }
    }

    [Fact]
    public void A_row_with_its_own_height_keeps_it_through_a_new_row_height_and_a_sort ()
    {
        var grid = Grid (out var form);

        using (form) {
            var dgv = (DataGridView) grid;

            grid.Rows[0].Height = 60;
            grid.TableElement.RowHeight = 30;

            Assert.Equal (60, dgv.Rows[0].Height);
            Assert.Equal (30, dgv.Rows[1].Height);

            // A row with a height of zero follows the grid's row height, as one with none does.
            grid.Rows[2].Height = 0;
            grid.TableElement.RowHeight = 34;
            Assert.Equal (34, grid.Rows.Single (r => Equals (r.Cells["Name"].Value, "Charlie")).DataRow.Height);

            grid.SortDescriptors.Add (new SortDescriptor ("Name", System.ComponentModel.ListSortDirection.Descending));
            var bravo = grid.Rows.Single (r => Equals (r.Cells["Name"].Value, "Bravo"));
            Assert.Equal (60, bravo.Height);
            Assert.Equal (60, bravo.DataRow.Height);
        }
    }

    [Fact]
    public void A_rows_ViewTemplate_and_a_templates_MasterTemplate_are_the_grids_master ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Same (grid.MasterTemplate, grid.Rows[0].ViewTemplate);
            Assert.Same (grid.MasterTemplate, grid.MasterTemplate.MasterTemplate);
            Assert.True (grid.Rows[0].ViewTemplate!.Columns.Contains ("Name"));
        }

        Assert.Null (new RadGridView ().Rows.NewRow ().ViewTemplate);

        var detached = new MasterGridViewTemplate ();
        Assert.Same (detached, detached.MasterTemplate);
    }

    private static string Shown (RadGridView grid, int row, string column)
        => grid.GetCellDisplay (((DataGridView) grid).Rows[row], grid.Columns[column]!.Index);

    [Fact]
    public void NullValue_is_what_a_null_cell_shows ()
    {
        var grid = Grid (out var form);

        using (form) {
            var name = grid.Columns["Name"]!;
            var amount = grid.Columns["Amount"]!;
            var when = grid.Columns["When"]!;

            Assert.Null (name.NullValue);
            Assert.Equal (string.Empty, Shown (grid, 1, "Name"));

            name.NullValue = "(none)";
            Assert.Equal ("(none)", Shown (grid, 1, "Name"));
            Assert.Equal ("Bravo", Shown (grid, 0, "Name"));

            // A numeric column shows it as one of its own numbers.
            amount.NullValue = 4.5m;
            Assert.Equal ("4", Shown (grid, 1, "Amount"));
            amount.NullValue = "7";
            Assert.Equal ("7", Shown (grid, 1, "Amount"));

            // A date column takes text or a date and does not show it.
            when.NullValue = "n/a";
            Assert.Equal ("n/a", when.NullValue);
            Assert.Equal (string.Empty, Shown (grid, 1, "When"));

            name.NullValue = null;
            Assert.Equal (string.Empty, Shown (grid, 1, "Name"));
        }
    }

    [Fact]
    public void NullValue_rejects_what_the_column_cannot_hold ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Throws<FormatException> (() => grid.Columns["Amount"]!.NullValue = "(none)");
            Assert.Throws<FormatException> (() => grid.Columns["When"]!.NullValue = 42);
            Assert.Null (grid.Columns["Amount"]!.NullValue);

            grid.Columns["Name"]!.NullValue = 42;
            Assert.Equal ("42", Shown (grid, 1, "Name"));

            grid.Columns["When"]!.NullValue = new DateTime (2000, 2, 3);
            grid.Columns["When"]!.NullValue = string.Empty;
        }
    }
}
