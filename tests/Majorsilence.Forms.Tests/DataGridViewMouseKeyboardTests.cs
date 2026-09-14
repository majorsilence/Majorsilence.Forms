using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.5 (findings DGV-30, DGV-29, DGV-25).
    //
    // Eight cell-level mouse events were `add { } remove { }` -- they took a handler and discarded it --
    // and the three that were raised fired on mouse-DOWN where upstream raises them on mouse-up. All
    // keyboard navigation lived in OnKeyUp, which cannot auto-repeat; Enter, Delete and Ctrl+C did
    // nothing at all, and Home/End moved by row where upstream moves by column. The check-box column
    // toggled a bool straight into the cell, ignoring the column's TrueValue, the grid's ReadOnly and
    // the bound object.
    [Collection ("Headless")]
    public class DataGridViewMouseKeyboardTests
    {
        private sealed class Item : INotifyPropertyChanged
        {
            private string name = string.Empty;
            private bool done;

            public string Name { get => name; set { name = value; Raise (nameof (Name)); } }
            public bool Done { get => done; set { done = value; Raise (nameof (Done)); } }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void Raise (string p) => PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (p));
        }

        private sealed class DrivenGrid : DataGridView
        {
            internal void Down (Point p, MouseButtons button = MouseButtons.Left)
                => OnMouseDown (new MouseEventArgs (button, 1, p.X, p.Y, Point.Empty));

            internal void Up (Point p, MouseButtons button = MouseButtons.Left)
                => OnMouseUp (new MouseEventArgs (button, 1, p.X, p.Y, Point.Empty));

            internal void MoveTo (Point p) => OnMouseMove (new MouseEventArgs (MouseButtons.None, 0, p.X, p.Y, Point.Empty));

            internal void DoubleClickAt (Point p) => OnDoubleClick (new MouseEventArgs (MouseButtons.Left, 2, p.X, p.Y, Point.Empty));

            internal void ClickAt (Point p, MouseButtons button = MouseButtons.Left)
            {
                Down (p, button);
                Up (p, button);
            }

            internal void Key (Keys key) => OnKeyDown (new KeyEventArgs (key));

            internal Point CellCentre (int rowIndex, int columnIndex)
            {
                var b = GetCellBounds (rowIndex, columnIndex);
                return new Point (b.Left + b.Width / 2, b.Top + b.Height / 2);
            }

            internal Point RowHeaderCentre (int rowIndex)
            {
                var b = GetCellBounds (rowIndex, 0);
                return new Point (GetContentArea ().Left + ScaledRowHeadersWidth / 2, b.Top + b.Height / 2);
            }

            internal Point ColumnHeaderCentre (int columnIndex)
                => new Point (GetColumnDeviceLeft (columnIndex) + LogicalToDeviceUnits (Columns[columnIndex].Width) / 2,
                              GetContentArea ().Top + ScaledHeaderHeight / 2);
        }

        private static DrivenGrid Grid (out Form form, int rows = 3, int columns = 2)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 520, Height = 340 };
            var grid = new DrivenGrid { Width = 400, Height = 220 };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Name = $"C{c}", Width = 80 });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.RenderOnForm (grid).Dispose ();     // cell bounds exist only once painted
            grid.MoveCurrentCell (0, 0);
            grid.ClearSelection ();
            grid.Rows[0].Selected = true;

            return grid;
        }

        // ---------------- DGV-30: the cell mouse events are raised

        [Fact]
        public void CellMouseDown_reports_the_cell_and_the_button ()
        {
            // The finding's own idiom: a right-click handler that makes the clicked cell current, so a
            // context menu acts on the row under the pointer. The event discarded its subscribers, so
            // the menu acted on whatever row happened to be current.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var seen = new List<(int Row, int Column, MouseButtons Button)> ();
                grid.CellMouseDown += (_, e) => seen.Add ((e.RowIndex, e.ColumnIndex, e.Button));

                grid.Down (grid.CellCentre (2, 1), MouseButtons.Right);

                Assert.Equal ((2, 1, MouseButtons.Right), Assert.Single (seen));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void CellMouseDown_reports_coordinates_relative_to_the_cell ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                DataGridViewCellMouseEventArgs? seen = null;
                grid.CellMouseDown += (_, e) => seen = e;
                var bounds = grid.GetCellBounds (1, 1);

                grid.Down (new Point (bounds.Left + 5, bounds.Top + 3));

                Assert.NotNull (seen);
                Assert.Equal (5, seen!.X);
                Assert.Equal (3, seen.Y);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void CellClick_is_raised_on_release_not_on_press ()
        {
            // Upstream raises the click events on mouse-up. They fired from OnMouseDown, so a handler
            // that acts on a click ran while the button was still down.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var clicks = 0;
                var content_clicks = 0;
                var mouse_clicks = 0;
                grid.CellClick += (_, _) => clicks++;
                grid.CellContentClick += (_, _) => content_clicks++;
                grid.CellMouseClick += (_, _) => mouse_clicks++;

                grid.Down (grid.CellCentre (1, 0));

                Assert.Equal (0, clicks);
                Assert.Equal (0, content_clicks);
                Assert.Equal (0, mouse_clicks);

                grid.Up (grid.CellCentre (1, 0));

                Assert.Equal (1, clicks);
                Assert.Equal (1, content_clicks);
                Assert.Equal (1, mouse_clicks);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void CellMouseUp_is_raised_even_when_the_release_is_not_a_click ()
        {
            // Pressing on one cell and releasing on another is a drag. The release is still a release --
            // CellMouseUp reports it -- but neither cell was clicked.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var ups = 0;
                var clicks = 0;
                grid.CellMouseUp += (_, _) => ups++;
                grid.CellClick += (_, _) => clicks++;

                grid.Down (grid.CellCentre (0, 0));
                grid.Up (grid.CellCentre (2, 1));

                Assert.Equal (1, ups);
                Assert.Equal (0, clicks);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void CellMouseMove_reports_the_cell_under_the_pointer ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var seen = new List<(int Row, int Column)> ();
                grid.CellMouseMove += (_, e) => seen.Add ((e.RowIndex, e.ColumnIndex));

                grid.MoveTo (grid.CellCentre (1, 1));

                Assert.Contains ((1, 1), seen);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_row_header_click_reports_the_row_with_no_column ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.RowHeadersVisible = true;
                PaintSurface.RenderOnForm (grid).Dispose ();
                var seen = new List<(int Row, int Column)> ();
                grid.RowHeaderMouseClick += (_, e) => seen.Add ((e.RowIndex, e.ColumnIndex));

                grid.ClickAt (grid.RowHeaderCentre (1));

                // ColumnIndex -1 is how upstream says "the header, not a cell".
                Assert.Equal ((1, -1), Assert.Single (seen));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Double_clicks_reach_the_cell_and_the_headers ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.RowHeadersVisible = true;
                PaintSurface.RenderOnForm (grid).Dispose ();
                var cell = 0;
                var content = 0;
                var column_header = 0;
                var row_header = 0;
                grid.CellMouseDoubleClick += (_, _) => cell++;
                grid.CellContentDoubleClick += (_, _) => content++;
                grid.ColumnHeaderMouseDoubleClick += (_, _) => column_header++;
                grid.RowHeaderMouseDoubleClick += (_, _) => row_header++;

                grid.DoubleClickAt (grid.CellCentre (1, 1));
                grid.DoubleClickAt (grid.ColumnHeaderCentre (0));
                grid.DoubleClickAt (grid.RowHeaderCentre (2));

                Assert.Equal (1, cell);
                Assert.Equal (1, content);
                Assert.Equal (1, column_header);
                Assert.Equal (1, row_header);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_click_still_moves_the_current_cell ()
        {
            // GUARD, not proof: selection on click was W5.2b's and is unchanged. It pins that moving the
            // click events to mouse-up did not move the selection with them -- upstream selects on the
            // press, and a grid that only selected on release would feel broken under a drag.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Down (grid.CellCentre (2, 1));

                Assert.Same (grid.Rows[2], grid.CurrentRow);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-29: the keyboard

        [Fact]
        public void Enter_moves_down_a_row ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Key (Keys.Enter);

                Assert.Equal (1, grid.SelectedRowIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Enter_commits_an_edit_in_progress ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.BeginEdit (true);
                Enumerable.OfType<TextBox> (grid.Controls).First ().Text = "typed";

                grid.Key (Keys.Enter);

                Assert.False (grid.IsCurrentCellInEditMode);
                Assert.Equal ("typed", grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Delete_removes_the_selected_rows_and_announces_each ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.ClearSelection ();
                grid.Rows[0].Selected = true;
                grid.Rows[2].Selected = true;
                var deleting = new List<DataGridViewRow> ();
                var deleted = new List<DataGridViewRow> ();
                grid.UserDeletingRow += (_, e) => deleting.Add (e.Row);
                grid.UserDeletedRow += (_, e) => deleted.Add (e.Row);

                grid.Key (Keys.Delete);

                Assert.Single (grid.Rows);
                Assert.Equal ("r1c0", grid.Rows[0].Cells[0].Value);
                Assert.Equal (2, deleting.Count);
                Assert.Equal (2, deleted.Count);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_cancelling_UserDeletingRow_handler_keeps_its_row ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.ClearSelection ();
                grid.Rows[0].Selected = true;
                grid.Rows[2].Selected = true;
                grid.UserDeletingRow += (_, e) => e.Cancel = e.Row.Index == 0;

                grid.Key (Keys.Delete);

                Assert.Equal (2, grid.Rows.Count);
                Assert.Equal ("r0c0", grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Delete_does_nothing_when_AllowUserToDeleteRows_is_off ()
        {
            // It was stored and read by nothing.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.AllowUserToDeleteRows = false;

                grid.Key (Keys.Delete);

                Assert.Equal (3, grid.Rows.Count);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Delete_on_a_bound_grid_removes_the_item_not_the_row ()
        {
            // Removing the ROW from a bound grid is undone by the next rebind (W5.3); the list is what
            // owns the rows.
            HeadlessRenderer.Use ();
            var list = new BindingList<Item> { new () { Name = "a" }, new () { Name = "b" } };
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DrivenGrid { Width = 400, Height = 220, DataSource = list };
            form.Controls.Add (grid);
            form.Show ();

            try {
                PaintSurface.RenderOnForm (grid).Dispose ();
                grid.ClearSelection ();
                grid.Rows[1].Selected = true;

                grid.Key (Keys.Delete);

                Assert.Single (list);
                Assert.Equal ("a", list[0].Name);
                Assert.Single (grid.Rows);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Ctrl_C_copies_the_selection ()
        {
            // GetClipboardContent () was implemented and working; no key reached it.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                Clipboard.Clear ();

                grid.Key (Keys.C | Keys.Control);

                var text = Clipboard.GetText ();
                Assert.False (string.IsNullOrEmpty (text), "Ctrl+C should have put the selection on the clipboard");
                Assert.Contains ("r0c0", text);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Home_and_End_move_along_the_row ()
        {
            // They jumped to the first/last ROW, which is what Ctrl+Home/End means -- so the unmodified
            // keys did the modified thing and the modified ones did nothing.
            var grid = Grid (out var form, rows: 3, columns: 3);
            using var _form = form;

            try {
                grid.MoveCurrentCell (1, 1);

                grid.Key (Keys.End);
                Assert.Equal (2, grid.SelectedColumnIndex);
                Assert.Equal (1, grid.SelectedRowIndex);

                grid.Key (Keys.Home);
                Assert.Equal (0, grid.SelectedColumnIndex);
                Assert.Equal (1, grid.SelectedRowIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Ctrl_Home_and_Ctrl_End_move_to_the_first_and_last_cell ()
        {
            var grid = Grid (out var form, rows: 3, columns: 3);
            using var _form = form;

            try {
                grid.MoveCurrentCell (1, 1);

                grid.Key (Keys.End | Keys.Control);
                Assert.Equal (2, grid.SelectedRowIndex);
                Assert.Equal (2, grid.SelectedColumnIndex);

                grid.Key (Keys.Home | Keys.Control);
                Assert.Equal (0, grid.SelectedRowIndex);
                Assert.Equal (0, grid.SelectedColumnIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Left_and_Right_move_between_cells_in_every_selection_mode ()
        {
            // They were ignored entirely in FullRowSelect.
            var grid = Grid (out var form, rows: 2, columns: 3);
            using var _form = form;

            try {
                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.MoveCurrentCell (0, 0);

                grid.Key (Keys.Right);
                Assert.Equal (1, grid.SelectedColumnIndex);

                grid.Key (Keys.Left);
                Assert.Equal (0, grid.SelectedColumnIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_arrows_are_handled_on_key_down ()
        {
            // On key-up they could not auto-repeat: holding Down moved one row and stopped. This asserts
            // the handling is on the DOWN, which is what repeats.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var args = new KeyEventArgs (Keys.Down);
                grid.OnKeyDownForTest (args);

                Assert.True (args.Handled);
                Assert.Equal (1, grid.SelectedRowIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void StandardTab_leaves_Tab_to_the_form ()
        {
            var grid = Grid (out var form, rows: 2, columns: 3);
            using var _form = form;

            try {
                grid.MoveCurrentCell (0, 0);
                grid.StandardTab = true;

                var args = new KeyEventArgs (Keys.Tab);
                grid.OnKeyDownForTest (args);

                Assert.False (args.Handled);
                Assert.Equal (0, grid.SelectedColumnIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Tab_moves_to_the_next_cell_by_default ()
        {
            // GUARD, not proof: Tab already moved cells in the non-row modes. It pins that the
            // StandardTab gate did not stop the default.
            var grid = Grid (out var form, rows: 2, columns: 3);
            using var _form = form;

            try {
                grid.MoveCurrentCell (0, 0);

                grid.Key (Keys.Tab);

                Assert.Equal (1, grid.SelectedColumnIndex);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-25: the check box commits

        [Fact]
        public void Clicking_a_check_box_writes_through_to_the_bound_object ()
        {
            // It assigned a bool straight into the cell, so a bound item never changed and the next
            // ListChanged reverted the tick.
            HeadlessRenderer.Use ();
            var list = new BindingList<Item> { new () { Name = "a" } };
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DrivenGrid { Width = 400, Height = 220, AutoGenerateColumns = false };
            grid.Columns.Add (new DataGridViewCheckBoxColumn { HeaderText = "Done", DataPropertyName = nameof (Item.Done), Width = 80 });
            grid.DataSource = list;
            form.Controls.Add (grid);
            form.Show ();

            try {
                PaintSurface.RenderOnForm (grid).Dispose ();

                grid.ClickAt (grid.CellCentre (0, 0));

                Assert.True (list[0].Done);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_check_box_column_stores_its_own_TrueValue ()
        {
            // A char-flag column ("Y"/"N", everywhere in DataTable-backed apps) never rendered checked
            // and toggling wrote a bool into a string column.
            var grid = Grid (out var form, rows: 1, columns: 1);
            using var _form = form;

            try {
                var column = new DataGridViewCheckBoxColumn { HeaderText = "Flag", Width = 80, TrueValue = "Y", FalseValue = "N" };
                grid.Columns.Clear ();
                grid.Columns.Add (column);
                grid.Rows[0].Cells[0].Value = "N";
                PaintSurface.RenderOnForm (grid).Dispose ();

                grid.ClickAt (grid.CellCentre (0, 0));

                Assert.Equal ("Y", grid.Rows[0].Cells[0].Value);

                grid.ClickAt (grid.CellCentre (0, 0));

                Assert.Equal ("N", grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_read_only_grid_does_not_toggle_a_check_box ()
        {
            // Only cell.ReadOnly was checked, so grid.ReadOnly = true still toggled.
            var grid = Grid (out var form, rows: 1, columns: 1);
            using var _form = form;

            try {
                grid.Columns.Clear ();
                grid.Columns.Add (new DataGridViewCheckBoxColumn { HeaderText = "Done", Width = 80 });
                grid.Rows[0].Cells[0].Value = false;
                PaintSurface.RenderOnForm (grid).Dispose ();
                grid.ReadOnly = true;

                grid.ClickAt (grid.CellCentre (0, 0));

                Assert.Equal (false, grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Toggling_a_check_box_raises_CellValueChanged_once ()
        {
            var grid = Grid (out var form, rows: 1, columns: 1);
            using var _form = form;

            try {
                grid.Columns.Clear ();
                grid.Columns.Add (new DataGridViewCheckBoxColumn { HeaderText = "Done", Width = 80 });
                grid.Rows[0].Cells[0].Value = false;
                PaintSurface.RenderOnForm (grid).Dispose ();
                var changed = 0;
                grid.CellValueChanged += (_, _) => changed++;

                grid.ClickAt (grid.CellCentre (0, 0));

                Assert.Equal (1, changed);
                Assert.Equal (true, grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_plain_cell_click_toggles_nothing ()
        {
            // GUARD, not proof: only check-box columns ever toggled. It pins that moving the toggle to
            // mouse-up did not start toggling ordinary cells.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var before = grid.Rows[1].Cells[0].Value;

                grid.ClickAt (grid.CellCentre (1, 0));

                Assert.Equal (before, grid.Rows[1].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }
    }
}
