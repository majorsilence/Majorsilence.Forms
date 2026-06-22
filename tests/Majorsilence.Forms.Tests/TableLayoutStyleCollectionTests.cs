// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/TableLayoutStyleCollectionTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Collections;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms TableLayoutStyleCollectionTests, adapted
    // to the Majorsilence.Forms API. Majorsilence.Forms exposes the style collections directly on
    // TableLayoutPanel (ColumnStyles / RowStyles) rather than via ToolStrip.LayoutSettings, so the
    // collections are obtained from a TableLayoutPanel here. The value-type semantics of
    // ColumnStyle / RowStyle themselves are covered by TableLayoutTypeTests and are not duplicated.
    public class TableLayoutStyleCollectionTests
    {
        // -- RowStyles ----------------------------------------------------------------

        [Fact]
        public void RowStyles_GetDefault_IsEmpty ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            Assert.Equal (0, collection.Count);
            Assert.False (((IList)collection).IsFixedSize);
            Assert.False (((IList)collection).IsReadOnly);
        }

        [Fact]
        public void RowStyles_Add_RowStyle_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            var index = collection.Add (style);

            Assert.Equal (0, index);
            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void RowStyles_Add_ReturnsIncreasingIndex ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            Assert.Equal (0, collection.Add (new RowStyle ()));
            Assert.Equal (1, collection.Add (new RowStyle ()));
            Assert.Equal (2, collection.Add (new RowStyle ()));
            Assert.Equal (3, collection.Count);
        }

        [Fact]
        public void RowStyles_Add_Object_Success ()
        {
            using var panel = new TableLayoutPanel ();
            IList collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void RowStyles_Add_Null_ThrowsArgumentNullException ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            Assert.Throws<ArgumentNullException> (() => collection.Add ((RowStyle)null!));
            Assert.Throws<ArgumentNullException> (() => ((IList)collection).Add (null));
        }

        [Fact]
        public void RowStyles_Add_StyleAlreadyAdded_ThrowsArgumentException ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            Assert.Throws<ArgumentException> (() => collection.Add (style));
            Assert.Throws<ArgumentException> (() => ((IList)collection).Add (style));
        }

        [Fact]
        public void RowStyles_Insert_Object_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var first = new RowStyle ();
            var inserted = new RowStyle ();
            collection.Add (first);
            collection.Insert (0, inserted);

            Assert.Equal (2, collection.Count);
            Assert.Same (inserted, collection[0]);
            Assert.Same (first, collection[1]);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        public void RowStyles_Insert_Null_ThrowsArgumentNullException (int index)
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            Assert.Throws<ArgumentNullException> (() => ((IList)collection).Insert (index, null));
        }

        [Fact]
        public void RowStyles_Insert_StyleAlreadyAdded_ThrowsArgumentException ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            Assert.Throws<ArgumentException> (() => collection.Insert (0, style));
        }

        [Fact]
        public void RowStyles_Item_SetRowStyle_GetReturnsExpected ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;
            collection.Add (new RowStyle ());

            var style = new RowStyle ();
            collection[0] = style;

            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        public void RowStyles_Item_SetNull_ThrowsArgumentNullException (int index)
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;
            collection.Add (new RowStyle ());

            Assert.Throws<ArgumentNullException> (() => collection[index] = null!);
        }

        [Fact]
        public void RowStyles_Item_StyleAlreadyAdded_ThrowsArgumentException ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            Assert.Throws<ArgumentException> (() => collection[0] = style);
        }

        [Fact]
        public void RowStyles_Remove_Object_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);
            collection.Remove (style);
            Assert.Equal (0, collection.Count);

            // After removal the style is unowned, so it can be re-added.
            collection.Add (style);
            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void RowStyles_Remove_Null_Nop ()
        {
            using var panel = new TableLayoutPanel ();
            IList collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);
            collection.Remove (null);

            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void RowStyles_RemoveAt_Invoke_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);
            collection.RemoveAt (0);
            Assert.Equal (0, collection.Count);

            collection.Add (style);
            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void RowStyles_Clear_Invoke_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);
            collection.Clear ();
            Assert.Equal (0, collection.Count);

            // Clearing an empty collection is a no-op.
            collection.Clear ();
            Assert.Equal (0, collection.Count);

            // Cleared styles are unowned, so they can be re-added.
            collection.Add (style);
            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void RowStyles_Contains_Object_ReturnsExpected ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            Assert.True (collection.Contains (style));
            Assert.False (collection.Contains (new RowStyle ()));
            Assert.False (((IList)collection).Contains (null));
        }

        [Fact]
        public void RowStyles_IndexOf_Object_ReturnsExpected ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            Assert.Equal (0, collection.IndexOf (style));
            Assert.Equal (-1, collection.IndexOf (new RowStyle ()));
            Assert.Equal (-1, ((IList)collection).IndexOf (null));
        }

        [Fact]
        public void RowStyles_CopyTo_Invoke_Success ()
        {
            using var panel = new TableLayoutPanel ();
            IList collection = panel.RowStyles;

            var style = new RowStyle ();
            collection.Add (style);

            var array = new object[] { 1, 2, 3 };
            collection.CopyTo (array, 1);

            Assert.Equal (1, array[0]);
            Assert.Same (style, array[1]);
            Assert.Equal (3, array[2]);
        }

        // -- ColumnStyles -------------------------------------------------------------

        [Fact]
        public void ColumnStyles_GetDefault_IsEmpty ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            Assert.Equal (0, collection.Count);
            Assert.False (((IList)collection).IsFixedSize);
            Assert.False (((IList)collection).IsReadOnly);
        }

        [Fact]
        public void ColumnStyles_Add_ColumnStyle_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var style = new ColumnStyle ();
            var index = collection.Add (style);

            Assert.Equal (0, index);
            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void ColumnStyles_Add_ReturnsIncreasingIndex ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            Assert.Equal (0, collection.Add (new ColumnStyle ()));
            Assert.Equal (1, collection.Add (new ColumnStyle ()));
            Assert.Equal (2, collection.Add (new ColumnStyle ()));
            Assert.Equal (3, collection.Count);
        }

        [Fact]
        public void ColumnStyles_Add_Null_ThrowsArgumentNullException ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            Assert.Throws<ArgumentNullException> (() => collection.Add ((ColumnStyle)null!));
            Assert.Throws<ArgumentNullException> (() => ((IList)collection).Add (null));
        }

        [Fact]
        public void ColumnStyles_Add_StyleAlreadyAdded_ThrowsArgumentException ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var style = new ColumnStyle ();
            collection.Add (style);

            Assert.Throws<ArgumentException> (() => collection.Add (style));
        }

        [Fact]
        public void ColumnStyles_Insert_Object_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var first = new ColumnStyle ();
            var inserted = new ColumnStyle ();
            collection.Add (first);
            collection.Insert (0, inserted);

            Assert.Equal (2, collection.Count);
            Assert.Same (inserted, collection[0]);
            Assert.Same (first, collection[1]);
        }

        [Fact]
        public void ColumnStyles_Item_SetColumnStyle_GetReturnsExpected ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;
            collection.Add (new ColumnStyle ());

            var style = new ColumnStyle ();
            collection[0] = style;

            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void ColumnStyles_Remove_Object_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var style = new ColumnStyle ();
            collection.Add (style);
            collection.Remove (style);
            Assert.Equal (0, collection.Count);

            collection.Add (style);
            Assert.Equal (1, collection.Count);
            Assert.Same (style, collection[0]);
        }

        [Fact]
        public void ColumnStyles_RemoveAt_Invoke_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var style = new ColumnStyle ();
            collection.Add (style);
            collection.RemoveAt (0);
            Assert.Equal (0, collection.Count);
        }

        [Fact]
        public void ColumnStyles_Clear_Invoke_Success ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            collection.Add (new ColumnStyle ());
            collection.Add (new ColumnStyle ());
            collection.Clear ();

            Assert.Equal (0, collection.Count);
        }

        [Fact]
        public void ColumnStyles_Contains_Object_ReturnsExpected ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var style = new ColumnStyle ();
            collection.Add (style);

            Assert.True (collection.Contains (style));
            Assert.False (collection.Contains (new ColumnStyle ()));
        }

        [Fact]
        public void ColumnStyles_IndexOf_Object_ReturnsExpected ()
        {
            using var panel = new TableLayoutPanel ();
            var collection = panel.ColumnStyles;

            var style = new ColumnStyle ();
            collection.Add (style);

            Assert.Equal (0, collection.IndexOf (style));
            Assert.Equal (-1, collection.IndexOf (new ColumnStyle ()));
        }
    }
}
