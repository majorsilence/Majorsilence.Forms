using System.Collections;
using System.ComponentModel;

namespace Modern.Forms
{
    /// <summary>
    /// WinForms compatibility: wraps a data list for binding to DataGridView and other controls.
    /// Implements IList so it can be assigned directly to DataGridView.DataSource.
    /// </summary>
    public class BindingSource : Component, IList
    {
        private IList _list = Array.Empty<object> ();
        private object? _dataSource;

        /// <summary>Gets or sets the data member path (no-op stub).</summary>
        public string DataMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the underlying data source.</summary>
        public object? DataSource {
            get => _dataSource;
            set {
                _dataSource = value;
                _list = value as IList ?? Array.Empty<object> ();
            }
        }

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
    }
}
