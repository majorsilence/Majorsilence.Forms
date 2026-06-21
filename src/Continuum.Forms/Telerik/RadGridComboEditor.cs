using System;
using System.Collections.Generic;
using System.Drawing;
using Continuum.Forms;

namespace Continuum.Forms.Telerik
{
    /// <summary>
    /// The in-place drop-down editor shown when a <see cref="GridViewComboBoxColumn"/> cell is opened
    /// for editing. Lists the column's display values; picking one calls back with the underlying value.
    /// </summary>
    internal static class RadGridComboEditor
    {
        public static void Show (RadGridView grid, List<(object? Value, string Display)> items,
            string? currentDisplay, Point screenLocation, int cellWidth, Action<object?> onPick)
        {
            if (grid.FindWindow () is not WindowBase window || items.Count == 0)
                return;

            var width = Math.Max (120, cellWidth);
            var height = Math.Min (220, 6 + items.Count * 22);

            var popup = new PopupWindow (window);
            var list = new ListBox { Left = 0, Top = 0, Width = width, Height = height };

            foreach (var item in items)
                list.Items.Add (item.Display, false);

            // Preselect the current value's display, if present.
            if (!string.IsNullOrEmpty (currentDisplay)) {
                for (var i = 0; i < items.Count; i++) {
                    if (string.Equals (items[i].Display, currentDisplay, StringComparison.CurrentCultureIgnoreCase)) {
                        list.SelectedIndex = i;
                        break;
                    }
                }
            }

            list.SelectedIndexChanged += (_, _) => {
                var idx = list.SelectedIndex;
                if (idx >= 0 && idx < items.Count) {
                    onPick (items[idx].Value);
                    popup.Hide ();
                }
            };

            popup.Controls.Add (list);
            popup.Size = new Size (width, height);
            popup.Show (screenLocation);
        }
    }
}
