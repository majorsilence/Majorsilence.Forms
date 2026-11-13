using System;
using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // How far apart a RadGridView puts its rows.
    //
    // A Telerik row's bottom border is the next row's top border, so rows it reports as 24 tall advance
    // 23 a row, and the first one starts at TableHeaderHeight - 1. Measured on UI for WinForms 2026.2.520
    // (probes/wfrowheight) at three declared heights, so it is a rule and not one coincidence at 24:
    //
    //   declared 24 (header 30)  rows at y29, 52, 75, 98 ...   pitch 23
    //   declared 40 (header 46)  rows at y45, 84, 123 ...      pitch 39
    //   declared 33 (header 39)  rows at y38, 70, 102 ...      pitch 32
    //
    // The grid advanced by the full declared height instead, so it gained a pixel a row and showed fewer
    // rows than Telerik in the same space -- finding 9 of the TCA/Budgeting/Work Order comparison.
    //
    // The declared heights themselves are unchanged: TableElement.RowHeight still answers 24, as Telerik's
    // does. Only the laid-out pitch moves.
    [Collection ("Headless")]
    public class RadGridViewRowPitchTests
    {
        private static (Form Form, RadGridView Grid) Grid (int rows = 8)
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
            var grid = new RadGridView {
                Location = new Point (0, 0), Size = new Size (400, 300), AllowAddNewRow = false, ShowGroupPanel = false,
            };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("A");
            for (var i = 0; i < rows; i++)
                table.Rows.Add ($"row {i}");
            grid.DataSource = table;

            form.Show ();
            return (form, grid);
        }

        [Fact]
        public void Rows_advance_by_one_less_than_the_height_the_grid_reports ()
        {
            var (form, grid) = Grid ();
            using (form) {
                Assert.Equal (24, grid.TableElement.RowHeight);
                Assert.Equal (23, grid.RowTemplate.Height);
            }
        }

        [Fact]
        public void A_row_height_of_its_own_is_laid_out_one_shorter_too ()
        {
            var (form, grid) = Grid ();
            using (form) {
                grid.TableElement.RowHeight = 40;
                Assert.Equal (40, grid.TableElement.RowHeight);
                Assert.Equal (39, grid.RowTemplate.Height);

                grid.TableElement.RowHeight = 33;
                Assert.Equal (32, grid.RowTemplate.Height);
            }
        }

        [Fact]
        public void The_first_row_starts_one_above_the_header_height ()
        {
            var (form, grid) = Grid ();
            using (form) {
                Assert.Equal (30, grid.TableElement.TableHeaderHeight);
                Assert.Equal (29, grid.ColumnHeadersHeight);
            }
        }

        // Not a test of the fix -- it passes either way. It pins the public rectangle to the template, so
        // that a later change to one cannot quietly disagree with the other.
        [Fact]
        public void The_rectangles_agree_with_the_template ()
        {
            var (form, grid) = Grid ();
            using (form) {
                var first = grid.GetRowDisplayRectangle (0, false);
                var second = grid.GetRowDisplayRectangle (1, false);
                var last = grid.GetRowDisplayRectangle (7, false);

                Assert.Equal (grid.ColumnHeadersHeight, first.Top);
                Assert.Equal (first.Height, second.Top - first.Top);
                Assert.Equal (7 * first.Height, last.Top - first.Top);
            }
        }
    }
}
