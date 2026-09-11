using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.2b (findings DGV-14, DGV-15): the selection model.
    //
    // Row.Selected and Cell.Selected were auto-properties nothing read -- the renderer highlighted
    // SelectedRowIndex -- so "delete the selected rows" loops deleted one row, programmatic selection
    // highlighted nothing and announced nothing, and MultiSelect was a stored bool. SelectedRows came
    // back in index order rather than most-recent-first, so SelectedRows[0] after a click was the wrong
    // row. SelectedColumns was hardcoded empty. And ClearSelection had to blank the current cell in
    // order to clear the selection, because one pair of indices was doing both jobs (DGV-15).
    //
    // Every "reads back what was set" assertion here is labelled a guard: those passed before the fix
    // too, which is exactly why the defect survived.
    [Collection ("Headless")]
    public class DataGridViewSelectionTests
    {
        private static DataGridView Grid (int rows = 4, int columns = 3,
                                          DataGridViewSelectionMode mode = DataGridViewSelectionMode.FullRowSelect)
        {
            HeadlessRenderer.Use ();
            var grid = new DataGridView { Width = 400, Height = 220, SelectionMode = mode };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Width = 80 });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            grid.ClearSelection ();      // start from nothing selected, whatever seeding did
            return grid;
        }

        // ---------------- DGV-14: programmatic selection is a real selection

        [Fact]
        public void Selecting_rows_in_code_announces_each_one_and_keeps_them_all ()
        {
            // The finding's own test.
            using var grid = Grid ();
            var raised = 0;
            grid.SelectionChanged += (_, _) => raised++;

            grid.Rows[0].Selected = true;
            grid.Rows[2].Selected = true;

            Assert.Equal (2, raised);
            Assert.Equal (2, grid.SelectedRows.Count);
            Assert.True (grid.Rows[0].Selected);
            Assert.True (grid.Rows[2].Selected);
        }

        [Fact]
        public void SelectedRows_is_most_recently_selected_first ()
        {
            // Upstream prepends to its selection list, so SelectedRows[0] is the row the last click or
            // assignment selected. Handlers that read SelectedRows[0] after a Ctrl-click got the
            // lowest-indexed row instead, which is a different row whenever more than one is selected.
            using var grid = Grid ();

            grid.Rows[2].Selected = true;
            grid.Rows[0].Selected = true;
            grid.Rows[3].Selected = true;

            Assert.Equal (new[] { 3, 0, 2 }, grid.SelectedRows.Select (r => r.Index).ToArray ());
        }

        [Fact]
        public void Deselecting_and_reselecting_moves_a_row_to_the_front ()
        {
            using var grid = Grid ();
            grid.Rows[0].Selected = true;
            grid.Rows[1].Selected = true;

            Assert.Equal (1, grid.SelectedRows[0].Index);

            grid.Rows[0].Selected = false;
            grid.Rows[0].Selected = true;

            Assert.Equal (0, grid.SelectedRows[0].Index);
        }

        [Fact]
        public void Setting_a_row_to_the_state_it_already_has_announces_nothing ()
        {
            using var grid = Grid ();
            grid.Rows[1].Selected = true;
            var raised = 0;
            grid.SelectionChanged += (_, _) => raised++;

            grid.Rows[1].Selected = true;
            grid.Rows[2].Selected = false;

            Assert.Equal (0, raised);
        }

        [Fact]
        public void MultiSelect_off_means_one_row_at_a_time ()
        {
            // MultiSelect was stored and read by nothing, so turning it off changed nothing at all.
            using var grid = Grid ();
            grid.MultiSelect = false;

            grid.Rows[0].Selected = true;
            grid.Rows[2].Selected = true;

            Assert.Same (grid.Rows[2], Assert.Single (grid.SelectedRows));
            Assert.False (grid.Rows[0].Selected);
        }

        [Fact]
        public void A_detached_row_stores_its_selection ()
        {
            // GUARD, not proof: a row built by a designer or a test before it joins a grid has no grid
            // to announce to. It must store rather than throw.
            var row = new DataGridViewRow ();

            row.Selected = true;

            Assert.True (row.Selected);
        }

        [Fact]
        public void Selected_reads_back_what_was_set ()
        {
            // GUARD, not proof: the auto-properties this replaced round-tripped perfectly -- that was
            // the whole defect. It pins that giving the setters behaviour did not lose the state.
            using var grid = Grid ();

            grid.Rows[1].Selected = true;
            grid.Rows[1].Cells[2].Selected = true;
            grid.Columns[0].Selected = true;

            Assert.True (grid.Rows[1].Selected);
            Assert.True (grid.Rows[1].Cells[2].Selected);
            Assert.True (grid.Columns[0].Selected);
        }

        // ---------------- DGV-14: the click path honours the modifiers

        [Fact]
        public void A_plain_click_replaces_the_selection ()
        {
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.ClickRow (0);
                grid.ClickRow (2);

                Assert.Same (grid.Rows[2], Assert.Single (grid.SelectedRows));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Ctrl_click_adds_to_the_selection_and_clicking_again_removes_it ()
        {
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.ClickRow (0);
                grid.ClickRow (2, Keys.Control);

                Assert.Equal (2, grid.SelectedRows.Count);
                Assert.Same (grid.Rows[2], grid.SelectedRows[0]);      // the one just added is first

                grid.ClickRow (2, Keys.Control);

                Assert.Same (grid.Rows[0], Assert.Single (grid.SelectedRows));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Shift_click_selects_the_range_from_the_anchor ()
        {
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.ClickRow (1);
                grid.ClickRow (3, Keys.Shift);

                Assert.Equal (new[] { 1, 2, 3 }, grid.SelectedRows.Select (r => r.Index).OrderBy (i => i).ToArray ());

                // The clicked end is the most recent, not the anchor -- SelectedRows[0] is where the
                // user just clicked.
                Assert.Equal (3, grid.SelectedRows[0].Index);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Successive_Shift_clicks_re_extend_from_the_same_anchor ()
        {
            // If the anchor moved with each Shift-click, the second one would extend from row 3 and the
            // range would grow instead of shrinking.
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.ClickRow (1);
                grid.ClickRow (3, Keys.Shift);
                grid.ClickRow (2, Keys.Shift);

                Assert.Equal (new[] { 1, 2 }, grid.SelectedRows.Select (r => r.Index).OrderBy (i => i).ToArray ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Shift_click_does_nothing_extra_when_MultiSelect_is_off ()
        {
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.MultiSelect = false;
                grid.ClickRow (1);
                grid.ClickRow (3, Keys.Shift);

                Assert.Same (grid.Rows[3], Assert.Single (grid.SelectedRows));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_hidden_row_is_not_swept_into_a_Shift_range ()
        {
            // The filter idiom and multi-selection meeting: DGV-20 made Row.Visible real, so a range
            // across a filtered-out row must not quietly include it in "delete the selected rows".
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.Rows[2].Visible = false;
                grid.ClickRow (1);
                grid.ClickRow (3, Keys.Shift);

                Assert.Equal (new[] { 1, 3 }, grid.SelectedRows.Select (r => r.Index).OrderBy (i => i).ToArray ());
                Assert.False (grid.Rows[2].Selected);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void One_click_is_one_SelectionChanged ()
        {
            // GUARD, not proof. Three elements change state on a plain click over a multi-row selection
            // (two deselected, one selected) and only one notification comes out, which is what a
            // selection-count label needs. But rewriting the batch to assign the Selected properties
            // per element -- the implementation this is meant to rule out -- did not make it fail, so it
            // pins the count without discriminating against that alternative.
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.ClickRow (0);
                grid.ClickRow (1, Keys.Control);
                var raised = 0;
                grid.SelectionChanged += (_, _) => raised++;

                grid.ClickRow (3);

                Assert.Equal (1, raised);
                Assert.Same (grid.Rows[3], Assert.Single (grid.SelectedRows));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_Ctrl_click_moves_the_current_cell_whether_it_selects_or_deselects ()
        {
            // The current cell follows the pointer even when the modifier means "add" or "remove"
            // rather than "replace". Written against a DIFFERENT row from the previous click, because
            // Ctrl-clicking the row that is already current cannot tell the two behaviours apart.
            using var grid = Clicked (out var form);
            using var _form = form;

            try {
                grid.ClickRow (2);
                grid.ClickRow (0, Keys.Control);          // adds row 0, and moves the cursor to it

                Assert.Same (grid.Rows[0], grid.CurrentRow);
                Assert.Equal (2, grid.SelectedRows.Count);

                grid.ClickRow (0, Keys.Control);          // removes row 0, cursor stays on it

                Assert.Same (grid.Rows[0], grid.CurrentRow);
                Assert.Same (grid.Rows[2], Assert.Single (grid.SelectedRows));
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-14: cells and columns

        [Fact]
        public void In_cell_mode_SelectedCells_reports_the_selected_cells ()
        {
            // SelectedCells was derived from SelectedRows, so in CellSelect mode -- where no row is ever
            // selected -- it was always empty however many cells were selected.
            using var grid = Grid (mode: DataGridViewSelectionMode.CellSelect);

            grid.Rows[0].Cells[1].Selected = true;
            grid.Rows[2].Cells[0].Selected = true;

            Assert.Equal (2, grid.SelectedCells.Count);
            Assert.Same (grid.Rows[2].Cells[0], grid.SelectedCells[0]);   // most recent first
        }

        [Fact]
        public void In_row_mode_a_selected_rows_cells_count_as_selected ()
        {
            using var grid = Grid (columns: 3);

            grid.Rows[1].Selected = true;

            Assert.Equal (3, grid.SelectedCells.Count);
            Assert.Equal (3, grid.GetCellCount (DataGridViewElementStates.Selected));
        }

        [Fact]
        public void SelectAll_in_cell_mode_selects_every_cell ()
        {
            // SelectAll set cell.Selected in cell mode, but SelectedCells read SelectedRows, so
            // SelectAll followed by SelectedCells.Count returned zero.
            using var grid = Grid (rows: 4, columns: 3, mode: DataGridViewSelectionMode.CellSelect);

            grid.SelectAll ();

            Assert.Equal (12, grid.SelectedCells.Count);
        }

        [Fact]
        public void SelectAll_announces_once ()
        {
            // GUARD, not proof -- see One_click_is_one_SelectionChanged. It pins one notification for a
            // four-row SelectAll; it did not fail when the batch was rewritten to assign the
            // properties element by element.
            using var grid = Grid ();
            var raised = 0;
            grid.SelectionChanged += (_, _) => raised++;

            grid.SelectAll ();

            Assert.Equal (1, raised);
            Assert.Equal (4, grid.SelectedRows.Count);
        }

        [Fact]
        public void SelectedColumns_reports_selected_columns ()
        {
            // It returned a new empty collection unconditionally.
            using var grid = Grid (mode: DataGridViewSelectionMode.FullColumnSelect);

            grid.Columns[2].Selected = true;
            grid.Columns[0].Selected = true;

            Assert.Equal (2, grid.SelectedColumns.Count);
            Assert.Same (grid.Columns[0], grid.SelectedColumns[0]);       // most recent first
        }

        [Fact]
        public void Reading_SelectedColumns_does_not_raise_the_column_events ()
        {
            // The projection collection exists for this: adding to a normal DataGridViewColumnCollection
            // re-owns the column and raises ColumnAdded, so a populated SelectedColumns built from one
            // would fire the grid's column events on every read and force a relayout.
            using var grid = Grid (mode: DataGridViewSelectionMode.FullColumnSelect);
            grid.Columns[1].Selected = true;
            var added = 0;
            grid.ColumnAdded += (_, _) => added++;

            _ = grid.SelectedColumns;
            _ = grid.SelectedColumns;

            Assert.Equal (0, added);
            Assert.Equal (3, grid.Columns.Count);       // and the real column list is untouched
        }

        [Fact]
        public void In_column_mode_a_selected_columns_cells_count_as_selected ()
        {
            using var grid = Grid (rows: 4, columns: 3, mode: DataGridViewSelectionMode.FullColumnSelect);

            grid.Columns[1].Selected = true;

            Assert.Equal (4, grid.SelectedCells.Count);
            Assert.All (grid.SelectedCells, c => Assert.Equal (1, c.ColumnIndex));
        }

        // ---------------- DGV-14: the renderer paints the selection

        [Fact]
        public void A_selected_row_is_painted_highlighted ()
        {
            // The half that made everything else invisible: the renderer keyed on SelectedRowIndex, so
            // row.Selected changed no pixels.
            using var grid = Grid ();
            using var form = new Form { Width = 500, Height = 320 };
            form.Controls.Add (grid);

            try {
                var highlight = DataGridView.DefaultSelectionStyle.GetBackgroundColor ();

                using (var before = PaintSurface.RenderOnForm (grid, 1f)) {
                    Assert.Equal (grid.Width, before.Width);         // a 0x0 surface would pass anything
                    Assert.True (Coverage (before, grid.GetCellBounds (2, 0), highlight) < Unfilled);
                }

                grid.Rows[2].Selected = true;

                using var after = PaintSurface.RenderOnForm (grid, 1f);

                Assert.True (Coverage (after, grid.GetCellBounds (2, 0), highlight) > Filled,
                    "the selected row was not painted highlighted");
            } finally {
                form.Dispose ();
            }
        }

        [Fact]
        public void Every_row_of_a_multi_selection_is_painted ()
        {
            using var grid = Grid ();
            using var form = new Form { Width = 500, Height = 320 };
            form.Controls.Add (grid);

            try {
                grid.Rows[0].Selected = true;
                grid.Rows[2].Selected = true;

                using var bitmap = PaintSurface.RenderOnForm (grid, 1f);
                var highlight = DataGridView.DefaultSelectionStyle.GetBackgroundColor ();

                Assert.True (Coverage (bitmap, grid.GetCellBounds (0, 0), highlight) > Filled, "row 0 not painted");
                Assert.True (Coverage (bitmap, grid.GetCellBounds (2, 0), highlight) > Filled, "row 2 not painted");
                Assert.True (Coverage (bitmap, grid.GetCellBounds (1, 0), highlight) < Unfilled, "row 1 should not be painted");
            } finally {
                form.Dispose ();
            }
        }

        [Fact]
        public void In_cell_mode_a_selected_cell_is_outlined_and_its_row_is_not_highlighted ()
        {
            using var grid = Grid (mode: DataGridViewSelectionMode.CellSelect);
            using var form = new Form { Width = 500, Height = 320 };
            form.Controls.Add (grid);

            try {
                grid.Rows[1].Cells[1].Selected = true;

                using var bitmap = PaintSurface.RenderOnForm (grid, 1f);

                // The outline is a 2px stroke on the cell's border, so it is counted rather than
                // measured as coverage -- it is deliberately NOT a fill.
                Assert.True (CountIn (bitmap, grid.GetCellBounds (1, 1), DataGridView.DefaultSelectionStyle.Border.GetColor ()) > 0,
                    "the selected cell was not outlined");
                Assert.True (Coverage (bitmap, grid.GetCellBounds (1, 0), DataGridView.DefaultSelectionStyle.GetBackgroundColor ()) < Unfilled,
                    "cell selection must not highlight the whole row");
            } finally {
                form.Dispose ();
            }
        }

        // ---------------- DGV-15: clearing the selection is not moving the cursor

        [Fact]
        public void ClearSelection_leaves_the_current_cell_alone ()
        {
            using var grid = Grid ();
            grid.SelectedRowIndex = 2;
            grid.SelectedColumnIndex = 1;

            grid.ClearSelection ();

            Assert.Same (grid.Rows[2], grid.CurrentRow);
            Assert.Same (grid.Rows[2].Cells[1], grid.CurrentCell);
            Assert.Empty (grid.SelectedRows);
        }

        [Fact]
        public void ClearSelection_announces_once_and_only_when_something_was_selected ()
        {
            using var grid = Grid ();
            grid.Rows[0].Selected = true;
            grid.Rows[1].Selected = true;
            var raised = 0;
            grid.SelectionChanged += (_, _) => raised++;

            grid.ClearSelection ();

            Assert.Equal (1, raised);

            grid.ClearSelection ();       // already empty

            Assert.Equal (1, raised);
        }

        // ---------------- helpers

        // A grid parented to a shown form, because a click path needs real bounds to hit-test against.
        private static ClickableGrid Clicked (out Form form)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 520, Height = 340 };
            var grid = new ClickableGrid { Width = 400, Height = 220 };

            for (var c = 0; c < 3; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Width = 80 });

            for (var r = 0; r < 4; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < 3; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();     // bounds exist only once painted
            grid.ClearSelection ();

            return grid;
        }

        private sealed class ClickableGrid : DataGridView
        {
            // Clicks the middle of a row's first cell with the given modifiers held. The modifiers ride
            // on the event args, which is where a real backend puts them -- priming the static
            // Control.ModifierKeys instead does nothing, because the MouseEventArgs constructor
            // overwrites that static from its own keyData.
            internal void ClickRow (int rowIndex, Keys modifiers = Keys.None)
            {
                var bounds = GetCellBounds (rowIndex, 0);

                OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1,
                    bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2,
                    Point.Empty, keyData: modifiers));
            }
        }

        // The interior of a cell rectangle, away from the border it shares with its neighbours.
        private static Rectangle Interior (Rectangle cell)
            => new Rectangle (cell.Left + 2, cell.Top + 2, Math.Max (1, cell.Width - 4), Math.Max (1, cell.Height - 4));

        // What fraction of a cell's interior carries the given colour.
        //
        // Measured as a ratio rather than an exact count because the cell text is anti-aliased against
        // a light background, and some of those intermediate greys land exactly on the highlight colour
        // (#c6c6c6) -- one glyph pixel inside an UNSELECTED row matched, so "expect zero" is not
        // achievable. A highlighted row is a fill: it covers essentially the whole interior, which
        // separates it from a stray glyph pixel by two orders of magnitude.
        private static double Coverage (SKBitmap bitmap, Rectangle cell, SKColor colour)
        {
            var area = Interior (cell);
            var counted = 0;
            var matched = 0;

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    counted++;
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue)
                        matched++;
                }

            return counted == 0 ? 0 : (double)matched / counted;
        }

        private static int CountIn (SKBitmap bitmap, Rectangle area, SKColor colour)
        {
            var count = 0;

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue)
                        count++;
                }

            return count;
        }

        // A row is highlighted when its interior is mostly the highlight colour; it is not highlighted
        // when only a stray anti-aliased pixel or two matches. The gap between the two is enormous, so
        // the exact thresholds are not doing any delicate work.
        private const double Filled = 0.5;
        private const double Unfilled = 0.05;
    }
}
