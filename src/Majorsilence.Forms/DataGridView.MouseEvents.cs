using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // W5.5, the mouse half (findings DGV-30, DGV-25).
    //
    // Eight cell-level mouse events were `add { } remove { }` -- they took a handler and discarded it,
    // so migrated source compiled and the handler never ran. The protected OnCellMouseDown/Up/Move were
    // empty method bodies nothing called. And the three events that WERE raised fired from
    // OnMouseDown, where upstream raises them on mouse-UP.
    //
    // The idiom that matters most:
    //
    //     grid.CellMouseDown += (s, e) => {
    //         if (e.Button == MouseButtons.Right)
    //             grid.CurrentCell = grid[e.ColumnIndex, e.RowIndex];   // context menu on the clicked row
    //     };
    //
    // Never ran, so a right-click context menu acted on whatever row happened to be current.
    public partial class DataGridView
    {
        private DataGridViewCellMouseEventHandler? _cellMouseDown;
        private DataGridViewCellMouseEventHandler? _cellMouseUp;
        private DataGridViewCellMouseEventHandler? _cellMouseMove;
        private DataGridViewCellMouseEventHandler? _cellMouseDoubleClick;
        private DataGridViewCellMouseEventHandler? _rowHeaderMouseClick;
        private DataGridViewCellMouseEventHandler? _rowHeaderMouseDoubleClick;
        private DataGridViewCellMouseEventHandler? _columnHeaderMouseDoubleClick;
        private DataGridViewCellEventHandler? _cellContentDoubleClick;

        /// <summary>Raised when a mouse button goes down over a cell.</summary>
        public event DataGridViewCellMouseEventHandler? CellMouseDown {
            add => _cellMouseDown += value;
            remove => _cellMouseDown -= value;
        }

        /// <summary>Raised when a mouse button is released over a cell.</summary>
        public event DataGridViewCellMouseEventHandler? CellMouseUp {
            add => _cellMouseUp += value;
            remove => _cellMouseUp -= value;
        }

        /// <summary>Raised as the pointer moves over a cell.</summary>
        public event DataGridViewCellMouseEventHandler? CellMouseMove {
            add => _cellMouseMove += value;
            remove => _cellMouseMove -= value;
        }

        /// <summary>Raised when a cell is double-clicked.</summary>
        public event DataGridViewCellMouseEventHandler? CellMouseDoubleClick {
            add => _cellMouseDoubleClick += value;
            remove => _cellMouseDoubleClick -= value;
        }

        /// <summary>Raised when a row header is clicked. <c>ColumnIndex</c> is -1.</summary>
        public event DataGridViewCellMouseEventHandler? RowHeaderMouseClick {
            add => _rowHeaderMouseClick += value;
            remove => _rowHeaderMouseClick -= value;
        }

        /// <summary>Raised when a row header is double-clicked. <c>ColumnIndex</c> is -1.</summary>
        public event DataGridViewCellMouseEventHandler? RowHeaderMouseDoubleClick {
            add => _rowHeaderMouseDoubleClick += value;
            remove => _rowHeaderMouseDoubleClick -= value;
        }

        /// <summary>Raised when a column header is double-clicked. <c>RowIndex</c> is -1.</summary>
        public event DataGridViewCellMouseEventHandler? ColumnHeaderMouseDoubleClick {
            add => _columnHeaderMouseDoubleClick += value;
            remove => _columnHeaderMouseDoubleClick -= value;
        }

        /// <summary>Raised when a cell's content is double-clicked.</summary>
        public event DataGridViewCellEventHandler? CellContentDoubleClick {
            add => _cellContentDoubleClick += value;
            remove => _cellContentDoubleClick -= value;
        }

        /// <summary>Raises the <see cref="CellMouseDown"/> event.</summary>
        protected virtual void OnCellMouseDown (DataGridViewCellMouseEventArgs e) => _cellMouseDown?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellMouseUp"/> event.</summary>
        protected virtual void OnCellMouseUp (DataGridViewCellMouseEventArgs e) => _cellMouseUp?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellMouseMove"/> event.</summary>
        protected virtual void OnCellMouseMove (DataGridViewCellMouseEventArgs e) => _cellMouseMove?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellMouseDoubleClick"/> event.</summary>
        protected virtual void OnCellMouseDoubleClick (DataGridViewCellMouseEventArgs e) => _cellMouseDoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeaderMouseClick"/> event.</summary>
        protected virtual void OnRowHeaderMouseClick (DataGridViewCellMouseEventArgs e) => _rowHeaderMouseClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="RowHeaderMouseDoubleClick"/> event.</summary>
        protected virtual void OnRowHeaderMouseDoubleClick (DataGridViewCellMouseEventArgs e) => _rowHeaderMouseDoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnHeaderMouseDoubleClick"/> event.</summary>
        protected virtual void OnColumnHeaderMouseDoubleClick (DataGridViewCellMouseEventArgs e) => _columnHeaderMouseDoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellContentDoubleClick"/> event.</summary>
        protected virtual void OnCellContentDoubleClick (DataGridViewCellEventArgs e) => _cellContentDoubleClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="CellClick"/> event.</summary>
        protected virtual void OnCellClick (DataGridViewCellEventArgs e) => CellClick?.Invoke (this, e);

        // ---------------- where a point landed

        // The cell (or header) under a point, in the shape the events want: -1 for a header index, and
        // coordinates relative to the cell rather than the control.
        internal readonly struct MouseTarget
        {
            internal MouseTarget (int rowIndex, int columnIndex, Point cellRelative, bool isRowHeader, bool isColumnHeader)
            {
                RowIndex = rowIndex;
                ColumnIndex = columnIndex;
                CellRelative = cellRelative;
                IsRowHeader = isRowHeader;
                IsColumnHeader = isColumnHeader;
            }

            internal int RowIndex { get; }
            internal int ColumnIndex { get; }
            internal Point CellRelative { get; }
            internal bool IsRowHeader { get; }
            internal bool IsColumnHeader { get; }

            // A point over a data cell: both indices real. A header has one of them at -1.
            internal bool IsCell => RowIndex >= 0 && ColumnIndex >= 0 && !IsRowHeader && !IsColumnHeader;
        }

        internal MouseTarget TargetAt (Point location)
        {
            var client = GetContentArea ();

            if (ColumnHeadersVisible
                && new Rectangle (client.Left, client.Top, client.Width, ScaledHeaderHeight).Contains (location)) {
                var header_column = GetColumnAtLocation (location);

                return new MouseTarget (-1, header_column,
                    new Point (location.X - (header_column >= 0 ? GetColumnDeviceLeft (header_column) : client.Left), location.Y - client.Top),
                    isRowHeader: false, isColumnHeader: true);
            }

            var row = GetRowAtLocation (location);

            // The row-header band, where upstream reports ColumnIndex -1. GetRowAtLocation already
            // answers for it, so the band is decided by the x alone.
            if (row_headers_visible && location.X < client.Left + ScaledRowHeadersWidth) {
                var row_bounds = row >= 0 ? Rows[row].Bounds : Rectangle.Empty;

                return new MouseTarget (row, -1,
                    new Point (location.X - client.Left, location.Y - (row_bounds.IsEmpty ? client.Top : row_bounds.Top)),
                    isRowHeader: true, isColumnHeader: false);
            }

            var column = GetColumnAtLocation (location);
            var cell_bounds = row >= 0 && column >= 0 ? GetCellBounds (row, column) : Rectangle.Empty;

            return new MouseTarget (row, column,
                cell_bounds.IsEmpty ? location : new Point (location.X - cell_bounds.Left, location.Y - cell_bounds.Top),
                isRowHeader: false, isColumnHeader: false);
        }

        private DataGridViewCellMouseEventArgs MouseArgs (MouseTarget target, MouseEventArgs e)
            => new DataGridViewCellMouseEventArgs (target.ColumnIndex, target.RowIndex, target.CellRelative.X, target.CellRelative.Y, e);

        // ---------------- the pipeline

        // Raised from OnMouseDown, before anything the click does: upstream reports the press, then acts.
        private void RaiseCellMouseDown (MouseEventArgs e)
        {
            var target = TargetAt (e.Location);

            if (target.IsCell)
                OnCellMouseDown (MouseArgs (target, e));
        }

        // Raised from OnMouseUp. CellClick, CellMouseClick and CellContentClick live here too, not on
        // the press: upstream raises them on release, and a handler that acts on a click should not run
        // while the button is still down (DGV-30).
        private void RaiseCellMouseUp (MouseEventArgs e)
        {
            var target = TargetAt (e.Location);

            if (target.IsRowHeader) {
                OnRowHeaderMouseClick (MouseArgs (target, e));
                return;
            }

            if (!target.IsCell)
                return;

            var args = MouseArgs (target, e);
            var cell_args = new DataGridViewCellEventArgs (target.ColumnIndex, target.RowIndex);

            OnCellMouseUp (args);

            // A click is a press and a release on the SAME cell. Pressing on one row and releasing on
            // another is a drag, and upstream does not call that a click on either.
            if (mouse_down_target is not { } pressed || pressed.RowIndex != target.RowIndex || pressed.ColumnIndex != target.ColumnIndex)
                return;

            // The check-box toggle commits here, so the value CellClick handlers read is the new one.
            ToggleCheckBoxCell (target.RowIndex, target.ColumnIndex);

            // TrackVisitedState: clicking a link marks it visited, which is the only thing that ever
            // sets LinkVisited in an ordinary application -- without it the visited colour was
            // reachable only by assigning LinkVisited by hand, so the whole visited/unvisited
            // distinction was inert in practice even after the colours were wired (DGV-43).
            MarkLinkVisited (target.RowIndex, target.ColumnIndex);

            OnCellClick (cell_args);
            OnCellMouseClick (args);
            OnCellContentClick (cell_args);
        }

        private MouseTarget? mouse_down_target;

        /// <summary>Whether the mouse is currently held down on this cell.</summary>
        /// <remarks>
        /// The renderer's window onto <see cref="mouse_down_target"/>, which is what makes
        /// <see cref="DataGridViewLinkCell.ActiveLinkColor"/> expressible here -- the strip has no
        /// equivalent, which is why its own ActiveLinkColor stays unwired (TSM-42).
        /// </remarks>
        internal bool IsPressedCell (int rowIndex, int columnIndex)
            => mouse_down_target is { IsCell: true } pressed
               && pressed.RowIndex == rowIndex
               && pressed.ColumnIndex == columnIndex;

        // The cell's own TrackVisitedState wins; a cell created by the column inherits the column's
        // through CopyStateTo, so reading the cell alone is right for both.
        private void MarkLinkVisited (int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count || columnIndex < 0 || columnIndex >= Rows[rowIndex].Cells.Count)
                return;

            if (Rows[rowIndex].Cells[columnIndex] is not DataGridViewLinkCell { TrackVisitedState: true, LinkVisited: false } link)
                return;

            link.LinkVisited = true;

            Invalidate ();
        }

        private void RaiseCellMouseMove (MouseEventArgs e)
        {
            var target = TargetAt (e.Location);

            if (target.IsCell)
                OnCellMouseMove (MouseArgs (target, e));
        }

        private void RaiseCellDoubleClick (MouseEventArgs e)
        {
            var target = TargetAt (e.Location);

            if (target.IsColumnHeader && target.ColumnIndex >= 0) {
                OnColumnHeaderMouseDoubleClick (MouseArgs (target, e));
                return;
            }

            if (target.IsRowHeader) {
                OnRowHeaderMouseDoubleClick (MouseArgs (target, e));
                return;
            }

            if (!target.IsCell)
                return;

            OnCellMouseDoubleClick (MouseArgs (target, e));
            OnCellContentDoubleClick (new DataGridViewCellEventArgs (target.ColumnIndex, target.RowIndex));
        }

        // ---------------- DGV-25: the check box commits what it shows

        /// <summary>
        /// Whether a cell's value counts as checked, resolved through the column's
        /// <see cref="DataGridViewCheckBoxColumn.TrueValue"/> when it sets one. A `char` flag column
        /// (<c>TrueValue = "Y"</c>) is the case the old <c>"True"</c>/<c>"1"</c> string test could not
        /// answer (DGV-25).
        /// </summary>
        // `cell` is the one being tested, when there is one. DataGridViewCheckBoxCell.TrueValue and its
        // siblings were stored and read by nothing -- only the COLUMN's were consulted -- so a cell that
        // overrode the column's mapping was ticked by the column's rule instead. Upstream lets the
        // cell's value win, which is the point of having it per cell.
        internal static bool IsCheckedValue (DataGridViewColumn column, object? value, DataGridViewCell? cell = null)
        {
            if (cell is DataGridViewCheckBoxCell { TrueValue: { } cell_yes })
                return Matches (value, cell_yes);

            if (cell is DataGridViewCheckBoxCell { FalseValue: { } cell_no } && Matches (value, cell_no))
                return false;

            if (column is DataGridViewCheckBoxColumn { TrueValue: { } yes })
                return Equals (value, yes) || string.Equals (value?.ToString (), yes.ToString (), StringComparison.OrdinalIgnoreCase);

            if (value is bool b)
                return b;

            var text = value?.ToString ();

            return string.Equals (text, "True", StringComparison.OrdinalIgnoreCase) || text == "1";
        }

        // The same loose comparison the column path has always used: a bound "Y" column stores a
        // string, and the configured value may be typed differently from what the binding produced.
        private static bool Matches (object? value, object expected)
            => Equals (value, expected)
            || string.Equals (value?.ToString (), expected.ToString (), StringComparison.OrdinalIgnoreCase);

        // Flips a check-box cell and commits it through the same write-back an edit uses, so a bound
        // object actually changes. It used to assign a bool straight into the cell and announce it,
        // which left a bound item untouched -- the next ListChanged reverted the tick -- and wrote a
        // bool into a string column.
        private void ToggleCheckBoxCell (int rowIndex, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count || rowIndex < 0 || rowIndex >= Rows.Count)
                return;

            var column = Columns[columnIndex];

            if (column is not DataGridViewCheckBoxColumn && !column.DisplaysAsCheckBox)
                return;

            if (columnIndex >= Rows[rowIndex].Cells.Count)
                return;

            // The grid, the column, the row and the cell each get a veto -- the same IsCellEditable the
            // edit path uses (DGV-07). Only cell.ReadOnly was checked, so grid.ReadOnly = true still
            // toggled.
            if (!IsCellEditable (rowIndex, columnIndex))
                return;

            var cell = Rows[rowIndex].Cells[columnIndex];
            var now_checked = !IsCheckedValue (column, cell.Value, cell);
            var next = NextCheckBoxValue (column, now_checked, cell);

            // Through the notifying setter, which pushes to the bound item and raises CellValueChanged
            // (W5.2a). Marked dirty first so a CurrentCellDirtyStateChanged handler -- the canonical
            // commit-a-checkbox-immediately idiom -- sees it (DGV-08).
            NotifyCurrentCellDirty (true);
            cell.Value = next;
            NotifyCurrentCellDirty (false);
        }

        // The value to store for a state, honouring the column's TrueValue/FalseValue so a "Y"/"N"
        // column stores "Y"/"N" rather than a bool.
        private static object? NextCheckBoxValue (DataGridViewColumn column, bool isChecked, DataGridViewCell? cell = null)
        {
            // The cell's own mapping wins over the column's, for the same reason it does when reading.
            if (cell is DataGridViewCheckBoxCell per_cell) {
                if (isChecked && per_cell.TrueValue is { } cell_yes)
                    return cell_yes;

                if (!isChecked && per_cell.FalseValue is { } cell_no)
                    return cell_no;
            }

            if (column is DataGridViewCheckBoxColumn box) {
                if (isChecked && box.TrueValue is { } yes)
                    return yes;

                if (!isChecked && box.FalseValue is { } no)
                    return no;
            }

            return isChecked;
        }
    }
}
