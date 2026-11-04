using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A node's own BackColor in an enabled tree as upstream draws it, measured on WinForms (.NET 10, Windows 11 theme),
    // a 220 x 120 tree in Microsoft Sans Serif 12, rows 22 high:
    //   - only the node's label is filled (22 to 74 across the client for "node 1", the label's whole height), the
    //     rest of the row the tree's BackColor;
    //   - with FullRowSelect and ShowLines off, the whole row (0 to 215);
    //   - with FullRowSelect and ShowLines on, only the label again: upstream ignores FullRowSelect while lines show,
    //     for the selection highlight too;
    //   - a child node's label starts at its own indent (41 for a level-1 "child").
    // Here a node's BackColor filled the whole row whatever FullRowSelect said, and FullRowSelect highlighted the
    // whole row with lines on.
    [Collection ("Headless")]
    public class TreeViewUpstreamNodeBackColorTests
    {
        private static readonly SKColor Control = new SKColor (0xF0, 0xF0, 0xF0);

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;

            // The pixel at a logical point of the tree's client area (a Fixed3D tree's client starts 2 in).
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round ((12 + x) * Scale), (int) Math.Round ((12 + y) * Scale));
        }

        private static Shot Capture (Action<TreeView> set, bool ported = true)
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
                set (tree);
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
        public void Only_the_label_is_filled ()
        {
            var shot = Capture (t => t.Nodes[1].BackColor = Color.Yellow);

            Assert.Equal (SKColors.Yellow, shot.At (23, 22 + 1));
            Assert.Equal (SKColors.Yellow, shot.At (23, 22 + 20));
            Assert.Equal (SKColors.White, shot.At (5, 22 + 11));
            Assert.Equal (SKColors.White, shot.At (150, 22 + 1));
            Assert.Equal (SKColors.White, shot.At (23, 44 + 1));
        }

        [Fact]
        public void A_child_s_label_is_filled_from_its_own_indent ()
        {
            var shot = Capture (t => { t.Nodes[1].Nodes.Add ("child"); t.Nodes[1].Expand (); t.Nodes[1].Nodes[0].BackColor = Color.Yellow; });

            Assert.Equal (SKColors.Yellow, shot.At (42, 44 + 1));
            Assert.Equal (SKColors.White, shot.At (36, 44 + 1));
            Assert.Equal (SKColors.White, shot.At (150, 44 + 1));
        }

        [Fact]
        public void Full_row_select_without_lines_fills_the_whole_row ()
        {
            var shot = Capture (t => { t.FullRowSelect = true; t.ShowLines = false; t.Nodes[1].BackColor = Color.Yellow; });

            Assert.Equal (SKColors.Yellow, shot.At (1, 22 + 1));
            Assert.Equal (SKColors.Yellow, shot.At (150, 22 + 1));
            Assert.Equal (SKColors.White, shot.At (150, 44 + 1));
        }

        [Fact]
        public void Full_row_select_with_lines_fills_only_the_label ()
        {
            var shot = Capture (t => { t.FullRowSelect = true; t.Nodes[1].BackColor = Color.Yellow; });

            Assert.Equal (SKColors.Yellow, shot.At (23, 22 + 1));
            Assert.Equal (SKColors.White, shot.At (150, 22 + 1));
        }

        [Fact]
        public void Full_row_select_with_lines_highlights_only_the_label ()
        {
            var shot = Capture (t => { t.FullRowSelect = true; t.HideSelection = false; t.SelectedNode = t.Nodes[1]; });

            Assert.NotEqual (SKColors.White, shot.At (23, 22 + 1));
            Assert.Equal (SKColors.White, shot.At (150, 22 + 1));
        }

        [Fact]
        public void An_app_that_has_not_chosen_its_font_keeps_the_whole_row_fill ()
        {
            var shot = Capture (t => t.Nodes[1].BackColor = Color.Yellow, ported: false);

            Assert.Equal (SKColors.Yellow, shot.At (150, 22 + 1));
        }
    }
}
