using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // What a ViewCellFormatting handler sets on a cell element beyond its colours: header cells, the element types, the
    // font, padding, fill and per-side border, and the row heights AutoSizeRows takes from them. Measured on Telerik (UI
    // for WinForms 2026.2.520) with a search list's handler: white bold headers padded (8,9,8,9) with a 1px bottom rule,
    // data cells padded (8,7,8,7) with a 1px bottom rule and the selected row filled. In Segoe UI 12pt the rows are 38px
    // and the header 42 (27 and 25 unformatted); no line runs between the columns; the selected row keeps its fill.
    [Collection ("Headless")]
    public class RadGridViewCellFormattingElementTests
    {
        private static readonly Color HeaderInk = Color.FromArgb (0x64, 0x72, 0x80);
        private static readonly Color HeaderRule = Color.FromArgb (0x6C, 0x95, 0xBE);
        private static readonly Color RowRule = Color.FromArgb (0xDC, 0xE4, 0xEC);
        private static readonly Color SelectedFill = Color.FromArgb (0xD9, 0xE0, 0xEC);
        private static readonly Color SelectedInk = Color.FromArgb (0x00, 0x57, 0x8E);

        private static void BottomRule (LightVisualElement cell, Color colour)
        {
            cell.DrawBorder = true;
            cell.BorderBoxStyle = BorderBoxStyle.FourBorders;
            cell.BorderTopWidth = 0;
            cell.BorderLeftWidth = 0;
            cell.BorderRightWidth = 0;
            cell.BorderBottomWidth = 1;
            cell.BorderBottomColor = colour;
        }

        // The search list's handler, as written against Telerik.
        private static void Format (object? sender, CellFormattingEventArgs e)
        {
            if (e.CellElement is GridHeaderCellElement) {
                e.CellElement.DrawFill = true;
                e.CellElement.NumberOfColors = 1;
                e.CellElement.BackColor = Color.White;
                e.CellElement.ForeColor = HeaderInk;
                e.CellElement.Font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", e.CellElement.Font!.Size, Majorsilence.Forms.Drawing.FontStyle.Bold);
                e.CellElement.Padding = new Padding (8, 9, 8, 9);
                BottomRule (e.CellElement, HeaderRule);
            } else if (e.CellElement is GridDataCellElement) {
                e.CellElement.DrawFill = true;
                e.CellElement.NumberOfColors = 1;
                var selected = e.CellElement.RowInfo is { IsSelected: true };
                e.CellElement.BackColor = selected ? SelectedFill : Color.White;
                e.CellElement.ForeColor = selected ? SelectedInk : Color.Black;
                e.CellElement.Padding = new Padding (8, 7, 8, 7);
                BottomRule (e.CellElement, RowRule);
            }
        }

        private static (Form Form, RadGridView Grid) Grid (bool formatted)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 220) };
            var grid = new RadGridView {
                Location = new Point (0, 0), Size = new Size (400, 220), ShowGroupPanel = false, AllowAddNewRow = false,
                Font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 12f), ReadOnly = true,
            };
            grid.AutoSizeRows = true;
            if (formatted)
                grid.ViewCellFormatting += Format;
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Columns.Add ("Name");
            table.Rows.Add ("A100", "Alpha");
            table.Rows.Add ("B200", "Bravo");
            table.Rows.Add ("C300", "Charlie");
            grid.DataSource = table;
            grid.Columns["Code"]!.Width = 120;
            grid.Columns["Name"]!.Width = 140;
            ((DataGridView) grid).RowHeadersVisible = false;
            form.Show ();
            grid.CurrentRow = grid.Rows[1];
            ((DataGridView) grid).ClearSelection ();
            grid.Rows[1].IsSelected = true;
            return (form, grid);
        }

        // Paints twice: the first frame tells AutoSizeRows what the handler wants, the second draws at it.
        private static SKBitmap Capture (Form form)
        {
            HeadlessRenderer.CapturePng (form, 400, 220);
            return SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 220));
        }

        private static SKColor SK (Color c) => new SKColor (c.R, c.G, c.B, c.A);

        [Fact]
        public void ViewCellFormatting_is_raised_for_header_cells_and_CellFormatting_is_not ()
        {
            var (form, grid) = Grid (formatted: false);

            using (form) {
                var view = new List<GridViewCellElement> ();
                var cell = new List<GridViewCellElement> ();
                grid.ViewCellFormatting += (s, e) => view.Add (e.CellElement);
                grid.CellFormatting += (s, e) => cell.Add (e.CellElement);
                HeadlessRenderer.CapturePng (form, 400, 220);

                var headers = view.OfType<GridHeaderCellElement> ().ToList ();
                Assert.Equal (new[] { 0, 1 }, headers.Select (h => h.ColumnIndex).Distinct ().OrderBy (i => i));
                Assert.All (headers, h => {
                    Assert.Equal (-1, h.RowIndex);
                    Assert.IsType<GridViewTableHeaderRowInfo> (h.RowInfo);
                    Assert.Equal (BorderBoxStyle.FourBorders, h.BorderBoxStyle);
                    Assert.Equal (1f, h.BorderBottomWidth);
                });
                Assert.Contains (headers, h => h.Text == "Code");

                // Every data cell's element is a GridDataCellElement -- and a LightVisualElement starting with the grid's font
                // and a four-sided 2px border, as Telerik's.
                var data = view.Where (e => e is not GridHeaderCellElement).ToList ();
                Assert.NotEmpty (data);
                Assert.All (data, d => {
                    Assert.IsAssignableFrom<GridDataCellElement> (d);
                    Assert.IsAssignableFrom<LightVisualElement> (d);
                    Assert.Same (grid.Font, d.Font);
                    Assert.Equal (2f, d.BorderLeftWidth);
                });
                Assert.DoesNotContain (cell, e => e is GridHeaderCellElement);
                Assert.NotEmpty (cell);
            }
        }

        [Fact]
        public void AutoSizeRows_takes_the_rows_and_header_heights_from_the_formatted_padding_and_border ()
        {
            var (plain_form, plain) = Grid (formatted: false);
            int plain_row, plain_header;
            using (plain_form) {
                using (Capture (plain_form)) { }
                plain_row = ((DataGridView) plain).Rows[0].Height;
                plain_header = ((DataGridView) plain).ColumnHeadersHeight;
            }

            var (form, grid) = Grid (formatted: true);
            using (form) {
                using (Capture (form)) { }

                // A line of the font, the padding (14), the bottom border (1) and 1: the unformatted row's line, 2px border a
                // side and 1, plus 11. Every row, painted or not, as they all want the same.
                Assert.All (Enumerable.Range (0, grid.Rows.Count), i => Assert.Equal (plain_row + 11, ((DataGridView) grid).Rows[i].Height));

                // The header: a line of the bold font, 18 and 1 and 1, against a line of the regular font, 1 and 1 and 1.
                var header = ((DataGridView) grid).ColumnHeadersHeight;
                Assert.True (header >= plain_header + 17, $"header {header}, unformatted {plain_header}");

                if (System.OperatingSystem.IsWindows ()) {
                    Assert.Equal (38, ((DataGridView) grid).Rows[0].Height);
                    Assert.Equal (42, header);
                }
            }
        }

        // A cell's device bounds as last painted.
        private static Rectangle CellBounds (RadGridView grid, int row, int column) => ((DataGridView) grid).Rows[row].Cells[column].Bounds;

        [Fact]
        public void The_formatted_fill_and_bottom_rules_are_drawn_and_no_line_runs_between_the_columns ()
        {
            var (form, grid) = Grid (formatted: true);

            using (form)
            using (var bitmap = Capture (form)) {
                var header = grid.Columns[0]!.HeaderBounds;
                var b = grid.Columns[1]!.HeaderBounds.Left;
                var x = header.Left + grid.LogicalToDeviceUnits (60);

                // The header: white, its bottom pixel row the header rule (not the band's own line), no line between columns.
                Assert.Equal (SKColors.White, bitmap.GetPixel (x, header.Top + 2));
                Assert.Equal (SK (HeaderRule), bitmap.GetPixel (x, header.Bottom - 1));
                Assert.Equal (SK (HeaderRule), bitmap.GetPixel (b - 1, header.Bottom - 1));
                Assert.Equal (SKColors.White, bitmap.GetPixel (b - 1, header.Top + 4));

                // The first row: white with its bottom rule, and nothing between its two cells.
                var first = CellBounds (grid, 0, 0);
                Assert.Equal (SK (RowRule), bitmap.GetPixel (x, first.Bottom - 1));
                Assert.Equal (SKColors.White, bitmap.GetPixel (b - 1, first.Top + 3));
                Assert.Equal (SKColors.White, bitmap.GetPixel (b, first.Top + 3));

                // The selected row keeps the handler's fill, not the selection's.
                var second = CellBounds (grid, 1, 0);
                Assert.Equal (SK (SelectedFill), bitmap.GetPixel (x, second.Top + 3));
                Assert.Equal (SK (SelectedFill), bitmap.GetPixel (b - 1, second.Top + 3));
            }
        }

        [Fact]
        public void A_handler_s_header_font_and_colour_reach_the_caption ()
        {
            var (form, grid) = Grid (formatted: true);

            using (form)
            using (var bitmap = Capture (form)) {
                var header = grid.LogicalToDeviceUnits (((DataGridView) grid).ColumnHeadersHeight);
                var right = grid.GetColumnDeviceLeft (1);

                // The caption's darkest pixel is the handler's ink (anti-aliased edges are paler, never another hue).
                var ink = Enumerable.Range (0, right).SelectMany (px => Enumerable.Range (1, header - 3).Select (py => bitmap.GetPixel (px, py)))
                    .Where (c => c != SKColors.White)
                    .OrderBy (c => c.Red + c.Green + c.Blue)
                    .First ();
                Assert.Equal (SK (HeaderInk), ink);
            }
        }

        [Fact]
        public void A_cell_s_padding_moves_its_text_in_from_the_unformatted_position ()
        {
            static int TextLeft (bool formatted)
            {
                var (form, grid) = Grid (formatted);
                using (form)
                using (var bitmap = Capture (form)) {
                    var cell = CellBounds (grid, 2, 0);
                    for (var px = cell.Left + 3; px < cell.Right - 3; px++)
                        for (var py = cell.Top + 3; py < cell.Bottom - 3; py++) {
                            var c = bitmap.GetPixel (px, py);
                            if (c.Red + c.Green + c.Blue < 300)
                                return (px - cell.Left) * 100 / grid.LogicalToDeviceUnits (100);
                        }
                    return -1;
                }
            }

            // Measured: 6 further in -- padding 8, less the 2 of border the unformatted cell's text is inside.
            Assert.Equal (TextLeft (formatted: false) + 6, TextLeft (formatted: true));
        }
    }
}
