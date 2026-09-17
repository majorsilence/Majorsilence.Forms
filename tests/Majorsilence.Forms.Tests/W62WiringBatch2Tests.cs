using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 — wiring the candidate list, batch 2.
    //
    //   DataGridView.HorizontalScrollingOffset          RC-6 again: the grid already had a live
    //                                                   horizontal offset and this WinForms-named
    //                                                   property stored a SECOND one nothing read.
    //   DataGridView.FirstDisplayedScrollingColumnIndex answered whatever had last been assigned.
    //
    // TabControl.HotTrack was attempted and REVERTED: the gating is a one-line change, but the default
    // theme gives the hover part no background, so a hovered tab is pixel-identical to an unhovered
    // one and nothing about the property is observable. Colouring the part through CSS did not make it
    // observable either. A wiring that cannot be demonstrated is not a wiring -- recorded in
    // docs/behaviour-gap/lists.md instead.
    [Collection ("Headless")]
    public class W62WiringBatch2Tests
    {
        private static DataGridView Wide (out Form form)
        {
            HeadlessRenderer.Use ();

            var grid = new DataGridView { Width = 200, Height = 120 };

            for (var c = 0; c < 6; c++)
                grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = $"c{c}", Width = 90 });

            grid.Rows.Add ();

            form = new Form { Width = 320, Height = 240 };
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        [Fact]
        public void HorizontalScrollingOffset_is_the_grids_real_offset ()
        {
            using var grid = Wide (out var form);

            try {
                grid.HorizontalScrollingOffset = 120;

                Assert.Equal (120, grid.HorizontalScrollingOffset);
                Assert.Equal (120, grid.HorizontalScrollOffset);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_offset_and_the_scrollbar_agree ()
        {
            // The twin's whole cost: the property and the thing that scrolls were two values. Reading
            // back through the INTERNAL offset is what proves the write reached the grid rather than a
            // second store.
            using var grid = Wide (out var form);

            try {
                Assert.True (grid.HorizontalScrollBarVisible, "premise: six wide columns need a bar");

                grid.HorizontalScrollingOffset = 60;

                Assert.Equal (grid.HorizontalScrollOffset, grid.HorizontalScrollingOffset);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_negative_offset_is_clamped ()
        {
            // GUARD: the scroll position is an offset from the left edge, so a negative would draw the
            // first column off-screen to the right.
            using var grid = Wide (out var form);

            try {
                grid.HorizontalScrollingOffset = -50;

                Assert.Equal (0, grid.HorizontalScrollingOffset);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void FirstDisplayedScrollingColumnIndex_follows_the_offset ()
        {
            using var grid = Wide (out var form);

            try {
                Assert.Equal (0, grid.FirstDisplayedScrollingColumnIndex);

                grid.HorizontalScrollingOffset = grid.LogicalToDeviceUnits (90) + 1;

                Assert.Equal (1, grid.FirstDisplayedScrollingColumnIndex);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Setting_the_first_displayed_column_scrolls_to_it ()
        {
            using var grid = Wide (out var form);

            try {
                var before = grid.HorizontalScrollingOffset;

                // Column 1, not 2: how far the grid can scroll depends on the visible width, and at
                // MF_HEADLESS_SCALE=2 column 2 is past the end -- the getter then honestly reports
                // where it actually got to. Asking for a column the control cannot reach tests the
                // clamp, not the property.
                grid.FirstDisplayedScrollingColumnIndex = 1;

                // The ROUND TRIP, not an exact offset: the scrollbar clamps to what there is left to
                // scroll, so the precise pixel the setter asks for is not always reachable -- and at
                // MF_HEADLESS_SCALE=2 the visible area is smaller, which is where asserting the exact
                // value went wrong. What the property promises is that the column it names ends up
                // first, and that it moved to get there.
                Assert.True (grid.HorizontalScrollingOffset > before,
                    $"did not scroll: {before} -> {grid.HorizontalScrollingOffset}");
                Assert.Equal (1, grid.FirstDisplayedScrollingColumnIndex);
            } finally {
                form.Close ();
            }
        }

    }
}
