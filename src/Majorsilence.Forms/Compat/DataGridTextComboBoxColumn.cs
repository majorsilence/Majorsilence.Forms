using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  A column style that displays and edits values from a bound list using a combo box in a <see cref="DataGrid"/>.
    /// </summary>
    public class DataGridTextComboBoxColumn : DataGridViewComboBoxColumn, IDataGridColumnStyle
    {
        /// <summary>
        ///  Gets or sets the name of the data member mapped to this column.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }
    }
}
