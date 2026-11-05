using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Expanding a node scrolls the tree so its children show, as upstream's native tree does before it raises AfterExpand.
    // Measured (.NET 10) on a tree 25 rows high: expanding the 21st root's 10 children brought "Root 6" to the top, the
    // last child on the bottom row -- the same by Expand (), ExpandAll (), a double-click and the plus/minus; with 40
    // children the expanded node went to the top. Telerik's RadTreeView keeps its scroll position (UI for WinForms
    // 2026.2.520: the scroll bar stayed at 0). Here no tree scrolled, so a menu node expanded near the bottom of a
    // module's navigation tree opened its children out of sight.
    [Collection ("Headless")]
    public class TreeViewExpandScrollTests
    {
        private static (Form form, T tree) Tree<T> (int children, int at = 20) where T : TreeView, new ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new Size (320, 560) };
            var tree = new T { Location = new Point (0, 0), ItemHeight = 20, BorderStyle = BorderStyle.None, Size = new Size (300, 20 * 25) };

            for (var i = 0; i < 30; i++) {
                var node = tree.Nodes.Add ($"Root {i}");

                if (i == at)
                    for (var c = 0; c < children; c++)
                        node.Nodes.Add ($"Child {c}");
            }

            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.RenderOnForm (tree, 1f).Dispose ();
            return (form, tree);
        }

        // The whole rows the tree shows (node bounds are in device pixels).
        private static int Rows (TreeView tree) => tree.DeviceClientRectangle.Height / tree.Nodes[0].DeviceBounds.Height;

        [Fact]
        public void Expanding_a_node_near_the_bottom_brings_its_last_child_onto_the_bottom_row ()
        {
            var (form, tree) = Tree<TreeView> (children: 10);
            using (form) {
                Assert.Same (tree.Nodes[0], tree.TopNode);

                Assert.True (Rows (tree) < 30);

                tree.Nodes[20].Expand ();

                // Root 20's last child is row 31: it lands on the bottom row.
                Assert.Same (tree.Nodes[31 - Rows (tree)], tree.TopNode);
                Assert.True (tree.Nodes[20].Nodes[9].IsVisible);
            }
        }

        [Fact]
        public void A_subtree_taller_than_the_tree_puts_the_expanded_node_at_the_top ()
        {
            var (form, tree) = Tree<TreeView> (children: 40);
            using (form) {
                tree.Nodes[20].Expand ();

                Assert.Same (tree.Nodes[20], tree.TopNode);
            }
        }

        [Fact]
        public void The_tree_has_scrolled_by_the_time_AfterExpand_is_raised ()
        {
            var (form, tree) = Tree<TreeView> (children: 10);
            using (form) {
                TreeNode? top = null;
                tree.AfterExpand += (s, e) => top = tree.TopNode;

                tree.Nodes[20].Expand ();

                Assert.Same (tree.Nodes[31 - Rows (tree)], top);
            }
        }

        [Fact]
        public void Children_that_already_show_do_not_scroll_the_tree ()
        {
            var (form, tree) = Tree<TreeView> (children: 10, at: 2);
            using (form) {
                tree.Nodes[2].Expand ();

                Assert.Same (tree.Nodes[0], tree.TopNode);
            }
        }

        [Fact]
        public void A_RadTreeView_keeps_its_scroll_position ()
        {
            var (form, rad) = Tree<RadTreeView> (children: 10);
            using (form) {
                var tree = (TreeView) rad;
                tree.Nodes[20].Expand ();

                Assert.True (tree.Nodes[20].IsExpanded);
                Assert.Same (tree.Nodes[0], tree.TopNode);
            }
        }
    }
}
