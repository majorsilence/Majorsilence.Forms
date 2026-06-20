using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Continuum.Forms
{
    /// <summary>
    /// WinForms compatibility: represents a ListBox in which a check box is displayed to the left of each item.
    /// In Continuum.Forms, this is implemented as a ListBox with checkbox rendering (visual only stub).
    /// </summary>
    public class CheckedListBox : ListBox
    {
        private CheckedObjectCollection? _checkedItems;

        /// <summary>Gets the items collection with checked-item Add overloads.</summary>
        public new CheckedObjectCollection Items => _checkedItems ??= new CheckedObjectCollection (base.Items);

        // Returns the CheckedListBoxItem wrapper stored in the underlying collection for the
        // given index, creating one in place if a raw value was added directly. All check-state
        // operations go through the wrappers, not the unwrapped public Items indexer.
        private CheckedListBoxItem GetWrapper (int index)
        {
            var raw = base.Items[index];

            if (raw is CheckedListBoxItem cli)
                return cli;

            // Promote a raw value to a wrapped item so we have somewhere to store check state.
            var wrapped = new CheckedListBoxItem (raw);
            base.Items[index] = wrapped;
            return wrapped;
        }

        /// <summary>Gets the collection of checked items.</summary>
        public IEnumerable<object> CheckedItems => base.Items.Where (i => i is CheckedListBoxItem cli && cli.Checked).Select (i => ((CheckedListBoxItem)i).Value ?? i);

        /// <summary>Gets or sets whether the check box is toggled when the item is selected.</summary>
        public bool CheckOnClick { get; set; }

        /// <summary>Gets or sets whether the check boxes are three-dimensional. Stub in Continuum.Forms.</summary>
        public bool ThreeDCheckBoxes { get; set; }

        /// <summary>Returns whether the item at the specified index is checked.</summary>
        public bool GetItemChecked (int index) => GetItemCheckState (index) != CheckState.Unchecked;

        /// <summary>Sets the checked state of the item at the specified index.</summary>
        public void SetItemChecked (int index, bool value) => SetItemCheckState (index, value ? CheckState.Checked : CheckState.Unchecked);

        /// <summary>Gets the check state of the item at the specified index.</summary>
        public CheckState GetItemCheckState (int index)
        {
            if (index < 0 || index >= base.Items.Count)
                throw new ArgumentOutOfRangeException (nameof (index));

            return GetWrapper (index).CheckState;
        }

        /// <summary>Sets the check state of the item at the specified index.</summary>
        public void SetItemCheckState (int index, CheckState value)
        {
            if (index < 0 || index >= base.Items.Count)
                throw new ArgumentOutOfRangeException (nameof (index));

            if (!Enum.IsDefined (value))
                throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (CheckState));

            var wrapper = GetWrapper (index);
            var current = wrapper.CheckState;

            if (current == value)
                return;

            var e = new ItemCheckEventArgs (index, value, current);
            OnItemCheck (e);

            wrapper.CheckState = e.NewValue;

            Invalidate ();
        }

        /// <summary>Gets the collection of indices of checked items.</summary>
        public IEnumerable<int> CheckedIndices {
            get {
                for (int i = 0; i < base.Items.Count; i++)
                    if (GetItemChecked (i)) yield return i;
            }
        }

        /// <summary>Raises the <see cref="ItemCheck"/> event.</summary>
        protected virtual void OnItemCheck (ItemCheckEventArgs e) => ItemCheck?.Invoke (this, e);

        /// <summary>Raised when the check state of an item changes.</summary>
        public event EventHandler<ItemCheckEventArgs>? ItemCheck;
    }

    /// <summary>Wraps a CheckedListBox item with a check state.</summary>
    public class CheckedListBoxItem
    {
        /// <summary>Initializes a new instance.</summary>
        public CheckedListBoxItem (object? value, bool isChecked = false)
        {
            Value = value;
            CheckState = isChecked ? CheckState.Checked : CheckState.Unchecked;
        }

        /// <summary>Gets or sets the underlying value.</summary>
        public object? Value { get; set; }

        /// <summary>Gets or sets the check state of this item.</summary>
        public CheckState CheckState { get; set; }

        /// <summary>Gets or sets whether this item is checked. Matches WinForms: true when Checked or Indeterminate.</summary>
        public bool Checked {
            get => CheckState != CheckState.Unchecked;
            set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
        }

        /// <inheritdoc/>
        public override string ToString () => Value?.ToString () ?? string.Empty;
    }

    /// <summary>Wraps a ListBoxItemCollection to add checked-state overloads for CheckedListBox.</summary>
    public class CheckedObjectCollection : System.Collections.IList
    {
        private readonly ListBoxItemCollection _inner;

        /// <summary>Initializes a new instance wrapping the specified collection.</summary>
        public CheckedObjectCollection (ListBoxItemCollection inner) { _inner = inner; }

        /// <summary>Adds an item with the specified initial check state.</summary>
        public int Add (object item, bool isChecked)
        {
            _inner.Add (new CheckedListBoxItem (item, isChecked));
            return _inner.Count - 1;
        }

        /// <summary>Adds an item with the specified initial check state.</summary>
        public int Add (object item, CheckState checkState)
        {
            _inner.Add (new CheckedListBoxItem (item) { CheckState = checkState });
            return _inner.Count - 1;
        }

        /// <summary>Adds an item (unchecked by default).</summary>
        public int Add (object item)
        {
            _inner.Add (new CheckedListBoxItem (item));
            return _inner.Count - 1;
        }

        /// <summary>Gets the number of items in the collection.</summary>
        public int Count => _inner.Count;

        /// <summary>Gets or sets the item at the specified index.</summary>
        public object? this[int index] {
            get => _inner[index] is CheckedListBoxItem cli ? cli.Value : _inner[index];
            set { }
        }

        /// <summary>Removes all items.</summary>
        public void Clear () => _inner.Clear ();

        /// <summary>Returns whether the collection contains the specified item.</summary>
        public bool Contains (object? item) => _inner.Any (i => i is CheckedListBoxItem cli ? Equals (cli.Value, item) : Equals (i, item));

        /// <summary>Returns the index of the specified item.</summary>
        public int IndexOf (object? item)
        {
            for (int i = 0; i < _inner.Count; i++) {
                var val = _inner[i] is CheckedListBoxItem cli ? cli.Value : _inner[i];
                if (Equals (val, item)) return i;
            }
            return -1;
        }

        /// <summary>Removes the item at the specified index.</summary>
        public void RemoveAt (int index) => _inner.RemoveAt (index);

        /// <summary>Removes the specified item.</summary>
        public void Remove (object? item)
        {
            int idx = IndexOf (item);
            if (idx >= 0) RemoveAt (idx);
        }

        // IList explicit implementation
        void System.Collections.IList.Insert (int index, object? value) { }
        bool System.Collections.IList.IsFixedSize => false;
        bool System.Collections.IList.IsReadOnly => false;
        bool System.Collections.ICollection.IsSynchronized => false;
        object System.Collections.ICollection.SyncRoot => this;
        void System.Collections.ICollection.CopyTo (System.Array array, int index) { }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => _inner.GetEnumerator ();
        int System.Collections.IList.Add (object? value) => value is null ? -1 : Add (value);
        bool System.Collections.IList.Contains (object? value) => Contains (value);
        int System.Collections.IList.IndexOf (object? value) => value is null ? -1 : IndexOf (value);
        void System.Collections.IList.Remove (object? value) { if (value is not null) Remove (value); }
    }

    /// <summary>Provides data for the ItemCheck event.</summary>
    public class ItemCheckEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ItemCheckEventArgs (int index, CheckState newValue, CheckState currentValue)
        {
            Index = index;
            NewValue = newValue;
            CurrentValue = currentValue;
        }

        /// <summary>Gets the index of the item whose check state is changing.</summary>
        public int Index { get; }

        /// <summary>Gets or sets the new check state.</summary>
        public CheckState NewValue { get; set; }

        /// <summary>Gets the current check state.</summary>
        public CheckState CurrentValue { get; }
    }
}
