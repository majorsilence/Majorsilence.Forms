using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  A column style that displays and edits boolean values using a check box in a <see cref="DataGrid"/>.
    /// </summary>
    public class  DataGridBoolColumn : DataGridViewCheckBoxColumn, IDataGridColumnStyle
    {
        /// <summary>
        ///  Gets or sets the name of the data member mapped to this column.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }

        /// <summary>
        ///  Gets or sets a value indicating whether the column allows a null (indeterminate) value.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AllowNull { get; set; }
    }
}
