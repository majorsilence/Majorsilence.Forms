using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class DataGridViewFrameAndHeaderTests
    {
        private static SKBitmap Capture (Form form)
            => SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));

        // Upstream's grid is FixedSingle by default, a frame in the window frame colour (black). The property
        // was stored and ignored with a Fixed3D default, so every grid had the theme's pale grey line.
        [Fact]
        public void A_grid_is_framed_in_the_window_frame_colour_by_default ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Location = new Point (10, 10), Size = new Size (200, 120) };
            form.Controls.Add (grid);
            form.Show ();

            Assert.Equal (BorderStyle.FixedSingle, grid.BorderStyle);

            using var bitmap = Capture (form);
            var scale = bitmap.Width / 300f;
            var frame = SystemColors.WindowFrame.ToSKColor ();

            Assert.Equal (frame, bitmap.GetPixel ((int) (grid.Left * scale), (int) ((grid.Top + 100) * scale)));
        }

        [Fact]
        public void A_grid_without_a_border_has_no_frame ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Location = new Point (10, 10), Size = new Size (200, 120), BorderStyle = BorderStyle.None, BackgroundColor = Color.White };
            form.Controls.Add (grid);
            form.Show ();

            using var bitmap = Capture (form);
            var scale = bitmap.Width / 300f;

            Assert.Equal (grid.BackgroundColor.ToSKColor (), bitmap.GetPixel ((int) (grid.Left * scale), (int) ((grid.Top + 100) * scale)));
        }

        // Upstream's default row height is the application's default font's height plus 9: 25 for the Segoe UI 9pt
        // a ported app chooses. It was a fixed 22, a few pixels short, so more (and cramped) rows showed.
        [Fact]
        public void A_rows_default_height_follows_the_chosen_default_font ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));
                HeadlessRenderer.Use ();
                using var grid = new DataGridView ();
                grid.Columns.Add ("a", "A");
                grid.Rows.Add ("x");

                Assert.Equal (SystemFonts.DefaultFont.Height + 9, grid.Rows[0].Height);
            } finally {
                Application.SetDefaultFont (null!);
            }

            using var classic = new DataGridView ();
            classic.Columns.Add ("a", "A");
            classic.Rows.Add ("x");
            Assert.Equal (22, classic.Rows[0].Height);
        }

        // Trailing spaces count towards a cell's text width, as GDI's do: a value padded to its column's width
        // overflows the cell and ends in an ellipsis upstream. Here the spaces hung past the edge unmeasured.
        [Fact]
        public void A_padded_value_too_wide_for_its_cell_ends_in_an_ellipsis ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Location = new Point (10, 10), Size = new Size (280, 120), RowHeadersVisible = false, AllowUserToAddRows = false };
            grid.Columns.Add ("a", "Name");
            grid.Columns[0].Width = 120;
            grid.Rows.Add ("AB" + new string (' ', 60));
            form.Controls.Add (grid);
            form.Show ();
            grid.ClearSelection ();

            using var bitmap = Capture (form);
            var scale = bitmap.Width / 300f;
            var row = grid.GetRowDisplayRectangle (0, false);
            var dark = 0;

            // The right half of the cell: blank but for an ellipsis.
            for (var x = (int) ((grid.Left + 60) * scale); x < (int) ((grid.Left + 118) * scale); x++)
                for (var y = (int) ((grid.Top + row.Top + 2) * scale); y < (int) ((grid.Top + row.Bottom - 2) * scale); y++)
                    if (bitmap.GetPixel (x, y).Red < 0x80)
                        dark++;

            Assert.True (dark > 0, "no ellipsis at the end of the padded value");
        }

        // Upstream draws a wrapped caption taller than its header as only the lines the band holds whole, aligned
        // as any caption: a two-line caption in a one-line band shows its first line, level with the others.
        [Fact]
        public void A_wrapped_caption_taller_than_its_header_shows_the_lines_that_fit ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
            var grid = new DataGridView {
                Location = new Point (10, 10), Size = new Size (280, 120),
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                RowHeadersVisible = false,
            };
            grid.Columns.Add ("a", "Name");
            grid.Columns.Add ("b", "Cheque Date");
            grid.Columns[0].Width = 100;
            grid.Columns[1].Width = 60;   // two lines' worth of caption
            form.Controls.Add (grid);
            form.Show ();

            Assert.True (grid.HeaderWraps);

            using var bitmap = Capture (form);
            var scale = bitmap.Width / 300f;

            // The topmost dark pixel of each caption, inside the header band.
            int InkTop (int left, int width)
            {
                for (var y = (int) ((grid.Top + 1) * scale); y < (int) ((grid.Top + grid.ColumnHeadersHeight) * scale); y++)
                    for (var x = (int) ((grid.Left + left) * scale); x < (int) ((grid.Left + left + width) * scale); x++)
                        if (bitmap.GetPixel (x, y).Red < 0x80)
                            return y;

                return -1;
            }

            var single = InkTop (8, 80);
            var wrapped = InkTop (108, 40);

            Assert.True (single > 0 && wrapped > 0, $"no caption ink ({single}, {wrapped})");
            // Its first line sits level with the one-line caption: it was drawn whole and overflowing, its first
            // line higher than the others' and the second cut off at the bottom.
            Assert.InRange (wrapped, single - (int) scale, single + (int) scale);
        }
    }
}
