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

            // Hand the header cell its turn. WinForms grids put custom header rendering in
            // DataGridViewCell.Paint, and a subclass overriding it is otherwise never reached -- this
            // library's own extension point is the renderer, which ported code does not know about.
            // The default drawing above has already happened, so an override overlays it.
            column.HeaderCell?.Paint (
                e.Graphics,
                e.ClipRectangle,
                bounds,
                -1,                                   // -1 is WinForms' row index for a column header
                DataGridViewElementStates.Visible,
                column.HeaderText,
                column.HeaderText,
                errorText: null,
                column.HeaderCell.InheritedStyle,
                new DataGridViewAdvancedBorderStyle (),
                DataGridViewPaintParts.All);
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
            var paint_parts = DataGridViewPaintParts.All;

            // RowPrePaint (WinForms): raised before anything of the row is drawn. A handler can draw the
            // row itself -- optionally composing with the grid's own painting through e.PaintCells /
            // e.PaintHeader -- and set Handled to suppress the default rendering below.
            if (control.HasRowPrePaintHandlers) {
                var pre = new DataGridViewRowPrePaintEventArgs (rowIndex) {
                    Graphics = new Majorsilence.Forms.Drawing.Graphics (e.Canvas),
                    ClipBounds = e.ClipRectangle,
                    RowBounds = bounds,
                    State = row.State,
                    ErrorText = row.ErrorText,
                    InheritedRowStyle = row.InheritedStyle,
                    IsFirstDisplayedRow = rowIndex == control.FirstDisplayedScrollingRowIndex,
                    IsLastVisibleRow = rowIndex == control.Rows.Count - 1
                };

                pre.PaintCellsCallback = (clip, parts) => RenderRowCells (control, row, rowIndex, bounds, e, parts);
                pre.PaintHeaderCallback = _ => RenderRowHeaderBand (control, row, rowIndex, bounds, e);

                if (control.RaiseRowPrePaint (pre))
                    return;

                paint_parts = pre.PaintParts;
            }

            // Determine background color from cell styles
            SKColor? bg = null;

            if (control.SelectedRowIndex == rowIndex)
                bg = Theme.ControlHighlightLowColor;
            else if (control.HoveredRowIndex == rowIndex)
                bg = Theme.ControlMidColor;
            else if (!row.DefaultCellStyle.BackColor.IsEmpty)
                // A row-level DefaultCellStyle back color outranks the alternating-row stripe (WinForms
                // style precedence), so per-row highlighting set on the row itself actually shows.
                bg = ToSK (row.DefaultCellStyle.BackColor);
            else if (rowIndex % 2 == 1 && control.AlternatingRowColorsEnabled && control.AlternatingRowsDefaultCellStyle.BackgroundColor.HasValue)
                bg = control.AlternatingRowsDefaultCellStyle.BackgroundColor.Value;
            else if (rowIndex % 2 == 1 && control.AlternatingRowColorsEnabled)
                bg = AlternatingRowColor ();
            else if (control.DefaultCellStyle.BackgroundColor.HasValue)
                bg = control.DefaultCellStyle.BackgroundColor.Value;

            if (bg.HasValue && paint_parts.HasFlag (DataGridViewPaintParts.Background))
                e.Canvas.FillRectangle (bounds, bg.Value);

            // Let subclasses apply row-level formatting (clears + sets per-cell styles for this frame).
            control.RaiseRowFormatting (row, rowIndex);

            // Draw row header
            RenderRowHeaderBand (control, row, rowIndex, bounds, e);

            RenderRowCells (control, row, rowIndex, bounds, e, paint_parts);

            // Draw row bottom border -- the horizontal grid line. Suppressed when the grid's advanced
            // border style says the cells have no bottom edge (CellBorderStyle.None / *Vertical).
            if (paint_parts.HasFlag (DataGridViewPaintParts.Border)
                && control.AdvancedCellBorderStyle.Bottom != DataGridViewAdvancedCellBorderStyle.None)
                e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, BorderColor (control.AdvancedCellBorderStyle.Bottom));

            // Post-paint hook (WinForms RowPostPaint), after the row's cells and border are drawn.
            if (control.HasRowPostPaintHandlers) {
                var post = new DataGridViewRowPostPaintEventArgs (rowIndex) {
                    Graphics = new Majorsilence.Forms.Drawing.Graphics (e.Canvas),
                    ClipBounds = e.ClipRectangle,
                    RowBounds = bounds,
                    State = row.State,
                    ErrorText = row.ErrorText,
                    InheritedRowStyle = row.InheritedStyle,
                    IsFirstDisplayedRow = rowIndex == control.FirstDisplayedScrollingRowIndex,
                    IsLastVisibleRow = rowIndex == control.Rows.Count - 1
                };

                post.PaintCellsCallback = (clip, parts) => RenderRowCells (control, row, rowIndex, bounds, e, parts);
                post.PaintHeaderCallback = _ => RenderRowHeaderBand (control, row, rowIndex, bounds, e);

                control.RaiseRowPostPaint (post);
            }
        }

        // Draws the row's header cell band, when row headers are visible.
        private void RenderRowHeaderBand (DataGridView control, DataGridViewRow row, int rowIndex, Rectangle bounds, PaintEventArgs e)
        {
            if (!control.RowHeadersVisible)
                return;

            var rh_rect = new Rectangle (bounds.Left, bounds.Top, control.ScaledRowHeadersWidth, bounds.Height);
            RenderRowHeader (control, row, rowIndex, rh_rect, e);
        }

        /// <summary>
        /// Draws the row's data cells. Scrollable columns are clipped to the middle band; pinned columns
        /// (left + right) are drawn last (on top) so they stay put and never reveal scrolled content.
        /// </summary>
        private void RenderRowCells (DataGridView control, DataGridViewRow row, int rowIndex, Rectangle bounds, PaintEventArgs e, DataGridViewPaintParts paintParts)
        {
            var left0 = bounds.Left + (control.RowHeadersVisible ? control.ScaledRowHeadersWidth : 0);
            var left_width = control.FrozenColumnsWidth;
            var right_width = control.RightPinnedColumnsWidth;
            var left_end = left0 + left_width;
            var right_start = bounds.Right - right_width;

            e.Canvas.Save ();
            e.Canvas.Clip (new Rectangle (left_end, bounds.Top, Math.Max (0, right_start - left_end), bounds.Height));
            for (var i = 0; i < control.Columns.Count; i++)
                if (control.Columns[i].Visible && !control.Columns[i].Frozen && !control.Columns[i].PinnedRight)
                    RenderRowCell (control, row, rowIndex, i, bounds, e, paintParts);
            e.Canvas.Restore ();

            if (left_width > 0) {
                e.Canvas.Save ();
                e.Canvas.Clip (new Rectangle (left0, bounds.Top, left_width, bounds.Height));
                for (var i = 0; i < control.Columns.Count; i++)
                    if (control.Columns[i].Visible && control.Columns[i].Frozen)
                        RenderRowCell (control, row, rowIndex, i, bounds, e, paintParts);
                e.Canvas.Restore ();
            }

            if (right_width > 0) {
                e.Canvas.Save ();
                e.Canvas.Clip (new Rectangle (right_start, bounds.Top, right_width, bounds.Height));
                for (var i = 0; i < control.Columns.Count; i++)
                    if (control.Columns[i].Visible && control.Columns[i].PinnedRight)
                        RenderRowCell (control, row, rowIndex, i, bounds, e, paintParts);
                e.Canvas.Restore ();
            }
        }

        // Draws a single data cell at its frozen-aware device position.
        private void RenderRowCell (DataGridView control, DataGridViewRow row, int rowIndex, int columnIndex, Rectangle bounds, PaintEventArgs e, DataGridViewPaintParts paintParts)
        {
            var column = control.Columns[columnIndex];
            var col_width = control.LogicalToDeviceUnits (column.Width);
            var cell_rect = new Rectangle (control.GetColumnDeviceLeft (columnIndex), bounds.Top, col_width, bounds.Height);

            if (columnIndex < row.Cells.Count)
                row.Cells[columnIndex].Bounds = cell_rect;

            // Formatting pass: the subclass hook plus the WinForms CellFormatting event, resolving the
            // display text and any style the handler applied for this frame only.
            var formatted = control.ApplyCellFormatting (row, rowIndex, columnIndex, out var handler_style);

            var the_cell = columnIndex < row.Cells.Count ? row.Cells[columnIndex] : null;
            var cell_value = formatted ?? the_cell?.FormattedTextOverride ?? the_cell?.Value?.ToString () ?? string.Empty;
            var cell_style = MergeFormattingStyle (the_cell?.Style, handler_style);

            // CellPainting (WinForms): a handler can draw the cell itself, call back into the grid's
            // default painting for the parts it does not draw, and set Handled to suppress the rest.
            if (control.HasCellPaintingHandlers) {
                var args = new DataGridViewCellPaintingEventArgs (columnIndex, rowIndex) {
                    Graphics = new Majorsilence.Forms.Drawing.Graphics (e.Canvas),
                    ClipBounds = e.ClipRectangle,
                    CellBounds = cell_rect,
                    Value = the_cell?.Value,
                    FormattedValue = cell_value,
                    ErrorText = the_cell?.ErrorText ?? string.Empty,
                    CellStyle = the_cell?.InheritedStyle,
                    PaintParts = paintParts
                };

                args.PaintCallback = (rect, parts) => RenderCell (control, column, cell_value, rowIndex, columnIndex, rect, cell_style, e, parts);

                if (control.RaiseCellPainting (args))
                    return;

                paintParts = args.PaintParts;
                cell_value = args.FormattedValue?.ToString () ?? cell_value;
            }

            // Route through the 8-argument overload for the everyday all-parts case so existing renderer
            // subclasses that override it keep being called.
            if (paintParts == DataGridViewPaintParts.All)
                RenderCell (control, column, cell_value, rowIndex, columnIndex, cell_rect, cell_style, e);
            else
                RenderCell (control, column, cell_value, rowIndex, columnIndex, cell_rect, cell_style, e, paintParts);
        }

        // Overlays the colors/font a CellFormatting handler set (System.Drawing-typed) onto the cell's own
        // style, for this paint only -- the cell object itself is never mutated.
        private static ControlStyle? MergeFormattingStyle (ControlStyle? cellStyle, DataGridViewCellStyle? handlerStyle)
        {
            if (handlerStyle is null
                || (handlerStyle.BackColor.IsEmpty && handlerStyle.ForeColor.IsEmpty && handlerStyle.Font is null))
                return cellStyle;

            var merged = new ControlStyle (cellStyle!) {
                BackgroundColor = cellStyle?.BackgroundColor,
                ForegroundColor = cellStyle?.ForegroundColor,
                Font = cellStyle?.Font,
                FontSize = cellStyle?.FontSize
            };

            if (!handlerStyle.BackColor.IsEmpty)
                merged.BackColor = handlerStyle.BackColor;

            if (!handlerStyle.ForeColor.IsEmpty)
                merged.ForeColor = handlerStyle.ForeColor;

            if (handlerStyle.Font is { } font) {
                merged.Font = font.GetSKTypeface ();
                // Pixels, not points -- see the note on Control.Font's setter.
                merged.FontSize = (int)System.Math.Round (font.PixelSize);
            }

            return merged;
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
            => RenderCell (control, column, value, rowIndex, columnIndex, bounds, cellStyle, e, DataGridViewPaintParts.All);

        /// <summary>
        /// Renders the requested parts of a single cell. Called with less than
        /// <see cref="DataGridViewPaintParts.All"/> when a <see cref="DataGridView.CellPainting"/> or
        /// <see cref="DataGridView.RowPrePaint"/> handler asked the grid to draw only some parts.
        /// </summary>
        protected virtual void RenderCell (DataGridView control, DataGridViewColumn column, string value, int rowIndex, int columnIndex, Rectangle bounds, ControlStyle? cellStyle, PaintEventArgs e, DataGridViewPaintParts paintParts)
        {
            // Draw per-cell background if set
            var cell_bg = cellStyle?.BackgroundColor;

            if (cell_bg.HasValue && paintParts.HasFlag (DataGridViewPaintParts.Background)
                && control.SelectedRowIndex != rowIndex && control.HoveredRowIndex != rowIndex)
                e.Canvas.FillRectangle (bounds, cell_bg.Value);

            // Draw the cell's borders as described by the grid's advanced (per-edge) border style.
            if (paintParts.HasFlag (DataGridViewPaintParts.Border))
                RenderCellBorders (control.AdvancedCellBorderStyle, bounds, e);

            // Draw cell selection for cell mode
            if (paintParts.HasFlag (DataGridViewPaintParts.SelectionBackground)
                && control.SelectionMode != DataGridViewSelectionMode.FullRowSelect
                && control.SelectedRowIndex == rowIndex && control.SelectedColumnIndex == columnIndex)
                e.Canvas.DrawRectangle (bounds, Theme.AccentColor, 2);

            if (!paintParts.HasFlag (DataGridViewPaintParts.ContentForeground)
                && !paintParts.HasFlag (DataGridViewPaintParts.ContentBackground))
                return;

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

            if (column is DataGridViewImageColumn image_col) {
                RenderImageCell (control, image_col, rowIndex, columnIndex, bounds, e);
            } else if (column is DataGridViewCheckBoxColumn || column.DisplaysAsCheckBox) {
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
        /// Draws a cell's edges from a <see cref="DataGridViewAdvancedBorderStyle"/>. The right and bottom
        /// edges are the grid lines and are drawn unless their edge is
        /// <see cref="DataGridViewAdvancedCellBorderStyle.None"/> (the bottom edge is drawn once per row,
        /// by <see cref="RenderRow"/>); the left and top edges are only drawn when explicitly set, so a
        /// default grid does not double up lines between neighbouring cells.
        /// </summary>
        protected virtual void RenderCellBorders (DataGridViewAdvancedBorderStyle borderStyle, Rectangle bounds, PaintEventArgs e)
        {
            ArgumentNullException.ThrowIfNull (borderStyle);

            if (borderStyle.Right != DataGridViewAdvancedCellBorderStyle.None)
                e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom, BorderColor (borderStyle.Right));

            if (borderStyle.Left is not DataGridViewAdvancedCellBorderStyle.None and not DataGridViewAdvancedCellBorderStyle.NotSet)
                e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Left, bounds.Bottom, BorderColor (borderStyle.Left));

            if (borderStyle.Top is not DataGridViewAdvancedCellBorderStyle.None and not DataGridViewAdvancedCellBorderStyle.NotSet)
                e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Right, bounds.Top, BorderColor (borderStyle.Top));
        }

        // System.Drawing color (the DataGridViewCellStyle surface) to the Skia color the canvas wants.
        private static SKColor ToSK (System.Drawing.Color color) => new SKColor (color.R, color.G, color.B, color.A);

        // Theme color for an edge style: sunken/raised edges read darker/lighter than a plain single line.
        private static SKColor BorderColor (DataGridViewAdvancedCellBorderStyle style) => style switch {
            DataGridViewAdvancedCellBorderStyle.Inset or DataGridViewAdvancedCellBorderStyle.InsetDouble => Theme.BorderMidColor,
            DataGridViewAdvancedCellBorderStyle.Outset or DataGridViewAdvancedCellBorderStyle.OutsetDouble
                or DataGridViewAdvancedCellBorderStyle.OutsetPartial => Theme.BorderHighColor,
            _ => Theme.BorderLowColor
        };

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

        /// <summary>
        /// Draws the cell's image, scaled to fit inside the cell and centred, preserving aspect ratio.
        /// </summary>
        /// <remarks>
        /// Reads the raw cell value rather than the formatted string the other branches use -- an image
        /// has no useful text form, which is exactly why a bound image column used to render as its type
        /// name. Falls back to the column's own Image so a column with a fixed icon still draws.
        /// </remarks>
        protected virtual void RenderImageCell (DataGridView control, DataGridViewImageColumn column,
            int rowIndex, int columnIndex, Rectangle bounds, PaintEventArgs e)
        {
            ArgumentNullException.ThrowIfNull (control);
            ArgumentNullException.ThrowIfNull (column);
            ArgumentNullException.ThrowIfNull (e);

            object? raw = null;

            if (rowIndex >= 0 && rowIndex < control.Rows.Count) {
                var cells = control.Rows[rowIndex].Cells;
                if (columnIndex >= 0 && columnIndex < cells.Count)
                    raw = cells[columnIndex].Value;
            }

            var image = raw as Majorsilence.Forms.Drawing.Image ?? column.Image;
            var bitmap = image?.GetSKBitmap ();

            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                return;

            var box = bounds;
            box.Inflate (-2, -2);

            if (box.Width <= 0 || box.Height <= 0)
                return;

            // Scale down to fit, but never up -- a 24px icon in a tall row should stay 24px rather than
            // being blown up and blurred.
            var scale = Math.Min (1.0, Math.Min ((double)box.Width / bitmap.Width, (double)box.Height / bitmap.Height));
            var w = (int)Math.Round (bitmap.Width * scale);
            var h = (int)Math.Round (bitmap.Height * scale);

            e.Canvas.DrawBitmap (bitmap,
                new Rectangle (box.Left + ((box.Width - w) / 2), box.Top + ((box.Height - h) / 2), w, h),
                disabled: !control.Enabled);
        }

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
