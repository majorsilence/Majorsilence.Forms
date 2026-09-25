using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Majorsilence.Forms
{
    // W5.5, the keyboard half (finding DGV-29).
    //
    // All navigation lived in OnKeyUp. A key-up handler cannot auto-repeat -- holding Down moved one
    // row and stopped -- and it runs after the key has already been offered to everything else.
    // Enter did nothing. Delete did nothing, with AllowUserToDeleteRows stored-only and
    // UserDeletingRow/UserDeletedRow declared and never invoked. Ctrl+C copied nothing, although
    // GetClipboardContent () was implemented and working. Home/End jumped to the first/last ROW, where
    // upstream moves to the first/last COLUMN and Ctrl+Home/End moves to the first/last cell. Left,
    // Right and Tab were ignored entirely in FullRowSelect.
    public partial class DataGridView
    {
        /// <summary>Drives <see cref="OnKeyDown"/> from a test, so the Handled flag can be asserted.</summary>
        internal void OnKeyDownForTest (KeyEventArgs e) => OnKeyDown (e);

        // Whether this key is one the grid navigates with. Asked before handling so a key the grid does
        // not use is left for the form -- a dialog's AcceptButton still sees Enter when the grid has no
        // current cell to move from.
        private bool HandleNavigationKey (KeyEventArgs e)
        {
            switch (e.KeyCode) {
                case Keys.Down:
                    return MoveCurrentRowBy (1);

                case Keys.Up:
                    return MoveCurrentRowBy (-1);

                case Keys.PageDown:
                    return MoveCurrentRowTo (Math.Min (selected_row_index + DisplayedRowCount (true), Rows.Count - 1));

                case Keys.PageUp:
                    return MoveCurrentRowTo (Math.Max (selected_row_index - DisplayedRowCount (true), 0));

                // Home/End move along the ROW -- first and last column -- and Ctrl+Home/End move to the
                // first and last cell of the grid. They used to jump to the first/last row, which is
                // what Ctrl+Home/End means, so the unmodified keys did the modified thing.
                case Keys.Home when e.Control:
                    return MoveCurrentCellChecked (0, FirstNavigableColumn ());

                case Keys.End when e.Control:
                    return MoveCurrentCellChecked (Rows.Count - 1, LastNavigableColumn ());

                case Keys.Home:
                    return MoveCurrentCellChecked (selected_row_index, FirstNavigableColumn ());

                case Keys.End:
                    return MoveCurrentCellChecked (selected_row_index, LastNavigableColumn ());

                case Keys.Left:
                    return MoveCurrentColumnBy (-1);

                case Keys.Right:
                    return MoveCurrentColumnBy (1);

                case Keys.Enter:
                    return HandleEnterKey ();

                case Keys.Delete:
                    return DeleteSelectedRows ();

                case Keys.C when e.Control:
                case Keys.Insert when e.Control:
                    return CopySelectionToClipboard ();

                case Keys.Tab:
                    // StandardTab means "Tab belongs to the form": leaving it unhandled is what lets
                    // focus move to the next control instead of the next cell.
                    if (StandardTab)
                        return false;

                    if (e.Shift)
                        NavigateToPreviousCell ();
                    else
                        NavigateToNextCell ();

                    return true;

                default:
                    return false;
            }
        }

        // ---------------- moving

        private bool MoveCurrentRowBy (int delta) => MoveCurrentRowTo (selected_row_index + delta);

        private bool MoveCurrentRowTo (int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= RowCountWithNewRow || rowIndex == selected_row_index)
                return false;

            SelectedRowIndex = rowIndex;
            EnsureRowVisible (selected_row_index);
            return true;
        }

        private bool MoveCurrentColumnBy (int delta)
        {
            // Left/Right move between cells, and in the row modes there is nothing to move between --
            // upstream moves the current cell there too, but the selection stays on the row.
            // Along the display order, so Left/Right walk the columns as they are shown (W6).
            var position = DisplayPositionOf (selected_column_index) + delta;

            if (position < 0 || position >= Columns.Count)
                return false;

            SelectedColumnIndex = DisplayOrder[position];
            return true;
        }

        private bool MoveCurrentCellChecked (int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count || columnIndex < 0 || columnIndex >= Columns.Count)
                return false;

            if (rowIndex == selected_row_index && columnIndex == selected_column_index)
                return false;

            if (!MoveCurrentCell (rowIndex, columnIndex))
                return false;

            ReplaceSelectionWithCurrentCell ();
            EnsureRowVisible (selected_row_index);
            return true;
        }

        private int FirstNavigableColumn ()
        {
            foreach (var i in DisplayOrder)
                if (Columns[i].Visible)
                    return i;

            return -1;
        }

        private int LastNavigableColumn ()
        {
            foreach (var i in Enumerable.Reverse (DisplayOrder))
                if (Columns[i].Visible)
                    return i;

            return -1;
        }

        // ---------------- Enter, Delete, Ctrl+C

        // Enter commits an edit in progress and moves down, which is what a user typing through a grid
        // expects and what upstream's ProcessEnterKey does.
        private bool HandleEnterKey ()
        {
            if (IsCurrentCellInEditMode) {
                EndEdit ();
                return true;
            }

            if (selected_row_index < 0)
                return false;

            return MoveCurrentRowBy (1);
        }

        /// <summary>
        /// Deletes the selected rows, raising <see cref="UserDeletingRow"/> before each (a cancelling
        /// handler keeps its row) and <see cref="UserDeletedRow"/> after. Returns whether anything was
        /// deleted. Honours <see cref="AllowUserToDeleteRows"/> and <see cref="ReadOnly"/>.
        /// </summary>
        private bool DeleteSelectedRows ()
        {
            if (!AllowUserToDeleteRows || read_only || IsCurrentCellInEditMode)
                return false;

            // Highest index first, so removing one does not shift the ones still to go.
            var doomed = SelectedRows.OrderByDescending (r => r.Index).ToList ();

            if (doomed.Count == 0 && CurrentRow is { } current)
                doomed.Add (current);

            if (doomed.Count == 0)
                return false;

            var deleted = false;

            foreach (var row in doomed) {
                var index = row.Index;

                if (index < 0)
                    continue;

                var cancelling = new DataGridViewRowCancelEventArgs (row);
                OnUserDeletingRow (cancelling);

                if (cancelling.Cancel)
                    continue;

                // A bound grid does not own its rows: removing from the LIST is what deletes, and the
                // ListChanged that follows removes the row (W5.3). Removing the row directly would be
                // undone by the next rebind.
                if (data_source is { } source && index < source.Count) {
                    if (source.IsReadOnly || source.IsFixedSize)
                        continue;

                    source.RemoveAt (index);
                } else {
                    Rows.RemoveAt (index);
                }

                deleted = true;
                OnUserDeletedRow (new DataGridViewRowEventArgs (row));
            }

            if (deleted) {
                ClampCurrentCellToRows ();
                UpdateScrollBars ();
                Invalidate ();
            }

            return deleted;
        }

        /// <summary>Raises the <see cref="UserDeletingRow"/> event.</summary>
        protected virtual void OnUserDeletingRow (DataGridViewRowCancelEventArgs e) => _userDeletingRow?.Invoke (this, e);

        /// <summary>Raises the <see cref="UserDeletedRow"/> event.</summary>
        protected virtual void OnUserDeletedRow (DataGridViewRowEventArgs e) => _userDeletedRow?.Invoke (this, e);

        // Ctrl+C / Ctrl+Insert. GetClipboardContent () already built the DataObject -- tab-delimited
        // text, CSV and an HTML table -- and nothing called it from a key.
        private bool CopySelectionToClipboard ()
        {
            if (GetClipboardContent () is not { } content)
                return false;

            Clipboard.SetDataObject (content);
            return true;
        }
    }
}
