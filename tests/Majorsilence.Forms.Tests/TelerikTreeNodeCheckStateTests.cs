using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadTreeNode.CheckState, the tri-state check Telerik code reads and writes, measured on UI.for.WinForms
// 2026.2.520: Checked is true only when the state is On, setting Checked moves the state to On or Off,
// and every change of state -- to Indeterminate too -- is asked through NodeCheckedChanging and reported
// through NodeCheckedChanged. Also the typed GetNodeAt and TreeNodeCheckedEventArgs' base class.
[Collection ("Headless")]
public class TelerikTreeNodeCheckStateTests
{
    // A down/up pair does not synthesise MouseClick, and MouseClick is what the tree toggles a box on.
    private sealed class ClickableTree : RadTreeView
    {
        // Takes the device-pixel point the layout reports and clicks it, in the logical units MouseClick takes.
        internal void ClickAtDevice (int x, int y) => OnMouseClick (new MouseEventArgs (MouseButtons.Left, 1, (int) (x / Scaling), (int) (y / Scaling), 0));
    }

    private static string Slot (Control control, Rectangle device)
    {
        using var bitmap = PaintSurface.Render (control);
        var builder = new System.Text.StringBuilder ();

        for (var y = System.Math.Max (0, device.Top); y < device.Bottom && y < bitmap.Height; y++)
            for (var x = System.Math.Max (0, device.Left); x < device.Right && x < bitmap.Width; x++)
                builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

        return builder.ToString ();
    }

    private static ClickableTree Tree (out Form form, out RadTreeNode a, out RadTreeNode a1, out RadTreeNode b, out System.Collections.Generic.List<string> events)
    {
        HeadlessRenderer.Use ();

        var tree = new ClickableTree { Width = 260, Height = 300, CheckBoxes = true };
        a = tree.Nodes.Add ("A");
        a1 = a.Nodes.Add ("A1");
        b = tree.Nodes.Add ("B");
        tree.ExpandAll ();

        form = new Form { Width = 360, Height = 400 };
        form.Controls.Add (tree);
        form.Show ();
        tree.LayoutItems ();

        var log = new System.Collections.Generic.List<string> ();
        tree.NodeCheckedChanging += (s, e) => log.Add ("changing " + e.Node.Text);
        tree.NodeCheckedChanged += (s, e) => log.Add ($"changed {e.Node.Text} {e.Node.CheckState}");
        events = log;

        return tree;
    }

    [Fact]
    public void CheckState_and_Checked_agree_as_Telerik_s_do ()
    {
        var tree = Tree (out var form, out var a, out var a1, out var b, out _);

        using (form) {
            Assert.Equal (ToggleState.Off, b.CheckState);

            b.CheckState = ToggleState.On;
            Assert.True (b.Checked);

            b.CheckState = ToggleState.Indeterminate;
            Assert.Equal (ToggleState.Indeterminate, b.CheckState);
            Assert.False (b.Checked);

            b.Checked = true;
            Assert.Equal (ToggleState.On, b.CheckState);

            b.CheckState = ToggleState.Indeterminate;
            b.Checked = false;
            Assert.Equal (ToggleState.Off, b.CheckState);

            // Without tri-state propagation a parent and its child are independent.
            a1.CheckState = ToggleState.On;
            Assert.Equal (ToggleState.Off, a.CheckState);
        }
    }

    [Fact]
    public void Every_change_of_state_is_asked_and_reported_once ()
    {
        var tree = Tree (out var form, out _, out _, out var b, out var events);

        using (form) {
            b.CheckState = ToggleState.Indeterminate;
            Assert.Equal (new[] { "changing B", "changed B Indeterminate" }, events);

            events.Clear ();
            b.CheckState = ToggleState.Indeterminate;
            Assert.Empty (events);

            b.CheckState = ToggleState.On;
            Assert.Equal (new[] { "changing B", "changed B On" }, events);

            events.Clear ();
            b.Checked = false;
            Assert.Equal (new[] { "changing B", "changed B Off" }, events);
        }
    }

    [Fact]
    public void A_vetoed_change_leaves_the_state_alone ()
    {
        var tree = Tree (out var form, out _, out _, out var b, out var events);

        using (form) {
            tree.NodeCheckedChanging += (s, e) => e.Cancel = true;

            b.CheckState = ToggleState.Indeterminate;
            Assert.Equal (ToggleState.Off, b.CheckState);

            b.CheckState = ToggleState.On;
            Assert.Equal (ToggleState.Off, b.CheckState);
            Assert.DoesNotContain (events, e => e.StartsWith ("changed"));
        }
    }

    [Fact]
    public void A_detached_node_keeps_its_state ()
    {
        var node = new RadTreeNode ("D") { CheckState = ToggleState.Indeterminate };

        Assert.Equal (ToggleState.Indeterminate, node.CheckState);
        Assert.False (node.Checked);

        node.Checked = true;
        Assert.Equal (ToggleState.On, node.CheckState);
    }

    [Fact]
    public void An_indeterminate_node_is_drawn_with_the_indeterminate_box ()
    {
        var tree = Tree (out var form, out _, out _, out var b, out _);

        using (form) {
            var box = tree.CheckBounds (b);

            b.CheckState = ToggleState.Off;
            var off = Slot (tree, box);

            b.CheckState = ToggleState.On;
            var on = Slot (tree, box);

            b.CheckState = ToggleState.Indeterminate;
            var indeterminate = Slot (tree, box);

            Assert.NotEqual (off, indeterminate);
            Assert.NotEqual (on, indeterminate);
        }
    }

    [Fact]
    public void A_click_on_an_indeterminate_box_checks_the_node ()
    {
        var tree = Tree (out var form, out _, out _, out var b, out _);

        using (form) {
            b.CheckState = ToggleState.Indeterminate;

            var box = tree.CheckBounds (b);
            tree.ClickAtDevice (box.Left + box.Width / 2, box.Top + box.Height / 2);

            Assert.Equal (ToggleState.On, b.CheckState);
        }
    }

    [Fact]
    public void GetNodeAt_returns_the_Telerik_node ()
    {
        var tree = Tree (out var form, out _, out _, out var b, out _);

        using (form) {
            // Bounds are device pixels; GetNodeAt takes the logical client point.
            var row = b.Bounds;
            var at = new Point ((int) ((row.Left + 30) / tree.Scaling), (int) ((row.Top + row.Height / 2) / tree.Scaling));

            Assert.Same (b, ((TreeView) tree).GetNodeAt (at));

            RadTreeNode? hit = tree.GetNodeAt (at);
            Assert.Same (b, hit);
            Assert.Same (b, tree.GetNodeAt (at.X, at.Y));
        }
    }

    [Fact]
    public void The_checked_event_args_are_tree_view_event_args ()
    {
        var tree = Tree (out var form, out _, out _, out var b, out _);

        using (form) {
            RadTreeViewEventArgs? seen = null;
            // A method group taking the base args, as a VB handler declared with RadTreeViewEventArgs binds.
            void Handler (object? sender, RadTreeViewEventArgs e) => seen = e;
            tree.NodeCheckedChanged += Handler;

            b.Checked = true;

            Assert.Same (b, seen?.Node);
        }
    }
}
