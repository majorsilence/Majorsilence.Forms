using System.Collections.ObjectModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of DataGridViewColumn objects in a DataGridView control.
    /// </summary>
    public class DataGridViewColumnCollection : Collection<DataGridViewColumn>
    {
        /// <summary>Moves a column to a new display position. Mirrors Telerik's Columns.Move.</summary>
        public void Move (int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= Count || toIndex < 0 || toIndex >= Count)
                return;
            var item = this[fromIndex];
            RemoveAt (fromIndex);
            Insert (toIndex, item);
        }

        private readonly DataGridView owner;

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumnCollection class.
        /// </summary>
        internal DataGridViewColumnCollection (DataGridView owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Returns the column with the specified name (header text or Name property), or null.
        /// </summary>
        public DataGridViewColumn? this[string name] {
            get {
                foreach (var col in this)
                    if (string.Equals (col.Name, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals (col.HeaderText, name, StringComparison.OrdinalIgnoreCase))
                        return col;
                return null;
            }
        }

        /// <summary>
        /// Returns true if a column with the given name exists.
        /// </summary>
        public bool Contains (string name) => this[name] is not null;

        /// <summary>
        /// Adds a column with the specified header text to the collection.
        /// </summary>
        public DataGridViewColumn Add (string headerText)
        {
            var column = new DataGridViewColumn (headerText);
            Add (column);
            return column;
        }

        /// <summary>
        /// Adds a column with the specified internal name and header text (WinForms-compatible overload).
        /// </summary>
        public DataGridViewColumn Add (string name, string headerText)
        {
            var column = new DataGridViewColumn (headerText) { Name = name };
            Add (column);
            return column;
        }

        /// <summary>
        /// Adds a column with the specified header text and width to the collection.
        /// </summary>
        public DataGridViewColumn Add (string headerText, int width)
        {
            var column = new DataGridViewColumn (headerText) { Width = width };
            Add (column);
            return column;
        }

        /// <summary>
        /// Adds a range of columns to the collection.
        /// </summary>
        public void AddRange (params DataGridViewColumn[] columns)
        {
            ArgumentNullException.ThrowIfNull (columns);

            foreach (var column in columns)
                Add (column);
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var column in this)
                column.SetOwner (null);

            base.ClearItems ();
            owner.OnColumnsChanged ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, DataGridViewColumn item)
        {
            item.SetOwner (owner);
            base.InsertItem (index, item);
            owner.OnColumnsChanged ();
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            this[index].SetOwner (null);
            base.RemoveItem (index);
            owner.OnColumnsChanged ();
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, DataGridViewColumn item)
        {
            this[index].SetOwner (null);
            item.SetOwner (owner);
            base.SetItem (index, item);
            owner.OnColumnsChanged ();
        }
    }
}
