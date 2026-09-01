using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a ListView.
    /// </summary>
    /// <remarks>
    /// View-aware as of W5.6 (finding LST-01, P0). This used to paint every item as a 70px large-icon
    /// tile with its text centred underneath, whatever <see cref="ListView.View"/> said -- so
    /// <c>View.Details</c>, the overwhelmingly common shape in a LOB app, rendered as a grid of tiles
    /// showing column 0 only: no header, and every subitem invisible. Nothing in here read
    /// <c>Columns</c>, <c>GridLines</c>, <c>FullRowSelect</c> or <c>CheckBoxes</c> at all.
    /// </remarks>
    public class ListViewRenderer : Renderer<ListView>
    {
        /// <inheritdoc/>
        protected override void Render (ListView control, PaintEventArgs e)
        {
            var area = control.ItemArea;

            // Rows are clipped to the item area so a partially-scrolled row is cut off at the edge
            // rather than painted over the header or outside the control.
            e.Canvas.Save ();
            e.Canvas.Clip (area);

            foreach (var item in control.Items) {
                // Items scrolled out of sight are laid out but not drawn.
                if (item.Bounds.Bottom < area.Top || item.Bounds.Top > area.Bottom)
                    continue;

                RenderItem (control, item, e);
            }

            e.Canvas.Restore ();

            // After the rows, so a row scrolled up under the header cannot overdraw it.
            if (control.ScaledHeaderHeight > 0)
                RenderHeader (control, e);
        }

        /// <summary>Renders the Details header band from <see cref="ListView.Columns"/>.</summary>
        protected virtual void RenderHeader (ListView control, PaintEventArgs e)
        {
            var height = control.ScaledHeaderHeight;
            var area = control.ItemArea;
            var band = new Rectangle (area.Left, area.Top - height, area.Width, height);
            var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

            e.Canvas.FillRectangle (band, Theme.ControlMidColor);
            e.Canvas.DrawLine (band.Left, band.Bottom - 1, band.Right, band.Bottom - 1, Theme.BorderMidColor);

            e.Canvas.Save ();
            e.Canvas.Clip (band);

            var x = band.Left + control.ScaledCheckWidth;

            foreach (var column in control.Columns) {
                var width = control.ScaledColumnWidth (column);
                var cell = new Rectangle (x, band.Top, width, height);

                e.Canvas.DrawText (column.Text ?? string.Empty, Theme.UIFont, font_size,
                    Padded (cell, e), Theme.ForegroundColor, Align (column.TextAlign), maxLines: 1);

                e.Canvas.DrawLine (cell.Right - 1, cell.Top + e.LogicalToDeviceUnits (2),
                    cell.Right - 1, cell.Bottom - e.LogicalToDeviceUnits (2), Theme.BorderLowColor);

                x += width;
            }

            e.Canvas.Restore ();
        }

        /// <summary>
        /// Renders a ListViewItem in whichever shape the current view calls for.
        /// </summary>
        protected virtual void RenderItem (ListView control, ListViewItem item, PaintEventArgs e)
        {
            if (control.View == View.Details)
                RenderDetailsRow (control, item, e);
            else if (control.IsRowView)
                RenderTextRow (control, item, e);
            else
                RenderTile (control, item, e);
        }

        /// <summary>Renders one Details row: the selection band, the check box, then a cell per column.</summary>
        protected virtual void RenderDetailsRow (ListView control, ListViewItem item, PaintEventArgs e)
        {
            var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

            // FullRowSelect highlights the whole row; without it, only the first column, as upstream.
            if (item.Selected) {
                var highlight = control.FullRowSelect || control.Columns.Count == 0
                    ? item.Bounds
                    : new Rectangle (item.Bounds.Left, item.Bounds.Top,
                        control.ScaledCheckWidth + control.ScaledColumnWidth (control.Columns[0]), item.Bounds.Height);

                e.Canvas.FillRectangle (highlight, Theme.ControlHighlightLowColor);
            }

            RenderCheckBox (control, item, e);

            var x = item.Bounds.Left + control.ScaledCheckWidth;

            for (var i = 0; i < control.Columns.Count; i++) {
                var width = control.ScaledColumnWidth (control.Columns[i]);
                var cell = new Rectangle (x, item.Bounds.Top, width, item.Bounds.Height);

                // Column 0 is the item's own Text; the rest are its subitems -- which is why every
                // subitem was invisible while this drew item.Text only.
                var text = i == 0
                    ? item.Text
                    : i < item.SubItems.Count ? item.SubItems[i].Text : string.Empty;

                if (!string.IsNullOrEmpty (text)) {
                    e.Canvas.Save ();
                    e.Canvas.Clip (cell);
                    e.Canvas.DrawText (text, Theme.UIFont, font_size, Padded (cell, e),
                        Foreground (item, i), Align (control.Columns[i].TextAlign), maxLines: 1);
                    e.Canvas.Restore ();
                }

                if (control.GridLines)
                    e.Canvas.DrawLine (cell.Right - 1, cell.Top, cell.Right - 1, cell.Bottom, Theme.BorderLowColor);

                x += width;
            }

            if (control.GridLines)
                e.Canvas.DrawLine (item.Bounds.Left, item.Bounds.Bottom - 1,
                    item.Bounds.Right, item.Bounds.Bottom - 1, Theme.BorderLowColor);
        }

        /// <summary>Renders a single-line row for the List and SmallIcon views.</summary>
        protected virtual void RenderTextRow (ListView control, ListViewItem item, PaintEventArgs e)
        {
            var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

            if (item.Selected)
                e.Canvas.FillRectangle (item.Bounds, Theme.ControlHighlightLowColor);

            RenderCheckBox (control, item, e);

            var x = item.Bounds.Left + control.ScaledCheckWidth;

            // SmallIcon shows the icon beside the text; List is text only.
            if (control.View == View.SmallIcon && item.ImageSK is not null) {
                var size = Math.Min (item.Bounds.Height - e.LogicalToDeviceUnits (2), e.LogicalToDeviceUnits (16));
                var image = new Rectangle (x + e.LogicalToDeviceUnits (1),
                    item.Bounds.Top + (item.Bounds.Height - size) / 2, size, size);

                e.Canvas.DrawBitmap (item.ImageSK, image);
                x = image.Right + e.LogicalToDeviceUnits (3);
            }

            var text_bounds = new Rectangle (x, item.Bounds.Top, item.Bounds.Right - x, item.Bounds.Height);

            e.Canvas.Save ();
            e.Canvas.Clip (item.Bounds);
            e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, Padded (text_bounds, e),
                Foreground (item, 0), ContentAlignment.MiddleLeft, maxLines: 1);
            e.Canvas.Restore ();
        }

        /// <summary>Renders a large-icon or tile item: the icon above centred text.</summary>
        protected virtual void RenderTile (ListView control, ListViewItem item, PaintEventArgs e)
        {
            if (item.Selected)
                e.Canvas.FillRectangle (item.Bounds, Theme.ControlHighlightLowColor);

            RenderCheckBox (control, item, e);

            var image_size = e.LogicalToDeviceUnits (32);
            var image_area = new Rectangle (item.Bounds.Left, item.Bounds.Top, item.Bounds.Width, item.Bounds.Width);
            var image_bounds = DrawingExtensions.CenterSquare (image_area, image_size);
            image_bounds.Y = item.Bounds.Top + e.LogicalToDeviceUnits (3);

            if (item.ImageSK != null)
                e.Canvas.DrawBitmap (item.ImageSK, image_bounds);

            if (!string.IsNullOrWhiteSpace (item.Text)) {
                var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

                e.Canvas.Save ();
                e.Canvas.Clip (item.Bounds);

                var text_bounds = new Rectangle (item.Bounds.Left, image_bounds.Bottom + e.LogicalToDeviceUnits (3), item.Bounds.Width, item.Bounds.Bottom - image_bounds.Bottom - e.LogicalToDeviceUnits (3));

                e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, text_bounds, Foreground (item, 0), ContentAlignment.MiddleCenter);

                e.Canvas.Restore ();
            }
        }

        /// <summary>Draws the item's check box when <see cref="ListView.CheckBoxes"/> is set.</summary>
        protected virtual void RenderCheckBox (ListView control, ListViewItem item, PaintEventArgs e)
        {
            if (!control.CheckBoxes)
                return;

            var size = e.LogicalToDeviceUnits (13);
            var box = new Rectangle (item.Bounds.Left + e.LogicalToDeviceUnits (2),
                item.Bounds.Top + (item.Bounds.Height - size) / 2, size, size);

            // The same glyph CheckBox draws, so the two cannot drift apart.
            ControlPaint.DrawCheckBox (e, box,
                item.Checked ? CheckState.Checked : CheckState.Unchecked, !control.Enabled);
        }

        // A per-item or per-subitem ForeColor overrides the theme; Color.Empty means "use the theme".
        private static SkiaSharp.SKColor Foreground (ListViewItem item, int column)
        {
            var color = column > 0 && column < item.SubItems.Count && item.SubItems[column].ForeColor != Color.Empty
                ? item.SubItems[column].ForeColor
                : item.ForeColor;

            return color == Color.Empty ? Theme.ForegroundColor : color.ToSKColor ();
        }

        private static Rectangle Padded (Rectangle cell, PaintEventArgs e)
        {
            var inset = e.LogicalToDeviceUnits (4);

            return new Rectangle (cell.Left + inset, cell.Top, Math.Max (0, cell.Width - inset * 2), cell.Height);
        }

        private static ContentAlignment Align (HorizontalAlignment alignment) => alignment switch {
            HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
            HorizontalAlignment.Right => ContentAlignment.MiddleRight,
            _ => ContentAlignment.MiddleLeft,
        };
    }
}
