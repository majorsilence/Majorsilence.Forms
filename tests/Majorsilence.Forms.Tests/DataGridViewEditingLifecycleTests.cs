using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.1 (findings DGV-01 P0, DGV-06, DGV-07, DGV-08, DGV-09, DGV-10, DGV-11): the editing lifecycle.
    //
    // The grid could edit a cell, through one non-WinForms overload. Everything around that was either
    // a stub that reported success or a check nobody made:
    //
    //   BeginEdit (bool) -- the only public WinForms way to start an edit from code -- was
    //   `{ return true; }`; EditMode was stored and read by nothing; Column.ReadOnly was consulted only
    //   by InheritedState; IsCurrentCellDirty meant "an editor exists"; Escape raised no CellEndEdit;
    //   the commit stored the editor's raw string and swallowed write-back failures without ever
    //   raising DataError; and moving the current cell raised none of CurrentCellChanged, CellLeave,
    //   CellValidating or CellEnter.
    [Collection ("Headless")]
    public class DataGridViewEditingLifecycleTests
    {
        private sealed class Item
        {
            public string Name { get; set; } = string.Empty;
            public int Qty { get; set; }
        }

        // Drives the protected entry points a real backend calls. The grid is parented and painted so
        // the cell rectangles a click has to hit actually exist.
        private sealed class EditableGrid : DataGridView
        {
            internal void PressKey (Keys key, bool shift = false)
                => OnKeyDown (new KeyEventArgs (shift ? key | Keys.Shift : key));

            internal void ClickCell (int rowIndex, int columnIndex)
            {
                var b = GetCellBounds (rowIndex, columnIndex);
                OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1,
                    b.Left + b.Width / 2, b.Top + b.Height / 2, Point.Empty));
            }

            internal void DoubleClickCell (int rowIndex, int columnIndex)
            {
                var b = GetCellBounds (rowIndex, columnIndex);
                OnDoubleClick (new MouseEventArgs (MouseButtons.Left, 2,
                    b.Left + b.Width / 2, b.Top + b.Height / 2, Point.Empty));
            }
        }

        private static EditableGrid Grid (out Form form, int rows = 3, int columns = 2)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 520, Height = 340 };
            var grid = new EditableGrid { Width = 400, Height = 200 };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Width = 80 });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();   // cell bounds exist only once painted
            grid.SelectedRowIndex = 0;
            grid.SelectedColumnIndex = 0;

            return grid;
        }

        private static TextBox? Editor (DataGridView grid)
            => Enumerable.OfType<TextBox> (grid.Controls).FirstOrDefault ();

        // ---------------- DGV-01 (P0): BeginEdit (bool) edits something

        [Fact]
        public void BeginEdit_edits_the_current_cell_and_reports_it ()
        {
            // The finding's own test. `public bool BeginEdit (bool selectAll) { return true; }` reported
            // success and edited nothing, so every "edit on single click" idiom and every toolbar Edit
            // button was a no-op that looked like it had worked.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var begun = 0;
                grid.CellBeginEdit += (_, _) => begun++;

                var started = grid.BeginEdit (true);

                Assert.True (started);
                Assert.True (grid.IsCurrentCellInEditMode);
                Assert.Equal (1, begun);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void BeginEdit_false_puts_the_caret_at_the_end_instead_of_selecting ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (false);

                var editor = Editor (grid);
                Assert.NotNull (editor);
                Assert.Equal (editor!.TextLength, editor.SelectionStart);
                Assert.Equal (0, editor.SelectionLength);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void BeginEdit_with_no_current_cell_reports_failure ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.MoveCurrentCell (-1, -1);

                Assert.False (grid.BeginEdit (true));
                Assert.False (grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void BeginEdit_reports_failure_when_a_handler_cancels ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.CellBeginEdit += (_, e) => e.Cancel = true;

                Assert.False (grid.BeginEdit (true));
                Assert.False (grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-07: ReadOnly has a veto

        [Theory]
        [InlineData ("column")]
        [InlineData ("row")]
        [InlineData ("cell")]
        public void A_read_only_cell_column_or_row_refuses_to_edit (string scope)
        {
            // `Columns["Id"].ReadOnly = true` is in every LOB grid. Only the GRID's ReadOnly was checked,
            // so the cell edited and the edit was written back to the bound object.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                switch (scope) {
                    case "column": grid.Columns[0].ReadOnly = true; break;
                    case "row": grid.Rows[0].ReadOnly = true; break;
                    case "cell": grid.Rows[0].Cells[0].ReadOnly = true; break;
                }

                var begun = 0;
                grid.CellBeginEdit += (_, _) => begun++;

                var started = grid.BeginEdit (true);

                Assert.False (started);
                Assert.False (grid.IsCurrentCellInEditMode);
                Assert.Equal (0, begun);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_editable_cell_still_edits ()
        {
            // GUARD, not proof: the grid could always edit an editable cell -- that half worked. It
            // pins that adding three vetoes did not veto everything.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[1].ReadOnly = true;     // a different column is read-only

                Assert.True (grid.BeginEdit (true));
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-08: dirty means dirty

        [Fact]
        public void An_edit_starts_clean_and_becomes_dirty_when_the_editor_changes ()
        {
            // IsCurrentCellDirty was `edit_textbox is not null`, so it was true the instant editing
            // began. The canonical commit-a-checkbox idiom
            // (CurrentCellDirtyStateChanged += … if (IsCurrentCellDirty) CommitEdit (…)) could not work.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var raised = 0;
                grid.CurrentCellDirtyStateChanged += (_, _) => raised++;

                grid.BeginEdit (true);

                Assert.False (grid.IsCurrentCellDirty);
                Assert.Equal (0, raised);

                Editor (grid)!.Text = "changed";

                Assert.True (grid.IsCurrentCellDirty);
                Assert.Equal (1, raised);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_dirty_event_fires_on_the_transition_only ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (true);
                var raised = 0;
                grid.CurrentCellDirtyStateChanged += (_, _) => raised++;

                Editor (grid)!.Text = "one";
                Editor (grid)!.Text = "two";

                Assert.Equal (1, raised);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void NotifyCurrentCellDirty_is_how_a_custom_editor_reports_itself ()
        {
            // It was an empty method body, in the no-op stub baseline.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (true);
                var raised = 0;
                grid.CurrentCellDirtyStateChanged += (_, _) => raised++;

                grid.NotifyCurrentCellDirty (true);

                Assert.True (grid.IsCurrentCellDirty);
                Assert.Equal (1, raised);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Ending_or_cancelling_an_edit_clears_the_dirty_flag ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (true);
                Editor (grid)!.Text = "changed";
                grid.EndEdit ();

                Assert.False (grid.IsCurrentCellDirty);

                grid.BeginEdit (true);
                Editor (grid)!.Text = "again";
                grid.CancelEdit ();

                Assert.False (grid.IsCurrentCellDirty);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_row_stays_dirty_after_a_commit_until_the_current_row_moves ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                Assert.False (grid.IsCurrentRowDirty);

                grid.BeginEdit (true);
                Editor (grid)!.Text = "changed";
                grid.EndEdit ();

                // The cell is no longer dirty -- it is committed -- but the ROW has unsaved work in it,
                // which is the question a "prompt to save" handler is asking.
                Assert.False (grid.IsCurrentCellDirty);
                Assert.True (grid.IsCurrentRowDirty);

                grid.SelectedRowIndex = 1;

                Assert.False (grid.IsCurrentRowDirty);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-09: Escape ends the edit

        [Fact]
        public void CancelEdit_raises_CellEndEdit ()
        {
            // Handlers that re-enable buttons or clear an "editing" status live in CellEndEdit, and
            // stayed stuck after Escape because CancelEdit tore the editor down silently.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (true);
                var ended = new List<(int Row, int Column)> ();
                grid.CellEndEdit += (_, e) => ended.Add ((e.RowIndex, e.ColumnIndex));

                grid.CancelEdit ();

                Assert.Equal ((0, 0), Assert.Single (ended));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void CancelEdit_with_nothing_being_edited_raises_nothing ()
        {
            // GUARD, not proof: the early return was always there. It pins that adding the event did
            // not make a no-op call announce an edit that never happened.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var ended = 0;
                grid.CellEndEdit += (_, _) => ended++;

                grid.CancelEdit ();

                Assert.Equal (0, ended);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-10: the commit converts, and says so when it cannot

        [Fact]
        public void A_committed_edit_is_converted_to_the_columns_ValueType ()
        {
            // The grid stored the editor's raw string, so a column declared ValueType = typeof (int)
            // held "5" and every (int)cell.Value cast threw -- and numeric sorting fell back to text.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].ValueType = typeof (int);

                grid.BeginEdit (true);
                Editor (grid)!.Text = "5";
                grid.EndEdit ();

                Assert.Equal (5, grid.Rows[0].Cells[0].Value);
                Assert.IsType<int> (grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_value_that_cannot_be_converted_raises_DataError_and_stays_in_edit_mode ()
        {
            // DataError was declared on this type and raised from nowhere in it. Typing "abc" into an
            // int column simply vanished.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].ValueType = typeof (int);
                var errors = new List<DataGridViewDataErrorEventArgs> ();
                grid.DataError += (_, e) => errors.Add (e);

                grid.BeginEdit (true);
                Editor (grid)!.Text = "not a number";
                var committed = grid.EndEdit ();

                Assert.False (committed);
                Assert.True (grid.IsCurrentCellInEditMode);        // the bad text is still there to fix
                var error = Assert.Single (errors);
                Assert.Equal (0, error.RowIndex);
                Assert.True (error.Context.HasFlag (DataGridViewDataErrorContexts.Parsing));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_DataError_handler_can_escalate_the_failure_back_into_an_exception ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].ValueType = typeof (int);
                grid.DataError += (_, e) => e.ThrowException = true;

                grid.BeginEdit (true);
                Editor (grid)!.Text = "not a number";

                Assert.ThrowsAny<Exception> (() => grid.EndEdit ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Clearing_a_typed_cell_stores_the_styles_null_value_not_an_empty_string ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].ValueType = typeof (int);

                grid.BeginEdit (true);
                Editor (grid)!.Text = string.Empty;
                grid.EndEdit ();

                // Not "": an empty string in an int column is the same corruption as "5" was.
                Assert.NotEqual (string.Empty, grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_CellParsing_handler_still_wins ()
        {
            // GUARD, not proof: this path worked before and is the reason the raw string survived at
            // all. It pins that adding the default conversion did not displace the handler.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].ValueType = typeof (int);
                grid.CellParsing += (_, e) => { e.Value = 99; e.ParsingApplied = true; };

                grid.BeginEdit (true);
                Editor (grid)!.Text = "5";
                grid.EndEdit ();

                Assert.Equal (99, grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_untyped_column_still_stores_the_text ()
        {
            // GUARD, not proof: with no ValueType anywhere the target type is string, so the text is
            // the correct result. It pins that the conversion did not start rejecting plain text.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (true);
                Editor (grid)!.Text = "plain";
                grid.EndEdit ();

                Assert.Equal ("plain", grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_bound_column_converts_to_the_bound_members_type_without_a_declared_ValueType ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 520, Height = 340 };
            var items = new List<Item> { new () { Name = "a", Qty = 1 } };
            var grid = new EditableGrid { Width = 400, Height = 200, AutoGenerateColumns = false };
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "Qty", DataPropertyName = nameof (Item.Qty), Width = 80 });
            grid.DataSource = items;
            form.Controls.Add (grid);
            form.Show ();

            try {
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();
                grid.MoveCurrentCell (0, 0);

                grid.BeginEdit (true);
                Editor (grid)!.Text = "42";
                grid.EndEdit ();

                Assert.Equal (42, items[0].Qty);
                Assert.IsType<int> (grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-06: EditMode decides what opens an editor

        [Fact]
        public void EditProgrammatically_refuses_F2_and_double_click ()
        {
            // The point of this mode: a read-mostly grid with a custom editor must never be opened by
            // the user. It opened on both, because EditMode was read by nothing.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.EditMode = DataGridViewEditMode.EditProgrammatically;

                grid.PressKey (Keys.F2);
                Assert.False (grid.IsCurrentCellInEditMode);

                grid.DoubleClickCell (0, 0);
                Assert.False (grid.IsCurrentCellInEditMode);

                // …but code can still open it, which is what the mode's name says.
                Assert.True (grid.BeginEdit (true));
            } finally {
                form.Close ();
            }
        }

        [Theory]
        [InlineData (DataGridViewEditMode.EditOnF2, true)]
        [InlineData (DataGridViewEditMode.EditOnKeystrokeOrF2, true)]
        [InlineData (DataGridViewEditMode.EditOnKeystroke, false)]
        public void F2_opens_an_editor_only_in_the_modes_that_say_so (DataGridViewEditMode mode, bool expected)
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.EditMode = mode;

                grid.PressKey (Keys.F2);

                Assert.Equal (expected, grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Theory]
        [InlineData (DataGridViewEditMode.EditOnKeystroke, true)]
        [InlineData (DataGridViewEditMode.EditOnKeystrokeOrF2, true)]
        [InlineData (DataGridViewEditMode.EditOnF2, false)]
        public void Typing_a_character_opens_an_editor_only_in_the_modes_that_say_so (DataGridViewEditMode mode, bool expected)
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.EditMode = mode;

                grid.PressKey (Keys.A);

                Assert.Equal (expected, grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_character_that_opened_the_editor_is_what_it_contains ()
        {
            // Typing over a cell overwrites it; the keystroke is not swallowed and the old value is not
            // kept in front of it.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

                grid.PressKey (Keys.A, shift: true);

                Assert.True (grid.IsCurrentCellInEditMode);
                Assert.Equal ("A", Editor (grid)!.Text);
                Assert.True (grid.IsCurrentCellDirty);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_navigation_key_does_not_open_an_editor ()
        {
            // GUARD, not proof: KeyEntersEditMode already refused these. It pins that the new keystroke
            // path asks it rather than opening on anything at all.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

                grid.PressKey (Keys.Down);
                grid.PressKey (Keys.Tab);

                Assert.False (grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void EditOnEnter_opens_the_editor_as_soon_as_a_cell_becomes_current ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.EditMode = DataGridViewEditMode.EditOnEnter;

                grid.SelectedRowIndex = 2;

                Assert.True (grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Clicking_the_cell_that_is_already_current_begins_editing_it ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.ClickCell (1, 1);                      // moves the current cell, does not edit
                Assert.False (grid.IsCurrentCellInEditMode);

                grid.ClickCell (1, 1);                      // clicking it again edits

                Assert.True (grid.IsCurrentCellInEditMode);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void EditingControl_is_the_live_editor ()
        {
            // It was `=> null`, so every `grid.EditingControl as TextBox` idiom -- the documented way to
            // attach a KeyPress filter to the editor -- dereferenced null.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                Assert.Null (grid.EditingControl);

                grid.BeginEdit (true);

                Assert.NotNull (grid.EditingControl);
                Assert.Same (Editor (grid), grid.EditingControl);

                grid.EndEdit ();

                Assert.Null (grid.EditingControl);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void EditingControlShowing_receives_the_cells_real_style ()
        {
            // It was handed a fresh empty DataGridViewCellStyle, so a handler reading e.CellStyle to
            // match the editor to the cell got defaults for everything.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].DefaultCellStyle.BackColor = Color.Salmon;
                DataGridViewCellStyle? seen = null;
                grid.EditingControlShowing += (_, e) => seen = e.CellStyle;

                grid.BeginEdit (true);

                Assert.NotNull (seen);
                Assert.Equal (Color.Salmon, seen!.BackColor);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-11: moving the current cell is an event

        [Fact]
        public void Moving_the_current_cell_raises_CurrentCellChanged_once ()
        {
            // It was raised only from the CurrentCell setter, so a click or a keyboard move never
            // raised it and master-detail forms that refresh on it never refreshed.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var raised = 0;
                grid.CurrentCellChanged += (_, _) => raised++;

                grid.ClickCell (2, 1);

                Assert.Equal (1, raised);
                Assert.Same (grid.Rows[2].Cells[1], grid.CurrentCell);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Leaving_a_cell_raises_CellLeave_and_entering_one_raises_CellEnter ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var order = new List<string> ();
                grid.CellLeave += (_, e) => order.Add ($"leave {e.RowIndex},{e.ColumnIndex}");
                grid.CellEnter += (_, e) => order.Add ($"enter {e.RowIndex},{e.ColumnIndex}");

                grid.SelectedRowIndex = 1;

                Assert.Equal (new[] { "leave 0,0", "enter 1,0" }, order.ToArray ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Leaving_an_untouched_cell_still_validates_it ()
        {
            // Per-cell validation on Tab through cells nobody edited: CellValidating ran only inside
            // EndEdit, so a cell that was never edited was never validated.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var validated = new List<(int Row, int Column)> ();
                grid.CellValidating += (_, e) => validated.Add ((e.RowIndex, e.ColumnIndex));

                grid.SelectedColumnIndex = 1;

                Assert.Equal ((0, 0), Assert.Single (validated));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_cancelling_CellValidating_handler_keeps_the_current_cell_where_it_is ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.CellValidating += (_, e) => e.Cancel = true;

                grid.SelectedColumnIndex = 1;

                Assert.Equal (0, grid.SelectedColumnIndex);
                Assert.Same (grid.Rows[0].Cells[0], grid.CurrentCell);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Setting_the_current_cell_to_where_it_already_is_raises_nothing ()
        {
            // GUARD, not proof: the setters always compared first. It pins that routing every move
            // through one choke point did not start announcing non-moves.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var raised = 0;
                grid.CurrentCellChanged += (_, _) => raised++;

                grid.SelectedRowIndex = 0;
                grid.SelectedColumnIndex = 0;

                Assert.Equal (0, raised);
            } finally {
                form.Close ();
            }
        }
    }
}
