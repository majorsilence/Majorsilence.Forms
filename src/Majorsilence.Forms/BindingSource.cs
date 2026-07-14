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
    public class BindingSource : Component, IList, ITypedList
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
            set { _dataMember = value ?? string.Empty; ResolveList (); }
        }

        /// <summary>Gets or sets the underlying data source.</summary>
        public object? DataSource {
            get => _dataSource;
            set { _dataSource = value; ResolveList (); }
        }

        // Resolves DataSource (+ DataMember) to the concrete IList that backs this BindingSource:
        // a DataSet resolves through DataMember to that table's DefaultView; a DataTable to its
        // DefaultView; an IListSource via GetList (); an IList is used directly.
        private void ResolveList ()
        {
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
                _ => new List<object?> ()
            };

            Position = _list.Count > 0 ? 0 : -1;
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
            foreach (var iface in _list.GetType ().GetInterfaces ())
                if (iface.IsGenericType && iface.GetGenericTypeDefinition () == typeof (IList<>))
                    return iface.GetGenericArguments ()[0];

            return _list.Count > 0 ? _list[0]?.GetType () : null;
        }

        /// <summary>Gets or sets the zero-based index of the current item.</summary>
        public int Position { get; set; } = -1;

        /// <summary>Gets the current item at the current position.</summary>
        public object? Current => (Position >= 0 && Position < _list.Count) ? _list[Position] : null;

        /// <summary>Gets or sets a filter expression (no-op stub — filtered data requires DataView).</summary>
        public string? Filter { get; set; }

        /// <summary>Gets or sets the sort expression (no-op stub).</summary>
        public string? Sort { get; set; }

        /// <summary>Raised when the current item changes.</summary>
        public event EventHandler? CurrentChanged { add { } remove { } }

        /// <summary>Raised when the list changes.</summary>
        public event System.ComponentModel.ListChangedEventHandler? ListChanged { add { } remove { } }

        /// <summary>Gets or sets whether ListChanged events are raised during bulk operations. Stub in Majorsilence.Forms.</summary>
        public bool RaiseListChangedEvents { get; set; } = true;

        /// <summary>Raised when the DataSource property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DataSourceChanged { add { } remove { } }

        /// <summary>Raised when the DataMember property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DataMemberChanged { add { } remove { } }

        /// <summary>Moves to the next item.</summary>
        public void MoveNext () { if (Position < _list.Count - 1) Position++; }

        /// <summary>Moves to the previous item.</summary>
        public void MovePrevious () { if (Position > 0) Position--; }

        /// <summary>Moves to the first item.</summary>
        public void MoveFirst () { if (_list.Count > 0) Position = 0; }

        /// <summary>Moves to the last item.</summary>
        public void MoveLast () { if (_list.Count > 0) Position = _list.Count - 1; }

        /// <summary>Resets all bindings (no-op stub).</summary>
        public void ResetBindings (bool metaDataChanged) { }

        /// <summary>Resets the current item binding (no-op stub).</summary>
        public void ResetCurrentItem () { }

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

        /// <summary>Raises the ListChanged event. Stub in Majorsilence.Forms.</summary>
        public void ResetItem (int itemIndex) { }

        /// <summary>Returns the index of the item with the given property value. Stub in Majorsilence.Forms.</summary>
        public int Find (string propertyName, object key) => -1;

        /// <summary>Returns whether the list allows new items. Stub in Majorsilence.Forms.</summary>
        public bool AllowNew { get; set; } = true;

        /// <summary>Returns whether the list allows edits. Stub in Majorsilence.Forms.</summary>
        public bool AllowEdit { get; set; } = true;

        /// <summary>Returns whether the list allows items to be removed. Stub in Majorsilence.Forms.</summary>
        public bool AllowRemove { get; set; } = true;
    }
}
