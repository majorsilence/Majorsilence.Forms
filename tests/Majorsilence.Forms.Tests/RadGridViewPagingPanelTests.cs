using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // A RadGridView with EnablePaging has Telerik's paging panel along its bottom: first, previous and "..." buttons, a
    // button for each page of the current group of five, "...", next and last, then "Page [n] of N". Here paging cut the
    // rows down to the first page and showed no way to reach the rest, and MasterTemplate.EnablePaging -- the property a
    // designer sets -- did not page at all until something rebuilt the view.
    [Collection ("Headless")]
    public class RadGridViewPagingPanelTests
    {
        private static (Form Form, RadGridView Grid) Grid (int rows, int pageSize = 2, string family = "Segoe UI", float points = 12f)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (900, 400) };
            var grid = new RadGridView { Location = new Point (0, 0), Size = new Size (900, 400), Font = new DFont (family, points), ShowGroupPanel = false };
            form.Controls.Add (grid);

            // As a designer does: the template's properties, before the data arrives.
            grid.MasterTemplate.EnablePaging = true;
            grid.MasterTemplate.PageSize = pageSize;

            var table = new DataTable ();
            table.Columns.Add ("Code");
            for (var i = 0; i < rows; i++)
                table.Rows.Add ("R" + i);
            grid.DataSource = table;

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            return (form, grid);
        }

        private static void Press (Form form, RadGridView grid, Rectangle device)
        {
            var at = grid.DeviceToLogicalUnits (new Point (device.Left + device.Width / 2, device.Top + device.Height / 2));
            var loc = WindowPoint.In (grid, at.X, at.Y);
            HeadlessRenderer.MouseDown (form, loc.X, loc.Y);
            HeadlessRenderer.MouseUp (form, loc.X, loc.Y);
        }

        private static string FirstCode (RadGridView grid) => grid.Rows[0].Cells["Code"].Value?.ToString () ?? string.Empty;

        [Fact]
        public void The_panel_runs_along_the_bottom_below_the_rows ()
        {
            var (form, grid) = Grid (rows: 25);

            using (form) {
                Assert.True (grid.ShowsPagingPanel);

                var layout = grid.LayoutPagingPanel ();
                var client = grid.DeviceClientRectangle;
                Assert.Equal (client.Bottom, layout.Band.Bottom);
                Assert.Equal (client.Width, layout.Band.Width);
                Assert.Equal (layout.Band.Top, grid.GetContentArea ().Bottom);

                // Items the font's line height plus 5 tall (24 at least), 2px down a band 3px taller (29 at least).
                using var font = new DFont ("Segoe UI", 12f);
                var item = System.Math.Max (24, font.Height + 5);
                Assert.Equal (grid.LogicalToDeviceUnits (System.Math.Max (29, item + 3)), layout.Band.Height);
                Assert.Equal (grid.LogicalToDeviceUnits (item), layout.First.Height);
                Assert.Equal (layout.Band.Top + grid.LogicalToDeviceUnits (2), layout.First.Top);
            }
        }

        [Fact]
        public void The_buttons_are_laid_out_as_Teleriks ()
        {
            var (form, grid) = Grid (rows: 25);

            using (form) {
                var layout = grid.LayoutPagingPanel ();
                int X (Rectangle r) => grid.DeviceToLogicalUnits (r.Left - layout.Band.Left);
                int W (Rectangle r) => grid.DeviceToLogicalUnits (r.Width);

                // 24px image buttons, each pair 3px either side of a 2px separator.
                Assert.Equal (new[] { 1, 33 }, new[] { X (layout.First), X (layout.Previous) });
                Assert.Equal (new[] { 24, 24, 24, 24 }, new[] { W (layout.First), W (layout.Previous), W (layout.Next), W (layout.Last) });
                Assert.Equal (65, X (layout.FastBack));
                Assert.Equal (new[] { 28, 60, X (layout.FastBack) + W (layout.FastBack) + 3 }, layout.Separators.Take (3).Select (X));

                // The page buttons share the strip between the "..." buttons, five to a group.
                Assert.Equal (new[] { 0, 1, 2, 3, 4 }, layout.Pages.Select (p => p.Page));
                Assert.Equal (X (layout.FastBack) + W (layout.FastBack) + 8, X (layout.Pages[0].Bounds));
                Assert.Equal (X (layout.FastForward) - 8, X (layout.Pages[4].Bounds) + W (layout.Pages[4].Bounds));
                Assert.True (layout.Pages.Max (p => W (p.Bounds)) - layout.Pages.Min (p => W (p.Bounds)) <= 1);

                // The left strip ends where "Page [n] of N" begins, and that 4px in from the right, the box 30px wide.
                Assert.Equal (X (layout.PageLabel), X (layout.Last) + W (layout.Last));
                Assert.Equal (30, W (layout.PageBox));
                Assert.Equal (grid.DeviceToLogicalUnits (layout.Band.Width) - 1 - 4, X (layout.CountLabel) + W (layout.CountLabel));
            }
        }

        [Fact]
        public void The_buttons_move_between_pages ()
        {
            var (form, grid) = Grid (rows: 25);

            using (form) {
                Assert.Equal (13, grid.PageCount);
                Assert.Equal ("R0", FirstCode (grid));

                Press (form, grid, grid.LayoutPagingPanel ().Next);
                Assert.Equal (1, grid.CurrentPageIndex);
                Assert.Equal ("R2", FirstCode (grid));

                Press (form, grid, grid.LayoutPagingPanel ().Pages.Single (p => p.Page == 3).Bounds);
                Assert.Equal (3, grid.CurrentPageIndex);

                Press (form, grid, grid.LayoutPagingPanel ().Previous);
                Assert.Equal (2, grid.CurrentPageIndex);

                // "..." moves a group of five: pages 6 to 10 show.
                Press (form, grid, grid.LayoutPagingPanel ().FastForward);
                Assert.Equal (5, grid.CurrentPageIndex);
                Assert.Equal (new[] { 5, 6, 7, 8, 9 }, grid.LayoutPagingPanel ().Pages.Select (p => p.Page));

                Press (form, grid, grid.LayoutPagingPanel ().FastBack);
                Assert.Equal (0, grid.CurrentPageIndex);

                Press (form, grid, grid.LayoutPagingPanel ().Last);
                Assert.Equal (12, grid.CurrentPageIndex);
                Assert.Equal ("R24", FirstCode (grid));

                Press (form, grid, grid.LayoutPagingPanel ().First);
                Assert.Equal (0, grid.CurrentPageIndex);
            }
        }

        [Fact]
        public void The_page_box_takes_a_page_number ()
        {
            var (form, grid) = Grid (rows: 25);

            using (form) {
                Press (form, grid, grid.LayoutPagingPanel ().PageBox);
                Assert.True (grid.EditingPageBox);

                HeadlessRenderer.TextInput (form, "7");
                HeadlessRenderer.KeyDown (form, Keys.Enter);

                Assert.False (grid.EditingPageBox);
                Assert.Equal (6, grid.CurrentPageIndex);
                Assert.Equal ("R12", FirstCode (grid));
            }
        }

        [Fact]
        public void The_scrollbar_stops_above_the_panel ()
        {
            var (form, grid) = Grid (rows: 60, pageSize: 1000);

            using (form) {
                var bar = grid.Controls.OfType<VScrollBar> ().Single ();
                Assert.True (bar.Visible);
                // To the pixel, but for the grid's 1px border, a device pixel at every scale where the bar is placed in logical ones.
                Assert.InRange (grid.LayoutPagingPanel ().Band.Top - grid.LogicalToDeviceUnits (bar.Bottom), 0, grid.LogicalToDeviceUnits (1) - 1);
            }
        }

        [Fact]
        public void Without_paging_there_is_no_panel ()
        {
            var (form, grid) = Grid (rows: 5);

            using (form) {
                grid.MasterTemplate.EnablePaging = false;

                Assert.False (grid.ShowsPagingPanel);
                Assert.Equal (grid.DeviceClientRectangle.Bottom, grid.GetContentArea ().Bottom);
                Assert.Equal (5, grid.RowCount);
            }
        }

        [Fact]
        public void A_page_is_twenty_rows_unless_set ()
        {
            Assert.Equal (20, new RadGridView ().MasterTemplate.PageSize);

            HeadlessRenderer.Use ();
            using var grid = new RadGridView ();
            var table = new DataTable ();
            table.Columns.Add ("Code");
            for (var i = 0; i < 25; i++)
                table.Rows.Add ("R" + i);
            grid.DataSource = table;
            grid.MasterTemplate.EnablePaging = true;

            Assert.Equal (20, grid.RowCount);
            Assert.Equal (2, grid.PageCount);
        }
    }
}
