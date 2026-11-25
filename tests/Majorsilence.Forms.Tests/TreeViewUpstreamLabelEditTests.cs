using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #729: in a ported app (one that chose its font), BeginEdit on a new, empty node opened a 4px editor --
    // upstream's label rectangle for no text -- so nothing typed could be seen. comctl32's edit control is
    // sized from its text with room for more, and widens as the text grows.
    [Collection ("Headless")]
    public class TreeViewUpstreamLabelEditTests
    {
        private static void Ported (Action<Form, TreeView> body)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (300, 200) };
                var tree = new TreeView { Dock = DockStyle.Fill, LabelEdit = true };
                form.Controls.Add (tree);
                form.Show ();

                body (form, tree);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static int CharWidth (TreeView tree, TreeNode node)
        {
            var (font, size) = Renderers.TreeViewRenderer.TextFont (tree, node);
            return tree.DeviceToLogicalUnits ((int) Math.Ceiling (TextMeasurer.MeasureText ("W", font, size).Width));
        }

        [Fact]
        public void An_empty_node_opens_an_editor_wide_enough_to_type_into ()
        {
            Ported ((_, tree) => {
                Assert.True (TreeView.UsesUpstreamLook);

                var node = new TreeNode ();
                tree.Nodes.Add (node);
                tree.SelectedNode = node;
                node.BeginEdit ();

                var editor = tree.LabelEditor!;
                Assert.NotNull (editor);
                // Three of the widest characters, give or take the rounding through device pixels.
                Assert.True (editor.Width >= 3 * CharWidth (tree, node) - 1, $"editor {editor.Width}px wide");
                Assert.True (editor.Right <= tree.ClientRectangle.Right + 2);
            });
        }

        [Fact]
        public void The_editor_widens_as_the_text_grows_and_never_past_the_client_area ()
        {
            Ported ((_, tree) => {
                var node = new TreeNode ();
                tree.Nodes.Add (node);
                node.BeginEdit ();

                var editor = tree.LabelEditor!;
                var empty_width = editor.Width;

                editor.Text = "A considerably longer label";
                Assert.True (editor.Width > empty_width);

                editor.Text = new string ('W', 200);
                Assert.True (editor.Right <= tree.ClientRectangle.Right + 2, $"editor right {editor.Right}");
            });
        }
    }
}
