using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The rest of TSM-22: ToolStripItem.Size was a store of its own, separate from the Bounds layout
    // writes. Width, Height and ContentRectangle all read it, so on a normally laid-out strip every
    // one of them answered 0 -- layout wrote Bounds and nothing wrote Size.
    //
    // Size is now a view over Bounds in both directions, which is upstream's shape exactly
    // (ToolStripItem.cs:1807 -- `get => Bounds.Size`, and a setter that writes the size into Bounds
    // through SetBounds).
    //
    // The recorded objection to merging them was that reading Bounds would answer 0 for an item that
    // has never been on a strip. That only held while the two were separate: assigning Size now writes
    // Bounds, so a size set before the item reaches a strip reads back unchanged. Asserted below
    // rather than argued, because it is the reason the merge had been avoided.
    //
    // This also subsumes the GetItemAt fix from #190. With one store,
    // `new Rectangle (Bounds.Location, Size)` and `Bounds` are the same rectangle, so the hit-test
    // cannot disagree with the layout again however it is written. That fix treated the symptom; this
    // is the cause.
    [Collection ("Headless")]
    public class ToolStripItemSizeTests
    {
        private static ToolStrip Built (out Form form)
        {
            HeadlessRenderer.Use ();

            var strip = new ToolStrip { Width = 300, Height = 30 };
            strip.Items.Add (new ToolStripButton { Text = "Alpha" });
            strip.Items.Add (new ToolStripButton { Text = "Beta" });

            form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        [Fact]
        public void A_laid_out_item_reports_the_size_layout_gave_it ()
        {
            // The whole of the remaining defect: all four of these read 0 before, on every item of
            // every strip that was laid out normally.
            using var strip = Built (out var form);

            try {
                foreach (var item in strip.Items.Cast<ToolStripItem> ()) {
                    Assert.NotEqual (Size.Empty, item.Bounds.Size);

                    Assert.Equal (item.Bounds.Size, item.Size);
                    Assert.Equal (item.Bounds.Width, item.Width);
                    Assert.Equal (item.Bounds.Height, item.Height);
                    Assert.Equal (new Rectangle (Point.Empty, item.Bounds.Size), item.ContentRectangle);
                }
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_size_assigned_before_the_item_is_on_a_strip_reads_back ()
        {
            // The objection that kept the two stores apart. It does not survive the merge, because the
            // setter writes Bounds rather than a private field -- so there is nothing to lose.
            using var item = new ToolStripButton { Text = "Alpha" };

            item.Size = new Size (150, 64);

            Assert.Equal (new Size (150, 64), item.Size);
            Assert.Equal (150, item.Width);
            Assert.Equal (64, item.Height);
            Assert.Equal (new Size (150, 64), item.Bounds.Size);
        }

        [Fact]
        public void Assigning_Size_moves_the_box_the_hit_test_uses ()
        {
            // What the split cost in practice, stated as one assertion: the size an application sets
            // and the rectangle the strip hit-tests are now the same rectangle, so they cannot drift.
            using var strip = Built (out var form);

            try {
                var beta = (ToolStripItem)strip.Items[1];
                var origin = beta.Bounds.Location;

                beta.AutoSize = false;
                beta.Size = new Size (40, 18);

                Assert.Equal (new Rectangle (origin, new Size (40, 18)), beta.Bounds);
                Assert.Same (beta, strip.GetItemAt (new Point (origin.X + 20, origin.Y + 9)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Assigning_Size_keeps_the_item_where_it_was ()
        {
            // GUARD: the setter writes a full rectangle, so a careless implementation that passed 0,0
            // for the origin would resize every item onto the left edge. That would satisfy the size
            // assertions above and wreck the strip.
            using var strip = Built (out var form);

            try {
                var beta = (ToolStripItem)strip.Items[1];
                var origin = beta.Bounds.Location;

                Assert.NotEqual (Point.Empty, origin);

                beta.AutoSize = false;
                beta.Size = new Size (40, 18);

                Assert.Equal (origin, beta.Bounds.Location);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Setting_the_same_size_again_is_a_no_op ()
        {
            // GUARD on the short-circuit: it compares against Bounds.Size now rather than a field, and
            // getting that comparison wrong would run a layout pass on every assignment.
            using var item = new ToolStripButton { Text = "Alpha" };

            item.Size = new Size (150, 64);
            item.SetBounds (7, 9, 150, 64);
            item.Size = new Size (150, 64);

            Assert.Equal (new Rectangle (7, 9, 150, 64), item.Bounds);
        }

        [Fact]
        public void Height_and_Width_write_through_to_Bounds ()
        {
            // Both delegate to Size, so both moved with it; neither had its own coverage.
            using var item = new ToolStripButton { Text = "Alpha" };

            item.SetBounds (3, 4, 50, 20);

            item.Height = 64;
            Assert.Equal (new Rectangle (3, 4, 50, 64), item.Bounds);

            item.Width = 150;
            Assert.Equal (new Rectangle (3, 4, 150, 64), item.Bounds);
        }
    }
}
