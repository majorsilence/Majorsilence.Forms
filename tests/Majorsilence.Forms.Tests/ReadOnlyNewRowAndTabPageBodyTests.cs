using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class ReadOnlyNewRowAndTabPageBodyTests
    {
        // Upstream's new row depends on AllowUserToAddRows and the data source alone (AllowUserToAddRowsInternal), not on
        // ReadOnly: a read-only grid still shows it. Here it was hidden, and an empty read-only grid showed no rows at all.
        [Fact]
        public void A_read_only_grid_keeps_its_new_row ()
        {
            HeadlessRenderer.Use ();
            using var grid = new DataGridView { ReadOnly = true };
            grid.Columns.Add ("a", "A");

            Assert.Equal (0, grid.NewRowIndex);
            Assert.Equal (1, grid.RowCountWithNewRow);
        }

        private static readonly Color Navy = Color.FromArgb (24, 69, 114);

        private static (SKColor page, SKColor label) Pixels (bool chooseFont)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
                var tabs = new TabControl { Location = new Point (0, 0), Size = new Size (300, 200) };
                var page = new TabPage ("Invoices") { BackColor = Navy, UseVisualStyleBackColor = true };
                var label = new Label { Text = "", Location = new Point (10, 10), Size = new Size (60, 20) };
                page.Controls.Add (label);
                tabs.TabPages.Add (page);
                form.Controls.Add (tabs);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                // PointToScreen answers in device pixels, as the capture is; offsets within a control are logical.
                var form_origin = form.PointToScreen (Point.Empty);
                SKColor At (Control c, int x, int y)
                {
                    var p = c.PointToScreen (Point.Empty);
                    return bitmap.GetPixel (p.X - form_origin.X + (int) (x * scale), p.Y - form_origin.Y + (int) (y * scale));
                }

                return (At (page, 150, 100), At (label, label.Width / 2, label.Height / 2));
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // Upstream's TabPage with UseVisualStyleBackColor paints the themed tab body (#F9F9F9 under Windows 11) over its
        // BackColor; its children still take the BackColor as their ambient colour. It was stored and ignored, so a page
        // with a navy designer BackColor painted navy all over.
        [Fact]
        public void A_visual_style_page_paints_the_themed_body_and_its_children_keep_its_colour ()
        {
            var (page, label) = Pixels (chooseFont: true);

            Assert.Equal (new SKColor (0xF9, 0xF9, 0xF9), page);
            Assert.Equal (Navy.ToSKColor (), label);
        }

        [Fact]
        public void Without_a_chosen_font_the_page_keeps_its_back_colour ()
            => Assert.Equal (Navy.ToSKColor (), Pixels (chooseFont: false).page);
    }
}
