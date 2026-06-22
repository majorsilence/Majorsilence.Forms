using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a DataGridView.
    /// </summary>
    public class DataGridViewRenderer : Renderer<DataGridView>
    {
        /// <inheritdoc/>
        protected override void Render (DataGridView control, PaintEventArgs e)
        {
            var content = control.GetContentArea ();

            e.Canvas.Save ();
            e.Canvas.Clip (content);

            // Draw column headers
            if (control.ColumnHeadersVisible)
                RenderColumnHeaders (control, e, content);

            // Draw rows
            RenderRows (control, e, content);

            e.Canvas.Restore ();
        }

        /// <summary>
        /// Renders the column headers.
        /// </summary>
        protected virtual void RenderColumnHeaders (DataGridView control, PaintEventArgs e, Rectangle contentArea)
        {
            var header_height = control.ScaledHeaderHeight;
            var row_header_offset = control.RowHeadersVisible ? control.ScaledRowHeadersWidth : 0;
            var y = contentArea.Top;
            var left0 = contentArea.Left + row_header_offset;
            var left_width = control.FrozenColumnsWidth;
            var right_width = control.RightPinnedColumnsWidth;
            var left_end = left0 + left_width;
            var right_start = contentArea.Right - right_width;

            // Draw header background
            var header_rect = new Rectangle (contentArea.Left, y, contentArea.Width, header_height);
            var header_bg = control.ColumnHeadersDefaultCellStyle.BackgroundColor ?? Theme.ControlMidColor;
            e.Canvas.FillRectangle (header_rect, header_bg);

            // Draw row header corner cell
            if (control.RowHeadersVisible) {
                var corner_rect = new Rectangle (contentArea.Left, y, row_header_offset, header_height);
                e.Canvas.FillRectangle (corner_rect, header_bg);
                e.Canvas.DrawLine (corner_rect.Right - 1, corner_rect.Top, corner_rect.Right - 1, corner_rect.Bottom, Theme.BorderLowColor);
            }

            // Scrollable headers (clipped to the middle band), then pinned headers (left + right) on top.
            e.Canvas.Save ();
            e.Canvas.Clip (new Rectangle (left_end, y, Math.Max (0, right_start - left_end), header_height));
            for (var i = 0; i < control.Columns.Count; i++)
                if (control.Columns[i].Visible && !control.Columns[i].Frozen && !control.Columns[i].PinnedRight)
                    RenderColumnHeaderAt (control, i, y, header_height, e);
            e.Canvas.Restore ();

            if (left_width > 0) {
                e.Canvas.Save ();
                e.Canvas.Clip (new Rectangle (left0, y, left_width, header_height));
                for (var i = 0; i < control.Columns.Count; i++)
                    if (control.Columns[i].Visible && control.Columns[i].Frozen)
                        RenderColumnHeaderAt (control, i, y, header_height, e);
                e.Canvas.Restore ();
            }

            if (right_width > 0) {
                e.Canvas.Save ();
                e.Canvas.Clip (new Rectangle (right_start, y, right_width, header_height));
                for (var i = 0; i < control.Columns.Count; i++)
                    if (control.Columns[i].Visible && control.Columns[i].PinnedRight)
                        RenderColumnHeaderAt (control, i, y, header_height, e);
                e.Canvas.Restore ();
            }

            // Draw header bottom border
            e.Canvas.DrawLine (contentArea.Left, y + header_height - 1, contentArea.Right, y + header_height - 1, Theme.BorderMidColor);
        }

        // Renders a single column header at its frozen-aware device position.
        private void RenderColumnHeaderAt (DataGridView control, int columnIndex, int y, int header_height, PaintEventArgs e)
        {
            var column = control.Columns[columnIndex];
            var col_width = control.LogicalToDeviceUnits (column.Width);
            var cell_rect = new Rectangle (control.GetColumnDeviceLeft (columnIndex), y, col_width, header_height);
            column.HeaderBounds = cell_rect;
            RenderColumnHeader (control, column, columnIndex, cell_rect, e);
        }

        /// <summary>
        /// Renders a single column header.
        /// </summary>
        protected virtual void RenderColumnHeader (DataGridView control, DataGridViewColumn column, int columnIndex, Rectangle bounds, PaintEventArgs e)
        {
            // Draw right border
            e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom, Theme.BorderLowColor);

            // Draw text, reserving room on the right for any glyphs (sort/filter funnel) a subclass draws.
            var text_bounds = bounds;
            text_bounds.Inflate (-6, 0);
            var right_inset = HeaderRightInset (control, column);
            if (right_inset > 0)
                text_bounds.Width = Math.Max (0, text_bounds.Width - right_inset);

            var fg = control.ColumnHeadersDefaultCellStyle.ForegroundColor ?? Theme.ForegroundColor;
            var font = control.ColumnHeadersDefaultCellStyle.Font ?? Theme.UIFontBold;
            var font_size = control.ColumnHeadersDefaultCellStyle.FontSize ?? Theme.ItemFontSize;

            e.Canvas.DrawText (column.HeaderText, font, control.LogicalToDeviceUnits (font_size), text_bounds, fg, column.HeaderAlignment, maxLines: 1);

            // Draw sort indicator
            if (column.SortOrder != SortOrder.None)
                RenderSortGlyph (e, bounds, column.SortOrder, fg);
        }

        /// <summary>
        /// Renders the sort direction glyph.
        /// </summary>
        protected virtual void RenderSortGlyph (PaintEventArgs e, Rectangle bounds, SortOrder sortOrder, SKColor color)
        {
            var glyph_size = 6;
            var glyph_x = bounds.Right - glyph_size - 8;
            var glyph_y = bounds.Top + (bounds.Height - glyph_size) / 2;

            using var path = new SKPath ();

            if (sortOrder == SortOrder.Ascending) {
                path.MoveTo (glyph_x, glyph_y + glyph_size);
                path.LineTo (glyph_x + glyph_size / 2, glyph_y);
                path.LineTo (glyph_x + glyph_size, glyph_y + glyph_size);
                path.Close ();
            } else {
                path.MoveTo (glyph_x, glyph_y);
                path.LineTo (glyph_x + glyph_size / 2, glyph_y + glyph_size);
                path.LineTo (glyph_x + glyph_size, glyph_y);
                path.Close ();
            }

            using var paint = new SKPaint { Color = color, IsAntialias = true };
            e.Canvas.DrawPath (path, paint);
        }

        /// <summary>
        /// Renders the data rows.
        /// </summary>
        protected virtual void RenderRows (DataGridView control, PaintEventArgs e, Rectangle contentArea)
        {
            var header_offset = control.RowsTopOffset;
            var y = contentArea.Top + header_offset;

            for (var i = control.FirstDisplayedScrollingRowIndex; i < control.Rows.Count; i++) {
                if (y >= contentArea.Bottom)
                    break;

                var row = control.Rows[i];
                var row_height = control.LogicalToDeviceUnits (row.Height);
                var row_rect = new Rectangle (contentArea.Left, y, contentArea.Width, Math.Min (row_height, contentArea.Bottom - y));

                row.Bounds = row_rect;

                RenderRow (control, row, i, row_rect, e);

                y += row_height;
            }
        }

        /// <summary>
        /// Renders a single row.
        /// </summary>
        protected virtual void RenderRow (DataGridView control, DataGridViewRow row, int rowIndex, Rectangle bounds, PaintEventArgs e)
        {
            // Determine background color from cell styles
            SKColor? bg = null;

            if (control.SelectedRowIndex == rowIndex)
                bg = Theme.ControlHighlightLowColor;
            else if (control.HoveredRowIndex == rowIndex)
                bg = Theme.ControlMidColor;
            else if (rowIndex % 2 == 1 && control.AlternatingRowColorsEnabled && control.AlternatingRowsDefaultCellStyle.BackgroundColor.HasValue)
                bg = control.AlternatingRowsDefaultCellStyle.BackgroundColor.Value;
            else if (rowIndex % 2 == 1 && control.AlternatingRowColorsEnabled)
                bg = AlternatingRowColor ();
            else if (control.DefaultCellStyle.BackgroundColor.HasValue)
                bg = control.DefaultCellStyle.BackgroundColor.Value;

            if (bg.HasValue)
                e.Canvas.FillRectangle (bounds, bg.Value);

            // Let subclasses apply row-level formatting (clears + sets per-cell styles for this frame).
            control.RaiseRowFormatting (row, rowIndex);

            // Draw row header
            if (control.RowHeadersVisible) {
                var rh_width = control.ScaledRowHeadersWidth;
                var rh_rect = new Rectangle (bounds.Left, bounds.Top, rh_width, bounds.Height);
                RenderRowHeader (control, row, rowIndex, rh_rect, e);
            }

            // Draw cells. Scrollable columns are clipped to the middle band; pinned columns (left + right)
            // are drawn last (on top) so they stay put and never reveal scrolled content.
            var left0 = bounds.Left + (control.RowHeadersVisible ? control.ScaledRowHeadersWidth : 0);
            var left_width = control.FrozenColumnsWidth;
            var right_width = control.RightPinnedColumnsWidth;
            var left_end = left0 + left_width;
            var right_start = bounds.Right - right_width;

            e.Canvas.Save ();
            e.Canvas.Clip (new Rectangle (left_end, bounds.Top, Math.Max (0, right_start - left_end), bounds.Height));
            for (var i = 0; i < control.Columns.Count; i++)
                if (control.Columns[i].Visible && !control.Columns[i].Frozen && !control.Columns[i].PinnedRight)
                    RenderRowCell (control, row, rowIndex, i, bounds, e);
            e.Canvas.Restore ();

            if (left_width > 0) {
                e.Canvas.Save ();
                e.Canvas.Clip (new Rectangle (left0, bounds.Top, left_width, bounds.Height));
                for (var i = 0; i < control.Columns.Count; i++)
                    if (control.Columns[i].Visible && control.Columns[i].Frozen)
                        RenderRowCell (control, row, rowIndex, i, bounds, e);
                e.Canvas.Restore ();
            }

            if (right_width > 0) {
                e.Canvas.Save ();
                e.Canvas.Clip (new Rectangle (right_start, bounds.Top, right_width, bounds.Height));
                for (var i = 0; i < control.Columns.Count; i++)
                    if (control.Columns[i].Visible && control.Columns[i].PinnedRight)
                        RenderRowCell (control, row, rowIndex, i, bounds, e);
                e.Canvas.Restore ();
            }

            // Draw row bottom border
            e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, Theme.BorderLowColor);
        }

        // Draws a single data cell at its frozen-aware device position.
        private void RenderRowCell (DataGridView control, DataGridViewRow row, int rowIndex, int columnIndex, Rectangle bounds, PaintEventArgs e)
        {
            var column = control.Columns[columnIndex];
            var col_width = control.LogicalToDeviceUnits (column.Width);
            var cell_rect = new Rectangle (control.GetColumnDeviceLeft (columnIndex), bounds.Top, col_width, bounds.Height);

            if (columnIndex < row.Cells.Count)
                row.Cells[columnIndex].Bounds = cell_rect;

            // Let subclasses apply cell-level formatting (colors + a display-text override) before
            // the value and style are read for drawing.
            control.RaiseCellFormatting (row, rowIndex, columnIndex);

            var the_cell = columnIndex < row.Cells.Count ? row.Cells[columnIndex] : null;
            var cell_value = the_cell?.FormattedTextOverride ?? the_cell?.Value?.ToString () ?? string.Empty;
            var cell_style = the_cell?.Style;
            RenderCell (control, column, cell_value, rowIndex, columnIndex, cell_rect, cell_style, e);
        }

        /// <summary>
        /// Renders a row header cell.
        /// </summary>
        protected virtual void RenderRowHeader (DataGridView control, DataGridViewRow row, int rowIndex, Rectangle bounds, PaintEventArgs e)
        {
            var bg = control.RowHeadersDefaultCellStyle.BackgroundColor ?? Theme.ControlMidColor;
            e.Canvas.FillRectangle (bounds, bg);

            // Draw right border
            e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom, Theme.BorderLowColor);

            // Draw selection indicator triangle for the selected row
            if (control.SelectedRowIndex == rowIndex) {
                var tri_size = 6;
                var tri_x = bounds.Left + (bounds.Width - tri_size) / 2;
                var tri_y = bounds.Top + (bounds.Height - tri_size) / 2;

                using var path = new SKPath ();
                path.MoveTo (tri_x, tri_y);
                path.LineTo (tri_x + tri_size, tri_y + tri_size / 2);
                path.LineTo (tri_x, tri_y + tri_size);
                path.Close ();

                using var paint = new SKPaint { Color = Theme.ForegroundColor, IsAntialias = true };
                e.Canvas.DrawPath (path, paint);
            }
        }

        /// <summary>
        /// Renders a single cell.
        /// </summary>
        protected virtual void RenderCell (DataGridView control, DataGridViewColumn column, string value, int rowIndex, int columnIndex, Rectangle bounds, ControlStyle? cellStyle, PaintEventArgs e)
        {
            // Draw per-cell background if set
            var cell_bg = cellStyle?.BackgroundColor;

            if (cell_bg.HasValue && control.SelectedRowIndex != rowIndex && control.HoveredRowIndex != rowIndex)
                e.Canvas.FillRectangle (bounds, cell_bg.Value);

            // Draw cell right border
            e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom, Theme.BorderLowColor);

            // Draw cell selection for cell mode
            if (control.SelectionMode != DataGridViewSelectionMode.FullRowSelect && control.SelectedRowIndex == rowIndex && control.SelectedColumnIndex == columnIndex)
                e.Canvas.DrawRectangle (bounds, Theme.AccentColor, 2);

            var text_bounds = bounds;
            text_bounds.Inflate (-4, 0);
            // Reserve room on the left for any glyph a subclass draws (e.g. a master-detail expander).
            var left_inset = CellLeftInset (control, column);
            if (left_inset > 0) {
                text_bounds.X += left_inset;
                text_bounds.Width = Math.Max (0, text_bounds.Width - left_inset);
            }

            var fg = cellStyle?.ForegroundColor ?? control.DefaultCellStyle.ForegroundColor ?? Theme.ForegroundColor;
            var font = cellStyle?.Font ?? control.DefaultCellStyle.Font ?? Theme.UIFont;
            var font_size = cellStyle?.FontSize ?? control.DefaultCellStyle.FontSize ?? Theme.ItemFontSize;
            var scaled_font = control.LogicalToDeviceUnits (font_size);

            if (column is DataGridViewCheckBoxColumn || column.DisplaysAsCheckBox) {
                RenderCheckBoxCell (e, bounds, value);
            } else if (column is DataGridViewButtonColumn btn_col) {
                var btn_text = btn_col.UseColumnTextForButtonValue ? btn_col.HeaderText : value;
                RenderButtonCell (e, text_bounds, btn_text, font, scaled_font, fg);
            } else if (column is DataGridViewComboBoxColumn) {
                RenderComboBoxCell (e, text_bounds, value, font, scaled_font, fg);
            } else {
                e.Canvas.DrawText (value, font, scaled_font, text_bounds, fg, column.DefaultCellStyleAlignment, maxLines: CellTextMaxLines (column));
            }
        }

        /// <summary>
        /// The maximum number of text lines a cell renders. Default 1 (single line). Subclasses override
        /// to allow wrapping (e.g. the Telerik-compat renderer returns null — unlimited — for a column
        /// whose <c>WrapText</c> is set, so tall rows show multi-line content).
        /// </summary>
        protected virtual int? CellTextMaxLines (DataGridViewColumn column) => 1;

        /// <summary>Device-pixel left inset applied to a cell's text, leaving room for a glyph a subclass draws at the cell's left (e.g. a master-detail expander). Default 0.</summary>
        protected virtual int CellLeftInset (DataGridView control, DataGridViewColumn column) => 0;

        /// <summary>Device-pixel right inset applied to a header's text, leaving room for glyphs a subclass draws at the header's right (sort/filter). Default 0.</summary>
        protected virtual int HeaderRightInset (DataGridView control, DataGridViewColumn column) => 0;

        private static void RenderCheckBoxCell (PaintEventArgs e, Rectangle bounds, string value)
        {
            var size = Math.Min (bounds.Width, bounds.Height) - 6;
            var cx = bounds.Left + (bounds.Width - size) / 2;
            var cy = bounds.Top + (bounds.Height - size) / 2;
            var box = new Rectangle (cx, cy, size, size);

            e.Canvas.DrawRectangle (box, Theme.BorderLowColor);

            var checked_ = value == "True" || value == "1" || value.Equals ("true", StringComparison.OrdinalIgnoreCase);

            if (checked_) {
                var inset = box;
                inset.Inflate (-3, -3);
                e.Canvas.FillRectangle (inset, Theme.AccentColor);
            }
        }

        private static void RenderButtonCell (PaintEventArgs e, Rectangle bounds, string text, SKTypeface font, int fontSize, SKColor fg)
        {
            e.Canvas.DrawRectangle (bounds, Theme.BorderLowColor);
            e.Canvas.DrawText (text, font, fontSize, bounds, fg, ContentAlignment.MiddleCenter, maxLines: 1);
        }

        private static void RenderComboBoxCell (PaintEventArgs e, Rectangle bounds, string value, SKTypeface font, int fontSize, SKColor fg)
        {
            var arrow_size = 10;
            var text_rect = new Rectangle (bounds.Left, bounds.Top, bounds.Width - arrow_size - 4, bounds.Height);
            e.Canvas.DrawText (value, font, fontSize, text_rect, fg, ContentAlignment.MiddleLeft, maxLines: 1);

            // Draw dropdown arrow
            var ax = bounds.Right - arrow_size;
            var ay = bounds.Top + (bounds.Height - arrow_size) / 2;
            e.Canvas.DrawLine (bounds.Right - arrow_size - 2, bounds.Top, bounds.Right - arrow_size - 2, bounds.Bottom, Theme.BorderLowColor);
            e.Canvas.DrawText ("▾", font, fontSize, new Rectangle (ax - 2, ay - 2, arrow_size + 4, arrow_size + 4), fg, ContentAlignment.MiddleCenter);
        }

        /// <summary>
        /// Gets the alternating row background color.
        /// </summary>
        private static SKColor AlternatingRowColor ()
        {
            // Slightly different from the default background
            var bg = Theme.ControlLowColor;
            return new SKColor (
                (byte)Math.Max (0, bg.Red - 5),
                (byte)Math.Max (0, bg.Green - 5),
                (byte)Math.Max (0, bg.Blue - 5),
                bg.Alpha
            );
        }
    }
}
