// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (ColumnHeaderTests.cs under
// src/test/unit/System.Windows.Forms/), rewritten for the Continuum.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using Xunit;

namespace Continuum.Forms.Tests
{
    public class ColumnHeaderTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            var header = new ColumnHeader ();

            Assert.Equal (string.Empty, header.Text);
            Assert.Equal (60, header.Width);
            Assert.Equal (HorizontalAlignment.Left, header.TextAlign);
            Assert.Equal (-1, header.Index);
            Assert.Equal (string.Empty, header.Name);
            Assert.Null (header.Tag);
            Assert.Equal (-1, header.DisplayIndex);
            Assert.Equal (-1, header.ImageIndex);
            Assert.Equal (string.Empty, header.ImageKey);
            Assert.Null (header.ListView);
        }

        [Theory]
        [InlineData ("Name")]
        [InlineData ("")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            var header = new ColumnHeader { Text = value };
            Assert.Equal (value, header.Text);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (100)]
        [InlineData (-1)]
        public void Width_Set_GetReturnsExpected (int value)
        {
            var header = new ColumnHeader { Width = value };
            Assert.Equal (value, header.Width);
        }

        [Theory]
        [InlineData (HorizontalAlignment.Left)]
        [InlineData (HorizontalAlignment.Center)]
        [InlineData (HorizontalAlignment.Right)]
        public void TextAlign_Set_GetReturnsExpected (HorizontalAlignment value)
        {
            var header = new ColumnHeader { TextAlign = value };
            Assert.Equal (value, header.TextAlign);
        }

        [Fact]
        public void Name_Tag_DisplayIndex_RoundTrip ()
        {
            var tag = new object ();
            var header = new ColumnHeader { Name = "col1", Tag = tag, DisplayIndex = 3 };

            Assert.Equal ("col1", header.Name);
            Assert.Same (tag, header.Tag);
            Assert.Equal (3, header.DisplayIndex);
        }

        [Fact]
        public void ImageIndex_ImageKey_RoundTrip ()
        {
            var header = new ColumnHeader { ImageIndex = 2, ImageKey = "img" };
            Assert.Equal (2, header.ImageIndex);
            Assert.Equal ("img", header.ImageKey);
        }

        [Fact]
        public void AutoResize_DoesNotThrow ()
        {
            var header = new ColumnHeader ();
            header.AutoResize (ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        [Fact]
        public void Collection_Add_Text_ReturnsHeaderWithIndex ()
        {
            var columns = new ColumnHeaderCollection ();

            var a = columns.Add ("A");
            var b = columns.Add ("B");

            Assert.Equal (2, columns.Count);
            Assert.Equal ("A", a.Text);
            Assert.Equal (0, a.Index);
            Assert.Equal ("B", b.Text);
            Assert.Equal (1, b.Index);
            Assert.Same (a, columns[0]);
            Assert.Same (b, columns[1]);
        }

        [Fact]
        public void Collection_Add_TextWidth_SetsWidth ()
        {
            var columns = new ColumnHeaderCollection ();
            var h = columns.Add ("A", 150);

            Assert.Equal ("A", h.Text);
            Assert.Equal (150, h.Width);
        }

        [Fact]
        public void Collection_AddHeader_SetsIndex ()
        {
            var columns = new ColumnHeaderCollection ();
            var h = new ColumnHeader { Text = "X" };

            columns.Add (h);

            Assert.Equal (1, columns.Count);
            Assert.Equal (0, h.Index);
            Assert.Same (h, columns[0]);
        }

        [Fact]
        public void Collection_Insert_AssignsIndex ()
        {
            var columns = new ColumnHeaderCollection ();
            columns.Add ("A");
            var inserted = new ColumnHeader { Text = "Z" };

            columns.Insert (0, inserted);

            Assert.Equal (0, inserted.Index);
            Assert.Same (inserted, columns[0]);
        }

        [Fact]
        public void Collection_RemoveAndClear ()
        {
            var columns = new ColumnHeaderCollection ();
            var a = columns.Add ("A");
            columns.Add ("B");

            columns.Remove (a);
            Assert.Equal (1, columns.Count);
            Assert.DoesNotContain (a, columns);

            columns.Clear ();
            Assert.Empty (columns);
        }
    }
}
