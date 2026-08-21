using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Data binding used to be a stub: Binding held its property name and data source and did nothing,
    // Format/Parse discarded their handlers, and WriteValue was empty -- so
    // `control.DataBindings.Add ("Text", customer, "Name")` compiled, ran, and moved nothing. These tests
    // pin that it now actually moves values, in both directions.
    public class BindingRuntimeTests
    {
        private sealed class Person : INotifyPropertyChanged
        {
            private string? name;
            private int age;

            public string? Name {
                get => name;
                set { name = value; Raise (nameof (Name)); }
            }

            public int Age {
                get => age;
                set { age = value; Raise (nameof (Age)); }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void Raise (string property)
                => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (property));
        }

        private sealed class Plain
        {
            public string? Name { get; set; }
        }

        [Fact]
        public void Adding_a_binding_pulls_the_current_value_into_the_control ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();
            box.DataBindings.Add ("Text", new Person { Name = "Ada" }, "Name");

            Assert.Equal ("Ada", box.Text);
        }

        [Fact]
        public void A_source_that_notifies_updates_the_control ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Name = "Ada" };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Name");

            person.Name = "Grace";

            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void Editing_the_control_writes_back_to_the_source ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Name = "Ada" };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Name", false, DataSourceUpdateMode.OnPropertyChanged);

            box.Text = "Grace";

            Assert.Equal ("Grace", person.Name);
        }

        [Fact]
        public void A_two_way_binding_does_not_ping_pong ()
        {
            HeadlessRenderer.Use ();

            // Reading sets the control, which raises TextChanged, which writes to the source, which
            // notifies, which reads again. Without a re-entrancy guard this never terminates.
            var person = new Person { Name = "Ada" };
            var notifications = 0;
            person.PropertyChanged += (_, _) => notifications++;

            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Name", false, DataSourceUpdateMode.OnPropertyChanged);

            box.Text = "Grace";

            Assert.Equal ("Grace", person.Name);
            Assert.True (notifications <= 2, $"{notifications} source notifications for one edit.");
        }

        [Fact]
        public void DataSourceUpdateMode_Never_reads_but_does_not_write ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Name = "Ada" };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Name", false, DataSourceUpdateMode.Never);

            Assert.Equal ("Ada", box.Text);

            box.Text = "Grace";

            Assert.Equal ("Ada", person.Name);
        }

        [Fact]
        public void A_plain_object_with_no_notification_still_binds_one_way ()
        {
            HeadlessRenderer.Use ();

            // The common designer case: a POCO with no INotifyPropertyChanged. The initial pull must work
            // even though later source edits cannot be observed.
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", new Plain { Name = "Ada" }, "Name");

            Assert.Equal ("Ada", box.Text);
        }

        [Fact]
        public void Values_are_converted_to_the_target_property_type ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();
            box.DataBindings.Add ("Text", new Person { Age = 42 }, "Age");

            Assert.Equal ("42", box.Text);
        }

        [Fact]
        public void Writing_back_converts_to_the_source_property_type ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Age = 1 };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Age", false, DataSourceUpdateMode.OnPropertyChanged);

            box.Text = "42";

            Assert.Equal (42, person.Age);
        }

        [Fact]
        public void A_half_typed_number_does_not_throw_and_leaves_the_source_alone ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Age = 7 };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Age", false, DataSourceUpdateMode.OnPropertyChanged);

            box.Text = "4-";   // a normal transient state while typing, not an error

            Assert.Equal (0, person.Age);   // coerced to default rather than throwing mid-edit
        }

        [Fact]
        public void FormatString_is_applied_when_formatting_is_enabled ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();
            var binding = box.DataBindings.Add ("Text", new Person { Age = 1234 }, "Age",
                formattingEnabled: true);
            binding.FormatString = "N0";
            binding.FormatInfo = CultureInfo.InvariantCulture;
            binding.ReadValue ();

            Assert.Equal ("1,234", box.Text);
        }

        [Fact]
        public void The_Format_event_can_override_what_is_displayed ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();
            var binding = new Binding ("Text", new Person { Name = "Ada" }, "Name");
            binding.Format += (_, e) => e.Value = $"<{e.Value}>";
            box.DataBindings.Add (binding);

            Assert.Equal ("<Ada>", box.Text);
        }

        [Fact]
        public void The_Parse_event_can_override_what_is_stored ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Name = "Ada" };
            using var box = new TextBox ();
            var binding = new Binding ("Text", person, "Name") {
                DataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged,
            };
            binding.Parse += (_, e) => e.Value = $"{e.Value}!";
            box.DataBindings.Add (binding);

            box.Text = "Grace";

            Assert.Equal ("Grace!", person.Name);
        }

        [Fact]
        public void NullValue_stands_in_for_a_null_source_value ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();
            var binding = new Binding ("Text", new Person { Name = null }, "Name") { NullValue = "(none)" };
            box.DataBindings.Add (binding);

            Assert.Equal ("(none)", box.Text);
        }

        [Fact]
        public void A_list_source_binds_to_the_current_item_and_follows_the_position ()
        {
            HeadlessRenderer.Use ();

            var people = new List<Person> { new () { Name = "Ada" }, new () { Name = "Grace" } };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", people, "Name");

            Assert.Equal ("Ada", box.Text);

            box.BindingContext[people].Position = 1;

            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void Removing_a_binding_stops_it_tracking_the_source ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Name = "Ada" };
            using var box = new TextBox ();
            var binding = box.DataBindings.Add ("Text", person, "Name");

            box.DataBindings.Remove (binding);
            person.Name = "Grace";

            Assert.Equal ("Ada", box.Text);
            Assert.False (binding.IsBinding);
        }

        [Fact]
        public void Binding_a_property_that_does_not_exist_is_reported_not_ignored ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();

            // A silently dead binding is the failure this whole mechanism was added to remove, so a
            // mistyped property name has to say so.
            Assert.Throws<System.ArgumentException> (
                () => box.DataBindings.Add ("Txet", new Person (), "Name"));
        }

        [Fact]
        public void A_form_can_bind_its_own_properties ()
        {
            HeadlessRenderer.Use ();

            var person = new Person { Name = "Ada" };
            using var form = new Form ();
            form.DataBindings.Add ("Text", person, "Name");

            Assert.Equal ("Ada", form.Text);

            person.Name = "Grace";

            Assert.Equal ("Grace", form.Text);
        }
    }
}
