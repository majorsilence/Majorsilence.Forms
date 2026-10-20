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

        // Upstream's grid is FixedSingle by default, the window border style's frame, drawn black. The property
        // was stored and ignored with a Fixed3D default, so every grid had the theme's pale grey line; then it was
        // drawn in WindowFrame, the (grey) grid line colour.
        [Fact]
        public void A_grid_is_framed_in_black_by_default ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Location = new Point (10, 10), Size = new Size (200, 120) };
            form.Controls.Add (grid);
            form.Show ();

            Assert.Equal (BorderStyle.FixedSingle, grid.BorderStyle);

            using var bitmap = Capture (form);
            var scale = bitmap.Width / 300f;
            var frame = SKColors.Black;

            Assert.Equal (frame, bitmap.GetPixel ((int) (grid.Left * scale), (int) ((grid.Top + 100) * scale)));
        }

        // Upstream's grid lines are WindowFrame by default (DefaultGridColor). GridColor was Empty, which drew the theme's
        // paler line.
        [Fact]
        public void Grid_lines_are_window_frame_by_default ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
            var grid = new DataGridView { Location = new Point (10, 10), Size = new Size (280, 120), RowHeadersVisible = false, AllowUserToAddRows = false };
            grid.Columns.Add ("a", "A");
            grid.Columns.Add ("b", "B");
            grid.Rows.Add ("x", "y");
            form.Controls.Add (grid);
            form.Show ();
            grid.ClearSelection ();

            Assert.Equal (SystemColors.WindowFrame, grid.GridColor);

            using var bitmap = Capture (form);
            var scale = bitmap.Width / 300f;
            var cell = grid.GetCellBounds (0, 0);   // device pixels, within the grid
            var origin = grid.PointToScreen (Point.Empty);
            var form_origin = form.PointToScreen (Point.Empty);

            Assert.Equal (SystemColors.WindowFrame.ToSKColor (),
                bitmap.GetPixel (origin.X - form_origin.X + cell.Right - 1, origin.Y - form_origin.Y + cell.Top + cell.Height / 2));
        }

        private static SKColor HeaderPixel (bool chooseFont, bool visualStyles)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
                var grid = new DataGridView { Location = new Point (10, 10), Size = new Size (280, 120), RowHeadersVisible = false, EnableHeadersVisualStyles = visualStyles };
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.MidnightBlue;
                grid.Columns.Add ("a", "");
                grid.Columns[0].Width = 100;
                form.Controls.Add (grid);
                form.Show ();

                using var bitmap = Capture (form);
                var origin = grid.PointToScreen (Point.Empty);
                var form_origin = form.PointToScreen (Point.Empty);
                var scale = bitmap.Width / 300f;

                return bitmap.GetPixel (origin.X - form_origin.X + (int) (50 * scale), origin.Y - form_origin.Y + (int) (grid.ColumnHeadersHeight / 2 * scale));
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // Upstream's EnableHeadersVisualStyles draws the theme's header items, white under Windows 11, whatever the header
        // style's BackColor; turned off, the header style is used. It was stored and ignored.
        [Fact]
        public void Visual_style_headers_are_the_themes_and_ignore_the_header_style ()
        {
            Assert.Equal (SKColors.White, HeaderPixel (chooseFont: true, visualStyles: true));
            Assert.Equal (Color.MidnightBlue.ToSKColor (), HeaderPixel (chooseFont: true, visualStyles: false));
            Assert.Equal (Color.MidnightBlue.ToSKColor (), HeaderPixel (chooseFont: false, visualStyles: true));
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
