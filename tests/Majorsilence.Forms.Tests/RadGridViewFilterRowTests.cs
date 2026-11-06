using System;
using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // A RadGridView's filtering row is Telerik's. Measured on Telerik (UI for WinForms 2026.2.520):
    // - it is TableElement.FilterRowHeight high, 26 unless set, whatever the font and RowHeight; with AutoSizeRows a line
    //   of the header font plus 3, as the header row, but 22 at least (25 in Segoe UI 12pt, 22 in Segoe UI 8.25pt);
    // - a cell leads with its operator's label, "Contains:" in a text column and "Equals:" in others, or its filter's
    //   operator ("GreaterThan:"), in the grid's font and colour a pixel in, then the filter's value; a 21px operator
    //   button ends on the cell's right line, a pixel in from its top and bottom, 2px right of the label.
    // Here the row was 26 high with AutoSizeRows too, and its cells said "Filter..." in the theme's small grey font.
    [Collection ("Headless")]
    public class RadGridViewFilterRowTests
    {
        private static (Form Form, RadGridView Grid) Grid (string family = "Segoe UI", float points = 12f)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 300) };
            var grid = new RadGridView { Location = new Point (0, 0), Size = new Size (600, 300), Font = new DFont (family, points), ShowGroupPanel = false, AllowAddNewRow = false, EnableFiltering = true };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Name");
            table.Columns.Add ("Amount", typeof (decimal));
            table.Rows.Add ("Alpha", 12.5m);
            table.Rows.Add ("Bravo", 7m);
            grid.DataSource = table;
            grid.Columns["Name"]!.Width = 160;
            grid.Columns["Amount"]!.Width = 160;
            form.Show ();
            return (form, grid);
        }

        [Fact]
        public void The_row_is_FilterRowHeight_high_whatever_the_rows ()
        {
            var (form, grid) = Grid ();

            using (form) {
                Assert.Equal (26, grid.TableElement.FilterRowHeight);
                grid.TableElement.RowHeight = 34;
                grid.TableElement.TableHeaderHeight = 38;
                Assert.Equal (grid.LogicalToDeviceUnits (26), grid.FilterRowBandHeight);

                grid.TableElement.FilterRowHeight = 30;
                Assert.Equal (grid.LogicalToDeviceUnits (30), grid.FilterRowBandHeight);
            }
        }

        [Fact]
        public void With_AutoSizeRows_the_row_is_a_line_of_the_font_plus_3_and_22_at_least ()
        {
            foreach (var points in new[] { 12f, 8.25f, 20f }) {
                var (form, grid) = Grid (points: points);

                using (form) {
                    grid.AutoSizeRows = true;

                    using var font = new DFont ("Segoe UI", points);
                    Assert.Equal (grid.LogicalToDeviceUnits (Math.Max (22, font.Height + 3)), grid.FilterRowBandHeight);
                    // What TableElement was told is kept for when AutoSizeRows is off.
                    Assert.Equal (26, grid.TableElement.FilterRowHeight);
                }
            }
        }

        [Fact]
        public void A_cell_leads_with_its_operators_label ()
        {
            var (form, grid) = Grid ();

            using (form) {
                DataGridViewColumn Name () => grid.Columns["Name"]!;
                DataGridViewColumn Amount () => grid.Columns["Amount"]!;

                Assert.Equal ("Contains:", grid.FilterRowOperatorLabel (Name ()));
                Assert.Equal ("Equals:", grid.FilterRowOperatorLabel (Amount ()));
                Assert.Equal (string.Empty, grid.FilterRowValueText (Name ()));

                grid.FilterDescriptors.Add (new FilterDescriptor ("Amount", FilterOperator.IsGreaterThan, 1m));
                Assert.Equal ("GreaterThan:", grid.FilterRowOperatorLabel (Amount ()));
                Assert.Equal (1m.ToString (System.Globalization.CultureInfo.CurrentCulture), grid.FilterRowValueText (Amount ()));

                grid.FilterDescriptors.Add (new FilterDescriptor ("Name", FilterOperator.IsLessThanOrEqualTo, "b"));
                Assert.Equal ("LessThanOrEquals:", grid.FilterRowOperatorLabel (Name ()));
            }
        }

        [Fact]
        public void The_label_value_and_button_are_laid_out_as_Teleriks ()
        {
            var (form, grid) = Grid ();

            using (form) {
                var u = (Func<int, int>) grid.LogicalToDeviceUnits;
                var cell = new Rectangle (u (20), u (30), u (160), u (26));
                var column = grid.Columns["Name"]!;

                var (label, value, button) = RadGridViewRenderer.LayoutFilterCell (grid, column, cell);
                Assert.Equal (new Rectangle (cell.Right - u (1) - u (21), cell.Top + u (1), u (21), u (24)), button);
                Assert.Equal (cell.Left + u (1), label.Left);
                Assert.Equal (button.Left - u (2), label.Right);
                Assert.True (value.IsEmpty);

                // With a value, the label keeps its text's width and the value takes the rest up to the button.
                grid.FilterDescriptors.Add (new FilterDescriptor ("Name", FilterOperator.Contains, "rav"));
                (label, value, button) = RadGridViewRenderer.LayoutFilterCell (grid, column, cell);
                Assert.True (label.Width < button.Left - u (2) - cell.Left - u (1));
                Assert.Equal (label.Right, value.Left);
                Assert.Equal (button.Left - u (2), value.Right);
            }
        }

        [Fact]
        public void The_label_is_in_the_grids_text_colour ()
        {
            var (form, grid) = Grid ();

            using (form) {
                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 600, 300));
                var scale = bitmap.Width / 600f;
                var content = grid.DeviceToLogicalUnits (grid.GetContentArea ());
                var top = content.Top + grid.DeviceToLogicalUnits (grid.ScaledHeaderHeight);
                var left = grid.DeviceToLogicalUnits (grid.GetColumnDeviceLeft (0));

                var darkest = int.MaxValue;
                for (var x = left + 1; x < left + 60; x++)
                    for (var y = top + 1; y < top + 25; y++) {
                        var c = bitmap.GetPixel ((int) (x * scale), (int) (y * scale));
                        darkest = Math.Min (darkest, c.Red + c.Green + c.Blue);
                    }

                var disabled = Theme.ForegroundDisabledColor;
                Assert.True (darkest < disabled.Red + disabled.Green + disabled.Blue, $"darkest {darkest}");
            }
        }
    }
}
