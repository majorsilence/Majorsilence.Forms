using System;
using System.IO.Compression;
using System.Linq;
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
            // This test used to assert -- and document at length -- that MessageBox.Show with no open
            // owner "falls back to a non-modal Show() and returns DialogResult.OK immediately",
            // leaking the form into Application.OpenForms for the caller to clean up. That was the bug
            // (SVC-01/FRM-01): a message box shown before the first form was answered OK on the user's
            // behalf. It is modal now with or without an owner, so the dialog has to be dismissed the
            // way a user would.
            //
            // Posting the dismissal onto the UI queue is how the other modal tests drive one: the
            // callback runs when the nested loop drains the queue, i.e. while ShowDialog is blocked.
            var formsBefore = Application.OpenForms.Count;

            Majorsilence.Forms.Backends.Platform.Backend.Post (() => {
                var dialog = Application.OpenForms.Cast<Form> ().LastOrDefault (f => f is MessageBoxForm);
                dialog?.Close ();
            });

            var result = RunWithTimeout (() =>
                RadMessageBox.Show ("The export completed.", "Export to CSV", MessageBoxButtons.YesNo, RadMessageIcon.Question));

            // Dismissed without choosing a button, which is a cancel -- the point being that it waited
            // for an answer at all rather than inventing one.
            Assert.Equal (DialogResult.Cancel, result);
            Assert.Equal (formsBefore, Application.OpenForms.Count);
        }

        // Runs a modal show on a background thread with a join timeout, so a regression that stops the
        // dialog closing fails this test instead of hanging the whole suite. Same shape as
        // FileDialogModalPumpTests.RunOnPumpingThread.
        private static DialogResult RunWithTimeout (Func<DialogResult> show)
        {
            var result = DialogResult.None;
            System.Exception? failure = null;

            var thread = new System.Threading.Thread (() => {
                try { result = show (); } catch (System.Exception ex) { failure = ex; }
            }) { IsBackground = true };

            thread.Start ();

            Assert.True (thread.Join (System.TimeSpan.FromSeconds (10)),
                "the modal message box never returned: nothing dismissed it");

            if (failure is not null)
                throw failure;

            return result;
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
