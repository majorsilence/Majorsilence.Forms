using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's multi-row FillToRight (TCS_RIGHTJUSTIFY) does not just stretch the rows the tabs wrapped into: it
    // wraps a pixel early and then moves tabs down so the last row is not left short. Every expectation below is
    // WinForms (.NET 10, Windows 11 theme) measured with tabs 54px wide: empty captions and TabControl.Padding
    // (27, 3). Ours stretched the plain wrap, so eleven tabs in a 300px control sat five, five and one instead of
    // three, four and four.
    [Collection ("Headless")]
    public class TabControlFillToRightRowsTests
    {
        private const int TabWidth = 54;

        private sealed record Layout (List<int> Counts, List<Rectangle> Tabs, int SelectedRowTop, List<int> RowTops);

        private static Layout Measure (bool choose, int count, int width, TabSizeMode mode = TabSizeMode.FillToRight, int selected = 0)
        {
            try {
                if (choose)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (1000, 500) };
                var tabs = new TabControl { Location = new Point (0, 0), Size = new Size (width, 400), Multiline = true, SizeMode = mode, Padding = new Point (TabWidth / 2, 3) };
                for (var i = 0; i < count; i++)
                    tabs.TabPages.Add (new TabPage (string.Empty));
                form.Controls.Add (tabs);
                form.Show ();
                tabs.SelectedIndex = selected;
                HeadlessRenderer.CapturePng (form, 1000, 500);

                // Read before the finally drops the chosen font.
                var rects = Enumerable.Range (0, count).Select (tabs.GetTabRect).ToList ();
                var counts = new List<int> ();
                for (var i = 0; i < count; i++) {
                    if (i == 0 || rects[i].Left <= rects[i - 1].Left)
                        counts.Add (0);
                    counts[^1]++;
                }

                var tops = new List<int> ();
                var start = 0;
                foreach (var c in counts) {
                    tops.Add (rects[start].Top);
                    start += c;
                }

                return new Layout (counts, rects, rects[selected].Top, tops);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Theory]
        // A tab moves down only when it fits on the last row with 2px to spare.
        [InlineData (3, 113, new[] { 2, 1 })]
        [InlineData (3, 114, new[] { 1, 2 })]
        // A row takes a tab only while it ends short of the edge: three 54px tabs do not share a 162px row.
        [InlineData (3, 166, new[] { 1, 2 })]
        // Only while the second-last row is wider than the last by more than an eighth of the control.
        [InlineData (13, 420, new[] { 6, 7 })]
        [InlineData (13, 432, new[] { 7, 6 })]
        [InlineData (15, 450, new[] { 8, 7 })]
        // At most one pass per row.
        [InlineData (12, 649, new[] { 9, 3 })]
        [InlineData (16, 600, new[] { 9, 7 })]
        // Each pass reaches one row further up.
        [InlineData (11, 300, new[] { 3, 4, 4 })]
        [InlineData (13, 239, new[] { 4, 3, 3, 3 })]
        [InlineData (14, 354, new[] { 4, 5, 5 })]
        [InlineData (9, 120, new[] { 2, 2, 2, 1, 2 })]
        public void Wrapped_rows_hold_as_many_tabs_as_upstream_s (int count, int width, int[] expected)
        {
            var layout = Measure (true, count, width);

            Assert.Equal (expected, layout.Counts);
        }

        [Fact]
        public void Every_row_ends_at_the_edge_with_the_remainder_handed_out_from_its_first_tab ()
        {
            // Measured: three 54px tabs in a 143px control are one 139px row and a row of 70 and 69.
            var layout = Measure (true, 3, 143);

            Assert.Equal (new[] { 139, 70, 69 }, layout.Tabs.Select (r => r.Width));
            Assert.Equal (new[] { 2, 2, 72 }, layout.Tabs.Select (r => r.Left));
        }

        [Theory]
        [InlineData (0)]
        [InlineData (4)]
        [InlineData (10)]
        public void The_selected_tab_s_row_sits_next_to_the_page_with_the_rest_rotating_above_it (int selected)
        {
            // Measured: eleven 54px tabs in 300px are rows of 3, 4 and 4; the selected one's row moves to the
            // bottom and the rows after it follow from the top, in order.
            var layout = Measure (true, 11, 300, selected: selected);
            var row_of_selected = selected < 3 ? 0 : selected < 7 ? 1 : 2;
            var height = layout.Tabs[0].Height;

            Assert.Equal (layout.RowTops.Max (), layout.SelectedRowTop);
            for (var r = 0; r < 3; r++)
                Assert.Equal (2 + (((r - row_of_selected - 1 + 3) % 3) * height), layout.RowTops[r]);
        }

        [Fact]
        public void An_app_that_never_chose_a_font_keeps_the_plain_wrap ()
        {
            // The theme's tabs keep their own widths and stretch the rows they wrapped into, unbalanced.
            var fill = Measure (false, 11, 300);
            var normal = Measure (false, 11, 300, TabSizeMode.Normal);

            Assert.True (fill.Counts.Count > 1, "the tabs must wrap");
            Assert.Equal (normal.Counts, fill.Counts);
        }
    }
}
