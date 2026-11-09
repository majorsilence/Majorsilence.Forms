using System;
using System.Drawing;
using Majorsilence.Forms.Telerik;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    // Drawing what a cell formatting handler set on a cell element: a header cell's fill, font, colour, padding and
    // border, a data cell's fill over the selection and pointer, and either cell's border in place of the grid lines.
    // What was measured is in RadGridViewCellFormatting.cs.
    public partial class RadGridViewRenderer
    {
        // The border of the data cell being painted, for RenderCellBorders (which is not told which cell it is).
        [ThreadStatic]
        private static RadGridView.FormattedBorder? painting_border;

        /// <inheritdoc/>
        protected override void RenderCell (DataGridView control, DataGridViewColumn column, string value, int rowIndex, int columnIndex, Rectangle bounds, ControlStyle? cellStyle, PaintEventArgs e, DataGridViewPaintParts paintParts)
        {
            var state = control is RadGridView && rowIndex >= 0 && rowIndex < control.Rows.Count && columnIndex >= 0 && columnIndex < control.Rows[rowIndex].Cells.Count
                ? RadGridView.FormattingOf (control.Rows[rowIndex].Cells[columnIndex])
                : null;

            // A cell that draws its fill covers the row's selection and pointer highlight, which the base leaves showing.
            if (state?.Fill is { } fill && paintParts.HasFlag (DataGridViewPaintParts.Background))
                e.Canvas.FillRectangle (bounds, fill.ToSKColor ());

            painting_border = state?.Border;
            try {
                base.RenderCell (control, column, value, rowIndex, columnIndex, bounds, cellStyle, e, paintParts);
            } finally {
                painting_border = null;
            }
        }

        // The row's bottom grid line is drawn across it after its cells. A cell with a formatted border has its own
        // bottom edge instead (or none), so its fill and border are put back over that line.
        private static void RestoreFormattedRowEdge (DataGridView control, DataGridViewRow row, PaintEventArgs e)
        {
            if (control is not RadGridView)
                return;

            var content = control.GetContentArea ();
            var left0 = content.Left + (control.RowHeadersVisible ? control.ScaledRowHeadersWidth : 0);
            var left_end = left0 + control.FrozenColumnsWidth;
            var right_start = content.Right - control.RightPinnedColumnsWidth;
            var background = control.DefaultCellStyle.BackgroundColor ?? control.GetEffectiveBackgroundColor ();

            for (var i = 0; i < row.Cells.Count && i < control.Columns.Count; i++) {
                var cell = row.Cells[i];
                var column = control.Columns[i];
                if (!column.Visible || RadGridView.FormattingOf (cell) is not { Border: { } border } state)
                    continue;

                var bounds = cell.Bounds;
                var edge = new Rectangle (bounds.Left, bounds.Bottom - 1, bounds.Width, 1);
                var band = column.Frozen ? new Rectangle (left0, bounds.Top, control.FrozenColumnsWidth, bounds.Height)
                    : column.PinnedRight ? new Rectangle (right_start, bounds.Top, control.RightPinnedColumnsWidth, bounds.Height)
                    : new Rectangle (left_end, bounds.Top, Math.Max (0, right_start - left_end), bounds.Height);

                e.Canvas.Save ();
                e.Canvas.Clip (Rectangle.Intersect (band, edge));
                e.Canvas.FillRectangle (edge, state.Fill is { } fill ? fill.ToSKColor () : background);
                RenderFormattedBorder (control, border, bounds, e);
                e.Canvas.Restore ();
            }
        }

        // A formatted border: each side the width it was given (scaled), inside the cell, in its colour or the grid's
        // line colour when it has none.
        private static void RenderFormattedBorder (DataGridView control, RadGridView.FormattedBorder border, Rectangle bounds, PaintEventArgs e)
        {
            var line = Theme.BorderLowColor;

            void Side (float width, Color colour, Func<int, Rectangle> rect)
            {
                if (width <= 0)
                    return;
                var device = Math.Max (1, control.LogicalToDeviceUnits ((int) Math.Round (width)));
                e.Canvas.FillRectangle (rect (device), colour.IsEmpty ? line : colour.ToSKColor ());
            }

            Side (border.Left, border.LeftColor, w => new Rectangle (bounds.Left, bounds.Top, w, bounds.Height));
            Side (border.Top, border.TopColor, w => new Rectangle (bounds.Left, bounds.Top, bounds.Width, w));
            Side (border.Right, border.RightColor, w => new Rectangle (bounds.Right - w, bounds.Top, w, bounds.Height));
            Side (border.Bottom, border.BottomColor, w => new Rectangle (bounds.Left, bounds.Bottom - w, bounds.Width, w));
        }

        // A column header as its ViewCellFormatting handler left its element: the fill, then the caption in the element's
        // font and colour inside its border and padding, the sort glyph, and the border (or the header's own line).
        private void RenderFormattedColumnHeader (RadGridView grid, DataGridViewColumn column, int columnIndex, Rectangle bounds, GridHeaderCellElement element, PaintEventArgs e)
        {
            grid.FormattedHeaders[columnIndex] = element;

            if (element.DrawFill && !element.BackColor.IsEmpty)
                e.Canvas.FillRectangle (bounds, element.BackColor.ToSKColor ());

            var fallback = grid.HeaderFallbackFont ();
            var own_font = element.FontChanged ? element.Font : null;
            var font = own_font is null ? grid.ColumnHeadersDefaultCellStyle.Font ?? fallback.Typeface : TypefaceCache.Resolve (own_font);
            var font_size = own_font is null ? grid.ColumnHeadersDefaultCellStyle.FontSize ?? fallback.Size
                : (int) Math.Round (own_font.SizeInPoints * 96f / 72f);
            var fg = !element.ForeColor.IsEmpty ? element.ForeColor.ToSKColor ()
                : grid.ColumnHeadersDefaultCellStyle.ForegroundColor ?? DataGridView.DefaultColumnHeaderStyle.GetForegroundColor ();

            // The caption's box: the header less 6px a side, as an unformatted header's, moved in by however much the
            // element's border and padding exceed an unformatted header's 1px border; then the room the funnel or sort
            // glyph keeps on the right.
            var (bl, bt, br, bb) = element.BorderThickness;
            var p = element.Padding;
            int Inset (float border, int padding) => grid.LogicalToDeviceUnits ((int) Math.Round (padding + border - RadGridView.HeaderCellBorderWidth));
            var side = grid.LogicalToDeviceUnits (6);
            var left = bounds.Left + side + Inset (bl, p.Left);
            var right = bounds.Right - side - Inset (br, p.Right) - HeaderRightInset (grid, column);
            var top = bounds.Top + Inset (bt, p.Top);
            var bottom = bounds.Bottom - Inset (bb, p.Bottom);
            var text_bounds = new Rectangle (left, top, Math.Max (0, right - left), Math.Max (0, bottom - top));

            e.Canvas.DrawText (element.Text, font, grid.LogicalToDeviceUnits (font_size), text_bounds, fg, element.TextAlignment, maxLines: 1, ellipsis: true);

            if (column.SortOrder != SortOrder.None)
                RenderSortGlyph (e, bounds, column.SortOrder, fg);

            if (RadGridView.FormattedBorder.Of (element) is { } border)
                RenderFormattedBorder (grid, border, bounds, e);
            else
                e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom, Theme.BorderLowColor);
        }

        /// <inheritdoc/>
        /// <remarks>The header band's bottom line is drawn over the headers; a formatted header's own bottom edge (its fill
        /// and border) is put back over it, as Telerik draws the header cell's border and no band line.</remarks>
        protected override void RenderColumnHeaders (DataGridView control, PaintEventArgs e, Rectangle contentArea)
        {
            base.RenderColumnHeaders (control, e, contentArea);

            if (control is not RadGridView grid || grid.FormattedHeaders.Count == 0)
                return;

            var row_header = control.RowHeadersVisible ? control.ScaledRowHeadersWidth : 0;
            var left0 = contentArea.Left + row_header;
            var left_end = left0 + control.FrozenColumnsWidth;
            var right_start = contentArea.Right - control.RightPinnedColumnsWidth;

            foreach (var (index, element) in grid.FormattedHeaders) {
                var filled = element.DrawFill && !element.BackColor.IsEmpty;
                var border = RadGridView.FormattedBorder.Of (element);
                if (!filled && border is null)
                    continue;   // the band's line is the header's own

                var column = control.Columns[index];
                var bounds = column.HeaderBounds;
                var edge = new Rectangle (bounds.Left, bounds.Bottom - 1, bounds.Width, 1);

                // The band the header is drawn in: pinned left, pinned right or scrolling between them.
                var band = column.Frozen ? new Rectangle (left0, bounds.Top, control.FrozenColumnsWidth, bounds.Height)
                    : column.PinnedRight ? new Rectangle (right_start, bounds.Top, control.RightPinnedColumnsWidth, bounds.Height)
                    : new Rectangle (left_end, bounds.Top, Math.Max (0, right_start - left_end), bounds.Height);

                e.Canvas.Save ();
                e.Canvas.Clip (Rectangle.Intersect (band, edge));

                var fill = filled ? element.BackColor.ToSKColor ()
                    : control.ColumnHeadersDefaultCellStyle.BackgroundColor ?? DataGridView.DefaultColumnHeaderStyle.GetBackgroundColor ();
                e.Canvas.FillRectangle (edge, fill);

                if (border is not null)
                    RenderFormattedBorder (control, border, bounds, e);

                e.Canvas.Restore ();
            }
        }
    }
}
