using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Majorsilence.Forms
{
    // W5.3 (findings DGV-31 P0, DGV-03 P0, DGV-32, DGV-33): incremental data binding.
    //
    // OnBoundListChanged ignored ListChangedType and re-ran the whole bind on any change -- Rows.Clear,
    // and with AutoGenerateColumns a Columns.Clear and regenerate too. Three consequences, each a
    // finding on its own:
    //
    //  * grid.DataSource = list; grid.Columns["Id"].Visible = false; grid.Columns["Name"].HeaderText =
    //    "Customer" -- and the first add, edit or delete in the list silently put the auto-generated
    //    columns back. Every INotifyPropertyChanged edit, including the grid's OWN write-back from
    //    EndEdit, rebuilt N rows and lost every DataGridViewRow reference, Tag, Height and per-row
    //    style the app had set. And since W5.2 it also threw away the selection and Row.Visible state
    //    those items made real (DGV-31).
    //  * A rebind raised RowsRemoved (0, N) from Rows.Clear and no RowsAdded at all from ReplaceAll, so
    //    counters went negative and the row-colouring RowsAdded handler never ran for bound rows
    //    (DGV-33).
    //  * DataSource's getter returned the RESOLVED list -- a DataView for a DataTable -- so
    //    ((DataTable)grid.DataSource) threw; DataMember was stored and never consulted; and a source
    //    the switch did not recognise silently kept the PREVIOUS list on screen (DGV-32).
    //
    // The fix keeps the bound schema -- the descriptors or properties the columns were generated from
    // -- so a single row can be built or refreshed from a single item, and switches on the change type.
    public partial class DataGridView
    {
        // What the caller assigned, as distinct from the list it resolved to. WinForms returns this
        // from the getter, and app code casts it back to what it assigned.
        private object? data_source_object;

        // The schema the current rows were built against. Exactly one of these is non-null while bound:
        // descriptors for an ITypedList source (DataView, BindingSource), CLR properties for a plain
        // list. Kept so ItemAdded/ItemChanged can build or refresh ONE row the same way the full bind
        // built all of them, rather than re-deriving the schema -- which is the moment a rebuild sneaks
        // back in.
        private PropertyDescriptorCollection? bound_descriptors;
        private PropertyInfo[]? bound_properties;

        // ---------------- DGV-32: what a source resolves to

        // Resolves the assigned object and member to the list the grid binds. Throws for a source that
        // is not list-like, where it used to keep whatever list was already bound and say nothing.
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "Following a data member is reflection over application types, exactly as upstream; a trimmed member falls through to the ArgumentException below.")]
        private static IList? ResolveDataSource (object? source, string member)
        {
            if (source is null)
                return null;

            // DataSet + DataMember is the classic ADO.NET shape, and the table name is not a CLR
            // property, so it is taken before the general path -- which would look for one.
            if (source is System.Data.DataSet set && !string.IsNullOrEmpty (member))
                return set.Tables[member]?.DefaultView
                    ?? throw new ArgumentException ($"The DataSet has no table named '{member}'.", nameof (member));

            var list = string.IsNullOrEmpty (member)
                ? ListBindingHelper.GetList (source)
                : ListBindingHelper.GetList (source, member);

            return list switch {
                null => null,
                IList l => l,
                System.Data.DataTable table => table.DefaultView,
                IListSource list_source => list_source.GetList (),
                // A LINQ query or other IEnumerable<T>: materialise it, the way BindingSource does. The
                // grid cannot watch it for changes, but it can at least show it.
                IEnumerable enumerable => enumerable.Cast<object> ().ToList (),
                _ => throw new ArgumentException (
                    "Complex data binding accepts an IList, an IListSource, or an IEnumerable as the data source.",
                    nameof (source)),
            };
        }

        // Re-resolves and rebinds. Both DataSource and DataMember setters land here so the two cannot
        // disagree about what is bound.
        private void RebindDataSource ()
        {
            DetachFromBoundList ();
            data_source = ResolveDataSource (data_source_object, data_member);
            AttachToBoundList ();
            OnDataSourceChanged ();
        }

        // ---------------- DGV-31: one row from one item

        // Builds the row for an item using the schema the columns were generated from. Shared by the
        // full bind and the ItemAdded path, so the two cannot drift apart.
        private DataGridViewRow BuildBoundRow (object item)
        {
            // From the template, so RowTemplate.Height -- the designer's way to set row height --
            // applies to bound rows too (DGV-19). FillBoundRow adds the cells.
            var row = (DataGridViewRow)row_template.Clone ();
            row.Cells.Clear ();
            FillBoundRow (row, item);
            row.DataBoundItem = item;
            return row;
        }

        // Writes an item's values into a row's cells, adding cells if the row is new. Values are TYPED,
        // not stringified -- WinForms cell values keep the bound member's type so handlers can cast.
        private void FillBoundRow (DataGridViewRow row, object item)
        {
            if (bound_descriptors is not null && AutoGenerateColumns) {
                for (var i = 0; i < bound_descriptors.Count; i++)
                    SetBoundCell (row, i, bound_descriptors[i].GetValue (item));

                return;
            }

            if (bound_properties is not null && AutoGenerateColumns) {
                for (var i = 0; i < bound_properties.Length; i++)
                    SetBoundCell (row, i, bound_properties[i].GetValue (item));

                return;
            }

            // Columns the caller defined: fill them in THEIR order, from the member each one names.
            for (var i = 0; i < Columns.Count; i++) {
                var column = Columns[i];
                var member = string.IsNullOrEmpty (column.DataPropertyName) ? column.HeaderText : column.DataPropertyName;
                SetBoundCell (row, i, ReadMember (item, member));
            }
        }

        // The source is telling the grid a value, not the other way round -- so this must not run the
        // write-back or raise CellValueChanged, both of which the Value setter does since W5.2a.
        private void SetBoundCell (DataGridViewRow row, int columnIndex, object? value)
        {
            while (row.Cells.Count <= columnIndex)
                row.Cells.Add (new DataGridViewCell ());

            var cell = row.Cells[columnIndex];

            if (Equals (cell.Value, value))
                return;

            suppress_cell_value_notification = true;

            try {
                cell.Value = value;
            } finally {
                suppress_cell_value_notification = false;
            }
        }

        // Whether the columns on the grid were generated from this schema. Decides, on Reset, whether
        // the columns must be regenerated (the schema changed -- a BindingSource that resolved to a
        // different table) or must be LEFT ALONE (same schema; the app's header renames and hidden
        // columns are exactly what a rebuild used to destroy).
        private bool ColumnsMatchSchema (PropertyDescriptorCollection descriptors)
        {
            if (Columns.Count != descriptors.Count)
                return false;

            for (var i = 0; i < descriptors.Count; i++)
                if (!string.Equals (Columns[i].DataPropertyName, descriptors[i].Name, StringComparison.Ordinal))
                    return false;

            return true;
        }

        private bool ColumnsMatchSchema (PropertyInfo[] properties)
        {
            if (Columns.Count != properties.Length)
                return false;

            for (var i = 0; i < properties.Length; i++)
                if (!string.Equals (Columns[i].DataPropertyName, properties[i].Name, StringComparison.Ordinal))
                    return false;

            return true;
        }

        // ---------------- DGV-31: honour the change type

        // Keeps the grid in step with a source that changes after binding.
        //
        // This is the normal case, not an edge case: designer code assigns DataSource inside
        // InitializeComponent, and the form loads its data afterwards -- so at bind time the source is
        // routinely an empty list with no schema yet.
        //
        // Each change type touches only what it names. A full rebuild is reserved for Reset (and only
        // regenerates columns when the schema actually changed) and for the PropertyDescriptor*
        // changes, which ARE schema changes.
        private void OnBoundListChanged (object? sender, ListChangedEventArgs e)
        {
            if (IsDisposed || data_source is null)
                return;

            switch (e.ListChangedType) {
                case ListChangedType.ItemAdded when e.NewIndex >= 0 && e.NewIndex <= Rows.Count && e.NewIndex < data_source.Count:
                    if (data_source[e.NewIndex] is { } added)
                        Rows.InsertBound (e.NewIndex, BuildBoundRow (added));

                    // The first row to arrive in an empty grid becomes current, as it does on bind.
                    SetInitialCurrentCell ();
                    break;

                case ListChangedType.ItemDeleted when e.NewIndex >= 0 && e.NewIndex < Rows.Count:
                    Rows.RemoveBound (e.NewIndex);
                    ClampCurrentCellToRows ();
                    break;

                case ListChangedType.ItemChanged when e.NewIndex >= 0 && e.NewIndex < Rows.Count && e.NewIndex < data_source.Count:
                    if (data_source[e.NewIndex] is { } changed) {
                        FillBoundRow (Rows[e.NewIndex], changed);
                        Rows[e.NewIndex].DataBoundItem = changed;
                    }

                    break;

                case ListChangedType.ItemMoved when e.OldIndex >= 0 && e.OldIndex < Rows.Count && e.NewIndex >= 0 && e.NewIndex < Rows.Count:
                    Rows.MoveBound (e.OldIndex, e.NewIndex);
                    break;

                case ListChangedType.Reset:
                    OnDataSourceChanged ();
                    break;

                default:
                    // PropertyDescriptorAdded/Changed/Deleted, and any indexed change whose index the
                    // grid cannot reconcile: the schema, or the grid's picture of the list, is stale.
                    OnDataSourceChanged (forceColumnRegeneration: true);
                    break;
            }

            Invalidate ();
        }

        // A deleted row can take the current cell with it. Upstream moves the current cell to the row
        // now at that index, or the last row, and raises the move like any other.
        private void ClampCurrentCellToRows ()
        {
            if (selected_row_index < Rows.Count)
                return;

            var target = Rows.Count - 1;

            if (target < 0) {
                selected_row_index = -1;
                OnCurrentCellChanged (EventArgs.Empty);
                return;
            }

            MoveCurrentCell (target, selected_column_index);
            ReplaceSelectionWithCurrentCell ();
        }
    }
}
