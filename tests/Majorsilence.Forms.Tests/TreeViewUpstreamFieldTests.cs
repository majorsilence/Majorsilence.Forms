using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // In a ported app (one that chose its font), a TreeView is the native field WinForms draws. Measured on WinForms
    // (.NET 10) under the Windows 11 theme, a 120 x 120 tree:
    //   - Fixed3D (the default): a #828790 line, then a pixel of the BackColor, the client 116 square from (2, 2);
    //   - FixedSingle: a #646464 line, the client 118 square from (1, 1);
    //   - None: no line, the client the whole tree;
    //   - the face white (SystemColors.Window) unless BackColor is set; the same lines when disabled.
    // Here the tree kept the theme's off-white face and a one-pixel #ABABAB frame whatever its BorderStyle, its client
    // 118 square for Fixed3D, so it showed two more pixels of rows each way.
    [Collection ("Headless")]
    public class TreeViewUpstreamFieldTests
    {
        private static readonly SKColor Outline3D = new SKColor (0x82, 0x87, 0x90);
        private static readonly SKColor OutlineSingle = new SKColor (0x64, 0x64, 0x64);

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;
            public Rectangle Client;
            public Color Back;

            // The pixel at a logical point of the tree, sampling the top-left device pixel of that logical pixel.
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round ((10 + x) * Scale), (int) Math.Round ((10 + y) * Scale));
        }

        private static Shot Capture (Action<TreeView>? set = null, bool ported = true)
        {
            try {
                if (ported)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (140, 140), FormBorderStyle = FormBorderStyle.None, BackColor = Color.Magenta };
                var tree = new TreeView { Location = new Point (10, 10), Size = new Size (120, 120) };
                set?.Invoke (tree);
                form.Controls.Add (tree);
                tree.Nodes.Add ("node 0");
                form.Show ();

                return new Shot {
                    Bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 140, 140)),
                    Scale = tree.LogicalToDeviceUnits (100) / 100f,
                    Client = tree.ClientRectangle,
                    Back = tree.BackColor,
                };
            } finally {
                if (ported)
                    Application.SetDefaultFont (null!);
            }
        }

        // The outline round the tree's edge, and what lies just inside it, at the middle of each side.
        private static void Edges (Shot shot, SKColor outline, SKColor inside)
        {
            Assert.Equal (outline, shot.At (0, 90));
            Assert.Equal (outline, shot.At (119, 90));
            Assert.Equal (outline, shot.At (90, 0));
            Assert.Equal (outline, shot.At (90, 119));
            Assert.Equal (inside, shot.At (1, 90));
            Assert.Equal (inside, shot.At (118, 90));
            Assert.Equal (inside, shot.At (90, 118));
        }

        [Fact]
        public void A_Fixed3D_tree_is_a_white_field_two_pixels_deep ()
        {
            var shot = Capture ();

            Assert.Equal (new Rectangle (2, 2, 116, 116), shot.Client);
            Assert.Equal (Color.White.ToArgb (), shot.Back.ToArgb ());
            Edges (shot, Outline3D, SKColors.White);
        }

        [Fact]
        public void A_FixedSingle_tree_has_a_darker_line_one_pixel_deep ()
        {
            var shot = Capture (t => t.BorderStyle = BorderStyle.FixedSingle);

            Assert.Equal (new Rectangle (1, 1, 118, 118), shot.Client);
            Edges (shot, OutlineSingle, SKColors.White);
        }

        [Fact]
        public void A_borderless_tree_is_all_client ()
        {
            var shot = Capture (t => t.BorderStyle = BorderStyle.None);

            Assert.Equal (new Rectangle (0, 0, 120, 120), shot.Client);
            Assert.Equal (SKColors.White, shot.At (0, 90));
        }

        [Fact]
        public void The_line_inside_the_outline_is_the_BackColor ()
        {
            var green = Color.FromArgb (200, 230, 200);
            var shot = Capture (t => t.BackColor = green);

            Edges (shot, Outline3D, new SKColor (200, 230, 200));
        }

        [Fact]
        public void A_disabled_tree_keeps_its_field ()
            => Edges (Capture (t => t.Enabled = false), Outline3D, SKColors.White);

        [Fact]
        public void An_app_that_has_not_chosen_its_font_keeps_the_theme_s_frame ()
        {
            var shot = Capture (ported: false);

            Assert.NotEqual (new Rectangle (2, 2, 116, 116), shot.Client);
            Assert.NotEqual (Outline3D, shot.At (0, 90));
            Assert.NotEqual (Color.White.ToArgb (), shot.Back.ToArgb ());
        }
    }
}
