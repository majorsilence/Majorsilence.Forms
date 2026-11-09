using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik's ThemeResolutionService.ApplicationThemeName and RadGridView.ThemeName pick the theme a grid paints with,
    // and "Fluent" / "FluentDark" paint as measured on Telerik (UI for WinForms 2026.2.520): white (black) rows and
    // header band, #CCCCCC (#4C4C4C) lines under the rows and none between the cells, #F2F2F2 (#2B2B2B) alternating
    // rows and filtering row, an #E6E6E6 (#333333) group panel, the selected row in the accent #0099BC and the current
    // cell outlined 2px in #333333 (white). An app that recolours Fluent's accent to #00578E moves its hue only: #0073BC.
    // Before, ThemeName was stored and ignored, and every grid took this library's theme colours.
    [Collection ("Headless")]
    public class RadGridViewTelerikThemeTests
    {
        private static readonly SKColor FluentAccent = new (0x00, 0x99, 0xBC);
        private static readonly SKColor RecolouredAccent = new (0x00, 0x73, 0xBC);

        private static (Form Form, RadGridView Grid) Grid (string themeName = "", bool alternating = false)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
            var grid = new RadGridView {
                Location = new Point (0, 0), Size = new Size (400, 300), AllowAddNewRow = false,
                ShowGroupPanel = true, EnableFiltering = true, ShowFilteringRow = true, EnablePaging = true, PageSize = 4,
                EnableAlternatingRowColor = alternating, ThemeName = themeName,
            };
            form.Controls.Add (grid);

            // Empty cells, so no text lands on a pixel read.
            var table = new DataTable ();
            table.Columns.Add ("A");
            table.Columns.Add ("B");
            for (var i = 0; i < 6; i++)
                table.Rows.Add ("", "");
            grid.DataSource = table;
            grid.Columns["A"]!.Width = 120;
            grid.Columns["B"]!.Width = 120;
            form.Show ();
            return (form, grid);
        }

        private static SKBitmap Capture (Form form) => SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));

        // The device y of the middle of a row, and of its bottom line (the row's bounds are laid out by a paint).
        private static int Middle (RadGridView grid, int row)
        {
            var r = ((DataGridView) grid).Rows[row].Bounds;
            return r.Top + r.Height / 2;
        }

        private static int BottomLine (RadGridView grid, int row) => ((DataGridView) grid).Rows[row].Bounds.Bottom - 1;

        // A point inside the middle of column B, clear of the current-cell outline (which is on A).
        private static int InB (RadGridView grid) => grid.GetColumnDeviceLeft (1) + grid.LogicalToDeviceUnits (60);

        private static void Reset ()
        {
            ThemeResolutionService.ApplicationThemeName = string.Empty;
            ThemeResolutionService.ApplicationAccentColor = Color.Empty;
        }

        [Fact]
        public void The_application_theme_wins_over_the_grids_own_and_the_grids_own_applies_without_one ()
        {
            try {
                var (form, grid) = Grid ("Fluent");

                using (form) {
                    Assert.Equal (SKColors.White, grid.ThemePalette!.Background);

                    ThemeResolutionService.ApplicationThemeName = "FluentDark";
                    Assert.Equal (SKColors.Black, grid.ThemePalette!.Background);

                    grid.ThemeName = "ControlDefault";
                    Assert.Equal (SKColors.Black, grid.ThemePalette!.Background);

                    ThemeResolutionService.ApplicationThemeName = string.Empty;
                    Assert.Null (grid.ThemePalette);
                }
            } finally {
                Reset ();
            }
        }

        [Fact]
        public void A_grid_with_no_theme_or_one_not_painted_keeps_the_library_colours ()
        {
            var (form, grid) = Grid ("ControlDefault");

            using (form) {
                Assert.Null (grid.ThemePalette);

                using var bitmap = Capture (form);
                var panel_y = grid.DeviceClientRectangle.Top + grid.GroupPanelBandHeight / 2;
                Assert.Equal (Theme.ControlLowColor, bitmap.GetPixel (grid.DeviceClientRectangle.Right - grid.LogicalToDeviceUnits (10), panel_y));
            }
        }

        [Fact]
        public void Fluent_paints_the_rows_lines_selection_and_current_cell_as_Telerik ()
        {
            var (form, grid) = Grid ("Fluent", alternating: true);

            using (form) {
                using var bitmap = Capture (form);
                var x = InB (grid);

                Assert.Equal (FluentAccent, bitmap.GetPixel (x, Middle (grid, 0)));                    // selected, current
                Assert.Equal (new SKColor (0xF2, 0xF2, 0xF2), bitmap.GetPixel (x, Middle (grid, 1)));   // alternating
                Assert.Equal (SKColors.White, bitmap.GetPixel (x, Middle (grid, 2)));
                Assert.Equal (new SKColor (0xCC, 0xCC, 0xCC), bitmap.GetPixel (x, BottomLine (grid, 2)));

                // No line between A and B.
                var b = grid.GetColumnDeviceLeft (1);
                Assert.Equal (SKColors.White, bitmap.GetPixel (b - 1, Middle (grid, 2)));

                // The current cell (A of row 0) is outlined 2px inside its edges.
                var a = grid.GetColumnDeviceLeft (0);
                var outline = new SKColor (0x33, 0x33, 0x33);
                var two = grid.LogicalToDeviceUnits (2);
                Assert.Equal (outline, bitmap.GetPixel (a, Middle (grid, 0)));
                Assert.Equal (outline, bitmap.GetPixel (a + two - 1, Middle (grid, 0)));
                Assert.Equal (FluentAccent, bitmap.GetPixel (a + two, Middle (grid, 0)));
            }
        }

        [Fact]
        public void Fluent_paints_the_header_filter_row_group_panel_and_pager_as_Telerik ()
        {
            var (form, grid) = Grid ("Fluent");

            using (form) {
                using var bitmap = Capture (form);
                var x = InB (grid);
                var client = grid.DeviceClientRectangle;

                Assert.Equal (new SKColor (0xE6, 0xE6, 0xE6), bitmap.GetPixel (client.Right - grid.LogicalToDeviceUnits (10), client.Top + grid.GroupPanelBandHeight / 2));

                // Telerik centres the captions: read the header band left of B's.
                var content = grid.GetContentArea ();
                Assert.Equal (SKColors.White, bitmap.GetPixel (grid.GetColumnDeviceLeft (1) + grid.LogicalToDeviceUnits (10), content.Top + grid.ScaledHeaderHeight / 2));
                Assert.Equal (new SKColor (0xCC, 0xCC, 0xCC), bitmap.GetPixel (x, content.Top + grid.ScaledHeaderHeight - 1));
                Assert.Equal (new SKColor (0xF2, 0xF2, 0xF2), bitmap.GetPixel (x, content.Top + grid.ScaledHeaderHeight + grid.FilterRowBandHeight / 2));

                var pager = grid.LayoutPagingPanel ().Band;
                Assert.Equal (new SKColor (0xCC, 0xCC, 0xCC), bitmap.GetPixel (pager.Right - grid.LogicalToDeviceUnits (4), pager.Top));
            }
        }

        [Fact]
        public void FluentDark_paints_black_rows_and_its_own_lines ()
        {
            try {
                ThemeResolutionService.ApplicationThemeName = "FluentDark";
                var (form, grid) = Grid (alternating: true);

                using (form) {
                    using var bitmap = Capture (form);
                    var x = InB (grid);

                    Assert.Equal (new SKColor (0x2B, 0x2B, 0x2B), bitmap.GetPixel (x, Middle (grid, 1)));
                    Assert.Equal (SKColors.Black, bitmap.GetPixel (x, Middle (grid, 2)));
                    Assert.Equal (new SKColor (0x4C, 0x4C, 0x4C), bitmap.GetPixel (x, BottomLine (grid, 2)));
                    Assert.Equal (SKColors.White, bitmap.GetPixel (grid.GetColumnDeviceLeft (0), Middle (grid, 0)));
                    Assert.Equal (new SKColor (0x33, 0x33, 0x33), bitmap.GetPixel (grid.DeviceClientRectangle.Right - grid.LogicalToDeviceUnits (10), grid.DeviceClientRectangle.Top + grid.GroupPanelBandHeight / 2));
                }
            } finally {
                Reset ();
            }
        }

        [Fact]
        public void The_hovered_row_takes_the_themes_hover_fill ()
        {
            var (form, grid) = Grid ("Fluent");

            using (form) {
                grid.HoveredRowIndex = 2;
                using var bitmap = Capture (form);
                Assert.Equal (new SKColor (0xC7, 0xC7, 0xC7), bitmap.GetPixel (InB (grid), Middle (grid, 2)));
            }
        }

        [Fact]
        public void A_recoloured_accent_moves_the_selection_to_its_hue ()
        {
            try {
                ThemeResolutionService.ApplicationAccentColor = Color.FromArgb (0x00, 0x57, 0x8E);
                var (form, grid) = Grid ("Fluent");

                using (form) {
                    using var bitmap = Capture (form);
                    Assert.Equal (RecolouredAccent, bitmap.GetPixel (InB (grid), Middle (grid, 0)));
                }
            } finally {
                Reset ();
            }
        }

        [Theory]
        [InlineData (0x00578E, 0x0073BC)]   // a blue accent: the hue moves, saturation and lightness stay
        [InlineData (0x00A0A0, 0x0099BC)]   // an accent already in Fluent's cyan-teal family leaves it alone
        [InlineData (0xC00000, 0xBC0000)]
        public void Recolouring_moves_the_accent_familys_hue_only (int accent, int expected)
        {
            var colour = RadGridThemePalette.Recolour (Color.FromArgb (0x00, 0x99, 0xBC), Color.FromArgb (accent));
            Assert.Equal (Color.FromArgb (expected).ToArgb () | unchecked ((int) 0xFF000000), colour.ToArgb ());
        }

        [Fact]
        public void No_accent_or_a_grey_keeps_the_themes ()
        {
            var fluent = Color.FromArgb (0x00, 0x99, 0xBC);
            Assert.Equal (fluent, RadGridThemePalette.Recolour (fluent, Color.Empty));
            Assert.Equal (Color.FromArgb (0x80, 0x80, 0x80), RadGridThemePalette.Recolour (Color.FromArgb (0x80, 0x80, 0x80), Color.FromArgb (0x00, 0x57, 0x8E)));
        }
    }
}
