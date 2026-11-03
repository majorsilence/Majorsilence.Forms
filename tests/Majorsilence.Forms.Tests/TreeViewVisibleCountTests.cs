using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A TreeView's VisibleCount is the number of whole rows its client area holds, and an odd ItemHeight is drawn a
    // pixel shorter, as upstream's are. Measured on WinForms (.NET 10) with Microsoft Sans Serif 12pt as the default
    // font, a borderless 200 x 200 tree of 30 nodes: 9 rows of the unset 22; 10 of ItemHeight 20; 11 of 19, drawn 18;
    // 10 of 21, drawn 20; 8 of 25, drawn 24; 6 of 30; 100 of 3, drawn 2; 200 of 1; 9 in a 199-high tree; 11 in Segoe
    // UI 9pt, whose rows are 18. ItemHeight reads back what was set.
    // The unset height follows the font, and Microsoft Sans Serif and Segoe UI are Windows fonts: elsewhere they fall
    // back to others, so those counts are checked against the rows the tree draws, and the measured ones on Windows.
    // Here VisibleCount divided the client's device height by the logical ItemHeight -- right at 100%, twice the rows
    // at 200% -- and an odd ItemHeight was drawn as set.
    [Collection ("Headless")]
    public class TreeViewVisibleCountTests
    {
        private static T WithTree<T> (System.Action<TreeView>? set, System.Func<TreeView, T> body)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (600, 600) };
                var tree = new TreeView { Location = new Point (0, 0), Size = new Size (200, 200), BorderStyle = BorderStyle.None };
                set?.Invoke (tree);
                form.Controls.Add (tree);

                for (var i = 0; i < 30; i++)
                    tree.Nodes.Add ("node " + i);

                form.Show ();
                return body (tree);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static int VisibleCount (System.Action<TreeView>? set = null) => WithTree (set, t => t.VisibleCount);

        // The measured count on Windows; elsewhere, the whole rows of the unset height -- the font's -- that fit.
        private static void FontRows (int measured, System.Action<TreeView>? set = null)
            => WithTree (set, t => {
                Assert.Equal (OperatingSystem.IsWindows () ? measured : t.ClientRectangle.Height / t.ItemHeight, t.VisibleCount);
                return 0;
            });

        [Fact]
        public void The_unset_item_height_s_rows ()
            => FontRows (9);

        [Theory]
        [InlineData (20, 10)]
        [InlineData (19, 11)]
        [InlineData (21, 10)]
        [InlineData (25, 8)]
        [InlineData (30, 6)]
        [InlineData (3, 100)]
        [InlineData (1, 200)]
        public void A_set_item_height_s_rows (int itemHeight, int expected)
            => Assert.Equal (expected, VisibleCount (t => t.ItemHeight = itemHeight));

        [Fact]
        public void A_part_row_does_not_count ()
            => Assert.Equal (9, VisibleCount (t => { t.Height = 199; t.ItemHeight = 22; }));

        [Fact]
        public void A_smaller_font_s_rows ()
            => FontRows (11, t => t.Font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));

        [Fact]
        public void An_odd_item_height_is_drawn_a_pixel_shorter_and_reads_back_as_set ()
        {
            // The second row starts 24 down, not 25.
            var (read, last, next) = WithTree (t => t.ItemHeight = 25, t => (t.ItemHeight, t.GetNodeAt (30, 23)?.Text, t.GetNodeAt (30, 24)?.Text));

            Assert.Equal (25, read);
            Assert.Equal ("node 0", last);
            Assert.Equal ("node 1", next);
        }
    }
}
