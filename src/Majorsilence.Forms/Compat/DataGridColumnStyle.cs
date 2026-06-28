using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class DataGridColumnStyle : DataGridViewColumn, IDataGridColumnStyle
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }
    }
}
