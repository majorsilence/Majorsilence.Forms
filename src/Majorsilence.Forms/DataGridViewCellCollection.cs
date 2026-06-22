using System.Collections.ObjectModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of DataGridViewCell objects in a DataGridViewRow.
    /// </summary>
    public class DataGridViewCellCollection : Collection<DataGridViewCell>
    {
        private readonly DataGridViewRow owner;

        /// <summary>
        /// Initializes a new instance of the DataGridViewCellCollection class.
        /// </summary>
        internal DataGridViewCellCollection (DataGridViewRow owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Gets the cell corresponding to the column with the specified name.
        /// </summary>
        public DataGridViewCell? this[string columnName]
        {
            get {
                var dgv = owner.DataGridView;

                if (dgv == null)
                    return null;

                for (int i = 0; i < dgv.Columns.Count; i++) {
                    var col = dgv.Columns[i];

                    if (string.Equals (col.Name, columnName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals (col.DataPropertyName, columnName, StringComparison.OrdinalIgnoreCase))
                        return i < Count ? this[i] : null;
                }

                return null;
            }
        }

        /// <summary>
        /// Adds the specified existing cell to the collection and returns it.
        /// </summary>
        public DataGridViewCell Add (DataGridViewCell cell)
        {
            // Routes through InsertItem, which assigns the owning row. Without this overload an
            // existing cell would bind to Add(object?) and be wrapped as a new cell's value.
            base.Add (cell);
            return cell;
        }

        /// <summary>
        /// Adds a cell with the specified value to the collection.
        /// </summary>
        public DataGridViewCell Add (object? value)
        {
            var cell = new DataGridViewCell (value);
            // Call the base Collection<T>.Add explicitly: because this class declares its own
            // Add(object?), C# overload resolution would otherwise pick it again and recurse forever.
            base.Add (cell);
            return cell;
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var cell in this)
                cell.SetOwner (null);

            base.ClearItems ();
            owner.DataGridView?.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, DataGridViewCell item)
        {
            item.SetOwner (owner);
            base.InsertItem (index, item);
            owner.DataGridView?.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            this[index].SetOwner (null);
            base.RemoveItem (index);
            owner.DataGridView?.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, DataGridViewCell item)
        {
            this[index].SetOwner (null);
            item.SetOwner (owner);
            base.SetItem (index, item);
            owner.DataGridView?.Invalidate ();
        }
    }
}
