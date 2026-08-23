using System.Collections.Generic;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A list control read its data source ONCE, when the property was assigned. That is the wrong moment
    // for the way designer code is written -- InitializeComponent assigns DataSource and the form fills
    // the data afterwards -- so the control kept the empty list it saw at bind time and never showed a
    // row. BindingSource.ListChanged exists to say when to look again and nothing was listening.
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
    }
}
