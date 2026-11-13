using System.Data;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // A RadGridView's rows are Telerik's: 24px and a 30px header row unless TableElement says otherwise, whatever the font,
    // laid out a pixel under each, because a Telerik row's bottom border is the next row's top border and the two share it
    // (measured at three heights in probes/wfrowheight -- a declared 24 advances 23, 40 advances 39, 33 advances 32). The
    // declared heights below are still Telerik's; the laid-out ones are a pixel less.
    // and with AutoSizeRows a line of the font plus 5 (the header plus 3), whatever TableElement says. Here the rows were
    // the WinForms grid's, sized to the app's font (28px in Microsoft Sans Serif 12pt) under a 23px header, and AutoSizeRows
    // was stored and ignored, so a search list asking for 34px rows "unless sized to content" got 34px rows.
    [Collection ("Headless")]
    public class RadGridViewRowSizingTests
    {
        private static (Form Form, RadGridView Grid) Grid (string family = "Segoe UI", float points = 12f)
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new System.Drawing.Size (500, 300) };
            var grid = new RadGridView { Dock = DockStyle.Fill, Font = new DFont (family, points), ShowGroupPanel = false, AllowAddNewRow = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Rows.Add ("A");
            table.Rows.Add ("B");
            grid.DataSource = table;
            form.Show ();
            return (form, grid);
        }

        private static int[] Heights (RadGridView grid) => ((DataGridView) grid).Rows.Cast<DataGridViewRow> ().Select (r => r.Height).ToArray ();

        [Fact]
        public void Rows_are_Teleriks_24px_under_a_30px_header ()
        {
            var (form, grid) = Grid ("Microsoft Sans Serif");

            using (form) {
                Assert.Equal (24, grid.TableElement.RowHeight);
                Assert.Equal (30, grid.TableElement.TableHeaderHeight);
                Assert.Equal (new[] { 23, 23 }, Heights (grid));
                Assert.Equal (29, grid.ColumnHeadersHeight);
            }
        }

        [Fact]
        public void AutoSizeRows_sizes_rows_and_header_to_the_font ()
        {
            var (form, grid) = Grid ();

            using (form) {
                // As a search list sets them: comfortable heights, sized to content.
                grid.TableElement.RowHeight = 34;
                grid.TableElement.TableHeaderHeight = 38;
                grid.AutoSizeRows = true;

                using var font = new DFont ("Segoe UI", 12f);
                Assert.Equal (new[] { font.Height + 4, font.Height + 4 }, Heights (grid));
                Assert.Equal (font.Height + 2, grid.ColumnHeadersHeight);

                // What TableElement was told is kept, and back in force once AutoSizeRows is off.
                Assert.Equal (34, grid.TableElement.RowHeight);
                Assert.Equal (38, grid.TableElement.TableHeaderHeight);

                grid.AutoSizeRows = false;
                Assert.Equal (new[] { 33, 33 }, Heights (grid));
                Assert.Equal (37, grid.ColumnHeadersHeight);
            }
        }

        [Fact]
        public void Auto_sized_rows_follow_the_grids_font ()
        {
            var (form, grid) = Grid ();

            using (form) {
                grid.AutoSizeRows = true;
                grid.Font = new DFont ("Segoe UI", 20f);

                using var font = new DFont ("Segoe UI", 20f);
                Assert.Equal (new[] { font.Height + 4, font.Height + 4 }, Heights (grid));
                Assert.Equal (font.Height + 2, grid.ColumnHeadersHeight);
            }
        }
    }
}
