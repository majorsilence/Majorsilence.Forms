using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the reopened entries -- the DataGridView link family (DGV-43). An earlier slice wired the
    // colours (LinkColor/VisitedLinkColor/LinkVisited); four members were left stored and unread:
    //
    //   LinkCell.LinkBehavior     the underline
    //   LinkColumn.LinkBehavior   the column-wide default for it
    //   LinkCell.TrackVisitedState  whether clicking marks the link visited
    //   LinkCell.ActiveLinkColor  the colour while the cell is held down
    //
    // ActiveLinkColor is the interesting one: the SAME property on ToolStripLabel is deliberately
    // unwired (TSM-42), because nothing tracks a pressed strip item. DataGridView does track one --
    // mouse_down_target -- so here it is expressible and is wired. Same property name, opposite call,
    // decided by what state each control actually has.
    [Collection ("Headless")]
    public class DataGridViewLinkCellTests
    {
        // Drives the protected entry points a real backend calls, the way
        // DataGridViewEditingLifecycleTests already does -- no test seam is added to the shipped type
        // for this.
        private sealed class LinkGrid : DataGridView
        {
            internal void PressAt (Point at)
                => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, Point.Empty));

            internal void ReleaseAt (Point at)
                => OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, Point.Empty));
        }

        private static LinkGrid Grid (out Form form, out DataGridViewLinkCell cell, out DataGridViewLinkColumn column)
        {
            HeadlessRenderer.Use ();

            var grid = new LinkGrid { Width = 320, Height = 160 };

            column = new DataGridViewLinkColumn { HeaderText = "Link", Width = 200 };
            grid.Columns.Add (column);
            grid.Rows.Add ();

            cell = new DataGridViewLinkCell { Value = "Open the report" };
            grid.Rows[0].Cells[0] = cell;

            form = new Form { Width = 420, Height = 280 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        [Fact]
        public void The_cells_LinkBehavior_controls_the_underline ()
        {
            var grid = Grid (out var form, out var cell, out _);

            using (form) {
                cell.LinkBehavior = LinkBehavior.AlwaysUnderline;
                var underlined = Ink (grid, cell);

                cell.LinkBehavior = LinkBehavior.NeverUnderline;

                Assert.True (underlined > Ink (grid, cell), "NeverUnderline still drew an underline.");
            }
        }

        // SystemDefault is the default value of the property, so treating it as "no underline" would
        // make the common case the wrong one.
        [Fact]
        public void SystemDefault_underlines ()
        {
            var grid = Grid (out var form, out var cell, out _);

            using (form) {
                Assert.Equal (LinkBehavior.SystemDefault, cell.LinkBehavior);
                var system_default = Ink (grid, cell);

                cell.LinkBehavior = LinkBehavior.NeverUnderline;

                Assert.True (system_default > Ink (grid, cell), "SystemDefault did not underline.");
            }
        }

        // A column sets the behaviour once for every link in it; the cell only falls through to it
        // while its own value is SystemDefault.
        [Fact]
        public void The_columns_LinkBehavior_applies_when_the_cell_has_none ()
        {
            var grid = Grid (out var form, out var cell, out var column);

            using (form) {
                Assert.Equal (LinkBehavior.SystemDefault, cell.LinkBehavior);

                column.LinkBehavior = LinkBehavior.NeverUnderline;
                var from_column = Ink (grid, cell);

                column.LinkBehavior = LinkBehavior.AlwaysUnderline;

                Assert.True (Ink (grid, cell) > from_column, "The column's LinkBehavior was not read.");
            }
        }

        [Fact]
        public void The_cells_LinkBehavior_wins_over_the_columns ()
        {
            var grid = Grid (out var form, out var cell, out var column);

            using (form) {
                column.LinkBehavior = LinkBehavior.NeverUnderline;
                cell.LinkBehavior = LinkBehavior.AlwaysUnderline;
                var cell_wins = Ink (grid, cell);

                cell.LinkBehavior = LinkBehavior.NeverUnderline;

                Assert.True (cell_wins > Ink (grid, cell), "The cell did not override its column.");
            }
        }

        // Without this, LinkVisited is only ever reachable by assigning it by hand, so the whole
        // visited/unvisited distinction is inert in any real application.
        [Fact]
        public void Clicking_a_link_marks_it_visited ()
        {
            var grid = Grid (out var form, out var cell, out _);

            using (form) {
                Assert.True (cell.TrackVisitedState);
                Assert.False (cell.LinkVisited);

                ClickCell (grid, cell);

                Assert.True (cell.LinkVisited);
            }
        }

        [Fact]
        public void TrackVisitedState_off_leaves_the_link_unvisited ()
        {
            var grid = Grid (out var form, out var cell, out _);

            using (form) {
                cell.TrackVisitedState = false;

                ClickCell (grid, cell);

                Assert.False (cell.LinkVisited);
            }
        }

        // The pressed colour, which ToolStripLabel cannot express and this can.
        [Fact]
        public void ActiveLinkColor_is_used_while_the_cell_is_held_down ()
        {
            var grid = Grid (out var form, out var cell, out _);

            using (form) {
                cell.LinkColor = Color.Blue;
                cell.ActiveLinkColor = Color.Red;

                Assert.False (HasPixel (grid, cell, p => p.Red > 180 && p.Green < 90 && p.Blue < 90),
                    "The active colour was drawn before anything was pressed.");

                var at = CellCentre (grid, cell);
                grid.PressAt (at);

                Assert.True (HasPixel (grid, cell, p => p.Red > 180 && p.Green < 90 && p.Blue < 90),
                    "ActiveLinkColor was not used while the cell was held down.");

                grid.ReleaseAt (at);

                Assert.False (HasPixel (grid, cell, p => p.Red > 180 && p.Green < 90 && p.Blue < 90),
                    "The active colour survived the release.");
            }
        }

        // Pressed outranks visited: it says what is happening now, not what happened before.
        [Fact]
        public void The_pressed_colour_wins_over_the_visited_one ()
        {
            var grid = Grid (out var form, out var cell, out _);

            using (form) {
                cell.LinkVisited = true;
                cell.VisitedLinkColor = Color.Lime;
                cell.ActiveLinkColor = Color.Red;

                grid.PressAt (CellCentre (grid, cell));

                Assert.True (HasPixel (grid, cell, p => p.Red > 180 && p.Green < 90 && p.Blue < 90));
                Assert.False (HasPixel (grid, cell, p => p.Green > 180 && p.Red < 90 && p.Blue < 90),
                    "The visited colour was drawn while the cell was pressed.");
            }
        }

        // GetCellBounds is the space MouseEventArgs are in -- the same pairing
        // DataGridViewEditingLifecycleTests uses to click a cell.
        private static Point CellCentre (DataGridView grid, DataGridViewCell cell)
        {
            var r = grid.GetCellBounds (cell.RowIndex, cell.ColumnIndex);

            return new Point (r.Left + r.Width / 2, r.Top + r.Height / 2);
        }

        private static void ClickCell (LinkGrid grid, DataGridViewCell cell)
        {
            var at = CellCentre (grid, cell);

            grid.PressAt (at);
            grid.ReleaseAt (at);
        }

        // GetCellDisplayRectangle is LOGICAL (W6.3); the bitmap is DEVICE, so the sampling window is
        // converted rather than used as-is.
        private static Rectangle DeviceCell (DataGridView grid, DataGridViewCell cell)
        {
            var r = grid.GetCellDisplayRectangle (cell.ColumnIndex, cell.RowIndex, false);

            return new Rectangle (
                grid.LogicalToDeviceUnits (r.Left), grid.LogicalToDeviceUnits (r.Top),
                grid.LogicalToDeviceUnits (r.Width), grid.LogicalToDeviceUnits (r.Height));
        }

        private static bool HasPixel (DataGridView grid, DataGridViewCell cell, System.Func<SkiaSharp.SKColor, bool> match)
        {
            using var bitmap = PaintSurface.Render (grid);
            var bounds = DeviceCell (grid, cell);

            for (var y = bounds.Top; y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (match (bitmap.GetPixel (x, y)))
                        return true;

            return false;
        }

        private static int Ink (DataGridView grid, DataGridViewCell cell)
        {
            using var bitmap = PaintSurface.Render (grid);
            var bounds = DeviceCell (grid, cell);
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
