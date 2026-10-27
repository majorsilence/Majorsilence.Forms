using System.Collections.Generic;
using System.ComponentModel;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A row's cells follow the grid's columns, as upstream's do: inserting a column gives every row a cell at its index
    // (the bound member's value for a bound column), and removing one takes its cells away. The cells stayed where they
    // were, so after a button column was inserted into a bound grid every value to its right showed one column to the left
    // of its header -- an item's name under "Item Code", its quantity on hand under "Item Name".
    public class DataGridViewColumnCellsTests
    {
        public class Item
        {
            public bool Include { get; set; }
            public string ItemCode { get; set; } = "CHIPS";
            public string ItemName { get; set; } = "Lays Chips";
            public decimal Onhand { get; set; } = 90m;
            public string AccountName { get; set; } = "Arena Canteen";
            public string VendorCode { get; set; } = "AMSTE001";
        }

        private static List<Item> Items (int count)
        {
            var items = new List<Item> ();
            for (var i = 0; i < count; i++)
                items.Add (new Item { ItemCode = "CODE" + i });
            return items;
        }

        private static object? Cell (DataGridView grid, int row, string column) => grid.Rows[row].Cells[grid.Columns[column]!.Index].Value;

        [Fact]
        public void An_inserted_column_leaves_each_value_under_its_own_header ()
        {
            using var grid = new DataGridView { DataSource = Items (2) };

            grid.Columns.Insert (1, new DataGridViewButtonColumn { Name = "btnView" });

            Assert.Equal ("CODE1", Cell (grid, 1, "ItemCode"));
            Assert.Equal ("Lays Chips", Cell (grid, 1, "ItemName"));
            Assert.Equal (90m, Cell (grid, 1, "Onhand"));
            Assert.Equal ("AMSTE001", Cell (grid, 1, "VendorCode"));
            Assert.Null (Cell (grid, 1, "btnView"));
        }

        [Fact]
        public void A_removed_column_takes_its_cells_with_it ()
        {
            using var grid = new DataGridView { DataSource = Items (2) };

            grid.Columns.Insert (1, new DataGridViewButtonColumn { Name = "btnView" });
            grid.Columns.Remove ("AccountName");

            Assert.Equal (grid.Columns.Count, grid.Rows[0].Cells.Count);
            Assert.Equal (90m, Cell (grid, 0, "Onhand"));
            Assert.Equal ("AMSTE001", Cell (grid, 0, "VendorCode"));
        }

        [Fact]
        public void A_bound_column_added_after_binding_shows_its_member ()
        {
            using var grid = new DataGridView { DataSource = Items (1) };

            grid.Columns.Insert (0, new DataGridViewTextBoxColumn { Name = "copy", DataPropertyName = "ItemName" });

            Assert.Equal ("Lays Chips", Cell (grid, 0, "copy"));
            Assert.Equal ("CODE0", Cell (grid, 0, "ItemCode"));
        }

        [Fact]
        public void An_unbound_grids_values_stay_in_their_columns ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", "A");
            grid.Columns.Add ("b", "B");
            grid.Rows.Add ("1", "2");

            grid.Columns.Insert (0, new DataGridViewTextBoxColumn { Name = "first" });

            Assert.Null (Cell (grid, 0, "first"));
            Assert.Equal ("1", Cell (grid, 0, "a"));
            Assert.Equal ("2", Cell (grid, 0, "b"));

            grid.Columns.Remove ("a");

            Assert.Equal ("2", Cell (grid, 0, "b"));
        }

        [Fact]
        public void A_changed_item_refills_the_columns_it_is_bound_to ()
        {
            var items = new BindingList<Item> (Items (1));
            using var grid = new DataGridView { DataSource = items };
            grid.Columns.Insert (1, new DataGridViewButtonColumn { Name = "btnView" });

            items[0].ItemName = "Ruffles";
            items.ResetItem (0);

            Assert.Equal ("Ruffles", Cell (grid, 0, "ItemName"));
            Assert.Equal (90m, Cell (grid, 0, "Onhand"));
            Assert.Null (Cell (grid, 0, "btnView"));
        }

        // A RadGridView keeps every row's cells in step, the rows on other pages too: a form inserting Telerik command
        // columns into a bound grid, as an inventory reorder screen does.
        [Fact]
        public void A_RadGridViews_rows_follow_inserted_and_removed_columns_on_every_page ()
        {
            using var grid = new RadGridView { DataSource = Items (5) };
            grid.AllowAddNewRow = false;
            grid.MasterTemplate.EnablePaging = true;
            grid.MasterTemplate.PageSize = 2;

            grid.Columns.Insert (3, new GridViewCommandColumn { Name = "btnVendorSearch", HeaderText = "Vendor Search" });
            grid.Columns.Insert (1, new GridViewCommandColumn { Name = "btnView", HeaderText = "View Reorder Details" });
            grid.Columns.Add (new GridViewCommandColumn { Name = "btnLocation", HeaderText = "Location Search" });
            grid.Columns.Remove ("AccountName");

            grid.GoToPage (2);

            Assert.Equal ("CODE4", grid.Rows[0].Cells["ItemCode"].Value);
            Assert.Equal ("Lays Chips", grid.Rows[0].Cells["ItemName"].Value);
            Assert.Equal (90m, grid.Rows[0].Cells["Onhand"].Value);
            Assert.Equal ("AMSTE001", grid.Rows[0].Cells["VendorCode"].Value);
        }
    }
}
