using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Specifies the appearance and behavior of a <see cref="DataGrid"/> table, including its columns and colors.
    /// </summary>
    public class DataGridTableStyle : Component, IDisposable
    {
        /// <summary>
        ///  Initializes a new instance of the <see cref="DataGridTableStyle"/> class.
        /// </summary>
        public DataGridTableStyle()
        {
            GridColumnStyles = new GridColumnStylesCollection();
        }

        /// <summary>
        ///  Gets or sets a value indicating whether the grid is read-only.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ReadOnly { get; set; }

        /// <summary>
        ///  Gets or sets the background color of odd-numbered rows.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color AlternatingBackColor { get; set; }

        /// <summary>
        ///  Gets or sets the background color of the grid.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BackColor { get; set; }

        /// <summary>
        ///  Gets or sets the <see cref="DataGrid"/> that this table style is associated with.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGrid? DataGrid { get; set; }

        /// <summary>
        ///  Gets or sets the foreground color of the grid.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color ForeColor { get; set; }

        /// <summary>
        ///  Gets or sets the color of the grid lines.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color GridLineColor { get; set; }

        /// <summary>
        ///  Gets or sets the background color of the column headers.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HeaderBackColor { get; set; }

        /// <summary>
        ///  Gets or sets the foreground color of the column headers.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HeaderForeColor { get; set; }

        /// <summary>
        ///  Gets or sets the color used to render hyperlinks in the grid.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color LinkColor { get; set; }

        /// <summary>
        ///  Gets or sets the background color of selected cells.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SelectionBackColor { get; set; }

        /// <summary>
        ///  Gets or sets the foreground color of selected cells.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SelectionForeColor { get; set; }

        /// <summary>
        ///  Gets or sets the name of the data member mapped to this table style.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? MappingName { get; set; }

        /// <summary>
        ///  Gets or sets a value indicating whether the grid columns can be sorted by clicking their headers.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AllowSorting { get; set; }

        /// <summary>
        ///  Gets or sets the collection of column styles used by this table style.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GridColumnStylesCollection GridColumnStyles { get; set; }

        /// <summary>
        ///  Releases the resources used by this <see cref="DataGridTableStyle"/>, including its associated <see cref="DataGrid"/>.
        /// </summary>
        public new void Dispose()
        {
            DataGrid?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
