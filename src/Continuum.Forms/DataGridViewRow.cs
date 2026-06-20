using System.Drawing;

namespace Continuum.Forms
{
    /// <summary>
    /// Represents a row in a DataGridView control.
    /// </summary>
    public class DataGridViewRow
    {
        private int height = 25;
        private DataGridView? owner;

        /// <summary>
        /// Initializes a new instance of the DataGridViewRow class.
        /// </summary>
        public DataGridViewRow ()
        {
            Cells = new DataGridViewCellCollection (this);
        }

        /// <summary>
        /// Gets the bounding rectangle of the row.
        /// </summary>
        internal Rectangle Bounds { get; set; }

        /// <summary>
        /// Gets the collection of cells in this row.
        /// </summary>
        public DataGridViewCellCollection Cells { get; }

        /// <summary>
        /// Gets the header cell for this row.
        /// </summary>
        public DataGridViewRowHeaderCell HeaderCell { get; } = new DataGridViewRowHeaderCell ();

        /// <summary>
        /// Gets the DataGridView that contains this row.
        /// </summary>
        public DataGridView? DataGridView => owner;

        /// <summary>
        /// Gets or sets the height, in pixels, of the row.
        /// </summary>
        public int Height {
            get => height;
            set {
                if (height != value) {
                    height = Math.Max (value, 10);
                    owner?.OnRowsChanged ();
                }
            }
        }

        /// <summary>
        /// Gets the index of this row in the DataGridView.
        /// </summary>
        public int Index => owner?.Rows.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets or sets whether this row is selected.
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>
        /// Gets or sets an object that contains data to associate with the row.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the data-source object bound to this row.
        /// Set automatically when AutoGenerateColumns=false and DataSource is assigned.
        /// </summary>
        public object? DataBoundItem { get; set; }

        /// <summary>Gets whether this row is the new-row placeholder. Always false in Continuum.Forms.</summary>
        public bool IsNewRow => false;

        /// <summary>Gets or sets whether all cells in this row are read-only.</summary>
        public bool ReadOnly {
            get => read_only;
            set {
                read_only = value;

                // WinForms semantics: setting the row's ReadOnly cascades to every cell. Clearing it
                // also clears the cell-level flags.
                foreach (var cell in Cells)
                    cell.ReadOnly = value;
            }
        }

        private bool read_only;

        /// <summary>Gets or sets the minimum height for this row.</summary>
        public int MinimumHeight { get; set; } = 10;

        /// <summary>Gets or sets the default cell style applied to cells in this row.</summary>
        public DataGridViewCellStyle DefaultCellStyle { get; set; } = new DataGridViewCellStyle ();

        /// <summary>Gets or sets whether this row is visible.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets whether the row can be resized by the user. Stub in Continuum.Forms.</summary>
        public DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>Gets a value indicating whether this row is frozen (cannot scroll). Stub in Continuum.Forms.</summary>
        public bool Frozen { get; set; }

        /// <summary>Gets or sets the error text for this row. Stub in Continuum.Forms.</summary>
        public string ErrorText { get; set; } = string.Empty;

        /// <summary>
        /// Sets the owning DataGridView.
        /// </summary>
        internal void SetOwner (DataGridView? dataGridView) => owner = dataGridView;
    }
}
