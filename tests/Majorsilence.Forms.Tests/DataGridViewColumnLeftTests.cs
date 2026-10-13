using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GetColumnDeviceLeft used to sort the columns into display order (a new list, by LINQ) for every cell painted, a
    // tenth of a scroll repaint; it now tests each column's place directly (performance plan F15). The oracle is the
    // old formula over DisplayOrder, for shuffled display orders, tied display indices, frozen, right-pinned and
    // hidden columns.
    [Collection ("Headless")]
    public class DataGridViewColumnLeftTests
    {
        [Fact]
        public void Every_column_starts_where_the_display_order_puts_it ()
        {
            HeadlessRenderer.Use ();
            var rnd = new Random (3);

            for (var round = 0; round < 60; round++) {
                using var grid = new DataGridView { Width = 900, Height = 300 };
                for (var c = 0; c < 8; c++)
                    grid.Columns.Add ($"c{c}", $"Column {c}");

                foreach (var column in grid.Columns.Cast<DataGridViewColumn> ()) {
                    column.Width = 20 + rnd.Next (90);
                    column.Visible = rnd.Next (5) != 0;
                    column.Frozen = rnd.Next (4) == 0;
                    column.PinnedRight = !column.Frozen && rnd.Next (5) == 0;
                    // Some ties, which display order breaks by index.
                    column.SetDisplayIndexInternal (rnd.Next (6));
                }

                for (var c = 0; c < grid.Columns.Count; c++)
                    Assert.Equal (OldLeft (grid, c), grid.GetColumnDeviceLeft (c));
            }
        }

        // GetColumnDeviceLeft as it was, over DisplayOrder.
        private static int OldLeft (DataGridView grid, int columnIndex)
        {
            var client = grid.GetContentArea ();
            var left0 = client.Left + (grid.RowHeadersVisible ? grid.ScaledRowHeadersWidth : 0);
            var column = grid.Columns[columnIndex];
            var before = grid.DisplayOrder.TakeWhile (i => i != columnIndex).Select (i => grid.Columns[i]).ToList ();

            if (column.Frozen)
                return left0 + before.Where (o => o.Visible && o.Frozen).Sum (o => grid.LogicalToDeviceUnits (o.Width));
            if (column.PinnedRight)
                return client.Right - grid.RightPinnedColumnsWidth + before.Where (o => o.Visible && o.PinnedRight).Sum (o => grid.LogicalToDeviceUnits (o.Width));
            return left0 + grid.FrozenColumnsWidth - grid.HorizontalScrollingOffset
                + before.Where (o => o.Visible && !o.Frozen && !o.PinnedRight).Sum (o => grid.LogicalToDeviceUnits (o.Width));
        }
    }
}
