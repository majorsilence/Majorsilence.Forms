using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Telerik;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Renderer for <see cref="RadGridView"/>. Builds on <see cref="DataGridViewRenderer"/> and adds the
    /// group panel band, per-column filter funnels, group-header rows, and the drag indicator. Although
    /// it derives from the DataGridView renderer it re-declares its <see cref="Type"/> as
    /// <see cref="RadGridView"/> so <see cref="RenderManager"/> resolves it for that control.
    /// </summary>
    public class RadGridViewRenderer : DataGridViewRenderer
    {
        /// <inheritdoc/>
        public override Type Type => typeof (RadGridView);

        /// <inheritdoc/>
        protected override void Render (DataGridView control, PaintEventArgs e)
        {
            if (control is not RadGridView grid) {
                base.Render (control, e);
                return;
            }

            // The header/row passes repopulate these (cleared each frame for fresh hit-test rects).
            grid.FilterGlyphRects.Clear ();
            grid.GroupPillLayouts.Clear ();

            base.Render (control, e);

            if (grid.ShowsFilterRow)
                RenderFilterRow (grid, e);

            if (grid.ShowsGroupPanel)
                RenderGroupPanel (grid, e);

            if (grid.ShowsPagingPanel)
                RenderPagingPanel (grid, e);

            // Only while the grid has no columns: Telerik's table element says nothing under the headers of a grid
            // whose data has no rows.
            if (control.Columns.Count == 0)
                RenderNoDataText (grid, e);

            if (grid.DragActive)
                RenderDragIndicator (grid, e);
        }

        // A grid with no columns says it has no data, centred in it, as Telerik's does.
        private static void RenderNoDataText (RadGridView grid, PaintEventArgs e)
        {
            var area = grid.GetContentArea ();

            // In the grid's own font, as Telerik's: a 12pt grid said so in the theme's small item font.
            e.Canvas.DrawText (RadGridView.NoDataText, grid.GetEffectiveFont (), grid.LogicalToDeviceUnits (grid.GetEffectiveFontSize ()), area,
                grid.Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor, ContentAlignment.MiddleCenter, maxLines: 1);
        }

        // ── Paging panel ──

        // The panel along the bottom (see RadGridView.LayoutPagingPanel), in the theme's colours: its buttons' arrows,
        // greyed where they cannot move, the current page's button selected, and the page box.
        private static void RenderPagingPanel (RadGridView grid, PaintEventArgs e)
        {
            var layout = grid.LayoutPagingPanel ();
            var band = layout.Band;
            var (font, size) = grid.CellFallbackFont ();
            var scaled = grid.LogicalToDeviceUnits (size);
            var page = grid.CurrentPageIndex;
            var last = grid.PageCount - 1;

            e.Canvas.FillRectangle (band, Theme.ControlLowColor);
            e.Canvas.DrawLine (band.Left, band.Top, band.Right, band.Top, Theme.BorderMidColor);

            foreach (var separator in layout.Separators)
                e.Canvas.DrawLine (separator.Left, separator.Top, separator.Left, separator.Bottom, Theme.BorderMidColor);

            SKColor Ink (bool enabled) => enabled && grid.Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;

            // With no rows to page, Telerik's next, last and second "..." show enabled.
            var empty = grid.PagesNoRows;

            DrawArrows (grid, e, layout.First, left: true, count: 2, Ink (page > 0));
            DrawArrows (grid, e, layout.Previous, left: true, count: 1, Ink (page > 0));
            DrawArrows (grid, e, layout.Next, left: false, count: 1, Ink (empty || page < last));
            DrawArrows (grid, e, layout.Last, left: false, count: 2, Ink (empty || page < last));

            var group = grid.PagingGroupStart;
            e.Canvas.DrawText ("...", font, scaled, layout.FastBack, Ink (group > 0), ContentAlignment.MiddleCenter, maxLines: 1);
            e.Canvas.DrawText ("...", font, scaled, layout.FastForward, Ink (empty || group + RadGridView.PagingNumericButtonsCount <= last), ContentAlignment.MiddleCenter, maxLines: 1);

            foreach (var (number, bounds) in layout.Pages) {
                var current = number == page;

                if (current)
                    e.Canvas.FillRectangle (bounds, DataGridView.DefaultSelectionStyle.GetBackgroundColor ());

                var ink = current && DataGridView.DefaultSelectionStyle.ForegroundColor is { } selected ? selected : Ink (true);
                e.Canvas.DrawText ((number + 1).ToString (System.Globalization.CultureInfo.CurrentCulture), font, scaled, bounds, ink, ContentAlignment.MiddleCenter, maxLines: 1);
            }

            e.Canvas.DrawText ("Page", font, scaled, layout.PageLabel, Ink (true), ContentAlignment.MiddleCenter, maxLines: 1);
            e.Canvas.DrawText ("of", font, scaled, layout.OfLabel, Ink (true), ContentAlignment.MiddleCenter, maxLines: 1);
            e.Canvas.DrawText (grid.PageCount.ToString (System.Globalization.CultureInfo.CurrentCulture), font, scaled, layout.CountLabel, Ink (true), ContentAlignment.MiddleCenter, maxLines: 1);

            var box = layout.PageBox;
            e.Canvas.FillRectangle (box, Theme.BackgroundColor);
            e.Canvas.DrawRectangle (box, Theme.BorderMidColor);

            if (!grid.EditingPageBox) {
                var inner = Rectangle.Inflate (box, -grid.LogicalToDeviceUnits (2), 0);
                e.Canvas.DrawText ((page + 1).ToString (System.Globalization.CultureInfo.CurrentCulture), font, scaled, inner, Ink (true), ContentAlignment.MiddleLeft, maxLines: 1);
            }
        }

        // One or two 4 x 7 arrowheads, a pixel apart, centred in a button.
        private static void DrawArrows (RadGridView grid, PaintEventArgs e, Rectangle button, bool left, int count, SKColor colour)
        {
            var w = grid.LogicalToDeviceUnits (4);
            var h = grid.LogicalToDeviceUnits (7);
            var spacing = grid.LogicalToDeviceUnits (1);
            var total = count * w + (count - 1) * spacing;
            var x = button.Left + (button.Width - total) / 2;
            var y = button.Top + (button.Height - h) / 2;

            using var paint = new SKPaint { Color = colour, IsAntialias = false, Style = SKPaintStyle.Fill };

            for (var i = 0; i < count; i++, x += w + spacing) {
                using var path = new SKPath ();

                if (left) {
                    path.MoveTo (x + w, y);
                    path.LineTo (x, y + h / 2f);
                    path.LineTo (x + w, y + h);
                } else {
                    path.MoveTo (x, y);
                    path.LineTo (x + w, y + h / 2f);
                    path.LineTo (x, y + h);
                }

                path.Close ();
                e.Canvas.DrawPath (path, paint);
            }
        }

        // ── Inline filter row ──

        // Telerik's filtering row, measured (UI for WinForms 2026.2.520): each cell leads with its operator's label
        // ("Contains:") in the grid's own font and colour, a pixel in from its left, then the filter's value, aligned as
        // the column's cells are; a 21px operator button runs a pixel down from its top to a pixel above its bottom, its
        // right edge on the cell's right line, 2px right of the label. The label is as wide as its text, cut short with
        // "..." to leave the value its room, and takes the rest of the cell while there is no value.
        internal const int FilterButtonLogicalWidth = 21;

        private static void RenderFilterRow (RadGridView grid, PaintEventArgs e)
        {
            var content = grid.GetContentArea ();
            var y = content.Top + (grid.ColumnHeadersVisible ? grid.ScaledHeaderHeight : 0);
            var h = grid.FilterRowBandHeight;
            var band = new Rectangle (content.Left, y, content.Width, h);

            e.Canvas.FillRectangle (band, Theme.ControlLowColor);
            e.Canvas.DrawLine (band.Left, band.Bottom - 1, band.Right, band.Bottom - 1, Theme.BorderMidColor);

            var left0 = content.Left + (grid.RowHeadersVisible ? grid.ScaledRowHeadersWidth : 0);
            var leftEnd = left0 + grid.FrozenColumnsWidth;
            var rightStart = content.Right - grid.RightPinnedColumnsWidth;

            // Scrollable filter cells (clipped to the middle band), then pinned cells on top — matches the data rows.
            DrawFilterCells (grid, e, y, h, c => !c.Frozen && !c.PinnedRight,
                new Rectangle (leftEnd, y, Math.Max (0, rightStart - leftEnd), h));
            if (grid.FrozenColumnsWidth > 0)
                DrawFilterCells (grid, e, y, h, c => c.Frozen, new Rectangle (left0, y, grid.FrozenColumnsWidth, h));
            if (grid.RightPinnedColumnsWidth > 0)
                DrawFilterCells (grid, e, y, h, c => c.PinnedRight, new Rectangle (rightStart, y, grid.RightPinnedColumnsWidth, h));
        }

        /// <summary>Where a filtering-row cell's operator label, value and operator button go, in device pixels.</summary>
        internal static (Rectangle Label, Rectangle Value, Rectangle Button) LayoutFilterCell (RadGridView grid, DataGridViewColumn column, Rectangle cell)
        {
            var one = grid.LogicalToDeviceUnits (1);
            var button_width = grid.LogicalToDeviceUnits (FilterButtonLogicalWidth);
            var button = new Rectangle (cell.Right - one - button_width, cell.Top + one, button_width, Math.Max (0, cell.Height - 2 * one));

            var text = new Rectangle (cell.Left + one, cell.Top + one, Math.Max (0, button.Left - grid.LogicalToDeviceUnits (2) - cell.Left - one), button.Height);
            var value = grid.FilterRowValueText (column);

            if (value.Length == 0)
                return (text, Rectangle.Empty, button);

            var (font, size) = grid.CellFallbackFont ();
            var scaled = grid.LogicalToDeviceUnits (size);
            var label_width = (int) Math.Ceiling (TextMeasurer.MeasureText (grid.FilterRowOperatorLabel (column), font, scaled).Width);
            var value_width = (int) Math.Ceiling (TextMeasurer.MeasureText (value, font, scaled).Width);
            label_width = Math.Max (0, Math.Min (label_width, text.Width - value_width));

            return (new Rectangle (text.Left, text.Top, label_width, text.Height),
                    Rectangle.FromLTRB (text.Left + label_width, text.Top, text.Right, text.Bottom), button);
        }

        private static void DrawFilterCells (RadGridView grid, PaintEventArgs e, int y, int h,
            Func<DataGridViewColumn, bool> include, Rectangle clip)
        {
            e.Canvas.Save ();
            e.Canvas.Clip (clip);

            var (font, size) = grid.CellFallbackFont ();
            var scaled = grid.LogicalToDeviceUnits (size);
            var ink = grid.Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;

            for (var i = 0; i < grid.Columns.Count; i++) {
                var column = grid.Columns[i];
                if (!column.Visible || !include (column))
                    continue;

                var x = grid.GetColumnDeviceLeft (i);
                var w = grid.LogicalToDeviceUnits (column.Width);
                var cell = new Rectangle (x, y, w, h);
                e.Canvas.DrawLine (cell.Right - 1, cell.Top, cell.Right - 1, cell.Bottom, Theme.BorderLowColor);

                if (i == grid.FilterEditColumn || !RadGridView.ColumnAllowsFiltering (column))
                    continue;   // editor overlays this cell, or filtering not allowed

                var (label, value, button) = LayoutFilterCell (grid, column, cell);

                // A check-box column filters by a check box, not by an operator and text.
                if (!column.DisplaysAsCheckBox) {
                    e.Canvas.DrawText (grid.FilterRowOperatorLabel (column), font, scaled, label, ink, ContentAlignment.MiddleLeft, maxLines: 1, ellipsis: true);

                    if (!value.IsEmpty) {
                        var align = column.DefaultCellStyleAlignment switch {
                            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => ContentAlignment.MiddleRight,
                            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => ContentAlignment.MiddleCenter,
                            _ => ContentAlignment.MiddleLeft,
                        };
                        e.Canvas.DrawText (grid.FilterRowValueText (column), font, scaled, value, ink, align, maxLines: 1, ellipsis: true);
                    }
                }

                // The operator button's funnel, in the accent colour while the column is filtered, as the header's is.
                var glyph = grid.LogicalToDeviceUnits (12);
                var glyph_rect = new Rectangle (button.Left + (button.Width - glyph) / 2, button.Top + (button.Height - glyph) / 2, glyph, glyph);
                var active = grid.ColumnHasActiveFilter (column);
                DrawFunnel (e, glyph_rect, active ? Theme.AccentColor : Theme.BorderHighColor, active);
            }

            e.Canvas.Restore ();
        }

        /// <inheritdoc/>
        protected override void RenderColumnHeader (DataGridView control, DataGridViewColumn column, int columnIndex, Rectangle bounds, PaintEventArgs e)
        {
            base.RenderColumnHeader (control, column, columnIndex, bounds, e);

            if (control is not RadGridView grid || !grid.ShowsHeaderFilterButtons || !RadGridView.ColumnAllowsFiltering (column) || !FunnelFits (column))
                return;

            // Funnel glyph at the right of the header (left of the sort glyph), recorded for hit-testing.
            var size = control.LogicalToDeviceUnits (12);
            var glyphRect = new Rectangle (bounds.Right - control.LogicalToDeviceUnits (30),
                                           bounds.Top + (bounds.Height - size) / 2, size, size);

            grid.FilterGlyphRects[columnIndex] = glyphRect;

            var active = grid.ColumnHasActiveFilter (column);
            DrawFunnel (e, glyphRect, active ? Theme.AccentColor : Theme.BorderHighColor, active);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Telerik's cells share their lines, measured (UI for WinForms 2026.2.520): each one's edges fall on its
        /// neighbours', so a pixel's line runs between two cells and between two rows, and the first column, with no row
        /// header column beside it, has its own line on its left, a pixel inside the grid's edge. Each cell here drew its
        /// left and top edges beside its neighbours' right and bottom ones, two pixels of line between every pair.
        /// </remarks>
        protected override void RenderCellBorders (DataGridView control, DataGridViewAdvancedBorderStyle borderStyle, Rectangle bounds, PaintEventArgs e)
        {
            if (control is not RadGridView) {
                base.RenderCellBorders (control, borderStyle, bounds, e);
                return;
            }

            var shared = new DataGridViewAdvancedBorderStyle {
                Right = borderStyle.Right,
                Bottom = borderStyle.Bottom,
                Top = DataGridViewAdvancedCellBorderStyle.None,
                Left = !control.RowHeadersVisible && IsFirstColumnAt (control, bounds.Left) ? borderStyle.Right : DataGridViewAdvancedCellBorderStyle.None,
            };

            base.RenderCellBorders (control, shared, bounds, e);
        }

        // Whether a cell starting at a device x is the first visible column's (scrolled or not).
        private static bool IsFirstColumnAt (DataGridView control, int left)
        {
            for (var i = 0; i < control.Columns.Count; i++)
                if (control.Columns[i].Visible)
                    return control.GetColumnDeviceLeft (i) == left;
            return false;
        }

        /// <inheritdoc/>
        protected override int? CellTextMaxLines (DataGridViewColumn column)
            => column is Majorsilence.Forms.Telerik.GridViewColumn { WrapText: true } ? null : 1;

        /// <inheritdoc/>
        protected override int CellLeftInset (DataGridView control, DataGridViewColumn column)
            => control is RadGridView grid && grid.HasChildView && ReferenceEquals (column, LeftmostColumn (control))
                ? control.LogicalToDeviceUnits (18) : 0;

        /// <inheritdoc/>
        protected override int HeaderRightInset (DataGridView control, DataGridViewColumn column)
        {
            if (control is RadGridView grid && grid.ShowsHeaderFilterButtons && RadGridView.ColumnAllowsFiltering (column) && FunnelFits (column))
                return control.LogicalToDeviceUnits (RadGridView.HeaderFunnelRoom);   // keep text left of the funnel (drawn at Right-30)
            if (column.Sortable && column.SortOrder != SortOrder.None)
                return control.LogicalToDeviceUnits (RadGridView.HeaderSortGlyphRoom);   // room for the sort glyph alone
            return 0;
        }

        /// <inheritdoc/>
        /// <remarks>Telerik's header cells end text that does not fit with "...".</remarks>
        protected override bool HeaderTextEllipsis => true;

        // The funnel glyph is suppressed on very narrow columns (it would crowd out the header text).
        private static bool FunnelFits (DataGridViewColumn column) => column.Width >= 60;

        // The physically leftmost visible column (where the master-detail expander is drawn): the first
        // visible frozen column if any, otherwise the first visible column.
        private static DataGridViewColumn? LeftmostColumn (DataGridView control)
        {
            DataGridViewColumn? firstVisible = null;
            foreach (DataGridViewColumn c in control.Columns) {
                if (!c.Visible)
                    continue;
                firstVisible ??= c;
                if (c.Frozen)
                    return c;
            }
            return firstVisible;
        }

        /// <inheritdoc/>
        protected override void RenderRow (DataGridView control, DataGridViewRow row, int rowIndex, Rectangle bounds, PaintEventArgs e)
        {
            if (row.Tag is GridGroupRow group) {
                RenderGroupRow (control, group, bounds, e);
                return;
            }

            if (row.Tag is GridSummaryRow summary) {
                RenderSummaryRow (control, summary, bounds, e);
                return;
            }

            if (row.Tag is GridDetailRow detail) {
                RenderDetailRow (control, detail, bounds, e);
                return;
            }

            if (row.Tag is GridNewRow) {
                RenderNewRow (control, bounds, e);
                return;
            }

            base.RenderRow (control, row, rowIndex, bounds, e);

            // Master-detail expander glyph at the left of a data row.
            if (control is RadGridView mdGrid && mdGrid.HasChildView)
                RenderExpander (control, bounds, mdGrid.IsRowExpanded (row), e);

            // Validation error indicator: a red bar at the row's left edge.
            if (!string.IsNullOrEmpty (row.ErrorText)) {
                var w = control.LogicalToDeviceUnits (3);
                e.Canvas.FillRectangle (new Rectangle (bounds.Left, bounds.Top, w, bounds.Height), new SKColor (200, 0, 0));
            }
        }

        private static void RenderNewRow (DataGridView control, Rectangle bounds, PaintEventArgs e)
        {
            e.Canvas.FillRectangle (bounds, Theme.ControlLowColor);
            e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, Theme.BorderLowColor);

            var rect = bounds;
            rect.Inflate (-control.LogicalToDeviceUnits (8), 0);
            e.Canvas.DrawText ("Click here to add a new row", Theme.UIFont, control.LogicalToDeviceUnits (Theme.ItemFontSize),
                rect, Theme.ForegroundDisabledColor, ContentAlignment.MiddleLeft, maxLines: 1);
        }

        // ── Master-detail ──

        private static void RenderExpander (DataGridView control, Rectangle bounds, bool expanded, PaintEventArgs e)
        {
            var size = control.LogicalToDeviceUnits (9);
            var left = bounds.Left + (control.RowHeadersVisible ? control.ScaledRowHeadersWidth : 0) + control.LogicalToDeviceUnits (4);
            var box = new Rectangle (left, bounds.Top + (bounds.Height - size) / 2, size, size);

            e.Canvas.DrawRectangle (box, Theme.BorderHighColor);
            // horizontal stroke (always) + vertical stroke (when collapsed) → +/- glyph
            var cy = box.Top + box.Height / 2;
            e.Canvas.DrawLine (box.Left + 2, cy, box.Right - 2, cy, Theme.ForegroundColor);
            if (!expanded) {
                var cx = box.Left + box.Width / 2;
                e.Canvas.DrawLine (cx, box.Top + 2, cx, box.Bottom - 2, Theme.ForegroundColor);
            }
        }

        private static void RenderDetailRow (DataGridView control, GridDetailRow detail, Rectangle bounds, PaintEventArgs e)
        {
            var child = detail.Child;
            e.Canvas.FillRectangle (bounds, Theme.ControlLowColor);

            var indent = control.LogicalToDeviceUnits (24);
            var area = new Rectangle (bounds.Left + indent, bounds.Top + control.LogicalToDeviceUnits (4),
                                      Math.Max (0, bounds.Width - indent - control.LogicalToDeviceUnits (8)),
                                      bounds.Height - control.LogicalToDeviceUnits (8));

            e.Canvas.DrawRectangle (area, Theme.BorderMidColor);

            if (child.Columns.Count == 0)
                return;

            var fontSize = control.LogicalToDeviceUnits (Theme.ItemFontSize);
            var headerH = control.LogicalToDeviceUnits (22);
            var rowH = control.LogicalToDeviceUnits (22);
            var colW = area.Width / child.Columns.Count;

            e.Canvas.Save ();
            e.Canvas.Clip (area);

            // Child header row.
            var headerRect = new Rectangle (area.Left, area.Top, area.Width, headerH);
            e.Canvas.FillRectangle (headerRect, Theme.ControlMidColor);
            for (var c = 0; c < child.Columns.Count; c++) {
                var cell = new Rectangle (area.Left + c * colW, area.Top, colW, headerH);
                cell.Inflate (-4, 0);
                e.Canvas.DrawText (child.Columns[c], Theme.UIFontBold, fontSize, cell, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
            }
            e.Canvas.DrawLine (area.Left, area.Top + headerH, area.Right, area.Top + headerH, Theme.BorderLowColor);

            // Child data rows.
            var y = area.Top + headerH;
            foreach (var childRow in child.Rows) {
                for (var c = 0; c < child.Columns.Count; c++) {
                    var text = c < childRow.Length ? childRow[c] : string.Empty;
                    var cell = new Rectangle (area.Left + c * colW, y, colW, rowH);
                    cell.Inflate (-4, 0);
                    e.Canvas.DrawText (text, Theme.UIFont, fontSize, cell, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
                }
                y += rowH;
            }

            e.Canvas.Restore ();
        }

        // ── Summary (aggregate) row ──

        private static void RenderSummaryRow (DataGridView control, GridSummaryRow summary, Rectangle bounds, PaintEventArgs e)
        {
            e.Canvas.FillRectangle (bounds, Theme.ControlMidHighColor);
            e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Right, bounds.Top, Theme.BorderMidColor);
            e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, Theme.BorderMidColor);

            var fontSize = control.LogicalToDeviceUnits (Theme.ItemFontSize);

            for (var i = 0; i < control.Columns.Count; i++) {
                var column = control.Columns[i];
                if (!column.Visible)
                    continue;

                if (summary.Values.TryGetValue (i, out var text) && !string.IsNullOrEmpty (text)) {
                    var colWidth = control.LogicalToDeviceUnits (column.Width);
                    var textRect = new Rectangle (control.GetColumnDeviceLeft (i), bounds.Top, colWidth, bounds.Height);
                    textRect.Inflate (-4, 0);
                    e.Canvas.DrawText (text, Theme.UIFontBold, fontSize, textRect, Theme.ForegroundColor, column.DefaultCellStyleAlignment, maxLines: 1);
                }
            }
        }

        // ── Group-header row ──

        private static void RenderGroupRow (DataGridView control, GridGroupRow group, Rectangle bounds, PaintEventArgs e)
        {
            e.Canvas.FillRectangle (bounds, Theme.ControlMidColor);
            e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, Theme.BorderMidColor);

            var indent = control.LogicalToDeviceUnits (8 + group.Level * 18);
            var glyphSize = control.LogicalToDeviceUnits (8);
            var glyphX = bounds.Left + indent;
            var glyphY = bounds.Top + (bounds.Height - glyphSize) / 2;

            // Expander triangle: right when collapsed, down when expanded.
            using (var path = new SKPath ()) {
                if (group.Collapsed) {
                    path.MoveTo (glyphX, glyphY);
                    path.LineTo (glyphX + glyphSize, glyphY + glyphSize / 2);
                    path.LineTo (glyphX, glyphY + glyphSize);
                } else {
                    path.MoveTo (glyphX, glyphY);
                    path.LineTo (glyphX + glyphSize, glyphY);
                    path.LineTo (glyphX + glyphSize / 2, glyphY + glyphSize);
                }
                path.Close ();
                using var paint = new SKPaint { Color = Theme.ForegroundColor, IsAntialias = true };
                e.Canvas.DrawPath (path, paint);
            }

            var textLeft = glyphX + glyphSize + control.LogicalToDeviceUnits (8);
            var textRect = new Rectangle (textLeft, bounds.Top, bounds.Right - textLeft - 4, bounds.Height);
            var fontSize = control.LogicalToDeviceUnits (Theme.ItemFontSize);
            var label = group.HeaderText;

            e.Canvas.DrawText (label, Theme.UIFontBold, fontSize, textRect, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
        }

        // ── Group panel band ──

        private static void RenderGroupPanel (RadGridView grid, PaintEventArgs e)
        {
            var client = grid.DeviceClientRectangle;
            var height = grid.GroupPanelBandHeight;
            var band = new Rectangle (client.Left, client.Top, client.Width, height);

            e.Canvas.FillRectangle (band, Theme.ControlLowColor);
            e.Canvas.DrawLine (band.Left, band.Bottom - 1, band.Right, band.Bottom - 1, Theme.BorderMidColor);

            var fontSize = grid.LogicalToDeviceUnits (Theme.ItemFontSize);

            if (grid.GroupDescriptors.Count == 0) {
                var hintRect = band;
                hintRect.Inflate (-grid.LogicalToDeviceUnits (10), 0);
                e.Canvas.DrawText ("Drag a column here to group by this column.",
                    Theme.UIFont, fontSize, hintRect, Theme.ForegroundDisabledColor, ContentAlignment.MiddleLeft, maxLines: 1);
                return;
            }

            var pad = grid.LogicalToDeviceUnits (6);
            var x = band.Left + pad;
            var pillHeight = height - grid.LogicalToDeviceUnits (12);
            var pillY = band.Top + (height - pillHeight) / 2;
            var closeSize = grid.LogicalToDeviceUnits (12);

            foreach (var descriptor in grid.GroupDescriptors) {
                var arrow = descriptor.Direction == System.ComponentModel.ListSortDirection.Ascending ? " ▲" : " ▼";
                var header = HeaderFor (grid, descriptor.PropertyName);
                var text = header + arrow;
                var textWidth = (int)TextMeasurer.MeasureText (text, Theme.UIFontBold, Theme.ItemFontSize).Width;

                var pillWidth = textWidth + closeSize + grid.LogicalToDeviceUnits (24);
                var pill = new Rectangle (x, pillY, pillWidth, pillHeight);

                using (var paint = new SKPaint { Color = Theme.AccentColor, IsAntialias = true })
                    e.Canvas.DrawRoundRect (new SKRect (pill.Left, pill.Top, pill.Right, pill.Bottom),
                        grid.LogicalToDeviceUnits (4), grid.LogicalToDeviceUnits (4), paint);

                var textRect = new Rectangle (pill.Left + grid.LogicalToDeviceUnits (8), pill.Top,
                    textWidth + grid.LogicalToDeviceUnits (4), pill.Height);
                e.Canvas.DrawText (text, Theme.UIFontBold, fontSize, textRect, Theme.ForegroundColorOnAccent, ContentAlignment.MiddleLeft, maxLines: 1);

                var closeRect = new Rectangle (pill.Right - closeSize - grid.LogicalToDeviceUnits (6),
                    pill.Top + (pill.Height - closeSize) / 2, closeSize, closeSize);
                e.Canvas.DrawText ("✕", Theme.UIFont, fontSize, closeRect, Theme.ForegroundColorOnAccent, ContentAlignment.MiddleCenter, maxLines: 1);

                grid.GroupPillLayouts.Add (new GroupPillLayout { Descriptor = descriptor, Bounds = pill, CloseBounds = closeRect });

                x += pillWidth + pad;
            }
        }

        private static string HeaderFor (RadGridView grid, string name)
        {
            var idx = grid.ColumnIndexByName (name);
            if (idx < 0 || idx >= grid.Columns.Count)
                return name;
            var col = grid.Columns[idx];
            return string.IsNullOrEmpty (col.HeaderText) ? col.Name : col.HeaderText;
        }

        // ── Drag indicator ──

        private static void RenderDragIndicator (RadGridView grid, PaintEventArgs e)
        {
            // Insertion line between columns when reordering.
            if (!grid.DragOverGroupPanel && grid.DragTargetColumn >= 0 && grid.DragTargetColumn < grid.Columns.Count) {
                var target = grid.Columns[grid.DragTargetColumn];
                if (!target.HeaderBounds.IsEmpty) {
                    var lineX = target.HeaderBounds.Left;
                    e.Canvas.DrawLine (lineX, target.HeaderBounds.Top, lineX, grid.DeviceClientRectangle.Bottom, Theme.AccentColor, 2);
                }
            }

            // Highlight the group panel when a header is dragged over it.
            if (grid.DragOverGroupPanel) {
                var band = new Rectangle (grid.DeviceClientRectangle.Left, grid.DeviceClientRectangle.Top, grid.DeviceClientRectangle.Width, grid.GroupPanelBandHeight);
                e.Canvas.DrawRectangle (band, Theme.AccentColor, 2);
            }

            // Floating label of the column being dragged.
            if (grid.DragColumn >= 0 && grid.DragColumn < grid.Columns.Count) {
                var text = grid.Columns[grid.DragColumn].HeaderText ?? string.Empty;
                var fontSize = grid.LogicalToDeviceUnits (Theme.ItemFontSize);
                var width = (int)TextMeasurer.MeasureText (text, Theme.UIFontBold, Theme.ItemFontSize).Width + grid.LogicalToDeviceUnits (16);
                var rect = new Rectangle (grid.DragLocation.X + grid.LogicalToDeviceUnits (8),
                                          grid.DragLocation.Y - grid.LogicalToDeviceUnits (10),
                                          width, grid.LogicalToDeviceUnits (22));

                using (var paint = new SKPaint { Color = Theme.ControlVeryHighColor.WithAlpha (230), IsAntialias = true })
                    e.Canvas.DrawRoundRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom),
                        grid.LogicalToDeviceUnits (3), grid.LogicalToDeviceUnits (3), paint);
                e.Canvas.DrawRectangle (rect, Theme.AccentColor, 1);
                e.Canvas.DrawText (text, Theme.UIFontBold, fontSize, rect, Theme.ForegroundColor, ContentAlignment.MiddleCenter, maxLines: 1);
            }
        }

        // Small funnel/filter glyph.
        private static void DrawFunnel (PaintEventArgs e, Rectangle r, SKColor color, bool filled)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true, IsStroke = !filled, StrokeWidth = 1 };
            using var path = new SKPath ();
            path.MoveTo (r.Left, r.Top);
            path.LineTo (r.Right, r.Top);
            path.LineTo (r.Left + r.Width * 0.6f, r.Top + r.Height * 0.5f);
            path.LineTo (r.Left + r.Width * 0.6f, r.Bottom);
            path.LineTo (r.Left + r.Width * 0.4f, r.Bottom - r.Height * 0.2f);
            path.LineTo (r.Left + r.Width * 0.4f, r.Top + r.Height * 0.5f);
            path.Close ();
            e.Canvas.DrawPath (path, paint);
        }
    }
}
