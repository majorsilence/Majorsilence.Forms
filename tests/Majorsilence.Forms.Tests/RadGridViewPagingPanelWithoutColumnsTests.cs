using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A paged RadGridView shows its paging panel whether or not it has columns, as Telerik's. Measured (UI for WinForms
    // 2026.2.520): the panel was visible, 29px along the bottom, with no columns, with columns and no rows, with rows, and
    // after the source and columns were cleared. Here it waited for columns, so a paged list with nothing loaded yet
    // showed "No data to display" with no pager under it.
    [Collection ("Headless")]
    public class RadGridViewPagingPanelWithoutColumnsTests
    {
        [Fact]
        public void A_paged_grid_with_no_columns_shows_its_paging_panel ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (700, 400) };
            var grid = new RadGridView { Dock = DockStyle.Fill, ShowGroupPanel = false };
            grid.MasterTemplate.EnablePaging = true;
            form.Controls.Add (grid);
            form.Show ();
            HeadlessRenderer.CapturePng (form);

            Assert.Empty (((DataGridView) grid).Columns);
            Assert.True (grid.ShowsPagingPanel);

            var band = grid.LayoutPagingPanel ().Band;
            Assert.True (band.Height > 0);
            Assert.Equal (grid.DeviceClientRectangle.Bottom, band.Bottom);
        }

        [Fact]
        public void Without_paging_a_grid_with_no_columns_has_no_panel ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (700, 400) };
            var grid = new RadGridView { Dock = DockStyle.Fill };
            form.Controls.Add (grid);
            form.Show ();

            Assert.False (grid.ShowsPagingPanel);
        }
    }
}
