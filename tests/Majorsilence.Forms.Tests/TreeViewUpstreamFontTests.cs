using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets upstream's tree
    // text and row height: the text at the tree's own font, and rows ItemHeight apart, where an unset
    // ItemHeight is the font's GDI line height plus 2 (or the image height when taller), rounded up to even.
    // The theme drew every tree's text at its own size, 24px a row, so a 12pt tree's text was smaller than
    // WinForms' and its 22px rows were 24.
    [Collection ("Headless")]
    public class TreeViewUpstreamFontTests
    {
        private static T WithFont<T> (bool chooseFont, Func<T> body)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

                HeadlessRenderer.Use ();
                return body ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static TreeView Tree (string family, float points, int imageSize = 0)
        {
            var tree = new TreeView { Font = new Majorsilence.Forms.Drawing.Font (family, points) };

            if (imageSize > 0)
                tree.ImageList = new ImageList { ImageSize = new Size (imageSize, imageSize) };

            return tree;
        }

        [Theory]
        [InlineData ("Microsoft Sans Serif", 12f, 0, 22)]
        [InlineData ("Segoe UI", 9f, 0, 18)]
        [InlineData ("Arial", 10f, 0, 18)]
        [InlineData ("Tahoma", 8.25f, 0, 16)]
        [InlineData ("Segoe UI", 9f, 21, 22)]
        [InlineData ("Segoe UI", 9f, 32, 32)]
        public void The_default_item_height_is_upstream_s_on_Windows (string family, float points, int imageSize, int expected)
        {
            // Measured from WinForms' TreeView.ItemHeight once its handle exists; the line height comes from
            // GDI, so only Windows has these numbers.
            if (!OperatingSystem.IsWindows ())
                return;

            Assert.Equal (expected, WithFont (true, () => Tree (family, points, imageSize).ItemHeight));
        }

        [Fact]
        public void A_set_item_height_is_kept_even_when_it_is_20 ()
        {
            Assert.Equal (20, WithFont (true, () => {
                var tree = Tree ("Microsoft Sans Serif", 12f);
                tree.ItemHeight = 20;
                return tree.ItemHeight;
            }));
        }

        [Fact]
        public void Rows_are_item_height_apart ()
        {
            var (step, expected) = WithFont (true, () => {
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 120) };
                var tree = Tree ("Microsoft Sans Serif", 12f);
                tree.Dock = DockStyle.Fill;
                tree.Nodes.Add ("System Settings");
                tree.Nodes.Add ("User Account Setup");
                form.Controls.Add (tree);
                form.Show ();
                HeadlessRenderer.CapturePng (form, 200, 120);

                return (tree.Nodes[1].Bounds.Top - tree.Nodes[0].Bounds.Top, tree.LogicalToDeviceUnits (tree.ItemHeight));
            });

            Assert.Equal (expected, step);
        }

        // The height of the ink in the first row's text, in device pixels.
        private static int TextInkHeight (bool chooseFont, float points)
            => WithFont (chooseFont, () => {
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (240, 80) };
                var tree = Tree ("Microsoft Sans Serif", points);
                tree.Dock = DockStyle.Fill;
                tree.ShowLines = false;
                tree.ShowPlusMinus = false;
                tree.ItemHeight = 60;
                tree.Nodes.Add ("HHHH");
                form.Controls.Add (tree);
                form.Show ();

                // A selected node's text is HighlightText on Highlight, which the dark-ink scan below
                // would not see; measure the unselected text.
                tree.SelectedNode = null;

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 240, 80));
                int top = int.MaxValue, bottom = -1;

                for (var y = 2; y < bitmap.Height - 2; y++)
                    for (var x = 2; x < bitmap.Width - 2; x++) {
                        var c = bitmap.GetPixel (x, y);
                        if (c.Red + c.Green + c.Blue < 200) {
                            top = Math.Min (top, y);
                            bottom = Math.Max (bottom, y);
                        }
                    }

                return bottom - top + 1;
            });

        [Fact]
        public void The_text_is_drawn_at_the_tree_s_font ()
        {
            var small = TextInkHeight (true, 8f);
            var large = TextInkHeight (true, 24f);

            // A capital H is about 0.72em: 8pt is ~8px of ink, 24pt ~23px. The theme drew both the same.
            Assert.True (large > small * 2, $"8pt ink {small}px, 24pt ink {large}px");
        }

        [Fact]
        public void Without_a_chosen_font_the_theme_look_is_kept ()
        {
            Assert.Equal (20, WithFont (false, () => Tree ("Microsoft Sans Serif", 12f).ItemHeight));
            Assert.Equal (TextInkHeight (false, 8f), TextInkHeight (false, 24f));
        }
    }
}
