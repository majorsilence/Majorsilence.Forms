using System;
using System.Data;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // BestFitColumns sizes a column so its caption and its cells show whole, in the font they are drawn in. Telerik's take
    // the grid's Font. Here every column was measured in the theme's small item font, so a 12pt grid's best-fitted columns
    // came out too narrow for their own text, ending it with "...": "CANADIAN TIRE ASSOCIATE STORE BRIDG...".
    [Collection ("Headless")]
    public class RadGridViewBestFitTests
    {
        private static (Form Form, RadGridView Grid) Grid (bool headerButtons)
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new System.Drawing.Size (900, 300) };
            var grid = new RadGridView { Dock = DockStyle.Fill, Font = new DFont ("Segoe UI", 12f), ShowGroupPanel = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("VendCode");
            table.Columns.Add ("VendName");
            table.Rows.Add ("1", "CANADIAN TIRE ASSOCIATE STORE BRIDGEWATER");
            grid.DataSource = table;

            grid.EnableFiltering = headerButtons;
            grid.MasterTemplate.ShowFilteringRow = false;
            grid.MasterTemplate.ShowHeaderCellButtons = headerButtons;
            form.Show ();
            grid.BestFitColumns ();
            return (form, grid);
        }

        private static int Measured (RadGridView grid, string text)
            => (int) Math.Ceiling (TextMeasurer.MeasureText (text, grid.GetEffectiveFont (), grid.GetEffectiveFontSize ()).Width);

        [Fact]
        public void A_column_fits_its_widest_cell_in_the_grids_font ()
        {
            var (form, grid) = Grid (headerButtons: false);

            using (form)
                Assert.Equal (Measured (grid, "CANADIAN TIRE ASSOCIATE STORE BRIDGEWATER") + 2 * 4, grid.Columns["VendName"]!.Width);
        }

        [Fact]
        public void A_column_fits_its_caption_in_the_grids_font ()
        {
            var (form, grid) = Grid (headerButtons: false);

            using (form)
                Assert.Equal (Measured (grid, "VendCode") + 2 * 6, grid.Columns["VendCode"]!.Width);
        }

        [Fact]
        public void A_caption_keeps_the_room_for_its_filter_button ()
        {
            var (form, grid) = Grid (headerButtons: true);

            using (form)
                Assert.Equal (Measured (grid, "VendCode") + 2 * 6 + 26, grid.Columns["VendCode"]!.Width);
        }
    }
}
