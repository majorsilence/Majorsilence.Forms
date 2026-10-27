using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The new row's cells start at their column's cell template's DefaultNewRowValue, as upstream's do. Measured on
    // WinForms (.NET 10), a column whose cell answers "STAMP": the new row reads "STAMP" there, bound or unbound, while a
    // plain text column beside it reads empty.
    [Collection ("Headless")]
    public class DataGridViewNewRowDefaultValueTests
    {
        private sealed class StampCell : DataGridViewTextBoxCell
        {
            public override object? DefaultNewRowValue => "STAMP";
        }

        private static DataGridViewTextBoxColumn Stamp (string name)
            => new DataGridViewTextBoxColumn { Name = name, HeaderText = name, DataPropertyName = name, CellTemplate = new StampCell () };

        [Fact]
        public void An_unbound_new_row_starts_at_the_templates_default ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Dock = DockStyle.Fill };
            grid.Columns.Add (Stamp ("S"));
            grid.Columns.Add ("T", "T");
            grid.Rows.Add ("a", "b");
            form.Controls.Add (grid);
            form.Show ();

            Assert.Equal ("STAMP", grid.Rows[grid.NewRowIndex].Cells[0].Value);
            Assert.Null (grid.Rows[grid.NewRowIndex].Cells[1].Value);
        }

        [Fact]
        public void A_bound_new_row_starts_at_the_templates_default ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
            var table = new DataTable ();
            table.Columns.Add ("S");
            table.Columns.Add ("T");
            table.Rows.Add ("x", "y");
            grid.Columns.Add (Stamp ("S"));
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "T", DataPropertyName = "T" });
            grid.DataSource = table;
            form.Controls.Add (grid);
            form.Show ();

            Assert.Equal ("STAMP", grid.Rows[grid.NewRowIndex].Cells[0].Value);
        }
    }
}
