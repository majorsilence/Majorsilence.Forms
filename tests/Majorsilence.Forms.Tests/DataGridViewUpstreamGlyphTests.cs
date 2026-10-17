using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class DataGridViewUpstreamGlyphTests
    {
        [Fact]
        public void Under_the_upstream_look_a_check_box_cell_shows_the_check_box_glyph ()
        {
            // As upstream's cell draws the system check box, and as a CheckBox here already does under the
            // upstream look; the cell kept the theme's filled square.
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
                var grid = new DataGridView { Width = 200, Height = 120, RowHeadersVisible = false, AllowUserToAddRows = false };
                grid.Columns.Add (new DataGridViewCheckBoxColumn { Name = "Include", Width = 60 });
                grid.Rows.Add (true);
                grid.ClearSelection ();
                form.Controls.Add (grid);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var cell = grid.DeviceToLogicalUnits (grid.GetCellBounds (0, 0));

                int accent = 0, white = 0;
                for (var x = (int) (cell.Left * scale); x < (int) (cell.Right * scale); x++)
                    for (var y = (int) (cell.Top * scale); y < (int) (cell.Bottom * scale); y++) {
                        var px = bitmap.GetPixel (x, y);
                        if (px == new SKColor (0x00, 0x5F, 0xB8))
                            accent++;
                        else if (px == SKColors.White)
                            white++;
                    }

                Assert.True (accent > 20, $"the glyph's accent fill ({accent} px)");
                Assert.True (white > 0, "the glyph's white tick");
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
