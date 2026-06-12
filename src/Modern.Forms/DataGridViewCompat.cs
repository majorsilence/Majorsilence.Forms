using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// Provides data for the DataGridView.CellClick event.
    /// </summary>
    public class DataGridViewCellEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellEventArgs (int columnIndex, int rowIndex)
        {
            ColumnIndex = columnIndex;
            RowIndex = rowIndex;
        }

        /// <summary>Gets the column index of the cell.</summary>
        public int ColumnIndex { get; }

        /// <summary>Gets the row index of the cell.</summary>
        public int RowIndex { get; }
    }

    /// <summary>
    /// Provides data for the DataGridView.CellToolTipTextNeeded event.
    /// </summary>
    public class DataGridViewCellToolTipTextNeededEventArgs : DataGridViewCellEventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public DataGridViewCellToolTipTextNeededEventArgs (int columnIndex, int rowIndex) : base (columnIndex, rowIndex) { }

        /// <summary>Gets or sets the tooltip text.</summary>
        public string ToolTipText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Stores style information for a DataGridView cell.
    /// </summary>
    public class DataGridViewCellStyle
    {
        /// <summary>Gets or sets the background color.</summary>
        public Color BackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the foreground color.</summary>
        public Color ForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the format string applied to cell content.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>Gets or sets the selection background color.</summary>
        public Color SelectionBackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the selection foreground color.</summary>
        public Color SelectionForeColor { get; set; } = Color.Empty;
    }

    /// <summary>
    /// Represents a text box column in a DataGridView.
    /// </summary>
    public class DataGridViewTextBoxColumn : DataGridViewColumn { }

    /// <summary>
    /// Represents a check box column in a DataGridView.
    /// </summary>
    public class DataGridViewCheckBoxColumn : DataGridViewColumn { }

    /// <summary>
    /// Represents a combo box column in a DataGridView.
    /// </summary>
    public class DataGridViewComboBoxColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets the data source for the combo box items.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the property used for display text.</summary>
        public string DisplayMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the property used as the underlying value.</summary>
        public string ValueMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the flat style (stub).</summary>
        public FlatStyle FlatStyle { get; set; }
    }

    /// <summary>
    /// Specifies how the widths of columns are adjusted when the DataGridView is resized.
    /// </summary>
    public enum DataGridViewAutoSizeColumnsMode
    {
        /// <summary>Column widths are not automatically adjusted.</summary>
        None,
        /// <summary>Column widths adjust to fit the content of all cells, including headers.</summary>
        AllCells,
        /// <summary>Column widths adjust to fit the content of all cells, excluding headers.</summary>
        AllCellsExceptHeader,
        /// <summary>Column widths adjust to fit the header content.</summary>
        ColumnHeader,
        /// <summary>Column widths adjust to fit the content of displayed cells, including headers.</summary>
        DisplayedCells,
        /// <summary>Column widths adjust to fit the content of displayed cells, excluding headers.</summary>
        DisplayedCellsExceptHeader,
        /// <summary>Columns share the available width equally.</summary>
        Fill
    }

    /// <summary>
    /// Specifies how the height of the column header row is adjusted.
    /// </summary>
    public enum DataGridViewColumnHeadersHeightSizeMode
    {
        /// <summary>Users can resize the column header row.</summary>
        EnableResizing,
        /// <summary>Users cannot resize the column header row.</summary>
        DisableResizing,
        /// <summary>The column header row height adjusts automatically to fit its contents.</summary>
        AutoSize
    }

    /// <summary>
    /// Specifies how a DataGridView column sorts data.
    /// </summary>
    public enum DataGridViewColumnSortMode
    {
        /// <summary>The column can be sorted automatically.</summary>
        Automatic,
        /// <summary>The column cannot be sorted.</summary>
        NotSortable,
        /// <summary>The column is sorted programmatically.</summary>
        Programmatic
    }

    /// <summary>
    /// Specifies a true, false, or indeterminate value for DataGridView properties.
    /// </summary>
    public enum DataGridViewTriState
    {
        /// <summary>Property is not set.</summary>
        NotSet,
        /// <summary>Property is true.</summary>
        True,
        /// <summary>Property is false.</summary>
        False
    }
}
