using System;
using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    // DGV-14 (and DGV-15, which could not be fixed before it): the selection model. Three things were
    // tangled together here.
    //
    //  1. Row.Selected and Cell.Selected were auto-properties. Nothing read them -- the renderer
    //     highlighted SelectedRowIndex -- so rows[i].Selected = true stored a bool and changed nothing
    //     on screen, raised no SelectionChanged, and left SelectedRows disagreeing with the paint.
    //  2. selected_row_index doubled as "the current cell" AND "the selection", which is why
    //     ClearSelection had to blank the current cell in order to clear the selection (DGV-15), and
    //     why MultiSelect could not mean anything: there is only one index.
    //  3. SelectedRows came back in index order. Upstream prepends to its selection list, so
    //     SelectedRows[0] is the most recently selected row -- the one a Ctrl-click just added, which
    //     is what a handler reading SelectedRows[0] after a click is asking for.
    //
    // The fix leaves the *element* as the single source of truth for "am I selected", so a detached row
    // or cell still round-trips the property, and puts the ordering on the grid as a monotonic stamp.
    // Recency then falls out of a sort, rather than out of a parallel list of references that would go
    // stale every time rows are sorted, replaced or removed -- which they are, on every rebind.
    public partial class DataGridView
    {
        // Handed out in increasing order, so a larger stamp means "selected more recently". Never
        // reset: exhausting a long takes 9.2e18 selections.
        private long selection_sequence;

        // Where a Shift-click extends from. -1 until the first plain click or programmatic single
        // selection.
        private int selection_anchor_row = -1;
        private int selection_anchor_column = -1;

        /// <summary>
        /// True when selecting anything selects whole rows -- <see cref="DataGridViewSelectionMode.FullRowSelect"/>
        /// or <see cref="DataGridViewSelectionMode.RowHeaderSelect"/>.
        /// </summary>
        internal bool SelectionIsRowBased
            => selection_mode is DataGridViewSelectionMode.FullRowSelect or DataGridViewSelectionMode.RowHeaderSelect;

        /// <summary>
        /// True when selecting anything selects whole columns -- <see cref="DataGridViewSelectionMode.FullColumnSelect"/>
        /// or <see cref="DataGridViewSelectionMode.ColumnHeaderSelect"/>.
        /// </summary>
        internal bool SelectionIsColumnBased
            => selection_mode is DataGridViewSelectionMode.FullColumnSelect or DataGridViewSelectionMode.ColumnHeaderSelect;

        // ---------------- the three choke points

        /// <summary>
        /// Applies <see cref="DataGridViewRow.Selected"/>. The property setter routes here whenever the
        /// row belongs to a grid, so the flag, the repaint and the notification cannot come apart.
        /// </summary>
        internal void SetRowSelected (DataGridViewRow row, bool value)
        {
            Guard.ThrowIfNull (row);

            if (row.Selected == value)
                return;

            // What MultiSelect = false is for: one selected thing at a time, whether the selection came
            // from a click or from code.
            if (value && !multi_select)
                ClearSelectionCore ();

            row.SetSelectedCore (value, value ? ++selection_sequence : 0);
            NotifySelectionChanged ();
        }

        /// <summary>
        /// Applies <see cref="DataGridViewCell.Selected"/>. See <see cref="SetRowSelected"/>.
        /// </summary>
        internal void SetCellSelected (DataGridViewCell cell, bool value)
        {
            Guard.ThrowIfNull (cell);

            if (cell.Selected == value)
                return;

            if (value && !multi_select)
                ClearSelectionCore ();

            cell.SetSelectedCore (value, value ? ++selection_sequence : 0);
            NotifySelectionChanged ();
        }

        /// <summary>
        /// Applies <see cref="DataGridViewColumn.Selected"/>. See <see cref="SetRowSelected"/>.
        /// </summary>
        internal void SetColumnSelected (DataGridViewColumn column, bool value)
        {
            Guard.ThrowIfNull (column);

            if (column.Selected == value)
                return;

            if (value && !multi_select)
                ClearSelectionCore ();

            column.SetSelectedCore (value, value ? ++selection_sequence : 0);
            NotifySelectionChanged ();
        }

        private void NotifySelectionChanged ()
        {
            OnSelectionChanged (EventArgs.Empty);
            Invalidate ();
        }

        // Drops every selected row, cell and column without announcing anything. Returns whether it
        // changed something, so a caller that must raise exactly once can tell whether to raise at all.
        private bool ClearSelectionCore ()
        {
            var changed = false;

            foreach (var row in Rows) {
                if (row.Selected) {
                    row.SetSelectedCore (false, 0);
                    changed = true;
                }

                foreach (var cell in row.Cells) {
                    if (cell.Selected) {
                        cell.SetSelectedCore (false, 0);
                        changed = true;
                    }
                }
            }

            foreach (var column in Columns) {
                if (column.Selected) {
                    column.SetSelectedCore (false, 0);
                    changed = true;
                }
            }

            return changed;
        }

        // Runs a composite selection change and announces it once.
        //
        // Every body below writes through SetSelectedCore rather than through the Selected properties,
        // so the per-element choke points do not run inside a batch and the batch owns the single
        // notification. There is deliberately no suppression flag guarding that: the first version of
        // this had one, and nothing took the path it guarded -- an unexercised guard is worse than none.
        //
        // Honest caveat: rewriting these bodies to assign the Selected properties instead did NOT make
        // the "announces once" tests fail, so those tests are guards rather than proof of this design
        // (they say so). The direct writes are kept because they make the single notification structural
        // rather than incidental, not because a test pins the difference.
        private void InSelectionBatch (Action body)
        {
            body ();
            NotifySelectionChanged ();
        }

        // ---------------- replacing the selection

        /// <summary>
        /// Replaces the whole selection with one row, and makes it the anchor a later Shift-click
        /// extends from. Raises <see cref="SelectionChanged"/> once.
        /// </summary>
        internal void SelectSingleRow (int rowIndex)
        {
            InSelectionBatch (() => {
                ClearSelectionCore ();

                if (rowIndex >= 0 && rowIndex < Rows.Count)
                    Rows[rowIndex].SetSelectedCore (true, ++selection_sequence);
            });

            selection_anchor_row = rowIndex;
        }

        /// <summary>
        /// Replaces the whole selection with one cell. Raises <see cref="SelectionChanged"/> once.
        /// </summary>
        internal void SelectSingleCell (int rowIndex, int columnIndex)
        {
            InSelectionBatch (() => {
                ClearSelectionCore ();

                if (IsCellAddress (rowIndex, columnIndex))
                    Rows[rowIndex].Cells[columnIndex].SetSelectedCore (true, ++selection_sequence);
            });

            selection_anchor_row = rowIndex;
            selection_anchor_column = columnIndex;
        }

        /// <summary>
        /// Replaces the whole selection with one column. Raises <see cref="SelectionChanged"/> once.
        /// </summary>
        internal void SelectSingleColumn (int columnIndex)
        {
            InSelectionBatch (() => {
                ClearSelectionCore ();

                if (columnIndex >= 0 && columnIndex < Columns.Count)
                    Columns[columnIndex].SetSelectedCore (true, ++selection_sequence);
            });

            selection_anchor_column = columnIndex;
        }

        private bool IsCellAddress (int rowIndex, int columnIndex)
            => rowIndex >= 0 && rowIndex < Rows.Count
               && columnIndex >= 0 && columnIndex < Rows[rowIndex].Cells.Count;

        // ---------------- extending the selection

        // Rows between two indices inclusive, replacing what was selected. The stamps are handed out
        // walking AWAY from the anchor, so the far end of the range -- the row the user actually
        // clicked -- is the most recent, and SelectedRows[0] is that row rather than the anchor.
        private void SelectRowRange (int from, int to)
        {
            InSelectionBatch (() => {
                ClearSelectionCore ();

                var step = from <= to ? 1 : -1;

                for (var i = from; ; i += step) {
                    // A hidden row is not part of a visible range: Shift-clicking across a filtered
                    // row must not quietly include it (DGV-20 made Row.Visible mean something).
                    if (i >= 0 && i < Rows.Count && Rows[i].Visible)
                        Rows[i].SetSelectedCore (true, ++selection_sequence);

                    if (i == to)
                        break;
                }
            });

            // The anchor deliberately survives, so successive Shift-clicks re-extend from the same
            // origin instead of walking it along behind the pointer.
        }

        // The rectangular block between two cell addresses, which is what Shift-click means in the cell
        // modes.
        private void SelectCellBlock (int fromRow, int fromColumn, int toRow, int toColumn)
        {
            var top = Math.Min (fromRow, toRow);
            var bottom = Math.Max (fromRow, toRow);
            var left = Math.Min (fromColumn, toColumn);
            var right = Math.Max (fromColumn, toColumn);

            InSelectionBatch (() => {
                ClearSelectionCore ();

                // Row order runs away from the anchor for the same reason as SelectRowRange; within a
                // row the columns run away from the anchor column.
                var row_step = fromRow <= toRow ? 1 : -1;
                var column_step = fromColumn <= toColumn ? 1 : -1;

                for (var r = fromRow; ; r += row_step) {
                    if (r >= top && r <= bottom && r < Rows.Count && Rows[r].Visible)
                        for (var c = fromColumn; ; c += column_step) {
                            if (c >= left && c <= right && IsCellAddress (r, c))
                                Rows[r].Cells[c].SetSelectedCore (true, ++selection_sequence);

                            if (c == toColumn)
                                break;
                        }

                    if (r == toRow)
                        break;
                }
            });
        }

        private void SelectColumnRange (int from, int to)
        {
            InSelectionBatch (() => {
                ClearSelectionCore ();

                var step = from <= to ? 1 : -1;

                for (var i = from; ; i += step) {
                    if (i >= 0 && i < Columns.Count && Columns[i].Visible)
                        Columns[i].SetSelectedCore (true, ++selection_sequence);

                    if (i == to)
                        break;
                }
            });
        }

        // ---------------- what a pointer click means

        /// <summary>
        /// Applies a click on a cell to the selection, honouring <see cref="MultiSelect"/> with Ctrl
        /// (toggle) and Shift (extend from the anchor). One <see cref="SelectionChanged"/> per click.
        /// </summary>
        internal void SelectFromPointer (int rowIndex, int columnIndex, Keys modifiers)
        {
            var ctrl = (modifiers & Keys.Control) == Keys.Control;
            var shift = (modifiers & Keys.Shift) == Keys.Shift;
            var extend = multi_select && shift;
            var toggle = multi_select && ctrl;

            if (SelectionIsColumnBased) {
                if (toggle) {
                    ToggleColumn (columnIndex);
                } else if (extend && selection_anchor_column >= 0) {
                    SelectColumnRange (selection_anchor_column, columnIndex);
                } else {
                    SelectSingleColumn (columnIndex);
                }

                return;
            }

            if (SelectionIsRowBased) {
                if (toggle) {
                    ToggleRow (rowIndex);
                } else if (extend && selection_anchor_row >= 0) {
                    SelectRowRange (selection_anchor_row, rowIndex);
                } else {
                    SelectSingleRow (rowIndex);
                }

                return;
            }

            if (toggle) {
                ToggleCell (rowIndex, columnIndex);
            } else if (extend && selection_anchor_row >= 0 && selection_anchor_column >= 0) {
                SelectCellBlock (selection_anchor_row, selection_anchor_column, rowIndex, columnIndex);
            } else {
                SelectSingleCell (rowIndex, columnIndex);
            }
        }

        // Ctrl-click: flip this one element and leave the rest of the selection alone. The anchor moves
        // to it, so a following Shift-click extends from where the Ctrl-click landed, as upstream does.
        private void ToggleRow (int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count)
                return;

            Rows[rowIndex].Selected = !Rows[rowIndex].Selected;
            selection_anchor_row = rowIndex;
        }

        private void ToggleCell (int rowIndex, int columnIndex)
        {
            if (!IsCellAddress (rowIndex, columnIndex))
                return;

            var cell = Rows[rowIndex].Cells[columnIndex];
            cell.Selected = !cell.Selected;
            selection_anchor_row = rowIndex;
            selection_anchor_column = columnIndex;
        }

        private void ToggleColumn (int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                return;

            Columns[columnIndex].Selected = !Columns[columnIndex].Selected;
            selection_anchor_column = columnIndex;
        }

        // ---------------- reading the selection back

        /// <summary>
        /// Gets the selected rows, most recently selected first -- so <c>SelectedRows[0]</c> is the row
        /// the last click or assignment selected, matching System.Windows.Forms, which prepends to its
        /// selection list.
        /// </summary>
        public IReadOnlyList<DataGridViewRow> SelectedRows
            => Rows.Where (r => r.Selected)
                   .OrderByDescending (r => r.SelectionOrder)
                   .ToList ()
                   .AsReadOnly ();

        /// <summary>
        /// Gets the selected cells, most recently selected first. In the row modes a selected row's
        /// cells all count as selected, and in the column modes a selected column's do, which is how
        /// <c>SelectedCells.Count</c> can be right in every <see cref="SelectionMode"/>.
        /// </summary>
        public IReadOnlyList<DataGridViewCell> SelectedCells {
            get {
                var selected = new List<(DataGridViewCell Cell, long Order, int Column)> ();

                for (var r = 0; r < Rows.Count; r++) {
                    var row = Rows[r];

                    for (var c = 0; c < row.Cells.Count; c++) {
                        var cell = row.Cells[c];

                        // A cell is selected in its own right, or by virtue of its row or its column
                        // being selected in a mode where that selects cells. The stamp used for
                        // ordering is whichever of those actually selected it.
                        if (cell.Selected)
                            selected.Add ((cell, cell.SelectionOrder, c));
                        else if (SelectionIsRowBased && row.Selected)
                            selected.Add ((cell, row.SelectionOrder, c));
                        else if (SelectionIsColumnBased && c < Columns.Count && Columns[c].Selected)
                            selected.Add ((cell, Columns[c].SelectionOrder, c));
                    }
                }

                return selected.OrderByDescending (e => e.Order)
                               .ThenBy (e => e.Column)
                               .Select (e => e.Cell)
                               .ToList ()
                               .AsReadOnly ();
            }
        }

        /// <summary>
        /// Gets the selected columns, most recently selected first. Only the column modes
        /// (<see cref="DataGridViewSelectionMode.FullColumnSelect"/>,
        /// <see cref="DataGridViewSelectionMode.ColumnHeaderSelect"/>) select columns from the UI, but
        /// <see cref="DataGridViewColumn.Selected"/> can be set in any mode and is reported here.
        /// </summary>
        public DataGridViewColumnCollection SelectedColumns {
            get {
                // A projection, not a second column list: adding to a normal DataGridViewColumnCollection
                // re-owns the column and raises ColumnAdded, which would mean reading this property
                // fired the grid's column events.
                var result = new DataGridViewColumnCollection (this, projection: true);

                foreach (var column in Columns.Where (c => c.Selected).OrderByDescending (c => c.SelectionOrder))
                    result.Add (column);

                return result;
            }
        }

        // ---------------- what the renderer asks

        /// <summary>
        /// True when the whole row should be drawn highlighted -- the row modes only, so a selected
        /// cell in <see cref="DataGridViewSelectionMode.CellSelect"/> does not light up its row.
        /// </summary>
        internal bool IsRowPaintedSelected (int rowIndex)
            => SelectionIsRowBased
               && rowIndex >= 0 && rowIndex < Rows.Count
               && Rows[rowIndex].Selected;

        /// <summary>
        /// True when an individual cell should be drawn as selected -- the cell and column modes, where
        /// selection is per-cell rather than per-row.
        /// </summary>
        internal bool IsCellPaintedSelected (int rowIndex, int columnIndex)
        {
            if (SelectionIsRowBased || !IsCellAddress (rowIndex, columnIndex))
                return false;

            return Rows[rowIndex].Cells[columnIndex].Selected
                   || (SelectionIsColumnBased && columnIndex < Columns.Count && Columns[columnIndex].Selected);
        }

        // ---------------- the whole-grid operations

        /// <summary>
        /// Selects everything the current <see cref="SelectionMode"/> selects -- every row, every column
        /// or every cell. Raises <see cref="SelectionChanged"/> once.
        /// </summary>
        public void SelectAll ()
        {
            InSelectionBatch (() => {
                ClearSelectionCore ();

                if (SelectionIsRowBased) {
                    foreach (var row in Rows)
                        row.SetSelectedCore (true, ++selection_sequence);
                } else if (SelectionIsColumnBased) {
                    foreach (var column in Columns)
                        column.SetSelectedCore (true, ++selection_sequence);
                } else {
                    foreach (var row in Rows)
                        foreach (var cell in row.Cells)
                            cell.SetSelectedCore (true, ++selection_sequence);
                }
            });
        }

        /// <summary>
        /// Clears the selection. The current cell is left where it is -- clearing a selection is not
        /// moving the cursor, and System.Windows.Forms leaves <see cref="CurrentCell"/> alone here, so
        /// <c>grid.ClearSelection (); grid.CurrentRow.Cells[...]</c> keeps working. Raises
        /// <see cref="SelectionChanged"/> once, and only if something was actually selected.
        /// </summary>
        public void ClearSelection ()
        {
            if (!ClearSelectionCore ())
                return;

            NotifySelectionChanged ();
        }
    }
}
