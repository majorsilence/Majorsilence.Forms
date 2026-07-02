using Majorsilence.Forms;

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
            // not needed but here for compatibility
        }
    }
}
