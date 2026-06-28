using System;
using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    public class DataGridTableStyle : Component, IDisposable
    {
        public DataGridTableStyle()
        {
            GridColumnStyles = new GridColumnStylesCollection();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ReadOnly { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color AlternatingBackColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BackColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public DataGrid DataGrid { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color ForeColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color GridLineColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HeaderBackColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color HeaderForeColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color LinkColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SelectionBackColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color SelectionForeColor { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string MappingName { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AllowSorting { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GridColumnStylesCollection GridColumnStyles { get; set; }

        public void Dispose()
        {
            DataGrid?.Dispose();
        }
    }
}
