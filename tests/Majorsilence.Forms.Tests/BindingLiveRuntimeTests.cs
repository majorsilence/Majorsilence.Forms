using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Phase 4 (W4.1-W4.6): the binding runtime as a LIVE system rather than a snapshot. Each test names
    // the finding it closes (docs/behaviour-gap/binding.md); the shapes are the findings' own repros,
    // which are in turn the shapes designer-generated code produces. The recurring theme: everything
    // here used to fail SILENTLY -- blank controls, zeroed records, dead buttons -- so these assert the
    // value moved, not merely that nothing threw.
    public class BindingLiveRuntimeTests
    {
        private sealed class Person
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public List<string> Orders { get; } = new ();
        }

        private static TextBox Box ()
        {
            HeadlessRenderer.Use ();
            return new TextBox ();
        }

        // ── W4.1: the manager survives the designer's own ordering ─────────────────────────────

        [Fact]
        public void A_binding_attached_before_the_data_arrives_still_works ()   // BND-01
        {
            // The exact order InitializeComponent + Load run in: BeginInit, bind, EndInit, THEN data.
            var source = new BindingSource ();
            using var box = Box ();

            source.BeginInit ();
            box.DataBindings.Add ("Text", source, "Name");
            source.EndInit ();

            source.DataSource = new List<Person> { new () { Name = "Ada" }, new () { Name = "Grace" } };

            Assert.Equal ("Ada", box.Text);

            source.Position = 1;

            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void Reassigning_DataSource_reaches_a_binding_attached_earlier ()   // BND-01
        {
            var source = new BindingSource { DataSource = new List<Person> { new () { Name = "old" } } };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            // 0 -> 0 across two different lists: the index does not move, the current item does.
            source.DataSource = new List<Person> { new () { Name = "new" } };

            Assert.Equal ("new", box.Text);
        }

        [Fact]
        public void The_first_item_added_to_an_empty_source_becomes_current ()   // BND-02
        {
            var list = new BindingList<Person> ();
            var source = new BindingSource { DataSource = list };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            list.Add (new Person { Name = "Ada" });

            Assert.Equal (0, source.Position);
            Assert.Equal ("Ada", box.Text);
        }

        [Fact]
        public void Removing_the_last_item_clears_the_bound_control ()   // BND-02
        {
            var source = new BindingSource { DataSource = new BindingList<Person> { new () { Name = "Ada" } } };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            Assert.Equal ("Ada", box.Text);

            source.RemoveCurrent ();

            Assert.Equal (-1, source.Position);
            Assert.Equal (string.Empty, box.Text);
        }

        [Fact]
        public void Deleting_the_current_row_announces_the_next_one ()   // BND-02
        {
            var source = new BindingSource {
                DataSource = new BindingList<Person> { new () { Name = "Ada" }, new () { Name = "Grace" } },
            };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            // The index stays 0; the OBJECT at it changes. The silent version of this left detail
            // fields showing the deleted record.
            source.RemoveCurrent ();

            Assert.Equal (0, source.Position);
            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void PositionChanged_is_raised_and_after_CurrentChanged ()   // BND-10, BND-20
        {
            var source = new BindingSource {
                DataSource = new List<Person> { new () { Name = "Ada" }, new () { Name = "Grace" } },
            };
            var order = new List<string> ();
            source.CurrentChanged += (_, _) => order.Add ("CurrentChanged");
            source.PositionChanged += (_, _) => order.Add ("PositionChanged");

            source.Position = 1;

            // Upstream's order, and observable: a PositionChanged handler that reads a bound control
            // must see the NEW record.
            Assert.Equal (new[] { "CurrentChanged", "PositionChanged" }, order);
        }

        [Fact]
        public void A_binding_registers_with_its_manager ()   // BND-16
        {
            var list = new List<Person> { new () { Name = "Ada" } };
            var source = new BindingSource { DataSource = list };
            using var box = Box ();

            box.DataBindings.Add ("Text", source, "Name");

            Assert.Equal (1, source.CurrencyManager.Bindings.Count);

            box.DataBindings.Clear ();

            Assert.Equal (0, source.CurrencyManager.Bindings.Count);
        }

        [Fact]
        public void Suspend_stops_both_directions_and_resume_catches_up ()   // BND-19
        {
            var person = new Person { Name = "Ada" };
            var source = new BindingSource { DataSource = new List<Person> { person } };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name", false, DataSourceUpdateMode.OnPropertyChanged);

            source.SuspendBinding ();

            box.Text = "typed-while-suspended";
            Assert.Equal ("Ada", person.Name);

            person.Name = "changed-while-suspended";
            Assert.Equal ("typed-while-suspended", box.Text);

            source.ResumeBinding ();

            Assert.Equal ("changed-while-suspended", box.Text);
        }

        [Fact]
        public void ResetCurrentItem_refreshes_a_control_bound_to_a_plain_object ()   // BND-14
        {
            // No INotifyPropertyChanged anywhere: ResetCurrentItem IS the documented refresh for this.
            var person = new Person { Name = "Ada" };
            var source = new BindingSource { DataSource = new List<Person> { person } };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            person.Name = "Grace";
            source.ResetCurrentItem ();

            Assert.Equal ("Grace", box.Text);
        }

        // ── W4.2: TypeDescriptor on both sides ──────────────────────────────────────────────────

        [Fact]
        public void A_DataTable_column_binds_and_writes_back ()   // BND-03
        {
            // THE typed-DataSet shape. DataRowView's columns exist only as custom descriptors; CLR
            // reflection sees Row/RowVersion/IsNew, so this bound nothing and saved nothing, silently.
            var table = new DataTable ();
            table.Columns.Add ("Name", typeof (string));
            table.Rows.Add ("Ada");

            var source = new BindingSource { DataSource = table };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            Assert.Equal ("Ada", box.Text);

            box.Text = "Grace";
            box.DataBindings[0]!.WriteValue ();
            source.EndEdit ();

            Assert.Equal ("Grace", table.Rows[0]["Name"]);
        }

        [Fact]
        public void The_target_property_name_is_case_insensitive ()   // BND-30
        {
            var person = new Person { Name = "Ada" };
            using var box = Box ();

            // A case slip that works in WinForms (TypeDescriptor, OrdinalIgnoreCase) threw here.
            box.DataBindings.Add ("text", person, "Name");

            Assert.Equal ("Ada", box.Text);
        }

        // ── W4.5: ResolveList's former catch-all ────────────────────────────────────────────────

        [Fact]
        public void DataSource_of_a_Type_yields_a_typed_empty_list_with_schema ()   // BND-04
        {
            // What the designer emits for every object data source.
            var source = new BindingSource { DataSource = typeof (Person) };

            Assert.IsType<BindingList<Person>> (source.List);
            Assert.Contains ("Name",
                ((ITypedList)source).GetItemProperties (null).Cast<PropertyDescriptor> ().Select (p => p.Name));
            Assert.IsType<Person> (source.AddNew ());
        }

        [Fact]
        public void A_child_BindingSource_follows_its_parents_current_item ()   // BND-06
        {
            // The designer's master/detail shape: new BindingSource(parent, "Orders").
            var customers = new List<Person> {
                new () { Name = "Ada", Orders = { "a1", "a2" } },
                new () { Name = "Grace", Orders = { "g1" } },
            };
            var parent = new BindingSource { DataSource = customers };
            using var child = new BindingSource (parent, "Orders");

            Assert.Equal (2, child.Count);
            Assert.Equal ("a1", child.Current);

            parent.Position = 1;

            Assert.Equal (1, child.Count);
            Assert.Equal ("g1", child.Current);
        }

        // ── W4.3 / W4.4: the edit lifecycle and the failure channel ─────────────────────────────

        [Fact]
        public void OnValidation_writes_during_Validating_so_a_handler_sees_the_new_value ()   // BND-07
        {
            var person = new Person { Age = 7 };
            using var box = Box ();
            box.DataBindings.Add ("Text", person, "Age");   // OnValidation is the default mode

            var seen = -1;
            box.Validating += (_, _) => seen = person.Age;

            box.Text = "42";
            var valid = box.Validate (true);

            Assert.True (valid);
            Assert.Equal (42, seen);
            Assert.Equal (42, person.Age);
        }

        [Fact]
        public void A_value_that_cannot_be_written_cancels_validation ()   // BND-07 + BND-13
        {
            var person = new Person { Age = 7 };
            using var box = Box ();
            box.DataBindings.Add ("Text", person, "Age");

            box.Text = "not a number";

            Assert.False (box.Validate (true));
            Assert.Equal (7, person.Age);
        }

        [Fact]
        public void A_failed_write_resets_the_control_to_the_source_value ()   // BND-13
        {
            var person = new Person { Age = 7 };
            using var box = Box ();
            box.DataBindings.Add ("Text", person, "Age", false, DataSourceUpdateMode.OnPropertyChanged);

            box.Text = "4x";

            // Upstream's recovery: what the user sees is what the record holds.
            Assert.Equal (7, person.Age);
            Assert.Equal ("7", box.Text);
        }

        [Fact]
        public void EndEdit_commits_pending_OnValidation_values ()   // BND-08
        {
            // Save from a ToolStripButton: nothing takes focus, no Validated ever fires, and EndEdit
            // used to lose the pending value of every OnValidation binding -- the default mode.
            var person = new Person { Name = "Ada" };
            var source = new BindingSource { DataSource = new List<Person> { person } };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            box.Text = "Grace";
            source.EndEdit ();

            Assert.Equal ("Grace", person.Name);
        }

        [Fact]
        public void CancelEdit_removes_an_uncommitted_AddNew_row ()   // BND-08
        {
            var list = new BindingList<Person> ();
            var source = new BindingSource { DataSource = list };

            source.AddNew ();
            source.CancelEdit ();

            Assert.Empty (list);
        }

        [Fact]
        public void CancelEdit_reverts_a_DataRow_edited_through_bindings ()   // BND-09
        {
            // The standard Cancel button of every DataSet form. DataRowView commits each column write
            // immediately unless inside BeginEdit, so without the manager opening the transaction this
            // reverted nothing and the DataSet already reported changes.
            var table = new DataTable ();
            table.Columns.Add ("Name", typeof (string));
            table.Rows.Add ("Ada");

            var source = new BindingSource { DataSource = table };
            using var box = Box ();
            box.DataBindings.Add ("Text", source, "Name");

            box.Text = "Grace";
            box.DataBindings[0]!.WriteValue ();
            source.CancelEdit ();

            Assert.Equal ("Ada", table.Rows[0]["Name"]);
            Assert.Equal ("Ada", box.Text);
        }

        [Fact]
        public void BindingComplete_carries_the_conversion_exception ()   // BND-18
        {
            var person = new Person { Age = 7 };
            using var box = Box ();
            var binding = box.DataBindings.Add ("Text", person, "Age", formattingEnabled: true,
                DataSourceUpdateMode.OnPropertyChanged);

            BindingCompleteEventArgs? got = null;
            binding.BindingComplete += (_, e) => {
                if (e.BindingCompleteState == BindingCompleteState.Exception)
                    got = e;
            };

            box.Text = "x";

            Assert.NotNull (got);
            Assert.NotNull (got!.Exception);
            Assert.Equal (7, person.Age);
        }

        [Fact]
        public void Clearing_a_bound_string_writes_the_empty_string ()   // BND-24
        {
            var person = new Person { Name = "Ada" };
            using var box = Box ();
            box.DataBindings.Add ("Text", person, "Name", false, DataSourceUpdateMode.OnPropertyChanged);

            box.Text = string.Empty;

            // "" into a string member is "", not null: the NOT NULL column upstream writes "" to
            // rejects the null this used to write.
            Assert.Equal (string.Empty, person.Name);
        }

        [Fact]
        public void An_explicit_ReadValue_refreshes_a_Never_mode_binding ()   // BND-23
        {
            var person = new Person { Name = "Ada" };
            using var box = Box ();
            var binding = box.DataBindings.Add ("Text", person, "Name");
            binding.ControlUpdateMode = ControlUpdateMode.Never;

            person.Name = "Grace";
            binding.ReadValue ();

            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void A_scalar_source_binds_through_a_PropertyManager_at_position_zero ()   // BND-31
        {
            var person = new Person { Name = "Ada" };
            using var box = Box ();
            box.DataBindings.Add ("Text", person, "Name");

            var manager = box.BindingContext![person];

            Assert.IsType<PropertyManager> (manager);
            Assert.Equal (0, manager.Position);
            Assert.Equal (1, manager.Count);
        }

        // ── W4.6: the navigator is buttons, not scenery ─────────────────────────────────────────

        [Fact]
        public void A_navigator_displays_and_drives_its_source ()   // BND-11
        {
            HeadlessRenderer.Use ();

            var source = new BindingSource {
                DataSource = new List<Person> { new () { Name = "Ada" }, new () { Name = "Grace" } },
            };
            using var navigator = new BindingNavigator ();
            navigator.AddStandardItems ();
            navigator.BindingSource = source;

            Assert.Equal ("1", navigator.PositionItem!.Text);
            Assert.Equal ("of 2", navigator.CountItem!.Text);

            navigator.MoveNextItem!.PerformClick ();

            Assert.Equal (1, source.Position);
            Assert.Equal ("2", navigator.PositionItem.Text);
            Assert.False (navigator.MoveNextItem.Enabled);
            Assert.True (navigator.MovePreviousItem!.Enabled);
        }

        [Fact]
        public void EndInit_preserves_the_designers_items ()   // BND-12
        {
            // InitializeComponent adds a custom Save button and wires its Click BEFORE EndInit runs.
            // EndInit used to clear and rebuild the strip: the Save button vanished and its handler
            // was attached to an orphan.
            HeadlessRenderer.Use ();

            using var navigator = new BindingNavigator ();
            navigator.BeginInit ();
            var save = new ToolStripButton ("Save");
            navigator.Items.Add (save);
            navigator.EndInit ();

            Assert.Contains (save, navigator.Items.Cast<ToolStripItem> ());
        }
    }
}

namespace Majorsilence.Forms.Tests
{
    public partial class BindingLiveRuntimeTests2
    {
        private sealed class Person
        {
            public string? Name { get; set; }
        }

        [Fact]
        public void UpdateBinding_rehomes_membership_subscriptions_and_value ()   // BND-28
        {
            Majorsilence.Forms.Headless.HeadlessRenderer.Use ();

            var list = new List<Person> { new () { Name = "Ada" } };
            using var box = new TextBox ();
            var binding = box.DataBindings.Add ("Text", list, "Name");
            var oldManager = binding.BindingManagerBase!;

            var context = new BindingContext ();
            BindingContext.UpdateBinding (context, binding);

            // Keyed on BindingPath, so the new manager is THE manager for this source in that context
            // -- the one every other binding to the same list shares.
            Assert.Same (context[list], binding.BindingManagerBase);
            Assert.Equal (0, oldManager.Bindings.Count);
            Assert.Equal (1, binding.BindingManagerBase!.Bindings.Count);

            // And the subscriptions moved with it: the new manager's moves reach the control.
            binding.BindingManagerBase.Position = 0;
            list[0].Name = "Grace";
            ((CurrencyManager)binding.BindingManagerBase).PushDataToBindings ();

            Assert.Equal ("Grace", box.Text);
        }
    }
}
