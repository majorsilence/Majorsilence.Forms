// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ComboBoxTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ComboBoxTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility plumbing). They pin the
    // default property values, the Items collection behavior, SelectedIndex/SelectedItem
    // coercion and validation, Text coupling, FindString[Exact] lookup, Sorted ordering,
    // and the SelectedIndexChanged event.
    public class ComboBoxTests
    {
        private static ComboBox CreateWithItems (int count)
        {
            var control = new ComboBox ();

            for (var i = 0; i < count; i++)
                control.Items.Add ($"item{i}");

            return control;
        }

        [Fact]
        public void Ctor_Default ()
        {
            using var control = new ComboBox ();

            Assert.Equal (ComboBoxStyle.DropDown, control.DropDownStyle);
            Assert.Empty (control.Items);
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
            Assert.Equal (string.Empty, control.Text);
            Assert.Empty (control.DisplayMember);
            Assert.Empty (control.ValueMember);
            Assert.Null (control.DataSource);
            Assert.False (control.Sorted);
            Assert.False (control.DroppedDown);
            Assert.False (control.FormattingEnabled);
            Assert.Equal (8, control.MaxDropDownItems);
            Assert.Equal (0, control.MaxLength);
            Assert.True (control.IntegralHeight);
            Assert.Equal (106, control.DropDownHeight);
            Assert.Equal (DrawMode.Normal, control.DrawMode);
            Assert.Equal (AutoCompleteMode.None, control.AutoCompleteMode);
            Assert.Equal (AutoCompleteSource.None, control.AutoCompleteSource);
            Assert.NotNull (control.AutoCompleteCustomSource);
        }

        [Fact]
        public void Items_SameInstance ()
        {
            using var control = new ComboBox ();

            // Items must return the same backing collection each access.
            Assert.Same (control.Items, control.Items);
        }

        [Theory]
        [InlineData (ComboBoxStyle.DropDown)]
        [InlineData (ComboBoxStyle.DropDownList)]
        [InlineData (ComboBoxStyle.Simple)]
        public void DropDownStyle_Set_GetReturnsExpected (ComboBoxStyle value)
        {
            using var control = new ComboBox { DropDownStyle = value };

            Assert.Equal (value, control.DropDownStyle);

            // Set same.
            control.DropDownStyle = value;
            Assert.Equal (value, control.DropDownStyle);
        }

        [Fact]
        public void Items_Add_AppendsAndCounts ()
        {
            using var control = new ComboBox ();

            control.Items.Add ("a");
            control.Items.Add ("b");

            Assert.Equal (2, control.Items.Count);
            Assert.Equal ("a", control.Items[0]);
            Assert.Equal ("b", control.Items[1]);
        }

        [Fact]
        public void Items_AddRange_AppendsAll ()
        {
            using var control = new ComboBox ();

            control.Items.AddRange ("a", "b", "c");

            Assert.Equal (3, control.Items.Count);
            Assert.Equal ("a", control.Items[0]);
            Assert.Equal ("c", control.Items[2]);
        }

        [Fact]
        public void Items_Insert_PlacesAtIndex ()
        {
            using var control = CreateWithItems (3);

            control.Items.Insert (1, "inserted");

            Assert.Equal (4, control.Items.Count);
            Assert.Equal ("inserted", control.Items[1]);
            Assert.Equal ("item1", control.Items[2]);
        }

        [Theory]
        [InlineData (10, 0, 5)]
        [InlineData (10, 2, 4)]
        public void Items_RemoveAt_DecrementsCount (int numberOfItems, int index, int numberOfItemsToRemove)
        {
            using var control = CreateWithItems (numberOfItems);

            var count = control.Items.Count;

            for (var i = 0; i < numberOfItemsToRemove; i++)
                control.Items.RemoveAt (index);

            Assert.Equal (count - numberOfItemsToRemove, control.Items.Count);
        }

        [Theory]
        [InlineData (10, 0, 5)]
        [InlineData (10, 2, 4)]
        public void Items_RemoveByObject_DecrementsCount (int numberOfItems, int index, int number)
        {
            using var control = CreateWithItems (numberOfItems);

            var count = control.Items.Count;

            for (var i = 0; i < number; i++)
                control.Items.Remove (control.Items[index]);

            Assert.Equal (count - number, control.Items.Count);
        }

        [Fact]
        public void Items_Clear_EmptiesCollection ()
        {
            using var control = CreateWithItems (5);

            control.Items.Clear ();

            Assert.Empty (control.Items);
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
        }

        [Fact]
        public void Items_IndexOf_ReturnsExpected ()
        {
            using var control = CreateWithItems (3);

            Assert.Equal (2, control.Items.IndexOf ("item2"));
            Assert.Equal (-1, control.Items.IndexOf ("missing"));
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (9)]
        public void SelectedIndex_Set_GetReturnsExpected (int value)
        {
            using var control = CreateWithItems (10);

            control.SelectedIndex = value;

            Assert.Equal (value, control.SelectedIndex);
            Assert.Equal (control.Items[value], control.SelectedItem);
        }

        [Fact]
        public void SelectedIndex_SetNegativeOne_Clears ()
        {
            using var control = CreateWithItems (5);
            control.SelectedIndex = 2;

            control.SelectedIndex = -1;

            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
        }

        [Fact]
        public void SelectedIndex_SetNegativeOneWhenEmpty_DoesNotThrow ()
        {
            using var control = new ComboBox ();

            control.SelectedIndex = -1;

            Assert.Equal (-1, control.SelectedIndex);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (5)]
        public void SelectedIndex_SetOutOfRange_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new ComboBox ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = value);
        }

        [Fact]
        public void SelectedIndex_SetBelowNegativeOne_ThrowsArgumentOutOfRangeException ()
        {
            using var control = CreateWithItems (3);

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = -2);
        }

        [Fact]
        public void SelectedItem_Set_GetReturnsExpected ()
        {
            using var control = CreateWithItems (5);

            control.SelectedItem = "item3";

            Assert.Equal (3, control.SelectedIndex);
            Assert.Equal ("item3", control.SelectedItem);
        }

        [Fact]
        public void SelectedItem_SetNull_Clears ()
        {
            using var control = CreateWithItems (5);
            control.SelectedIndex = 1;

            control.SelectedItem = null;

            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
        }

        [Fact]
        public void SelectedItem_SetNotInList_ThrowsArgumentException ()
        {
            using var control = CreateWithItems (5);

            Assert.Throws<ArgumentException> (() => control.SelectedItem = "missing");
        }

        [Fact]
        public void SelectedItem_HandlesItemRemoval ()
        {
            using var control = CreateWithItems (5);
            control.SelectedIndex = 4;

            control.Items.RemoveAt (4);

            // Removing the selected item must not leave a dangling index.
            Assert.Equal (4, control.Items.Count);
            Assert.True (control.SelectedIndex < control.Items.Count);
        }

        [Fact]
        public void Text_NoSelection_ReturnsBaseText ()
        {
            using var control = new ComboBox ();

            Assert.Equal (string.Empty, control.Text);

            control.Text = "free text";
            Assert.Equal ("free text", control.Text);
            Assert.Equal (-1, control.SelectedIndex);
        }

        [Fact]
        public void Text_MatchingItem_SelectsItem ()
        {
            using var control = CreateWithItems (5);

            control.Text = "item3";

            Assert.Equal (3, control.SelectedIndex);
            Assert.Equal ("item3", control.Text);
        }

        [Fact]
        public void Text_WhenSelected_ReturnsItemText ()
        {
            using var control = CreateWithItems (5);

            control.SelectedIndex = 2;

            Assert.Equal ("item2", control.Text);
        }

        [Theory]
        [InlineData ("item0", -1, 0)]
        [InlineData ("item1", -1, 1)]
        [InlineData ("ITEM7", -1, 7)]
        [InlineData ("missing", -1, -1)]
        public void FindStringExact_ReturnsExpected (string value, int startIndex, int expected)
        {
            using var control = CreateWithItems (10);

            Assert.Equal (expected, control.FindStringExact (value, startIndex));
        }

        [Theory]
        [InlineData ("item", -1, 0)]
        [InlineData ("ITEM5", -1, 5)]
        [InlineData ("nope", -1, -1)]
        public void FindString_ReturnsExpected (string value, int startIndex, int expected)
        {
            using var control = CreateWithItems (10);

            Assert.Equal (expected, control.FindString (value, startIndex));
        }

        [Fact]
        public void FindString_EmptyCollection_ReturnsNegativeOne ()
        {
            using var control = new ComboBox ();

            Assert.Equal (-1, control.FindString ("anything"));
            Assert.Equal (-1, control.FindStringExact ("anything"));
        }

        [Fact]
        public void Sorted_Set_SortsItemsAscending ()
        {
            using var control = new ComboBox ();
            control.Items.AddRange ("banana", "apple", "cherry");

            control.Sorted = true;

            Assert.True (control.Sorted);
            Assert.Equal ("apple", control.Items[0]);
            Assert.Equal ("banana", control.Items[1]);
            Assert.Equal ("cherry", control.Items[2]);
        }

        [Fact]
        public void Sorted_SetPreservesSelection ()
        {
            using var control = new ComboBox ();
            control.Items.AddRange ("banana", "apple", "cherry");
            control.SelectedItem = "banana";

            control.Sorted = true;

            Assert.Equal ("banana", control.SelectedItem);
            Assert.Equal (1, control.SelectedIndex);
        }

        [Fact]
        public void SelectedIndexChanged_FiresOnChange ()
        {
            using var control = CreateWithItems (10);

            var callCount = 0;
            control.SelectedIndexChanged += (s, e) => callCount++;

            control.SelectedIndex = 3;
            Assert.Equal (1, callCount);
            Assert.Equal (3, control.SelectedIndex);

            // Setting the same index again does not re-fire.
            control.SelectedIndex = 3;
            Assert.Equal (1, callCount);

            // Changing to a new index fires again.
            control.SelectedIndex = 5;
            Assert.Equal (2, callCount);
        }

        [Fact]
        public void SelectedIndexChanged_NotFiredWhenSetSameOnEmpty ()
        {
            using var control = new ComboBox ();

            var callCount = 0;
            control.SelectedIndexChanged += (s, e) => callCount++;

            control.SelectedIndex = -1;

            Assert.Equal (0, callCount);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (int.MaxValue)]
        public void MaxLength_Set_GetReturnsExpected (int value)
        {
            using var control = new ComboBox { MaxLength = value };

            Assert.Equal (value, control.MaxLength);
        }

        [Fact]
        public void BeginEndUpdate_DoesNotThrow ()
        {
            using var control = new ComboBox ();

            control.BeginUpdate ();
            control.Items.Add ("a");
            control.EndUpdate ();

            Assert.Single (control.Items);

            // EndUpdate without a matching BeginUpdate should not throw.
            using var control2 = new ComboBox ();
            control2.EndUpdate ();
        }

        [Fact]
        public void SelectedText_NoSelection_ReturnsEmpty ()
        {
            using var control = new ComboBox ();

            Assert.Equal (string.Empty, control.SelectedText);

            // Setting SelectedText is a no-op stub in Continuum.Forms.
            control.SelectedText = "Test";
            Assert.Equal (string.Empty, control.SelectedText);
        }
    }
}
