using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // DGV-23: AllowUserToOrderColumns. Dragging a column header past the drag threshold moves the column
    // in the display order, after upstream's BeginColumnRelocation / ColumnRelocationTarget /
    // EndColumnRelocation (Controls/DataGridView/DataGridView.Methods.cs). A drag has to be told from a
    // click, so the header click -- the sort and ColumnHeaderMouseClick -- moved from the press to the
    // release, where upstream raises it.
    //
    // DGV-34's remainder is here too: VerticalScrollingOffset answered a row index and
    // FirstDisplayedScrollingColumnHiddenWidth answered 0. All three scrolling offsets are LOGICAL, as every
    // public offset is (RC-8); HorizontalScrollingOffset answered device pixels, double upstream's at scale 2.
    [Collection ("Headless")]
    public sealed class DataGridViewColumnReorderTests : IDisposable
    {
        private readonly double original_ui_scale = Application.UiScale;

        public void Dispose () => Application.UiScale = original_ui_scale;

        private sealed class DrivenGrid : DataGridView
        {
            internal void Down (Point p) => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));

            internal void MoveTo (Point p) => OnMouseMove (new MouseEventArgs (MouseButtons.Left, 0, p.X, p.Y, Point.Empty));

            internal void Up (Point p) => OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));

            // LOGICAL, as MouseEventArgs carry them; the geometry is device (RC-8).
            internal Rectangle LogicalCell (int rowIndex, int columnIndex) => DeviceToLogicalUnits (GetCellBounds (rowIndex, columnIndex));
        }

        private static DrivenGrid Grid (out Form form, int columns = 4, int rows = 3, int columnWidth = 80)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 600, Height = 340 };
            var grid = new DrivenGrid { Width = 480, Height = 220, AllowUserToOrderColumns = true };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Name = $"C{c}", Width = columnWidth });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        private static Point HeaderCentre (DrivenGrid grid, int column)
        {
            var header = grid.LogicalCell (-1, column);
            return new Point (header.Left + header.Width / 2, header.Top + header.Height / 2);
        }

        // A point in the right quarter of a header: "after this column".
        private static Point HeaderRightPart (DrivenGrid grid, int column)
        {
            var header = grid.LogicalCell (-1, column);
            return new Point (header.Right - header.Width / 4, header.Top + header.Height / 2);
        }

        private static void Drag (DrivenGrid grid, Point from, Point to)
        {
            grid.Down (from);
            grid.MoveTo (new Point ((from.X + to.X) / 2, to.Y));
            grid.MoveTo (to);
            grid.Up (to);
        }

        private static int[] Order (DataGridView grid)
            => grid.Columns.Cast<DataGridViewColumn> ().OrderBy (c => c.DisplayIndex).Select (c => c.Index).ToArray ();

        // ---------------- the click moved to the release

        [Fact]
        public void A_header_press_does_not_sort_and_the_release_does ()
        {
            var grid = Grid (out var form, columns: 2);

            using (form) {
                var clicks = 0;
                grid.ColumnHeaderMouseClick += (_, e) => {
                    clicks++;
                    Assert.Equal (-1, e.RowIndex);
                    Assert.Equal (0, e.ColumnIndex);
                };

                grid.Down (HeaderCentre (grid, 0));

                Assert.Null (grid.SortedColumn);
                Assert.Equal (0, clicks);

                grid.Up (HeaderCentre (grid, 0));

                Assert.Same (grid.Columns[0], grid.SortedColumn);
                Assert.Equal (1, clicks);
            }
        }

        [Fact]
        public void A_press_on_one_header_released_on_another_is_not_a_click ()
        {
            var grid = Grid (out var form);

            using (form) {
                grid.AllowUserToOrderColumns = false;
                var clicks = 0;
                grid.ColumnHeaderMouseClick += (_, _) => clicks++;

                Drag (grid, HeaderCentre (grid, 0), HeaderCentre (grid, 2));

                Assert.Null (grid.SortedColumn);
                Assert.Equal (0, clicks);
                // Ordering not allowed: nothing moved either.
                Assert.Equal (new[] { 0, 1, 2, 3 }, Order (grid));
            }
        }

        // ---------------- the drag

        [Fact]
        public void Dragging_a_header_past_another_moves_the_column_and_announces_each_column_that_moved ()
        {
            var grid = Grid (out var form);

            using (form) {
                var moved = new List<int> ();
                var clicks = 0;
                grid.ColumnDisplayIndexChanged += (_, e) => moved.Add (e.Column.Index);
                grid.ColumnHeaderMouseClick += (_, _) => clicks++;

                Drag (grid, HeaderCentre (grid, 0), HeaderRightPart (grid, 2));

                Assert.Equal (new[] { 1, 2, 0, 3 }, Order (grid));
                Assert.Equal (2, grid.Columns[0].DisplayIndex);
                Assert.Equal (new[] { 0, 1, 2 }, moved.OrderBy (i => i));

                // A drag is not a click: no sort, no ColumnHeaderMouseClick.
                Assert.Null (grid.SortedColumn);
                Assert.Equal (0, clicks);

                // The header and the cells follow the display order: column 0 is now drawn third.
                Assert.True (grid.LogicalCell (0, 0).Left > grid.LogicalCell (0, 2).Left);
            }
        }

        [Fact]
        public void Dragging_to_the_left_half_of_the_first_header_moves_the_column_first ()
        {
            var grid = Grid (out var form);

            using (form) {
                var first = grid.LogicalCell (-1, 0);
                Drag (grid, HeaderCentre (grid, 3), new Point (first.Left + 3, first.Top + first.Height / 2));

                Assert.Equal (new[] { 3, 0, 1, 2 }, Order (grid));
            }
        }

        [Fact]
        public void A_drop_beside_the_column_itself_moves_nothing ()
        {
            // The left half of the next header means "after the column itself": upstream's
            // ColumnRelocationTarget answers no target, so no DisplayIndex changes.
            var grid = Grid (out var form);

            using (form) {
                var moved = 0;
                grid.ColumnDisplayIndexChanged += (_, _) => moved++;
                var next = grid.LogicalCell (-1, 2);

                var beside = new Point (next.Left + 4, next.Top + next.Height / 2);

                // No insertion bar is offered for a drop that would not move the column.
                grid.Down (HeaderCentre (grid, 1));
                grid.MoveTo (beside);
                Assert.True (grid.GetColumnRelocationFeedback (out _, out var bar));
                Assert.True (bar.IsEmpty);
                grid.Up (beside);

                Assert.Equal (new[] { 0, 1, 2, 3 }, Order (grid));
                Assert.Equal (0, moved);
            }
        }

        [Fact]
        public void A_movement_inside_the_drag_threshold_is_still_a_click ()
        {
            var grid = Grid (out var form);

            using (form) {
                var start = HeaderCentre (grid, 1);
                var nudged = new Point (start.X + SystemInformation.DragSize.Width - 1, start.Y);

                grid.Down (start);
                grid.MoveTo (nudged);
                grid.Up (nudged);

                Assert.Equal (new[] { 0, 1, 2, 3 }, Order (grid));
                Assert.Same (grid.Columns[1], grid.SortedColumn);
            }
        }

        [Fact]
        public void A_scrolling_column_cannot_be_dropped_among_the_frozen_ones ()
        {
            var grid = Grid (out var form);

            using (form) {
                grid.Columns[0].Frozen = true;
                PaintSurface.Render (grid).Dispose ();

                // Over the frozen column's header the pointer is held at the edge of the scrolling band,
                // so the furthest the column can go is the first scrolling position.
                Drag (grid, HeaderCentre (grid, 3), HeaderCentre (grid, 0));

                Assert.Equal (0, grid.Columns[0].DisplayIndex);
                Assert.Equal (new[] { 0, 3, 1, 2 }, Order (grid));
            }
        }

        [Fact]
        public void The_drag_draws_the_insertion_bar_where_the_column_would_land ()
        {
            var grid = Grid (out var form);

            using (form) {
                var hot = SystemColors.HotTrack.ToSKColor ();

                grid.Down (HeaderCentre (grid, 0));
                grid.MoveTo (HeaderRightPart (grid, 2));

                Assert.True (grid.GetColumnRelocationFeedback (out _, out var bar));
                Assert.False (bar.IsEmpty);

                // The bar straddles the boundary between columns 2 and 3 (device geometry).
                var boundary = grid.GetColumnDeviceLeft (3);
                Assert.InRange (boundary, bar.Left, bar.Right);

                using (var during = PaintSurface.Render (grid))
                    Assert.True (Count (during, bar, hot) > 0, "no insertion bar drawn");

                grid.Up (HeaderRightPart (grid, 2));

                Assert.False (grid.GetColumnRelocationFeedback (out _, out _));

                using var after = PaintSurface.Render (grid);
                Assert.Equal (0, Count (after, bar, hot));
            }
        }

        private static int Count (SKBitmap bitmap, Rectangle area, SKColor color)
        {
            var count = 0;

            for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
                for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                    if (bitmap.GetPixel (x, y) == color)
                        count++;

            return count;
        }

        // ---------------- DGV-34's remainder

        [Fact]
        public void VerticalScrollingOffset_is_the_height_of_the_rows_scrolled_off_not_a_row_index ()
        {
            var grid = Grid (out var form, rows: 60);

            using (form) {
                grid.Rows[0].Height = 50;
                grid.FirstDisplayedScrollingRowIndex = 3;

                var expected = grid.DeviceToLogicalUnits (grid.RowDeviceHeight (0) + grid.RowDeviceHeight (1) + grid.RowDeviceHeight (2));
                Assert.Equal (expected, grid.VerticalScrollingOffset);
                Assert.NotEqual (3, grid.VerticalScrollingOffset);
            }
        }

        [Fact]
        public void FirstDisplayedScrollingColumnHiddenWidth_is_the_part_of_the_first_column_scrolled_off ()
        {
            var grid = Grid (out var form, columns: 12, columnWidth: 90);

            using (form) {
                grid.HorizontalScrollingOffset = 90 + 25;

                Assert.Equal (1, grid.FirstDisplayedScrollingColumnIndex);
                Assert.Equal (25, grid.FirstDisplayedScrollingColumnHiddenWidth);
            }
        }

        // ---------------- RC-8: the offsets are logical

        private static (int Horizontal, int Hidden, int Vertical, int AfterColumnAssign, int EventNew) OffsetsAt (double scale)
        {
            Application.UiScale = scale;
            var grid = Grid (out var form, rows: 60, columns: 12, columnWidth: 90);

            using (form) {
                var event_new = -1;
                grid.Scroll += (_, e) => {
                    if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
                        event_new = e.NewValue;
                };

                grid.HorizontalScrollingOffset = 115;
                var horizontal = grid.HorizontalScrollingOffset;
                var hidden = grid.FirstDisplayedScrollingColumnHiddenWidth;
                var event_value = event_new;

                grid.FirstDisplayedScrollingRowIndex = 3;
                var vertical = grid.VerticalScrollingOffset;

                // A scroll the grid works out itself, in device pixels, read back through the property.
                grid.FirstDisplayedScrollingColumnIndex = 2;

                return (horizontal, hidden, vertical, grid.HorizontalScrollingOffset, event_value);
            }
        }

        [Fact]
        public void The_scrolling_offsets_are_logical_and_do_not_grow_with_the_display_scale ()
        {
            var single = OffsetsAt (original_ui_scale);
            var doubled = OffsetsAt (original_ui_scale * 2);

            // One pixel of rounding either way, from the device round trip at odd gate scales.
            Assert.InRange (doubled.Horizontal, single.Horizontal - 1, single.Horizontal + 1);
            Assert.InRange (doubled.Hidden, single.Hidden - 1, single.Hidden + 1);
            Assert.InRange (doubled.Vertical, single.Vertical - 1, single.Vertical + 1);
            Assert.InRange (doubled.AfterColumnAssign, single.AfterColumnAssign - 1, single.AfterColumnAssign + 1);
            Assert.InRange (doubled.EventNew, single.EventNew - 1, single.EventNew + 1);

            // And at either scale they are what was asked for.
            Assert.Equal (115, single.Horizontal);
            Assert.Equal (115, single.EventNew);
        }
    }
}