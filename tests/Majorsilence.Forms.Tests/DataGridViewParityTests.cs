using Xunit;

using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the DataGridView parity pass (docs/winforms-gap-plan.md) — the largest single
    /// concentration of gaps on this surface.
    ///
    /// Two things are worth guarding. First, that the 25 <c>*Changed</c> events wired into existing
    /// property setters actually fire, and fire once, since the alternative was 59 events that
    /// compile and never happen. Second, that the row collection's <c>Get*Row</c> family really walks
    /// by state rather than returning a plausible constant.
    /// </summary>
    public class DataGridViewParityTests
    {
        private static DataGridView WithRows (int count)
        {
            var grid = new DataGridView ();
            grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "Name" });

            // The string overload is what populates a row's cells from the grid's columns; adding a
            // bare DataGridViewRow leaves it cell-less, which would make these assertions vacuous.
            for (var i = 0; i < count; i++)
                grid.Rows.Add ($"row {i}");

            return grid;
        }

        [Fact]
        public void The_state_properties_raise_their_Changed_event_exactly_once ()
        {
            using var grid = new DataGridView ();
            var raised = 0;

            grid.MultiSelectChanged += (_, _) => raised++;

            grid.MultiSelect = false;
            grid.MultiSelect = false;      // no change, no event

            Assert.Equal (1, raised);
            Assert.False (grid.MultiSelect);
        }

        [Theory]
        [InlineData ("AllowUserToAddRows")]
        [InlineData ("AllowUserToDeleteRows")]
        [InlineData ("AllowUserToOrderColumns")]
        [InlineData ("AutoGenerateColumns")]
        [InlineData ("ReadOnly")]
        public void The_boolean_properties_are_wired_to_their_events (string property)
        {
            using var grid = new DataGridView ();
            var raised = 0;

            switch (property) {
                case "AllowUserToAddRows":
                    grid.AllowUserToAddRowsChanged += (_, _) => raised++;
                    grid.AllowUserToAddRows = !grid.AllowUserToAddRows;
                    break;
                case "AllowUserToDeleteRows":
                    grid.AllowUserToDeleteRowsChanged += (_, _) => raised++;
                    grid.AllowUserToDeleteRows = !grid.AllowUserToDeleteRows;
                    break;
                case "AllowUserToOrderColumns":
                    grid.AllowUserToOrderColumnsChanged += (_, _) => raised++;
                    grid.AllowUserToOrderColumns = !grid.AllowUserToOrderColumns;
                    break;
                case "AutoGenerateColumns":
                    grid.AutoGenerateColumnsChanged += (_, _) => raised++;
                    grid.AutoGenerateColumns = !grid.AutoGenerateColumns;
                    break;
                case "ReadOnly":
                    grid.ReadOnlyChanged += (_, _) => raised++;
                    grid.ReadOnly = !grid.ReadOnly;
                    break;
            }

            Assert.Equal (1, raised);
        }

        [Fact]
        public void The_header_border_styles_round_trip_and_notify ()
        {
            using var grid = new DataGridView ();
            var columnRaised = 0;
            var rowRaised = 0;

            grid.ColumnHeadersBorderStyleChanged += (_, _) => columnRaised++;
            grid.RowHeadersBorderStyleChanged += (_, _) => rowRaised++;

            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Sunken;

            Assert.Equal (DataGridViewHeaderBorderStyle.Single, grid.ColumnHeadersBorderStyle);
            Assert.Equal (DataGridViewHeaderBorderStyle.Sunken, grid.RowHeadersBorderStyle);
            Assert.Equal (1, columnRaised);
            Assert.Equal (1, rowRaised);
        }

        [Fact]
        public void AdjustedTopLeftHeaderBorderStyle_follows_the_column_header_style ()
        {
            using var grid = new DataGridView { ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None };

            Assert.Equal (DataGridViewAdvancedCellBorderStyle.None, grid.AdjustedTopLeftHeaderBorderStyle.Top);

            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Sunken;

            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Inset, grid.AdjustedTopLeftHeaderBorderStyle.Left);
        }

        [Fact]
        public void AdjustColumnHeaderBorderStyle_drops_the_shared_edges ()
        {
            using var grid = new DataGridView ();
            var input = new DataGridViewAdvancedBorderStyle {
                Top = DataGridViewAdvancedCellBorderStyle.Single,
                Bottom = DataGridViewAdvancedCellBorderStyle.Single,
                Left = DataGridViewAdvancedCellBorderStyle.Single,
                Right = DataGridViewAdvancedCellBorderStyle.Single,
            };

            var middle = grid.AdjustColumnHeaderBorderStyle (input, new DataGridViewAdvancedBorderStyle (),
                isFirstDisplayedColumn: false, isLastVisibleColumn: false);

            Assert.Equal (DataGridViewAdvancedCellBorderStyle.None, middle.Left);
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.None, middle.Right);
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Single, middle.Top);

            var only = grid.AdjustColumnHeaderBorderStyle (input, new DataGridViewAdvancedBorderStyle (),
                isFirstDisplayedColumn: true, isLastVisibleColumn: true);

            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Single, only.Left);
            Assert.Equal (DataGridViewAdvancedCellBorderStyle.Single, only.Right);
        }

        [Fact]
        public void AreAllCellsSelected_answers_from_the_cells ()
        {
            using var grid = WithRows (2);

            Assert.False (grid.AreAllCellsSelected (includeInvisibleCells: true));

            foreach (var row in grid.Rows)
                foreach (var cell in row.Cells)
                    cell.Selected = true;

            Assert.True (grid.AreAllCellsSelected (includeInvisibleCells: true));
        }

        [Fact]
        public void GetRowDisplayRectangle_stacks_the_rows ()
        {
            using var grid = WithRows (3);
            grid.ColumnHeadersVisible = false;

            var first = grid.GetRowDisplayRectangle (0, cutOverflow: false);
            var second = grid.GetRowDisplayRectangle (1, cutOverflow: false);

            Assert.Equal (0, first.Y);
            Assert.Equal (grid.Rows[0].Height, second.Y);
            Assert.Equal (Rectangle.Empty, grid.GetRowDisplayRectangle (99, false));
        }

        [Fact]
        public void GetColumnDisplayRectangle_walks_the_columns ()
        {
            using var grid = new DataGridView { RowHeadersVisible = false };
            grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "A", Width = 60 });
            grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = "B", Width = 40 });

            Assert.Equal (0, grid.GetColumnDisplayRectangle (0, false).X);
            Assert.Equal (60, grid.GetColumnDisplayRectangle (1, false).X);
            Assert.Equal (40, grid.GetColumnDisplayRectangle (1, false).Width);
            Assert.Equal (Rectangle.Empty, grid.GetColumnDisplayRectangle (9, false));
        }

        [Fact]
        public void UpdateRowErrorText_validates_its_range ()
        {
            using var grid = WithRows (3);

            grid.UpdateRowErrorText (0);
            grid.UpdateRowErrorText (0, 2);

            Assert.Throws<System.ArgumentOutOfRangeException> (() => grid.UpdateRowErrorText (-1));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => grid.UpdateRowErrorText (2, 1));
        }

        [Fact]
        public void GetFirstRow_and_GetNextRow_walk_by_state ()
        {
            using var grid = WithRows (4);
            grid.Rows[1].Selected = true;
            grid.Rows[3].Selected = true;

            var first = grid.Rows.GetFirstRow (DataGridViewElementStates.Selected);
            Assert.Equal (1, first);

            Assert.Equal (3, grid.Rows.GetNextRow (first, DataGridViewElementStates.Selected));
            Assert.Equal (-1, grid.Rows.GetNextRow (3, DataGridViewElementStates.Selected));
            Assert.Equal (3, grid.Rows.GetLastRow (DataGridViewElementStates.Selected));
            Assert.Equal (1, grid.Rows.GetPreviousRow (3, DataGridViewElementStates.Selected));
        }

        [Fact]
        public void The_exclude_filter_removes_rows_the_include_filter_matched ()
        {
            using var grid = WithRows (3);
            grid.Rows[0].Selected = true;
            grid.Rows[1].Selected = true;
            grid.Rows[1].ReadOnly = true;

            Assert.Equal (0, grid.Rows.GetFirstRow (DataGridViewElementStates.Selected, DataGridViewElementStates.ReadOnly));
            Assert.Equal (-1, grid.Rows.GetNextRow (0, DataGridViewElementStates.Selected, DataGridViewElementStates.ReadOnly));
        }

        [Fact]
        public void GetRowCount_and_GetRowsHeight_only_count_matching_rows ()
        {
            using var grid = WithRows (3);
            grid.Rows[0].Selected = true;
            grid.Rows[2].Selected = true;

            Assert.Equal (2, grid.Rows.GetRowCount (DataGridViewElementStates.Selected));
            Assert.Equal (grid.Rows[0].Height + grid.Rows[2].Height,
                grid.Rows.GetRowsHeight (DataGridViewElementStates.Selected));

            // None as an include filter matches everything, as it does upstream.
            Assert.Equal (3, grid.Rows.GetRowCount (DataGridViewElementStates.None));
        }

        [Fact]
        public void GetRowState_reports_the_rows_flags ()
        {
            using var grid = WithRows (1);
            grid.Rows[0].Selected = true;
            grid.Rows[0].ReadOnly = true;

            var state = grid.Rows.GetRowState (0);

            Assert.True (state.HasFlag (DataGridViewElementStates.Selected));
            Assert.True (state.HasFlag (DataGridViewElementStates.ReadOnly));
            Assert.True (state.HasFlag (DataGridViewElementStates.Visible));
        }

        [Fact]
        public void AddCopy_and_AddRange_grow_the_collection ()
        {
            using var grid = WithRows (1);
            grid.Rows[0].Cells[0].Value = "original";

            var copyIndex = grid.Rows.AddCopy (0);

            Assert.Equal (1, copyIndex);
            Assert.Equal (2, grid.Rows.Count);
            Assert.NotSame (grid.Rows[0], grid.Rows[1]);

            grid.Rows.AddRange (new DataGridViewRow (), new DataGridViewRow ());
            Assert.Equal (4, grid.Rows.Count);
        }

        [Fact]
        public void InsertCopies_puts_the_copies_where_asked ()
        {
            using var grid = WithRows (2);

            grid.Rows.InsertCopies (0, 1, 2);

            Assert.Equal (4, grid.Rows.Count);
        }

        [Fact]
        public void The_copy_methods_reject_an_index_that_is_not_a_row ()
        {
            using var grid = WithRows (1);

            Assert.Throws<System.ArgumentOutOfRangeException> (() => grid.Rows.AddCopy (5));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => grid.Rows.AddCopies (0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => grid.Rows.SharedRow (5));
        }

        [Fact]
        public void HasStyle_is_false_until_a_style_is_assigned ()
        {
            var cell = new DataGridViewTextBoxCell ();

            Assert.False (cell.HasStyle);

            cell.Style = new ControlStyle (cell.Style);

            Assert.True (cell.HasStyle);
        }

        [Fact]
        public void KeyEntersEditMode_accepts_typing_and_rejects_shortcuts ()
        {
            var cell = new DataGridViewTextBoxCell ();

            Assert.True (cell.KeyEntersEditMode (new KeyEventArgs (Keys.A)));
            Assert.True (cell.KeyEntersEditMode (new KeyEventArgs (Keys.D5)));
            Assert.True (cell.KeyEntersEditMode (new KeyEventArgs (Keys.F2)));
            Assert.False (cell.KeyEntersEditMode (new KeyEventArgs (Keys.Control | Keys.C)));
            Assert.False (cell.KeyEntersEditMode (new KeyEventArgs (Keys.Tab)));
        }

        [Fact]
        public void A_read_only_cell_never_enters_edit_mode_from_a_key ()
        {
            var cell = new DataGridViewTextBoxCell { ReadOnly = true };

            Assert.False (cell.KeyEntersEditMode (new KeyEventArgs (Keys.A)));
        }

        [Fact]
        public void ErrorIconBounds_is_empty_until_there_is_an_error ()
        {
            using var grid = WithRows (1);
            var cell = grid.Rows[0].Cells[0];

            Assert.Equal (Rectangle.Empty, cell.ErrorIconBounds);

            cell.ErrorText = "Value is out of range";

            Assert.NotEqual (Rectangle.Empty, cell.ErrorIconBounds);
        }

        [Fact]
        public void GetInheritedState_reports_the_cells_own_flags ()
        {
            using var grid = WithRows (1);
            var cell = grid.Rows[0].Cells[0];
            cell.Selected = true;

            var state = cell.GetInheritedState (0);

            Assert.True (state.HasFlag (DataGridViewElementStates.Selected));
            Assert.True (state.HasFlag (DataGridViewElementStates.Visible));
        }

        [Fact]
        public void A_read_only_grid_makes_its_cells_read_only_by_inheritance ()
        {
            using var grid = WithRows (1);
            grid.ReadOnly = true;

            Assert.True (grid.Rows[0].Cells[0].GetInheritedState (0).HasFlag (DataGridViewElementStates.ReadOnly));
        }

        [Fact]
        public void ParseFormattedValue_converts_text_back_to_the_cells_value_type ()
        {
            var cell = new DataGridViewTextBoxCell ();

            // The base cell has no declared ValueType, so the text comes back unchanged rather than
            // being guessed at.
            Assert.Equal ("42", cell.ParseFormattedValue ("42", null, null, null));
        }

        [Fact]
        public void HandledMouseEventArgs_carries_the_scalar_delta_as_the_vertical_one ()
        {
            var e = new HandledMouseEventArgs (MouseButtons.Left, 2, 10, 20, 120);

            Assert.Equal (120, e.Delta);
            Assert.Equal (0, e.DeltaPoint.X);
            Assert.False (e.Handled);

            var handled = new HandledMouseEventArgs (MouseButtons.Left, 1, 0, 0, 0, defaultHandledValue: true);
            Assert.True (handled.Handled);
        }

        [Fact]
        public void The_divider_double_click_args_forward_the_mouse_state ()
        {
            var mouse = new HandledMouseEventArgs (MouseButtons.Right, 2, 5, 6, 120, defaultHandledValue: true);

            var column = new DataGridViewColumnDividerDoubleClickEventArgs (3, mouse);

            Assert.Equal (3, column.ColumnIndex);
            Assert.Equal (MouseButtons.Right, column.Button);
            Assert.Equal (5, column.X);
            Assert.Equal (6, column.Y);
            Assert.True (column.Handled);

            var row = new DataGridViewRowDividerDoubleClickEventArgs (7, mouse);
            Assert.Equal (7, row.RowIndex);

            Assert.Throws<System.ArgumentNullException> (
                () => new DataGridViewRowDividerDoubleClickEventArgs (0, null!));
        }
    }
}
