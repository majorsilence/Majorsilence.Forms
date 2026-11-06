using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik's grid cells share their lines. Measured on Telerik (UI for WinForms 2026.2.520): one pixel of line between
    // two cells and between two rows (each cell's edges fall on its neighbours'), and, with the row header column hidden,
    // the first column's own line on its left, a pixel inside the grid's edge. Here every cell drew its left and top edges
    // beside its neighbours' right and bottom ones: two pixels of line between every pair of cells and rows.
    [Collection ("Headless")]
    public class RadGridViewCellLinesTests
    {
        private static (Form Form, RadGridView Grid) Grid (bool rowHeaders)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 200) };
            var grid = new RadGridView { Location = new Point (0, 0), Size = new Size (400, 200), ShowGroupPanel = false, AllowAddNewRow = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("A");
            table.Columns.Add ("B");
            table.Rows.Add ("", "");
            table.Rows.Add ("", "");
            table.Rows.Add ("", "");
            grid.DataSource = table;
            grid.Columns["A"]!.Width = 100;
            grid.Columns["B"]!.Width = 100;
            ((DataGridView) grid).RowHeadersVisible = rowHeaders;
            form.Show ();
            return (form, grid);
        }

        // The grid's line colour, and what is at a device pixel of the captured form.
        private static (SKColor Line, System.Func<int, int, SKColor> At, SKBitmap Bitmap) Capture (Form form)
        {
            var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 200));
            return (Theme.BorderLowColor, (x, y) => bitmap.GetPixel (x, y), bitmap);
        }

        [Fact]
        public void One_pixel_of_line_runs_between_two_cells_and_two_rows ()
        {
            var (form, grid) = Grid (rowHeaders: false);

            using (form) {
                var (line, at, bitmap) = Capture (form);

                using (bitmap) {
                    // The second and third rows: neither is the current, selected one.
                    var second = grid.GetRowDisplayRectangle (1, cutOverflow: false);
                    var third = grid.GetRowDisplayRectangle (2, cutOverflow: false);
                    var middle = grid.LogicalToDeviceUnits (third.Top + third.Height / 2);
                    var a = grid.GetColumnDeviceLeft (0);
                    var b = grid.GetColumnDeviceLeft (1);

                    // Across from the middle of A's cell to the middle of B's: one line pixel, A's right edge.
                    var across = Enumerable.Range (a + (b - a) / 2, b - a).Count (x => at (x, middle) == line);
                    Assert.Equal (1, across);
                    Assert.Equal (line, at (b - 1, middle));

                    // Down from the middle of the second row to the middle of the third: one, the second's bottom edge.
                    var x0 = b + grid.LogicalToDeviceUnits (50);
                    var from = grid.LogicalToDeviceUnits (second.Top + second.Height / 2);
                    var down = Enumerable.Range (from, middle - from).Count (y => at (x0, y) == line);
                    Assert.Equal (1, down);
                }
            }
        }

        [Fact]
        public void With_no_row_headers_the_first_column_has_its_own_left_line ()
        {
            var (form, grid) = Grid (rowHeaders: false);

            using (form) {
                var (line, at, bitmap) = Capture (form);

                using (bitmap) {
                    var row = grid.GetRowDisplayRectangle (1, cutOverflow: false);
                    var middle = grid.LogicalToDeviceUnits (row.Top + row.Height / 2);
                    var a = grid.GetColumnDeviceLeft (0);

                    Assert.Equal (line, at (a, middle));
                    Assert.NotEqual (line, at (a + 1, middle));
                }
            }
        }

        [Fact]
        public void Beside_the_row_headers_the_first_column_has_no_left_line_of_its_own ()
        {
            var (form, grid) = Grid (rowHeaders: true);

            using (form) {
                var (line, at, bitmap) = Capture (form);

                using (bitmap) {
                    var row = grid.GetRowDisplayRectangle (1, cutOverflow: false);
                    var middle = grid.LogicalToDeviceUnits (row.Top + row.Height / 2);
                    var a = grid.GetColumnDeviceLeft (0);

                    Assert.NotEqual (line, at (a, middle));
                    Assert.NotEqual (line, at (a + 1, middle));
                }
            }
        }
    }
}
