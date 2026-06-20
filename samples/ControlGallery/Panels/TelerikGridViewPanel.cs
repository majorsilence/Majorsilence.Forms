using System;
using System.Data;
using System.Drawing;
using Continuum.Forms;
using Continuum.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadGridView (Continuum.Forms.Telerik) — backed by Continuum.Forms.DataGridView. Demonstrates
    // the GridView* column types, FormatString (currency), right-aligned numbers, a foreign-key combo
    // column (stores DeptId, shows the name), a toggleable check-box column, header-click sorting, and
    // the CellFormatting / RowFormatting data-driven coloring events.
    public class TelerikGridViewPanel : BasePanel
    {
        public TelerikGridViewPanel ()
        {
            Controls.Add (new Label {
                Text = "RadGridView — currency FormatString + right-aligned, FK combo column, toggleable check-box, header-click sorting, and Cell/RowFormatting (rows ≥100k highlighted, salaries <50k red).",
                Left = 10, Top = 10, Width = 760
            });

            // Department lookup table (the combo column stores DeptId but displays DeptName).
            var depts = new DataTable ();
            depts.Columns.Add ("DeptId", typeof (int));
            depts.Columns.Add ("DeptName", typeof (string));
            depts.Rows.Add (1, "Engineering");
            depts.Rows.Add (2, "Design");
            depts.Rows.Add (3, "Support");
            depts.Rows.Add (4, "Legal");

            var grid = new RadGridView { Left = 10, Top = 50, Width = 750, Height = 280 };

            grid.MasterTemplate.ViewDefinition = new TableViewDefinition ();
            grid.MasterTemplate.AllowAddNewRow = false;

            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 170 });
            grid.Columns.Add (new GridViewDecimalColumn ("Salary") {
                HeaderText = "Salary", Width = 130, FormatString = "C2",
                TextAlignment = ContentAlignment.MiddleRight, HeaderTextAlignment = ContentAlignment.MiddleRight
            });
            grid.Columns.Add (new GridViewComboBoxColumn ("Dept") {
                HeaderText = "Department", Width = 160,
                DataSource = depts, ValueMember = "DeptId", DisplayMember = "DeptName"
            });
            grid.Columns.Add (new GridViewCheckBoxColumn ("Active") { HeaderText = "Active", Width = 80 });

            // Five rows with strongly-typed cell values so FormatString / FK lookup / check-box all apply.
            for (var i = 0; i < 5; i++)
                grid.Rows.Add ();

            SetRow (grid, 0, "Alice Johnson", 85000m, 1, true);
            SetRow (grid, 1, "Bob Smith", 48000m, 2, true);
            SetRow (grid, 2, "Carol Williams", 64000m, 3, false);
            SetRow (grid, 3, "David Brown", 105000m, 1, true);
            SetRow (grid, 4, "Eve Davis", 42500m, 4, true);

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

            Controls.Add (grid);

            Controls.Add (new Label {
                Text = "Click a column header to sort. Click an Active check-box to toggle it.",
                Left = 10, Top = 338, Width = 760
            });
        }

        private static void SetRow (RadGridView grid, int index, string name, decimal salary, int deptId, bool active)
        {
            var row = grid.Rows[index];
            row.Cells["Name"].Value = name;
            row.Cells["Salary"].Value = salary;
            row.Cells["Dept"].Value = deptId;
            row.Cells["Active"].Value = active;
        }
    }
}
