// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/TreeViewTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms TreeViewTests / TreeNodeTests /
    // TreeNodeCollectionTests, adapted to the Majorsilence.Forms API (no Handle/CreateParams/
    // accessibility plumbing). Majorsilence.Forms models nodes as TreeViewItem (with a TreeNode
    // compatibility subclass) and keeps an internal hidden root item, so a few WinForms
    // facts (e.g. a top-level node's Parent being null, or FullPath throwing when detached)
    // are intentionally different and are not asserted here. Everything tested below pins
    // Majorsilence.Forms' actual, source-verified behavior against the WinForms semantics that do
    // carry over: Text/Index/Level/FullPath, the Nodes collection add/remove/clear/indexer
    // rules, Expand/Collapse, SelectedItem/SelectedNode, and the node-count helpers.
    public class TreeViewTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var treeView = new TreeView ();

            Assert.Empty (treeView.Items);
            Assert.Empty (treeView.Nodes);
            Assert.Same (treeView.Items, treeView.Nodes);
            Assert.Equal (0, treeView.GetNodeCount (includeSubTrees: false));
            Assert.Equal (0, treeView.GetNodeCount (includeSubTrees: true));
            // Majorsilence.Forms keeps a hidden root selected by default; it is never null.
            Assert.NotNull (treeView.SelectedItem);
            Assert.Equal (TreeViewDrawMode.Normal, treeView.DrawMode);
            Assert.True (treeView.ShowDropdownGlyph);
            Assert.True (treeView.ShowItemImages);
            Assert.False (treeView.CheckBoxes);
            Assert.False (treeView.VirtualMode);
            Assert.Equal ("\\", treeView.PathSeparator);
        }

        [Fact]
        public void TreeNode_Ctor_Default ()
        {
            var node = new TreeNode ();

            Assert.Equal (Color.Empty, node.BackColor);
            Assert.Equal (Rectangle.Empty, node.Bounds);
            Assert.False (node.Checked);
            Assert.Null (node.FirstNode);
            Assert.Equal (Color.Empty, node.ForeColor);
            Assert.Equal (-1, node.ImageIndex);
            Assert.Empty (node.ImageKey);
            Assert.Equal (0, node.Index);
            Assert.False (node.IsExpanded);
            Assert.False (node.IsSelected);
            Assert.Null (node.LastNode);
            Assert.Equal (0, node.Level);
            Assert.Empty (node.Name);
            Assert.Null (node.NextNode);
            Assert.Null (node.NodeFont);
            Assert.Empty (node.Items);
            Assert.Same (node.Items, node.Items);
            Assert.Same (node.Items, node.Nodes);
            Assert.Null (node.Parent);
            Assert.Null (node.PrevNode);
            Assert.Equal (-1, node.SelectedImageIndex);
            Assert.Empty (node.SelectedImageKey);
            Assert.Null (node.Tag);
            Assert.Empty (node.Text);
            Assert.Empty (node.ToolTipText);
            Assert.Null (node.TreeView);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void TreeNode_Ctor_String (string? text, string expectedText)
        {
            // Majorsilence.Forms' TreeViewItem does not normalize null to "" in the ctor, so guard.
            var node = new TreeNode (text ?? string.Empty);

            Assert.Equal (expectedText, node.Text);
            Assert.Null (node.Parent);
            Assert.Empty (node.Items);
            Assert.Equal (0, node.Index);
            Assert.Equal (0, node.Level);
        }

        [Fact]
        public void TreeNode_Ctor_String_TreeNodeArray ()
        {
            var child1 = new TreeNode ();
            var child2 = new TreeNode ("text");
            var node = new TreeNode ("parent", new[] { child1, child2 });

            Assert.Equal ("parent", node.Text);
            Assert.Equal (2, node.Items.Count);
            Assert.Same (child1, node.FirstNode);
            Assert.Same (child2, node.LastNode);
            Assert.Same (node, child1.Parent);
            Assert.Same (node, child2.Parent);
        }

        [Theory]
        [InlineData ("text", 1, 7)]
        [InlineData (null, -1, -1)]
        public void TreeNode_Ctor_String_Int_Int (string? text, int imageIndex, int selectedImageIndex)
        {
            var node = new TreeNode (text ?? string.Empty, imageIndex, selectedImageIndex);

            Assert.Equal (text ?? string.Empty, node.Text);
            Assert.Equal (imageIndex, node.ImageIndex);
            Assert.Equal (selectedImageIndex, node.SelectedImageIndex);
        }

        [Theory]
        [InlineData ("Node 1")]
        [InlineData ("")]
        public void TreeNodeCollection_Add_String_Success (string text)
        {
            using var treeView = new TreeView ();
            var collection = treeView.Nodes;

            var node = collection.Add (text);

            Assert.Same (node, collection[0]);
            Assert.Single (collection);
            Assert.Equal (text, node.Text);
            Assert.Equal (text, node.FullPath);
            Assert.Equal (0, node.Index);
            Assert.Equal (0, node.Level);
            Assert.Same (treeView, node.TreeView);
        }

        [Fact]
        public void TreeNodeCollection_Add_TreeViewItem_ReturnsSameInstance ()
        {
            using var treeView = new TreeView ();
            var node = new TreeViewItem ("text");

            var result = treeView.Nodes.Add (node);

            Assert.Same (node, result);
            Assert.Single (treeView.Nodes);
            Assert.Same (treeView, node.TreeView);
        }

        [Fact]
        public void TreeNodeCollection_AddRange_Success ()
        {
            using var treeView = new TreeView ();
            var node1 = new TreeViewItem ("Node 1");
            var node2 = new TreeViewItem ("Node 2");
            var node3 = new TreeViewItem ("Node 3");

            treeView.Nodes.AddRange (new[] { node1, node2, node3 });

            Assert.Equal (3, treeView.Nodes.Count);
            Assert.Same (node1, treeView.Nodes[0]);
            Assert.Same (node2, treeView.Nodes[1]);
            Assert.Same (node3, treeView.Nodes[2]);
        }

        [Fact]
        public void SelectedItem_BeforeLayout_DoesNotThrow ()
        {
            // Regression: setting SelectedItem before the control is laid out (e.g. in a form
            // constructor) used to throw ArgumentOutOfRangeException from ScrollBar.Value because
            // EnsureItemVisible computed a scroll target outside the scrollbar's range.
            using var treeView = new TreeView ();
            var nodes = new TreeViewItem[20];

            for (var i = 0; i < nodes.Length; i++) {
                nodes[i] = new TreeViewItem ($"Node {i}");
                treeView.Nodes.Add (nodes[i]);
            }

            var ex = Record.Exception (() => treeView.SelectedItem = nodes[15]);

            Assert.Null (ex);
            Assert.Same (nodes[15], treeView.SelectedItem);
        }

        [Fact]
        public void TreeNodeCollection_Insert_Success ()
        {
            using var treeView = new TreeView ();
            treeView.Nodes.Add ("Node 0");
            treeView.Nodes.Add ("Node 2");

            var inserted = new TreeViewItem ("Node 1");
            treeView.Nodes.Insert (1, inserted);

            Assert.Equal (3, treeView.Nodes.Count);
            Assert.Same (inserted, treeView.Nodes[1]);
            Assert.Equal (1, inserted.Index);
            Assert.Same (treeView, inserted.TreeView);
        }

        [Fact]
        public void TreeNodeCollection_Remove_Success ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node 0");
            treeView.Nodes.Add ("Node 1");

            treeView.Nodes.Remove (node);

            Assert.Single (treeView.Nodes);
            Assert.Equal ("Node 1", treeView.Nodes[0].Text);
            Assert.Null (node.Parent);
        }

        [Fact]
        public void TreeViewItem_Remove_RemovesFromParent ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node 0");

            node.Remove ();

            Assert.Empty (treeView.Nodes);
            Assert.Null (node.Parent);
        }

        [Fact]
        public void TreeNodeCollection_Clear_ResetsParents ()
        {
            using var treeView = new TreeView ();
            var node1 = treeView.Nodes.Add ("Node 0");
            var node2 = treeView.Nodes.Add ("Node 1");

            treeView.Nodes.Clear ();

            Assert.Empty (treeView.Nodes);
            Assert.Null (node1.Parent);
            Assert.Null (node2.Parent);
        }

        [Fact]
        public void TreeNodeCollection_Contains_ReturnsExpected ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node 0");
            var other = new TreeViewItem ("Other");

            Assert.Contains (node, treeView.Nodes);
            Assert.DoesNotContain (other, treeView.Nodes);
        }

        [Fact]
        public void TreeNodeCollection_IndexOf_ReturnsExpected ()
        {
            using var treeView = new TreeView ();
            var node0 = treeView.Nodes.Add ("Node 0");
            var node1 = treeView.Nodes.Add ("Node 1");

            Assert.Equal (0, treeView.Nodes.IndexOf (node0));
            Assert.Equal (1, treeView.Nodes.IndexOf (node1));
            Assert.Equal (-1, treeView.Nodes.IndexOf (new TreeViewItem ("x")));
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (1)]
        public void TreeNodeCollection_Item_GetInvalidIndex_ThrowsArgumentOutOfRangeException (int index)
        {
            using var treeView = new TreeView ();
            treeView.Nodes.Add ("text");

            Assert.Throws<ArgumentOutOfRangeException> (() => treeView.Nodes[index]);
        }

        [Fact]
        public void TreeNodeCollection_Item_Set_ReplacesNode ()
        {
            using var treeView = new TreeView ();
            treeView.Nodes.Add ("Node 0");

            var replacement = new TreeViewItem ("New Node 0");
            treeView.Nodes[0] = replacement;

            Assert.Single (treeView.Nodes);
            Assert.Same (replacement, treeView.Nodes[0]);
            Assert.Same (treeView, replacement.TreeView);
        }

        [Fact]
        public void Items_AddChild_SetsParentAndPath ()
        {
            using var treeView = new TreeView ();
            var parent = treeView.Nodes.Add ("Parent");
            var child = parent.Items.Add ("Child");

            Assert.Same (parent, child.Parent);
            Assert.Equal (1, child.Level);
            Assert.Equal (0, parent.Level);
            Assert.Equal ("Parent\\Child", child.FullPath);
            Assert.Same (treeView, child.TreeView);
        }

        [Fact]
        public void Level_NestedNodes_ReturnsDepthFromTreeView ()
        {
            using var treeView = new TreeView ();
            var root = treeView.Nodes.Add ("Root");
            var child = root.Items.Add ("Child");
            var grandChild = child.Items.Add ("GrandChild");

            Assert.Equal (0, root.Level);
            Assert.Equal (1, child.Level);
            Assert.Equal (2, grandChild.Level);
        }

        [Fact]
        public void GetNodeCount_IncludeSubTrees_CountsAllDescendants ()
        {
            using var treeView = new TreeView ();
            var parent = treeView.Nodes.Add ("Parent");
            parent.Items.Add ("Child 1");
            var child2 = parent.Items.Add ("Child 2");
            child2.Items.Add ("GrandChild");
            treeView.Nodes.Add ("Sibling");

            Assert.Equal (2, treeView.GetNodeCount (includeSubTrees: false));
            Assert.Equal (5, treeView.GetNodeCount (includeSubTrees: true));
        }

        [Fact]
        public void TreeViewItem_GetNodeCount_CountsChildren ()
        {
            var parent = new TreeViewItem ("Parent");
            parent.Items.Add ("Child 1");
            var child2 = parent.Items.Add ("Child 2");
            child2.Items.Add ("GrandChild");

            Assert.Equal (2, parent.GetNodeCount (includeSubTrees: false));
            Assert.Equal (3, parent.GetNodeCount (includeSubTrees: true));
        }

        [Fact]
        public void HasChildren_ReturnsExpected ()
        {
            var node = new TreeViewItem ("Node");
            Assert.False (node.HasChildren);

            node.Items.Add ("Child");
            Assert.True (node.HasChildren);
        }

        [Fact]
        public void Expanded_TopLevelNode_TogglesWhenHasChildren ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");
            node.Items.Add ("Child");

            // Default collapsed for non-root nodes.
            Assert.False (node.Expanded);

            node.Expand ();
            Assert.True (node.Expanded);
            Assert.True (node.IsExpanded);

            node.Collapse ();
            Assert.False (node.Expanded);
        }

        [Fact]
        public void Toggle_FlipsExpandedState ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");
            node.Items.Add ("Child");

            Assert.False (node.Expanded);
            node.Toggle ();
            Assert.True (node.Expanded);
            node.Toggle ();
            Assert.False (node.Expanded);
        }

        [Fact]
        public void Expand_LeafNode_DoesNotExpand ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Leaf");

            node.Expand ();

            // A node with no children does not expand.
            Assert.False (node.Expanded);
        }

        [Fact]
        public void ExpandAll_ExpandsEveryNodeWithChildren ()
        {
            using var treeView = new TreeView ();
            var parent = treeView.Nodes.Add ("Parent");
            var child = parent.Items.Add ("Child");
            child.Items.Add ("GrandChild");

            treeView.ExpandAll ();

            Assert.True (parent.Expanded);
            Assert.True (child.Expanded);
        }

        [Fact]
        public void CollapseAll_CollapsesEveryNode ()
        {
            using var treeView = new TreeView ();
            var parent = treeView.Nodes.Add ("Parent");
            var child = parent.Items.Add ("Child");
            child.Items.Add ("GrandChild");

            treeView.ExpandAll ();
            treeView.CollapseAll ();

            Assert.False (parent.Expanded);
            Assert.False (child.Expanded);
        }

        [Fact]
        public void FirstNode_LastNode_ReturnExpected ()
        {
            var parent = new TreeViewItem ("Parent");
            Assert.Null (parent.FirstNode);
            Assert.Null (parent.LastNode);

            var first = parent.Items.Add ("First");
            var last = parent.Items.Add ("Last");

            Assert.Same (first, parent.FirstNode);
            Assert.Same (last, parent.LastNode);
        }

        [Fact]
        public void NextNode_PrevNode_ReturnSiblings ()
        {
            using var treeView = new TreeView ();
            var node0 = treeView.Nodes.Add ("Node 0");
            var node1 = treeView.Nodes.Add ("Node 1");
            var node2 = treeView.Nodes.Add ("Node 2");

            Assert.Same (node1, node0.NextNode);
            Assert.Null (node2.NextNode);
            Assert.Same (node1, node2.PrevNode);
            Assert.Null (node0.PrevNode);
        }

        [Fact]
        public void NextItem_PreviousItem_ReturnSiblings ()
        {
            using var treeView = new TreeView ();
            var node0 = treeView.Nodes.Add ("Node 0");
            var node1 = treeView.Nodes.Add ("Node 1");

            Assert.Same (node1, node0.NextItem ());
            Assert.Null (node1.NextItem ());
            Assert.Same (node0, node1.PreviousItem ());
            Assert.Null (node0.PreviousItem ());
        }

        [Fact]
        public void Index_ReturnsPositionInParentCollection ()
        {
            using var treeView = new TreeView ();
            var node0 = treeView.Nodes.Add ("Node 0");
            var node1 = treeView.Nodes.Add ("Node 1");
            var node2 = treeView.Nodes.Add ("Node 2");

            Assert.Equal (0, node0.Index);
            Assert.Equal (1, node1.Index);
            Assert.Equal (2, node2.Index);
        }

        [Fact]
        public void SelectedItem_Set_GetReturnsExpected ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");

            treeView.SelectedItem = node;

            Assert.Same (node, treeView.SelectedItem);
            Assert.Same (node, treeView.SelectedNode);
            Assert.True (node.IsSelected);
        }

        [Fact]
        public void SelectedItem_SetNull_IsIgnored ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");
            treeView.SelectedItem = node;

            treeView.SelectedItem = null!;

            // Majorsilence.Forms does not allow unselecting; the previous selection remains.
            Assert.Same (node, treeView.SelectedItem);
        }

        [Fact]
        public void SelectedNode_Set_GetReturnsExpected ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");

            treeView.SelectedNode = node;

            Assert.Same (node, treeView.SelectedNode);
        }

        [Fact]
        public void ItemSelected_RaisedOnSelectionChange ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");

            var raised = 0;
            TreeViewItem? selected = null;
            treeView.ItemSelected += (s, e) => { raised++; selected = e.Value; };

            treeView.SelectedItem = node;

            Assert.Equal (1, raised);
            Assert.Same (node, selected);

            // Setting the same value again does not re-raise.
            treeView.SelectedItem = node;
            Assert.Equal (1, raised);
        }

        [Fact]
        public void AfterSelect_RaisedOnSelectionChange ()
        {
            using var treeView = new TreeView ();
            var node = treeView.Nodes.Add ("Node");

            var raised = 0;
            treeView.AfterSelect += (s, e) => raised++;

            treeView.SelectedItem = node;

            Assert.Equal (1, raised);
        }

        [Fact]
        public void FindNodeByFullPath_ReturnsMatchingNode ()
        {
            using var treeView = new TreeView ();
            var parent = treeView.Nodes.Add ("Parent");
            var child = parent.Items.Add ("Child");

            Assert.Same (parent, treeView.FindNodeByFullPath ("Parent"));
            Assert.Same (child, treeView.FindNodeByFullPath ("Parent\\Child"));
            Assert.Null (treeView.FindNodeByFullPath ("Missing"));
        }

        [Fact]
        public void TopNode_ReturnsFirstTopLevelNode ()
        {
            using var treeView = new TreeView ();
            Assert.Null (treeView.TopNode);

            var node0 = treeView.Nodes.Add ("Node 0");
            treeView.Nodes.Add ("Node 1");

            Assert.Same (node0, treeView.TopNode);
        }

        [Fact]
        public void Tag_Set_GetReturnsExpected ()
        {
            var tag = new object ();
            var node = new TreeViewItem ("Node") { Tag = tag };

            Assert.Same (tag, node.Tag);
        }

        [Theory]
        [InlineData (TreeViewDrawMode.Normal)]
        [InlineData (TreeViewDrawMode.OwnerDrawContent)]
        public void DrawMode_Set_GetReturnsExpected (TreeViewDrawMode value)
        {
            using var treeView = new TreeView { DrawMode = value };

            Assert.Equal (value, treeView.DrawMode);

            // Set same.
            treeView.DrawMode = value;
            Assert.Equal (value, treeView.DrawMode);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void VirtualMode_Set_GetReturnsExpected (bool value)
        {
            using var treeView = new TreeView { VirtualMode = value };

            Assert.Equal (value, treeView.VirtualMode);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowDropdownGlyph_Set_GetReturnsExpected (bool value)
        {
            using var treeView = new TreeView { ShowDropdownGlyph = value };

            Assert.Equal (value, treeView.ShowDropdownGlyph);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowItemImages_Set_GetReturnsExpected (bool value)
        {
            using var treeView = new TreeView { ShowItemImages = value };

            Assert.Equal (value, treeView.ShowItemImages);
        }
    }
}
