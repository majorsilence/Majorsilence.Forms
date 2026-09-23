namespace Majorsilence.Forms
{
    // The band/cell -> grid notification seams (W6.1, DGV-45). Sixteen of the grid's *Changed events
    // had protected raisers that nothing called: the properties they describe live on
    // DataGridViewColumn, DataGridViewRow and DataGridViewCell, whose setters stored the value and
    // told nobody. Upstream's setters call the owning grid's OnXxxChanged; these are the internal
    // doors that let ours do the same without making the raisers public.
    public partial class DataGridView
    {
        internal void NotifyColumnNameChanged (DataGridViewColumn c) => OnColumnNameChanged (new DataGridViewColumnEventArgs (c));

        // Error text for painting. Upstream asks the *ErrorTextNeeded handler only for a bound or virtual
        // grid, and only when one is attached; otherwise the stored ErrorText stands (W6 mechanisms).
        internal string ResolveCellErrorText (DataGridViewCell? cell, int rowIndex, int columnIndex)
        {
            var text = cell?.ErrorText ?? string.Empty;

            if (CellErrorTextNeeded is null || !(VirtualMode || DataSource is not null) || rowIndex < 0 || columnIndex < 0)
                return text;

            var e = new DataGridViewCellErrorTextNeededEventArgs (columnIndex, rowIndex, text);
            OnCellErrorTextNeeded (e);
            return e.ErrorText ?? string.Empty;
        }

        internal string ResolveRowErrorText (DataGridViewRow row, int rowIndex)
        {
            var text = row.ErrorText ?? string.Empty;

            if (RowErrorTextNeeded is null || !(VirtualMode || DataSource is not null) || rowIndex < 0)
                return text;

            var e = new DataGridViewRowErrorTextNeededEventArgs (rowIndex, text);
            OnRowErrorTextNeeded (e);
            return e.ErrorText ?? string.Empty;
        }
        internal void NotifyColumnContextMenuStripChanged (DataGridViewColumn c) => OnColumnContextMenuStripChanged (new DataGridViewColumnEventArgs (c));
        internal void NotifyRowContextMenuStripChanged (DataGridViewRow r) => OnRowContextMenuStripChanged (new DataGridViewRowEventArgs (r));
        internal void NotifyCellContextMenuStripChanged (DataGridViewCell c) => OnCellContextMenuStripChanged (new DataGridViewCellEventArgs (c.ColumnIndex, c.RowIndex));
        internal void NotifyColumnDataPropertyNameChanged (DataGridViewColumn c) => OnColumnDataPropertyNameChanged (new DataGridViewColumnEventArgs (c));
        internal void NotifyColumnToolTipTextChanged (DataGridViewColumn c) => OnColumnToolTipTextChanged (new DataGridViewColumnEventArgs (c));
        internal void NotifyColumnMinimumWidthChanged (DataGridViewColumn c) => OnColumnMinimumWidthChanged (new DataGridViewColumnEventArgs (c));
        internal void NotifyColumnDividerWidthChanged (DataGridViewColumn c) => OnColumnDividerWidthChanged (new DataGridViewColumnEventArgs (c));
        internal void NotifyColumnDefaultCellStyleChanged (DataGridViewColumn c) => OnColumnDefaultCellStyleChanged (new DataGridViewColumnEventArgs (c));
        internal void NotifyColumnHeaderCellChanged (DataGridViewColumn c) => OnColumnHeaderCellChanged (new DataGridViewColumnEventArgs (c));

        // One event for four flags, carrying WHICH flag moved -- a handler that redraws on Visible
        // must not have to guess from the column's current state which of them changed.
        internal void NotifyColumnStateChanged (DataGridViewColumn c, DataGridViewElementStates changed)
            => OnColumnStateChanged (new DataGridViewColumnStateChangedEventArgs (c, changed));

        internal void NotifyRowMinimumHeightChanged (DataGridViewRow r) => OnRowMinimumHeightChanged (new DataGridViewRowEventArgs (r));
        internal void NotifyRowDividerHeightChanged (DataGridViewRow r) => OnRowDividerHeightChanged (new DataGridViewRowEventArgs (r));
        internal void NotifyRowDefaultCellStyleChanged (DataGridViewRow r) => OnRowDefaultCellStyleChanged (new DataGridViewRowEventArgs (r));
        internal void NotifyRowErrorTextChanged (DataGridViewRow r) => OnRowErrorTextChanged (new DataGridViewRowEventArgs (r));
        internal void NotifyRowHeaderCellChanged (DataGridViewRow r) => OnRowHeaderCellChanged (new DataGridViewRowEventArgs (r));

        internal void NotifyCellStyleChanged (DataGridViewCell c) => OnCellStyleChanged (new DataGridViewCellEventArgs (c.ColumnIndex, c.RowIndex));
        internal void NotifyCellToolTipTextChanged (DataGridViewCell c) => OnCellToolTipTextChanged (new DataGridViewCellEventArgs (c.ColumnIndex, c.RowIndex));
        internal void NotifyCellErrorTextChanged (DataGridViewCell c) => OnCellErrorTextChanged (new DataGridViewCellEventArgs (c.ColumnIndex, c.RowIndex));
    }
}
