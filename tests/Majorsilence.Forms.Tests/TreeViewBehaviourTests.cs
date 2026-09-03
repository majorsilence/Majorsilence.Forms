using System.Collections;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.9 (findings LST-05 P0, LST-11, LST-21, LST-22, LST-23, LST-24, LST-25, LST-26): the TreeView
    // reported a synthetic root as the selection, hit-tested against a layout it did not draw, showed
    // none of the images/checkboxes/fonts it was told about, and dropped most of its event surface.
    [Collection ("Headless")]
    public class TreeViewBehaviourTests
    {
        private static TreeView Tree (params string[] roots)
        {
            HeadlessRenderer.Use ();
            var tree = new TreeView { Width = 240, Height = 240 };

            foreach (var text in roots)
                tree.Nodes.Add (text);

            return tree;
        }

        // A subclass, because a down/up pair does not synthesise MouseClick: the finding's own
        // suggested test says "simulate OnMouseClick", and that is the entry point the tree acts on.
        private sealed class ClickableTree : TreeView
        {
            internal void ClickAt (int x, int y, MouseButtons button)
                => OnMouseClick (new MouseEventArgs (button, 1, x, y, 0));
        }

        private static ClickableTree ClickableWith (params string[] roots)
        {
            HeadlessRenderer.Use ();
            var tree = new ClickableTree { Width = 240, Height = 240 };

            foreach (var text in roots)
                tree.Nodes.Add (text);

            return tree;
        }

        // ── LST-05: null means nothing selected ─────────────────────────────────────────────────

        [Fact]
        public void A_fresh_tree_has_no_selected_node ()
        {
            using var tree = Tree ("a", "b");

            // This returned the hidden synthetic root: non-null, Text == "", and its Nodes were the
            // tree's own top-level nodes -- so `if (tree.SelectedNode == null) return;` never fired
            // and `SelectedNode.Nodes.Add (...)` added a top-level node.
            Assert.Null (tree.SelectedNode);
        }

        [Fact]
        public void Assigning_null_clears_the_selection ()
        {
            using var tree = Tree ("a", "b");
            tree.SelectedNode = tree.Nodes[1];

            Assert.Same (tree.Nodes[1], tree.SelectedNode);

            tree.SelectedNode = null;

            Assert.Null (tree.SelectedNode);
        }

        [Fact]
        public void Removing_the_selected_node_clears_the_selection ()
        {
            using var tree = Tree ("a", "b");
            tree.SelectedNode = tree.Nodes[1];

            tree.Nodes[1].Remove ();

            // The stale node used to still be reported, one no longer in the tree at all.
            Assert.Null (tree.SelectedNode);
        }

        [Fact]
        public void Clearing_the_nodes_clears_the_selection ()
        {
            using var tree = Tree ("a", "b");
            var child = tree.Nodes[0].Nodes.Add ("child");
            tree.SelectedNode = child;

            tree.Nodes.Clear ();

            // Also covers the subtree case: the selection was a descendant of what was removed.
            Assert.Null (tree.SelectedNode);
        }

        // ── LST-21 / LST-22: the mouse ──────────────────────────────────────────────────────────

        [Fact]
        public void GetNodeAt_agrees_with_the_control_itself ()
        {
            // FOUR nodes, probing the LAST row. With three and a middle row the test passes against
            // the old algorithm by coincidence: index 1 is the fixed point of a sibling reversal, and
            // the row-height error (a stored 20 against a measured ~24) does not reach far enough to
            // change the answer that early. Both defects show at the bottom of a longer list.
            using var tree = Tree ("a", "b", "c", "d");
            tree.LayoutItems ();

            var row = tree.Nodes[3].Bounds;
            var logical = new Point (
                row.Left + 8, (int)((row.Top + row.Height / 2.0) / tree.ScaleFactor.Height));

            // Naming the right node, not merely agreeing with the sibling method -- delegation makes
            // that agreement true by construction and proves nothing. GetNodeAt walked a REVERSED
            // traversal and compared against rectangles synthesised from the stored ItemHeight (20)
            // rather than the real row height, so it returned a different node from the one the tree
            // itself selected on the same click.
            Assert.Same (tree.Nodes[3], tree.GetNodeAt (logical.X, logical.Y));
            Assert.Same (tree.GetItemAtLocation (logical), tree.GetNodeAt (logical.X, logical.Y));
        }

        [Fact]
        public void A_right_click_reports_NodeMouseClick ()
        {
            using var tree = ClickableWith ("a", "b");
            tree.LayoutItems ();

            TreeNodeMouseClickEventArgs? seen = null;
            tree.NodeMouseClick += (_, e) => seen = e;

            var row = tree.Nodes[0].Bounds;
            tree.ClickAt (row.Left + 30, row.Top + 2, MouseButtons.Right);

            // The standard context-menu pattern hangs off this, and the right button returned above
            // the only raise.
            Assert.NotNull (seen);
            Assert.Equal (MouseButtons.Right, seen!.Button);
            Assert.Same (tree.Nodes[0], seen.Node);
        }

        // ── LST-23: the event set ───────────────────────────────────────────────────────────────

        [Fact]
        public void BeforeSelect_can_veto_a_selection ()
        {
            using var tree = Tree ("a", "b");
            tree.BeforeSelect += (_, e) => e.Cancel = true;

            tree.SelectedNode = tree.Nodes[1];

            Assert.Null (tree.SelectedNode);
        }

        [Fact]
        public void A_programmatic_expand_and_collapse_are_announced ()
        {
            using var tree = Tree ("a");
            tree.Nodes[0].Nodes.Add ("child");

            var expands = 0;
            var collapses = 0;
            var before_collapse = 0;
            tree.AfterExpand += (_, _) => expands++;
            tree.BeforeCollapse += (_, _) => before_collapse++;
            tree.AfterCollapse += (_, _) => collapses++;

            tree.Nodes[0].Expand ();
            tree.Nodes[0].Collapse ();

            // AfterExpand fired only from a glyph click, and BeforeCollapse from nowhere at all -- so
            // a lazy-loading tree had no completion hook on the programmatic path.
            Assert.Equal (1, expands);
            Assert.Equal (1, before_collapse);
            Assert.Equal (1, collapses);
        }

        [Fact]
        public void AfterSelect_reports_the_action_that_caused_it ()
        {
            using var tree = ClickableWith ("a", "b");
            tree.LayoutItems ();

            var actions = new System.Collections.Generic.List<TreeViewAction> ();
            tree.AfterSelect += (_, e) => actions.Add (e.Action);

            tree.SelectedNode = tree.Nodes[0];                          // programmatic
            var row = tree.Nodes[1].Bounds;
            tree.ClickAt (row.Left + 30, row.Top + 2, MouseButtons.Left); // mouse

            // Everything used to report ByMouse, keyboard and programmatic included.
            Assert.Equal (new[] { TreeViewAction.Unknown, TreeViewAction.ByMouse }, actions.ToArray ());
        }

        // ── LST-24: check boxes ─────────────────────────────────────────────────────────────────

        [Fact]
        public void BeforeCheck_can_veto_and_AfterCheck_reports ()
        {
            using var tree = Tree ("a", "b");
            tree.CheckBoxes = true;

            var after = 0;
            tree.AfterCheck += (_, _) => after++;

            tree.Nodes[0].Checked = true;

            Assert.True (tree.Nodes[0].Checked);
            Assert.Equal (1, after);

            tree.BeforeCheck += (_, e) => e.Cancel = true;
            tree.Nodes[1].Checked = true;

            Assert.False (tree.Nodes[1].Checked);
            Assert.Equal (1, after);
        }

        [Fact]
        public void A_click_on_the_check_box_toggles_without_moving_the_selection ()
        {
            using var tree = ClickableWith ("a", "b");
            tree.CheckBoxes = true;
            tree.LayoutItems ();

            var box = tree.CheckBounds (tree.Nodes[1]);
            tree.ClickAt (box.Left + box.Width / 2, box.Top + box.Height / 2, MouseButtons.Left);

            Assert.True (tree.Nodes[1].Checked);
            Assert.Null (tree.SelectedNode);
        }

        [Fact]
        public void A_checked_node_draws_a_glyph_where_an_unchecked_tree_draws_nothing ()
        {
            // The visual half. Compared against the SAME tree with CheckBoxes off, sampling the
            // check box's own rectangle -- a node's text and indent are drawn either way, so only
            // that rectangle is evidence of a box.
            using var form = new Form { Size = new Size (320, 240) };
            form.UseSystemDecorations = false;

            var checkedTree = Tree ("");
            checkedTree.CheckBoxes = true;
            checkedTree.Left = 0;
            checkedTree.Top = 0;
            checkedTree.Nodes[0].Checked = true;

            var plainTree = Tree ("");
            plainTree.Left = 0;
            plainTree.Top = 0;

            form.Controls.Add (checkedTree);
            form.Controls.Add (plainTree);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            checkedTree.LayoutItems ();
            var box = checkedTree.CheckBounds (checkedTree.Nodes[0]);

            Assert.True (InkIn (checkedTree, box) > 0, "a checked node should draw a check box");
            Assert.Equal (0, InkIn (plainTree, box));

            form.Close ();
        }

        // ── LST-25: ImageList images ────────────────────────────────────────────────────────────

        [Fact]
        public void A_node_draws_its_ImageList_image_by_index ()
        {
            using var form = new Form { Size = new Size (320, 240) };
            form.UseSystemDecorations = false;

            var images = new ImageList ();
            var red = new SkiaSharp.SKBitmap (16, 16);
            using (var canvas = new SkiaSharp.SKCanvas (red))
                canvas.Clear (new SkiaSharp.SKColor (255, 0, 0));
            images.Images.Add (red);

            var tree = Tree ("");
            tree.Left = 0;
            tree.Top = 0;
            tree.ImageList = images;
            tree.Nodes[0].ImageIndex = 0;

            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            // Only TreeNode.Image was ever read, so every explorer-style tree built the WinForms way
            // -- an ImageList plus per-node ImageIndex -- showed no icons at all.
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap)buffer.Invoke (tree, null)!;
            var reds = 0;

            for (var x = 0; x < System.Math.Min (120, bitmap.Width); x++)
                for (var y = 0; y < System.Math.Min (tree.ScaledItemHeight + 4, bitmap.Height); y++) {
                    var pixel = bitmap.GetPixel (x, y);

                    if (pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60)
                        reds++;
                }

            Assert.True (reds > 0, "the node's ImageList image should be drawn");

            form.Close ();
        }

        // ── LST-26: per-node styling and metrics ────────────────────────────────────────────────

        [Fact]
        public void ItemHeight_drives_the_row_height ()
        {
            using var tree = Tree ("a", "b");

            tree.ItemHeight = 40;
            tree.LayoutItems ();

            // It was stored while the renderer measured every row from the node's preferred size, so
            // taller rows for touch were silently ignored.
            Assert.Equal (tree.LogicalToDeviceUnits (40), tree.Nodes[1].Bounds.Top - tree.Nodes[0].Bounds.Top);
        }

        [Fact]
        public void ShowPlusMinus_is_the_same_knob_as_ShowDropdownGlyph ()
        {
            using var tree = Tree ("a");

            tree.ShowPlusMinus = false;

            // Two properties for one piece of state: setting the WinForms-named one changed nothing.
            Assert.False (tree.ShowDropdownGlyph);

            tree.ShowDropdownGlyph = true;

            Assert.True (tree.ShowPlusMinus);
        }

        [Fact]
        public void A_nodes_ForeColor_is_used_when_drawing_it ()
        {
            using var form = new Form { Size = new Size (320, 240) };
            form.UseSystemDecorations = false;

            var tree = Tree ("coloured");
            tree.Left = 0;
            tree.Top = 0;
            tree.Nodes[0].ForeColor = Color.Red;

            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap)buffer.Invoke (tree, null)!;
            var reds = 0;

            for (var x = 0; x < System.Math.Min (200, bitmap.Width); x++)
                for (var y = 0; y < System.Math.Min (tree.ScaledItemHeight + 4, bitmap.Height); y++) {
                    var pixel = bitmap.GetPixel (x, y);

                    if (pixel.Red > 150 && pixel.Green < 90 && pixel.Blue < 90)
                        reds++;
                }

            Assert.True (reds > 0, "the node's own ForeColor should be used for its text");

            form.Close ();
        }

        // ── LST-11: sorting ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Sorted_orders_the_nodes_by_text ()
        {
            using var tree = Tree ("b", "c", "a");

            tree.Sorted = true;

            Assert.Equal (new[] { "a", "b", "c" }, tree.Nodes.Select (n => n.Text));
        }

        [Fact]
        public void TreeViewNodeSorter_sorts_every_level ()
        {
            using var tree = Tree ("b", "a");
            tree.Nodes[0].Nodes.Add ("z");
            tree.Nodes[0].Nodes.Add ("y");

            tree.TreeViewNodeSorter = new TextSorter ();

            Assert.Equal (new[] { "a", "b" }, tree.Nodes.Select (n => n.Text));
            Assert.Equal (new[] { "y", "z" }, tree.Nodes[1].Nodes.Select (n => n.Text));
        }

        private sealed class TextSorter : IComparer
        {
            public int Compare (object? x, object? y)
                => string.Compare ((x as TreeNode)?.Text, (y as TreeNode)?.Text, System.StringComparison.Ordinal);
        }

        private static int InkIn (Control control, Rectangle area)
        {
            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap)buffer.Invoke (control, null)!;
            var background = bitmap.GetPixel (System.Math.Min (area.Right + 60, bitmap.Width - 1), bitmap.Height - 2);
            var ink = 0;

            for (var x = area.Left; x < System.Math.Min (area.Right, bitmap.Width); x++)
                for (var y = area.Top; y < System.Math.Min (area.Bottom, bitmap.Height); y++) {
                    var pixel = bitmap.GetPixel (x, y);

                    if (pixel.Alpha > 0 && pixel != background)
                        ink++;
                }

            return ink;
        }
    }
}
