using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the reopened entries -- the grid's FlatStyle family (DGV-44). Five baseline entries across
    // three cell types and two columns, one cause: no cell renderer had ever read FlatStyle, so every
    // button, check box and combo cell drew its 3D chrome whatever the property said.
    //
    // The chrome is the whole test: each of those renderers draws a frame (or, for the combo, a
    // separator rule) in Theme.BorderLowColor and then its content. Flat means the frame goes and the
    // content stays -- suppressing both would leave a cell that says nothing.
    //
    // Popup counts as flat. Upstream raises a Popup frame under the pointer and this grid tracks no
    // hovered cell, the same limit HoverUnderline hits on a link cell (DGV-43). Stated, not faked.
    [Collection ("Headless")]
    public class DataGridViewFlatStyleTests
    {
        private static DataGridView Grid (out Form form, DataGridViewColumn column, DataGridViewCell cell)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 320, Height = 160 };

            column.HeaderText = "Cell";
            column.Width = 200;
            grid.Columns.Add (column);
            grid.Rows.Add ();
            grid.Rows[0].Cells[0] = cell;

            form = new Form { Width = 420, Height = 280 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        [Fact]
        public void A_flat_button_cell_draws_no_frame ()
        {
            var cell = new DataGridViewButtonCell { Value = "Go" };
            var grid = Grid (out var form, new DataGridViewButtonColumn (), cell);

            using (form) {
                var framed = Ink (grid, cell);

                cell.FlatStyle = FlatStyle.Flat;

                Assert.True (framed > Ink (grid, cell), "A flat button cell still drew its frame.");
            }
        }

        [Fact]
        public void A_flat_check_box_cell_draws_no_box ()
        {
            var cell = new DataGridViewCheckBoxCell { Value = true };
            var grid = Grid (out var form, new DataGridViewCheckBoxColumn (), cell);

            using (form) {
                var framed = Ink (grid, cell);

                cell.FlatStyle = FlatStyle.Flat;
                var flat = Ink (grid, cell);

                Assert.True (framed > flat, "A flat check-box cell still drew its box.");

                // The tick survives -- the cell still says whether it is checked.
                Assert.True (flat > 0, "A flat checked cell drew nothing at all.");
            }
        }

        [Fact]
        public void A_flat_combo_cell_draws_no_separator ()
        {
            var cell = new DataGridViewComboBoxCell { Value = "One" };
            var grid = Grid (out var form, new DataGridViewComboBoxColumn (), cell);

            using (form) {
                var framed = Ink (grid, cell);

                cell.FlatStyle = FlatStyle.Flat;
                var flat = Ink (grid, cell);

                Assert.True (framed > flat, "A flat combo cell still drew its separator rule.");

                // The arrow survives -- it is what marks the cell as a drop-down.
                Assert.True (flat > 0, "A flat combo cell drew nothing at all.");
            }
        }

        [Fact]
        public void Popup_is_flat_because_no_cell_hover_is_tracked ()
        {
            var cell = new DataGridViewButtonCell { Value = "Go" };
            var grid = Grid (out var form, new DataGridViewButtonColumn (), cell);

            using (form) {
                cell.FlatStyle = FlatStyle.Flat;
                var flat = Ink (grid, cell);

                cell.FlatStyle = FlatStyle.Popup;

                Assert.Equal (flat, Ink (grid, cell));
            }
        }

        [Fact]
        public void Standard_and_System_keep_the_chrome ()
        {
            var cell = new DataGridViewButtonCell { Value = "Go" };
            var grid = Grid (out var form, new DataGridViewButtonColumn (), cell);

            using (form) {
                cell.FlatStyle = FlatStyle.Flat;
                var flat = Ink (grid, cell);

                cell.FlatStyle = FlatStyle.Standard;
                Assert.True (Ink (grid, cell) > flat);

                cell.FlatStyle = FlatStyle.System;
                Assert.True (Ink (grid, cell) > flat, "System was treated as flat.");
            }
        }

        // The column's setter is the one a designer writes; it has to reach cells that already exist,
        // not just the ones created afterwards.
        [Fact]
        public void The_column_pushes_its_FlatStyle_into_existing_cells ()
        {
            var cell = new DataGridViewButtonCell { Value = "Go" };
            var column = new DataGridViewButtonColumn ();
            var grid = Grid (out var form, column, cell);

            using (form) {
                var framed = Ink (grid, cell);

                column.FlatStyle = FlatStyle.Flat;

                Assert.Equal (FlatStyle.Flat, cell.FlatStyle);
                Assert.True (framed > Ink (grid, cell), "The column's FlatStyle never reached the cell.");
            }
        }

        [Fact]
        public void A_check_box_column_pushes_its_FlatStyle_too ()
        {
            var cell = new DataGridViewCheckBoxCell { Value = true };
            var column = new DataGridViewCheckBoxColumn ();
            var grid = Grid (out var form, column, cell);

            using (form) {
                var framed = Ink (grid, cell);

                column.FlatStyle = FlatStyle.Flat;

                Assert.Equal (FlatStyle.Flat, cell.FlatStyle);
                Assert.True (framed > Ink (grid, cell));
            }
        }

        // GetCellDisplayRectangle is LOGICAL (W6.3); the bitmap is DEVICE.
        private static int Ink (DataGridView grid, DataGridViewCell cell)
        {
            using var bitmap = PaintSurface.Render (grid);
            var r = grid.GetCellDisplayRectangle (cell.ColumnIndex, cell.RowIndex, false);
            var bounds = new Rectangle (
                grid.LogicalToDeviceUnits (r.Left), grid.LogicalToDeviceUnits (r.Top),
                grid.LogicalToDeviceUnits (r.Width), grid.LogicalToDeviceUnits (r.Height));

            var background = bitmap.GetPixel (bounds.Left + 1, bounds.Top + 1);
            var ink = 0;

            for (var y = bounds.Top; y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }
    }
}
