namespace Majorsilence.Forms
{
    /// <summary>
    ///  Represents a cell within a <see cref="DataGrid"/>, identified by row and column indices.
    /// </summary>
    public class DataGridCell : DataGridViewCell
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="DataGridCell"/> class.
        /// </summary>
        public DataGridCell()
        {
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="DataGridCell"/> class with the specified row and column indices.
        /// </summary>
        public DataGridCell(int row, int column)
        {
            // These used to be discarded, which made the two-argument constructor indistinguishable
            // from the empty one -- so DataGrid.GetCellBounds (cell) had nothing to read.
            RowNumber = row;
            ColumnNumber = column;
        }

        /// <summary>Gets or sets the row this cell identifies.</summary>
        public int RowNumber { get; set; }

        /// <summary>Gets or sets the column this cell identifies.</summary>
        public int ColumnNumber { get; set; }
    }
}
