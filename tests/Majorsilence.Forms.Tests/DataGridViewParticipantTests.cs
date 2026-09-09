using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.2 (findings DGV-02 P0, DGV-20 P0): the cell/row objects were passive stores. Setting
    // Cell.Value repainted and told nobody, and Row.Visible was read by nothing at all.
    //
    // Both are the silent kind: the state reads back exactly as assigned, so a property test passes
    // while the grid ignores it.
    [Collection ("Headless")]
    public class DataGridViewParticipantTests
    {
        private sealed class Item
        {
            public string Name { get; set; } = string.Empty;
            public int Qty { get; set; }
        }

        private static DataGridView Grid (int rows = 3, int columns = 2)
        {
            HeadlessRenderer.Use ();
            var grid = new DataGridView { Width = 400, Height = 200 };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Width = 80 });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            return grid;
        }

        // ---------------- DGV-02: a value change is an event, and reaches the bound object

        [Fact]
        public void Setting_a_cell_value_raises_CellValueChanged_once ()
        {
            using var grid = Grid ();
            var raised = new List<(int Row, int Column)> ();
            grid.CellValueChanged += (_, e) => raised.Add ((e.RowIndex, e.ColumnIndex));

            grid.Rows[1].Cells[0].Value = "changed";

            Assert.Single (raised);
            Assert.Equal ((1, 0), raised[0]);
        }

        [Fact]
        public void Setting_a_cell_to_the_value_it_already_has_raises_nothing ()
        {
            using var grid = Grid ();
            var raised = 0;
            grid.CellValueChanged += (_, _) => raised++;

            grid.Rows[1].Cells[0].Value = grid.Rows[1].Cells[0].Value;

            Assert.Equal (0, raised);
        }

        [Fact]
        public void Setting_a_cell_value_writes_through_to_the_bound_object ()
        {
            // The half that silently lost data: the grid showed the new value and the object kept the
            // old one, so the next ListChanged rebind reverted it.
            HeadlessRenderer.Use ();
            var items = new List<Item> { new () { Name = "a", Qty = 1 }, new () { Name = "b", Qty = 2 } };
            // AutoGenerateColumns is on by default and CLEARS the columns when a source is bound, so
            // the explicit DataPropertyName mapping only survives with it off.
            using var grid = new DataGridView { Width = 400, Height = 200, AutoGenerateColumns = false };
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "Name", DataPropertyName = nameof (Item.Name), Width = 80 });
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "Qty", DataPropertyName = nameof (Item.Qty), Width = 80 });
            grid.DataSource = items;

            grid.Rows[1].Cells[1].Value = 42;

            Assert.Equal (42, items[1].Qty);
            Assert.Equal (42, grid.Rows[1].Cells[1].Value);
        }

        [Fact]
        public void A_value_the_bound_member_cannot_take_is_reverted ()
        {
            // The push and the display must not disagree: if the object refuses the value, the grid
            // must not go on showing it.
            HeadlessRenderer.Use ();
            var items = new List<Item> { new () { Name = "a", Qty = 1 } };
            using var grid = new DataGridView { Width = 400, Height = 200, AutoGenerateColumns = false };
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "Qty", DataPropertyName = nameof (Item.Qty), Width = 80 });
            grid.DataSource = items;

            var before = grid.Rows[0].Cells[0].Value;
            var raised = 0;
            grid.CellValueChanged += (_, _) => raised++;

            grid.Rows[0].Cells[0].Value = "not a number";

            Assert.Equal (before, grid.Rows[0].Cells[0].Value);
            Assert.Equal (1, items[0].Qty);
            Assert.Equal (0, raised);            // nothing changed, so nothing is announced
        }

        [Fact]
        public void An_unbound_grid_still_raises_the_event ()
        {
            // GUARD, not proof: there is no object to push to, so the push cannot fail -- this pins
            // that the notification does not depend on a data source being present.
            using var grid = Grid ();
            var raised = 0;
            grid.CellValueChanged += (_, _) => raised++;

            grid.Rows[0].Cells[0].Value = "x";

            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_detached_cell_can_still_hold_a_value ()
        {
            // GUARD, not proof: a cell built by a designer or a test before it joins a row has no grid
            // to notify. It must store rather than throw.
            var cell = new DataGridViewCell ();

            cell.Value = "free";

            Assert.Equal ("free", cell.Value);
        }

        [Fact]
        public void Committing_an_edit_announces_the_change_exactly_once ()
        {
            // EndEdit assigns Value itself, so the notifying setter must not double-report. Both paths
            // now share one push implementation and notify from whichever the caller used.
            using var grid = Grid ();
            var raised = 0;
            grid.CellValueChanged += (_, _) => raised++;

            // BeginEdit (row, column) is the overload that actually creates the editor; the public
            // EditingControl property is a stub that returns null, so the editor is reached through the
            // child it adds to the grid.
            using var form = new Form { Width = 500, Height = 300 };
            form.Controls.Add (grid);
            grid.BeginEdit (0, 0);

            var editor = System.Linq.Enumerable.OfType<TextBox> (grid.Controls).FirstOrDefault ();
            Assert.NotNull (editor);

            editor!.Text = "typed";
            grid.EndEdit ();

            Assert.Equal (1, raised);
            Assert.Equal ("typed", grid.Rows[0].Cells[0].Value);
        }

        // ---------------- DGV-20: a hidden row is hidden

        [Fact]
        public void Hiding_a_row_takes_its_space_from_the_rows_below ()
        {
            using var grid = Grid ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();

            var second_top = grid.GetCellBounds (1, 0).Top;
            var third_top = grid.GetCellBounds (2, 0).Top;
            Assert.True (third_top > second_top, "rows should start at increasing offsets");

            grid.Rows[1].Visible = false;
            PaintSurface.Render (grid, 1f).Dispose ();

            // The third row moves up into the hidden row's place -- the filter idiom's whole point.
            Assert.Equal (second_top, grid.GetCellBounds (2, 0).Top);
        }

        [Fact]
        public void A_hidden_row_has_no_cell_rectangle ()
        {
            using var grid = Grid ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();
            Assert.NotEqual (Rectangle.Empty, grid.GetCellBounds (1, 0));

            grid.Rows[1].Visible = false;

            // Not a zero-height rect at the old position: an editor or context menu placed there would
            // appear against a row nobody can see.
            Assert.Equal (Rectangle.Empty, grid.GetCellBounds (1, 0));
        }

        [Fact]
        public void A_hidden_row_is_not_hit_tested ()
        {
            using var grid = Grid ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();

            var y = grid.GetCellBounds (1, 0).Top + 2;
            Assert.Equal (1, grid.GetRowAtLocation (new Point (grid.GetCellBounds (1, 0).Left + 2, y)));

            grid.Rows[1].Visible = false;
            PaintSurface.Render (grid, 1f).Dispose ();

            // The row now at that position is the one that moved up, not the hidden one.
            Assert.Equal (2, grid.GetRowAtLocation (new Point (grid.GetCellBounds (2, 0).Left + 2, y)));
        }

        [Fact]
        public void A_hidden_row_is_not_counted_as_displayed ()
        {
            using var grid = Grid (rows: 3);
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();

            var before = grid.DisplayedRowCount (false);
            Assert.True (before >= 3, $"all three rows should fit; got {before}");

            grid.Rows[1].Visible = false;

            Assert.Equal (before - 1, grid.DisplayedRowCount (false));
        }

        [Fact]
        public void A_hidden_row_paints_nothing_and_keeps_no_stale_bounds ()
        {
            using var grid = Grid ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();
            Assert.NotEqual (Rectangle.Empty, grid.Rows[1].Bounds);

            grid.Rows[1].Visible = false;
            PaintSurface.Render (grid, 1f).Dispose ();

            // A stale rectangle from when the row was visible is worse than none: it is what the
            // renderer and any bounds-based lookup would keep using.
            Assert.Equal (Rectangle.Empty, grid.Rows[1].Bounds);
        }

        [Fact]
        public void Showing_the_row_again_restores_it ()
        {
            using var grid = Grid ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();
            var original = grid.GetCellBounds (1, 0);

            grid.Rows[1].Visible = false;
            PaintSurface.Render (grid, 1f).Dispose ();
            grid.Rows[1].Visible = true;
            PaintSurface.Render (grid, 1f).Dispose ();

            Assert.Equal (original, grid.GetCellBounds (1, 0));
        }

        [Fact]
        public void Visible_reads_back_what_was_set ()
        {
            // GUARD, not proof: the auto-property this replaced round-tripped correctly -- that was the
            // entire problem. It pins that giving the setter behaviour did not lose the state.
            using var grid = Grid ();

            grid.Rows[1].Visible = false;

            Assert.False (grid.Rows[1].Visible);
            Assert.True (grid.Rows[0].Visible);
        }
    }
}
