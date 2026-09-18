using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the stored-only sweep (RC-7) -- the tree-line slice (LST-61). ShowLines, ShowRootLines and
    // LineColor were all stored and read by nothing, so no connector was ever drawn at any setting and
    // the three properties were indistinguishable from each other: a hierarchy read as a flat indented
    // list whatever you set.
    //
    // These three were only reachable at all because the framework-written marker was fixed -- all of
    // them are initialised auto-properties, so the scan called them outbound state and a sweep was
    // told to leave them alone. See StoredOnlyPropertyBaselineTests.
    //
    // The gutter is what makes this testable without a golden image -- but only the right part of it.
    // A node's own row has its glyph and its text in that column, so ink there proves nothing. What is
    // exclusively a connector is an ANCESTOR's gutter on a DESCENDANT's row: two levels in, at the x of
    // a shallower node, nothing else in the renderer draws. That is what GutterInk counts, and the
    // first version of this file got it wrong -- it sampled whole columns and read the node text as a
    // connector, which showed up as 99 pixels of "line" with ShowLines switched off.
    [Collection ("Headless")]
    public class TreeViewLineTests
    {
        // A parent with two children, the first of which has a child of its own, so there is an
        // ancestor gutter that must stay continuous past a node that has a sibling below it.
        private static TreeView Tree (out Form form)
        {
            HeadlessRenderer.Use ();

            var tree = new TreeView { Width = 260, Height = 200 };

            var root = new TreeNode ("root");
            var first = new TreeNode ("first");

            first.Nodes.Add (new TreeNode ("grandchild"));
            root.Nodes.Add (first);
            root.Nodes.Add (new TreeNode ("second"));

            tree.Nodes.Add (root);
            tree.Nodes.Add (new TreeNode ("sibling of root"));

            root.Expand ();
            first.Expand ();

            form = new Form { Width = 360, Height = 300 };
            form.Controls.Add (tree);
            form.Show ();

            return tree;
        }

        [Fact]
        public void ShowLines_off_draws_no_connectors ()
        {
            var tree = Tree (out var form);

            using (form) {
                tree.ShowLines = true;
                var drawn = GutterInk (tree);

                tree.ShowLines = false;
                var bare = GutterInk (tree);

                Assert.True (drawn > 0, "ShowLines = true drew nothing in any gutter.");
                Assert.Equal (0, bare);
            }
        }

        [Fact]
        public void ShowRootLines_off_keeps_the_deeper_gutters ()
        {
            var tree = Tree (out var form);

            using (form) {
                tree.ShowLines = true;
                tree.ShowRootLines = true;
                var root_gutter = GutterInk (tree, level: 0);
                var deep_gutter = GutterInk (tree, level: 1);

                Assert.True (root_gutter > 0);
                Assert.True (deep_gutter > 0);

                tree.ShowRootLines = false;

                // Only the root level goes quiet. A ShowRootLines that cleared everything would pass a
                // test that looked at the whole control, which is why these are counted per level.
                Assert.Equal (0, GutterInk (tree, level: 0));
                Assert.Equal (deep_gutter, GutterInk (tree, level: 1));
            }
        }

        [Fact]
        public void LineColor_is_what_gets_drawn ()
        {
            var tree = Tree (out var form);

            using (form) {
                tree.ShowLines = true;
                tree.LineColor = Color.Red;

                using var bitmap = PaintSurface.Render (tree);

                var found = false;

                for (var y = 0; y < bitmap.Height && !found; y++) {
                    var pixel = bitmap.GetPixel (GutterX (tree, 0), y);

                    found = pixel.Red > 200 && pixel.Green < 80 && pixel.Blue < 80;
                }

                Assert.True (found, "No red pixel in the root gutter -- LineColor was not used.");
            }
        }

        // Color.Empty is the declared default and means "the theme picks", so it must not be taken
        // literally: an empty SKColor is transparent black, which would draw nothing at all.
        [Fact]
        public void The_default_LineColor_still_draws ()
        {
            var tree = Tree (out var form);

            using (form) {
                tree.ShowLines = true;

                Assert.Equal (Color.Empty, tree.LineColor);
                Assert.True (GutterInk (tree) > 0);
            }
        }

        // The other half of the geometry: a node's own gutter, which GutterInk deliberately never
        // looks at. A LEAF draws no glyph, so on its own row its own gutter carries nothing but the
        // vertical it shares with its siblings and the stub out to its text.
        [Fact]
        public void A_leafs_own_gutter_carries_its_stub ()
        {
            var tree = Tree (out var form);

            using (form) {
                var leaf = tree.Nodes[0].Nodes[0].Nodes[0];
                Assert.Equal ("grandchild", leaf.Text);
                Assert.False (leaf.HasChildren);

                tree.ShowLines = true;
                var drawn = OwnGutterInk (tree, leaf);

                tree.ShowLines = false;
                var bare = OwnGutterInk (tree, leaf);

                Assert.True (drawn > 0, "The leaf's own gutter is blank with ShowLines on.");
                Assert.Equal (0, bare);
            }
        }

        private static int OwnGutterInk (TreeView tree, TreeNode node)
        {
            using var bitmap = PaintSurface.Render (tree);

            var x = GutterX (tree, node.IndentLevel);
            var ink = 0;

            for (var y = node.Bounds.Top; y < node.Bounds.Bottom && y < bitmap.Height; y++)
                if (bitmap.GetPixel (x, y) != bitmap.GetPixel (bitmap.Width - 2, y))
                    ink++;

            return ink;
        }

        // The x of a gutter's centre, mirroring TreeViewRenderer.GetIndentStart plus half a glyph.
        // TreeNode.Bounds is DEVICE, and so is the bitmap, so no conversion belongs here.
        private static int GutterX (TreeView tree, int level)
        {
            var step = tree.LogicalToDeviceUnits (tree.Indent > 0 ? tree.Indent : 18);

            return tree.Nodes[0].Bounds.Left + level * step + 2 + tree.LogicalToDeviceUnits (10) / 2;
        }

        // Non-background pixels at a gutter's x, counted ONLY over the rows of nodes deeper than that
        // gutter's level -- where a connector is the sole thing the renderer draws. Sampling the far
        // right of the same row gives the background, so this does not depend on knowing the theme.
        private static int GutterInk (TreeView tree, int level = -1)
        {
            using var bitmap = PaintSurface.Render (tree);

            var levels = level >= 0 ? new[] { level } : new[] { 0, 1 };
            var ink = 0;

            foreach (var l in levels) {
                var x = GutterX (tree, l);

                if (x < 0 || x >= bitmap.Width)
                    continue;

                foreach (var node in Deeper (tree, l))
                    for (var y = node.Bounds.Top; y < node.Bounds.Bottom && y < bitmap.Height; y++)
                        if (bitmap.GetPixel (x, y) != bitmap.GetPixel (bitmap.Width - 2, y))
                            ink++;
            }

            return ink;
        }

        private static System.Collections.Generic.IEnumerable<TreeNode> Deeper (TreeView tree, int level)
            => tree.LayoutedItems.Where (n => n.IndentLevel > level);
    }
}
