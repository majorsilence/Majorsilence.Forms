using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a row in a DataGridView control.
    /// </summary>
    public partial class DataGridViewRow
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

        /// <summary>Gets whether this row is the new-row placeholder. Always false in Majorsilence.Forms.</summary>
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

        /// <summary>Gets or sets whether the row can be resized by the user. Stub in Majorsilence.Forms.</summary>
        public DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>Gets a value indicating whether this row is frozen (cannot scroll). Stub in Majorsilence.Forms.</summary>
        public bool Frozen { get; set; }

        /// <summary>Gets or sets the error text for this row. Stub in Majorsilence.Forms.</summary>
        public string ErrorText { get; set; } = string.Empty;

        /// <summary>
        /// Gets the style used for this row: the grid's <see cref="DataGridView.DefaultCellStyle"/>, then
        /// its <see cref="DataGridView.RowsDefaultCellStyle"/>, then (on odd rows) its
        /// <see cref="DataGridView.AlternatingRowsDefaultCellStyle"/>, then this row's
        /// <see cref="DefaultCellStyle"/> -- later layers win. Mirrors WinForms
        /// DataGridViewRow.InheritedStyle.
        /// </summary>
        public DataGridViewCellStyle InheritedStyle {
            get {
                var result = new DataGridViewCellStyle ();

                if (owner is not null) {
                    result.ApplyStyle (owner.DefaultCellStyle.ToDataGridViewCellStyle ());
                    result.ApplyStyle (owner.RowsDefaultCellStyle.ToDataGridViewCellStyle ());

                    if (Index % 2 == 1)
                        result.ApplyStyle (owner.AlternatingRowsDefaultCellStyle.ToDataGridViewCellStyle ());
                }

                result.ApplyStyle (DefaultCellStyle);
                return result;
            }
        }

        /// <summary>Gets the state of this row (WinForms DataGridViewRow.State / InheritedState).</summary>
        public DataGridViewElementStates State {
            get {
                var state = DataGridViewElementStates.None;

                if (Visible)
                    state |= DataGridViewElementStates.Visible;
                if (ReadOnly || (owner?.ReadOnly ?? false))
                    state |= DataGridViewElementStates.ReadOnly;
                if (Selected)
                    state |= DataGridViewElementStates.Selected;
                if (Frozen)
                    state |= DataGridViewElementStates.Frozen;
                if (!Bounds.IsEmpty)
                    state |= DataGridViewElementStates.Displayed;
                if (Resizable != DataGridViewTriState.False && (owner?.AllowUserToResizeRows ?? true))
                    state |= DataGridViewElementStates.Resizable;
                if (Resizable != DataGridViewTriState.NotSet)
                    state |= DataGridViewElementStates.ResizableSet;

                return state;
            }
        }

        /// <summary>Gets the state of this row, including state inherited from the grid. Mirrors WinForms.</summary>
        public DataGridViewElementStates InheritedState => State;

        /// <summary>
        /// Creates an exact copy of this row, including clones of its cells (WinForms
        /// DataGridViewRow.Clone). The clone is unowned -- add it to a grid's row collection to attach it.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072",
            Justification = "Row types are concrete public types with public parameterless constructors; cloning mirrors WinForms.")]
        public virtual object Clone ()
        {
            var clone = (DataGridViewRow)Activator.CreateInstance (GetType ())!;

            clone.height = height;
            clone.MinimumHeight = MinimumHeight;
            clone.Tag = Tag;
            clone.Visible = Visible;
            clone.Frozen = Frozen;
            clone.Selected = Selected;
            clone.ErrorText = ErrorText;
            clone.Resizable = Resizable;
            clone.DataBoundItem = DataBoundItem;
            clone.DefaultCellStyle = DefaultCellStyle.Clone ();
            clone.read_only = read_only;

            foreach (var cell in Cells)
                clone.Cells.Add ((DataGridViewCell)cell.Clone ());

            return clone;
        }

        /// <summary>
        /// Sets the owning DataGridView.
        /// </summary>
        internal void SetOwner (DataGridView? dataGridView) => owner = dataGridView;
    }
}
