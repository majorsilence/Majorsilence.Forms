using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik marks a selected row in its theme's pale amber with black text; the WinForms grid underneath
    // marks one with SystemColors.Highlight and HighlightText -- blue with white text. Measured on UI for
    // WinForms 2026.2.520 under the default theme (probes/wfgridsel): a row that is selected but not
    // current fills a flat #FFE7AE right across its width and keeps #000000 text. Every grid in a migrated
    // application showed the blue instead, which is the most visible of the grid differences.
    //
    // Telerik's CURRENT row is a vertical gradient around #FFE3A2, a shade darker. The cell-style model
    // underneath has neither a current-row style nor a per-row gradient, so a current row takes the same
    // flat selection fill: close, not exact, and not asserted here.
    [Collection ("Headless")]
    public class RadGridViewSelectionColourTests
    {
        private static readonly Color TelerikSelection = Color.FromArgb (0xFF, 0xE7, 0xAE);

        private static (Form form, RadGridView grid) Build ()
        {
            HeadlessRenderer.Use ();

            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Columns.Add ("Name");
            table.Rows.Add ("A100", "Alpha");
            table.Rows.Add ("B200", "Bravo");
            table.Rows.Add ("C300", "Charlie");

            var form = new Form { Size = new Size (420, 240) };
            var grid = new RadGridView { Left = 0, Top = 0, Width = 400, Height = 200 };
            form.Controls.Add (grid);
            grid.DataSource = table;

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            return (form, grid);
        }

        private static SKBitmap Buffer (Control control)
        {
            var method = typeof (Control).GetMethod ("GetBackBuffer",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (SKBitmap) method.Invoke (control, null)!;
        }

        private static int Count (SKBitmap bitmap, Color colour, int tolerance = 6)
        {
            var hits = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha > 0
                        && System.Math.Abs (c.Red - colour.R) <= tolerance
                        && System.Math.Abs (c.Green - colour.G) <= tolerance
                        && System.Math.Abs (c.Blue - colour.B) <= tolerance)
                        hits++;
                }
            return hits;
        }

        [Fact]
        public void The_selection_colour_is_a_fallback_and_not_pinned_into_the_cell_style ()
        {
            var (form, grid) = Build ();

            // Setting the colour on DefaultCellStyle instead would outrank the theme palette, and a grid
            // given a named Telerik theme would stop painting its selection in that theme's accent -- which
            // is exactly what the first attempt at this did.
            Assert.True (grid.DefaultCellStyle.SelectionBackColor.IsEmpty);
            Assert.True (grid.DefaultCellStyle.SelectionForeColor.IsEmpty);

            form.Dispose ();
        }

        [Fact]
        public void A_selected_row_is_painted_in_the_Telerik_selection_colour ()
        {
            var (form, grid) = Build ();

            grid.Rows[1].IsSelected = true;
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var bitmap = Buffer (grid);
            var amber = Count (bitmap, TelerikSelection);
            var highlight = Count (bitmap, SystemColors.Highlight);

            Assert.True (amber > 200, $"a selected row should be filled with Telerik's selection colour; found {amber} such pixels");
            Assert.True (highlight == 0, $"no part of the grid should use the system highlight; found {highlight} such pixels");

            form.Dispose ();
        }

        [Fact]
        public void A_grid_that_sets_its_own_selection_colour_keeps_it ()
        {
            var (form, grid) = Build ();

            // The Telerik colours are a default, not a fixed rule: an application that asked for its own
            // still gets it.
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb (0x20, 0x60, 0x20);
            grid.Rows[1].IsSelected = true;
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            Assert.Equal (Color.FromArgb (0x20, 0x60, 0x20), grid.DefaultCellStyle.SelectionBackColor);
            Assert.True (Count (Buffer (grid), Color.FromArgb (0x20, 0x60, 0x20)) > 200);

            form.Dispose ();
        }
    }
}
