using System.Collections.ObjectModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of DataGridViewRow objects in a DataGridView control.
    /// </summary>
    public partial class DataGridViewRowCollection : Collection<DataGridViewRow>
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
        public new int Add (DataGridViewRow dataGridViewRow)
        {
            // Routes through InsertItem, which assigns the owning DataGridView.
            base.Add (dataGridViewRow);
            return Count - 1;
        }

        // Upstream throws InvalidOperationException from every public Add/Insert while the grid is
        // bound: a row added by hand to a bound grid is a phantom that the next ListChanged deletes
        // (DGV-33). The grid's own binding goes through InsertBound/RemoveBound/MoveBound instead.
        private void ThrowIfBound ()
        {
            if (owner.DataSource is not null)
                throw new InvalidOperationException (
                    "Rows cannot be programmatically added to or removed from the DataGridView's rows collection when the control is data-bound. Change the data source instead.");
        }

        /// <summary>
        /// Adds a new row with the specified cell values.
        /// </summary>
        public DataGridViewRow Add (params string[] values)
        {
            ThrowIfBound ();
            var row = (DataGridViewRow)owner.RowTemplate.Clone ();
            row.Cells.Clear ();

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
            ThrowIfBound ();
            var row = (DataGridViewRow)owner.RowTemplate.Clone ();
            row.Cells.Clear ();

            foreach (var value in values)
                row.Cells.Add (value);

            base.Add (row);
            return row;
        }

        /// <summary>
        /// Adds the specified number of new empty rows.
        /// </summary>
        /// <returns>The index of the last row added -- so <c>Rows.Add ()</c> returns the new row's index.</returns>
        public int Add (int count)
        {
            // It returned Count, so Rows.Add () on an empty grid returned 1 and the canonical
            // `int i = grid.Rows.Add (); grid.Rows[i].Cells[0].Value = …` threw on every call (DGV-03).
            if (count < 1)
                throw new ArgumentOutOfRangeException (nameof (count), count, "At least one row must be added.");

            ThrowIfBound ();

            for (var i = 0; i < count; i++)
                base.Add (CreateEmptyRow ());

            return Count - 1;
        }

        // An empty row has one cell per column, as upstream's RowTemplate clone does. Without them the
        // finding's own idiom -- Rows[Rows.Add ()].Cells[0].Value = … -- still threw once the index
        // was right, on the Cells indexer instead (DGV-03).
        private DataGridViewRow CreateEmptyRow () => owner.CreateRowFromTemplate ();

        /// <summary>
        /// Returns the index of the specified row, or -1 if not found.
        /// </summary>
        public new int IndexOf (DataGridViewRow row) => Items.IndexOf (row);

        /// <summary>Inserts a new row at the specified index with the given cell values.</summary>
        public void Insert (int rowIndex, params object[] values)
        {
            var row = (DataGridViewRow)owner.RowTemplate.Clone ();
            row.Cells.Clear ();
            foreach (var value in values)
                row.Cells.Add (value);
            Insert (rowIndex, row);
        }

        /// <summary>Inserts the specified row at the specified index.</summary>
        public new void Insert (int rowIndex, DataGridViewRow dataGridViewRow)
        {
            ThrowIfBound ();

            // Through InsertItem, so RowsAdded fires: it went straight to Items.Insert and bypassed the
            // event (DGV-33).
            base.Insert (rowIndex, dataGridViewRow);
        }

        // The grid's own binding path. Not guarded by ThrowIfBound -- these ARE the bound changes.
        internal void InsertBound (int rowIndex, DataGridViewRow row) => base.Insert (rowIndex, row);

        internal void RemoveBound (int rowIndex) => base.RemoveAt (rowIndex);

        internal void MoveBound (int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
                return;

            // Move the object, not its contents: the app's DataGridViewRow reference, Tag, Height and
            // per-row style travel with it. Done silently -- a move is neither an add nor a remove.
            var row = Items[fromIndex];
            Items.RemoveAt (fromIndex);
            Items.Insert (toIndex, row);
            owner.OnRowsChanged ();
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var row in this)
                row.SetOwner (null);

            int clearedCount = Count;
            base.ClearItems ();
            owner.OnRowsChanged ();

            if (clearedCount > 0)
                owner.RaiseRowsRemoved (0, clearedCount);
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, DataGridViewRow item)
        {
            item.SetOwner (owner);
            base.InsertItem (index, item);
            owner.OnRowsChanged ();
            owner.RaiseRowsAdded (index, 1);
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            this[index].SetOwner (null);
            base.RemoveItem (index);
            owner.OnRowsChanged ();
            owner.RaiseRowsRemoved (index, 1);
        }

        /// <summary>
        /// Replaces all rows with the specified list, raising a single change notification.
        /// </summary>
        internal void ReplaceAll (List<DataGridViewRow> rows)
        {
            // Clear without per-item notifications
            var removed = Count;

            foreach (var row in this)
                row.SetOwner (null);

            Items.Clear ();

            foreach (var row in rows) {
                row.SetOwner (owner);
                Items.Add (row);
            }

            owner.OnRowsChanged ();

            // One RowsRemoved and one RowsAdded for the batch, so the two balance. A bind used to raise
            // RowsRemoved (0, N) from a Clear before it and nothing from here, so RowsAdded handlers --
            // the row-colouring idiom -- never ran for bound rows and counters went negative (DGV-33).
            if (removed > 0)
                owner.RaiseRowsRemoved (0, removed);

            if (rows.Count > 0)
                owner.RaiseRowsAdded (0, rows.Count);
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
