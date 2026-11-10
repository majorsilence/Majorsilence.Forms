using System.Data;
using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A grid paints its rows and cells rather than hosting them as controls, so the automation walk used to
    // stop at the grid: a test could find a DataGridView or RadGridView and read nothing inside it. These
    // check that the displayed rows and cells are in the tree, readable, and clickable at the bounds given.
    [Collection ("Headless")]
    public class GridAutomationTests
    {
        private static DataTable People (int count)
        {
            var table = new DataTable ();
            table.Columns.Add ("Id");
            table.Columns.Add ("Surname");
            for (var i = 1; i <= count; i++)
                table.Rows.Add ("P" + i, i == 7 ? "Smith" : "Jones" + i);
            return table;
        }

        private static Form Host (DataGridView grid)
        {
            var form = new Form { UseSystemDecorations = true };
            grid.Name = "results";
            grid.Dock = DockStyle.Fill;
            form.Controls.Add (grid);
            HeadlessRenderer.CapturePng (form, 400, 300);
            return form;
        }

        private static AutomationElement GridNode (Form form)
            => Assert.Single (AutomationProvider.BuildTree (form).Self (), e => e.AutomationId == "results");

        public static TheoryData<string> Grids => new () { "DataGridView", "RadGridView" };

        private static DataGridView Make (string kind, int rows)
        {
            DataGridView grid = kind == "RadGridView" ? new RadGridView () : new DataGridView ();
            grid.DataSource = People (rows);
            return grid;
        }

        [Theory, MemberData (nameof (Grids))]
        public void Displayed_rows_and_cells_are_in_the_tree (string kind)
        {
            using var form = Host (Make (kind, 3));

            var rows = GridNode (form).Children.Where (e => e.Role == "row").ToList ();

            Assert.Equal (new [] { "P1;Jones1", "P2;Jones2", "P3;Jones3" }, rows.Select (r => r.Value));
            Assert.Equal (new [] { "P2", "Jones2" }, rows [1].Children.Select (c => c.Name));
            Assert.All (rows [1].Children, c => Assert.Equal ("cell", c.Role));
            Assert.Equal ("Surname", rows [1].Children [1].State ["column"]);
            Assert.Equal ("1", rows [1].State ["row"]);

            var headers = GridNode (form).Children.Where (e => e.Role == "columnheader").Select (e => e.Name);
            Assert.Equal (new [] { "Id", "Surname" }, headers);
        }

        [Theory, MemberData (nameof (Grids))]
        public void Clicking_a_cell_by_name_makes_it_current (string kind)
        {
            var grid = Make (kind, 10);
            using var form = Host (grid);
            var session = new AutomationSession (form);

            // Ordered after the grid's own selection of the first row, so a click landing anywhere but on
            // the "Smith" cell leaves a different current cell behind.
            session.Click (session.FindOrThrow (By.Name ("Smith")));

            Assert.Equal (6, grid.CurrentCell!.RowIndex);
            Assert.Equal (1, grid.CurrentCell.ColumnIndex);

            // A DataGridView's default RowHeaderSelect selects the clicked cell, RadGridView's FullRowSelect the
            // row; the cell reports its own selection either way.
            var row = Assert.Single (GridNode (form).Children, e => e.Role == "row" && e.State ["row"] == "6");
            Assert.Equal ("true", row.Children [1].State ["selected"]);
        }

        [Theory, MemberData (nameof (Grids))]
        public void Only_rows_scrolled_into_view_are_in_the_tree (string kind)
        {
            var grid = Make (kind, 200);
            using var form = Host (grid);

            var rows = GridNode (form).Children.Where (e => e.Role == "row").ToList ();

            Assert.InRange (rows.Count, 2, 199);
            Assert.Equal ("0", rows [0].State ["row"]);

            grid.FirstDisplayedScrollingRowIndex = 50;
            HeadlessRenderer.CapturePng (form, 400, 300);

            rows = GridNode (form).Children.Where (e => e.Role == "row").ToList ();
            Assert.Equal ("50", rows [0].State ["row"]);
            Assert.All (rows, r => Assert.InRange (r.Bounds.Bottom, 0, form.ClientSize.Height + r.Bounds.Height));
        }

        [Fact]
        public void A_filtered_RadGridView_shows_only_the_matching_rows ()
        {
            var grid = new RadGridView { DataSource = People (20) };
            grid.FilterDescriptors.Add (new FilterDescriptor ("Surname", FilterOperator.IsEqualTo, "Smith"));
            using var form = Host (grid);

            var row = Assert.Single (GridNode (form).Children, e => e.Role == "row");

            Assert.Equal ("P7;Smith", row.Value);
        }

        [Fact]
        public void Hidden_rows_and_columns_are_left_out ()
        {
            var grid = Make ("DataGridView", 5);
            using var form = Host (grid);
            grid.Rows [1].Visible = false;
            grid.Columns [0].Visible = false;
            HeadlessRenderer.CapturePng (form, 400, 300);

            var rows = GridNode (form).Children.Where (e => e.Role == "row").ToList ();

            Assert.Equal (new [] { "Jones1", "Jones3", "Jones4", "Jones5" }, rows.Select (r => r.Value));
        }
    }
}
