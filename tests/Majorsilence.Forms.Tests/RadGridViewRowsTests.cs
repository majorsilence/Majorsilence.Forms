using System.ComponentModel;
using System.Data;
using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadGridView's Rows and RowCount are every data row it holds, in the order it holds them, whatever page is
    // shown, whatever is filtered out and however it is sorted or grouped; ChildRows are the rows on display. As
    // Telerik's, measured on UI for WinForms 2026.2.520 with 23 rows, R1..R23, every third of Kind "A":
    //   - Rows and RowCount 23 unpaged, paged ten to a page on the first page and the last, filtered to Kind "A",
    //     and grouped; ChildRows 23, 10, 3 and 7;
    //   - sorted by Id descending, Rows[0] is still R1 and ChildRows[0] is R9;
    //   - filtered, Rows[0] (R1, filtered out) can be removed through Rows, and Rows.RemoveAt (0) removes the
    //     next row held, R2, leaving R3 first; a row added that the filter hides is counted but not shown.
    // Here Rows and RowCount held only the rows on display, 10 on the first page -- so a paged search reported
    // "10 Record(s) matching" for 20 -- and a row added to a paged or filtered grid dropped every hidden row.
    public class RadGridViewRowsTests
    {
        private static DataTable Table ()
        {
            var table = new DataTable ();
            table.Columns.Add ("Id");
            table.Columns.Add ("Kind");
            for (var i = 1; i <= 23; i++)
                table.Rows.Add ("R" + i, i % 3 == 0 ? "A" : "B");
            return table;
        }

        private static RadGridView Grid (bool bound)
        {
            if (bound)
                return new RadGridView { DataSource = Table () };

            var grid = new RadGridView ();
            grid.Columns.Add ("Id", "Id");
            grid.Columns.Add ("Kind", "Kind");
            for (var i = 1; i <= 23; i++)
                grid.Rows.Add ("R" + i, i % 3 == 0 ? "A" : "B");
            return grid;
        }

        private static string Id (GridViewRowInfo row) => (string) row.Cells[0].Value!;

        private static void FilterToA (RadGridView grid)
            => grid.FilterDescriptors.Add (new FilterDescriptor ("Kind", FilterOperator.IsEqualTo, "A"));

        [Theory, InlineData (true), InlineData (false)]
        public void Paged_Rows_hold_the_rows_of_every_page (bool bound)
        {
            using var grid = Grid (bound);
            grid.EnablePaging = true;
            grid.PageSize = 10;

            Assert.Equal (23, grid.RowCount);
            Assert.Equal (23, grid.Rows.Count);
            Assert.Equal (10, grid.ChildRows.Count);

            grid.CurrentPageIndex = 2;

            Assert.Equal (23, grid.RowCount);
            Assert.Equal (23, grid.Rows.Count);
            Assert.Equal (3, grid.ChildRows.Count);
            Assert.Equal ("R21", Id (grid.ChildRows[0]));
            Assert.Equal ("R1", Id (grid.Rows[0]));
        }

        [Theory, InlineData (true), InlineData (false)]
        public void Filtered_Rows_hold_the_rows_filtered_out (bool bound)
        {
            using var grid = Grid (bound);
            FilterToA (grid);

            Assert.Equal (23, grid.RowCount);
            Assert.Equal (23, grid.Rows.Count);
            Assert.Equal (7, grid.ChildRows.Count);
            Assert.Equal ("R3", Id (grid.ChildRows[0]));
        }

        [Theory, InlineData (true), InlineData (false)]
        public void Sorted_Rows_keep_their_order (bool bound)
        {
            using var grid = Grid (bound);
            grid.SortDescriptors.Add (new SortDescriptor ("Id", ListSortDirection.Descending));

            Assert.Equal ("R1", Id (grid.Rows[0]));
            Assert.Equal ("R9", Id (grid.ChildRows[0]));
            Assert.Equal (8, grid.Rows.IndexOf (grid.ChildRows[0]));
        }

        [Theory, InlineData (true), InlineData (false)]
        public void Grouped_Rows_hold_the_data_rows_not_the_group_rows (bool bound)
        {
            using var grid = Grid (bound);
            var descriptor = new GroupDescriptor ();
            descriptor.GroupNames.Add ("Kind", ListSortDirection.Ascending);
            grid.GroupDescriptors.Add (descriptor);

            Assert.Equal (23, grid.RowCount);
            Assert.Equal (23, grid.Rows.Count);
            Assert.Equal ("R1", Id (grid.Rows[0]));
        }

        [Theory, InlineData (true), InlineData (false)]
        public void A_row_filtered_out_can_be_removed_through_Rows (bool bound)
        {
            using var grid = Grid (bound);
            FilterToA (grid);

            grid.Rows.Remove (grid.Rows[0]);

            Assert.Equal (22, grid.RowCount);
            Assert.Equal ("R2", Id (grid.Rows[0]));
            Assert.Equal (7, grid.ChildRows.Count);

            grid.Rows.RemoveAt (0);

            Assert.Equal (21, grid.RowCount);
            Assert.Equal ("R3", Id (grid.Rows[0]));
            Assert.Equal (7, grid.ChildRows.Count);

            if (bound)
                Assert.Equal (21, ((DataTable) grid.DataSource!).Rows.Count);
        }

        // Measured with the same grid filtered: an item changed in the list to pass the filter joins the rows on
        // display, one changed while filtered out changes its own row, and items added to the list join Rows and,
        // when they pass, ChildRows. Here each change landed on the row at the item's index among those on display.
        [Fact]
        public void A_change_to_the_bound_list_reaches_its_own_row_when_the_view_is_filtered ()
        {
            using var grid = Grid (bound: true);
            FilterToA (grid);
            var table = (DataTable) grid.DataSource!;

            table.Rows[0]["Kind"] = "A";

            Assert.Equal (8, grid.ChildRows.Count);
            Assert.Equal ("R1", Id (grid.ChildRows[0]));

            table.Rows[1]["Id"] = "R2x";

            Assert.Equal ("R2x", Id (grid.Rows[1]));
            Assert.Equal ("R1", Id (grid.ChildRows[0]));
            Assert.Equal ("R3", Id (grid.ChildRows[1]));

            table.Rows.Add ("T1", "A");
            table.Rows.Add ("T2", "B");

            Assert.Equal (25, grid.RowCount);
            Assert.Equal (9, grid.ChildRows.Count);
            Assert.Equal ("T1", Id (grid.ChildRows[8]));
        }

        [Fact]
        public void A_row_added_to_a_paged_grid_keeps_the_other_pages ()
        {
            using var grid = Grid (bound: false);
            grid.EnablePaging = true;
            grid.PageSize = 10;

            grid.Rows.Add ("NEW", "B");

            Assert.Equal (24, grid.RowCount);
            Assert.Equal ("NEW", Id (grid.Rows[23]));
            Assert.Equal (10, grid.ChildRows.Count);
        }

        [Fact]
        public void A_row_added_that_the_filter_hides_is_counted_but_not_shown ()
        {
            using var grid = Grid (bound: false);
            FilterToA (grid);

            grid.Rows.Add ("NEW", "B");

            Assert.Equal (24, grid.RowCount);
            Assert.Equal ("NEW", Id (grid.Rows.Last ()));
            Assert.Equal (7, grid.ChildRows.Count);
        }
    }
}
