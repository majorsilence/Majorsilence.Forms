using System.Collections.Generic;
using System.ComponentModel;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // BindingSource had a working list/position core but three things that only matter once bindings are
    // live: it handed out a NEW CurrencyManager on every read (so nothing shared a current item),
    // AddNew put a literal null into the caller's list, and Find always answered -1.
    public class BindingSourceRuntimeTests
    {
        private sealed class Person
        {
            public Person () { }
            public Person (string name) => Name = name;
            public string? Name { get; set; }
        }

        [Fact]
        public void The_currency_manager_is_one_shared_instance ()
        {
            var source = new BindingSource { DataSource = new List<Person> { new ("Ada"), new ("Grace") } };

            Assert.Same (source.CurrencyManager, source.CurrencyManager);
        }

        [Fact]
        public void Moving_the_BindingSource_moves_the_currency_manager ()
        {
            var source = new BindingSource { DataSource = new List<Person> { new ("Ada"), new ("Grace") } };

            source.Position = 1;

            Assert.Equal (1, source.CurrencyManager.Position);
            Assert.Equal ("Grace", ((Person)source.CurrencyManager.Current!).Name);
        }

        [Fact]
        public void Moving_the_currency_manager_moves_the_BindingSource ()
        {
            var source = new BindingSource { DataSource = new List<Person> { new ("Ada"), new ("Grace") } };

            source.CurrencyManager.Position = 1;

            Assert.Equal (1, source.Position);
        }

        [Fact]
        public void A_control_bound_through_a_BindingSource_follows_its_position ()
        {
            HeadlessRenderer.Use ();

            // The point of ICurrencyManagerProvider: BindingContext used to build its own manager over the
            // BindingSource, with an independent position, so this moved nothing.
            var source = new BindingSource { DataSource = new List<Person> { new ("Ada"), new ("Grace") } };
            using var box = new TextBox ();
            box.DataBindings.Add ("Text", source, "Name");

            Assert.Equal ("Ada", box.Text);

            source.Position = 1;

            Assert.Equal ("Grace", box.Text);
        }

        [Fact]
        public void AddNew_adds_a_real_item_not_a_null ()
        {
            var people = new List<Person> { new ("Ada") };
            var source = new BindingSource { DataSource = people };

            var added = source.AddNew ();

            Assert.NotNull (added);
            Assert.IsType<Person> (added);
            Assert.DoesNotContain (people, p => p is null);
            Assert.Same (added, source.Current);   // the new item becomes current
        }

        [Fact]
        public void AddingNew_can_supply_the_item ()
        {
            var source = new BindingSource { DataSource = new List<Person> { new ("Ada") } };
            var supplied = new Person ("Supplied");
            source.AddingNew += (_, e) => e.NewObject = supplied;

            Assert.Same (supplied, source.AddNew ());
        }

        [Fact]
        public void Find_locates_an_item_by_property_value ()
        {
            var source = new BindingSource {
                DataSource = new List<Person> { new ("Ada"), new ("Grace"), new ("Katherine") },
            };

            Assert.Equal (1, source.Find ("Name", "Grace"));
            Assert.Equal (-1, source.Find ("Name", "Nobody"));
        }

        [Fact]
        public void Sorting_is_applied_when_the_list_can_sort_and_reported_when_it_cannot ()
        {
            // A plain List<T> cannot sort itself, and BindingSource says so rather than reordering the
            // caller's collection behind its back.
            var plain = new BindingSource { DataSource = new List<Person> { new ("Grace"), new ("Ada") } };

            Assert.False (plain.SupportsSorting);
            plain.Sort = "Name ASC";
            Assert.Equal ("Grace", ((Person)plain[0]!).Name);   // untouched
            Assert.Equal ("Name ASC", plain.Sort);              // but remembered

            // A BindingList<T> with sorting support does apply it.
            var sortable = new SortablePeople { new ("Grace"), new ("Ada") };
            var source = new BindingSource { DataSource = sortable };

            Assert.True (source.SupportsSorting);

            source.Sort = "Name ASC";

            Assert.True (source.IsSorted);
            Assert.Equal ("Ada", ((Person)source[0]!).Name);
        }

        [Fact]
        public void EndEdit_and_CancelEdit_reach_an_editable_current_item ()
        {
            var item = new EditableItem ();
            var source = new BindingSource { DataSource = new List<EditableItem> { item } };

            source.EndEdit ();
            source.CancelEdit ();

            Assert.Equal (1, item.Ends);
            Assert.Equal (1, item.Cancels);
        }

        // A minimal IBindingList that really sorts, standing in for BindingList<T> with sorting enabled.
        private sealed class SortablePeople : List<Person>, IBindingList
        {
            public bool AllowEdit => true;
            public bool AllowNew => true;
            public bool AllowRemove => true;
            public bool IsSorted { get; private set; }
            public ListSortDirection SortDirection { get; private set; }
            public PropertyDescriptor? SortProperty { get; private set; }
            public bool SupportsChangeNotification => true;
            public bool SupportsSearching => true;
            public bool SupportsSorting => true;

            public event ListChangedEventHandler? ListChanged;

            public void AddIndex (PropertyDescriptor property) { }
            public object AddNew () { var p = new Person (); Add (p); return p; }

            public void ApplySort (PropertyDescriptor property, ListSortDirection direction)
            {
                Sort ((a, b) => string.Compare ((string?)property.GetValue (a), (string?)property.GetValue (b),
                    System.StringComparison.Ordinal) * (direction == ListSortDirection.Descending ? -1 : 1));

                SortProperty = property;
                SortDirection = direction;
                IsSorted = true;
                ListChanged?.Invoke (this, new ListChangedEventArgs (ListChangedType.Reset, -1));
            }

            public int Find (PropertyDescriptor property, object key)
                => FindIndex (p => Equals (property.GetValue (p), key));

            public void RemoveIndex (PropertyDescriptor property) { }
            public void RemoveSort () { IsSorted = false; SortProperty = null; }
        }

        private sealed class EditableItem : IEditableObject
        {
            internal int Ends;
            internal int Cancels;

            public void BeginEdit () { }
            public void EndEdit () => Ends++;
            public void CancelEdit () => Cancels++;
        }
    }
}
