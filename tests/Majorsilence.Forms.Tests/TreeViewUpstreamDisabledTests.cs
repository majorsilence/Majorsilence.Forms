using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A disabled tree's nodes as upstream draws them, measured on WinForms (.NET 10, Windows 11 theme), a 220 x 120
    // tree in Microsoft Sans Serif 12:
    //   - every node's label (22 to 74 across the client for "node 0", its rows 22 high) filled with Control
    //     (#F0F0F0), selected or not, the rest of the row the tree's BackColor;
    //   - with FullRowSelect, the whole row Control;
    //   - the text GrayText (#6D6D6D), whatever the tree's ForeColor;
    //   - a node's own BackColor and ForeColor in place of Control and GrayText;
    //   - no selection highlight.
    // Here the labels were the face's colour, a node's ForeColor gave way to the theme's disabled colour, and the
    // selected node kept its highlight.
    [Collection ("Headless")]
    public class TreeViewUpstreamDisabledTests
    {
        private static readonly SKColor Control = new SKColor (0xF0, 0xF0, 0xF0);
        private static readonly SKColor GrayText = new SKColor (0x6D, 0x6D, 0x6D);

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;

            // The pixel at a logical point of the tree's client area (a Fixed3D tree's client starts 2 in).
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round ((12 + x) * Scale), (int) Math.Round ((12 + y) * Scale));

            // Whether any pixel of a row, across the label's first 50 logical pixels, is the colour.
            public bool RowHas (int row, SKColor colour)
            {
                for (var x = (int) (34 * Scale); x < (int) (84 * Scale); x++)
                    for (var y = (int) ((12 + row * 22) * Scale); y < (int) ((12 + row * 22 + 22) * Scale); y++)
                        if (Bitmap.GetPixel (x, y) == colour)
                            return true;
                return false;
            }
        }

        private static Shot Capture (Action<TreeView>? set = null, bool ported = true)
        {
            try {
                if (ported)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (240, 140), FormBorderStyle = FormBorderStyle.None, BackColor = Color.Magenta };
                var tree = new TreeView { Location = new Point (10, 10), Size = new Size (220, 120), ItemHeight = 22 };
                form.Controls.Add (tree);
                for (var i = 0; i < 3; i++)
                    tree.Nodes.Add ("node " + i);
                set?.Invoke (tree);
                tree.Enabled = false;
                form.Show ();

                return new Shot {
                    Bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 240, 140)),
                    Scale = tree.LogicalToDeviceUnits (100) / 100f,
                };
            } finally {
                if (ported)
                    Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Each_label_is_filled_with_Control_and_the_rest_of_the_row_is_the_face ()
        {
            var shot = Capture ();

            for (var row = 0; row < 3; row++) {
                Assert.Equal (Control, shot.At (23, row * 22 + 1));
                Assert.Equal (SKColors.White, shot.At (150, row * 22 + 1));
            }
            Assert.Equal (SKColors.White, shot.At (23, 3 * 22 + 1));
        }

        [Fact]
        public void The_text_is_GrayText_whatever_the_tree_s_ForeColor ()
        {
            var shot = Capture (t => t.ForeColor = Color.Red);

            Assert.True (shot.RowHas (0, GrayText));
            Assert.False (shot.RowHas (0, SKColors.Red));
        }

        [Fact]
        public void The_selected_node_is_drawn_as_the_others ()
        {
            var shot = Capture (t => { t.HideSelection = false; t.SelectedNode = t.Nodes[1]; });

            Assert.Equal (Control, shot.At (23, 22 + 1));
            Assert.True (shot.RowHas (1, GrayText));
            Assert.False (shot.RowHas (1, SKColors.Black));
        }

        [Fact]
        public void A_full_row_select_tree_fills_each_whole_row ()
        {
            var shot = Capture (t => { t.FullRowSelect = true; t.ShowLines = false; });

            Assert.Equal (Control, shot.At (1, 1));
            Assert.Equal (Control, shot.At (150, 22 + 1));
        }

        [Fact]
        public void A_node_s_own_colours_still_show ()
        {
            var shot = Capture (t => { t.Nodes[1].BackColor = Color.Yellow; t.Nodes[1].ForeColor = Color.Red; });

            Assert.Equal (SKColors.Yellow, shot.At (23, 22 + 1));
            Assert.True (shot.RowHas (1, SKColors.Red));
            Assert.Equal (Control, shot.At (23, 1));
        }

        [Fact]
        public void An_app_that_has_not_chosen_its_font_keeps_the_theme_s_disabled_nodes ()
        {
            var shot = Capture (ported: false);

            Assert.NotEqual (Control, shot.At (23, 1));
        }
    }
}
