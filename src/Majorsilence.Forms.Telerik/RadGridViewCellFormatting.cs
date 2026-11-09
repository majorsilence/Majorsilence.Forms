using System.Drawing;
using System.Runtime.CompilerServices;

namespace Majorsilence.Forms.Telerik
{
    // What a cell formatting handler sets on a cell element beyond its colours and text: its font, its padding, its fill
    // over the selection and its border, and the row and header heights AutoSizeRows takes from them.
    //
    // Measured on UI for WinForms 2026.2.520 (probes/wfcellfmt), a grid in Segoe UI 12pt whose ViewCellFormatting sets
    // the header cells to a white fill, bold font, padding (8,9,8,9) and a 1px bottom-only border, and the data cells to
    // padding (8,7,8,7) and a 1px bottom-only border:
    // - ViewCellFormatting is raised for header cells (GridHeaderCellElement, row GridViewTableHeaderRowInfo, row
    //   index -1) as well as data cells (GridDataCellElement). CellFormatting is raised for data cells only.
    // - Every element starts with the grid's font. A data cell's border is four 2px sides, a header cell's four 1px ones.
    // - With AutoSizeRows a row is as tall as a line of its tallest cell's font, plus that cell's top and bottom padding
    //   and border, plus 1: 38 for the data rows above (22 + 14 + 1 + 1), 42 for the header (22 + 18 + 1 + 1), and 27
    //   and 25 unformatted (22 + 4 + 1, 22 + 2 + 1), as AutoSizeRows already sized them.
    // - The text sits inside the border and padding: a cell's text starts its left border and padding in, 2px less
    //   than that further than an unformatted cell's (whose border is 2).
    // - A cell that draws its fill keeps it on the selected row: the handler's colour, not the selection's.
    // - The border replaces the cell's grid lines: with only the bottom side, no line runs between the columns.
    public partial class RadGridView
    {
        // A data cell element's border width before a handler changes it, and a header cell element's (measured).
        internal const float DataCellBorderWidth = 2, HeaderCellBorderWidth = 1;

        // What the last formatting pass applied to a cell, so the next one can take back what its handler no longer sets.
        internal sealed class FormattedCell
        {
            public bool FontApplied;
            public bool PaddingApplied;
            public Color? Fill;
            public FormattedBorder? Border;
        }

        // A formatted element's border, in logical pixels.
        internal sealed record FormattedBorder (float Left, float Top, float Right, float Bottom, Color LeftColor, Color TopColor, Color RightColor, Color BottomColor)
        {
            internal static FormattedBorder? Of (LightVisualElement element)
            {
                if (!element.BordersChanged)
                    return null;

                var (l, t, r, b) = element.BorderThickness;
                var (lc, tc, rc, bc) = element.BorderColors;
                Color Or (Color side) => side.IsEmpty ? element.BorderColor : side;
                return new FormattedBorder (l, t, r, b, Or (lc), Or (tc), Or (rc), Or (bc));
            }
        }

        private static readonly ConditionalWeakTable<DataGridViewCell, FormattedCell> formatted_cells = new ();

        // The heights AutoSizeRows wants for the rows and header painted this frame, from what their handlers set.
        private readonly System.Collections.Generic.Dictionary<DataGridViewRow, int> formatted_row_heights = new ();
        private int formatted_header_height;

        // The column headers formatted this frame, by column index; settled after the frame.
        internal System.Collections.Generic.Dictionary<int, GridHeaderCellElement> FormattedHeaders { get; } = new ();

        private GridViewTableHeaderRowInfo? header_row_info;

        internal static FormattedCell? FormattingOf (DataGridViewCell cell) => formatted_cells.TryGetValue (cell, out var state) ? state : null;

        // Gives a new cell element what Telerik's starts with: the grid's font and its border.
        internal void PrepareCellElement (GridViewCellElement element, float borderWidth)
        {
            element.Font = Font;
            element.DrawBorder = true;
            element.SetDefaultBorder (BorderBoxStyle.FourBorders, borderWidth);
        }

        // Applies to a data cell what its formatting handlers set on its element, after they have run.
        internal void ApplyFormattedElement (DataGridViewRow row, DataGridViewCell cell, GridViewCellElement element, Majorsilence.Forms.Drawing.Font initialFont)
        {
            var state = formatted_cells.GetValue (cell, _ => new FormattedCell ());

            var font_changed = element.Font is not null && !ReferenceEquals (element.Font, initialFont);
            if (font_changed) {
                cell.Style.Font = TypefaceCache.Resolve (element.Font);
                cell.Style.FontSize = (int) System.Math.Round (element.Font!.SizeInPoints * 96f / 72f);
                state.FontApplied = true;
            } else if (state.FontApplied) {
                cell.Style.Font = null;
                cell.Style.FontSize = null;
                state.FontApplied = false;
                RadCellStyle.Existing (cell)?.ApplyTo (cell);
            }

            // The text sits inside the element's border and padding. The renderer insets an unformatted cell's text by
            // what its 2px border does, so only the difference is padding here; in device pixels, as the cell style's is.
            if (element.Padding != Padding.Empty || element.BordersChanged) {
                var (l, t, r, b) = element.BorderThickness;
                var p = element.Padding;
                cell.Style.Padding = new Padding (
                    LogicalToDeviceUnits ((int) System.Math.Round (p.Left + l - DataCellBorderWidth)),
                    LogicalToDeviceUnits ((int) System.Math.Round (p.Top + t - DataCellBorderWidth)),
                    LogicalToDeviceUnits ((int) System.Math.Round (p.Right + r - DataCellBorderWidth)),
                    LogicalToDeviceUnits ((int) System.Math.Round (p.Bottom + b - DataCellBorderWidth)));
                state.PaddingApplied = true;
            } else if (state.PaddingApplied) {
                cell.Style.Padding = Padding.Empty;
                state.PaddingApplied = false;
            }

            // A cell that draws its fill keeps it, and its text colour, when its row is selected or under the pointer.
            state.Fill = element.DrawFill && !element.BackColor.IsEmpty ? element.BackColor : null;
            if (state.Fill is { } fill)
                cell.Style.SelectionBackColor = fill;
            if (!element.ForeColor.IsEmpty)
                cell.Style.SelectionForeColor = element.ForeColor;

            state.Border = FormattedBorder.Of (element);

            if (AutoSizeRows && !GridViewRowInfo.HasOwnHeight (row)) {
                var height = AutoSizeHeight (element, font_changed ? element.Font : null, CellFallbackFont ());
                formatted_row_heights[row] = formatted_row_heights.TryGetValue (row, out var tallest) ? System.Math.Max (tallest, height) : height;
            }
        }

        // The height AutoSizeRows gives a row or header for an element: a line of its font, its top and bottom padding
        // and border, and 1.
        private static int AutoSizeHeight (LightVisualElement element, Majorsilence.Forms.Drawing.Font? font, (SkiaSharp.SKTypeface Typeface, int Size) fallback)
        {
            var (_, top, _, bottom) = element.BorderThickness;
            var line = font is not null ? font.Height : FontHeight (fallback);
            return line + element.Padding.Vertical + (int) System.Math.Round (top + bottom) + 1;
        }

        /// <summary>Raises <see cref="ViewCellFormatting"/> for a column header cell, as Telerik's grid does.</summary>
        /// <returns>The element, when the handlers changed how the header is drawn; null otherwise.</returns>
        internal GridHeaderCellElement? FormatHeaderCell (DataGridViewColumn column, int columnIndex)
        {
            if (_viewCellFormatting is null)
                return null;

            var caption = column.HeaderDisplayText;
            var element = new GridHeaderCellElement {
                Text = caption,
                RowIndex = -1,
                ColumnIndex = columnIndex,
                ColumnInfo = column as GridViewDataColumn,
                RowInfo = header_row_info ??= new GridViewTableHeaderRowInfo (new DataGridViewRow ()),
                TextAlignment = column.HeaderAlignment,
            };
            PrepareCellElement (element, HeaderCellBorderWidth);
            var initial_font = element.Font!;

            _viewCellFormatting.Invoke (this, new GridViewCellFormattingEventArgs {
                CellElement = element, RowIndex = -1, ColumnIndex = columnIndex, Row = element.RowInfo, Column = column
            });

            element.FontChanged = element.Font is not null && !ReferenceEquals (element.Font, initial_font);
            var changed = (element.DrawFill && !element.BackColor.IsEmpty) || !element.ForeColor.IsEmpty
                || element.FontChanged || element.Padding != Padding.Empty
                || element.TextAlignment != column.HeaderAlignment || element.BordersChanged
                || !string.Equals (element.Text, caption, StringComparison.Ordinal);

            if (AutoSizeRows && (element.Padding != Padding.Empty || element.BordersChanged || element.FontChanged)) {
                var font = element.FontChanged ? element.Font : null;
                formatted_header_height = System.Math.Max (formatted_header_height, AutoSizeHeight (element, font, HeaderFallbackFont ()));
            }

            return changed ? element : null;
        }

        // Called once a frame is painted: sizes the rows and header to what their formatted cells want, as Telerik's
        // AutoSizeRows does. Rows that all want one height take it as the grid's row height, so rows not yet painted
        // (scrolled out of view) are drawn at it too.
        internal void SettleFormattedHeights ()
        {
            if (AutoSizeRows && formatted_row_heights.Count > 0) {
                int? common = null;
                foreach (var height in formatted_row_heights.Values) {
                    if (common is null)
                        common = height;
                    else if (common != height) {
                        common = -1;
                        break;
                    }
                }

                if (common > 0) {
                    if (RowTemplate.Height != common || AnyRowHeightDiffers (common.Value))
                        ApplyRowHeight (common.Value);
                } else {
                    foreach (var (row, height) in formatted_row_heights)
                        if (row.Height != height)
                            row.Height = height;
                }
            }

            if (AutoSizeRows && formatted_header_height > 0 && base.ColumnHeadersHeight != formatted_header_height)
                base.ColumnHeadersHeight = formatted_header_height;

            formatted_row_heights.Clear ();
            formatted_header_height = 0;
        }

        private bool AnyRowHeightDiffers (int height)
        {
            foreach (DataGridViewRow row in base.Rows)
                if (!GridViewRowInfo.HasOwnHeight (row) && row.Height != height)
                    return true;
            return false;
        }
    }

    /// <summary>Telerik-compat row info of the column header row: the <see cref="GridViewCellElement.RowInfo"/> of a header cell's element.</summary>
    public class GridViewTableHeaderRowInfo : GridViewRowInfo
    {
        internal GridViewTableHeaderRowInfo (DataGridViewRow row) : base (row) { }
    }
}
