using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The arrow keys move through a tree as upstream's do. Measured on WinForms: Right on a collapsed node
    // expands it and keeps it selected; Right on an expanded node selects its first child; Right on a leaf
    // does nothing; Left on a child selects its parent; Left on an expanded node collapses it; Left on a
    // collapsed root does nothing. Here Right expanded the node and selected its first child in one press.
    [Collection ("Headless")]
    public class TreeViewArrowKeyTests
    {
        private static (Form form, TreeView tree, List<string> log) Tree ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (240, 160) };
            var tree = new TreeView { Dock = DockStyle.Fill };
            var settings = tree.Nodes.Add ("System Settings");
            settings.Nodes.Add ("Alpha child");
            settings.Nodes.Add ("Beta child");
            tree.Nodes.Add ("User Account Setup").Nodes.Add ("Gamma child");

            var log = new List<string> ();
            tree.AfterSelect += (s, e) => log.Add ($"select {e.Node!.Text} {e.Action}");
            tree.AfterExpand += (s, e) => log.Add ($"expand {e.Node!.Text}");
            tree.AfterCollapse += (s, e) => log.Add ($"collapse {e.Node!.Text}");

            form.Controls.Add (tree);
            form.Show ();
            tree.Focus ();
            tree.SelectedNode = settings;
            log.Clear ();

            return (form, tree, log);
        }

        [Fact]
        public void Right_on_a_collapsed_node_expands_it_and_keeps_it_selected ()
        {
            var (form, tree, log) = Tree ();

            using (form) {
                HeadlessRenderer.KeyDown (form, Keys.Home);
                HeadlessRenderer.KeyDown (form, Keys.Right);

                Assert.Same (tree.Nodes[0], tree.SelectedNode);
                Assert.True (tree.Nodes[0].IsExpanded);
                Assert.Equal (new[] { "expand System Settings" }, log);
            }
        }

        [Fact]
        public void Right_on_an_expanded_node_selects_its_first_child_and_stops_at_a_leaf ()
        {
            var (form, tree, log) = Tree ();

            using (form) {
                tree.Nodes[0].Expand ();
                log.Clear ();

                HeadlessRenderer.KeyDown (form, Keys.Right);
                Assert.Same (tree.Nodes[0].Nodes[0], tree.SelectedNode);

                HeadlessRenderer.KeyDown (form, Keys.Right);
                Assert.Same (tree.Nodes[0].Nodes[0], tree.SelectedNode);
                Assert.Equal (new[] { "select Alpha child ByKeyboard" }, log);
            }
        }

        [Fact]
        public void Left_goes_to_the_parent_then_collapses_it_then_does_nothing ()
        {
            var (form, tree, log) = Tree ();

            using (form) {
                tree.Nodes[0].Expand ();
                tree.SelectedNode = tree.Nodes[0].Nodes[0];
                log.Clear ();

                HeadlessRenderer.KeyDown (form, Keys.Left);
                HeadlessRenderer.KeyDown (form, Keys.Left);
                HeadlessRenderer.KeyDown (form, Keys.Left);

                Assert.Same (tree.Nodes[0], tree.SelectedNode);
                Assert.False (tree.Nodes[0].IsExpanded);
                Assert.Equal (new[] { "select System Settings ByKeyboard", "collapse System Settings" }, log);
            }
        }
    }
}
