using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // What font a grid's data cells and column headers fall back to when nothing styles them.
    //
    // Upstream's DataGridView.DefaultCellStyle.Font IS the control's Font, so a grid draws its cells in
    // whatever font the application chose -- including one chosen only with Application.SetDefaultFont,
    // where no control in the tree is ever given a Font of its own. Measured on WinForms with Microsoft
    // Sans Serif 12pt as the application default and no Font anywhere else (probes/wfgridfont): a
    // "04-01-2021" cell is 79x12 of ink, its column header 34x11, and setting the grid's Font or
    // DefaultCellStyle.Font to Tahoma 8pt takes the cell down to 55x8 while the styled header stays put.
    //
    // Here the cell came out 57x9: the fallback asked HasAmbientFontChoice, which only reports a Font
    // somebody assigned, so a ported application's grids drew the theme font a third smaller than their
    // own designer-styled headers.
    [Collection ("Headless")]
    public class DataGridViewAmbientCellFontTests
    {
        // A grid given no font at all, on a form given no font at all: the only font choice available is
        // the application's.
        private static (Form form, DataGridView grid) Grid ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 200) };
            var grid = new DataGridView {
                Location = new Point (0, 0), Size = new Size (600, 200), AllowUserToAddRows = false,
            };
            grid.Columns.Add ("Date", "Date");
            form.Controls.Add (grid);
            form.Show ();
            grid.Rows.Add ("04-01-2021");
            grid.ClearSelection ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            return (form, grid);
        }

        // The height of the text drawn inside a cell, in the grid's own units. The back buffer is in
        // device pixels, so the cell's logical rectangle is scaled onto it -- MF_HEADLESS_SCALE 2 would
        // otherwise sample the wrong half of the grid.
        private static int InkHeight (DataGridView grid, int columnIndex, int rowIndex)
        {
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (grid, null)!;

            var scale = bitmap.Width / (double) grid.Width;
            var cell = grid.GetCellDisplayRectangle (columnIndex, rowIndex, false);
            var r = Rectangle.FromLTRB (
                (int) Math.Round (cell.Left * scale), (int) Math.Round (cell.Top * scale),
                (int) Math.Round (cell.Right * scale), (int) Math.Round (cell.Bottom * scale));

            // Past the cell's own borders and gridlines, which are dark too.
            r.Inflate (-(int) Math.Round (4 * scale), -(int) Math.Round (4 * scale));

            int top = int.MaxValue, bottom = -1;

            for (var y = Math.Max (r.Top, 0); y < Math.Min (r.Bottom, bitmap.Height); y++)
                for (var x = Math.Max (r.Left, 0); x < Math.Min (r.Right, bitmap.Width); x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha == 0 || c.Red + c.Green + c.Blue >= 600) continue;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                    break;
                }

            return bottom < 0 ? 0 : (int) Math.Round ((bottom - top + 1) / scale);
        }

        [Fact]
        public void Data_cells_fall_back_to_the_font_the_application_chose ()
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

            try {
                var (form, grid) = Grid ();

                // Nothing in the tree was given a Font -- the application default is the only choice.
                Assert.False (grid.HasAmbientFontChoice ());

                // The font the grid resolves for itself, whatever the platform makes of the name: CI's
                // Linux images have no Microsoft Sans Serif and land on DejaVu Sans, so naming the family
                // here would be asserting what fonts the machine has rather than where the cell gets its
                // font from. The size is what separates this from the theme's font either way -- 12pt
                // comes out 16px, against the theme's item size.
                Assert.Equal (grid.GetEffectiveFont ().FamilyName, grid.CellFallbackFont ().Typeface.FamilyName);
                Assert.Equal (grid.GetEffectiveFontSize (), grid.CellFallbackFont ().Size);

                form.Dispose ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Column_headers_fall_back_to_it_too ()
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

            try {
                var (form, grid) = Grid ();

                Assert.Equal (grid.GetEffectiveFont ().FamilyName, grid.HeaderFallbackFont ().Typeface.FamilyName);
                Assert.Equal (grid.GetEffectiveFontSize (), grid.HeaderFallbackFont ().Size);

                form.Dispose ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // The rendered result, not just the font the fallback names: WinForms draws that cell 12 units of
        // ink high, and this drew 9.
        [Fact]
        public void A_data_cell_is_drawn_as_tall_as_WinForms_draws_it ()
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

            try {
                var (form, grid) = Grid ();

                var height = InkHeight (grid, 0, 0);
                Assert.InRange (height, 11, 13);

                form.Dispose ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // And an application that chose no font keeps the theme's fonts, as it always did: this follows
        // the application's choice, it does not abandon the theme.
        [Fact]
        public void An_application_that_chose_no_font_keeps_the_theme_font ()
        {
            var (form, grid) = Grid ();

            Assert.Same (Theme.UIFont, grid.CellFallbackFont ().Typeface);
            Assert.Equal (Theme.ItemFontSize, grid.CellFallbackFont ().Size);

            form.Dispose ();
        }
    }
}
