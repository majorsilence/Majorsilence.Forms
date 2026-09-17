using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 — wiring the candidates, first batch.
    //
    //   DataGridViewCheckBoxCell.TrueValue/FalseValue  only the COLUMN's mapping was ever consulted, so
    //                                                  a cell that overrode it was ticked by the
    //                                                  column's rule instead.
    //   ButtonBase.Command                             stored and read by nothing, so a button bound to
    //                                                  a command did nothing at all when clicked.
    [Collection ("Headless")]
    public class W62WiringBatchTests
    {
        // ---------------- per-cell check values

        private static DataGridView Grid (out Form form, out DataGridViewCheckBoxCell cell)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 260, Height = 120 };
            grid.Columns.Add (new DataGridViewCheckBoxColumn { HeaderText = "Flag", Width = 120 });
            grid.Rows.Add ();

            cell = new DataGridViewCheckBoxCell ();
            grid.Rows[0].Cells[0] = cell;

            form = new Form { Width = 360, Height = 220 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        [Fact]
        public void A_cells_own_TrueValue_decides_whether_it_is_checked ()
        {
            using var grid = Grid (out var form, out var cell);

            try {
                cell.TrueValue = "Y";
                cell.Value = "Y";

                Assert.True (DataGridView.IsCheckedValue (grid.Columns[0], cell.Value, cell));

                cell.Value = "N";

                Assert.False (DataGridView.IsCheckedValue (grid.Columns[0], cell.Value, cell));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_cells_mapping_beats_the_columns ()
        {
            // The point of having it per cell. With only the column consulted, a cell that overrode the
            // mapping was ticked by the column's rule -- the opposite answer for the same value.
            using var grid = Grid (out var form, out var cell);

            try {
                ((DataGridViewCheckBoxColumn)grid.Columns[0]).TrueValue = "1";
                cell.TrueValue = "Y";
                cell.Value = "Y";

                Assert.True (DataGridView.IsCheckedValue (grid.Columns[0], cell.Value, cell));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_cells_FalseValue_is_honoured_too ()
        {
            using var grid = Grid (out var form, out var cell);

            try {
                // The column is deliberately set to call this same value TRUE. Without the cell's
                // FalseValue being read, the column's rule wins and the answer flips -- which is the
                // only arrangement that distinguishes the two. Asserting "off is not checked" against a
                // bare column passes on the "True"/"1" fallback whether or not FalseValue is read at
                // all, which is what the first version of this test did.
                ((DataGridViewCheckBoxColumn)grid.Columns[0]).TrueValue = "off";
                cell.FalseValue = "off";
                cell.Value = "off";

                Assert.False (DataGridView.IsCheckedValue (grid.Columns[0], cell.Value, cell));
                Assert.True (DataGridView.IsCheckedValue (grid.Columns[0], "off", null));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Without_a_cell_mapping_the_column_still_decides ()
        {
            // GUARD: the cell's mapping is an override, not a replacement. Every existing grid relies
            // on the column path, which must keep working untouched.
            using var grid = Grid (out var form, out var cell);

            try {
                ((DataGridViewCheckBoxColumn)grid.Columns[0]).TrueValue = "1";
                cell.Value = "1";

                Assert.True (DataGridView.IsCheckedValue (grid.Columns[0], cell.Value, cell));
                Assert.True (DataGridView.IsCheckedValue (grid.Columns[0], "1", null));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_plain_bool_still_works ()
        {
            // GUARD: the overwhelmingly common case, which has no mapping at all on either side.
            using var grid = Grid (out var form, out var cell);

            try {
                cell.Value = true;

                Assert.True (DataGridView.IsCheckedValue (grid.Columns[0], cell.Value, cell));
            } finally {
                form.Close ();
            }
        }

        // ---------------- ButtonBase.Command

        private sealed class CountingCommand : ICommandExecutor
        {
            public int Runs { get; private set; }

            public event System.EventHandler? CommandCanExecuteChanged { add { } remove { } }

            public void Execute () => Runs++;
        }

        [Fact]
        public void Clicking_a_button_runs_its_command ()
        {
            using var button = new Button { Text = "Go" };
            var command = new CountingCommand ();

            button.Command = command;
            button.PerformClick ();

            Assert.Equal (1, command.Runs);
        }

        [Fact]
        public void A_button_with_no_command_still_clicks ()
        {
            // GUARD: the command runs after the Click handlers, so a button without one must be
            // unaffected -- which is every button that exists today.
            using var button = new Button { Text = "Go" };

            var clicked = 0;
            button.Click += (_, _) => clicked++;

            button.PerformClick ();

            Assert.Equal (1, clicked);
        }

        [Fact]
        public void The_command_runs_after_the_click_handlers ()
        {
            // Upstream's order, and it matters: a handler that reconfigures or disables the button gets
            // to run first.
            using var button = new Button { Text = "Go" };
            var order = new System.Collections.Generic.List<string> ();

            button.Click += (_, _) => order.Add ("handler");
            button.Command = new OrderingCommand (order);

            button.PerformClick ();

            Assert.Equal (new[] { "handler", "command" }, order);
        }

        private sealed class OrderingCommand (System.Collections.Generic.List<string> order) : ICommandExecutor
        {
            public event System.EventHandler? CommandCanExecuteChanged { add { } remove { } }

            public void Execute () => order.Add ("command");
        }
    }
}
