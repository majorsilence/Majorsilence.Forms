using System;
using System.Data;
using Majorsilence.Forms;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases the Telerik-compat grid export suite (Majorsilence.Forms.Telerik, RadGridExport.cs) —
    // GridViewSpreadExport (.xlsx), ExportToCSV, ExportToHTML, and GridViewPdfExport — run against a small
    // RadGridView (built the same way as TelerikGridViewPanel's employee grid). Each button writes to a
    // temp file and shows the resulting path in the status label so it can be opened and inspected.
    public class TelerikGridExportPanel : BasePanel
    {
        private readonly RadGridView grid;
        private readonly Label status;

        public TelerikGridExportPanel ()
        {
            Controls.Add (new Label {
                Text = "RadGridView export suite — GridViewSpreadExport (.xlsx), ExportToCSV, ExportToHTML, GridViewPdfExport. Each writes a real file to the temp folder.",
                Left = 10, Top = 10, Width = 780, Height = 34
            });

            grid = new RadGridView {
                Left = 10, Top = 48, Width = 780, Height = 300,
                EnableAlternatingRowColor = true,
            };

            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 180 });
            grid.Columns.Add (new GridViewTextBoxColumn ("Country") { HeaderText = "Country", Width = 150 });
            grid.Columns.Add (new GridViewDecimalColumn ("Salary") {
                HeaderText = "Salary", Width = 150, FormatString = "C0",
                TextAlignment = ContentAlignment.MiddleRight, HeaderTextAlignment = ContentAlignment.MiddleRight
            });
            grid.Columns.Add (new GridViewCheckBoxColumn ("Active") { HeaderText = "Active", Width = 80 });

            AddRow ("Alice Johnson", "USA", 85000m, true);
            AddRow ("Bob Smith", "Canada", 48000m, true);
            AddRow ("Carol Williams", "USA", 64000m, false);
            AddRow ("David Brown", "UK", 105000m, true);
            AddRow ("Eve Davis", "Canada", 42500m, true);

            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Name", GridAggregateFunction.Count, "Count: {0}"),
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum, "C0")));

            Controls.Add (grid);

            status = new Label { Text = "Last action: (none)", Left = 10, Top = 356, Width = 780 };
            Controls.Add (status);

            AddButton ("Export to .xlsx", 10, 384, () => {
                var path = TempPath ("xlsx");
                new GridViewSpreadExport (grid).RunExport (path);
                Report ($"GridViewSpreadExport wrote {path}");
            });

            AddButton ("Export to CSV", 190, 384, () => {
                var path = TempPath ("csv");
                new ExportToCSV (grid).RunExport (path);
                Report ($"ExportToCSV wrote {path}");
            });

            AddButton ("Export to HTML", 370, 384, () => {
                var path = TempPath ("htm");
                new ExportToHTML (grid).RunExport (path);
                Report ($"ExportToHTML wrote {path}");
            });

            AddButton ("Export to PDF", 550, 384, () => {
                var path = TempPath ("pdf");
                var exporter = new GridViewPdfExport (grid) {
                    ShowHeaderAndFooter = true,
                    MiddleHeader = "Employee Report",
                };
                exporter.RunExport (path);
                Report ($"GridViewPdfExport wrote {path}");
            });
        }

        private void AddRow (string name, string country, decimal salary, bool active)
        {
            grid.Rows.Add ();
            var row = grid.Rows[grid.RowCount - 1];
            row.Cells["Name"].Value = name;
            row.Cells["Country"].Value = country;
            row.Cells["Salary"].Value = salary;
            row.Cells["Active"].Value = active;
        }

        private static string TempPath (string extension) =>
            System.IO.Path.Combine (System.IO.Path.GetTempPath (), $"telerik-grid-export-{Guid.NewGuid ():N}.{extension}");

        private void AddButton (string text, int left, int top, Action onClick)
        {
            var button = new Button { Text = text, Left = left, Top = top, Width = 170, Height = 30 };
            button.Click += (_, _) => onClick ();
            Controls.Add (button);
        }

        private void Report (string action) => status.Text = $"Last action: {action}";
    }
}
