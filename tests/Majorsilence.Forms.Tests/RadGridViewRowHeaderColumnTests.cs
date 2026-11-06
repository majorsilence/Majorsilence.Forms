using System.Data;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik's row header column (the current-row arrow's strip left of the cells) is 20px wide and is hidden with
    // ShowRowHeaderColumn. Measured on Telerik (UI for WinForms 2026.2.520): the row header cells are 20 wide, in
    // Microsoft Sans Serif 12pt and Segoe UI alike, and the first column starts at 20 in the table, at 1 with the
    // column hidden. Here the row headers were the WinForms grid's 41px, and ShowRowHeaderColumn did not exist.
    [Collection ("Headless")]
    public class RadGridViewRowHeaderColumnTests
    {
        private static (Form Form, RadGridView Grid) Grid ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new System.Drawing.Size (500, 300) };
            var grid = new RadGridView { Dock = DockStyle.Fill, ShowGroupPanel = false, AllowAddNewRow = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Rows.Add ("A");
            grid.DataSource = table;
            form.Show ();
            return (form, grid);
        }

        [Fact]
        public void The_row_header_column_is_Teleriks_20px ()
        {
            var (form, grid) = Grid ();

            using (form) {
                Assert.True (grid.ShowRowHeaderColumn);
                Assert.Equal (20, grid.TableElement.RowHeaderColumnWidth);
                Assert.Equal (20, ((DataGridView) grid).RowHeadersWidth);

                // The first column starts right of it, in device pixels at any scale.
                var table_left = grid.GetContentArea ().Left;
                Assert.Equal (table_left + grid.LogicalToDeviceUnits (20), grid.GetColumnDeviceLeft (0));
            }
        }

        [Fact]
        public void ShowRowHeaderColumn_hides_the_row_headers ()
        {
            var (form, grid) = Grid ();

            using (form) {
                grid.ShowRowHeaderColumn = false;

                Assert.False (((DataGridView) grid).RowHeadersVisible);
                Assert.False (grid.MasterTemplate.ShowRowHeaderColumn);
                Assert.Equal (grid.GetContentArea ().Left, grid.GetColumnDeviceLeft (0));

                // Either says the same: the grid's row headers are the column.
                ((DataGridView) grid).RowHeadersVisible = true;
                Assert.True (grid.ShowRowHeaderColumn);
            }
        }

        [Fact]
        public void RowHeaderColumnWidth_is_the_row_headers_width ()
        {
            var (form, grid) = Grid ();

            using (form) {
                grid.TableElement.RowHeaderColumnWidth = 32;
                Assert.Equal (32, ((DataGridView) grid).RowHeadersWidth);

                ((DataGridView) grid).RowHeadersWidth = 26;
                Assert.Equal (26, grid.TableElement.RowHeaderColumnWidth);
            }
        }
    }
}
