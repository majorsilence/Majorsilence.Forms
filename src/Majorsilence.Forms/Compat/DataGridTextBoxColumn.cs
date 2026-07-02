using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  A column style that displays and edits text values in a <see cref="DataGrid"/>.
    /// </summary>
    public class DataGridTextBoxColumn : DataGridViewTextBoxColumn, IDataGridColumnStyle
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="DataGridTextBoxColumn"/> class.
        /// </summary>
        public DataGridTextBoxColumn()
        {
            this.TextBox = new DataGridTextBox();
        }

        /// <summary>
        ///  Gets or sets the text box cell used to render and edit values in this column.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGridTextBox TextBox { get; set; }

        /// <summary>
        ///  Gets or sets the name of the data member mapped to this column.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get => base.DataPropertyName; set => base.DataPropertyName = value; }

        /// <summary>
        ///  Gets or sets the text displayed in a cell when its value is <see cref="DBNull"/>.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? NullText { get; set; }

        /// <summary>
        ///  Gets or sets the horizontal alignment of text in the column's cells.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public HorizontalAlignment Alignment { get; set; }
    }
}
