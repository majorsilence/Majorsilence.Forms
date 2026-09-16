using System.Linq;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, a wider sweep across four controls. Seven entries, each a property the control stored and
    // read nowhere:
    //
    //   TreeView.HideSelection      + a WRONG DEFAULT (false; upstream's TreeView is true -- and
    //                                 upstream's ListView is false, so the siblings really do differ)
    //   TreeView.FullRowSelect      every tree highlighted the whole row, which is what TRUE means
    //                                 while the property defaults to false
    //   TreeView.PathSeparator      FullPath hard-coded "\\", so FindNodeByFullPath could not match a
    //                                 path the application had built with its own separator
    //   ListBox.Sorted              a sorted list box came out in insertion order
    //   ListBox.ScrollAlwaysVisible a second store beside ScrollbarAlwaysVisible, which is the one the
    //                                 scrollbar logic reads (RC-6: a private twin kept alongside)
    //   SplitContainer.IsSplitterFixed  a locked splitter dragged like any other
    //   ListViewItem.UseItemStyleForSubItems  the sub-item's own colour always won, which is the
    //                                 FALSE behaviour applied to every list
    [Collection ("Headless")]
    public class W62SweepTests
    {
        // ---------------- TreeView.HideSelection

        [Fact]
        public void TreeView_HideSelection_defaults_to_true ()
        {
            // Upstream's TreeView carries [DefaultValue(true)] and sets the flag in its constructor --
            // the opposite of ListView, which is [DefaultValue(false)] and does not. Both were checked
            // against the upstream source rather than assumed, because they look like they should match.
            using var tree = new TreeView ();
            using var list = new ListView ();

            Assert.True (tree.HideSelection);
            Assert.False (list.HideSelection);
        }

        private static TreeView Tree (out Form form)
        {
            HeadlessRenderer.Use ();

            var tree = new TreeView { Width = 200, Height = 160 };
            tree.Items.Add ("Documents");
            tree.Items.Add ("Pictures");

            form = new Form { Width = 300, Height = 260 };
            form.Controls.Add (tree);
            form.Show ();

            return tree;
        }

        // The pixels of the first node's row, as a comparable string.
        private static string Row (TreeView tree)
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
        public void TreeView_HideSelection_hides_the_band_when_focus_is_elsewhere ()
        {
            using var tree = Tree (out var form);

            try {
                tree.SelectedNode = tree.Items[0];
                tree.HideSelection = false;

                var shown = Row (tree);

                tree.HideSelection = true;

                // PREMISE: the tree really is unfocused, so HideSelection is the only thing that
                // changed between the two renders.
                Assert.False (tree.Focused);
                Assert.NotEqual (shown, Row (tree));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void TreeView_a_focused_tree_keeps_its_band ()
        {
            using var tree = Tree (out var form);

            try {
                tree.SelectedNode = tree.Items[0];
                tree.HideSelection = true;

                var unfocused = Row (tree);

                tree.Focus ();

                Assert.True (tree.Focused);
                Assert.NotEqual (unfocused, Row (tree));
            } finally {
                form.Close ();
            }
        }

        // ---------------- TreeView.FullRowSelect

        [Fact]
        public void TreeView_FullRowSelect_widens_the_band ()
        {
            using var tree = Tree (out var form);

            try {
                tree.HideSelection = false;
                tree.SelectedNode = tree.Items[0];

                var label_only = Row (tree);

                tree.FullRowSelect = true;

                Assert.NotEqual (label_only, Row (tree));
            } finally {
                form.Close ();
            }
        }

        // ---------------- TreeView.PathSeparator

        [Fact]
        public void TreeView_FullPath_uses_the_separator_it_was_given ()
        {
            using var tree = new TreeView { PathSeparator = "/" };

            var root = tree.Items.Add ("root");
            var child = root.Items.Add ("child");
            var grandchild = child.Items.Add ("leaf");

            Assert.Equal ("root/child/leaf", grandchild.FullPath);
        }

        [Fact]
        public void TreeView_the_default_separator_is_still_a_backslash ()
        {
            // GUARD: the fallback is what every existing tree relies on, and FindNodeByFullPath
            // compares against whatever FullPath produces -- so changing the default silently would
            // break lookups that work today.
            using var tree = new TreeView ();

            var root = tree.Items.Add ("root");
            var child = root.Items.Add ("child");
            var grandchild = child.Items.Add ("leaf");

            Assert.Equal ("root\\child\\leaf", grandchild.FullPath);
        }

        [Fact]
        public void TreeView_a_node_can_be_found_by_the_path_it_reports ()
        {
            // The round trip the property exists for: an application builds a path with its own
            // separator and looks the node up again.
            using var tree = new TreeView { PathSeparator = "/" };

            var root = tree.Items.Add ("root");
            var child = root.Items.Add ("child");
            var leaf = child.Items.Add ("leaf");

            Assert.Same (leaf, tree.FindNodeByFullPath ("root/child/leaf"));
        }

        // ---------------- ListBox.Sorted

        [Fact]
        public void ListBox_Sorted_orders_the_items ()
        {
            using var box = new ListBox ();
            box.Items.Add ("pear");
            box.Items.Add ("apple");
            box.Items.Add ("cherry");

            box.Sorted = true;

            Assert.Equal (new[] { "apple", "cherry", "pear" }, box.Items.Cast<object> ().Select (i => i.ToString ()).ToArray ());
        }

        [Fact]
        public void ListBox_Sorted_keeps_the_selected_item ()
        {
            // Sorting moves rows, so an index-based selection would now point at a different item.
            // Preserved by VALUE, which is what the user still has selected.
            using var box = new ListBox ();
            box.Items.Add ("pear");
            box.Items.Add ("apple");
            box.SelectedItem = "pear";

            box.Sorted = true;

            Assert.Equal ("pear", box.SelectedItem);
            Assert.Equal (1, box.SelectedIndex);
        }

        [Fact]
        public void ListBox_unsorted_keeps_insertion_order ()
        {
            // GUARD: sorting is behind the flag, so every list box that never sets it is untouched.
            using var box = new ListBox ();
            box.Items.Add ("pear");
            box.Items.Add ("apple");

            Assert.Equal (new[] { "pear", "apple" }, box.Items.Cast<object> ().Select (i => i.ToString ()).ToArray ());
        }

        // ---------------- ListBox.ScrollAlwaysVisible

        [Fact]
        public void ListBox_ScrollAlwaysVisible_is_the_same_value_as_its_twin ()
        {
            // RC-6: it was a second store beside ScrollbarAlwaysVisible, and the scrollbar logic reads
            // that one -- so the property WinForms code actually writes did nothing. One value now, so
            // which name a caller uses cannot change the answer.
            using var box = new ListBox ();

            box.ScrollAlwaysVisible = true;
            Assert.True (box.ScrollbarAlwaysVisible);

            box.ScrollbarAlwaysVisible = false;
            Assert.False (box.ScrollAlwaysVisible);
        }

        [Fact]
        public void ListBox_ScrollAlwaysVisible_shows_the_bar_for_a_short_list ()
        {
            // What the property is for, end to end: two items in a tall box need no scrollbar, and
            // asking for one anyway produces one.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 300, Height = 300 };
            var box = new ListBox { Width = 150, Height = 200 };
            box.Items.Add ("one");
            box.Items.Add ("two");
            form.Controls.Add (box);
            form.Show ();
            PaintSurface.Render (box).Dispose ();

            try {
                // Observed through the item rectangle rather than the private scrollbar: a visible bar
                // takes width out of the item area, which is the consequence a user and an application
                // can both see.
                var without = box.GetItemRectangle (0).Width;

                box.ScrollAlwaysVisible = true;
                PaintSurface.Render (box).Dispose ();

                Assert.True (box.GetItemRectangle (0).Width < without,
                    $"the bar took no width: {without} -> {box.GetItemRectangle (0).Width}");
            } finally {
                form.Close ();
            }
        }

        // ---------------- SplitContainer.IsSplitterFixed

        [Fact]
        public void SplitContainer_a_fixed_splitter_does_not_move ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var split = new SplitContainer { Width = 300, Height = 200 };
            form.Controls.Add (split);
            form.Show ();

            try {
                var before = split.SplitterDistance;

                split.IsSplitterFixed = true;
                split.DriveSplitterDrag (new Point (-30, 0));

                Assert.Equal (before, split.SplitterDistance);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void SplitContainer_an_ordinary_splitter_still_moves ()
        {
            // PREMISE: without this, "did not move" is also what a splitter that cannot move at all
            // looks like, and the test above would pass against a broken drag path.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var split = new SplitContainer { Width = 300, Height = 200 };
            form.Controls.Add (split);
            form.Show ();

            try {
                var before = split.SplitterDistance;

                split.DriveSplitterDrag (new Point (-30, 0));

                Assert.NotEqual (before, split.SplitterDistance);
            } finally {
                form.Close ();
            }
        }

        // ---------------- ListViewItem.UseItemStyleForSubItems

        private static ListView Details (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 300, Height = 120, View = View.Details };
            view.Columns.Add (new ColumnHeader { Text = "A", Width = 100 });
            view.Columns.Add (new ColumnHeader { Text = "B", Width = 100 });

            var item = new ListViewItem ("first") { ForeColor = Color.Blue };
            item.SubItems.Add (new ListViewItem.ListViewSubItem { Text = "second", ForeColor = Color.Red });
            view.Items.Add (item);

            form = new Form { Width = 400, Height = 220 };
            form.Controls.Add (view);
            form.Show ();

            return view;
        }

        [Fact]
        public void ListViewItem_UseItemStyleForSubItems_decides_whose_colour_wins ()
        {
            using var view = Details (out var form);

            try {
                var item = view.Items[0];

                // True (the WinForms default): the sub-item's own red is ignored.
                item.UseItemStyleForSubItems = true;
                var unified = SubItemRow (view);

                item.UseItemStyleForSubItems = false;

                Assert.NotEqual (unified, SubItemRow (view));
            } finally {
                form.Close ();
            }
        }

        // The pixels of the second column of the first row, where only the sub-item draws.
        private static string SubItemRow (ListView view)
        {
            using var bitmap = PaintSurface.Render (view);

            // The whole row: the sub-item's text does not start at a fraction of the row that can be
            // computed from the bounds alone, and guessing one samples background in both renders --
            // which passes for the wrong reason. Only the sub-item's colour changes between them.
            var bounds = view.Items[0].DeviceBounds;
            var builder = new System.Text.StringBuilder ();

            for (var y = System.Math.Max (0, bounds.Top); y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, bounds.Left); x < bounds.Right && x < bitmap.Width; x++)
                    builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

            return builder.ToString ();
        }
    }
}
