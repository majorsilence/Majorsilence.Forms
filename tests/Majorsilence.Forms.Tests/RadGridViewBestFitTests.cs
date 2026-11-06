using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // BestFitColumns sizes a column as Telerik's does. Measured on Telerik (UI for WinForms 2026.2.520) in Microsoft Sans
    // Serif 12pt and Segoe UI 8.25 and 12pt: a column is the widest of its caption, its cells and its filtering-row cell, each
    // as wide as GDI+ measures its text in the grid's font, rounded up; a caption 4 more, 15 more in a check-box column, 13
    // more with its filter button or 18 with its sort glyph; a cell 4 more; a filtering-row cell its operator's label
    // ("Contains:") and 25 more. An approved-PO list best-fitted to "Acme Supplies Ltd" got 147 there; here every text was
    // measured unhinted and padded as the renderer insets it (a caption 6 a side, a cell 4), so it got 142, its check-box
    // column 81 for 95, a sorted column's glyph 16 for 18, and the filtering row nothing at all, cutting off its "Contains:".
    [Collection ("Headless")]
    public class RadGridViewBestFitTests
    {
        private static (Form Form, RadGridView Grid) Grid (bool headerButtons, string family = "Segoe UI", float points = 12f, bool filterRow = false)
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new System.Drawing.Size (900, 300) };
            var grid = new RadGridView { Dock = DockStyle.Fill, Font = new DFont (family, points), ShowGroupPanel = false, AllowAddNewRow = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("VendCode");
            table.Columns.Add ("VendName");
            table.Rows.Add ("1", "CANADIAN TIRE ASSOCIATE STORE BRIDGEWATER");
            grid.DataSource = table;

            grid.EnableFiltering = headerButtons || filterRow;
            grid.MasterTemplate.ShowFilteringRow = filterRow;
            grid.MasterTemplate.ShowHeaderCellButtons = headerButtons;
            form.Show ();
            grid.BestFitColumns ();
            return (form, grid);
        }

        private static int Measured (RadGridView grid, string text)
            => RadGridView.GdiPlusTextWidth (text, grid.GetEffectiveFont (), grid.GetEffectiveFontSize ());

        [Fact]
        public void A_column_fits_its_widest_cell_in_the_grids_font ()
        {
            var (form, grid) = Grid (headerButtons: false);

            using (form)
                Assert.Equal (Measured (grid, "CANADIAN TIRE ASSOCIATE STORE BRIDGEWATER") + 4, grid.Columns["VendName"]!.Width);
        }

        [Fact]
        public void A_column_fits_its_caption_in_the_grids_font ()
        {
            var (form, grid) = Grid (headerButtons: false);

            using (form)
                Assert.Equal (Measured (grid, "VendCode") + 4, grid.Columns["VendCode"]!.Width);
        }

        [Fact]
        public void A_caption_keeps_the_room_for_its_filter_button ()
        {
            var (form, grid) = Grid (headerButtons: true);

            using (form)
                Assert.Equal (Measured (grid, "VendCode") + 4 + 13, grid.Columns["VendCode"]!.Width);
        }

        [Fact]
        public void A_column_fits_its_filtering_row_cell ()
        {
            var (form, grid) = Grid (headerButtons: false, filterRow: true);

            using (form)
                Assert.Equal (Measured (grid, "Contains:") + 25, grid.Columns["VendCode"]!.Width);
        }

        [Fact]
        public void GDI_plus_widths_are_the_unkerned_advance_widths_three_percent_wider_and_a_third_of_the_em ()
        {
            HeadlessRenderer.Use ();
            var typeface = SkiaSharp.SKTypeface.FromFamilyName ("Segoe UI");
            using var font = new SkiaSharp.SKFont (typeface, 16);
            var advance = font.MeasureText ("Vendor Name");

            Assert.Equal ((int) Math.Ceiling (advance * 1.03f + 16 * 0.333f), RadGridView.GdiPlusTextWidth ("Vendor Name", typeface, 16));
            Assert.Equal (0, RadGridView.GdiPlusTextWidth (string.Empty, typeface, 16));
        }

        public class Po
        {
            public bool Approved { get; set; }
            public string PoNumber { get; set; } = "";
            public string VendorCode { get; set; } = "";
            public string VendorName { get; set; } = "";
            public DateTime Date { get; set; }
            public decimal Balance { get; set; }
            public decimal OverrideInvoiceAmount { get; set; }
            public string OverrideRequestUserName { get; set; } = "";
        }

        // Telerik's own widths, in the fonts measured: only where those fonts are installed.
        [Fact]
        public void An_approved_PO_list_best_fits_to_Teleriks_widths ()
        {
            if (!OperatingSystem.IsWindows ())
                return;

            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new System.Drawing.Size (900, 300) };
            var grid = new RadGridView { Dock = DockStyle.Fill, Font = new DFont ("Microsoft Sans Serif", 12f) };
            form.Controls.Add (grid);
            grid.DataSource = new List<Po> {
                new () { PoNumber = "PO-000123", VendorCode = "V001", VendorName = "Acme Supplies Ltd", Date = new DateTime (2026, 3, 4), Balance = 1520.75m, OverrideInvoiceAmount = 1600m, OverrideRequestUserName = "Jordan Smith" },
                new () { Approved = true, PoNumber = "PO-000124", VendorCode = "V002", VendorName = "Bay Hardware", Date = new DateTime (2026, 3, 5), Balance = 88m, OverrideInvoiceAmount = 90.5m, OverrideRequestUserName = "Sam Lee" },
            };
            grid.Columns["Date"]!.FormatString = "{0:MM-dd-yyyy}";
            grid.Columns["PoNumber"]!.HeaderText = "PO Number";
            grid.Columns["VendorCode"]!.HeaderText = "Vendor Code";
            grid.Columns["VendorName"]!.HeaderText = "Vendor Name";
            grid.Columns["Balance"]!.HeaderText = "PO Amount";
            grid.Columns["OverrideInvoiceAmount"]!.HeaderText = "Invoice Amount";
            grid.Columns["OverrideRequestUserName"]!.HeaderText = "Override Request User";
            form.Show ();

            grid.BestFitColumns ();

            Assert.Equal (new[] { 95, 97, 107, 147, 94, 95, 123, 178 }, grid.Columns.Cast<GridViewColumn> ().Select (c => c.Width).ToArray ());
        }

        [Fact]
        public void Captions_filter_cells_and_sort_glyphs_best_fit_to_Teleriks_widths ()
        {
            if (!OperatingSystem.IsWindows ())
                return;

            foreach (var (family, points, plain, buttons, filterRow, sorted) in new[] {
                ("Microsoft Sans Serif", 12f, 49, 62, 101, 67),
                ("Segoe UI", 8.25f, 34, 47, 75, 52),
                ("Segoe UI", 12f, 48, 61, 98, 66),
            }) {
                HeadlessRenderer.Use ();
                using var form = new Form { ClientSize = new System.Drawing.Size (900, 300) };
                var grid = new RadGridView { Dock = DockStyle.Fill, Font = new DFont (family, points), ShowGroupPanel = false, AllowAddNewRow = false };
                form.Controls.Add (grid);
                var table = new DataTable ();
                table.Columns.Add ("Code");
                table.Rows.Add ("A100");
                grid.DataSource = table;
                form.Show ();

                int Fit () { grid.BestFitColumns (); return grid.Columns["Code"]!.Width; }

                Assert.Equal (plain, Fit ());
                grid.EnableFiltering = true;
                grid.MasterTemplate.ShowFilteringRow = false;
                grid.MasterTemplate.ShowHeaderCellButtons = true;
                Assert.Equal (buttons, Fit ());
                grid.MasterTemplate.ShowFilteringRow = true;
                grid.MasterTemplate.ShowHeaderCellButtons = false;
                Assert.Equal (filterRow, Fit ());
                grid.EnableFiltering = false;
                grid.SortDescriptors.Add (new SortDescriptor ("Code", System.ComponentModel.ListSortDirection.Ascending));
                Assert.Equal (sorted, Fit ());
            }
        }

        [Fact]
        public void Check_box_columns_best_fit_to_Teleriks_widths ()
        {
            if (!OperatingSystem.IsWindows ())
                return;

            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new System.Drawing.Size (900, 300) };
            var grid = new RadGridView { Dock = DockStyle.Fill, Font = new DFont ("Microsoft Sans Serif", 12f), ShowGroupPanel = false, AllowAddNewRow = false };
            form.Controls.Add (grid);
            var table = new DataTable ();
            table.Columns.Add ("A", typeof (bool));
            table.Columns.Add ("Approved", typeof (bool));
            table.Columns.Add ("A much longer caption", typeof (bool));
            table.Rows.Add (true, false, true);
            grid.DataSource = table;
            form.Show ();

            grid.BestFitColumns ();

            Assert.Equal (new[] { 36, 95, 188 }, grid.Columns.Cast<GridViewColumn> ().Select (c => c.Width).ToArray ());
        }
    }
}
