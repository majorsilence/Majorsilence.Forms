using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Maps points to tree nodes the way a tap/click does.
    /// </summary>
    /// <remarks>
    /// <see cref="TreeView.GetItemAtLocation"/> takes the logical coordinates a
    /// <see cref="MouseEventArgs"/> carries, but the laid-out node bounds it tests against come from
    /// <c>LayoutItems()</c> against <c>ClientRectangle</c> and the items' device-pixel
    /// <c>GetPreferredSize</c>, so they are in device pixels. Without converting, the point picked the
    /// node at index x scale — on a phone at scale ~2.6 a tap on the second row selected a node two or
    /// three rows down. Written in logical units so the assertions hold at any scaling (the scaled CI
    /// pass, <c>MF_HEADLESS_SCALE=2</c>, is what makes them catch it). Mirrors
    /// <see cref="ListBoxHitTestTests"/>.
    /// </remarks>
    public class TreeViewHitTestTests
    {
        private static Form BuildForm (out TreeView tree, int itemCount = 6)
        {
            var form = new Form { UseSystemDecorations = true, Width = 320, Height = 320 };

            tree = new TreeView { Name = "tree", Left = 0, Top = 0, Width = 220, Height = 260 };
            for (var i = 0; i < itemCount; i++)
                tree.Items.Add ($"Item {i}");

            form.Controls.Add (tree);
            HeadlessRenderer.CapturePng (form, 320, 320);   // force a layout pass
            return form;
        }

        // The logical centre of a node, in the units a mouse event arrives in: the node's own laid-out
        // Bounds are in device pixels, so convert the centre back through the control's scaling.
        private static Point NodeCentreLogical (TreeView tree, TreeNode node) =>
            new (tree.DeviceToLogicalUnits (node.Bounds.Left + 5),
                 tree.DeviceToLogicalUnits (node.Bounds.Top + node.Bounds.Height / 2));

        [Fact]
        public void A_point_inside_a_node_returns_that_node ()
        {
            using var form = BuildForm (out var tree);

            foreach (var node in tree.Nodes.Cast<TreeNode> ().Take (4))
                Assert.Same (node, tree.GetItemAtLocation (NodeCentreLogical (tree, node)));
        }

        [Fact]
        public void A_point_below_the_last_node_belongs_to_no_node ()
        {
            using var form = BuildForm (out var tree, itemCount: 2);

            var last = tree.Nodes.Cast<TreeNode> ().Last ();
            var belowEverything = new Point (
                tree.DeviceToLogicalUnits (last.Bounds.Left + 5),
                tree.DeviceToLogicalUnits (last.Bounds.Bottom) + 200);

            Assert.Null (tree.GetItemAtLocation (belowEverything));
        }
    }
}
