using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TreeView.ImageIndex and SelectedImageIndex read as upstream's: -1 without an ImageList, 0 until set with
    // one, and the last image's index when set past it. Measured on WinForms: a tree given an ImageList and no
    // indices reports 0 for both and draws the first image on every node, the selected one included. They were
    // -1 here, so such a tree drew no images.
    [Collection ("Headless")]
    public class TreeViewDefaultImageIndexTests
    {
        private static readonly SKColor Red = new SKColor (0xFF, 0x00, 0x00);
        private static readonly SKColor Green = new SKColor (0x00, 0xFF, 0x00);

        private static ImageList Images ()
        {
            var images = new ImageList { ImageSize = new Size (16, 16) };

            foreach (var colour in new[] { Red, Green }) {
                var bitmap = new SKBitmap (16, 16);
                using (var canvas = new SKCanvas (bitmap))
                    canvas.Clear (colour);
                images.Images.Add (bitmap);
            }

            return images;
        }

        [Fact]
        public void The_indices_are_minus_one_without_an_image_list_and_zero_with_one ()
        {
            var tree = new TreeView ();

            Assert.Equal (-1, tree.ImageIndex);
            Assert.Equal (-1, tree.SelectedImageIndex);

            tree.ImageList = Images ();

            Assert.Equal (0, tree.ImageIndex);
            Assert.Equal (0, tree.SelectedImageIndex);
        }

        [Fact]
        public void A_set_index_is_kept_and_one_past_the_end_reads_as_the_last ()
        {
            var tree = new TreeView { ImageList = Images (), ImageIndex = 1, SelectedImageIndex = 5 };

            Assert.Equal (1, tree.ImageIndex);
            Assert.Equal (1, tree.SelectedImageIndex);

            tree.ImageIndex = -1;
            Assert.Equal (-1, tree.ImageIndex);

            tree.ImageList = null;
            Assert.Equal (-1, tree.SelectedImageIndex);
        }

        [Fact]
        public void Every_node_draws_the_first_image_when_no_index_is_set ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (240, 80) };
            var tree = new TreeView { Dock = DockStyle.Fill, ImageList = Images () };
            tree.Nodes.Add ("System Settings");
            tree.Nodes.Add ("User Account Setup");
            form.Controls.Add (tree);
            form.Show ();
            tree.SelectedNode = tree.Nodes[0];
            tree.Focus ();

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 240, 80));
            int red = 0, green = 0;

            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c == Red) red++;
                    else if (c == Green) green++;
                }

            var scale = tree.LogicalToDeviceUnits (100) / 100f;

            // Two 16px images, both the first.
            Assert.True (red >= 2 * 16 * 16 * scale * scale * 0.9, $"{red} red pixels");
            Assert.Equal (0, green);
        }
    }
}
