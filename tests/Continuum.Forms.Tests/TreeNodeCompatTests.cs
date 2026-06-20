// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (TreeNodeTests.cs / TreeNodeMouseClickEventArgsTests.cs
// under src/test/unit/System.Windows.Forms/), rewritten for the Continuum.Forms WinForms-compat API
// (TreeNode is a compat subclass of TreeViewItem). Original work Copyright (c) .NET Foundation and Contributors.

using Xunit;

namespace Continuum.Forms.Tests
{
    public class TreeNodeCompatTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var node = new TreeNode ();
            Assert.Equal (string.Empty, node.Text);
            Assert.IsAssignableFrom<TreeViewItem> (node);
        }

        [Fact]
        public void Ctor_Text ()
        {
            var node = new TreeNode ("Root");
            Assert.Equal ("Root", node.Text);
        }

        [Fact]
        public void Ctor_TextChildren ()
        {
            var children = new[] { new TreeNode ("a"), new TreeNode ("b") };
            var node = new TreeNode ("Root", children);

            Assert.Equal ("Root", node.Text);
            Assert.Equal (2, node.Items.Count);
        }

        [Fact]
        public void Ctor_TextImageIndices ()
        {
            var node = new TreeNode ("Root", 3, 4);
            Assert.Equal ("Root", node.Text);
        }

        [Fact]
        public void MouseClickEventArgs_SetsProperties ()
        {
            var node = new TreeViewItem ("n");
            var e = new TreeNodeMouseClickEventArgs (node, MouseButtons.Right, 2, 10, 20);

            Assert.Same (node, e.Node);
            Assert.Equal (MouseButtons.Right, e.Button);
            Assert.Equal (2, e.Clicks);
            Assert.Equal (10, e.X);
            Assert.Equal (20, e.Y);
        }

        [Fact]
        public void MouseHoverEventArgs_SetsNode ()
        {
            var node = new TreeViewItem ("n");
            var e = new TreeNodeMouseHoverEventArgs (node);
            Assert.Same (node, e.Node);
        }
    }
}
