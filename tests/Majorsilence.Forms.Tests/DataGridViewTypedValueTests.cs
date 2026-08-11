using System.Data;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Bound cell values keep the type of the member they came from, as in WinForms. Populating cells
    /// with <c>value.ToString ()</c> made every cell a String, so handlers casting a cell value threw
    /// InvalidCastException and numeric columns sorted lexically (1, 10, 2).
    /// </summary>
    public class DataGridViewTypedValueTests
    {
        private static DataTable Stock ()
        {
            var t = new DataTable ();
            t.Columns.Add ("Name", typeof (string));
            t.Columns.Add ("Quantity", typeof (int));
            t.Columns.Add ("Price", typeof (decimal));
            t.Columns.Add ("Added", typeof (DateTime));
            t.Columns.Add ("Active", typeof (bool));
            t.Rows.Add ("Widget", 10, 9.99m, new DateTime (2020, 1, 2), true);
            t.Rows.Add ("Gadget", 2, 100.50m, new DateTime (2019, 6, 7), false);
            t.Rows.Add ("Doohickey", 100, 0.25m, new DateTime (2021, 12, 25), true);
            return t;
        }

        private sealed class Part
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        [Fact]
        public void DataTable_bound_cells_keep_their_column_types ()
        {
            using var grid = new DataGridView { DataSource = Stock () };

            var row = grid.Rows[0];

            Assert.Equal ("Widget", Assert.IsType<string> (row.Cells[0].Value));
            Assert.Equal (10, Assert.IsType<int> (row.Cells[1].Value));
            Assert.Equal (9.99m, Assert.IsType<decimal> (row.Cells[2].Value));
            Assert.Equal (new DateTime (2020, 1, 2), Assert.IsType<DateTime> (row.Cells[3].Value));
            Assert.True (Assert.IsType<bool> (row.Cells[4].Value));
        }

        [Fact]
        public void Object_list_bound_cells_keep_their_property_types ()
        {
            var parts = new List<Part> {
                new Part { Name = "Widget", Quantity = 10, Price = 9.99m },
            };

            using var grid = new DataGridView { DataSource = parts };

            Assert.Equal (10, Assert.IsType<int> (grid.Rows[0].Cells[1].Value));
            Assert.Equal (9.99m, Assert.IsType<decimal> (grid.Rows[0].Cells[2].Value));
        }

        [Fact]
        public void Manually_defined_columns_populate_from_a_DataTable_via_DataPropertyName ()
        {
            // DataRowView exposes its columns as property descriptors, not CLR properties, so the
            // reflection-only lookup left every cell of a manually-columned grid empty.
            using var grid = new DataGridView { AutoGenerateColumns = false };

            grid.Columns.Add ("colPrice", "Unit Price");
            grid.Columns[0].DataPropertyName = "Price";
            grid.Columns.Add ("colQty", "Qty");
            grid.Columns[1].DataPropertyName = "Quantity";

            grid.DataSource = Stock ();

            Assert.Equal (9.99m, grid.Rows[0].Cells[0].Value);
            Assert.Equal (10, grid.Rows[0].Cells[1].Value);
        }

        [Fact]
        public void Numeric_columns_sort_numerically_not_lexically ()
        {
            using var grid = new DataGridView { DataSource = Stock () };

            // Quantity is 10, 2, 100 — as text that would order 10, 100, 2.
            grid.SortByColumn (1, SortOrder.Ascending);

            Assert.Equal (new object?[] { 2, 10, 100 },
                grid.Rows.Select (r => r.Cells[1].Value).ToArray ());

            grid.SortByColumn (1, SortOrder.Descending);

            Assert.Equal (new object?[] { 100, 10, 2 },
                grid.Rows.Select (r => r.Cells[1].Value).ToArray ());
        }

        [Fact]
        public void Date_columns_sort_chronologically ()
        {
            using var grid = new DataGridView { DataSource = Stock () };

            grid.SortByColumn (3, SortOrder.Ascending);

            Assert.Equal (
                new[] { new DateTime (2019, 6, 7), new DateTime (2020, 1, 2), new DateTime (2021, 12, 25) },
                grid.Rows.Select (r => (DateTime) r.Cells[3].Value!).ToArray ());
        }

        [Fact]
        public void Text_columns_still_sort_as_text ()
        {
            using var grid = new DataGridView { DataSource = Stock () };

            grid.SortByColumn (0, SortOrder.Ascending);

            Assert.Equal (new object?[] { "Doohickey", "Gadget", "Widget" },
                grid.Rows.Select (r => r.Cells[0].Value).ToArray ());
        }

        [Fact]
        public void Null_cell_values_sort_first_without_throwing ()
        {
            var t = Stock ();
            t.Rows.Add ("Unknown", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

            using var grid = new DataGridView { DataSource = t };

            grid.SortByColumn (1, SortOrder.Ascending);

            // DBNull is not a number; it must not break the comparison.
            Assert.Equal (4, grid.Rows.Count);
        }
    }
}
