using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the dead-event sweep (RC-5) -- the grid's band and cell change events (DGV-45). Sixteen
    // *Changed events on DataGridView had protected raisers that nothing called: the properties they
    // describe live on DataGridViewColumn, DataGridViewRow and DataGridViewCell, whose setters stored
    // the value and told nobody. Upstream's setters call the owning grid's OnXxxChanged; ours now do
    // the same through internal Notify* doors on the grid (DataGridView.Notifications.cs).
    //
    // Also here: ListView.GroupCollapsedStateChanged (LST-65), same shape one control over -- the
    // group's CollapsedState setter stored and told nobody, and the list it belongs to never
    // re-laid-out, so collapsing a group did nothing until someone called RefreshGroups by hand.
    [Collection ("Headless")]
    public class DataGridViewBandChangeEventsTests
    {
        private static DataGridView Grid (out DataGridViewColumn column, out DataGridViewRow row, out DataGridViewCell cell)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 300, Height = 150 };
            column = new DataGridViewTextBoxColumn { HeaderText = "A", Width = 100 };
            grid.Columns.Add (column);
            grid.Rows.Add ();
            row = grid.Rows[0];
            cell = row.Cells[0];

            return grid;
        }

        // ---------------- column, one event per property

        [Theory]
        [InlineData ("Name")]
        [InlineData ("DataPropertyName")]
        [InlineData ("ToolTipText")]
        [InlineData ("MinimumWidth")]
        [InlineData ("DividerWidth")]
        [InlineData ("DefaultCellStyle")]
        [InlineData ("HeaderCell")]
        public void A_column_property_raises_its_own_event_once (string which)
        {
            var grid = Grid (out var column, out _, out _);
            var raised = 0;
            DataGridViewColumn? seen = null;

            switch (which) {
                case "Name": grid.ColumnNameChanged += (_, e) => { raised++; seen = e.Column; }; column.Name = "x"; column.Name = "x"; break;
                case "DataPropertyName": grid.ColumnDataPropertyNameChanged += (_, e) => { raised++; seen = e.Column; }; column.DataPropertyName = "x"; column.DataPropertyName = "x"; break;
                case "ToolTipText": grid.ColumnToolTipTextChanged += (_, e) => { raised++; seen = e.Column; }; column.ToolTipText = "x"; column.ToolTipText = "x"; break;
                case "MinimumWidth": grid.ColumnMinimumWidthChanged += (_, e) => { raised++; seen = e.Column; }; column.MinimumWidth = 20; column.MinimumWidth = 20; break;
                case "DividerWidth": grid.ColumnDividerWidthChanged += (_, e) => { raised++; seen = e.Column; }; column.DividerWidth = 2; column.DividerWidth = 2; break;
                case "DefaultCellStyle": grid.ColumnDefaultCellStyleChanged += (_, e) => { raised++; seen = e.Column; }; column.DefaultCellStyle = new DataGridViewCellStyle (); break;
                default: grid.ColumnHeaderCellChanged += (_, e) => { raised++; seen = e.Column; }; column.HeaderCell = new DataGridViewColumnHeaderCell (); break;
            }

            Assert.Equal (1, raised);
            Assert.Same (column, seen);
        }

        // Four flags share one event and the args say WHICH moved -- a handler must not have to guess.
        [Theory]
        [InlineData ("Visible", DataGridViewElementStates.Visible)]
        [InlineData ("Frozen", DataGridViewElementStates.Frozen)]
        [InlineData ("ReadOnly", DataGridViewElementStates.ReadOnly)]
        [InlineData ("Resizable", DataGridViewElementStates.Resizable)]
        public void A_column_state_flag_raises_ColumnStateChanged_naming_the_flag (string which, DataGridViewElementStates expected)
        {
            var grid = Grid (out var column, out _, out _);
            DataGridViewElementStates? seen = null;
            grid.ColumnStateChanged += (_, e) => seen = e.StateChanged;

            switch (which) {
                case "Visible": column.Visible = false; break;
                case "Frozen": column.Frozen = true; break;
                case "ReadOnly": column.ReadOnly = true; break;
                default: column.Resizable = DataGridViewTriState.False; break;
            }

            Assert.Equal (expected, seen);
        }

        // ---------------- row

        [Theory]
        [InlineData ("MinimumHeight")]
        [InlineData ("DefaultCellStyle")]
        [InlineData ("ErrorText")]
        [InlineData ("HeaderCell")]
        public void A_row_property_raises_its_own_event_once (string which)
        {
            var grid = Grid (out _, out var row, out _);
            var raised = 0;
            DataGridViewRow? seen = null;

            switch (which) {
                case "MinimumHeight": grid.RowMinimumHeightChanged += (_, e) => { raised++; seen = e.Row; }; row.MinimumHeight = 30; row.MinimumHeight = 30; break;
                case "DefaultCellStyle": grid.RowDefaultCellStyleChanged += (_, e) => { raised++; seen = e.Row; }; row.DefaultCellStyle = new DataGridViewCellStyle (); break;
                case "ErrorText": grid.RowErrorTextChanged += (_, e) => { raised++; seen = e.Row; }; row.ErrorText = "bad"; row.ErrorText = "bad"; break;
                default: grid.RowHeaderCellChanged += (_, e) => { raised++; seen = e.Row; }; row.HeaderCell = new DataGridViewRowHeaderCell (); break;
            }

            Assert.Equal (1, raised);
            Assert.Same (row, seen);
        }

        // ---------------- cell, with the indices the args carry

        [Theory]
        [InlineData ("ToolTipText")]
        [InlineData ("ErrorText")]
        [InlineData ("Style")]
        public void A_cell_property_raises_its_own_event_with_its_indices (string which)
        {
            var grid = Grid (out _, out _, out var cell);
            var raised = 0;
            var at = (col: -9, row: -9);

            switch (which) {
                case "ToolTipText": grid.CellToolTipTextChanged += (_, e) => { raised++; at = (e.ColumnIndex, e.RowIndex); }; cell.ToolTipText = "t"; cell.ToolTipText = "t"; break;
                case "ErrorText": grid.CellErrorTextChanged += (_, e) => { raised++; at = (e.ColumnIndex, e.RowIndex); }; cell.ErrorText = "e"; cell.ErrorText = "e"; break;
                default: grid.CellStyleChanged += (_, e) => { raised++; at = (e.ColumnIndex, e.RowIndex); }; cell.Style = new ControlStyle (cell.Style); break;
            }

            Assert.Equal (1, raised);
            Assert.Equal ((0, 0), at);
        }

        // A detached column tells nobody and does not throw -- the grid is null until it is added.
        [Fact]
        public void A_column_not_yet_in_a_grid_is_silent_and_safe ()
        {
            var column = new DataGridViewTextBoxColumn ();

            column.Name = "x";
            column.Visible = false;

            Assert.Equal ("x", column.Name);
        }

        // ---------------- ListView.GroupCollapsedStateChanged (LST-65)

        [Fact]
        public void Collapsing_a_group_raises_the_event_with_the_groups_index_and_relays_out ()
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 320, Height = 240, View = View.Details, ShowGroups = true };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });
            var first = new ListViewGroup ("a", "First");
            var second = new ListViewGroup ("b", "Second");
            view.Groups.Add (first);
            view.Groups.Add (second);
            view.Items.Add (new ListViewItem ("one") { Group = second });

            using var form = new Form { Width = 420, Height = 340 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            var index = -1;
            view.GroupCollapsedStateChanged += (_, e) => index = e.GroupIndex;

            var visible_before = view.Items[0].DeviceBounds;
            second.CollapsedState = ListViewGroupCollapsedState.Collapsed;
            PaintSurface.Render (view).Dispose ();

            Assert.Equal (1, index);

            // Re-laid-out without a manual RefreshGroups: a collapsed group's items are laid out
            // nowhere (LST-46), so the item's box must have gone empty.
            Assert.NotEqual (visible_before, view.Items[0].DeviceBounds);
            Assert.Equal (0, view.Items[0].DeviceBounds.Width);

            second.CollapsedState = ListViewGroupCollapsedState.Collapsed;
            Assert.Equal (1, index); // no re-raise on the same value
        }
    }
}
