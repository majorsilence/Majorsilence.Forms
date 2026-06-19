using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a collection of items for ListBox.
    /// </summary>
    public class ListBoxItemCollection : ObservableCollection<object>
    {
        private readonly ListBox owner;
        private int focused_index;
        private int hovered_index = -1;

        internal ListBoxItemCollection (ListBox owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds a collection of items to the collection.
        /// </summary>
        public void AddRange (params object[] items)
        {
            owner.SuspendLayout ();

            foreach (var item in items)
                Add (item);

            owner.ResumeLayout (true);
        }

        /// <summary>
        /// Adds an item with an optional checked state (WinForms compat for CheckedListBox.Items.Add).
        /// </summary>
        public int Add (object item, bool isChecked)
        {
            var wrapped = new CheckedListBoxItem (item, isChecked);
            Add (wrapped);
            return Count - 1;
        }

        /// <summary>
        /// Adds an item with a CheckState (WinForms compat for CheckedListBox.Items.Add).
        /// </summary>
        public int Add (object item, CheckState state)
        {
            var wrapped = new CheckedListBoxItem (item, state == CheckState.Checked || state == CheckState.Indeterminate);
            Add (wrapped);
            return Count - 1;
        }

        internal void AddSelectedIndex (int index, bool single)
        {
            if (single)
                SelectedIndexes.Clear ();

            focused_index = Math.Max (index, 0);

            if (index != -1)
                SelectedIndexes.Add (index);

            owner.Invalidate ();
        }

        internal int FocusedIndex {
            get => focused_index;
            set {
                if (focused_index != value) {
                    focused_index = value;
                    owner.Invalidate ();
                }
            }
        }

        internal (int start, int end) GetSingleContiguousSelection ()
        {
            if (SelectedIndexes.Count == 0)
                return (-1, -1);

            if (SelectedIndexes.Count == 1)
                return (SelectedIndex, SelectedIndex);

            var indexes = SelectedIndexes.OrderBy (p => p).ToList ();

            if (indexes.Last () - indexes.First () + 1 == indexes.Count)
                return (indexes.First (), indexes.Last ());

            return (-1, -1);
        }

        internal int HoveredIndex {
            get => hovered_index;
            set {
                if (hovered_index != value) {
                    hovered_index = value;
                    owner.Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnCollectionChanged (NotifyCollectionChangedEventArgs e)
        {
            // Keep the selected/focused indexes in sync as items are added, removed,
            // or the collection is reset. Stale indexes would otherwise point at the
            // wrong item (or out of range) after a mutation.
            switch (e.Action) {
                case NotifyCollectionChangedAction.Remove:
                    AdjustIndexesForRemove (e.OldStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Add:
                    AdjustIndexesForInsert (e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    SelectedIndexes.Clear ();
                    focused_index = 0;
                    break;
            }

            base.OnCollectionChanged (e);

            owner.Invalidate ();
        }

        private void AdjustIndexesForRemove (int removedIndex)
        {
            if (removedIndex < 0)
                return;

            // Drop the removed index and shift everything after it down by one.
            for (var i = SelectedIndexes.Count - 1; i >= 0; i--) {
                if (SelectedIndexes[i] == removedIndex)
                    SelectedIndexes.RemoveAt (i);
                else if (SelectedIndexes[i] > removedIndex)
                    SelectedIndexes[i]--;
            }

            if (focused_index > removedIndex || focused_index >= Count)
                focused_index = Math.Max (0, focused_index - 1);
        }

        private void AdjustIndexesForInsert (int insertedIndex)
        {
            if (insertedIndex < 0)
                return;

            // Shift any selected index at or after the insertion point up by one.
            for (var i = 0; i < SelectedIndexes.Count; i++) {
                if (SelectedIndexes[i] >= insertedIndex)
                    SelectedIndexes[i]++;
            }

            if (focused_index >= insertedIndex)
                focused_index++;
        }

        internal void RemoveSelectedIndex (int index)
        {
            focused_index = Math.Max (index, 0);

            SelectedIndexes.Remove (index);

            owner.Invalidate ();
        }

        internal int SelectedIndex {
            get => SelectedIndexes.Count > 0 ? SelectedIndexes[0] : -1;
            set {
                if (value < -1 || value >= Count)
                    throw new ArgumentOutOfRangeException (nameof (value), "Index out of range");

                AddSelectedIndex (value, true);
            }
        }

        internal List<int> SelectedIndexes { get; } = [];

        internal object? SelectedItem {
            get => SelectedIndexes.Count > 0 ? this[SelectedIndexes[0]] : null;
            set {
                if (value is null) {
                    SelectedIndex = -1;
                    return;
                }

                var index = IndexOf (value);

                if (index == -1)
                    throw new ArgumentException ("Item is not part of this list");

                SelectedIndex = index;
            }
        }

        internal IEnumerable<object> SelectedItems => SelectedIndexes.Select (i => this[i]);

        internal void ToggleSelectedIndex (int index)
        {
            if (SelectedIndexes.Contains (index))
                RemoveSelectedIndex (index);
            else
                AddSelectedIndex (index, false);
        }
    }
}
