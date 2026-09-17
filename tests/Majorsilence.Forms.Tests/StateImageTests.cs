using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LST-60: state images. ListView.StateImageList, ListViewItem.StateImageIndex,
    // TreeView.StateImageList and TreeNode.StateImageIndex were all stored and read by nothing -- four
    // baseline entries, one missing image slot -- so a list or tree using state images showed ordinary
    // check boxes instead.
    //
    // Chosen as the next mechanism because it is genuinely self-contained: a second image in a slot
    // both renderers already draw, with no shared pipeline behind it. (The paragraph model was picked
    // first on that reasoning and turned out not to be -- see TXT-31.)
    [Collection ("Headless")]
    public class StateImageTests
    {
        private static ImageList Images ()
        {
            var images = new ImageList ();
            images.Images.Add ("a", Solid (Color.Magenta));
            images.Images.Add ("b", Solid (Color.Lime));

            return images;
        }

        private static Majorsilence.Forms.Drawing.Bitmap Solid (Color colour)
        {
            var bitmap = new Majorsilence.Forms.Drawing.Bitmap (16, 16);

            using var canvas = Majorsilence.Forms.Drawing.Graphics.FromImage (bitmap);
            canvas.Clear (colour);

            return bitmap;
        }

        // ---------------- ListView

        private static ListView List (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 260, Height = 120, View = View.Details, CheckBoxes = true };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 180 });
            view.Items.Add (new ListViewItem ("one"));

            form = new Form { Width = 360, Height = 220 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        // The pixels of the check-box slot, where a state image replaces the glyph.
        private static string Slot (Control control, Rectangle device)
        {
            using var bitmap = PaintSurface.Render (control);
            var builder = new System.Text.StringBuilder ();

            for (var y = System.Math.Max (0, device.Top); y < device.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, device.Left); x < device.Right && x < bitmap.Width; x++)
                    builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

            return builder.ToString ();
        }

        private static Rectangle ListSlot (ListView view)
        {
            var bounds = view.Items[0].DeviceBounds;
            var size = view.LogicalToDeviceUnits (13);

            return new Rectangle (bounds.Left + view.LogicalToDeviceUnits (2),
                bounds.Top + (bounds.Height - size) / 2, size, size);
        }

        [Fact]
        public void A_state_image_replaces_the_ListView_check_box ()
        {
            using var view = List (out var form);

            try {
                var glyph = Slot (view, ListSlot (view));

                view.StateImageList = Images ();
                view.Items[0].StateImageIndex = 0;

                Assert.NotEqual (glyph, Slot (view, ListSlot (view)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_index_selects_which_state_image ()
        {
            // Both halves have to be read: a renderer that drew image 0 whatever the index said would
            // satisfy the test above and ignore the property that chooses.
            using var view = List (out var form);

            try {
                view.StateImageList = Images ();
                view.Items[0].StateImageIndex = 0;

                var first = Slot (view, ListSlot (view));

                view.Items[0].StateImageIndex = 1;

                Assert.NotEqual (first, Slot (view, ListSlot (view)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Without_a_state_image_the_check_box_is_drawn ()
        {
            // GUARD: state images are an override, not a replacement. Every checked list in existence
            // relies on the glyph, and an index of -1 is the default.
            using var view = List (out var form);

            try {
                var glyph = Slot (view, ListSlot (view));

                view.StateImageList = Images ();

                Assert.Equal (-1, view.Items[0].StateImageIndex);
                Assert.Equal (glyph, Slot (view, ListSlot (view)));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_index_outside_the_list_falls_back_to_the_glyph ()
        {
            // GUARD: an index and a list that disagree is an application mistake, and a paint path is
            // the worst place to throw one.
            using var view = List (out var form);

            try {
                var glyph = Slot (view, ListSlot (view));

                view.StateImageList = Images ();
                view.Items[0].StateImageIndex = 99;

                Assert.Equal (glyph, Slot (view, ListSlot (view)));
            } finally {
                form.Close ();
            }
        }

        // ---------------- TreeView

        private static TreeView Tree (out Form form)
        {
            HeadlessRenderer.Use ();

            var tree = new TreeView { Width = 240, Height = 140, CheckBoxes = true };
            tree.Items.Add ("node");

            form = new Form { Width = 340, Height = 240 };
            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.Render (tree).Dispose ();

            return tree;
        }

        [Fact]
        public void A_state_image_replaces_the_TreeView_check_box ()
        {
            using var tree = Tree (out var form);

            try {
                var slot = tree.CheckBounds (tree.Items[0]);
                var glyph = Slot (tree, slot);

                tree.StateImageList = Images ();
                tree.Items[0].StateImageIndex = 0;

                Assert.NotEqual (glyph, Slot (tree, tree.CheckBounds (tree.Items[0])));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_tree_index_selects_which_image_too ()
        {
            using var tree = Tree (out var form);

            try {
                tree.StateImageList = Images ();
                tree.Items[0].StateImageIndex = 0;

                var first = Slot (tree, tree.CheckBounds (tree.Items[0]));

                tree.Items[0].StateImageIndex = 1;

                Assert.NotEqual (first, Slot (tree, tree.CheckBounds (tree.Items[0])));
            } finally {
                form.Close ();
            }
        }
    }
}
