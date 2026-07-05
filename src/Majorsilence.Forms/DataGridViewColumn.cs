using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a column in a DataGridView control.
    /// </summary>
    public class DataGridViewColumn
    {
        private string header_text = string.Empty;
        private int width = 100;
        private DataGridView? owner;
        private DataGridViewCellStyle default_cell_style = new DataGridViewCellStyle ();

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumn class.
        /// </summary>
        public DataGridViewColumn ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumn class with the specified header text.
        /// </summary>
        public DataGridViewColumn (string headerText)
        {
            header_text = headerText;
        }

        /// <summary>
        /// Gets or sets the name used to identify this column.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data source property name for this column.
        /// </summary>
        public string DataPropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data type of the values in this column's cells. WinForms compatibility —
        /// used to drive default formatting; null when unbound/unknown.
        /// </summary>
        public Type? ValueType { get; set; }

        /// <summary>
        /// Gets or sets whether this column is bound to a data source. WinForms compatibility stub.
        /// </summary>
        public bool IsDataBound { get; set; }

        /// <summary>
        /// Gets or sets whether cells in this column are read-only.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the tooltip text for this column.
        /// </summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default cell style for this column.
        /// </summary>
        public DataGridViewCellStyle DefaultCellStyle {
            get => default_cell_style;
            set => default_cell_style = value ?? new DataGridViewCellStyle ();
        }

        /// <summary>
        /// Gets or sets whether the column is resizable.
        /// </summary>
        public DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>
        /// Gets or sets the sort mode for this column.
        /// </summary>
        public DataGridViewColumnSortMode SortMode { get; set; } = DataGridViewColumnSortMode.Automatic;

        /// <summary>
        /// Gets the bounding rectangle of the column header.
        /// </summary>
        internal Rectangle HeaderBounds { get; set; }

        /// <summary>
        /// Gets the header cell for this column.
        /// </summary>
        public DataGridViewColumnHeaderCell HeaderCell { get; } = new DataGridViewColumnHeaderCell ();

        /// <summary>
        /// Gets or sets the header text for this column.
        /// </summary>
        public string HeaderText {
            get => header_text;
            set {
                if (header_text != value) {
                    header_text = value;
                    owner?.Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the index of this column in the DataGridView.
        /// </summary>
        public int Index => owner?.Columns.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets or sets the minimum width, in pixels, of the column.
        /// </summary>
        public int MinimumWidth { get; set; } = 30;

        /// <summary>
        /// Gets the DataGridView control that contains this column.
        /// </summary>
        public DataGridView? DataGridView => owner;

        /// <summary>
        /// Gets or sets a value indicating whether the column is sortable.
        /// </summary>
        public bool Sortable { get; set; } = true;

        /// <summary>
        /// Gets or sets the sort order for this column.
        /// </summary>
        public SortOrder SortOrder { get; set; } = SortOrder.None;

        /// <summary>
        /// Gets or sets an object that contains data to associate with the column.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets whether the column is visible.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets the auto-size mode. Stub in Majorsilence.Forms.</summary>
        public DataGridViewAutoSizeColumnMode AutoSizeMode { get; set; } = DataGridViewAutoSizeColumnMode.None;

        /// <summary>Gets or sets the relative fill weight for fill-mode auto-sizing. Stub.</summary>
        public float FillWeight { get; set; } = 100f;

        /// <summary>Gets or sets whether the column is frozen to the left (does not scroll horizontally).</summary>
        public bool Frozen { get; set; }

        /// <summary>
        /// Whether the column is pinned to the right edge (does not scroll horizontally). Telerik-only
        /// concept, set via <c>GridViewColumn.PinPosition = PinnedColumnPosition.Right</c>.
        /// </summary>
        internal bool PinnedRight { get; set; }

        /// <summary>Gets or sets the width of the column divider. Stub in Majorsilence.Forms.</summary>
        public int DividerWidth { get; set; }

        /// <summary>Gets or sets the template used to create new cells. Stub in Majorsilence.Forms.</summary>
        public virtual DataGridViewCell? CellTemplate { get; set; }

        /// <summary>Gets or sets the display order of the column. Stub in Majorsilence.Forms.</summary>
        public int DisplayIndex {
            get => Index;
            set { /* ordering not implemented */ }
        }

        /// <summary>Gets or sets the column cell content alignment.</summary>
        public ContentAlignment DefaultCellStyleAlignment { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>Gets or sets the alignment of the column header text.</summary>
        public ContentAlignment HeaderAlignment { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>
        /// When true, the renderer draws a check-box glyph instead of text for this column's cells.
        /// Default false; check-box column types (including the Telerik-compat GridViewCheckBoxColumn) override.
        /// </summary>
        protected internal virtual bool DisplaysAsCheckBox => false;

        /// <summary>
        /// Gets or sets the width, in pixels, of the column.
        /// </summary>
        public int Width {
            get => width;
            set {
                value = Math.Max (value, MinimumWidth);

                if (width != value) {
                    width = value;
                    owner?.OnColumnsChanged ();
                }
            }
        }

        /// <summary>
        /// Sets the owning DataGridView.
        /// </summary>
        internal void SetOwner (DataGridView? dataGridView) => owner = dataGridView;
    }

    /// <summary>
    /// Specifies the appearance of a control.
    /// </summary>
    public enum FlatStyle
    {
        /// <summary>Flat appearance.</summary>
        Flat,
        /// <summary>Popup appearance.</summary>
        Popup,
        /// <summary>Standard (3D) appearance.</summary>
        Standard,
        /// <summary>Uses the system default.</summary>
        System
    }

    /// <summary>
    /// Specifies the sort order for a column.
    /// </summary>
    public enum SortOrder
    {
        /// <summary>
        /// No sort order.
        /// </summary>
        None,
        /// <summary>
        /// Items are sorted in ascending order.
        /// </summary>
        Ascending,
        /// <summary>
        /// Items are sorted in descending order.
        /// </summary>
        Descending
    }
}
