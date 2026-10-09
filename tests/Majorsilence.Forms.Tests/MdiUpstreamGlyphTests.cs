using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Under upstream's menu metrics (the app chose a font) a maximized MDI child's merged buttons carry
    // WinForms' caption glyphs, measured from it: a 7 x 2 minimize bar and an X of 2px strokes, in solid
    // pixels of the text colour. The theme's 1px outlines read as a lighter, different set.
    [Collection ("Headless")]
    public class MdiUpstreamGlyphTests
    {
        private static int GlyphPixels (MdiControlItem.Kind kind)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));
                HeadlessRenderer.Use ();

                // Borderless, so the menu's bounds are the capture's coordinates.
                using var parent = new Form { Text = "Designer", FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 300) };
                var menu = new MenuStrip ();
                menu.Items.Add (new ToolStripMenuItem ("&File"));
                parent.Controls.Add (menu);
                parent.MainMenuStrip = menu;
                parent.IsMdiContainer = true;
                parent.Show ();

                var child = new Form { Text = "Doc", MdiParent = parent, ClientSize = new Size (200, 100) };
                child.WindowState = FormWindowState.Maximized;
                child.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (parent, 600, 300));
                var scale = bitmap.Width / 600f;
                var item = menu.Items.OfType<MdiControlItem> ().Single (i => i.ControlKind == kind);
                var colour = Menu.DefaultItemStyle.GetForegroundColor ();
                var count = 0;

                for (var x = (int) (item.Bounds.Left * scale); x < (int) (item.Bounds.Right * scale); x++)
                    for (var y = (int) (item.Bounds.Top * scale); y < (int) (item.Bounds.Bottom * scale); y++)
                        if (bitmap.GetPixel (x, y) == colour)
                            count++;

                return (int) System.Math.Round (count / (scale * scale));
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Minimize_is_a_seven_by_two_bar ()
            => Assert.Equal (14, GlyphPixels (MdiControlItem.Kind.Minimize));

        [Fact]
        public void Close_is_an_x_of_two_pixel_strokes ()
            // Two strokes of ten 2px rows each, sharing the 2 pixels where they cross.
            => Assert.Equal (38, GlyphPixels (MdiControlItem.Kind.Close));
    }
}
