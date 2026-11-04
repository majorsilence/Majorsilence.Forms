using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadTreeView is laid out and drawn as Telerik's is under its default theme, whether or not the app chose
    // its font. Measured on UI for WinForms 2026.2.520 (.NET 10) with a 12pt Microsoft Sans Serif tree: rows 20
    // high, 19 apart; a 9px navy expander 6px into each 20px column; a 16px image 1px past the expander column;
    // black Segoe UI 9pt text, a "|" 3px past the image element and 5..16px down the row; white inside a #6E99D2
    // border; no lines, and with ShowLines #808080 dots down the columns; nothing selected on focus. Here the tree
    // was drawn as WinForms' (or the theme's): rows 22 high in the 12pt font, the text 7px further left and
    // 4px taller, grey upstream buttons, dotted lines and the first node selected.
    [Collection ("Headless")]
    public class RadTreeViewTelerikLookTests
    {
        private static readonly SKColor Orange = new SKColor (0xFF, 0xA5, 0x00);
        private static readonly SKColor Navy = new SKColor (0x0A, 0x1F, 0x42);
        private static readonly SKColor Line = new SKColor (0x80, 0x80, 0x80);

        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;
            public int ItemHeight;
            public int RowPitch;   // device pixels from a row's top to the next's
            public bool Focused;
            public TreeNode? Selected;

            // The pixel at a logical point, sampling the top-left device pixel of that logical pixel.
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round (x * Scale), (int) Math.Round (y * Scale));

            // The logical rectangle round the dark pixels from x onwards in [top, top + 19).
            public Rectangle Ink (int left, int top)
            {
                int? x0 = null, y0 = null, y1 = null;

                for (var x = (int) Math.Round (left * Scale); x < Bitmap.Width - 2 && x0 is null; x++)
                    for (var y = (int) Math.Round (top * Scale); y < (int) Math.Round ((top + 19) * Scale); y++) {
                        var p = Bitmap.GetPixel (x, y);

                        if (p.Red + p.Green + p.Blue < 450 && p != Orange) {
                            x0 = x;
                            y0 ??= y;
                            y1 = y;
                        }
                    }

                Assert.True (x0.HasValue, $"No text on the row at {top}.");
                return Rectangle.FromLTRB ((int) Math.Round (x0!.Value / Scale), (int) Math.Round (y0!.Value / Scale), 0, (int) Math.Round (y1!.Value / Scale));
            }
        }

        // Alpha (open) > Child (open) > Grand, Alpha > Leaf, Beta (closed) > Hidden, Gamma with a 16px image.
        private static Shot Capture (bool chooseFont, Action<RadTreeView>? configure = null)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

                HeadlessRenderer.Use ();

                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (340, 300) };
                var tree = new RadTreeView { Location = new Point (0, 0), Size = new Size (340, 300), Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12F) };
                configure?.Invoke (tree);

                var icon = new SKBitmap (16, 16);
                using (var canvas = new SKCanvas (icon))
                    canvas.Clear (Orange);

                TreeView nodes = tree;
                var alpha = nodes.Nodes.Add ("|Alpha");
                var child = alpha.Nodes.Add ("|Child");
                child.Nodes.Add ("|Grand");
                alpha.Nodes.Add ("|Leaf");
                nodes.Nodes.Add ("|Beta").Nodes.Add ("|Hidden");
                nodes.Nodes.Add ("|Gamma").Image = icon;
                alpha.Expand ();
                child.Expand ();

                form.Controls.Add (tree);
                form.Show ();
                tree.Focus ();

                return new Shot {
                    Bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 340, 300)),
                    Scale = tree.LogicalToDeviceUnits (100) / 100f,
                    ItemHeight = tree.ItemHeight,
                    RowPitch = child.DeviceBounds.Top - alpha.DeviceBounds.Top,
                    Focused = tree.Focused,
                    Selected = ((TreeView) tree).SelectedNode,
                };
            } finally {
                if (chooseFont)
                    Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void It_has_Telerik_s_defaults ()
        {
            var tree = new RadTreeView ();

            Assert.False (tree.ShowLines);
            Assert.True (tree.FullRowSelect);
            Assert.Equal (20, tree.TreeIndent);
            Assert.Equal (20, tree.ItemHeight);
            Assert.Equal (-1, tree.SpacingBetweenNodes);

            tree.SpacingBetweenNodes = -3;
            Assert.Equal (-3, tree.SpacingBetweenNodes);
        }

        [Theory, InlineData (false), InlineData (true)]
        public void Rows_are_20_high_and_19_apart_whatever_the_font (bool chooseFont)
        {
            var shot = Capture (chooseFont);

            Assert.Equal (20, shot.ItemHeight);
            Assert.Equal ((int) Math.Round (19 * shot.Scale), shot.RowPitch);
        }

        [Theory, InlineData (false), InlineData (true)]
        public void The_text_is_Segoe_UI_9_after_the_expander_column (bool chooseFont)
        {
            var shot = Capture (chooseFont);
            var slack = shot.Scale > 1 ? 1 : 0;

            // Rows 1, 20, 39, ...: a root's "|" at 26, a child's at 46, a grandchild's at 66, and the imaged
            // Gamma's at 42; each 5..16 down its row, which is Segoe UI 9pt's bar, not the 12pt font's. Only
            // Windows has Segoe UI; elsewhere a fallback draws the bar, so all that holds is that the text
            // starts past the expander column (and past Gamma's image).
            foreach (var (left, top, expected) in new[] { (21, 1, 26), (41, 20, 46), (61, 39, 66), (41, 58, 46), (21, 77, 26), (40, 96, 42) }) {
                var ink = shot.Ink (left, top);

                if (!OperatingSystem.IsWindows ()) {
                    Assert.InRange (ink.Left, left + 1, expected + 6);
                    continue;
                }

                Assert.InRange (ink.Left, expected - slack, expected + slack);
                Assert.InRange (ink.Top - top, 5 - slack, 5 + slack);
                Assert.InRange (ink.Bottom - top, 16 - slack, 16 + slack);
            }
        }

        [Fact]
        public void The_expander_is_Telerik_s_navy_box ()
        {
            var shot = Capture (true);

            // Alpha's open box at (7, 7): navy edges, a minus, white inside.
            Assert.Equal (new SKColor (0x6F, 0x7B, 0x90), shot.At (7, 7));
            Assert.Equal (Navy, shot.At (8, 7));
            Assert.Equal (new SKColor (0x96, 0xAC, 0xCA), shot.At (15, 15));
            Assert.Equal (Navy, shot.At (9, 11));
            Assert.Equal (SKColors.White, shot.At (11, 9));

            // Child's, one column in.
            Assert.Equal (Navy, shot.At (28, 26));

            // Beta's closed box at (7, 83): lighter at the top left, and an upright through the minus.
            Assert.Equal (new SKColor (0x9F, 0xB6, 0xD6), shot.At (7, 83));
            Assert.Equal (new SKColor (0x6F, 0x7B, 0x90), shot.At (15, 91));
            Assert.Equal (new SKColor (0x17, 0x31, 0x5D), shot.At (11, 85));
            Assert.Equal (new SKColor (0x36, 0x5D, 0x9C), shot.At (11, 87));
        }

        [Fact]
        public void The_image_is_drawn_1px_past_the_expander_column ()
        {
            var shot = Capture (true);

            Assert.Equal (Orange, shot.At (22, 98));
            Assert.Equal (Orange, shot.At (37, 113));
            Assert.NotEqual (Orange, shot.At (21, 98));
            Assert.NotEqual (Orange, shot.At (38, 113));
        }

        [Fact]
        public void The_tree_is_white_inside_a_blue_border_and_has_no_lines ()
        {
            var shot = Capture (true);

            Assert.Equal (new SKColor (0x6E, 0x99, 0xD2), shot.At (0, 150));
            Assert.Equal (SKColors.White, shot.At (1, 150));
            Assert.Equal (SKColors.White, shot.At (11, 17));
        }

        [Fact]
        public void ShowLines_draws_grey_dots_down_the_columns_and_out_to_the_content ()
        {
            var shot = Capture (true, tree => { tree.ShowLines = true; tree.TreeIndent = 21; });

            // Down the roots' column from below Alpha's box, and down Child's column above its box.
            Assert.Equal (Line, shot.At (11, 17));
            Assert.Equal (SKColors.White, shot.At (11, 18));
            Assert.Equal (Line, shot.At (32, 22));
            Assert.Equal (SKColors.White, shot.At (32, 24));

            // Out along Alpha's row from past its box, stopping short of Child's column.
            Assert.Equal (Line, shot.At (18, 11));
            Assert.Equal (Line, shot.At (20, 11));
            Assert.Equal (SKColors.White, shot.At (22, 11));

            // Gamma has no box: from 2px past the centre to 3px short of the column's edge.
            Assert.Equal (Line, shot.At (13, 106));
            Assert.Equal (Line, shot.At (19, 106));
            Assert.Equal (SKColors.White, shot.At (21, 106));
        }

        [Fact]
        public void Gaining_the_focus_selects_nothing ()
        {
            var shot = Capture (true);

            Assert.True (shot.Focused);
            Assert.Null (shot.Selected);
        }
    }
}
