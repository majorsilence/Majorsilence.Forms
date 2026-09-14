using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Majorsilence.Forms
{
    // W5.4, the sorting half (findings DGV-16, DGV-17).
    //
    // SortedColumn and SortOrder had private setters that nothing assigned, so the standard toggle
    // `if (grid.SortedColumn == col && grid.SortOrder == Ascending) …` always saw null/None. Sorted was
    // never raised, so a handler that re-selects the row after a sort never ran. SortCompare was
    // `add { } remove { }`, so a natural-order or numeric-string comparer was silently ignored. The
    // header click gated on the non-WinForms Column.Sortable and never on SortMode, so a Programmatic
    // column -- the one an app sorts itself in the click handler -- got sorted twice. And a bound grid
    // was sorted by reordering ITS rows, which the next ListChanged put straight back.
    public partial class DataGridView
    {
        private EventHandler<DataGridViewSortCompareEventArgs>? sort_compare;

        /// <summary>
        /// Raised for each pair of cells compared during an unbound sort, so a handler can supply its own
        /// ordering: set <see cref="DataGridViewSortCompareEventArgs.SortResult"/> and
        /// <c>Handled = true</c>.
        /// </summary>
        public event EventHandler<DataGridViewSortCompareEventArgs>? SortCompare {
            add => sort_compare += value;
            remove => sort_compare -= value;
        }

        /// <summary>Raises the <see cref="SortCompare"/> event.</summary>
        protected virtual void OnSortCompare (DataGridViewSortCompareEventArgs e) => sort_compare?.Invoke (this, e);

        /// <summary>Raises the <see cref="Sorted"/> event.</summary>
        protected virtual void OnSorted (EventArgs e) => Sorted?.Invoke (this, e);

        /// <summary>Gets the column the grid is currently sorted by, or null.</summary>
        public DataGridViewColumn? SortedColumn { get; private set; }

        /// <summary>Gets the direction of the current sort, or <see cref="SortOrder.None"/>.</summary>
        public SortOrder SortOrder { get; private set; } = SortOrder.None;

        /// <summary>
        /// Sorts the rows by the specified column. Records <see cref="SortedColumn"/> and
        /// <see cref="SortOrder"/>, moves the sort glyph to that column's header, and raises
        /// <see cref="Sorted"/>. A bound list that supports sorting is asked to sort itself, so the order
        /// survives the next change to the list.
        /// </summary>
        public void SortByColumn (int columnIndex, SortOrder order)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count || order == SortOrder.None)
                return;

            var column = Columns[columnIndex];

            // Recorded BEFORE the rows move, so a Sorted handler -- and a ColumnHeaderMouseClick handler
            // reading grid.SortOrder -- sees the new order, not the previous one (the ordering trap
            // DGV-16 records).
            SortedColumn = column;
            SortOrder = order;

            // One glyph on the grid at a time. Column.SortOrder is the header cell's glyph (see the
            // column), so clearing it here clears what the renderer draws.
            foreach (var other in Columns)
                other.HeaderCell.SortGlyphDirection = SortOrder.None;

            column.HeaderCell.SortGlyphDirection = order;

            if (!TrySortBoundList (column, order) && Rows.Count > 0)
                SortRowsLocally (columnIndex, order);

            OnSorted (EventArgs.Empty);
            Invalidate ();
        }

        /// <summary>Sorts the data by the specified column in the specified direction.</summary>
        public void Sort (DataGridViewColumn column, ListSortDirection direction)
        {
            Guard.ThrowIfNull (column);

            var idx = Columns.IndexOf (column);

            if (idx >= 0)
                SortByColumn (idx, direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending);
        }

        // A bound grid does not own its order: reordering Rows appeared sorted until the next
        // ListChanged rebound them from the source, in source order. The list is asked to sort itself
        // instead (a DataView honours it; so does BindingSource over one), and the Reset it raises
        // brings the rows back in the sorted order (DGV-17).
        private bool TrySortBoundList (DataGridViewColumn column, SortOrder order)
        {
            if (bound_list is not IBindingList { SupportsSorting: true } list)
                return false;

            var member = string.IsNullOrEmpty (column.DataPropertyName) ? column.HeaderText : column.DataPropertyName;

            if (string.IsNullOrEmpty (member))
                return false;

            var descriptor = (list as ITypedList)?.GetItemProperties (null)[member]
                ?? (bound_descriptors is not null ? bound_descriptors[member] : null);

            if (descriptor is null)
                return false;

            try {
                list.ApplySort (descriptor, order == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
                return true;
            } catch (NotSupportedException) {
                // SupportsSorting lied, which IBindingList implementations occasionally do. Fall back to
                // sorting the rows rather than leaving the grid unsorted.
                return false;
            }
        }

        // An unbound grid owns its rows, so they are reordered directly -- through SortCompare when a
        // handler is attached, so a custom comparer is consulted for every pair the way upstream's
        // OnSortCompare is.
        private void SortRowsLocally (int columnIndex, SortOrder order)
        {
            var column = Columns[columnIndex];
            var indexed = Rows.Select ((row, index) => (row, index)).ToList ();

            // List.Sort is not stable; the original index breaks ties so equal keys keep their order.
            indexed.Sort ((a, b) => {
                var raw_a = columnIndex < a.row.Cells.Count ? a.row.Cells[columnIndex].Value : null;
                var raw_b = columnIndex < b.row.Cells.Count ? b.row.Cells[columnIndex].Value : null;

                var cmp = CompareForSort (column, raw_a, raw_b, a.index, b.index);

                if (cmp == 0)
                    cmp = a.index.CompareTo (b.index);

                return order == SortOrder.Descending ? -cmp : cmp;
            });

            // Replace rows without per-item notifications; the row OBJECTS move, so selection, Visible,
            // Tag and Height travel with them.
            Rows.ReplaceAll (indexed.Select (e => e.row).ToList ());
        }

        private int CompareForSort (DataGridViewColumn column, object? a, object? b, int rowA, int rowB)
        {
            if (sort_compare is not null) {
                var args = new DataGridViewSortCompareEventArgs (column, a, b, rowA, rowB);
                OnSortCompare (args);

                // Handled is CancelEventArgs.Cancel on this type, as upstream.
                if (args.Cancel)
                    return args.SortResult;
            }

            return CompareCellValues (a, b);
        }

        // The header click's sort gate. SortMode is what upstream consults; Column.Sortable is this
        // library's older knob and is kept as an additional veto so grids that already turned it off
        // stay unsorted.
        internal bool CanSortByHeaderClick (DataGridViewColumn column)
            => column.SortMode == DataGridViewColumnSortMode.Automatic && column.Sortable;
    }
}
