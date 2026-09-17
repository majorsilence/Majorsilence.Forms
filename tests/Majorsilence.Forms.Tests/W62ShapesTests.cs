using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 continued, still working by shape.
    //
    //   TreeView.ImageKey / SelectedImageKey   the renderer's own remark says each image "falls back to
    //                                          the tree's own default". That was true of the INDEX
    //                                          chain and had never been implemented for the KEY chain,
    //                                          so a tree naming its default icon by key showed none.
    //   ListView.Activation                    only a double click ever activated, so a list set to
    //                                          OneClick -- the whole point of the property -- behaved
    //                                          exactly like a Standard one.
    [Collection ("Headless")]
    public class W62ShapesTests
    {
        // ---------------- the TreeView image-key fallback

        private static TreeView WithImages (out Form form)
        {
            HeadlessRenderer.Use ();

            var images = new ImageList ();
            images.Images.Add ("plain", NewBitmap (Color.Red));
            images.Images.Add ("chosen", NewBitmap (Color.Lime));

            var tree = new TreeView { Width = 200, Height = 120, ImageList = images, ShowItemImages = true };
            tree.Items.Add ("node");

            form = new Form { Width = 300, Height = 220 };
            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.Render (tree).Dispose ();

            return tree;
        }

        private static Majorsilence.Forms.Drawing.Bitmap NewBitmap (Color colour)
        {
            var bitmap = new Majorsilence.Forms.Drawing.Bitmap (16, 16);

            using var canvas = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
            canvas.Clear (colour);

            return bitmap;
        }

        private static string NodeRow (TreeView tree)
        {
            using var bitmap = PaintSurface.Render (tree);

            var bounds = tree.Items[0].Bounds;
            var builder = new System.Text.StringBuilder ();

            for (var y = System.Math.Max (0, bounds.Top); y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, bounds.Left); x < bounds.Right && x < bitmap.Width; x++)
                    builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

            return builder.ToString ();
        }

        [Fact]
        public void The_tree_level_ImageKey_is_used_when_the_node_names_none ()
        {
            using var tree = WithImages (out var form);

            try {
                var without = NodeRow (tree);

                tree.ImageKey = "chosen";

                Assert.NotEqual (without, NodeRow (tree));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_nodes_own_ImageKey_still_wins ()
        {
            // GUARD, and the order the remark describes: the control's key is a FALLBACK, not an
            // override. An implementation that preferred it would ignore every per-node icon.
            using var tree = WithImages (out var form);

            try {
                tree.Items[0].ImageKey = "plain";

                var node_chose = NodeRow (tree);

                tree.ImageKey = "chosen";

                Assert.Equal (node_chose, NodeRow (tree));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_selected_key_applies_to_the_selected_node ()
        {
            using var tree = WithImages (out var form);

            try {
                // The node is selected BEFORE the capture. Selecting it afterwards would change the
                // row through the selection band alone, so the comparison would pass whether or not
                // the selected-image key was ever read -- which is exactly what it did at first.
                tree.ImageKey = "plain";
                tree.HideSelection = false;
                tree.SelectedNode = tree.Items[0];

                var before = NodeRow (tree);

                tree.SelectedImageKey = "chosen";

                Assert.NotEqual (before, NodeRow (tree));
            } finally {
                form.Close ();
            }
        }

        // ---------------- ListView.Activation

        private static ListView List (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 260, Height = 160, View = View.Details };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });
            view.Items.Add (new ListViewItem ("one"));
            view.Items.Add (new ListViewItem ("two"));

            form = new Form { Width = 360, Height = 260 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        private static Point Centre (ListView view, int index)
        {
            var bounds = view.Items[index].Bounds;

            return new Point (bounds.Left + 20, bounds.Top + bounds.Height / 2);
        }

        [Fact]
        public void Standard_activation_ignores_a_single_click ()
        {
            // PREMISE for the OneClick test: the default really does NOT activate on one click, so a
            // OneClick list activating is the property being read rather than a click always doing it.
            using var view = List (out var form);

            try {
                var activated = 0;
                view.ItemActivate += (_, _) => activated++;

                view.DriveClick (Centre (view, 0));

                Assert.Equal (ItemActivation.Standard, view.Activation);
                Assert.Equal (0, activated);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void OneClick_activation_fires_on_a_single_click ()
        {
            using var view = List (out var form);

            try {
                view.Activation = ItemActivation.OneClick;

                var activated = 0;
                view.ItemActivate += (_, _) => activated++;

                view.DriveClick (Centre (view, 0));

                Assert.Equal (1, activated);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_modified_click_extends_the_selection_rather_than_activating ()
        {
            // Ctrl- and Shift-click are selection gestures. Activating on them would fire an "open
            // this item" event every time a user built a multiple selection.
            using var view = List (out var form);
            view.MultiSelect = true;

            try {
                view.Activation = ItemActivation.OneClick;

                var activated = 0;
                view.ItemActivate += (_, _) => activated++;

                view.DriveClick (Centre (view, 0), Keys.Control);
                view.DriveClick (Centre (view, 1), Keys.Shift);

                Assert.Equal (0, activated);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_click_on_empty_space_activates_nothing ()
        {
            // GUARD: the activation sits after the hit test, so a miss must stay a miss.
            using var view = List (out var form);

            try {
                view.Activation = ItemActivation.OneClick;

                var activated = 0;
                view.ItemActivate += (_, _) => activated++;

                view.DriveClick (new Point (10, view.Height - 4));

                Assert.Equal (0, activated);
            } finally {
                form.Close ();
            }
        }
    }
}
