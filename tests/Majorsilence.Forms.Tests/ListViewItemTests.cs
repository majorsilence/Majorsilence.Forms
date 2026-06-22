// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ListViewItemTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ListViewItemTests, adapted to the
    // Majorsilence.Forms API. Majorsilence.Forms has no Handle/accessibility/Font/Focused/ImageList plumbing,
    // and (unlike WinForms) does not auto-create a sub-item for the item Text, so those upstream
    // cases are omitted. These pin the constructor variants, the Text/Checked/Selected/Tag/Name/
    // ImageIndex/ImageKey round-trips, sub-item population, and parenting (Index/ListView/Parent/Remove).
    public class ListViewItemTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var item = new ListViewItem ();

            Assert.Equal (Rectangle.Empty, item.Bounds);
            Assert.False (item.Checked);
            Assert.Null (item.Group);
            Assert.Equal (-1, item.ImageIndex);
            Assert.Equal (string.Empty, item.ImageKey);
            Assert.Equal (0, item.IndentCount);
            Assert.Equal (-1, item.Index);
            Assert.Null (item.ListView);
            Assert.Null (item.Parent);
            Assert.Empty (item.Name);
            Assert.False (item.Selected);
            Assert.Equal (-1, item.StateImageIndex);
            Assert.Empty (item.SubItems);
            Assert.Same (item.SubItems, item.SubItems);
            Assert.Null (item.Tag);
            Assert.Empty (item.Text);
            Assert.Empty (item.ToolTipText);
            Assert.True (item.UseItemStyleForSubItems);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void Ctor_String (string? text, string expectedText)
        {
            var item = new ListViewItem (text!);

            Assert.Equal (expectedText, item.Text);
            Assert.Equal (-1, item.ImageIndex);
            Assert.Empty (item.ImageKey);
            Assert.Empty (item.SubItems);
            Assert.Equal (-1, item.Index);
            Assert.Null (item.Parent);
        }

        [Fact]
        public void Ctor_StringArray_SetsTextAndSubItems ()
        {
            var item = new ListViewItem (new[] { "text", "sub1", "sub2" });

            Assert.Equal ("text", item.Text);
            Assert.Equal (2, item.SubItems.Count);
            Assert.Equal ("sub1", item.SubItems[0].Text);
            Assert.Equal ("sub2", item.SubItems[1].Text);
        }

        [Fact]
        public void Ctor_StringArray_Empty_SetsEmptyText ()
        {
            var item = new ListViewItem (System.Array.Empty<string> ());

            Assert.Empty (item.Text);
            Assert.Empty (item.SubItems);
        }

        [Fact]
        public void Ctor_StringArray_Single_NoSubItems ()
        {
            var item = new ListViewItem (new[] { "text" });

            Assert.Equal ("text", item.Text);
            Assert.Empty (item.SubItems);
        }

        [Theory]
        [InlineData ("text", -1)]
        [InlineData ("text", 0)]
        [InlineData ("text", 1)]
        public void Ctor_String_Int (string text, int imageIndex)
        {
            var item = new ListViewItem (text, imageIndex);

            Assert.Equal (text, item.Text);
            Assert.Equal (imageIndex, item.ImageIndex);
            Assert.Empty (item.ImageKey);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (2)]
        public void Ctor_StringArray_Int (int imageIndex)
        {
            var item = new ListViewItem (new[] { "text", "sub1" }, imageIndex);

            Assert.Equal ("text", item.Text);
            Assert.Equal (imageIndex, item.ImageIndex);
            Assert.Single (item.SubItems);
            Assert.Equal ("sub1", item.SubItems[0].Text);
        }

        [Theory]
        [InlineData ("text", "")]
        [InlineData ("text", "imageKey")]
        public void Ctor_String_String (string text, string imageKey)
        {
            var item = new ListViewItem (text, imageKey);

            Assert.Equal (text, item.Text);
            Assert.Equal (imageKey, item.ImageKey);
            Assert.Equal (-1, item.ImageIndex);
        }

        [Fact]
        public void Ctor_StringArray_String ()
        {
            var item = new ListViewItem (new[] { "text", "sub1" }, "imageKey");

            Assert.Equal ("text", item.Text);
            Assert.Equal ("imageKey", item.ImageKey);
            Assert.Equal (-1, item.ImageIndex);
            Assert.Single (item.SubItems);
            Assert.Equal ("sub1", item.SubItems[0].Text);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void Text_Set_GetReturnsExpected (string? value, string expected)
        {
            var item = new ListViewItem { Text = value! };

            Assert.Equal (expected, item.Text);

            // Set same.
            item.Text = value!;
            Assert.Equal (expected, item.Text);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Checked_Set_GetReturnsExpected (bool value)
        {
            var item = new ListViewItem { Checked = value };

            Assert.Equal (value, item.Checked);

            // Set same.
            item.Checked = value;
            Assert.Equal (value, item.Checked);

            // Set different.
            item.Checked = !value;
            Assert.Equal (!value, item.Checked);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Selected_Set_GetReturnsExpected (bool value)
        {
            var item = new ListViewItem { Selected = value };

            Assert.Equal (value, item.Selected);

            // Set same.
            item.Selected = value;
            Assert.Equal (value, item.Selected);

            // Set different.
            item.Selected = !value;
            Assert.Equal (!value, item.Selected);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (1)]
        public void ImageIndex_Set_GetReturnsExpected (int value)
        {
            var item = new ListViewItem { ImageIndex = value };

            Assert.Equal (value, item.ImageIndex);

            // Set same.
            item.ImageIndex = value;
            Assert.Equal (value, item.ImageIndex);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("imageKey", "imageKey")]
        public void ImageKey_Set_GetReturnsExpected (string? value, string expected)
        {
            var item = new ListViewItem { ImageKey = value! };

            Assert.Equal (expected, item.ImageKey);

            // Set same.
            item.ImageKey = value!;
            Assert.Equal (expected, item.ImageKey);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("name", "name")]
        public void Name_Set_GetReturnsExpected (string? value, string expected)
        {
            var item = new ListViewItem { Name = value! };

            Assert.Equal (expected, item.Name);
        }

        [Fact]
        public void Tag_Set_GetReturnsExpected ()
        {
            var tag = new object ();
            var item = new ListViewItem { Tag = tag };

            Assert.Same (tag, item.Tag);

            item.Tag = null;
            Assert.Null (item.Tag);
        }

        [Fact]
        public void SubItems_Add_String_ReturnsExpected ()
        {
            var item = new ListViewItem ();

            var subItem = item.SubItems.Add ("sub");

            Assert.Equal ("sub", subItem.Text);
            Assert.Single (item.SubItems);
            Assert.Same (subItem, item.SubItems[0]);
        }

        [Fact]
        public void SubItems_GetText_OutOfRange_ReturnsEmpty ()
        {
            var item = new ListViewItem ();
            item.SubItems.Add ("sub");

            Assert.Equal ("sub", item.SubItems.GetText (0));
            Assert.Equal (string.Empty, item.SubItems.GetText (5));
        }

        [Fact]
        public void Add_ToListView_SetsParentAndIndex ()
        {
            using var listView = new ListView ();
            var item = new ListViewItem ("text");

            listView.Items.Add (item);

            Assert.Same (listView, item.Parent);
            Assert.Same (listView, item.ListView);
            Assert.Equal (0, item.Index);
            Assert.Contains (item, listView.Items);
        }

        [Fact]
        public void Add_String_ToListView_ReturnsItemWithText ()
        {
            using var listView = new ListView ();

            var item = listView.Items.Add ("text");

            Assert.Equal ("text", item.Text);
            Assert.Same (listView, item.Parent);
            Assert.Equal (0, item.Index);
        }

        [Fact]
        public void Index_MultipleItems_ReturnsExpected ()
        {
            using var listView = new ListView ();
            var item1 = new ListViewItem ("a");
            var item2 = new ListViewItem ("b");

            listView.Items.Add (item1);
            listView.Items.Add (item2);

            Assert.Equal (0, item1.Index);
            Assert.Equal (1, item2.Index);
        }

        [Fact]
        public void Remove_HasListView_ClearsParent ()
        {
            using var listView = new ListView ();
            var item = new ListViewItem ("text");
            listView.Items.Add (item);

            item.Remove ();

            Assert.Null (item.Parent);
            Assert.Null (item.ListView);
            Assert.Equal (-1, item.Index);
            Assert.DoesNotContain (item, listView.Items);
        }

        [Fact]
        public void Remove_NoListView_Nop ()
        {
            var item = new ListViewItem ("text");

            item.Remove ();

            Assert.Null (item.Parent);
            Assert.Equal (-1, item.Index);
        }

        [Fact]
        public void EnsureVisible_NoListView_Nop ()
        {
            var item = new ListViewItem ("text");

            // Should not throw.
            item.EnsureVisible ();
        }

        [Fact]
        public void Clone_CopiesStateAndSubItems ()
        {
            var tag = new object ();
            var item = new ListViewItem ("text") {
                Name = "name",
                Checked = true,
                ImageIndex = 2,
                ImageKey = "key",
                Tag = tag
            };
            item.SubItems.Add ("sub1");

            var clone = item.Clone ();

            Assert.Equal ("text", clone.Text);
            Assert.Equal ("name", clone.Name);
            Assert.True (clone.Checked);
            Assert.Equal (2, clone.ImageIndex);
            Assert.Equal ("key", clone.ImageKey);
            Assert.Same (tag, clone.Tag);
            Assert.Single (clone.SubItems);
            Assert.Equal ("sub1", clone.SubItems[0].Text);
            Assert.NotSame (item, clone);
        }
    }
}
