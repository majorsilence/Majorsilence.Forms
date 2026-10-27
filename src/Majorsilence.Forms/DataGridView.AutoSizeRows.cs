using System.ComponentModel;

namespace Majorsilence.Forms
{
    // AutoSizeRowsMode, as upstream applies it (DataGridView.Methods.cs: AdjustShrinkingRows, AutoResizeRowInternal,
    // OnAddedRow_PostNotification, OnCellCommonChange, OnColumnGlobalAutoSize, PaintRows). The property was stored and
    // raised its event, and rows never sized themselves: a Fill grid with wrapping descriptions showed every row one line
    // tall and cut the text off.
    //
    // Measured on WinForms (.NET 10), a 1334x450 Fill grid, ReadOnly, DefaultCellStyle.WrapMode True, bound to four rows of
    // ~130-character descriptions, Microsoft Sans Serif 12 (RowTemplate.Height 25):
    //   - every non-None mode sizes the rows on bind; the cell modes to their wrapped text (63 = three 20px lines plus 3 in
    //     430px columns), the header modes to the empty row header (17). None leaves them at 25.
    //   - a row's height is what its content needs, larger or smaller than RowTemplate.Height: short text gives 23 (24 with
    //     WrapMode False), a template of 40 still gives 23, and only MinimumHeight floors it (30 gives 30).
    //   - narrowing the grid to 900 grows them to 83, widening to 1800 shrinks them to 43, and a fixed-width Description
    //     column at 200 gives 123 and at 1000 gives 23: a width change re-sizes them both ways. The header modes ignore it.
    //   - a value change re-sizes its row (RowHeightChanged before CellValueChanged), an added row is sized as it arrives
    //     (after RowsAdded), and WrapMode True to False re-sizes every row to one line.
    //   - Height assignments are ignored while a mode is set (no RowHeightChanged); the value is kept and given back when the
    //     mode returns to None -- as are the heights the rows had when the mode was switched on, unless the rows have been
    //     painted since, painting re-sizes a shown row and records the result as the height to give back.
    //   - the Displayed* modes size only the rows on screen: binding 40 rows sized rows 0-7 and left the rest at the first
    //     row's height (upstream builds bound rows as copies of the first), scrolling to row 25 sized rows 22-28 as they
    //     were painted, and narrowing the grid then re-sized only those. The All* modes size every row.
    //   - mode changes raise RowHeightChanged for each row before AutoSizeRowsModeChanged.
    public partial class DataGridView
    {
        [Flags]
        private enum AutoSizeRowsFlags
        {
            None = 0,
            Header = 0x01,
            AllColumns = 0x02,
            AllRows = 0x04,
            DisplayedRows = 0x08,
        }

        private static AutoSizeRowsFlags RowsFlags (DataGridViewAutoSizeRowsMode mode) => (AutoSizeRowsFlags) (int) mode;

        // The upper bound upstream puts on any band (DataGridViewBand.MaxBandThickness).
        private const int MaxRowHeight = 65536;

        // Non-zero while this file is sizing rows: a height it sets must not start another pass, and the per-row layout is
        // done once at the end of a batch.
        private int auto_size_rows_count;

        // The column widths rows were last sized against, so a layout pass re-sizes them only when a width moved.
        private int auto_rows_width_key;

        internal bool InRowAutoSizeBatch => auto_size_rows_count > 0;

        // Whether the mode sizes rows to their data cells, which is what a width or value change can alter.
        private bool AutoSizesRowCells => (RowsFlags (auto_size_rows_mode) & AutoSizeRowsFlags.AllColumns) != 0;

        // AdjustShrinkingRows' own test: something to measure -- the cells, or visible row headers.
        private bool RowsAutoSizable (DataGridViewAutoSizeRowsMode mode)
        {
            var flags = RowsFlags (mode);
            return (flags & AutoSizeRowsFlags.AllColumns) != 0 || ((flags & AutoSizeRowsFlags.Header) != 0 && RowHeadersVisible);
        }

        private static DataGridViewAutoSizeRowMode RowModeOf (DataGridViewAutoSizeRowsMode mode)
            => mode switch {
                DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders or DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders => DataGridViewAutoSizeRowMode.AllCellsExceptHeader,
                DataGridViewAutoSizeRowsMode.AllCells or DataGridViewAutoSizeRowsMode.DisplayedCells => DataGridViewAutoSizeRowMode.AllCells,
                _ => DataGridViewAutoSizeRowMode.RowHeader,
            };

        private static void ValidateAutoSizeRowsMode (DataGridViewAutoSizeRowsMode value)
        {
            switch (value) {
                case DataGridViewAutoSizeRowsMode.None:
                case DataGridViewAutoSizeRowsMode.AllHeaders:
                case DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders:
                case DataGridViewAutoSizeRowsMode.AllCells:
                case DataGridViewAutoSizeRowsMode.DisplayedHeaders:
                case DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders:
                case DataGridViewAutoSizeRowsMode.DisplayedCells:
                    return;
                default:
                    throw new InvalidEnumArgumentException (nameof (value), (int) value, typeof (DataGridViewAutoSizeRowsMode));
            }
        }

        // ---------------- measuring

        // The height a row's content needs under the given row mode, upstream's DataGridViewRow.GetPreferredHeight with a
        // fixed width: the row header when it is counted and shown, and every visible column's cell in that column's width.
        // Not floored at the row's current height -- that is what lets a row shrink back when its column widens.
        internal int PreferredAutoRowHeight (int rowIndex, DataGridViewAutoSizeRowMode rowMode)
        {
            var row = Rows[rowIndex];
            var tallest = 0;

            if (RowHeadersVisible && ((int) rowMode & (int) DataGridViewAutoSizeRowMode.RowHeader) != 0)
                tallest = Math.Max (tallest, PreferredRowHeaderHeight (row));

            if (((int) rowMode & (int) DataGridViewAutoSizeRowMode.AllCellsExceptHeader) != 0) {
                for (var c = 0; c < Columns.Count && c < row.Cells.Count; c++) {
                    if (Columns[c].Visible)
                        tallest = Math.Max (tallest, PreferredAutoCellHeight (row.Cells[c], Columns[c]));
                }
            }

            return tallest;
        }

        // Upstream's row header cell wants 17px when it shows no text (its glyph area), and its text lines plus 4 when it
        // does: a three-line header value made a 64px row in Microsoft Sans Serif 12.
        private const int EmptyRowHeaderPreferredHeight = 17;

        private int PreferredRowHeaderHeight (DataGridViewRow row)
        {
            var text = row.HeaderCell?.Value?.ToString ();

            if (string.IsNullOrEmpty (text))
                return EmptyRowHeaderPreferredHeight;

            var (font, size) = CellFallbackFont ();
            var lines = text!.Split ('\n').Length;
            return Math.Max (EmptyRowHeaderPreferredHeight, lines * LogicalCellLineHeight (font, size) + 4);
        }

        // A text cell's preferred height, as upstream's DataGridViewTextBoxCell measures it: the text -- wrapped in the
        // column when the style wraps and there is more than one character, one line otherwise -- plus a 1px top margin
        // when wrapping (2px when not), a 1px bottom margin, the 1px bottom border and the style's vertical padding. With
        // Microsoft Sans Serif 12's 20px lines that is 23 for one wrapped line, 24 unwrapped and 63 for three lines.
        //
        // The lines are counted where they are painted: in device pixels, in the width the renderer draws text in (the
        // cell less 4px a side and the padding), so a row is never sized for fewer lines than it draws.
        private int PreferredAutoCellHeight (DataGridViewCell cell, DataGridViewColumn column)
        {
            var style = Renderers.DataGridViewRenderer.WithInherited (cell);
            var (ambient_font, ambient_size) = CellFallbackFont ();
            var font = style?.Font ?? DefaultCellStyle.Font ?? ambient_font;
            var font_size = style?.FontSize ?? DefaultCellStyle.FontSize ?? ambient_size;
            var padding = style?.Padding ?? Padding.Empty;
            var wraps = style?.WrapMode == DataGridViewTriState.True;

            var text = cell.FormattedValue?.ToString ();

            if (string.IsNullOrEmpty (text))
                text = " ";

            var line = LogicalCellLineHeight (font, font_size);
            var content = line;

            if (wraps && text!.Length > 1) {
                var device_size = LogicalToDeviceUnits (font_size);
                var width = Math.Max (1, LogicalToDeviceUnits (column.Width) - 8 - padding.Horizontal);
                var single = TextMeasurer.MeasureText ("X", font, device_size).Height;
                var wrapped = TextMeasurer.MeasureText (text, font, device_size, new System.Drawing.Size (width, int.MaxValue)).Height;
                var lines = single > 0 ? Math.Max (1, (int) Math.Round (wrapped / single)) : 1;

                content = Math.Max (lines * line, DeviceToLogicalUnits ((int) Math.Ceiling (wrapped)));
            }

            return content + (wraps ? 1 : 2) + 1 + 1 + padding.Vertical;
        }

        // One line of cell text in logical pixels: GDI's line height under the upstream look, where the rows are meant to
        // come out the size upstream's do, and never less than the line the text is drawn with.
        private int LogicalCellLineHeight (SkiaSharp.SKTypeface font, int size)
        {
            var drawn = DeviceToLogicalUnits ((int) Math.Ceiling (TextMeasurer.MeasureText ("Wg", font, LogicalToDeviceUnits (size)).Height));

            if (!ControlPaint.UsesUpstreamGlyphs)
                return drawn;

            var style = (font.IsBold ? Majorsilence.Forms.Drawing.FontStyle.Bold : Majorsilence.Forms.Drawing.FontStyle.Regular)
                      | (font.IsItalic ? Majorsilence.Forms.Drawing.FontStyle.Italic : Majorsilence.Forms.Drawing.FontStyle.Regular);

            return Math.Max (drawn, TextLineHeight.Of (new Majorsilence.Forms.Drawing.Font (font.FamilyName, size * 72f / 96f, style)));
        }

        // ---------------- sizing

        // AutoResizeRowInternal under an auto mode: the row takes its preferred height, floored at its MinimumHeight.
        // Returns whether the height changed.
        private bool AutoSizeRowCore (int rowIndex, DataGridViewAutoSizeRowMode rowMode)
        {
            if (rowIndex < 0 || rowIndex >= RowCountWithNewRow)
                return false;

            var row = Rows[rowIndex];

            if (!row.Visible)
                return false;

            var preferred = Math.Min (MaxRowHeight, Math.Max (PreferredAutoRowHeight (rowIndex, rowMode), row.MinimumHeight));

            if (row.Height == preferred)
                return false;

            row.SetAutoSizedHeight (preferred);
            return true;
        }

        // Runs a batch of row sizing with one layout pass at the end, rather than one per row.
        private void InRowAutoSizeBatchDo (Func<bool> body)
        {
            var changed = false;
            auto_size_rows_count++;

            try {
                changed = body ();

                if (changed)
                    OnRowsChanged ();
            } finally {
                auto_size_rows_count--;
            }

            auto_rows_width_key = ColumnWidthsKey ();
        }

        // Upstream's AdjustShrinkingRows: every visible row in the All* modes, the rows on screen in the Displayed* modes.
        private void AdjustAutoSizedRows ()
        {
            var mode = auto_size_rows_mode;

            if (mode == DataGridViewAutoSizeRowsMode.None || !RowsAutoSizable (mode))
                return;

            var row_mode = RowModeOf (mode);

            InRowAutoSizeBatchDo (() => {
                var changed = false;

                if ((RowsFlags (mode) & AutoSizeRowsFlags.AllRows) != 0) {
                    for (var i = 0; i < RowCountWithNewRow; i++)
                        changed |= AutoSizeRowCore (i, row_mode);
                } else {
                    changed = SizeDisplayedRows (row_mode, 0, int.MaxValue, cacheHeights: false);
                }

                return changed;
            });
        }

        // Walks the rows on screen from the first displayed one, sizing those in [first, first + count) as it goes, so a
        // row that grows pushes the ones after it off the screen before they are measured. cacheHeights records each
        // row's height first, as PaintRows does.
        private bool SizeDisplayedRows (DataGridViewAutoSizeRowMode rowMode, int first, int count, bool cacheHeights)
        {
            var available = GetContentArea ().Height - RowsTopOffset;
            var used = 0;
            var changed = false;
            var last = count == int.MaxValue ? int.MaxValue : first + count;

            for (var i = Math.Max (0, top_index); i < RowCountWithNewRow && used < available; i++) {
                if (i >= first && i < last && Rows[i].Visible) {
                    if (cacheHeights)
                        Rows[i].CachedHeight = Rows[i].Height;

                    changed |= AutoSizeRowCore (i, rowMode);
                }

                used += RowDeviceHeight (i);
            }

            return changed;
        }

        private int ColumnWidthsKey ()
        {
            var key = 17;

            foreach (DataGridViewColumn column in Columns)
                key = unchecked (key * 31 + (column.Visible ? column.Width : -1));

            return key;
        }

        // ---------------- hooks

        // OnAutoSizeRowsModeChanged's work, before the event: back to None gives every visible row the height it had when
        // the mode was switched on (or was assigned since); switching on from None records those heights, then sizes.
        private void ApplyAutoSizeRowsModeChange (bool previousModeAutoSized)
        {
            if (auto_size_rows_mode == DataGridViewAutoSizeRowsMode.None) {
                InRowAutoSizeBatchDo (() => {
                    var changed = false;

                    for (var i = 0; i < RowCountWithNewRow; i++) {
                        var row = Rows[i];

                        if (row.Visible && row.CachedHeight > 0 && row.Height != row.CachedHeight) {
                            row.SetAutoSizedHeight (row.CachedHeight);
                            changed = true;
                        }
                    }

                    return changed;
                });

                return;
            }

            if (!previousModeAutoSized) {
                for (var i = 0; i < RowCountWithNewRow; i++)
                    Rows[i].CachedHeight = Rows[i].Height;
            }

            AdjustAutoSizedRows ();
        }

        // OnAddedRow_PostNotification: rows that have just arrived are sized, after RowsAdded -- in the Displayed* modes
        // only those that are on screen. Each keeps the height it arrived with for a return to None.
        internal void AutoSizeAddedRows (int index, int count)
        {
            var mode = auto_size_rows_mode;

            if (mode == DataGridViewAutoSizeRowsMode.None || auto_size_rows_count > 0 || count <= 0)
                return;

            for (var i = index; i < index + count && i < RowCountWithNewRow; i++)
                Rows[i].CachedHeight = Rows[i].Height;

            if (!RowsAutoSizable (mode))
                return;

            var row_mode = RowModeOf (mode);

            InRowAutoSizeBatchDo (() => {
                if ((RowsFlags (mode) & AutoSizeRowsFlags.AllRows) == 0)
                    return SizeDisplayedRows (row_mode, index, count, cacheHeights: false);

                var changed = false;

                for (var i = index; i < index + count; i++)
                    changed |= AutoSizeRowCore (i, row_mode);

                return changed;
            });
        }

        // A bind builds its rows the way upstream's does: the first is added and sized, and the rest are copies of it,
        // so they start at its sized height -- which is the height a Displayed* grid leaves the rows that are off screen.
        private void AutoSizeBoundRows ()
        {
            if (auto_size_rows_mode == DataGridViewAutoSizeRowsMode.None || Rows.Count == 0)
                return;

            for (var i = 0; i < Rows.Count; i++)
                Rows[i].CachedHeight = Rows[i].Height;

            if (!RowsAutoSizable (auto_size_rows_mode))
                return;

            var row_mode = RowModeOf (auto_size_rows_mode);

            InRowAutoSizeBatchDo (() => {
                var changed = AutoSizeRowCore (0, row_mode);
                var first = Rows[0].Height;

                for (var i = 1; i < Rows.Count; i++) {
                    if (Rows[i].Height != first) {
                        Rows[i].SetCopiedHeight (first);
                        changed = true;
                    }
                }

                if ((RowsFlags (auto_size_rows_mode) & AutoSizeRowsFlags.AllRows) != 0) {
                    for (var i = 1; i < Rows.Count; i++)
                        changed |= AutoSizeRowCore (i, row_mode);
                } else {
                    changed |= SizeDisplayedRows (row_mode, 1, Rows.Count - 1, cacheHeights: false);
                }

                return changed;
            });
        }

        // OnCellCommonChange: a cell whose value changed re-sizes its row in the cell modes, on screen or not.
        private void AutoSizeRowForCellChange (int rowIndex)
        {
            if (!AutoSizesRowCells || auto_size_rows_count > 0 || rowIndex < 0 || rowIndex >= RowCountWithNewRow)
                return;

            var row_mode = RowModeOf (auto_size_rows_mode);
            InRowAutoSizeBatchDo (() => AutoSizeRowCore (rowIndex, row_mode));
        }

        // OnColumnGlobalAutoSize, and the layout pass that re-fills Fill columns: once the columns' widths have moved, the
        // cell modes re-size the rows. A pass can show or hide the vertical scrollbar and move the Fill widths again, so it
        // repeats until the widths settle (a bounded number of times, so rows that toggle the bar cannot loop).
        private void AutoSizeRowsAfterLayout ()
        {
            for (var pass = 0; pass < 3; pass++) {
                if (!AutoSizesRowCells || auto_size_rows_count > 0 || ColumnWidthsKey () == auto_rows_width_key)
                    return;

                AdjustAutoSizedRows ();
            }
        }

        // PaintRows: every row about to be painted is sized, and the height it had is recorded as the one a return to None
        // gives back. This is what sizes the rows a Displayed* grid scrolls onto the screen.
        private void AutoSizeRowsForPaint ()
        {
            if (auto_size_rows_mode == DataGridViewAutoSizeRowsMode.None || auto_size_rows_count > 0)
                return;

            var row_mode = RowModeOf (auto_size_rows_mode);
            InRowAutoSizeBatchDo (() => SizeDisplayedRows (row_mode, 0, int.MaxValue, cacheHeights: true));
        }
    }
}
