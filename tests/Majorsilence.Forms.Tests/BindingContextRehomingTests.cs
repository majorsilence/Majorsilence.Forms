using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Binding-context plumbing from #340: designer-order binding (BND-15), the collection's Add rules
    // (BND-17), BindableComponent (BND-25), list controls sharing a plain source's manager (BND-26),
    // BindingSource's Allow* answers (BND-22) and the long Binding constructors (BND-33).
    [Collection ("Headless")]
    public class BindingContextRehomingTests
    {
        private sealed class Person
        {
            public Person (string name) => Name = name;
            public string Name { get; set; }
        }

        private static List<Person> People () => new () { new ("Ada"), new ("Grace"), new ("Linus") };

        // ── BND-15 ───────────────────────────────────────────────────────────────

        [Fact]
        public void Controls_bound_before_they_are_parented_follow_the_forms_manager ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var form = new Form ();

            // InitializeComponent order: bind first, parent afterwards.
            var a = new TextBox ();
            var b = new TextBox ();
            a.DataBindings.Add ("Text", people, "Name");
            b.DataBindings.Add ("Text", people, "Name");
            form.Controls.Add (a);
            form.Controls.Add (b);

            form.BindingContext[people].Position = 1;

            Assert.Equal ("Grace", a.Text);
            Assert.Equal ("Grace", b.Text);
            Assert.Same (form.BindingContext[people], a.DataBindings[0].BindingManagerBase);
        }

        [Fact]
        public void A_bound_control_inside_a_panel_rehomes_when_the_panel_is_parented ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var form = new Form ();
            var panel = new Panel ();
            var box = new TextBox ();
            panel.Controls.Add (box);
            box.DataBindings.Add ("Text", people, "Name");

            form.Controls.Add (panel);
            form.BindingContext[people].Position = 2;

            Assert.Equal ("Linus", box.Text);
        }

        [Fact]
        public void Setting_a_controls_own_context_moves_its_bindings_onto_it ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", people, "Name");

            var context = new BindingContext ();
            box.BindingContext = context;
            context[people].Position = 1;

            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void An_unparented_control_does_not_keep_a_private_context ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var box = new TextBox ();
            var provisional = box.BindingContext;

            form.Controls.Add (box);

            Assert.NotSame (provisional, box.BindingContext);
            Assert.Same (form.BindingContext, box.BindingContext);
        }

        // ── BND-17 ───────────────────────────────────────────────────────────────

        [Fact]
        public void A_second_binding_to_the_same_property_is_refused ()
        {
            HeadlessRenderer.Use ();

            var person = new Person ("Ada");
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", person, "Name");

            Assert.Throws<ArgumentException> (() => box.DataBindings.Add ("Text", person, "Name"));
            Assert.Single (box.DataBindings);
        }

        [Fact]
        public void A_binding_that_fails_to_attach_is_not_left_in_the_collection ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();

            Assert.Throws<ArgumentException> (() => box.DataBindings.Add ("NoSuchProperty", new Person ("Ada"), "Name"));
            Assert.Empty (box.DataBindings);
        }

        [Fact]
        public void The_long_Add_overload_refuses_a_null_data_source ()
        {
            HeadlessRenderer.Use ();

            using var box = new TextBox ();

            Assert.Throws<ArgumentNullException> (() => box.DataBindings.Add (
                "Text", null, "Name", false, DataSourceUpdateMode.OnValidation, null, null, null));
        }

        [Fact]
        public void The_short_Add_uses_the_collections_default_update_mode ()
        {
            HeadlessRenderer.Use ();

            // Guard: already wired by the W6.2 sweep before #340; kept so the P1's headline stays pinned.
            using var box = new TextBox ();
            box.DataBindings.DefaultDataSourceUpdateMode = DataSourceUpdateMode.OnPropertyChanged;

            var binding = box.DataBindings.Add ("Text", new Person ("Ada"), "Name");

            Assert.Equal (DataSourceUpdateMode.OnPropertyChanged, binding.DataSourceUpdateMode);
        }

        // ── BND-25 ───────────────────────────────────────────────────────────────

        private sealed class Bindable : BindableComponent
        {
            public string Caption { get; set; } = string.Empty;
        }

        [Fact]
        public void A_BindableComponent_subclass_can_be_bound ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var component = new Bindable ();

            component.DataBindings.Add ("Caption", people, "Name");

            Assert.Same (component, component.DataBindings.BindableComponent);
            Assert.Equal ("Ada", component.Caption);
        }

        [Fact]
        public void A_BindableComponents_new_context_takes_its_bindings ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var component = new Bindable ();
            component.DataBindings.Add ("Caption", people, "Name");

            var context = new BindingContext ();
            component.BindingContext = context;
            context[people].Position = 2;

            Assert.Equal ("Linus", component.Caption);
        }

        // ── BND-26 ───────────────────────────────────────────────────────────────

        [Fact]
        public void Selecting_in_a_ListBox_over_a_plain_list_moves_a_bound_TextBox ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var form = new Form ();
            var list = new ListBox { DataSource = people, DisplayMember = "Name" };
            var box = new TextBox ();
            box.DataBindings.Add ("Text", people, "Name");
            form.Controls.Add (list);
            form.Controls.Add (box);

            list.SelectedIndex = 2;

            Assert.Equal ("Linus", box.Text);
        }

        [Fact]
        public void Moving_the_forms_manager_moves_a_ComboBox_over_a_plain_list ()
        {
            HeadlessRenderer.Use ();

            var people = People ();
            using var form = new Form ();
            var combo = new ComboBox { DataSource = people, DisplayMember = "Name" };
            form.Controls.Add (combo);

            form.BindingContext[people].Position = 1;

            Assert.Equal (1, combo.SelectedIndex);
        }

        // ── BND-22 ───────────────────────────────────────────────────────────────

        [Fact]
        public void An_array_source_allows_neither_new_rows_nor_removal ()
        {
            var source = new BindingSource { DataSource = new[] { new Person ("Ada"), new Person ("Grace") } };

            Assert.False (source.AllowNew);
            Assert.False (source.AllowRemove);
            Assert.True (source.AllowEdit);
        }

        [Fact]
        public void A_read_only_source_allows_no_edits ()
        {
            var source = new BindingSource {
                DataSource = new ReadOnlyCollection<Person> (new List<Person> { new ("Ada") }),
            };

            Assert.False (source.AllowEdit);
            Assert.False (source.AllowRemove);
        }

        [Fact]
        public void Setting_AllowNew_announces_a_reset ()
        {
            var source = new BindingSource { DataSource = new List<Person> { new ("Ada") } };
            var resets = 0;
            source.ListChanged += (_, e) => { if (e.ListChangedType == ListChangedType.Reset) resets++; };

            source.AllowNew = false;
            source.AllowNew = false;   // unchanged: not a second reset

            Assert.Equal (1, resets);
        }

        [Fact]
        public void AllowNew_cannot_be_forced_on_over_an_array ()
        {
            var source = new BindingSource { DataSource = new[] { new Person ("Ada") } };

            Assert.Throws<InvalidOperationException> (() => source.AllowNew = true);
        }

        // ── BND-33 ───────────────────────────────────────────────────────────────

        [Fact]
        public void The_long_Binding_constructor_carries_every_argument ()
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var binding = new Binding ("Text", new Person ("Ada"), "Name", true,
                DataSourceUpdateMode.OnPropertyChanged, "(none)", "N2", culture);

            Assert.Equal ("(none)", binding.NullValue);
            Assert.Equal ("N2", binding.FormatString);
            Assert.Same (culture, binding.FormatInfo);
            Assert.Equal (DataSourceUpdateMode.OnPropertyChanged, binding.DataSourceUpdateMode);
        }
    }
}
