using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: wraps a data list for binding to DataGridView and other controls.
    /// Implements IList so it can be assigned directly to DataGridView.DataSource, and ITypedList so
    /// bound controls can read the schema (column set) of the resolved list -- including the case where
    /// the source is a DataSet and DataMember names one of its tables.
    /// </summary>
    public partial class BindingSource : Component, IList, ITypedList, IBindingList, ISupportInitialize, ISupportInitializeNotification
    {
        private IList _list = new List<object?> ();
        private object? _dataSource;
        private string _dataMember = string.Empty;

        /// <summary>Initializes a new instance of BindingSource.</summary>
        public BindingSource () { }

        /// <summary>Initializes a new instance of BindingSource and adds it to the specified container.</summary>
        public BindingSource (IContainer container) { container.Add (this); }

        /// <summary>Initializes a new instance of BindingSource with a data source and data member.</summary>
        public BindingSource (object dataSource, string dataMember) { DataSource = dataSource; DataMember = dataMember; }

        /// <summary>Gets or sets the data member (e.g. a table name within a DataSet source).</summary>
        public string DataMember {
            get => _dataMember;
            set {
                if (string.Equals (_dataMember, value ?? string.Empty, StringComparison.Ordinal))
                    return;

                _dataMember = value ?? string.Empty;
                ResolveList ();
                OnDataMemberChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the underlying data source.</summary>
        public object? DataSource {
            get => _dataSource;
            set {
                if (ReferenceEquals (_dataSource, value))
                    return;

                _dataSource = value;
                ResolveList ();
                OnDataSourceChanged (EventArgs.Empty);
            }
        }

        private bool initializing;

        /// <summary>Gets whether initialization has completed.</summary>
        public bool IsInitialized { get; private set; } = true;

        /// <summary>Raised when <see cref="EndInit"/> completes.</summary>
        public event EventHandler? Initialized;

        /// <summary>
        /// Suspends list resolution until <see cref="EndInit"/>.
        /// </summary>
        /// <remarks>
        /// Designer code sets DataSource and DataMember as separate statements, and each setter
        /// resolves. Between the two the pair is inconsistent -- a DataSet assigned before its member
        /// name resolves to an empty list, which resets Position to -1 and makes bound controls read an
        /// empty schema. Deferring to EndInit means the pair is only ever resolved once, complete.
        /// </remarks>
        public void BeginInit ()
        {
            initializing = true;
            IsInitialized = false;
        }

        /// <summary>Resumes list resolution and resolves once against the fully-assigned state.</summary>
        public void EndInit ()
        {
            if (!initializing)
                return;

            initializing = false;
            ResolveList ();
            IsInitialized = true;
            Initialized?.Invoke (this, EventArgs.Empty);
        }

        // Resolves DataSource (+ DataMember) to the concrete IList that backs this BindingSource:
        // a DataSet resolves through DataMember to that table's DefaultView; a DataTable to its
        // DefaultView; an IListSource via GetList (); an IList is used directly.
        private void ResolveList ()
        {
            if (initializing)
                return;

            object? src = _dataSource;

            if (src is System.Data.DataSet ds)
                src = !string.IsNullOrEmpty (_dataMember) && ds.Tables.Contains (_dataMember)
                    ? ds.Tables[_dataMember]!.DefaultView
                    : null;
            else if (src is System.Data.DataTable table)
                src = table.DefaultView;

            _list = src switch {
                IList list => list,
                System.ComponentModel.IListSource listSource => listSource.GetList (),
                // Any other enumerable is materialised, as WinForms does. Dictionaries, HashSets and LINQ
                // results are all IEnumerable but not IList, and binding a combo to one is ordinary code
                // -- returning an empty list for them made the control render nothing and report
                // Items.Count == 0, which callers then compute indices from.
                string => new List<object?> (),
                System.Collections.IEnumerable enumerable => Materialize (enumerable),
                _ => new List<object?> ()
            };

            Position = _list.Count > 0 ? 0 : -1;

            AttachToList ();
            OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
        }

        // A snapshot, not a live view: an arbitrary IEnumerable has no change notification to forward, so
        // re-reading it later could not be kept in step anyway. Callers who need updates re-assign
        // DataSource or call ResetBindings, both of which re-run this.
        private static List<object?> Materialize (System.Collections.IEnumerable source)
        {
            var list = new List<object?> ();

            foreach (var item in source)
                list.Add (item);

            return list;
        }

        // ITypedList — expose the schema of the resolved list so bound controls can build columns even
        // when the list has no rows. Delegates to the list's own ITypedList (DataView) when available,
        // otherwise reflects the element type's properties.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Data binding requires runtime reflection over user-provided types.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "Data binding requires runtime reflection over user-provided types.")]
        PropertyDescriptorCollection ITypedList.GetItemProperties (PropertyDescriptor[]? listAccessors)
        {
            if (_list is ITypedList typed)
                return typed.GetItemProperties (listAccessors);

            var elementType = ListElementType ();
            return elementType is null
                ? new PropertyDescriptorCollection (System.Array.Empty<PropertyDescriptor> ())
                : TypeDescriptor.GetProperties (elementType);
        }

        string ITypedList.GetListName (PropertyDescriptor[]? listAccessors) => _dataMember;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection over user-provided types.")]
        private System.Type? ListElementType ()
        {
            foreach (var iface in _list.GetType ().GetInterfaces ()) {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition () != typeof (IList<>))
                    continue;

                var argument = iface.GetGenericArguments ()[0];

                // A declared element type of object tells us nothing -- and it is exactly what a
                // materialized IEnumerable produces. Prefer what is actually in the list, so binding to a
                // dictionary still exposes Key and Value for DisplayMember/ValueMember to name.
                if (argument != typeof (object))
                    return argument;

                break;
            }

            return _list.Count > 0 ? _list[0]?.GetType () : null;
        }

        /// <summary>Gets or sets the zero-based index of the current item.</summary>
        public int Position {
            get => position;
            set {
                // Stored as given, not clamped: Current already reports null for an index outside the
                // list, and callers rely on being able to park the position out of range.
                if (position == value)
                    return;

                position = value;
                OnCurrentChanged (EventArgs.Empty);
            }
        }

        private int position = -1;

        /// <summary>Gets the current item at the current position.</summary>
        public object? Current => (Position >= 0 && Position < _list.Count) ? _list[Position] : null;

        /// <summary>Gets or sets a filter expression (no-op stub — filtered data requires DataView).</summary>
        public string? Filter { get; set; }

        /// <summary>Gets or sets the sort expression (no-op stub).</summary>
        public string? Sort { get; set; }

        /// <summary>Raised when the current item changes.</summary>
        public event EventHandler? CurrentChanged;

        /// <summary>Raised when the list, or an item in it, changes.</summary>
        /// <remarks>
        /// This is what makes a bound grid track its data. Controls bind to a BindingSource long before
        /// it has anything in it -- designer code assigns the grid's DataSource in InitializeComponent,
        /// and the form fills the DataSet afterwards -- so without this the grid renders the empty list
        /// it saw at bind time and never updates. Raised both when this BindingSource re-resolves (a new
        /// DataSource or DataMember) and when the resolved list reports its own changes.
        /// </remarks>
        public event System.ComponentModel.ListChangedEventHandler? ListChanged;

        /// <summary>Gets or sets whether <see cref="ListChanged"/> is raised.</summary>
        /// <remarks>
        /// Set false around a bulk load so bound controls rebuild once at the end rather than per row,
        /// then set it back to true -- which itself raises a reset, as WinForms does, since the
        /// suppressed changes still have to reach the bindings somehow.
        /// </remarks>
        public bool RaiseListChangedEvents {
            get => raise_list_changed_events;
            set {
                if (raise_list_changed_events == value)
                    return;

                raise_list_changed_events = value;

                if (value)
                    OnListChanged (new ListChangedEventArgs (ListChangedType.Reset, -1));
            }
        }

        private bool raise_list_changed_events = true;

        /// <summary>Raised when the <see cref="DataSource"/> property changes.</summary>
        public event EventHandler? DataSourceChanged;

        /// <summary>Raised when the <see cref="DataMember"/> property changes.</summary>
        public event EventHandler? DataMemberChanged;

        /// <summary>Raises <see cref="ListChanged"/>, unless suppressed or binding is suspended.</summary>
        protected virtual void OnListChanged (ListChangedEventArgs e)
        {
            if (raise_list_changed_events && !binding_suspended)
                ListChanged?.Invoke (this, e);
        }

        /// <summary>Raises <see cref="CurrentChanged"/>.</summary>
        protected virtual void OnCurrentChanged (EventArgs e) => CurrentChanged?.Invoke (this, e);

        /// <summary>Raises <see cref="DataSourceChanged"/>.</summary>
        protected virtual void OnDataSourceChanged (EventArgs e) => DataSourceChanged?.Invoke (this, e);

        /// <summary>Raises <see cref="DataMemberChanged"/>.</summary>
        protected virtual void OnDataMemberChanged (EventArgs e) => DataMemberChanged?.Invoke (this, e);

        // The list we have a ListChanged subscription on, kept so it can be detached on re-resolve.
        // Without this every re-resolve leaves the old list holding a reference to this BindingSource --
        // a DataTable outlives the grids bound to it, so the leak is real, not theoretical.
        private IBindingList? subscribed_list;

        private void AttachToList ()
        {
            if (ReferenceEquals (subscribed_list, _list))
                return;

            if (subscribed_list is not null)
                subscribed_list.ListChanged -= OnUnderlyingListChanged;

            subscribed_list = _list as IBindingList;

            if (subscribed_list is not null)
                subscribed_list.ListChanged += OnUnderlyingListChanged;
        }

        // A DataView raises this when rows are added to its DataTable. Forwarding it is what lets a grid
        // bound before the table was populated catch up.
        private void OnUnderlyingListChanged (object? sender, ListChangedEventArgs e) => OnListChanged (e);

        /// <summary>Moves to the next item.</summary>
        public void MoveNext () { if (Position < _list.Count - 1) Position++; }

        /// <summary>Moves to the previous item.</summary>
        public void MovePrevious () { if (Position > 0) Position--; }

        /// <summary>Moves to the first item.</summary>
        public void MoveFirst () { if (_list.Count > 0) Position = 0; }

        /// <summary>Moves to the last item.</summary>
        public void MoveLast () { if (_list.Count > 0) Position = _list.Count - 1; }

        /// <summary>Tells every bound control to re-read the list.</summary>
        /// <param name="metaDataChanged">
        /// True when the *schema* changed, not just the rows, so bound controls rebuild their columns
        /// as well as their contents.
        /// </param>
        public void ResetBindings (bool metaDataChanged)
            => OnListChanged (new ListChangedEventArgs (
                metaDataChanged ? ListChangedType.PropertyDescriptorChanged : ListChangedType.Reset, -1));

        /// <summary>Tells bound controls to re-read the current item.</summary>
        public void ResetCurrentItem ()
            => OnListChanged (new ListChangedEventArgs (ListChangedType.ItemChanged, Position));

        // IList — delegate to the underlying list ──────────────────────────────

        /// <inheritdoc/>
        public object? this[int index] {
            get => _list[index];
            set => _list[index] = value;
        }

        /// <inheritdoc/>
        public int Count => _list.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => _list.IsReadOnly;

        /// <inheritdoc/>
        public bool IsFixedSize => _list.IsFixedSize;

        /// <inheritdoc/>
        public bool IsSynchronized => _list.IsSynchronized;

        /// <inheritdoc/>
        public object SyncRoot => _list.SyncRoot;

        /// <inheritdoc/>
        public int Add (object? value) => _list.Add (value);

        /// <inheritdoc/>
        public void Clear () => _list.Clear ();

        /// <inheritdoc/>
        public bool Contains (object? value) => _list.Contains (value);

        /// <inheritdoc/>
        public int IndexOf (object? value) => _list.IndexOf (value);

        /// <inheritdoc/>
        public void Insert (int index, object? value) => _list.Insert (index, value);

        /// <inheritdoc/>
        public void Remove (object? value) => _list.Remove (value);

        /// <inheritdoc/>
        public void RemoveAt (int index) => _list.RemoveAt (index);

        /// <inheritdoc/>
        public void CopyTo (Array array, int index) => _list.CopyTo (array, index);

        /// <inheritdoc/>
        public IEnumerator GetEnumerator () => _list.GetEnumerator ();

        /// <summary>Adds a new item to the underlying list. Stub in Majorsilence.Forms.</summary>
#pragma warning disable CA1711
        public object? AddNew () { _list.Add (null); return null; }
#pragma warning restore CA1711

        /// <summary>Removes the current item from the list. Stub in Majorsilence.Forms.</summary>
        public void RemoveCurrent () { if (Position >= 0 && Position < _list.Count) _list.RemoveAt (Position); }

        /// <summary>Commits the pending edit. Stub in Majorsilence.Forms.</summary>
        public void EndEdit () { }

        /// <summary>Cancels the pending edit. Stub in Majorsilence.Forms.</summary>
        public void CancelEdit () { }

        /// <summary>Tells bound controls to re-read the item at the given index.</summary>
        public void ResetItem (int itemIndex)
            => OnListChanged (new ListChangedEventArgs (ListChangedType.ItemChanged, itemIndex));

        /// <summary>Returns the index of the item with the given property value. Stub in Majorsilence.Forms.</summary>
        public int Find (string propertyName, object key) => -1;

        /// <summary>Finds the index of the item whose described property equals the given key.</summary>
        public int Find (PropertyDescriptor property, object key)
        {
            ArgumentNullException.ThrowIfNull (property);
            return Find (property.Name, key);
        }

        /// <summary>Returns whether the list allows new items. Stub in Majorsilence.Forms.</summary>
        public bool AllowNew { get; set; } = true;

        /// <summary>Returns whether the list allows edits. Stub in Majorsilence.Forms.</summary>
        public bool AllowEdit { get; set; } = true;

        /// <summary>Returns whether the list allows items to be removed. Stub in Majorsilence.Forms.</summary>
        public bool AllowRemove { get; set; } = true;
    }
}
