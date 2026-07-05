using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a cell in a DataGridView control.
    /// </summary>
    public class DataGridViewCell
    {
        /// <summary>The editing-control type for this cell. Mirrors WinForms DataGridViewCell.EditType.</summary>
        public virtual Type? EditType => null;

        /// <summary>The value type of the cell. Mirrors WinForms DataGridViewCell.ValueType.</summary>
        public virtual Type? ValueType => null;

        /// <summary>The default value for a new row's cell. Mirrors WinForms.</summary>
        public virtual object? DefaultNewRowValue => null;

        /// <summary>Initializes the hosted editing control. Mirrors WinForms.</summary>
        public virtual void InitializeEditingControl (int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
        }

        private object? value;
        private DataGridViewRow? owner;

        // Default style used as the base parent for all cell Style instances.
        internal static readonly ControlStyle DefaultCellStyleInternal = new ControlStyle (null,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.ForegroundColor = Theme.ForegroundColor;
            });

        /// <summary>
        /// Initializes a new instance of the DataGridViewCell class.
        /// </summary>
        public DataGridViewCell ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataGridViewCell class with the specified value.
        /// </summary>
        public DataGridViewCell (object? value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the bounding rectangle of the cell.
        /// </summary>
        internal Rectangle Bounds { get; set; }

        /// <summary>
        /// Gets the column index of this cell.
        /// </summary>
        public int ColumnIndex => owner?.Cells.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets the DataGridView that contains this cell.
        /// </summary>
        public DataGridView? DataGridView => owner?.DataGridView;

        /// <summary>
        /// Gets the row that contains this cell.
        /// </summary>
        public DataGridViewRow? OwningRow => owner;

        /// <summary>
        /// Gets the row index of this cell.
        /// </summary>
        public int RowIndex => owner?.Index ?? -1;

        /// <summary>
        /// Gets or sets whether this cell is selected.
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>
        /// Gets or sets the style for this cell.
        /// </summary>
        public ControlStyle Style { get; set; } = new ControlStyle (DefaultCellStyleInternal);

        /// <summary>
        /// Gets or sets an object that contains data to associate with the cell.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the value of this cell.
        /// </summary>
        public object? Value {
            get => value;
            set {
                if (!Equals (this.value, value)) {
                    this.value = value;
                    owner?.DataGridView?.Invalidate ();
                }
            }
        }

        /// <summary>Gets the formatted (display) value of this cell.</summary>
        public object? FormattedValue => FormattedTextOverride ?? value?.ToString ();

        /// <summary>
        /// An optional display-text override set by a formatting pass (e.g. RadGridView's CellFormatting
        /// or a column FormatString). When set, the renderer draws this instead of the raw value. Reset
        /// each paint by the formatting hook so it never goes stale.
        /// </summary>
        internal string? FormattedTextOverride { get; set; }

        /// <summary>Gets or sets whether this cell is read-only.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Gets or sets the tooltip text for this cell.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the error message text for this cell. Stub in Majorsilence.Forms.</summary>
        public string ErrorText { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this cell is visible. Stub in Majorsilence.Forms.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets the column that contains this cell.</summary>
        public DataGridViewColumn? OwningColumn {
            get {
                var colIndex = ColumnIndex;
                var dgv = DataGridView;
                if (dgv is null || colIndex < 0 || colIndex >= dgv.Columns.Count) return null;
                return dgv.Columns[colIndex];
            }
        }

        /// <summary>
        /// Sets the owning row.
        /// </summary>
        internal void SetOwner (DataGridViewRow? row) => owner = row;
    }
}
