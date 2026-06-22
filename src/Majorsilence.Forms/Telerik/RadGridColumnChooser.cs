using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// The column-chooser popup (shown via the header context menu when
    /// <see cref="RadGridView.AllowColumnChooser"/> is set). Lists a checkbox per column whose
    /// <see cref="GridViewColumn.VisibleInColumnChooser"/> is true; toggling a box shows/hides the column.
    /// </summary>
    internal static class RadGridColumnChooser
    {
        public static void Show (RadGridView grid, Point screenLocation)
        {
            if (grid.FindWindow () is not WindowBase window)
                return;

            const int width = 220;
            const int margin = 8;
            const int rowH = 22;

            var popup = new PopupWindow (window);
            var y = margin;

            popup.Controls.Add (new Label {
                Text = "Columns", Left = margin, Top = y, Width = width - margin * 2, Height = 18
            });
            y += 22;

            var any = false;
            foreach (DataGridViewColumn column in grid.Columns) {
                // Honor a column opting out of the chooser.
                if (column is GridViewColumn gvc && !gvc.VisibleInColumnChooser)
                    continue;

                any = true;
                var captured = column;
                var box = new CheckBox {
                    Text = string.IsNullOrEmpty (column.HeaderText) ? column.Name : column.HeaderText,
                    Left = margin, Top = y, Width = width - margin * 2, Height = rowH,
                    Checked = column.Visible
                };
                box.CheckedChanged += (_, _) => {
                    captured.Visible = box.Checked;
                    grid.Invalidate ();
                };
                popup.Controls.Add (box);
                y += rowH;
            }

            if (!any) {
                popup.Controls.Add (new Label {
                    Text = "No columns available.", Left = margin, Top = y, Width = width - margin * 2, Height = 18
                });
                y += 20;
            }

            y += 4;
            var closeButton = new Button { Text = "Close", Left = margin, Top = y, Width = width - margin * 2, Height = 26 };
            closeButton.Click += (_, _) => popup.Hide ();
            popup.Controls.Add (closeButton);
            y += 26 + margin;

            popup.Size = new Size (width, y);
            popup.Show (screenLocation);
        }
    }
}
