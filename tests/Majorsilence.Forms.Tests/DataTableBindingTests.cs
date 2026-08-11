using System.Data;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// A DataTable is the archetypal WinForms data source, and it is NOT an IList -- it is an
    /// IListSource. Treating a non-IList source as "no source at all" left bound controls empty.
    /// </summary>
    public class DataTableBindingTests
    {
        private static DataTable Fruit ()
        {
            var t = new DataTable ();
            t.Columns.Add ("Code", typeof (string));
            t.Columns.Add ("Description", typeof (string));
            t.Rows.Add ("APL", "Apple");
            t.Rows.Add ("BAN", "Banana");
            t.Rows.Add ("CHR", "Cherry");
            return t;
        }

        [Fact]
        public void ComboBox_bound_to_a_DataTable_populates_items_from_the_display_member ()
        {
            using var cbo = new ComboBox ();
            cbo.DataSource = Fruit ();
            cbo.DisplayMember = "Description";

            Assert.Equal (3, cbo.Items.Count);

            // Items are the BOUND OBJECTS, as in WinForms -- not their display text.
            Assert.IsType<DataRowView> (cbo.Items[0]);

            // DataRowView exposes columns as property descriptors, not CLR properties, so plain
            // GetProperty reflection would have fallen back to ToString ("System.Data.DataRowView").
            Assert.Equal ("Apple", cbo.GetItemText (cbo.Items[0]));
            Assert.Equal ("Cherry", cbo.GetItemText (cbo.Items[2]));
        }

        [Fact]
        public void Setting_SelectedIndex_after_binding_a_DataTable_does_not_throw ()
        {
            // The reported failure: binding a table then selecting the first row threw
            // ArgumentOutOfRangeException because Items was still empty.
            using var cbo = new ComboBox ();
            cbo.DataSource = Fruit ();
            cbo.DisplayMember = "Description";

            cbo.SelectedIndex = 0;

            Assert.Equal (0, cbo.SelectedIndex);
        }

        [Fact]
        public void ComboBox_selects_the_first_row_when_a_source_is_bound ()
        {
            using var cbo = new ComboBox ();
            cbo.DataSource = Fruit ();

            Assert.Equal (0, cbo.SelectedIndex);
        }

        [Fact]
        public void ComboBox_SelectedValue_reads_the_value_member_from_a_DataTable ()
        {
            using var cbo = new ComboBox ();
            cbo.DataSource = Fruit ();
            cbo.DisplayMember = "Description";
            cbo.ValueMember = "Code";

            cbo.SelectedIndex = 1;
            Assert.Equal ("BAN", cbo.SelectedValue);

            cbo.SelectedValue = "CHR";
            Assert.Equal (2, cbo.SelectedIndex);
        }

        [Fact]
        public void ListBox_bound_to_a_DataTable_populates_items ()
        {
            using var lb = new ListBox ();
            lb.DataSource = Fruit ();
            lb.DisplayMember = "Description";

            Assert.Equal (3, lb.Items.Count);
            Assert.IsType<DataRowView> (lb.Items[1]);
            Assert.Equal ("Banana", lb.GetItemText (lb.Items[1]));
        }

        [Fact]
        public void A_plain_IList_source_still_binds ()
        {
            // The IList path must keep working unchanged.
            using var cbo = new ComboBox ();
            cbo.DataSource = new List<string> { "one", "two" };

            Assert.Equal (2, cbo.Items.Count);
            Assert.Equal ("one", cbo.Items[0]?.ToString ());
            Assert.Equal ("one", cbo.GetItemText (cbo.Items[0]));
        }

        [Fact]
        public void SelectedItem_is_the_bound_row_so_a_handler_can_cast_it ()
        {
            // The reported failure: a SelectedIndexChanged handler did
            //   CType(cbo.SelectedItem, DataRowView)
            // and got InvalidCastException because items had been flattened to strings.
            using var cbo = new ComboBox ();
            cbo.DataSource = Fruit ();
            cbo.DisplayMember = "Description";
            cbo.SelectedIndex = 2;

            var row = Assert.IsType<DataRowView> (cbo.SelectedItem);
            Assert.Equal ("CHR", row["Code"]);
        }
    }
}
