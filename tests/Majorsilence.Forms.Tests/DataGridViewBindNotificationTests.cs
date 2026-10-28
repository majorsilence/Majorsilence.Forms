using System;
using System.Collections.Generic;
using System.Data;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A bind makes the first cell current, and System.Windows.Forms ANNOUNCES it: forms fill a detail
    // pane from SelectionChanged or CurrentCellChanged, and upstream fills that pane the moment the grid
    // is bound. Here the cell was seeded silently -- fields written directly "to avoid raising selection
    // events mid-bind" -- so only DataBindingComplete was raised and the pane stayed empty until a click.
    //
    // Measured on System.Windows.Forms (.NET 10), DataGridView on a shown form:
    //
    //   bind 2 rows                RowEnter 0, CurrentCellChanged, CellEnter 0,0, SelectionChanged
    //                              (1 cell selected), DataBindingComplete
    //   rebind a new 2-row table   SelectionChanged (nothing selected), CellLeave 1,1, RowLeave 1,
    //   ((1,1) current)            CurrentCellChanged (no current cell), RowEnter 0, CurrentCellChanged,
    //                              CellEnter 0,0, SelectionChanged, DataBindingComplete
    //   bind a 0-row table         SelectionChanged, CellLeave, RowLeave, CurrentCellChanged (none),
    //                              DataBindingComplete
    //   DataSource = null          SelectionChanged, CellLeave, RowLeave, CurrentCellChanged (none)
    //   the same DataSource again  nothing
    //   rows added to an empty     the first: RowEnter 0, CurrentCellChanged, CellEnter 0,0,
    //   bound table                SelectionChanged, DataBindingComplete; the second: DataBindingComplete
    //   grid on an unshown form    the bind's sequence and DataBindingComplete when the form is shown
    //
    // Not reproduced: the first bind of a grid on a form whose BindingContext does not exist yet runs the
    // sequence twice upstream (creating the BindingContext re-binds the grid); upstream also has no
    // current cell at all before the grid's handle exists, where this grid seeds one silently.
    [Collection ("Headless")]
    public class DataGridViewBindNotificationTests
    {
        private static DataTable Table (int rows)
        {
            var table = new DataTable ("T");
            table.Columns.Add ("Id", typeof (int));
            table.Columns.Add ("Comment");

            for (var i = 0; i < rows; i++)
                table.Rows.Add (i + 1, "c" + (i + 1));

            return table;
        }

        private static List<string> Record (DataGridView grid)
        {
            var log = new List<string> ();
            string current () => grid.CurrentCell is { } cell ? $"{cell.RowIndex},{cell.ColumnIndex}" : "none";
            grid.RowEnter += (_, e) => log.Add ($"RowEnter {e.RowIndex}");
            grid.RowLeave += (_, e) => log.Add ($"RowLeave {e.RowIndex}");
            grid.CellEnter += (_, e) => log.Add ($"CellEnter {e.RowIndex},{e.ColumnIndex}");
            grid.CellLeave += (_, e) => log.Add ($"CellLeave {e.RowIndex},{e.ColumnIndex}");
            grid.CurrentCellChanged += (_, _) => log.Add ($"CurrentCellChanged {current ()}");
            grid.SelectionChanged += (_, _) => log.Add ($"SelectionChanged {grid.SelectedCells.Count}");
            grid.DataBindingComplete += (_, e) => log.Add ($"DataBindingComplete {e.ListChangedType}");
            return log;
        }

        private static (Form Form, DataGridView Grid) Shown ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { Width = 600, Height = 400 };
            var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
            form.Controls.Add (grid);
            form.Show ();
            return (form, grid);
        }

        private static readonly string[] BindSequence = {
            "RowEnter 0",
            "CurrentCellChanged 0,0",
            "CellEnter 0,0",
            "SelectionChanged 1",
            "DataBindingComplete Reset",
        };

        [Fact]
        public void Binding_rows_announces_the_first_cell_before_DataBindingComplete ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var log = Record (grid);

            grid.DataSource = Table (2);

            Assert.Equal (BindSequence, log);
        }

        [Fact]
        public void A_detail_pane_filled_from_SelectionChanged_is_filled_by_the_bind ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var notes = new TextBox ();
            form.Controls.Add (notes);
            grid.SelectionChanged += (_, _) => notes.Text = grid.CurrentRow?.Cells["Comment"]?.Value?.ToString () ?? "";

            grid.DataSource = Table (2);

            Assert.Equal ("c1", notes.Text);
        }

        [Fact]
        public void Rebinding_leaves_the_current_cell_and_announces_the_first_one ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            grid.DataSource = Table (2);
            grid.CurrentCell = grid.Rows[1].Cells[1];
            var log = Record (grid);

            grid.DataSource = Table (2);

            var expected = new List<string> {
                "SelectionChanged 0",
                "CellLeave 1,1",
                "RowLeave 1",
                "CurrentCellChanged none",
            };
            expected.AddRange (BindSequence);
            Assert.Equal (expected, log);
            Assert.Same (grid.Rows[0].Cells[0], grid.CurrentCell);
        }

        [Fact]
        public void Binding_no_rows_after_rows_leaves_the_current_cell ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            grid.DataSource = Table (2);
            var log = Record (grid);

            grid.DataSource = Table (0);

            Assert.Equal (new[] {
                "SelectionChanged 0",
                "CellLeave 0,0",
                "RowLeave 0",
                "CurrentCellChanged none",
                "DataBindingComplete Reset",
            }, log);
            Assert.Null (grid.CurrentCell);
        }

        [Fact]
        public void Unbinding_leaves_the_current_cell_without_DataBindingComplete ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            grid.DataSource = Table (2);
            var log = Record (grid);

            grid.DataSource = null;

            Assert.Equal (new[] {
                "SelectionChanged 0",
                "CellLeave 0,0",
                "RowLeave 0",
                "CurrentCellChanged none",
            }, log);
        }

        [Fact]
        public void Assigning_the_same_DataSource_again_raises_nothing ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var table = Table (2);
            grid.DataSource = table;
            var log = Record (grid);

            grid.DataSource = table;

            Assert.Empty (log);
        }

        [Fact]
        public void The_first_row_added_to_an_empty_bound_table_is_announced ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var table = Table (0);
            grid.DataSource = table;
            var log = Record (grid);

            table.Rows.Add (1, "c1");
            table.Rows.Add (2, "c2");

            Assert.Equal (new[] {
                "RowEnter 0",
                "CurrentCellChanged 0,0",
                "CellEnter 0,0",
                "SelectionChanged 1",
                "DataBindingComplete ItemAdded",
                "DataBindingComplete ItemAdded",
            }, log);
        }

        [Fact]
        public void A_grid_bound_before_its_form_is_shown_announces_the_bind_when_shown ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 600, Height = 400 };
            var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
            form.Controls.Add (grid);
            grid.DataSource = Table (2);
            var log = Record (grid);

            form.Show ();

            Assert.Equal (BindSequence, log.GetRange (0, Math.Min (log.Count, BindSequence.Length)));
        }
    }
}
