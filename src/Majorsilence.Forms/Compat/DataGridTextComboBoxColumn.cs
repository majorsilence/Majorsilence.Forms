using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class DataGridTextComboBoxColumn : DataGridViewComboBoxColumn, IDataGridColumnStyle
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }
    }
}
