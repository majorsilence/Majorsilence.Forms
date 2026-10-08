using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LST-46's remainder: grouping in the tile views. LST-46 grouped Details/List/SmallIcon; LargeIcon
    // and Tile still flowed every item in insertion order with no band, so the same Groups an app built
    // for Details vanished when the user switched view. Upstream groups the icon views as it groups
    // Details: a header over each group, whose tiles start on a fresh line.
    //
    // A band takes one whole tile line, so the scroll arithmetic keeps stepping by one uniform line --
    // the same simplification the row views make with a one-row band.
    [Collection ("Headless")]
    public class ListViewTileGroupingTests
    {
        private static ListView Grouped (out Form form, out ListViewGroup first, out ListViewGroup second, View view = View.LargeIcon, int width = 400)
        {
            HeadlessRenderer.Use ();

            var list = new ListView { Width = width, Height = 300, View = view };

            foreach (var text in new[] { "alpha", "bravo", "charlie", "delta" })
                list.Items.Add (new ListViewItem (text));

            first = new ListViewGroup ("g1", "First");
            second = new ListViewGroup ("g2", "Second");
            list.Groups.Add (first);
            list.Groups.Add (second);

            // Interleaved on purpose: grouped, alpha and charlie share a line under First and bravo
            // starts its own under Second.
            list.Items[0].Group = first;
            list.Items[1].Group = second;
            list.Items[2].Group = first;
            list.Items[3].Group = second;

            form = new Form { Width = width + 100, Height = 400 };
            form.Controls.Add (list);
            form.Show ();
            PaintSurface.Render (list).Dispose ();

            return list;
        }

        [Theory]
        [InlineData (View.LargeIcon)]
        [InlineData (View.Tile)]
        public void Each_group_heads_its_own_tiles_which_start_on_a_fresh_line (View view)
        {
            var list = Grouped (out var form, out var first, out var second, view);

            using (form) {
                var alpha = list.Items[0].DeviceBounds;
                var bravo = list.Items[1].DeviceBounds;
                var charlie = list.Items[2].DeviceBounds;
                var delta = list.Items[3].DeviceBounds;

                Assert.Equal (2, list.GroupBands.Count);
                Assert.Same (first, list.GroupBands[0].Group);
                Assert.Same (second, list.GroupBands[1].Group);

                var first_band = list.GroupBands[0].DeviceBounds;
                var second_band = list.GroupBands[1].DeviceBounds;

                // First's tiles under First's band, side by side.
                Assert.True (first_band.Bottom <= alpha.Top);
                Assert.Equal (alpha.Top, charlie.Top);
                Assert.True (charlie.Left > alpha.Left);

                // Second's band below First's tiles, and Second's tiles start a fresh line under it,
                // at the left edge -- not after charlie, where the ungrouped flow put bravo.
                Assert.True (second_band.Top >= alpha.Bottom);
                Assert.True (bravo.Top >= second_band.Bottom);
                Assert.Equal (alpha.Left, bravo.Left);
                Assert.Equal (bravo.Top, delta.Top);
            }
        }

        [Fact]
        public void The_band_caption_is_drawn_in_the_icon_view ()
        {
            var list = Grouped (out var form, out _, out _);

            using (form) {
                using var bitmap = PaintSurface.Render (list);
                var band = list.GroupBands[0].DeviceBounds;
                // The most common colour: the list's empty background, whatever the theme.
                var background = Enumerable.Range (0, bitmap.Width)
                    .SelectMany (x => Enumerable.Range (0, bitmap.Height).Select (y => bitmap.GetPixel (x, y)))
                    .GroupBy (c => c).OrderByDescending (g => g.Count ()).First ().Key;

                var ink = 0;

                for (var x = band.Left; x < band.Right; x++)
                    for (var y = band.Top; y < band.Bottom; y++)
                        if (bitmap.GetPixel (x, y) != background)
                            ink++;

                Assert.True (ink > 0, "nothing drawn in the group header band");
            }
        }

        [Fact]
        public void A_collapsed_group_takes_its_tiles_off_screen_and_the_next_group_moves_up ()
        {
            var list = Grouped (out var form, out var first, out _);

            using (form) {
                var bravo_before = list.Items[1].DeviceBounds.Top;

                first.CollapsedState = ListViewGroupCollapsedState.Collapsed;
                list.LayoutItems ();

                Assert.True (list.Items[0].DeviceBounds.IsEmpty);
                Assert.True (list.Items[2].DeviceBounds.IsEmpty);
                Assert.True (list.Items[1].DeviceBounds.Top < bravo_before);
            }
        }

        [Fact]
        public void The_line_count_is_the_lines_laid_out_and_EnsureVisible_reaches_the_last_tile ()
        {
            // Narrow enough that each group wraps, short enough to scroll.
            HeadlessRenderer.Use ();
            var list = new ListView { Width = 180, Height = 160, View = View.LargeIcon };
            var first = new ListViewGroup ("g1", "First");
            var second = new ListViewGroup ("g2", "Second");
            list.Groups.Add (first);
            list.Groups.Add (second);

            for (var i = 0; i < 5; i++)
                list.Items.Add (new ListViewItem ("a" + i) { Group = first });
            for (var i = 0; i < 3; i++)
                list.Items.Add (new ListViewItem ("b" + i) { Group = second });

            using var form = new Form { Width = 300, Height = 300 };
            form.Controls.Add (list);
            form.Show ();
            PaintSurface.Render (list).Dispose ();

            var per_line = list.ItemsPerLine;
            Assert.True (per_line < 5, "the first group must wrap for this test to mean anything");

            // Bands plus each group's own whole lines; 5 + 3 items in one flow would be fewer lines
            // whenever 8 does not split the same way.
            var expected = 2 + ((5 + per_line - 1) / per_line) + ((3 + per_line - 1) / per_line);
            Assert.Equal (expected, list.LineCount);

            var last = list.Items.Count - 1;
            list.EnsureVisible (last);
            list.LayoutItems ();

            var area = list.ItemArea;
            var bounds = list.Items[last].DeviceBounds;
            Assert.True (bounds.Top >= area.Top && bounds.Bottom <= area.Bottom,
                $"the last tile {bounds} is not inside the item area {area}");
        }

        [Fact]
        public void Turning_groups_off_in_an_icon_view_drops_the_bands ()
        {
            var list = Grouped (out var form, out _, out _, View.Details);

            using (form) {
                Assert.NotEmpty (list.GroupBands);

                list.ShowGroups = false;
                list.View = View.LargeIcon;
                list.LayoutItems ();

                // Before, the tile layout never cleared the bands the row layout had placed, so the
                // stale Details headers were painted over the tiles.
                Assert.Empty (list.GroupBands);
            }
        }
    }
}
