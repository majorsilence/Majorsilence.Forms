using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// A grid must track a data source that is filled AFTER it was bound. Designer code assigns
    /// DataSource inside InitializeComponent and the form loads its data afterwards, so binding to a
    /// still-empty source is the normal path, not an edge case.
    /// </summary>
    public class DataGridViewLiveBindingTests
    {
        private static (DataSet Set, DataTable Table) MakeSet ()
        {
            var set = new DataSet ();
            var table = set.Tables.Add ("people");
            table.Columns.Add ("name", typeof (string));
            table.Columns.Add ("active", typeof (bool));
            return (set, table);
        }

        [Fact]
        public void Grid_picks_up_rows_added_after_it_was_bound ()
        {
            var (set, table) = MakeSet ();
            var source = new BindingSource { DataSource = set, DataMember = "people" };

            using var grid = new DataGridView { DataSource = source };
            Assert.Empty (grid.Rows);

            table.Rows.Add ("ada", true);
            table.Rows.Add ("grace", false);

            Assert.Equal (2, grid.Rows.Count);
        }

        [Fact]
        public void Grid_picks_up_a_DataMember_assigned_after_it_was_bound ()
        {
            // The sequence a designer actually produces: the grid binds to a BindingSource that has not
            // resolved to anything yet, and the form points it at a table afterwards.
            var (set, table) = MakeSet ();
            table.Rows.Add ("ada", true);

            var source = new BindingSource { DataSource = set };
            using var grid = new DataGridView { DataSource = source };
            Assert.Empty (grid.Columns);

            source.DataMember = "people";

            Assert.Equal (2, grid.Columns.Count);
            Assert.Single (grid.Rows);
        }

        [Fact]
        public void Grid_stops_tracking_a_source_it_is_no_longer_bound_to ()
        {
            var (set, table) = MakeSet ();
            var source = new BindingSource { DataSource = set, DataMember = "people" };

            using var grid = new DataGridView { DataSource = source };
            grid.DataSource = null;

            table.Rows.Add ("ada", true);

            // Still detached -- and, just as importantly, the old source no longer holds the grid alive.
            Assert.Empty (grid.Rows);
        }

        [Fact]
        public void BindingSource_forwards_the_underlying_list_change ()
        {
            var (set, table) = MakeSet ();
            var source = new BindingSource { DataSource = set, DataMember = "people" };

            var events = new List<ListChangedType> ();
            source.ListChanged += (_, e) => events.Add (e.ListChangedType);

            table.Rows.Add ("ada", true);

            Assert.NotEmpty (events);
        }

        [Fact]
        public void BindingSource_raises_a_reset_when_it_re_resolves ()
        {
            var (set, _) = MakeSet ();
            var source = new BindingSource { DataSource = set };

            var resets = 0;
            source.ListChanged += (_, e) => { if (e.ListChangedType == ListChangedType.Reset) resets++; };

            source.DataMember = "people";

            Assert.Equal (1, resets);
        }

        [Fact]
        public void RaiseListChangedEvents_false_suppresses_notification_and_re_enabling_resets ()
        {
            var (set, table) = MakeSet ();
            var source = new BindingSource { DataSource = set, DataMember = "people" };

            var events = 0;
            source.ListChanged += (_, _) => events++;

            source.RaiseListChangedEvents = false;
            table.Rows.Add ("ada", true);
            Assert.Equal (0, events);

            // Re-enabling has to tell the bindings something changed while they were not listening.
            source.RaiseListChangedEvents = true;
            Assert.Equal (1, events);
        }

        [Fact]
        public void ResetBindings_notifies_bound_controls ()
        {
            var source = new BindingSource { DataSource = new List<string> { "ada" } };
            var events = new List<ListChangedType> ();
            source.ListChanged += (_, e) => events.Add (e.ListChangedType);

            source.ResetBindings (metaDataChanged: false);
            source.ResetBindings (metaDataChanged: true);

            Assert.Contains (ListChangedType.Reset, events);
            Assert.Contains (ListChangedType.PropertyDescriptorChanged, events);
        }

        [Fact]
        public void BindingSource_is_an_IBindingList_so_controls_can_subscribe_generically ()
        {
            // How the grid finds the notification: it looks for IBindingList, not for BindingSource.
            var source = new BindingSource ();
            Assert.IsAssignableFrom<IBindingList> (source);
        }

        [Fact]
        public void A_dictionary_source_is_materialized_rather_than_ignored ()
        {
            // A SortedDictionary is IEnumerable but not IList. Binding a combo to one is ordinary code,
            // and callers compute indices from the resulting Items.Count.
            var saved = new SortedDictionary<int, string> { [0] = "first", [1] = "second" };
            var source = new BindingSource (saved, null!);

            Assert.Equal (2, source.List.Count);
            Assert.IsType<KeyValuePair<int, string>> (source.List[0]);
        }

        [Fact]
        public void A_set_source_is_materialized ()
        {
            var source = new BindingSource { DataSource = new HashSet<string> { "ada", "grace" } };
            Assert.Equal (2, source.List.Count);
        }

        [Fact]
        public void A_string_source_is_not_treated_as_a_list_of_characters ()
        {
            // string is IEnumerable; enumerating it would bind five rows of chars for "hello".
            var source = new BindingSource { DataSource = "hello" };
            Assert.Empty (source.List);
        }

        [Fact]
        public void An_IList_source_is_used_directly_not_copied ()
        {
            var backing = new List<string> { "ada" };
            var source = new BindingSource { DataSource = backing };

            Assert.Same (backing, source.List);
        }

        [Fact]
        public void Dictionary_entries_expose_Key_and_Value_for_DisplayMember_binding ()
        {
            var saved = new SortedDictionary<int, string> { [0] = "first" };
            var source = new BindingSource (saved, null!);
            var properties = ((ITypedList)source).GetItemProperties (null);

            Assert.NotNull (properties["Key"]);
            Assert.NotNull (properties["Value"]);
        }

        [Fact]
        public void Position_change_raises_CurrentChanged ()
        {
            var source = new BindingSource { DataSource = new List<string> { "ada", "grace" } };
            var fired = 0;
            source.CurrentChanged += (_, _) => fired++;

            source.Position = 1;
            Assert.Equal (1, fired);

            // Setting the same position again is not a change.
            source.Position = 1;
            Assert.Equal (1, fired);
        }
    }

    /// <summary>
    /// WinForms chooses the generated column's class from the bound member's type. Generating a text
    /// column for everything renders a bool as the literal "True" and an image as its type name.
    /// </summary>
    public class DataGridViewGeneratedColumnTypeTests
    {
        private sealed class Row
        {
            public string Name { get; set; } = string.Empty;
            public bool Active { get; set; }
            public bool? Maybe { get; set; }
            public Majorsilence.Forms.Drawing.Image? Picture { get; set; }
        }

        [Fact]
        public void Bool_member_generates_a_checkbox_column ()
        {
            using var grid = new DataGridView { DataSource = new List<Row> { new () } };
            Assert.IsType<DataGridViewCheckBoxColumn> (grid.Columns["Active"]);
        }

        [Fact]
        public void Nullable_bool_member_generates_a_checkbox_column ()
        {
            using var grid = new DataGridView { DataSource = new List<Row> { new () } };
            Assert.IsType<DataGridViewCheckBoxColumn> (grid.Columns["Maybe"]);
        }

        [Fact]
        public void Image_member_generates_an_image_column ()
        {
            using var grid = new DataGridView { DataSource = new List<Row> { new () } };
            Assert.IsType<DataGridViewImageColumn> (grid.Columns["Picture"]);
        }

        [Fact]
        public void Other_members_still_generate_plain_columns ()
        {
            using var grid = new DataGridView { DataSource = new List<Row> { new () } };
            var column = grid.Columns["Name"];

            Assert.NotNull (column);
            Assert.Equal (typeof (DataGridViewColumn), column!.GetType ());
        }

        [Fact]
        public void Generated_columns_keep_Name_and_DataPropertyName ()
        {
            using var grid = new DataGridView { DataSource = new List<Row> { new () } };
            var column = grid.Columns["Active"]!;

            Assert.Equal ("Active", column.Name);
            Assert.Equal ("Active", column.DataPropertyName);
        }

        [Fact]
        public void Bound_DataTable_bool_column_generates_a_checkbox_column ()
        {
            var table = new DataTable ("t");
            table.Columns.Add ("flag", typeof (bool));
            table.Rows.Add (true);

            using var grid = new DataGridView { DataSource = table };
            Assert.IsType<DataGridViewCheckBoxColumn> (grid.Columns["flag"]);
        }
    }

    /// <summary>
    /// A column header cell belongs to a column rather than a row. It still has to be able to answer
    /// which column, which grid, and which rectangle it occupies -- custom header cells paint and
    /// hit-test against exactly those.
    /// </summary>
    public class DataGridViewHeaderCellOwnershipTests
    {
        [Fact]
        public void Header_cell_knows_its_column ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", 50);

            Assert.Same (grid.Columns[0], grid.Columns[0].HeaderCell.OwningColumn);
        }

        [Fact]
        public void Header_cell_knows_its_grid_and_column_index ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", 50);
            grid.Columns.Add ("b", 50);

            Assert.Same (grid, grid.Columns[1].HeaderCell.DataGridView);
            Assert.Equal (1, grid.Columns[1].HeaderCell.ColumnIndex);
        }

        [Fact]
        public void A_substituted_header_cell_is_linked_to_its_column ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", 50);

            var replacement = new DataGridViewColumnHeaderCell ();
            grid.Columns[0].HeaderCell = replacement;

            Assert.Same (grid.Columns[0], replacement.OwningColumn);
            Assert.Same (grid, replacement.DataGridView);
        }

        [Fact]
        public void Assigning_a_null_header_cell_leaves_a_usable_one ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", 50);
            grid.Columns[0].HeaderCell = null!;

            Assert.NotNull (grid.Columns[0].HeaderCell);
            Assert.Same (grid.Columns[0], grid.Columns[0].HeaderCell.OwningColumn);
        }

        [Fact]
        public void Row_index_minus_one_addresses_the_column_header ()
        {
            using var grid = new DataGridView { Width = 400, Height = 300 };
            grid.Columns.Add ("a", 60);
            grid.Columns.Add ("b", 60);

            var header = grid.GetCellDisplayRectangle (1, -1, false);

            Assert.NotEqual (Rectangle.Empty, header);
            Assert.Equal (grid.ScaledHeaderHeight, header.Height);
        }

        [Fact]
        public void Row_index_below_minus_one_is_still_empty ()
        {
            using var grid = new DataGridView { Width = 400, Height = 300 };
            grid.Columns.Add ("a", 60);

            Assert.Equal (Rectangle.Empty, grid.GetCellDisplayRectangle (0, -2, false));
        }

        [Fact]
        public void Header_rectangle_is_empty_when_headers_are_hidden ()
        {
            using var grid = new DataGridView { Width = 400, Height = 300, ColumnHeadersVisible = false };
            grid.Columns.Add ("a", 60);

            Assert.Equal (Rectangle.Empty, grid.GetCellDisplayRectangle (0, -1, false));
        }
    }
}
