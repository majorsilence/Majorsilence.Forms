using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // DGV-23: AllowUserToOrderColumns -- dragging a column header to a new place in the display order,
    // after upstream's BeginColumnRelocation / MoveColumnRelocation / ColumnRelocationTarget /
    // EndColumnRelocation (Controls/DataGridView/DataGridView.Methods.cs).
    //
    // A drag has to be told from a click, so the header click -- the sort and ColumnHeaderMouseClick --
    // is raised on the release, as upstream raises it (OnMouseClick, skipped while a relocation is
    // tracking), and not on the press, where it used to be: every reorder drag would have sorted the
    // column on the way.
    //
    // All positions here are DEVICE pixels, the grid's geometry; the mouse handlers convert the logical
    // pointer once on the way in (RC-8).
    public partial class DataGridView
    {
        // Upstream's InsertionBarWidth (DataGridView.cs), in logical pixels.
        private const int RelocationInsertionBarWidth = 3;

        // The column whose header the left button went down on, until the release; -1 otherwise.
        private int header_press_column = -1;
        private Point header_press_location;

        // The column being dragged while a relocation is tracking; -1 otherwise.
        private int relocation_column = -1;
        // The column's left edge less the pointer's x at the press, so the shadow keeps the grip point.
        private int relocation_mouse_offset;
        private int relocation_mouse_x;
        // Upstream's _trackColumnEdge with State2_ShowColumnRelocationInsertion: the column the dragged one
        // would be inserted after (-1 for "first"), and whether the pointer is over a place that would
        // move it at all.
        private bool relocation_has_target;
        private int relocation_previous_column = -1;

        /// <summary>Whether a column header is being dragged to a new position.</summary>
        internal bool IsRelocatingColumn => relocation_column >= 0;

        // The header press, recorded for the release to decide between a click and a drag.
        private void BeginHeaderPress (int columnIndex, Point logical, Keys modifiers)
        {
            header_press_column = columnIndex;
            header_press_location = logical;

            // Upstream: in the column-selection modes a plain drag selects a range of columns, so the
            // reorder gesture is Alt+drag and begins on the press.
            if ((modifiers & Keys.Alt) == Keys.Alt && AllowUserToOrderColumns && SelectionIsColumnBased)
                BeginColumnRelocation (LogicalToDeviceUnits (logical.X), columnIndex);
        }

        // Upstream's OnCellMouseMove gate: a left press on a column header, ordering allowed, not a
        // column-selection mode, and the pointer past the system drag threshold.
        private bool TryBeginColumnRelocation (Point logical)
        {
            if (header_press_column < 0 || IsRelocatingColumn || !AllowUserToOrderColumns || SelectionIsColumnBased)
                return false;

            if (header_press_column >= Columns.Count || Columns[header_press_column].PinnedRight)
                return false;

            var drag = SystemInformation.DragSize;

            if (Math.Abs (logical.X - header_press_location.X) < drag.Width
                && Math.Abs (logical.Y - header_press_location.Y) < drag.Height)
                return false;

            BeginColumnRelocation (LogicalToDeviceUnits (header_press_location.X), header_press_column);
            MoveColumnRelocation (LogicalToDeviceUnits (logical.X));
            return true;
        }

        private void BeginColumnRelocation (int deviceX, int columnIndex)
        {
            relocation_column = columnIndex;
            relocation_has_target = false;
            relocation_previous_column = -1;
            relocation_mouse_offset = GetColumnDeviceLeft (columnIndex) - deviceX;
            relocation_mouse_x = deviceX;
            Invalidate ();
        }

        private void MoveColumnRelocation (int deviceX)
        {
            relocation_mouse_x = deviceX;
            relocation_has_target = ColumnRelocationTarget (deviceX, out relocation_previous_column);
            Invalidate ();
        }

        // Applies the move the release point asks for, as upstream's EndColumnRelocation does.
        private void EndColumnRelocation (int deviceX)
        {
            var track = Columns[relocation_column];
            var has_target = ColumnRelocationTarget (deviceX, out var previous);

            CancelColumnRelocation ();

            if (!has_target)
                return;

            if (previous == -1)
                track.DisplayIndex = 0;
            else if (track.DisplayIndex > Columns[previous].DisplayIndex)
                track.DisplayIndex = Columns[previous].DisplayIndex + 1;
            else
                track.DisplayIndex = Columns[previous].DisplayIndex;
        }

        private void CancelColumnRelocation ()
        {
            if (!IsRelocatingColumn)
                return;

            relocation_column = -1;
            relocation_has_target = false;
            relocation_previous_column = -1;
            Invalidate ();
        }

        // Upstream's cursor clip (BeginColumnRelocation): a frozen column moves among the frozen ones and
        // a scrolling column among the scrolling ones, so the pointer is held inside that band.
        private int ClampToRelocationBand (int deviceX)
        {
            var client = GetContentArea ();
            var left0 = client.Left + (row_headers_visible ? ScaledRowHeadersWidth : 0);
            var frozen = FrozenColumnsWidth;

            int left, right;

            if (Columns[relocation_column].Frozen) {
                left = left0;
                right = left0 + frozen;
            } else {
                left = left0 + frozen;
                right = client.Right - RightPinnedColumnsWidth;
            }

            if (right <= left)
                return left;

            return Math.Max (left, Math.Min (right - 1, deviceX));
        }

        // Upstream's ColumnRelocationTarget: over the right half of a header the column goes after it, over
        // the left half after the column displayed before it (or first). A drop that would leave the
        // column where it is, is no target.
        private bool ColumnRelocationTarget (int deviceX, out int previousColumnIndex)
        {
            previousColumnIndex = -1;

            if (!IsRelocatingColumn)
                return false;

            var x = ClampToRelocationBand (deviceX);
            var client = GetContentArea ();
            var column = GetColumnAtLocation (new Point (x, client.Top + ScaledHeaderHeight / 2));

            if (column < 0 || Columns[column].PinnedRight || Columns[column].Frozen != Columns[relocation_column].Frozen)
                return false;

            var left = GetColumnDeviceLeft (column);
            var width = LogicalToDeviceUnits (Columns[column].Width);

            if (x > left + width / 2)
                previousColumnIndex = column;
            else
                previousColumnIndex = PreviousDisplayedColumn (column);

            var next = previousColumnIndex == -1 ? -1 : NextDisplayedColumn (previousColumnIndex);

            return relocation_column != previousColumnIndex
                && !(previousColumnIndex == -1 && column == relocation_column)
                && (next == -1 || next != relocation_column);
        }

        // The visible columns either side of one in display order (upstream's Columns.GetPreviousColumn /
        // GetNextColumn with DataGridViewElementStates.Visible).
        private int PreviousDisplayedColumn (int columnIndex)
        {
            var previous = -1;

            foreach (var i in DisplayOrder) {
                if (i == columnIndex)
                    return previous;

                if (Columns[i].Visible)
                    previous = i;
            }

            return -1;
        }

        private int NextDisplayedColumn (int columnIndex)
        {
            var found = false;

            foreach (var i in DisplayOrder) {
                if (found && Columns[i].Visible)
                    return i;

                if (i == columnIndex)
                    found = true;
            }

            return -1;
        }

        /// <summary>
        /// The feedback a relocation drag draws, in device pixels: the dragged header's shadow under the
        /// pointer and, when the pointer is over a place the column would move to, the insertion bar.
        /// False when no relocation is tracking.
        /// </summary>
        internal bool GetColumnRelocationFeedback (out Rectangle shadow, out Rectangle insertionBar)
        {
            shadow = Rectangle.Empty;
            insertionBar = Rectangle.Empty;

            if (!IsRelocatingColumn || !ColumnHeadersVisible)
                return false;

            var client = GetContentArea ();
            var header = new Rectangle (client.Left, client.Top, client.Width, ScaledHeaderHeight);
            var width = LogicalToDeviceUnits (Columns[relocation_column].Width);

            // Upstream's CalcColRelocationFeedbackRect: the column's width, following the pointer, kept
            // inside the header band.
            var x = Math.Max (header.Left, Math.Min (header.Right - width, relocation_mouse_x + relocation_mouse_offset - 1));
            shadow = new Rectangle (x, header.Top, width, header.Height);

            if (relocation_has_target) {
                var bar = LogicalToDeviceUnits (RelocationInsertionBarWidth);
                int bar_x;

                if (relocation_previous_column == -1) {
                    bar_x = GetColumnDeviceLeft (FirstDisplayedColumn ());
                } else {
                    // Centred on the boundary, or inside the header band after the last column (DrawColHeaderShadow).
                    var boundary = GetColumnDeviceLeft (relocation_previous_column) + LogicalToDeviceUnits (Columns[relocation_previous_column].Width);
                    var offset = NextDisplayedColumn (relocation_previous_column) == -1 ? bar : bar / 2 + 1;
                    bar_x = Math.Min (boundary - offset, header.Right - bar);
                }

                insertionBar = new Rectangle (bar_x, header.Top, bar, header.Height);
            }

            return true;
        }

        private int FirstDisplayedColumn ()
        {
            foreach (var i in DisplayOrder)
                if (Columns[i].Visible)
                    return i;

            return -1;
        }

        // The release that ends a header press: a relocation drag applies its move; anything else released
        // over the same header is a header click -- the sort, then ColumnHeaderMouseClick, which upstream
        // raises after the sort so a handler reads the new SortOrder (DGV-16).
        private void EndHeaderPress (MouseEventArgs e)
        {
            var pressed = header_press_column;
            header_press_column = -1;

            if (IsRelocatingColumn) {
                EndColumnRelocation (LogicalToDeviceUnits (e.Location.X));
                return;
            }

            if (pressed < 0 || pressed >= Columns.Count)
                return;

            var target = TargetAt (e.Location);

            if (!target.IsColumnHeader || target.ColumnIndex != pressed)
                return;

            // SortMode is the gate, not just this library's Sortable: a Programmatic column is one the app
            // sorts itself in the click handler, and sorting it here too sorted it twice (DGV-17).
            if (CanSortByHeaderClick (Columns[pressed]))
                OnColumnHeaderClick (pressed);

            OnColumnHeaderMouseClick (new DataGridViewCellMouseEventArgs (pressed, -1,
                target.CellRelative.X, target.CellRelative.Y, e));
        }
    }
}
