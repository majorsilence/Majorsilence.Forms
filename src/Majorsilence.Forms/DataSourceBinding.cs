using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Shared list-binding helpers for the data-bound controls (<see cref="ListBox"/>,
    /// <see cref="ComboBox"/>).
    ///
    /// Two things they all need, and which a plain <c>is IList</c> plus
    /// <c>GetType().GetProperty(member)</c> gets wrong:
    ///
    /// 1. The common WinForms data source is a <c>DataTable</c>, which is NOT an
    ///    <see cref="IList"/> -- it is an <see cref="IListSource"/> whose <c>GetList()</c> returns the
    ///    default view. Treating a non-IList source as "no source" left the item collection empty, so
    ///    a form that bound a table and then selected a row threw
    ///    ArgumentOutOfRangeException from the SelectedIndex setter.
    /// 2. Items from such a view are <c>DataRowView</c>s, whose columns are exposed as
    ///    <see cref="PropertyDescriptor"/>s rather than CLR properties, so reflection over
    ///    <c>GetProperty</c> never finds DisplayMember/ValueMember. WinForms reads these through
    ///    <see cref="TypeDescriptor"/>, which handles both shapes.
    /// </summary>
    internal static class DataSourceBinding
    {
        /// <summary>
        /// The bindable list behind a data source: the source itself when it is already a list, or the
        /// list an <see cref="IListSource"/> (DataTable, DataView, DataSet) produces. Null when the
        /// source cannot be enumerated as a list.
        /// </summary>
        internal static IList? AsList (object? dataSource) => dataSource switch {
            IList list => list,
            IListSource source => source.GetList (),
            _ => null,
        };

        /// <summary>
        /// Tracks a list data source for a list control: re-reads it whenever the source's contents
        /// change, and keeps the control's selection and the source's current-item position in step.
        /// </summary>
        /// <remarks>
        /// A list control used to read its data source ONCE, when the property was assigned. That is the
        /// wrong moment for the way designer code is written: <c>InitializeComponent</c> assigns
        /// <c>DataSource</c> and the form fills the data afterwards, so the control kept the empty list it
        /// saw at bind time and never showed a row. <see cref="BindingSource.ListChanged"/> exists to say
        /// when to look again, and nothing was listening.
        /// </remarks>
        internal sealed class ListSourceTracker : IDisposable
        {
            private readonly Action reload;
            private readonly Action<int> selectPosition;
            private readonly Func<int> currentSelection;

            private IBindingList? watched;
            private CurrencyManager? manager;
            private bool syncing;

            internal ListSourceTracker (Action reload, Action<int> selectPosition, Func<int> currentSelection)
            {
                this.reload = reload;
                this.selectPosition = selectPosition;
                this.currentSelection = currentSelection;
            }

            /// <summary>Points the tracker at a new data source, detaching from the previous one.</summary>
            internal void Attach (object? dataSource)
            {
                Detach ();

                if (AsList (dataSource) is IBindingList list) {
                    list.ListChanged += OnListChanged;
                    watched = list;
                }

                // A source that owns a currency manager (a BindingSource) shares its current item with
                // every control bound to it, which is what makes master/detail and a bound navigator move
                // a list control's selection.
                if (dataSource is ICurrencyManagerProvider provider
                    && provider.GetRelatedCurrencyManager (null) is { } owned) {
                    owned.PositionChanged += OnPositionChanged;
                    manager = owned;
                }
            }

            /// <summary>Reports a selection change made in the control, moving the source's position.</summary>
            internal void OnSelectionChanged (int index)
            {
                if (manager is null || syncing || index < 0 || index == manager.Position)
                    return;

                Guard (() => manager.Position = index);
            }

            private void OnListChanged (object? sender, ListChangedEventArgs e) => Guard (reload);

            private void OnPositionChanged (object? sender, EventArgs e)
            {
                if (manager is not null && manager.Position != currentSelection ())
                    Guard (() => selectPosition (manager.Position));
            }

            // The control and the source drive each other, so whichever side moves first holds the guard.
            private void Guard (Action action)
            {
                if (syncing)
                    return;

                syncing = true;

                try {
                    action ();
                } finally {
                    syncing = false;
                }
            }

            private void Detach ()
            {
                if (watched is not null)
                    watched.ListChanged -= OnListChanged;

                if (manager is not null)
                    manager.PositionChanged -= OnPositionChanged;

                watched = null;
                manager = null;
            }

            public void Dispose () => Detach ();
        }

        /// <summary>
        /// The value of <paramref name="member"/> on <paramref name="item"/>, or null when the item is
        /// null or exposes no such member. Property descriptors are consulted first so custom type
        /// descriptors (notably <c>DataRowView</c>'s columns) resolve before CLR reflection.
        /// </summary>
        [UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "DataSource item types require runtime reflection — same as WinForms.")]
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "TypeDescriptor.GetProperties is how WinForms itself resolves DisplayMember/ValueMember; a data source's item type is only known at runtime. A trimmed-away descriptor degrades to the CLR property lookup below, and then to the item's own ToString.")]
        internal static object? MemberValue (object? item, string? member)
        {
            if (item is null || string.IsNullOrEmpty (member))
                return null;

            var descriptor = TypeDescriptor.GetProperties (item)[member];

            if (descriptor is not null)
                return descriptor.GetValue (item);

            return item.GetType ().GetProperty (member)?.GetValue (item);
        }

        /// <summary>
        /// The text a bound control shows for <paramref name="item"/>: the display member's value when
        /// one is set and resolvable, else the item's own string form.
        /// </summary>
        internal static string DisplayText (object? item, string? displayMember)
        {
            if (!string.IsNullOrEmpty (displayMember)) {
                var value = MemberValue (item, displayMember);

                if (value is not null)
                    return value.ToString () ?? string.Empty;
            }

            return item?.ToString () ?? string.Empty;
        }
    }
}
