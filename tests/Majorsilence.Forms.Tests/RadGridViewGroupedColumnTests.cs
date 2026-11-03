using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadGridView hides the column it is grouped by, as Telerik's does. Measured on Telerik (UI for WinForms
    // 2026.2.520): ShowGroupedColumns is false by default on the grid and its MasterTemplate; a grid of Source, Status,
    // Type and Name grouped by Type lays out the headers Source, Status, Name, with Type's IsVisible still true;
    // ShowGroupedColumns = true brings Type back between Status and Name, and so does clearing the grouping. Here the
    // grouped column stayed on screen, repeating on every row the value its group row already reads.
    [Collection ("Headless")]
    public class RadGridViewGroupedColumnTests
    {
        private static (Form form, RadGridView grid) Grid ()
        {
            HeadlessRenderer.Use ();

            var table = new DataTable ();
            foreach (var name in new[] { "Source", "Status", "Type", "Name" })
                table.Columns.Add (name);
            table.Rows.Add ("a", "o", "WO", "n1");
            table.Rows.Add ("b", "o", "WR", "n2");

            var form = new Form { ClientSize = new Size (800, 400) };
            var grid = new RadGridView { Dock = DockStyle.Fill };
            form.Controls.Add (grid);
            grid.DataSource = table;
            form.Show ();
            grid.PerformLayout ();

            return (form, grid);
        }

        // The columns the grid lays out, left to right.
        private static string[] Shown (RadGridView grid)
            => ((DataGridView) grid).Columns.Cast<DataGridViewColumn> ()
                .Where (c => c.Visible).OrderBy (c => c.DisplayIndex).Select (c => c.Name).ToArray ();

        [Fact]
        public void ShowGroupedColumns_is_off_by_default ()
        {
            using var grid = new RadGridView ();

            Assert.False (grid.ShowGroupedColumns);
            Assert.False (grid.MasterTemplate.ShowGroupedColumns);
        }

        [Fact]
        public void The_grouped_column_is_hidden_but_keeps_IsVisible ()
        {
            var (form, grid) = Grid ();
            using var _ = form;
            var type = ((DataGridView) grid).Columns["Type"]!;
            var name = ((DataGridView) grid).Columns["Name"]!;
            var left = type.HeaderBounds.Left;

            grid.GroupDescriptors.Add (new GroupDescriptor ("Type", ListSortDirection.Ascending));
            grid.PerformLayout ();

            Assert.Equal (new[] { "Source", "Status", "Name" }, Shown (grid));
            Assert.True (type.IsVisible);
            Assert.False (type.Visible);
            Assert.Equal (left, name.HeaderBounds.Left);
        }

        [Fact]
        public void ShowGroupedColumns_or_ungrouping_shows_it_again ()
        {
            var (form, grid) = Grid ();
            using var _ = form;
            var all = new[] { "Source", "Status", "Type", "Name" };

            grid.GroupDescriptors.Add (new GroupDescriptor ("Type", ListSortDirection.Ascending));
            grid.ShowGroupedColumns = true;
            Assert.Equal (all, Shown (grid));

            grid.ShowGroupedColumns = false;
            Assert.Equal (new[] { "Source", "Status", "Name" }, Shown (grid));

            grid.GroupDescriptors.Clear ();
            Assert.Equal (all, Shown (grid));
            Assert.True (((DataGridView) grid).Columns["Type"]!.IsVisible);
        }

        [Fact]
        public void A_column_the_app_hides_stays_hidden_after_ungrouping ()
        {
            var (form, grid) = Grid ();
            using var _ = form;
            var type = ((DataGridView) grid).Columns["Type"]!;

            grid.GroupDescriptors.Add (new GroupDescriptor ("Type", ListSortDirection.Ascending));
            type.IsVisible = false;
            grid.GroupDescriptors.Clear ();

            Assert.False (type.IsVisible);
            Assert.Equal (new[] { "Source", "Status", "Name" }, Shown (grid));
        }
    }
}
