using System.Collections.Generic;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A list control read its data source ONCE, when the property was assigned. That is the wrong moment
    // for the way designer code is written -- InitializeComponent assigns DataSource and the form fills
    // the data afterwards -- so the control kept the empty list it saw at bind time and never showed a
    // row. BindingSource.ListChanged exists to say when to look again and nothing was listening.
    [Collection ("Headless")]
    public class ListControlDataSourceTrackingTests
    {
        private sealed class Person
        {
            public Person (string name) => Name = name;
            public string Name { get; set; }
        }

        [Fact]
        public void A_ListBox_shows_rows_added_after_its_DataSource_was_assigned ()
        {
            HeadlessRenderer.Use ();

            var source = new BindingSource { DataSource = new List<Person> () };
            using var list = new ListBox { DataSource = source };   // bound while empty, as designer code does

            Assert.Empty (list.Items);

            source.Add (new Person ("Ada"));

            Assert.Single (list.Items);
        }

        [Fact]
        public void A_ComboBox_shows_rows_added_after_its_DataSource_was_assigned ()
        {
            HeadlessRenderer.Use ();

            var source = new BindingSource { DataSource = new List<Person> () };
            using var combo = new ComboBox { DataSource = source };

            Assert.Empty (combo.Items);

            source.Add (new Person ("Ada"));

            Assert.Single (combo.Items);
        }

        [Fact]
        public void Moving_the_BindingSource_moves_a_bound_ListBox_selection ()
        {
            HeadlessRenderer.Use ();

            var source = new BindingSource {
                DataSource = new List<Person> { new ("Ada"), new ("Grace") },
            };
            using var list = new ListBox { DataSource = source };

            source.Position = 1;

            Assert.Equal (1, list.SelectedIndex);
        }

        [Fact]
        public void Selecting_in_a_ListBox_moves_the_BindingSource ()
        {
            HeadlessRenderer.Use ();

            // The other half of master/detail: picking a row here has to move the source so a detail view
            // bound to the same BindingSource follows.
            var source = new BindingSource {
                DataSource = new List<Person> { new ("Ada"), new ("Grace") },
            };
            using var list = new ListBox { DataSource = source };

            list.SelectedIndex = 1;

            Assert.Equal (1, source.Position);
            Assert.Equal ("Grace", ((Person)source.Current!).Name);
        }

        [Fact]
        public void A_ListBox_and_a_bound_TextBox_stay_on_the_same_item ()
        {
            HeadlessRenderer.Use ();

            // Master/detail end to end, which is the point of all of this.
            var source = new BindingSource {
                DataSource = new List<Person> { new ("Ada"), new ("Grace") },
            };
            using var list = new ListBox { DataSource = source, DisplayMember = "Name" };
            using var detail = new TextBox ();
            detail.DataBindings.Add ("Text", source, "Name");

            Assert.Equal ("Ada", detail.Text);

            list.SelectedIndex = 1;

            Assert.Equal ("Grace", detail.Text);
        }

        [Fact]
        public void Re_pointing_the_DataSource_stops_tracking_the_old_one ()
        {
            HeadlessRenderer.Use ();

            var first = new BindingSource { DataSource = new List<Person> () };
            var second = new BindingSource { DataSource = new List<Person> { new ("Grace") } };
            using var list = new ListBox { DataSource = first };

            list.DataSource = second;
            first.Add (new Person ("Ada"));   // the old source must no longer reach the control

            Assert.Single (list.Items);
            Assert.Equal ("Grace", ((Person)list.Items[0]!).Name);
        }

        [Fact]
        public void Display_and_value_members_match_a_column_whatever_its_case ()
        {
            // As upstream's binding resolves them (PropertyDescriptorCollection.Find, ignoring case): a
            // DisplayMember of "Description" over a "description" column showed "System.Data.DataRowView".
            HeadlessRenderer.Use ();

            var table = new System.Data.DataTable ();
            table.Columns.Add ("code", typeof (string));
            table.Columns.Add ("description", typeof (string));
            table.Rows.Add ("PP", "PP - Prepayment");

            using var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = table };
            combo.DisplayMember = "Description";
            combo.ValueMember = "Code";
            combo.SelectedIndex = 0;

            Assert.Equal ("PP - Prepayment", combo.GetItemText (combo.SelectedItem));
            Assert.Equal ("PP", combo.SelectedValue);
        }

        private static System.Data.DataTable TwoPeople ()
        {
            var table = new System.Data.DataTable ();
            table.Columns.Add ("name", typeof (string));
            table.Rows.Add ("Ada");
            table.Rows.Add ("Grace");
            return table;
        }

        // Upstream's ListControl.DataManager_ItemChanged loads the items and then selects the source's current
        // item, so a list bound on a shown form starts on the first row and raises SelectedIndexChanged -- the
        // event forms fill their detail fields from. Here nothing was selected and the details stayed empty.
        [Fact]
        public void Binding_a_ListBox_on_a_form_selects_the_current_row ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var list = new ListBox ();
            form.Controls.Add (list);
            form.Show ();

            var changes = 0;
            list.SelectedIndexChanged += (s, e) => changes++;
            list.DataSource = TwoPeople ();

            Assert.Equal (0, list.SelectedIndex);
            Assert.True (changes > 0, "SelectedIndexChanged was not raised");
        }

        [Fact]
        public void Binding_a_ComboBox_on_a_form_selects_the_current_row ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            form.Controls.Add (combo);
            form.Show ();

            combo.DataSource = TwoPeople ();

            Assert.Equal (0, combo.SelectedIndex);
        }

        [Fact]
        public void A_ListBox_without_selection_stays_unselected_when_bound ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();
            var list = new ListBox { SelectionMode = SelectionMode.None };
            form.Controls.Add (list);
            form.Show ();

            list.DataSource = TwoPeople ();

            Assert.Equal (-1, list.SelectedIndex);
        }
    }
}
