// The renderer paints in DEVICE pixels, so every rectangle here is DeviceBounds rather than the
// public logical Bounds (LAY-38). The two differ only when the display scale is not 1, which is
// exactly when getting it wrong is invisible in a default test run.
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
                // Items scrolled out of sight are laid out but not drawn. A collapsed group's items
                // are laid out nowhere, so the empty rectangle fails this test and they are skipped
                // by the same line rather than by a second rule.
                if (item.DeviceBounds.Bottom < area.Top || item.DeviceBounds.Top > area.Bottom)
                    continue;

                RenderItem (control, item, e);
            }

            foreach (var band in control.GroupBands) {
                if (band.DeviceBounds.Bottom < area.Top || band.DeviceBounds.Top > area.Bottom)
                    continue;

                if (band.IsFooter)
                    RenderGroupFooter (control, band.Group, band.DeviceBounds, e);
                else
                    RenderGroupHeader (control, band.Group, band.DeviceBounds, e);
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

        /// <summary>Renders one group header band: its rule, its header text, and its subtitle.</summary>
        /// <remarks>
        /// <see cref="ListViewGroup.HeaderAlignment"/> and <see cref="ListViewGroup.Subtitle"/> are read
        /// here; both were stored and consumed by nothing, along with the rest of the group family
        /// (<c>LST-46</c>). The band is one row tall, which is what lets the scrolling arithmetic treat
        /// it as an ordinary line.
        /// </remarks>
        protected virtual void RenderGroupHeader (ListView control, ListViewGroup group, Rectangle bounds, PaintEventArgs e)
        {
            var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);
            var inset = e.LogicalToDeviceUnits (4);
            var text = new Rectangle (bounds.Left + inset, bounds.Top, Math.Max (0, bounds.Width - inset * 2), bounds.Height);

            e.Canvas.Save ();
            e.Canvas.Clip (bounds);

            // The header's own image, from the list's GroupImageList. TitleImageIndex and
            // TitleImageKey were both stored and read by nothing, so a group that named an icon showed
            // none -- and the key was the more likely of the two to be used, since that is what the
            // designer writes.
            if (TitleImage (control, group) is { } title_image) {
                var side = Math.Min (title_image.Height, bounds.Height - inset);
                var image_bounds = new Rectangle (text.Left, bounds.Top + (bounds.Height - side) / 2, side, side);

                e.Canvas.DrawBitmap (title_image, image_bounds);

                // The caption starts after the image rather than under it.
                text = new Rectangle (image_bounds.Right + inset, text.Top,
                    Math.Max (0, text.Right - image_bounds.Right - inset), text.Height);
            }

            // The task link is drawn right-aligned and takes its space out of the caption's, so a long
            // header cannot run underneath it. ListView.GroupTaskLinkBounds keeps the same rectangle
            // for the click that raises GroupTaskLinkClick -- one arithmetic, two readers.
            if (!string.IsNullOrEmpty (group.TaskLink)) {
                var link = control.GroupTaskLinkBounds (group, bounds);

                e.Canvas.DrawText (group.TaskLink, Theme.UIFont, font_size, link,
                    Theme.AccentColor, ContentAlignment.MiddleRight, maxLines: 1);

                text = new Rectangle (text.Left, text.Top, Math.Max (0, link.Left - text.Left - inset), text.Height);
            }

            var caption = string.IsNullOrEmpty (group.Subtitle)
                ? group.Header
                : $"{group.Header}  {group.Subtitle}";

            e.Canvas.DrawText (caption, Theme.UIFontBold, font_size, text,
                Theme.ForegroundColor, HeaderAlign (group.HeaderAlignment), maxLines: 1);

            // The rule under the caption is what separates a band from a row at a glance; without it a
            // header reads as just another item in bold.
            e.Canvas.DrawLine (bounds.Left + inset, bounds.Bottom - 1, bounds.Right - inset, bounds.Bottom - 1,
                Theme.BorderLowColor);

            e.Canvas.Restore ();
        }

        /// <summary>Renders one group footer band: the group's <see cref="ListViewGroup.Footer"/>.</summary>
        /// <remarks>
        /// Placed after the group's items rather than under its header, which is where upstream puts it
        /// and the only placement that makes <see cref="ListViewGroup.FooterAlignment"/> mean anything
        /// separate from <see cref="ListViewGroup.HeaderAlignment"/>. Like the header it is exactly one
        /// row tall, so the scroll arithmetic still sees uniform lines -- see <c>ListView.Groups.cs</c>.
        /// </remarks>
        protected virtual void RenderGroupFooter (ListView control, ListViewGroup group, Rectangle bounds, PaintEventArgs e)
        {
            var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);
            var inset = e.LogicalToDeviceUnits (4);
            var text = new Rectangle (bounds.Left + inset, bounds.Top, Math.Max (0, bounds.Width - inset * 2), bounds.Height);

            e.Canvas.Save ();
            e.Canvas.Clip (bounds);

            e.Canvas.DrawText (group.Footer, Theme.UIFont, font_size, text,
                Theme.ForegroundColor, HeaderAlign (group.FooterAlignment), maxLines: 1);

            e.Canvas.Restore ();
        }

        /// <summary>The drawn width of a group's task-link text, in device pixels.</summary>
        /// <remarks>Lives here rather than on the control because the font and size are the renderer's
        /// to choose; <see cref="ListView.GroupTaskLinkBounds"/> is the only caller.</remarks>
        internal static float MeasureLinkWidth (ListView control, string text)
            => TextMeasurer.MeasureText (text, Theme.UIFont, control.LogicalToDeviceUnits (Theme.ItemFontSize)).Width;

        // The group header's image: by key first, then by index, which is the order every other image
        // pair in this renderer resolves in.
        private static SkiaSharp.SKBitmap? TitleImage (ListView control, ListViewGroup group)
        {
            if (control.GroupImageList is not { } images)
                return null;

            if (!string.IsNullOrEmpty (group.TitleImageKey)) {
                var index = images.Images.IndexOfKey (group.TitleImageKey);

                if (index >= 0)
                    return StateImage (images, index);
            }

            return StateImage (images, group.TitleImageIndex);
        }

        private static ContentAlignment HeaderAlign (HorizontalAlignment alignment)
            => alignment switch {
                HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft
            };

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
            if (ShowsSelection (control, item)) {
                var highlight = control.FullRowSelect || control.Columns.Count == 0
                    ? item.DeviceBounds
                    : new Rectangle (item.DeviceBounds.Left, item.DeviceBounds.Top,
                        control.ScaledCheckWidth + control.ScaledColumnWidth (control.Columns[0]), item.DeviceBounds.Height);

                e.Canvas.FillRectangle (highlight, ListView.DefaultSelectionStyle.GetBackgroundColor ());
            }

            RenderCheckBox (control, item, e);

            var x = item.DeviceBounds.Left + control.ScaledCheckWidth;

            for (var i = 0; i < control.Columns.Count; i++) {
                var width = control.ScaledColumnWidth (control.Columns[i]);
                var cell = new Rectangle (x, item.DeviceBounds.Top, width, item.DeviceBounds.Height);

                // Column 0 is the item's own Text; the rest are its subitems -- which is why every
                // subitem was invisible while this drew item.Text only.
                var text = i == 0
                    ? item.Text
                    : i < item.SubItems.Count ? item.SubItems[i].Text : string.Empty;

                if (!string.IsNullOrEmpty (text)) {
                    e.Canvas.Save ();
                    e.Canvas.Clip (cell);
                    e.Canvas.DrawText (text, Theme.UIFont, font_size, Padded (cell, e),
                        Foreground (item, i, ShowsSelection (control, item)), Align (control.Columns[i].TextAlign), maxLines: 1);
                    e.Canvas.Restore ();
                }

                if (control.GridLines)
                    e.Canvas.DrawLine (cell.Right - 1, cell.Top, cell.Right - 1, cell.Bottom, Theme.BorderLowColor);

                x += width;
            }

            if (control.GridLines)
                e.Canvas.DrawLine (item.DeviceBounds.Left, item.DeviceBounds.Bottom - 1,
                    item.DeviceBounds.Right, item.DeviceBounds.Bottom - 1, Theme.BorderLowColor);
        }

        /// <summary>Renders a single-line row for the List and SmallIcon views.</summary>
        protected virtual void RenderTextRow (ListView control, ListViewItem item, PaintEventArgs e)
        {
            var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

            if (item.Selected)
                e.Canvas.FillRectangle (item.DeviceBounds, ListView.DefaultSelectionStyle.GetBackgroundColor ());

            RenderCheckBox (control, item, e);

            var x = item.DeviceBounds.Left + control.ScaledCheckWidth;

            // SmallIcon shows the icon beside the text; List is text only.
            if (control.View == View.SmallIcon && item.ImageSK is not null) {
                var size = Math.Min (item.DeviceBounds.Height - e.LogicalToDeviceUnits (2), e.LogicalToDeviceUnits (16));
                var image = new Rectangle (x + e.LogicalToDeviceUnits (1),
                    item.DeviceBounds.Top + (item.DeviceBounds.Height - size) / 2, size, size);

                e.Canvas.DrawBitmap (item.ImageSK, image);
                x = image.Right + e.LogicalToDeviceUnits (3);
            }

            var text_bounds = new Rectangle (x, item.DeviceBounds.Top, item.DeviceBounds.Right - x, item.DeviceBounds.Height);

            e.Canvas.Save ();
            e.Canvas.Clip (item.DeviceBounds);
            e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, Padded (text_bounds, e),
                Foreground (item, 0, ShowsSelection (control, item)), ContentAlignment.MiddleLeft, maxLines: 1);
            e.Canvas.Restore ();
        }

        /// <summary>Renders a large-icon or tile item: the icon above centred text.</summary>
        protected virtual void RenderTile (ListView control, ListViewItem item, PaintEventArgs e)
        {
            if (ShowsSelection (control, item))
                e.Canvas.FillRectangle (item.DeviceBounds, ListView.DefaultSelectionStyle.GetBackgroundColor ());

            RenderCheckBox (control, item, e);

            var image_size = e.LogicalToDeviceUnits (32);
            var image_area = new Rectangle (item.DeviceBounds.Left, item.DeviceBounds.Top, item.DeviceBounds.Width, item.DeviceBounds.Width);
            var image_bounds = DrawingExtensions.CenterSquare (image_area, image_size);
            image_bounds.Y = item.DeviceBounds.Top + e.LogicalToDeviceUnits (3);

            if (item.ImageSK != null)
                e.Canvas.DrawBitmap (item.ImageSK, image_bounds);

            if (!string.IsNullOrWhiteSpace (item.Text)) {
                var font_size = e.LogicalToDeviceUnits (Theme.ItemFontSize);

                e.Canvas.Save ();
                e.Canvas.Clip (item.DeviceBounds);

                var text_bounds = new Rectangle (item.DeviceBounds.Left, image_bounds.Bottom + e.LogicalToDeviceUnits (3), item.DeviceBounds.Width, item.DeviceBounds.Bottom - image_bounds.Bottom - e.LogicalToDeviceUnits (3));

                e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, text_bounds, Foreground (item, 0, ShowsSelection (control, item)), ContentAlignment.MiddleCenter);

                e.Canvas.Restore ();
            }
        }

        /// <summary>Draws the item's check box when <see cref="ListView.CheckBoxes"/> is set.</summary>
        protected virtual void RenderCheckBox (ListView control, ListViewItem item, PaintEventArgs e)
        {
            if (!control.CheckBoxes)
                return;

            var size = e.LogicalToDeviceUnits (13);
            var box = new Rectangle (item.DeviceBounds.Left + e.LogicalToDeviceUnits (2),
                item.DeviceBounds.Top + (item.DeviceBounds.Height - size) / 2, size, size);

            // A state image replaces the glyph when the list has a StateImageList and the item names an
            // image in it -- which is what that pair is for, and what upstream draws in this slot.
            // Both were stored and read by nothing, so a list using state images showed ordinary check
            // boxes instead.
            if (StateImage (control.StateImageList, item.StateImageIndex) is { } state) {
                e.Canvas.DrawBitmap (state, box, !control.Enabled);
                return;
            }

            // The same glyph CheckBox draws, so the two cannot drift apart.
            ControlPaint.DrawCheckBox (e, box,
                item.Checked ? CheckState.Checked : CheckState.Unchecked, !control.Enabled);
        }

        /// <summary>The image a state-image index names, or null when there is none to draw.</summary>
        /// <remarks>
        /// Shared by this renderer and <c>TreeViewRenderer</c> so the two cannot answer differently.
        /// An index outside the list is treated as "no state image" rather than throwing: an index and
        /// a list that disagree is an application mistake, and a painting path is the worst place to
        /// surface it.
        /// </remarks>
        internal static SkiaSharp.SKBitmap? StateImage (ImageList? images, int index)
            => images is { } list && index >= 0 && index < list.Images.Count ? list.Images[index] : null;

        // A per-item or per-subitem ForeColor overrides the theme; Color.Empty means "use the theme".
        private static SkiaSharp.SKColor Foreground (ListViewItem item, int column, bool selected)
        {
            // UseItemStyleForSubItems (the WinForms default, true) means the sub-items take the item's
            // appearance and their own is ignored; false lets each sub-item colour itself. It was read
            // by nothing, so a sub-item's colour always won -- which is the FALSE behaviour, applied to
            // every list whether it asked for it or not.
            var color = item.UseItemStyleForSubItems
                ? item.ForeColor
                : column > 0 && column < item.SubItems.Count && item.SubItems[column].ForeColor != Color.Empty
                    ? item.SubItems[column].ForeColor
                    : item.ForeColor;

            if (color != Color.Empty)
                return color.ToSKColor ();

            // A `ListView::selection { color }` rule recolours selected items' text.
            return selected && ListView.DefaultSelectionStyle.ForegroundColor is { } selection_fg ? selection_fg : Theme.ForegroundColor;
        }

        /// <summary>Whether <paramref name="item"/> should be drawn as selected right now.</summary>
        /// <remarks>
        /// <see cref="ListView.HideSelection"/> was stored and read by nothing, so a list that had lost
        /// focus went on showing its highlight and the property could not do the one thing it is for.
        /// Upstream defaults it to <c>false</c> -- keep the highlight -- which is why this reads as a
        /// double negative: the highlight disappears only when the application asked for that AND the
        /// control does not have focus.
        /// </remarks>
        protected static bool ShowsSelection (ListView control, ListViewItem item)
            => item.Selected && (control.Focused || !control.HideSelection);

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
