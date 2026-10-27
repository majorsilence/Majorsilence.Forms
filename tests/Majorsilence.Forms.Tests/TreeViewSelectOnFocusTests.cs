using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A tree that gains the focus with no node selected selects the node at the top of its view, as
    // upstream's does. Measured on WinForms: BeforeSelect and AfterSelect with TreeViewAction.Unknown,
    // before GotFocus; nothing is selected while the tree has never had the focus; a scrolled tree selects
    // its TopNode; a click that focuses the tree selects only the clicked node, ByMouse. Here a tree
    // selected nothing until it was clicked, so the keyboard had no node to start from.
    [Collection ("Headless")]
    public class TreeViewSelectOnFocusTests
    {
        private static (Form form, TreeView tree, Button button, List<string> log) Tree (bool buttonFirst, int extra = 0)
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 120) };
            var tree = new TreeView { Location = new Point (0, 0), Size = new Size (240, 120), TabIndex = 1 };
            var button = new Button { Text = "x", Location = new Point (250, 10), Size = new Size (40, 24), TabIndex = buttonFirst ? 0 : 2 };

            tree.Nodes.Add ("System Settings").Nodes.Add ("Alpha child");
            tree.Nodes.Add ("User Account Setup");
            for (var i = 0; i < extra; i++)
                tree.Nodes.Add ("Extra " + i);

            var log = new List<string> ();
            tree.AfterSelect += (s, e) => log.Add ($"select {e.Node!.Text} {e.Action}");
            tree.GotFocus += (s, e) => log.Add ($"focus {tree.SelectedNode?.Text}");

            form.Controls.Add (tree);
            form.Controls.Add (button);
            form.Show ();

            return (form, tree, button, log);
        }

        [Fact]
        public void The_first_node_is_selected_when_the_tree_takes_the_focus_on_show ()
        {
            var (form, tree, _, log) = Tree (buttonFirst: false);

            using (form) {
                Assert.True (tree.Focused);
                Assert.Same (tree.Nodes[0], tree.SelectedNode);
                Assert.Equal (new[] { "select System Settings Unknown", "focus System Settings" }, log);
            }
        }

        [Fact]
        public void Nothing_is_selected_until_the_tree_has_the_focus ()
        {
            var (form, tree, _, log) = Tree (buttonFirst: true);

            using (form) {
                Assert.Null (tree.SelectedNode);

                tree.Focus ();

                Assert.Same (tree.Nodes[0], tree.SelectedNode);
                Assert.Equal (new[] { "select System Settings Unknown", "focus System Settings" }, log);
            }
        }

        [Fact]
        public void A_scrolled_tree_selects_its_top_node ()
        {
            var (form, tree, _, _) = Tree (buttonFirst: true, extra: 30);

            using (form) {
                tree.TopNode = tree.Nodes[8];
                var top = tree.TopNode;

                tree.Focus ();

                Assert.NotSame (tree.Nodes[0], top);
                Assert.Same (top, tree.SelectedNode);
            }
        }

        [Fact]
        public void A_click_that_focuses_the_tree_selects_only_the_clicked_node ()
        {
            var (form, tree, _, log) = Tree (buttonFirst: true);

            using (form) {
                HeadlessRenderer.CapturePng (form);
                var row = tree.Nodes[1].Bounds;
                var scale = tree.LogicalToDeviceUnits (100) / 100f;

                HeadlessRenderer.Click (form, (int) ((row.Left + row.Width / 4) / scale), (int) ((row.Top + row.Height / 2) / scale));

                Assert.Same (tree.Nodes[1], tree.SelectedNode);
                Assert.Equal (new[] { "focus ", "select User Account Setup ByMouse" }, log);
            }
        }
    }
}
