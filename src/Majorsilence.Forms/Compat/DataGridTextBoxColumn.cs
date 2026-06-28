using System.ComponentModel;
using Majorsilence.Forms;

namespace Majorsilence.Forms
{
    public class DataGridTextBoxColumn : DataGridViewTextBoxColumn, IDataGridColumnStyle
    {
        public DataGridTextBoxColumn()
        {
            this.TextBox = new DataGridTextBox();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGridTextBox TextBox { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string NullText { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public HorizontalAlignment Alignment { get; set; }
    }
}
