using System.IO.Compression;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Exercises the Telerik-compat grid export suite (RadGridExport.cs) added in Phase 7: the four
    // RunExport paths (spread/.xlsx, CSV, HTML, PDF) plus the shared HiddenColumnOption/SummariesExportOption
    // handling and the RadMessageBox facade. Runs on the headless backend — no rendering required for the
    // export paths (the PDF path draws through the same Skia pipeline PrintDocument already exercises).
    public class RadGridExportTests
    {
        private static RadGridView MakeGrid ()
        {
            var grid = new RadGridView ();
            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 150 });
            grid.Columns.Add (new GridViewDecimalColumn ("Salary") { HeaderText = "Salary", Width = 120, FormatString = "C0" });
            grid.Columns.Add (new GridViewTextBoxColumn ("Notes") { HeaderText = "Notes", Width = 100, Visible = false });
            return grid;
        }

        private static void AddRow (RadGridView grid, string name, decimal salary, string notes)
        {
            grid.Rows.Add ();
            var row = grid.Rows[grid.Rows.Count - 1];
            row.Cells["Name"].Value = name;
            row.Cells["Salary"].Value = salary;
            row.Cells["Notes"].Value = notes;
        }

        private static RadGridView Populated ()
        {
            var grid = MakeGrid ();
            AddRow (grid, "Alice", 85000m, "n/a");
            AddRow (grid, "Bob", 48000m, "n/a");
            return grid;
        }

        [Fact]
        public void ExportToCSV_writes_visible_columns_and_row_count ()
        {
            var grid = Populated ();
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.csv");
            try {
                new ExportToCSV (grid).RunExport (path);

                var lines = File.ReadAllLines (path);
                Assert.Equal (3, lines.Length); // header + 2 data rows
                Assert.Equal ("Name,Salary", lines[0]); // Notes hidden, HiddenColumnOption defaults to DoNotExport
                Assert.Contains ("Alice", lines[1]);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void ExportToCSV_includes_hidden_columns_when_requested ()
        {
            var grid = Populated ();
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.csv");
            try {
                new ExportToCSV (grid) { HiddenColumnOption = HiddenOption.Export }.RunExport (path);

                var lines = File.ReadAllLines (path);
                Assert.Equal ("Name,Salary,Notes", lines[0]);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void GridViewSpreadExport_produces_a_real_openable_xlsx ()
        {
            var grid = Populated ();
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.xlsx");
            try {
                var exporter = new GridViewSpreadExport (grid);
                exporter.RunExport (path, new SpreadExportRenderer ());

                Assert.True (new FileInfo (path).Length > 0);

                using var zip = ZipFile.OpenRead (path);
                Assert.NotNull (zip.GetEntry ("xl/worksheets/sheet1.xml"));
                using var reader = new StreamReader (zip.GetEntry ("xl/worksheets/sheet1.xml")!.Open ());
                var xml = reader.ReadToEnd ();
                Assert.Contains ("Alice", xml);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void ExportToExcelML_produces_valid_spreadsheet_xml ()
        {
            var grid = Populated ();
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.xml");
            try {
                new ExportToExcelML (grid).RunExport (path);

                var xml = File.ReadAllText (path);
                Assert.Contains ("<Workbook", xml);
                Assert.Contains ("Alice", xml);
                // Must parse as well-formed XML.
                _ = System.Xml.Linq.XDocument.Parse (xml);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void ExportToHTML_produces_a_table_containing_the_data ()
        {
            var grid = Populated ();
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.htm");
            try {
                new ExportToHTML (grid).RunExport (path);

                var html = File.ReadAllText (path);
                Assert.Contains ("<table", html);
                Assert.Contains ("Alice", html);
                Assert.Contains ("Bob", html);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void GridViewPdfExport_produces_a_valid_pdf_with_header ()
        {
            var grid = Populated ();
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.pdf");
            try {
                var exporter = new GridViewPdfExport (grid) {
                    ShowHeaderAndFooter = true,
                    HeaderHeight = 20,
                    HeaderFont = new Majorsilence.Forms.Drawing.Font ("Arial", 12),
                    MiddleHeader = "Payroll Report",
                    FitToPageWidth = true,
                    Scale = 1,
                };

                exporter.RunExport (path, new PdfExportRenderer ());

                var bytes = File.ReadAllBytes (path);
                Assert.True (bytes.Length > 0);
                Assert.Equal ("%PDF", System.Text.Encoding.ASCII.GetString (bytes, 0, 4));
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void SummariesExportOption_DoNotExport_excludes_summary_rows ()
        {
            var grid = Populated ();
            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum)));

            var withSummaries = new ExportToCSV (grid) { SummariesExportOption = SummariesOption.ExportAll }.RunExportText ();
            var withoutSummaries = new ExportToCSV (grid) { SummariesExportOption = SummariesOption.DoNotExport }.RunExportText ();

            Assert.True (CountLines (withSummaries) > CountLines (withoutSummaries));
        }

        [Fact]
        public void RadMessageBox_SetThemeName_is_a_stored_no_op ()
        {
            RadMessageBox.SetThemeName ("Office2019Light");
            Assert.Equal ("Office2019Light", RadMessageBox.ThemeName);
        }

        [Fact]
        public void RadMessageBox_Show_maps_RadMessageIcon_and_returns_a_result ()
        {
            // Headless backend: MessageBox.Show with no open owner form falls back to a non-modal Show()
            // and returns DialogResult.OK immediately (see Majorsilence.Forms.MessageBox.Show). That path
            // never closes the form it creates (there's no user to click a button), so it leaks into the
            // process-wide Application.OpenForms list unless the caller cleans up — do that here so this
            // test doesn't poison Application.OpenForms for whatever test class runs after it.
            var formsBefore = Application.OpenForms.Count;
            var result = RadMessageBox.Show ("The export completed.", "Export to CSV", MessageBoxButtons.YesNo, RadMessageIcon.Question);
            Assert.Equal (DialogResult.OK, result);

            Assert.Equal (formsBefore + 1, Application.OpenForms.Count);
            var leaked = Application.OpenForms[Application.OpenForms.Count - 1];
            Assert.NotNull (leaked);
            leaked.Close ();
            Assert.Equal (formsBefore, Application.OpenForms.Count);
        }

        private static int CountLines (string text) => text.Split ('\n').Length;
    }

    // Small extension so the CSV-only assertions above can compare text without touching disk.
    internal static class ExportToCsvTestExtensions
    {
        public static string RunExportText (this ExportToCSV exporter)
        {
            var path = Path.Combine (Path.GetTempPath (), $"{System.Guid.NewGuid ():N}.csv");
            try {
                exporter.RunExport (path);
                return File.ReadAllText (path);
            } finally {
                File.Delete (path);
            }
        }
    }
}
