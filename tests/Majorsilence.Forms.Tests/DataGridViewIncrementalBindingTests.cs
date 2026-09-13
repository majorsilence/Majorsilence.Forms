using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.3 (findings DGV-31 P0, DGV-03 P0, DGV-32, DGV-33): incremental data binding.
    //
    // OnBoundListChanged ignored ListChangedType and re-ran the whole bind on any change, so the first
    // add, edit or delete in a bound list silently put the auto-generated columns back, every row
    // object the app held went stale, and -- since W5.2 -- the selection and Row.Visible state were
    // thrown away too. A rebind raised RowsRemoved (0, N) and no RowsAdded. DataSource's getter handed
    // back the resolved list rather than what was assigned, DataMember was stored and never followed,
    // and Rows.Add () returned Count rather than the new row's index.
    [Collection ("Headless")]
    public class DataGridViewIncrementalBindingTests
    {
        private sealed class Item : INotifyPropertyChanged
        {
            private string name = string.Empty;
            private int qty;

            public string Name { get => name; set { name = value; PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (Name))); } }
            public int Qty { get => qty; set { qty = value; PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (nameof (Qty))); } }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        // Counts the row-collection events through the protected raisers, so a derived grid's view of
        // them is what is measured.
        private sealed class CountingGrid : DataGridView
        {
            public readonly List<(int Index, int Count)> Added = new ();
            public readonly List<(int Index, int Count)> Removed = new ();

            protected override void OnRowsAdded (DataGridViewRowsAddedEventArgs e)
            {
                Added.Add ((e.RowIndex, e.RowCount));
                base.OnRowsAdded (e);
            }

            protected override void OnRowsRemoved (DataGridViewRowsRemovedEventArgs e)
            {
                Removed.Add ((e.RowIndex, e.RowCount));
                base.OnRowsRemoved (e);
            }
        }

        private static (CountingGrid grid, BindingList<Item> list) Bound (int count = 3)
        {
            HeadlessRenderer.Use ();
            var list = new BindingList<Item> ();

            for (var i = 0; i < count; i++)
                list.Add (new Item { Name = $"item{i}", Qty = i });

            var grid = new CountingGrid { Width = 400, Height = 200, DataSource = list };
            return (grid, list);
        }

        // ---------------- DGV-31: a change to the list touches only what changed

        [Fact]
        public void Adding_to_the_list_keeps_the_apps_column_changes ()
        {
            // The finding's own test. grid.Columns["Name"].HeaderText = "Customer" -- and the first add
            // to the list silently put the auto-generated header back.
            var (grid, list) = Bound ();
            using var _grid = grid;
            grid.Columns["Name"]!.HeaderText = "Customer";
            grid.Columns["Qty"]!.Visible = false;
            grid.Columns["Name"]!.Width = 123;

            list.Add (new Item { Name = "new", Qty = 9 });

            Assert.Equal (4, grid.Rows.Count);
            Assert.Equal ("Customer", grid.Columns["Name"]!.HeaderText);
            Assert.False (grid.Columns["Qty"]!.Visible);
            Assert.Equal (123, grid.Columns["Name"]!.Width);
        }

        [Fact]
        public void Adding_to_the_list_raises_RowsAdded_for_that_row_only ()
        {
            var (grid, list) = Bound ();
            using var _grid = grid;
            grid.Added.Clear ();
            grid.Removed.Clear ();

            list.Add (new Item { Name = "new" });

            Assert.Equal ((3, 1), Assert.Single (grid.Added));
            Assert.Empty (grid.Removed);
        }

        [Fact]
        public void The_rows_the_app_holds_survive_a_list_change ()
        {
            // Every DataGridViewRow reference, and the Tag/Height/style hung on it, went stale on any
            // change, because the rows were rebuilt.
            var (grid, list) = Bound ();
            using var _grid = grid;
            var row1 = grid.Rows[1];
            row1.Tag = "kept";
            row1.Height = 41;

            list.Add (new Item { Name = "new" });
            list[0].Qty = 100;

            Assert.Same (row1, grid.Rows[1]);
            Assert.Equal ("kept", grid.Rows[1].Tag);
            Assert.Equal (41, grid.Rows[1].Height);
            Assert.Same (grid, row1.DataGridView);
        }

        [Fact]
        public void Removing_from_the_list_removes_that_row_and_raises_RowsRemoved_once ()
        {
            var (grid, list) = Bound ();
            using var _grid = grid;
            var row2 = grid.Rows[2];
            grid.Added.Clear ();
            grid.Removed.Clear ();

            list.RemoveAt (1);

            Assert.Equal (2, grid.Rows.Count);
            Assert.Same (row2, grid.Rows[1]);              // shifted, not rebuilt
            Assert.Equal ((1, 1), Assert.Single (grid.Removed));
            Assert.Empty (grid.Added);
        }

        [Fact]
        public void Editing_an_item_refreshes_its_row_in_place ()
        {
            var (grid, list) = Bound ();
            using var _grid = grid;
            var row1 = grid.Rows[1];
            grid.Added.Clear ();
            grid.Removed.Clear ();
            var value_changed = 0;
            grid.CellValueChanged += (_, _) => value_changed++;

            list[1].Name = "renamed";

            Assert.Same (row1, grid.Rows[1]);
            Assert.Equal ("renamed", grid.Rows[1].Cells["Name"]!.Value);
            Assert.Empty (grid.Added);
            Assert.Empty (grid.Removed);

            // The SOURCE changed; the grid is reflecting it, not editing it. Announcing it as a cell
            // value change would make a CellValueChanged handler write it back to where it came from.
            Assert.Equal (0, value_changed);
        }

        [Fact]
        public void The_selection_survives_a_list_change ()
        {
            // The W5.2 interaction: selection lives on the row objects, so a rebuild discarded it.
            var (grid, list) = Bound ();
            using var _grid = grid;
            grid.ClearSelection ();
            grid.Rows[1].Selected = true;
            grid.Rows[2].Selected = true;

            list.Add (new Item { Name = "new" });

            Assert.Equal (2, grid.SelectedRows.Count);
            Assert.True (grid.Rows[1].Selected);
            Assert.True (grid.Rows[2].Selected);
        }

        [Fact]
        public void A_hidden_row_stays_hidden_through_a_list_change ()
        {
            var (grid, list) = Bound ();
            using var _grid = grid;
            grid.Rows[1].Visible = false;

            list[0].Qty = 5;
            list.Add (new Item { Name = "new" });

            Assert.False (grid.Rows[1].Visible);
        }

        [Fact]
        public void A_Reset_over_the_same_schema_keeps_the_columns ()
        {
            // ResetBindings, a re-sort, a filter: the LIST changed wholesale but its shape did not, so
            // the rows are rebuilt and the columns -- with the app's changes -- are left alone.
            var (grid, list) = Bound ();
            using var _grid = grid;
            grid.Columns["Name"]!.HeaderText = "Customer";
            var name_column = grid.Columns["Name"];

            list.ResetBindings ();

            Assert.Same (name_column, grid.Columns["Name"]);
            Assert.Equal ("Customer", grid.Columns["Name"]!.HeaderText);
            Assert.Equal (3, grid.Rows.Count);
        }

        [Fact]
        public void Sorting_a_DataView_keeps_the_columns ()
        {
            // The ITypedList half of the same guard: a DataView raises Reset when its Sort changes, and
            // the schema has not moved -- so the app's header renames must survive a sort.
            HeadlessRenderer.Use ();
            var table = new DataTable ();
            table.Columns.Add ("name", typeof (string));
            table.Columns.Add ("qty", typeof (int));
            table.Rows.Add ("b", 2);
            table.Rows.Add ("a", 1);
            using var grid = new DataGridView { DataSource = table };
            grid.Columns["name"]!.HeaderText = "Customer";
            var name_column = grid.Columns["name"];

            table.DefaultView.Sort = "name";

            Assert.Same (name_column, grid.Columns["name"]);
            Assert.Equal ("Customer", grid.Columns["name"]!.HeaderText);
            Assert.Equal ("a", grid.Rows[0].Cells[0].Value);      // and the rows did re-sort
        }

        [Fact]
        public void A_Reset_that_changes_the_schema_regenerates_the_columns ()
        {
            // The sequence a designer produces: the grid binds to a BindingSource that has not resolved
            // yet, and the form points it at a table afterwards. That Reset MUST regenerate.
            HeadlessRenderer.Use ();
            var set = new DataSet ();
            var table = set.Tables.Add ("people");
            table.Columns.Add ("name", typeof (string));
            table.Columns.Add ("active", typeof (bool));
            table.Rows.Add ("ada", true);
            var source = new BindingSource { DataSource = set };
            using var grid = new DataGridView { DataSource = source };
            Assert.Empty (grid.Columns);

            source.DataMember = "people";

            Assert.Equal (2, grid.Columns.Count);
            Assert.Single (grid.Rows);
        }

        [Fact]
        public void Deleting_the_current_row_moves_the_current_cell_rather_than_leaving_it_dangling ()
        {
            var (grid, list) = Bound ();
            using var _grid = grid;
            grid.SelectedRowIndex = 2;
            var moved = 0;
            grid.CurrentCellChanged += (_, _) => moved++;

            list.RemoveAt (2);

            Assert.Equal (1, grid.SelectedRowIndex);
            Assert.NotNull (grid.CurrentRow);
            Assert.Equal (1, moved);
        }

        [Fact]
        public void The_grids_own_write_back_does_not_rebuild_the_grid ()
        {
            // EndEdit writes to the bound item, which raises ItemChanged, which used to rebuild every
            // row in the middle of the commit.
            var (grid, list) = Bound ();
            using var form = new Form { Width = 500, Height = 300 };
            form.Controls.Add (grid);
            var row0 = grid.Rows[0];
            grid.MoveCurrentCell (0, 1);
            var value_changed = 0;
            grid.CellValueChanged += (_, _) => value_changed++;

            grid.BeginEdit (0, 1);
            Enumerable.OfType<TextBox> (grid.Controls).First ().Text = "77";
            grid.EndEdit ();

            Assert.Equal (77, list[0].Qty);
            Assert.Same (row0, grid.Rows[0]);
            Assert.Equal (1, value_changed);
        }

        [Fact]
        public void Assigning_a_null_source_clears_the_rows ()
        {
            // GUARD, not proof: the full rebind always cleared. It pins that the incremental path did not
            // lose the one case that must still be wholesale.
            var (grid, _) = Bound ();
            using var _grid = grid;

            grid.DataSource = null;

            Assert.Empty (grid.Rows);
        }

        // ---------------- DGV-03: Rows.Add returns the new row's index

        [Fact]
        public void Rows_Add_returns_the_index_of_the_row_it_added ()
        {
            // The canonical unbound idiom: int i = grid.Rows.Add (); grid.Rows[i].Cells[0].Value = …
            // It returned Count, so Rows[i] threw on every call.
            HeadlessRenderer.Use ();
            using var grid = new DataGridView ();
            grid.Columns.Add ("c", "C");

            var first = grid.Rows.Add ();
            var second = grid.Rows.Add ();

            Assert.Equal (0, first);
            Assert.Equal (1, second);
            grid.Rows[second].Cells[0].Value = "works";
        }

        [Fact]
        public void Rows_Add_count_returns_the_index_of_the_last_row_added ()
        {
            HeadlessRenderer.Use ();
            using var grid = new DataGridView ();
            grid.Columns.Add ("c", "C");
            grid.Rows.Add ();

            Assert.Equal (3, grid.Rows.Add (3));
            Assert.Equal (4, grid.Rows.Count);
        }

        [Fact]
        public void Rows_Add_refuses_a_count_below_one ()
        {
            HeadlessRenderer.Use ();
            using var grid = new DataGridView ();

            Assert.Throws<ArgumentOutOfRangeException> (() => grid.Rows.Add (0));
        }

        // ---------------- DGV-32: DataSource and DataMember

        [Fact]
        public void DataSource_returns_what_was_assigned ()
        {
            // ((DataTable)grid.DataSource).Rows threw InvalidCastException: the getter returned the
            // DataView the table resolved to.
            HeadlessRenderer.Use ();
            var table = new DataTable ();
            table.Columns.Add ("a");
            using var grid = new DataGridView { DataSource = table };

            Assert.Same (table, grid.DataSource);
        }

        [Fact]
        public void DataMember_selects_the_table_in_a_DataSet ()
        {
            // The classic ADO.NET shape. DataMember was stored and never consulted, so this showed the
            // DataViewManager's columns rather than the Orders table's.
            HeadlessRenderer.Use ();
            var set = new DataSet ();
            var customers = set.Tables.Add ("Customers");
            customers.Columns.Add ("CustomerName");
            var orders = set.Tables.Add ("Orders");
            orders.Columns.Add ("OrderId", typeof (int));
            orders.Columns.Add ("Total", typeof (decimal));
            orders.Rows.Add (1, 9.5m);
            using var grid = new DataGridView ();

            grid.DataSource = set;
            grid.DataMember = "Orders";

            Assert.Equal (new[] { "OrderId", "Total" }, grid.Columns.Cast<DataGridViewColumn> ().Select (c => c.DataPropertyName).ToArray ());
            Assert.Single (grid.Rows);
            Assert.Same (set, grid.DataSource);
        }

        [Fact]
        public void Changing_DataMember_rebinds_to_the_other_table ()
        {
            HeadlessRenderer.Use ();
            var set = new DataSet ();
            set.Tables.Add ("A").Columns.Add ("x");
            set.Tables.Add ("B").Columns.Add ("y");
            using var grid = new DataGridView { DataSource = set, DataMember = "A" };
            Assert.Equal ("x", grid.Columns[0].DataPropertyName);

            grid.DataMember = "B";

            Assert.Equal ("y", grid.Columns[0].DataPropertyName);
        }

        [Fact]
        public void A_source_that_is_not_a_list_is_rejected_rather_than_ignored ()
        {
            // It fell through the switch to `_ => data_source`, keeping the PREVIOUS list on screen with
            // no error -- the most misleading outcome available.
            var (grid, list) = Bound ();
            using var _grid = grid;

            Assert.Throws<ArgumentException> (() => grid.DataSource = new object ());
        }

        [Fact]
        public void An_enumerable_query_is_materialised ()
        {
            // customers.Where (…) is not an IList. It used to hit the same fall-through and leave the
            // old data on screen.
            HeadlessRenderer.Use ();
            var items = new List<Item> { new () { Name = "a", Qty = 1 }, new () { Name = "b", Qty = 2 }, new () { Name = "c", Qty = 3 } };
            using var grid = new DataGridView ();

            grid.DataSource = items.Where (i => i.Qty >= 2);

            Assert.Equal (2, grid.Rows.Count);
        }

        // ---------------- DGV-33: the row events balance

        [Fact]
        public void Binding_raises_RowsAdded_once_for_all_the_rows ()
        {
            // The row-colouring RowsAdded handler is a top-five idiom, and it never ran for bound rows:
            // ReplaceAll raised nothing.
            HeadlessRenderer.Use ();
            var list = new BindingList<Item> { new (), new (), new () };
            using var grid = new CountingGrid { Width = 400, Height = 200 };

            grid.DataSource = list;

            Assert.Equal ((0, 3), Assert.Single (grid.Added));
        }

        [Fact]
        public void A_rebind_balances_its_removals_and_additions ()
        {
            var (grid, _) = Bound (3);
            using var _grid = grid;
            grid.Added.Clear ();
            grid.Removed.Clear ();

            grid.DataSource = new BindingList<Item> { new (), new () };

            // Counters that track RowsAdded/RowsRemoved used to go negative: N removed, 0 added.
            Assert.Equal (3, grid.Removed.Sum (r => r.Count));
            Assert.Equal (2, grid.Added.Sum (a => a.Count));
            Assert.Equal (2, grid.Rows.Count);
        }

        [Fact]
        public void Adding_rows_by_hand_to_a_bound_grid_throws ()
        {
            // A hand-added row on a bound grid is a phantom the next ListChanged deletes. Upstream
            // refuses up front, and so does this grid now.
            var (grid, _) = Bound ();
            using var _grid = grid;

            Assert.Throws<InvalidOperationException> (() => grid.Rows.Add ());
            Assert.Throws<InvalidOperationException> (() => grid.Rows.Add ("x", 1));
            Assert.Throws<InvalidOperationException> (() => grid.Rows.Insert (0, new DataGridViewRow ()));
        }

        [Fact]
        public void Insert_on_an_unbound_grid_raises_RowsAdded ()
        {
            // Insert (int, row) went straight to Items.Insert and bypassed the event.
            HeadlessRenderer.Use ();
            using var grid = new CountingGrid ();
            grid.Columns.Add ("c", "C");
            grid.Rows.Add ();
            grid.Added.Clear ();

            grid.Rows.Insert (0, new DataGridViewRow ());

            Assert.Equal ((0, 1), Assert.Single (grid.Added));
        }

        [Fact]
        public void An_unbound_grid_still_adds_rows_normally ()
        {
            // GUARD, not proof: the unbound path was never broken. It pins that the bound guard did not
            // start refusing every grid.
            HeadlessRenderer.Use ();
            using var grid = new DataGridView ();
            grid.Columns.Add ("c", "C");

            grid.Rows.Add ("a");
            grid.Rows.Add (2);
            grid.Rows.Insert (0, new DataGridViewRow ());

            Assert.Equal (4, grid.Rows.Count);
        }
    }
}
