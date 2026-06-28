using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class  DataGridBoolColumn : DataGridViewCheckBoxColumn, IDataGridColumnStyle
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool AllowNull { get; set; }
    }
}
