using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TSM-47. ToolStrip.Items is a facade over the root MenuItemCollection that layout and paint
    // read. It forwarded every insertion as an Add, so `Items.Insert (0, x)` put x FIRST in the
    // facade and LAST on screen -- the right collection order and the wrong visual order, with no
    // error. Insert is how WinForms code positions an item; it is exactly the call migrated code makes.
    //
    // Found by a LocationChanged test in TSM-46 that inserted an item ahead of another and asserted
    // the other moved. It did not, and the probe written to see why found the two lists disagreeing.
    //
    // The invariant every test here checks is the one the bug broke: the facade's order and the laid
    // out order must be the same sequence, after every kind of mutation.
    [Collection ("Headless")]
    public class ToolStripItemsOrderTests
    {
        private static ToolStrip Strip (out Form form, params string[] captions)
        {
            HeadlessRenderer.Use ();

            var strip = new ToolStrip { Width = 400, Height = 30, GripVisible = false };

            foreach (var c in captions)
                strip.Items.Add (new ToolStripButton { Text = c });

            form = new Form { Width = 500, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        // The order layout actually produced, read off the items' x positions after a paint.
        private static string Painted (ToolStrip strip)
        {
            strip.PerformLayout ();
            PaintSurface.Render (strip).Dispose ();

            return string.Join (",", strip.Items.OrderBy (i => i.Bounds.Left).Select (i => i.Text));
        }

        private static string Facade (ToolStrip strip) => string.Join (",", strip.Items.Select (i => i.Text));

        [Fact]
        public void Insert_at_the_front_puts_the_item_first_on_screen ()
        {
            var strip = Strip (out var form, "Open");

            using (form) {
                strip.Items.Insert (0, new ToolStripButton { Text = "New" });

                Assert.Equal ("New,Open", Facade (strip));
                Assert.Equal ("New,Open", Painted (strip));
            }
        }

        [Fact]
        public void Insert_in_the_middle_lands_in_the_middle ()
        {
            var strip = Strip (out var form, "A", "C");

            using (form) {
                strip.Items.Insert (1, new ToolStripButton { Text = "B" });

                Assert.Equal ("A,B,C", Facade (strip));
                Assert.Equal ("A,B,C", Painted (strip));
            }
        }

        // The indexer is SetItem: the replacement must take the old item's place, not the end.
        [Fact]
        public void Replacing_by_index_keeps_the_position ()
        {
            var strip = Strip (out var form, "A", "B", "C");

            using (form) {
                strip.Items[1] = new ToolStripButton { Text = "X" };

                Assert.Equal ("A,X,C", Facade (strip));
                Assert.Equal ("A,X,C", Painted (strip));
            }
        }

        [Fact]
        public void Remove_keeps_the_rest_in_order ()
        {
            var strip = Strip (out var form, "A", "B", "C");

            using (form) {
                strip.Items.RemoveAt (1);

                Assert.Equal ("A,C", Facade (strip));
                Assert.Equal ("A,C", Painted (strip));
            }
        }

        // Add is the path that always worked; it must still append.
        [Fact]
        public void Add_still_appends ()
        {
            var strip = Strip (out var form, "A");

            using (form) {
                strip.Items.Add (new ToolStripButton { Text = "B" });

                Assert.Equal ("A,B", Painted (strip));
            }
        }

        // The two lists, compared directly rather than through paint: the root collection is what
        // layout reads, and it has to be the same SEQUENCE as the facade, not just the same set.
        [Fact]
        public void The_facade_and_the_root_collection_are_the_same_sequence ()
        {
            var strip = Strip (out var form, "A", "C");

            using (form) {
                strip.Items.Insert (1, new ToolStripButton { Text = "B" });
                strip.Items[0] = new ToolStripButton { Text = "Z" };
                strip.Items.Insert (0, new ToolStripButton { Text = "Y" });

                var facade = strip.Items.Select (i => i.Text).ToArray ();
                var root = strip.RootItems.Select (i => i.Text).ToArray ();

                Assert.Equal (facade, root);
                Assert.Equal (new[] { "Y", "Z", "B", "C" }, facade);
            }
        }
    }
}
