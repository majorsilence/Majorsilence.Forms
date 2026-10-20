using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's list box fills a selected item with SystemColors.Highlight. Here it was the theme's pale
    // control highlight, so a list with its first row selected read as one with nothing selected.
    [Collection ("Headless")]
    public class ListBoxSelectionColourTests
    {
        [Fact]
        public void A_selected_item_is_filled_with_the_highlight_colour ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 100) };
            var list = new ListBox { Location = new Point (10, 10), Size = new Size (150, 80) };
            list.Items.Add ("Ada");
            list.Items.Add ("Grace");
            form.Controls.Add (list);
            form.Show ();
            list.SelectedIndex = 0;

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 200, 100));
            var scale = bitmap.Width / 200f;
            var highlight = SystemColors.Highlight.ToSKColor ();
            int filled = 0;

            // The first item's row, right of its text.
            var y = (int) ((10 + 2 + list.ItemHeight / 2) * scale);

            for (var x = (int) (100 * scale); x < (int) (150 * scale); x++)
                if (bitmap.GetPixel (x, y) == highlight)
                    filled++;

            Assert.True (filled > 20 * scale, $"{filled} highlight pixels in the selected row");
        }
    }
}
