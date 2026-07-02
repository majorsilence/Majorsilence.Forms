using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Base class for a column style used to render and edit data in a <see cref="DataGrid"/>.
    /// </summary>
    public class DataGridColumnStyle : DataGridViewColumn, IDataGridColumnStyle
    {
        /// <summary>
        ///  Gets or sets the name of the data member mapped to this column.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }
    }
}
