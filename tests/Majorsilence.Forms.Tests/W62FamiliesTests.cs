using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, worked by FAMILY rather than by control -- which is what the triage said to do once the
    // same shape had turned up three times.
    //
    //   TreeView.Scrollable   the FIFTH control found with a scrollbar-policy property sitting beside
    //                         scrollbar logic that never consults it, after TextBox (TXT-26),
    //                         RichTextBox (TXT-29), ListBox's twin and DataGridView (DGV-40).
    //   ListBox.HideSelection the FOURTH HideSelection found dead. Not an upstream ListBox member, and
    //                         it defaulted to true while nothing read it -- so the property described
    //                         behaviour the control did not have.
    [Collection ("Headless")]
    public class W62FamiliesTests
    {
        // ---------------- TreeView.Scrollable

        private static TreeView Tall (out Form form)
        {
            HeadlessRenderer.Use ();

            var tree = new TreeView { Width = 180, Height = 80 };

            for (var i = 0; i < 30; i++)
                tree.Items.Add ($"node {i}");

            form = new Form { Width = 300, Height = 200 };
            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.Render (tree).Dispose ();

            return tree;
        }

        [Fact]
        public void A_tall_tree_has_a_scrollbar ()
        {
            // PREMISE: without it, "no bar" is also what a tree that cannot scroll at all looks like.
            using var tree = Tall (out var form);

            try {
                Assert.True (tree.VerticalScrollBarVisible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Scrollable_false_takes_the_bar_away ()
        {
            using var tree = Tall (out var form);

            try {
                tree.Scrollable = false;
                PaintSurface.Render (tree).Dispose ();

                Assert.False (tree.VerticalScrollBarVisible);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Scrollable_allows_but_does_not_force ()
        {
            // GUARD: the property says the tree MAY scroll; the item count still says whether it NEEDS
            // to. An implementation that showed a bar because it was permitted would put one on every
            // tree in existence, since true is the default.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 300, Height = 200 };
            var tree = new TreeView { Width = 180, Height = 120, Scrollable = true };
            tree.Items.Add ("only");
            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.Render (tree).Dispose ();

            try {
                Assert.False (tree.VerticalScrollBarVisible);
            } finally {
                form.Close ();
            }
        }

        // ---------------- ListBox.HideSelection

        [Fact]
        public void ListBox_HideSelection_defaults_to_showing_the_selection ()
        {
            // Corrected from true. It is not an upstream ListBox member, so there is no default to
            // match -- and it defaulted to true while nothing read it, which meant the property
            // described behaviour the control did not have. Honouring that default when wiring it
            // would have taken the highlight off every unfocused list in existence; false changes
            // nothing until an application asks.
            using var box = new ListBox ();

            Assert.False (box.HideSelection);
        }

        private static ListBox Populated (out Form form)
        {
            HeadlessRenderer.Use ();

            var box = new ListBox { Width = 150, Height = 100 };
            box.Items.Add ("alpha");
            box.Items.Add ("beta");
            box.SelectedIndex = 0;

            form = new Form { Width = 250, Height = 200 };
            form.Controls.Add (box);
            form.Show ();

            return box;
        }

        private static string FirstRow (ListBox box)
        {
            using var bitmap = PaintSurface.Render (box);

            var bounds = box.GetItemRectangleDevice (0);
            var builder = new System.Text.StringBuilder ();

            for (var y = System.Math.Max (0, bounds.Top); y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, bounds.Left); x < bounds.Right && x < bitmap.Width; x++)
                    builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

            return builder.ToString ();
        }

        [Fact]
        public void ListBox_HideSelection_drops_the_band_when_focus_is_elsewhere ()
        {
            using var box = Populated (out var form);

            try {
                var shown = FirstRow (box);

                box.HideSelection = true;

                // PREMISE: the box really is unfocused, so HideSelection is the only thing that
                // changed between the two renders.
                Assert.False (box.Focused);
                Assert.NotEqual (shown, FirstRow (box));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ListBox_a_focused_box_keeps_its_band ()
        {
            using var box = Populated (out var form);

            try {
                box.HideSelection = true;

                var unfocused = FirstRow (box);

                box.Focus ();

                Assert.True (box.Focused);
                Assert.NotEqual (unfocused, FirstRow (box));
            } finally {
                form.Close ();
            }
        }
    }
}
