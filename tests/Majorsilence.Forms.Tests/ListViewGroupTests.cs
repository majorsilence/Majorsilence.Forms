// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (ListViewGroupTests.cs under
// src/test/unit/System.Windows.Forms/), rewritten for the Majorsilence.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class ListViewGroupTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var group = new ListViewGroup ();

            Assert.Equal (string.Empty, group.Header);
            Assert.Equal (string.Empty, group.Name);
            Assert.Equal (HorizontalAlignment.Left, group.HeaderAlignment);
            Assert.Null (group.Tag);
            Assert.NotNull (group.Items);
            Assert.Empty (group.Items);
        }

        [Fact]
        public void Ctor_Header ()
        {
            var group = new ListViewGroup ("My Group");
            Assert.Equal ("My Group", group.Header);
            Assert.Equal (string.Empty, group.Name);
        }

        [Fact]
        public void Ctor_KeyHeader ()
        {
            var group = new ListViewGroup ("key1", "My Group");
            Assert.Equal ("key1", group.Name);
            Assert.Equal ("My Group", group.Header);
        }

        [Theory]
        [InlineData (HorizontalAlignment.Left)]
        [InlineData (HorizontalAlignment.Center)]
        [InlineData (HorizontalAlignment.Right)]
        public void HeaderAlignment_Set_GetReturnsExpected (HorizontalAlignment value)
        {
            var group = new ListViewGroup { HeaderAlignment = value };
            Assert.Equal (value, group.HeaderAlignment);
        }

        [Fact]
        public void Header_Name_Tag_RoundTrip ()
        {
            var tag = new object ();
            var group = new ListViewGroup { Header = "h", Name = "n", Tag = tag };

            Assert.Equal ("h", group.Header);
            Assert.Equal ("n", group.Name);
            Assert.Same (tag, group.Tag);
        }

        [Fact]
        public void Items_CanAddListViewItems ()
        {
            var group = new ListViewGroup ();
            var item = new ListViewItem ("a");

            group.Items.Add (item);

            Assert.Single (group.Items);
            Assert.Same (item, group.Items[0]);
        }

        [Fact]
        public void Collection_Add_Header ()
        {
            var groups = new ListViewGroupCollection ();
            var g = groups.Add ("Group A");

            Assert.Equal (1, groups.Count);
            Assert.Equal ("Group A", g.Header);
            Assert.Same (g, groups[0]);
        }

        [Fact]
        public void Collection_Add_KeyHeader ()
        {
            var groups = new ListViewGroupCollection ();
            var g = groups.Add ("key", "Group A");

            Assert.Equal ("key", g.Name);
            Assert.Equal ("Group A", g.Header);
        }

        [Fact]
        public void Collection_AddRemoveClear ()
        {
            var groups = new ListViewGroupCollection ();
            var a = groups.Add ("A");
            groups.Add ("B");

            Assert.Equal (2, groups.Count);

            groups.Remove (a);
            Assert.Equal (1, groups.Count);

            groups.Clear ();
            Assert.Empty (groups);
        }
    }
}
