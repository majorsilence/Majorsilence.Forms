using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the tree WinForms
    // draws under the Windows 11 theme: grey 9px plus/minus boxes joined by dotted GrayText lines, root lines
    // included, and upstream's columns. Measured on WinForms with a 16px ImageList and the default Indent of
    // 19: the button centred 11px in, the image at 22, the label at 41, each level 19 further in; without an
    // ImageList the button centred at 12 and the label at 22. The theme drew black arrows, solid lines in
    // another place, and the image 6px further left. Apps that have not chosen a font keep the theme's tree.
    [Collection ("Headless")]
    public class TreeViewUpstreamLookTests
    {
        private static readonly SKColor Edge = new SKColor (0x91, 0x91, 0x91);
        private static readonly SKColor Corner = new SKColor (0xBA, 0xBB, 0xBC);
        private static readonly SKColor Minus = new SKColor (0x4B, 0x63, 0xA7);
        private static readonly SKColor Upright = new SKColor (0x29, 0x42, 0x72);
        private static readonly SKColor Line = new SKColor (0x6D, 0x6D, 0x6D);
        private static readonly SKColor Red = new SKColor (0xFF, 0x00, 0x00);

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

        private static ImageList RedImages ()
        {
            var images = new ImageList { ImageSize = new Size (16, 16) };
            var red = new SKBitmap (16, 16);
            using (var canvas = new SKCanvas (red))
                canvas.Clear (Red);
            images.Images.Add (red);
            return images;
        }

        // Two roots with children, the first expanded; the tree fills a borderless form so its client
        // area starts at the form's (BorderStyle.None).
        private static (Form form, TreeView tree) Tree (bool images, Action<TreeView>? configure = null)
        {
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (240, 160) };
            var tree = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };

            if (images) {
                tree.ImageList = RedImages ();
                tree.ImageIndex = 0;
            }

            configure?.Invoke (tree);

            var settings = tree.Nodes.Add ("System Settings");
            settings.Nodes.Add ("Alpha child");
            settings.Nodes.Add ("Beta child");
            tree.Nodes.Add ("User Account Setup").Nodes.Add ("Gamma child");
            settings.Expand ();

            form.Controls.Add (tree);
            form.Show ();

            return (form, tree);
        }

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;
            public int RowTop;     // logical, of the first root
            public int RowHeight;  // logical

            // The pixel at a logical point, sampling the top-left device pixel of that logical pixel.
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round (x * Scale), (int) Math.Round (y * Scale));

            // The logical y of the centre of row `row`: half the row, rounded up to even.
            public int CentreY (int row)
            {
                var half = RowHeight / 2;
                return RowTop + row * RowHeight + (half % 2 == 0 ? half : half + 1);
            }
        }

        private static Shot Capture (bool chooseFont, bool images, Action<TreeView>? configure = null)
            => WithFont (chooseFont, () => {
                var (form, tree) = Tree (images, configure);

                using (form) {
                    var png = HeadlessRenderer.CapturePng (form, 240, 160);
                    var scale = tree.LogicalToDeviceUnits (100) / 100f;

                    return new Shot {
                        Bitmap = SKBitmap.Decode (png),
                        Scale = scale,
                        RowTop = (int) Math.Round (tree.Nodes[0].Bounds.Top / scale),
                        RowHeight = (int) Math.Round (tree.Nodes[0].Bounds.Height / scale),
                    };
                }
            });

        [Fact]
        public void An_expanded_node_has_upstream_s_minus_box ()
        {
            var shot = Capture (true, images: true);
            var cy = shot.CentreY (0);

            // A 9px box centred 11px in: corners, edges, and a 5px minus through the middle.
            Assert.Equal (Corner, shot.At (7, cy - 4));
            Assert.Equal (Corner, shot.At (15, cy + 4));
            Assert.Equal (Edge, shot.At (11, cy - 4));
            Assert.Equal (Edge, shot.At (7, cy));
            Assert.Equal (Minus, shot.At (9, cy));
            Assert.Equal (Minus, shot.At (13, cy));
            Assert.NotEqual (Upright, shot.At (11, cy - 2));
        }

        [Fact]
        public void A_collapsed_node_has_upstream_s_plus_box ()
        {
            var shot = Capture (true, images: true);
            var cy = shot.CentreY (3);   // User Account Setup, after the two children of the first root

            Assert.Equal (Edge, shot.At (7, cy));
            Assert.Equal (Minus, shot.At (11, cy));
            Assert.Equal (Upright, shot.At (11, cy - 2));
            Assert.Equal (Upright, shot.At (11, cy + 2));
        }

        [Fact]
        public void The_lines_are_dotted_and_run_down_the_button_column_and_out_to_the_content ()
        {
            var shot = Capture (true, images: true);
            var child = shot.CentreY (1);

            // The root column, through the first child's row: every other pixel.
            var top = shot.RowTop + shot.RowHeight;
            Assert.Equal (Line, shot.At (11, top));
            Assert.NotEqual (Line, shot.At (11, top + 1));
            Assert.Equal (Line, shot.At (11, top + 2));

            // The child's own column, 19 further in, and its stub out to the image at 41.
            Assert.Equal (Line, shot.At (30, child));
            Assert.Equal (Line, shot.At (34, child));
            Assert.NotEqual (Line, shot.At (35, child));
            Assert.Equal (Line, shot.At (40, child));
            Assert.Equal (Red, shot.At (41, child));
        }

        [Fact]
        public void The_image_starts_22px_in ()
        {
            var shot = Capture (true, images: true);
            var cy = shot.CentreY (0);

            Assert.NotEqual (Red, shot.At (21, cy));
            Assert.Equal (Red, shot.At (22, cy));
            Assert.Equal (Red, shot.At (37, cy));
            Assert.NotEqual (Red, shot.At (38, cy));
        }

        [Theory]
        // With a 16px ImageList: the button around 11, the image from 22, the label from 41.
        [InlineData (true, 19, true, 11, 25, 45)]
        // Without one: the button around 12, the label from 22.
        [InlineData (false, 19, true, 12, -1, 25)]
        // A wider Indent moves the content, not the button: image from 33, label from 52.
        [InlineData (true, 30, true, 11, 36, 56)]
        // Without root lines the roots have no column: the image from 3, the label from 22.
        [InlineData (true, 19, false, -1, 5, 25)]
        public void Hit_testing_follows_upstream_s_columns (bool images, int indent, bool rootLines, int button, int image, int label)
        {
            var (plus_minus, on_image, on_label) = WithFont (true, () => {
                var (form, tree) = Tree (images, t => {
                    t.Indent = indent;
                    t.ShowRootLines = rootLines;
                });

                using (form) {
                    HeadlessRenderer.CapturePng (form, 240, 160);
                    var y = (int) Math.Round ((tree.Nodes[1].Bounds.Top + tree.Nodes[1].Bounds.Height / 2f) / (tree.LogicalToDeviceUnits (100) / 100f));

                    TreeViewHitTestLocations At (int x) => x < 0 ? TreeViewHitTestLocations.PlusMinus : tree.HitTest (x, y).Location;

                    // Nodes[1] is the second root, collapsed, so it has a button.
                    return (button < 0 ? TreeViewHitTestLocations.PlusMinus : At (button),
                        image < 0 ? TreeViewHitTestLocations.Image : At (image),
                        At (label));
                }
            });

            Assert.Equal (TreeViewHitTestLocations.PlusMinus, plus_minus);
            Assert.Equal (TreeViewHitTestLocations.Image, on_image);
            Assert.Equal (TreeViewHitTestLocations.Label, on_label);
        }

        [Fact]
        public void Check_boxes_take_a_13px_column_before_the_image ()
        {
            // Measured: the check column from 22 to 34, the image from 35 to 50, the label from 54.
            var (check, image, label) = WithFont (true, () => {
                var (form, tree) = Tree (true, t => t.CheckBoxes = true);

                using (form) {
                    HeadlessRenderer.CapturePng (form, 240, 160);
                    var y = (int) Math.Round ((tree.Nodes[1].Bounds.Top + tree.Nodes[1].Bounds.Height / 2f) / (tree.LogicalToDeviceUnits (100) / 100f));

                    return (tree.HitTest (34, y).Location, tree.HitTest (52, y).Location, tree.HitTest (56, y).Location);
                }
            });

            Assert.Equal (TreeViewHitTestLocations.StateImage, check);
            Assert.Equal (TreeViewHitTestLocations.Image, image);
            Assert.Equal (TreeViewHitTestLocations.Label, label);
        }

        [Fact]
        public void Without_a_chosen_font_the_theme_tree_is_kept ()
        {
            var shot = Capture (false, images: true);
            var cy = shot.CentreY (0);
            var boxes = 0;

            for (var x = 0; x < 24; x++)
                for (var y = cy - 6; y <= cy + 6; y++)
                    if (shot.At (x, y) == Edge || shot.At (x, y) == Minus)
                        boxes++;

            Assert.Equal (0, boxes);
        }
    }
}
