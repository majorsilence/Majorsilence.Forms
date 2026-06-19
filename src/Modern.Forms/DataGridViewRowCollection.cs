using System.Collections.ObjectModel;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a collection of DataGridViewRow objects in a DataGridView control.
    /// </summary>
    public class DataGridViewRowCollection : Collection<DataGridViewRow>
    {
        private readonly DataGridView owner;

        /// <summary>
        /// Initializes a new instance of the DataGridViewRowCollection class.
        /// </summary>
        internal DataGridViewRowCollection (DataGridView owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds the specified existing row to the collection and returns its index.
        /// </summary>
        public int Add (DataGridViewRow dataGridViewRow)
        {
            // Routes through InsertItem, which assigns the owning DataGridView.
            base.Add (dataGridViewRow);
            return Count - 1;
        }

        /// <summary>
        /// Adds a new row with the specified cell values.
        /// </summary>
        public DataGridViewRow Add (params string[] values)
        {
            var row = new DataGridViewRow ();

            foreach (var value in values)
                row.Cells.Add (value);

            base.Add (row);
            return row;
        }

        /// <summary>
        /// Adds a new row with the specified object cell values.
        /// </summary>
        public DataGridViewRow Add (params object[] values)
        {
            var row = new DataGridViewRow ();

            foreach (var value in values)
                row.Cells.Add (value);

            base.Add (row);
            return row;
        }

        /// <summary>
        /// Adds the specified number of new empty rows.
        /// </summary>
        public int Add (int count)
        {
            for (var i = 0; i < count; i++) {
                var row = new DataGridViewRow ();
                base.Add (row);
            }
            return Count;
        }

        /// <summary>
        /// Returns the index of the specified row, or -1 if not found.
        /// </summary>
        public new int IndexOf (DataGridViewRow row) => Items.IndexOf (row);

        /// <summary>Inserts a new row at the specified index with the given cell values.</summary>
        public void Insert (int rowIndex, params object[] values)
        {
            var row = new DataGridViewRow ();
            foreach (var value in values)
                row.Cells.Add (value);
            Insert (rowIndex, row);
        }

        /// <summary>Inserts the specified row at the specified index.</summary>
        public new void Insert (int rowIndex, DataGridViewRow dataGridViewRow)
        {
            dataGridViewRow.SetOwner (owner);
            Items.Insert (rowIndex, dataGridViewRow);
            owner.OnRowsChanged ();
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var row in this)
                row.SetOwner (null);

            base.ClearItems ();
            owner.OnRowsChanged ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, DataGridViewRow item)
        {
            item.SetOwner (owner);
            base.InsertItem (index, item);
            owner.OnRowsChanged ();
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            this[index].SetOwner (null);
            base.RemoveItem (index);
            owner.OnRowsChanged ();
        }

        /// <summary>
        /// Replaces all rows with the specified list, raising a single change notification.
        /// </summary>
        internal void ReplaceAll (List<DataGridViewRow> rows)
        {
            // Clear without per-item notifications
            foreach (var row in this)
                row.SetOwner (null);

            Items.Clear ();

            foreach (var row in rows) {
                row.SetOwner (owner);
                Items.Add (row);
            }

            owner.OnRowsChanged ();
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, DataGridViewRow item)
        {
            this[index].SetOwner (null);
            item.SetOwner (owner);
            base.SetItem (index, item);
            owner.OnRowsChanged ();
        }
    }
}
