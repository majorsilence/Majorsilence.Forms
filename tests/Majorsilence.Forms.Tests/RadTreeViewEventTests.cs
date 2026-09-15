using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the Telerik dead-event sweep — the RadTreeView slice.
    //
    // NodeFormatting, NodeCheckedChanged and NodeCheckedChanging each had a correct
    // `protected internal virtual On...` raiser, and nothing in the assembly called any of them. That
    // is the shape no grep finds: the event is field-backed, the raiser invokes it properly, and every
    // surface reads as wired — it is only dead one level up. The Telerik unraised-event baseline (#178)
    // is what surfaced all three.
    //
    // The engine underneath has had the real hooks since W5.9, so all three fixes are forwards rather
    // than new machinery.
    [Collection ("Headless")]
    public class RadTreeViewEventTests
    {
        private static RadTreeView Tree (out RadTreeNode first)
        {
            HeadlessRenderer.Use ();

            var tree = new RadTreeView { Width = 200, Height = 150, CheckBoxes = true };

            first = new RadTreeNode ("alpha");
            tree.Nodes.Add (first);
            tree.Nodes.Add (new RadTreeNode ("beta"));

            return tree;
        }

        // ---------------- NodeCheckedChanging

        [Fact]
        public void Checking_a_node_raises_NodeCheckedChanging ()
        {
            using var tree = Tree (out var node);
            var seen = new List<RadTreeNode> ();
            tree.NodeCheckedChanging += (_, e) => seen.Add ((RadTreeNode)e.Node!);

            node.Checked = true;

            Assert.Single (seen);
            Assert.Same (node, seen[0]);
        }

        [Fact]
        public void A_handler_can_veto_the_check ()
        {
            // The reason this one matters most: a cancelled event and an unwired one are
            // indistinguishable from the handler's side, so the veto failing is silent.
            using var tree = Tree (out var node);
            tree.NodeCheckedChanging += (_, e) => e.Cancel = true;

            node.Checked = true;

            Assert.False (node.Checked);
        }

        [Fact]
        public void An_uncancelled_check_goes_through ()
        {
            // GUARD, not proof: a forward that cancelled everything would pass the veto test above and
            // break every checkbox tree in the process.
            using var tree = Tree (out var node);
            tree.NodeCheckedChanging += (_, _) => { };

            node.Checked = true;

            Assert.True (node.Checked);
        }

        // ---------------- NodeCheckedChanged

        [Fact]
        public void Checking_a_node_raises_NodeCheckedChanged_afterwards ()
        {
            using var tree = Tree (out var node);
            var checkedWhenRaised = (bool?)null;
            tree.NodeCheckedChanged += (_, e) => checkedWhenRaised = ((RadTreeNode)e.Node!).Checked;

            node.Checked = true;

            // After, not before: a handler that reads the state has to see the new one.
            Assert.True (checkedWhenRaised);
        }

        [Fact]
        public void A_vetoed_check_does_not_raise_NodeCheckedChanged ()
        {
            using var tree = Tree (out var node);
            tree.NodeCheckedChanging += (_, e) => e.Cancel = true;
            var raised = 0;
            tree.NodeCheckedChanged += (_, _) => raised++;

            node.Checked = true;

            Assert.Equal (0, raised);
        }

        // ---------------- NodeFormatting

        [Fact]
        public void NodeFormatting_is_raised_for_each_node_as_it_is_drawn ()
        {
            using var tree = Tree (out _);
            var formatted = new List<string> ();
            tree.NodeFormatting += (_, e) => formatted.Add (e.Node.Text);

            PaintSurface.RenderOnForm (tree, 1f).Dispose ();

            Assert.Contains ("alpha", formatted);
            Assert.Contains ("beta", formatted);
        }

        [Fact]
        public void The_element_a_handler_sets_reaches_the_node ()
        {
            // The element is a carrier: a handler sets properties on it and nothing read them back.
            // Per-node colouring is what a Telerik LOB tree uses this event for.
            using var tree = Tree (out var node);
            tree.NodeFormatting += (_, e) => {
                if (e.Node.Text == "alpha")
                    e.VisualElement.ForeColor = Color.Red;
            };

            PaintSurface.RenderOnForm (tree, 1f).Dispose ();

            Assert.Equal (Color.Red, node.ForeColor);
        }

        [Fact]
        public void An_element_left_alone_does_not_erase_what_the_node_had ()
        {
            // Empty means "the handler did not set this". Writing Color.Empty back would wipe an
            // appearance the application set another way, which is a worse bug than the one being fixed.
            using var tree = Tree (out var node);
            node.ForeColor = Color.Blue;
            tree.NodeFormatting += (_, _) => { };

            PaintSurface.RenderOnForm (tree, 1f).Dispose ();

            Assert.Equal (Color.Blue, node.ForeColor);
        }
    }
}
