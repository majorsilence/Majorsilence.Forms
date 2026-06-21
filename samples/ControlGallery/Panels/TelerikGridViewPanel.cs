using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using Continuum.Forms;
using Continuum.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadGridView (Continuum.Forms.Telerik) — backed by Continuum.Forms.DataGridView. In addition
    // to the data surface (GridView* column types, currency FormatString, FK combo column, check-box
    // column, Cell/RowFormatting), this panel demonstrates the *interactive* RadGridView features:
    //   • Filtering   — click a column's funnel glyph for a distinct-value checklist + condition.
    //   • Sorting     — click a header (or use the header right-click menu).
    //   • Grouping    — drag a header into the group panel, or right-click → "Group By This Column";
    //                   click a group header to expand/collapse; click a group pill to flip direction.
    //   • Reordering  — drag a column header onto another to move the column.
    //   • Layout XML  — Save/Load the column + sort + group + filter state as Telerik-shaped XML.
    public class TelerikGridViewPanel : BasePanel
    {
        private readonly RadGridView grid;
        private readonly TextBox xml_box;
        private string saved_layout = string.Empty;

        public TelerikGridViewPanel ()
        {
            Controls.Add (new Label {
                Text = "RadGridView: filter row + funnel filtering, multi-sort (Shift-click), grouping + footers, summary row, reorder, frozen column, master-detail, CSV/Excel export, layout XML.",
                Left = 10, Top = 10, Width = 760
            });
            Controls.Add (new Label {
                Text = "Type in the filter row to filter live. Click the [+] at a row's left for its detail table. Drag a header to the group panel; drag one header onto another to reorder.",
                Left = 10, Top = 30, Width = 760
            });

            // Department lookup table (the combo column stores DeptId but displays DeptName).
            var depts = new DataTable ();
            depts.Columns.Add ("DeptId", typeof (int));
            depts.Columns.Add ("DeptName", typeof (string));
            depts.Rows.Add (1, "Engineering");
            depts.Rows.Add (2, "Design");
            depts.Rows.Add (3, "Support");
            depts.Rows.Add (4, "Legal");

            grid = new RadGridView {
                Left = 10, Top = 56, Width = 760, Height = 340,
                ShowGroupPanel = true,
                ShowFilterRow = true,
                EnableGrouping = true,
                EnableFiltering = true,
                EnableSorting = true,
                AllowColumnReorder = true,
                AllowColumnChooser = true,
                EnableAlternatingRowColor = true
            };

            grid.MasterTemplate.ViewDefinition = new TableViewDefinition ();
            grid.MasterTemplate.AllowAddNewRow = false;

            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 150 });
            grid.Columns.Add (new GridViewComboBoxColumn ("Dept") {
                HeaderText = "Department", Width = 150,
                DataSource = depts, ValueMember = "DeptId", DisplayMember = "DeptName"
            });
            grid.Columns.Add (new GridViewTextBoxColumn ("Country") { HeaderText = "Country", Width = 120 });
            grid.Columns.Add (new GridViewDecimalColumn ("Salary") {
                HeaderText = "Salary", Width = 120, FormatString = "C0",
                TextAlignment = ContentAlignment.MiddleRight, HeaderTextAlignment = ContentAlignment.MiddleRight
            });
            grid.Columns.Add (new GridViewCheckBoxColumn ("Active") { HeaderText = "Active", Width = 70 });
            grid.Columns.Add (new GridViewDateTimeColumn ("HireDate") { HeaderText = "Hire Date", Width = 110, FormatString = "yyyy-MM-dd" });

            // Pin the Name column: it stays put when the grid is scrolled horizontally.
            grid.Columns["Name"]!.Frozen = true;

            AddRow ("Alice Johnson", 1, "USA", 85000m, true);
            AddRow ("Bob Smith", 2, "Canada", 48000m, true);
            AddRow ("Carol Williams", 3, "USA", 64000m, false);
            AddRow ("David Brown", 1, "UK", 105000m, true);
            AddRow ("Eve Davis", 4, "Canada", 42500m, true);
            AddRow ("Frank Miller", 1, "USA", 92000m, true);
            AddRow ("Grace Lee", 2, "UK", 57000m, false);
            AddRow ("Heidi Müller", 3, "Canada", 61000m, true);
            AddRow ("Ivan Petrov", 1, "UK", 78000m, true);
            AddRow ("Judy Garland", 4, "USA", 49000m, false);
            AddRow ("Mallory Quinn", 2, "Canada", 53000m, true);
            AddRow ("Niaj Khan", 3, "USA", 67000m, true);

            // Hire dates — double-click a Hire Date cell to pick from the calendar editor (Department uses the combo editor).
            var hireDates = new[] {
                new DateTime (2018, 3, 12), new DateTime (2020, 7, 1), new DateTime (2019, 11, 5), new DateTime (2016, 1, 20),
                new DateTime (2021, 5, 9), new DateTime (2017, 9, 30), new DateTime (2022, 2, 14), new DateTime (2015, 6, 18),
                new DateTime (2020, 12, 1), new DateTime (2019, 4, 22), new DateTime (2023, 1, 10), new DateTime (2018, 8, 8)
            };
            for (var i = 0; i < grid.RowCount; i++)
                grid.Rows[i].Cells["HireDate"].Value = hireDates[i];

            // RowFormatting — highlight rows whose salary is >= 100k.
            grid.RowFormatting += (o, e) => {
                if (e.Row?.Cells["Salary"].Value is decimal salary && salary >= 100000) {
                    e.RowElement.DrawFill = true;
                    e.RowElement.BackColor = Color.FromArgb (255, 226, 187);
                }
            };

            // CellFormatting — show salaries below 50k in red.
            grid.CellFormatting += (o, e) => {
                if (e.Column?.Name == "Salary" && e.CellElement.Value is decimal salary && salary < 50000)
                    e.CellElement.ForeColor = Color.FromArgb (200, 0, 0);
            };

            // Summary row — grand totals at the bottom (respects the active filter).
            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Name", GridAggregateFunction.Count, "Count: {0}"),
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum, "C0")));

            // Group footers — per-group salary total shown under each group when grouping is active.
            grid.GroupSummaryItems.Add (new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum, "C0"));

            // Master-detail — click the [+] at the left of a row to reveal a child (projects) table.
            grid.ChildViewProvider = row => {
                var name = row.Cells["Name"]?.Value?.ToString () ?? "Employee";
                var view = new GridChildView ("Project", "Role", "Hours");
                view.AddRow (name.Split (' ')[0] + " Onboarding", "Lead", "40");
                view.AddRow ("Platform Migration", "Member", "24");
                view.AddRow ("Quarterly Review", "Reviewer", "8");
                return view;
            };

            Controls.Add (grid);

            // ── Search box (quick-filter across all columns) ──
            var x = 780;
            Controls.Add (new Label { Text = "Search:", Left = x, Top = 14, Width = 55 });
            var search = new TextBox { Left = x + 58, Top = 10, Width = 172, Height = 26 };
            search.TextChanged += (_, _) => grid.SearchText = search.Text;
            Controls.Add (search);
            AddButton ("Group by Dept", x, 56, () => grid.GroupByColumn ("Dept"));
            AddButton ("Group by Country", x, 92, () => grid.GroupByColumn ("Country"));
            AddButton ("Clear Grouping", x, 128, () => grid.ClearGrouping ());
            AddButton ("Expand All", x, 172, () => grid.ExpandAllGroups ());
            AddButton ("Collapse All", x, 208, () => grid.CollapseAllGroups ());
            AddButton ("Filter < 60k", x, 252, () =>
                grid.FilterDescriptors.Add (new FilterDescriptor ("Salary", FilterOperator.IsLessThan, 60000)));
            AddButton ("Clear Filters", x, 288, () => grid.FilterDescriptors.Clear ());
            AddButton ("Column Chooser", x, 332, () => grid.ShowColumnChooser ());
            AddButton ("Save Layout", x, 368, SaveLayout);
            AddButton ("Load Layout", x, 404, LoadLayout);
            AddButton ("Export CSV", x, 440, () => xml_box.Text = grid.ExportToCsv ());
            AddButton ("Export Excel", x, 476, () => {
                var path = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "radgridview.xlsx");
                grid.ExportToXlsx (path, "Employees");
                xml_box.Text = "Exported to: " + path;
            });

            Controls.Add (new Label {
                Text = "Saved layout XML (click \"Save Layout\"):",
                Left = 10, Top = 404, Width = 400
            });

            xml_box = new TextBox {
                Left = 10, Top = 426, Width = 760, Height = 180,
                Multiline = true, ReadOnly = true
            };
            Controls.Add (xml_box);
        }

        private void AddRow (string name, int deptId, string country, decimal salary, bool active)
        {
            grid.Rows.Add ();
            var row = grid.Rows[grid.RowCount - 1];
            row.Cells["Name"].Value = name;
            row.Cells["Dept"].Value = deptId;
            row.Cells["Country"].Value = country;
            row.Cells["Salary"].Value = salary;
            row.Cells["Active"].Value = active;
        }

        private void AddButton (string text, int left, int top, Action onClick)
        {
            var button = new Button { Text = text, Left = left, Top = top, Width = 230, Height = 30 };
            button.Click += (_, _) => onClick ();
            Controls.Add (button);
        }

        private void SaveLayout ()
        {
            saved_layout = grid.SaveLayoutToString ();
            xml_box.Text = saved_layout;
        }

        private void LoadLayout ()
        {
            // Round-trip from the XML the user can see in the text box (or the last save).
            var xml = string.IsNullOrWhiteSpace (xml_box.Text) ? saved_layout : xml_box.Text;
            if (!string.IsNullOrWhiteSpace (xml))
                grid.LoadLayoutFromString (xml);
        }
    }
}
