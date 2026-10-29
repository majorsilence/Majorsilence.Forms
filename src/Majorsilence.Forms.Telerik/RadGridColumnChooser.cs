using System.Drawing;

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
            foreach (var column in Columns (grid)) {
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

        // The columns the chooser lists: those not opted out, in the grid's order or by caption, per ColumnChooserSortOrder.
        internal static List<DataGridViewColumn> Columns (RadGridView grid)
        {
            var columns = ((DataGridView) grid).Columns.Cast<DataGridViewColumn> ()
                .Where (c => c is not GridViewColumn gvc || gvc.VisibleInColumnChooser)
                .ToList ();

            string Caption (DataGridViewColumn c) => string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText;

            return grid.ColumnChooserSortOrder switch {
                RadSortOrder.Ascending => columns.OrderBy (Caption, StringComparer.CurrentCultureIgnoreCase).ToList (),
                RadSortOrder.Descending => columns.OrderByDescending (Caption, StringComparer.CurrentCultureIgnoreCase).ToList (),
                _ => columns,
            };
        }
    }
}
