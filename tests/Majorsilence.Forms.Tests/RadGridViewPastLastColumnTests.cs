using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik's grid is blank right of its last column. Measured on Telerik (UI for WinForms 2026.2.520) with a
    // 400px grid whose two columns came to 150px: the header cells, the selected row's highlight and the grid
    // lines all stopped at the last column, and the rest was the grid's own background. Here the header band
    // and the selected row ran on to the right edge, with a header cell's border after the last column.
    [Collection ("Headless")]
    public class RadGridViewPastLastColumnTests
    {
        [Fact]
        public void The_header_band_and_a_selected_row_stop_at_the_last_column ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (420, 200) };
            var grid = new RadGridView { Location = new Point (10, 10), Size = new Size (400, 150), ShowGroupPanel = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("A");
            table.Columns.Add ("B");
            table.Rows.Add ("a1", "b1");
            grid.DataSource = table;
            grid.Columns["A"]!.Width = 70;
            grid.Columns["B"]!.Width = 80;
            grid.AllowAddNewRow = false;
            form.Show ();
            grid.Rows[0].IsSelected = true;

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 420, 200));
            var scale = bitmap.Width / 420f;
            SKColor At (int x, int y) => bitmap.GetPixel ((int) (x * scale), (int) (y * scale));

            var below = At (360, 140);                       // below the last row: the grid's background
            var row = grid.GetRowDisplayRectangle (0, cutOverflow: false);
            var header = At (360, 10 + grid.ColumnHeadersHeight / 2 + 2);
            var selected = At (360, 10 + row.Top + row.Height / 2 + 2);

            Assert.Equal (below, header);
            Assert.Equal (below, selected);
        }
    }
}
