// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ListBoxTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ListBoxTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility plumbing). They pin the
    // Items collection semantics, SelectedIndex get/set + coercion, SelectedItem,
    // SelectionMode, SelectedIndices/SelectedItems, ClearSelected, and the
    // SelectedIndexChanged event.
    public class ListBoxTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new ListBox ();

            Assert.Empty (control.Items);
            Assert.Equal (0, control.Items.Count);
            Assert.Equal (SelectionMode.One, control.SelectionMode);
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
            Assert.Empty (control.SelectedIndices);
            Assert.Empty (control.SelectedItems);
            Assert.Equal (new Size (120, 96), control.Size);
        }

        // ----- Items collection -----

        [Fact]
        public void Items_Add_AppendsAndIncrementsCount ()
        {
            using var control = new ListBox ();

            control.Items.Add ("item1");
            control.Items.Add ("item2");

            Assert.Equal (2, control.Items.Count);
            Assert.Equal ("item1", control.Items[0]);
            Assert.Equal ("item2", control.Items[1]);
        }

        [Fact]
        public void Items_AddRange_AppendsAll ()
        {
            using var control = new ListBox ();

            control.Items.AddRange ("a", "b", "c");

            Assert.Equal (3, control.Items.Count);
            Assert.Equal ("a", control.Items[0]);
            Assert.Equal ("c", control.Items[2]);
        }

        [Fact]
        public void Items_Insert_InsertsAtIndex ()
        {
            using var control = new ListBox ();
            control.Items.Add ("a");
            control.Items.Add ("c");

            control.Items.Insert (1, "b");

            Assert.Equal (3, control.Items.Count);
            Assert.Equal ("a", control.Items[0]);
            Assert.Equal ("b", control.Items[1]);
            Assert.Equal ("c", control.Items[2]);
        }

        [Fact]
        public void Items_Remove_RemovesItem ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");

            control.Items.Remove ("b");

            Assert.Equal (2, control.Items.Count);
            Assert.Equal ("a", control.Items[0]);
            Assert.Equal ("c", control.Items[1]);
        }

        [Fact]
        public void Items_RemoveAt_RemovesItem ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");

            control.Items.RemoveAt (0);

            Assert.Equal (2, control.Items.Count);
            Assert.Equal ("b", control.Items[0]);
        }

        [Fact]
        public void Items_Clear_RemovesAll ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");

            control.Items.Clear ();

            Assert.Equal (0, control.Items.Count);
            Assert.Empty (control.Items);
        }

        [Fact]
        public void Items_Indexer_GetSet ()
        {
            using var control = new ListBox ();
            control.Items.Add ("a");

            Assert.Equal ("a", control.Items[0]);

            control.Items[0] = "b";
            Assert.Equal ("b", control.Items[0]);
        }

        [Fact]
        public void Items_ContainsAndIndexOf ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");

            Assert.True (control.Items.Contains ("b"));
            Assert.False (control.Items.Contains ("z"));
            Assert.Equal (2, control.Items.IndexOf ("c"));
            Assert.Equal (-1, control.Items.IndexOf ("z"));
        }

        // ----- SelectedIndex -----

        [Fact]
        public void SelectedIndex_GetEmpty_ReturnsMinusOne ()
        {
            using var control = new ListBox ();

            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
            Assert.Empty (control.SelectedIndices);
            Assert.Empty (control.SelectedItems);
        }

        [Fact]
        public void SelectedIndex_SetEmptyMinusOne_Nop ()
        {
            using var control = new ListBox ();

            control.SelectedIndex = -1;

            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
            Assert.Empty (control.SelectedIndices);
            Assert.Empty (control.SelectedItems);
        }

        [Fact]
        public void SelectedIndex_SetSelectionModeOne_GetReturnsExpected ()
        {
            using var control = new ListBox ();
            control.Items.Add ("item1");
            control.Items.Add ("item2");
            control.Items.Add ("item1");

            // Select middle.
            control.SelectedIndex = 1;
            Assert.Equal (1, control.SelectedIndex);
            Assert.Equal ("item2", control.SelectedItem);
            Assert.Equal (new[] { 1 }, control.SelectedIndices);
            Assert.Equal (new object[] { "item2" }, control.SelectedItems);

            // Select same.
            control.SelectedIndex = 1;
            Assert.Equal (1, control.SelectedIndex);
            Assert.Equal ("item2", control.SelectedItem);

            // Select first.
            control.SelectedIndex = 0;
            Assert.Equal (0, control.SelectedIndex);
            Assert.Equal ("item1", control.SelectedItem);
            Assert.Equal (new[] { 0 }, control.SelectedIndices);

            // Clear selection.
            control.SelectedIndex = -1;
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedItem);
            Assert.Empty (control.SelectedIndices);
            Assert.Empty (control.SelectedItems);
        }

        [Theory]
        [InlineData (-2)]
        [InlineData (0)]
        [InlineData (1)]
        public void SelectedIndex_SetInvalidValueEmpty_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new ListBox ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = value);
        }

        [Theory]
        [InlineData (-2)]
        [InlineData (1)]
        public void SelectedIndex_SetInvalidValueNotEmpty_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new ListBox ();
            control.Items.Add ("Item");

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = value);
        }

        [Fact]
        public void SelectedIndex_SetSelectionModeNone_ThrowsArgumentException ()
        {
            using var control = new ListBox { SelectionMode = SelectionMode.None };
            control.Items.Add ("Item1");

            Assert.Throws<ArgumentException> (() => control.SelectedIndex = 0);
        }

        [Theory]
        [InlineData (SelectionMode.One)]
        [InlineData (SelectionMode.MultiSimple)]
        [InlineData (SelectionMode.MultiExtended)]
        public void SelectedIndex_SetSelectionModeValid_DoesNotThrow (SelectionMode selectionMode)
        {
            using var control = new ListBox { SelectionMode = selectionMode };
            control.Items.Add ("Item1");

            control.SelectedIndex = 0;
            Assert.Equal (0, control.SelectedIndex);
        }

        // ----- SelectedItem -----

        [Fact]
        public void SelectedItem_SetSelectionModeOne_GetReturnsExpected ()
        {
            using var control = new ListBox ();
            control.Items.Add ("item1");
            control.Items.Add ("item2");

            control.SelectedItem = "item2";
            Assert.Equal ("item2", control.SelectedItem);
            Assert.Equal (1, control.SelectedIndex);

            control.SelectedItem = "item1";
            Assert.Equal ("item1", control.SelectedItem);
            Assert.Equal (0, control.SelectedIndex);
        }

        [Fact]
        public void SelectedItem_SetNull_ClearsSelection ()
        {
            using var control = new ListBox ();
            control.Items.Add ("item1");
            control.SelectedIndex = 0;

            control.SelectedItem = null;

            Assert.Null (control.SelectedItem);
            Assert.Equal (-1, control.SelectedIndex);
        }

        [Fact]
        public void SelectedItem_SetItemNotInList_ThrowsArgumentException ()
        {
            using var control = new ListBox ();
            control.Items.Add ("item1");

            Assert.Throws<ArgumentException> (() => control.SelectedItem = "NoSuchItem");
        }

        // ----- SelectionMode -----

        [Theory]
        [InlineData (SelectionMode.None)]
        [InlineData (SelectionMode.One)]
        [InlineData (SelectionMode.MultiSimple)]
        [InlineData (SelectionMode.MultiExtended)]
        public void SelectionMode_Set_GetReturnsExpected (SelectionMode value)
        {
            using var control = new ListBox { SelectionMode = value };

            Assert.Equal (value, control.SelectionMode);

            // Set same.
            control.SelectionMode = value;
            Assert.Equal (value, control.SelectionMode);
        }

        [Fact]
        public void SelectionMode_SetToNone_ClearsSelectedIndex ()
        {
            using var control = new ListBox ();
            control.Items.Add ("item1");
            control.Items.Add ("item2");
            control.SelectedIndex = 1;

            control.SelectionMode = SelectionMode.None;

            Assert.Equal (SelectionMode.None, control.SelectionMode);
            Assert.Equal (-1, control.SelectedIndex);
        }

        [Theory]
        [InlineData ((SelectionMode)(-1))]
        [InlineData ((SelectionMode)4)]
        public void SelectionMode_SetInvalid_ThrowsInvalidEnumArgumentException (SelectionMode value)
        {
            using var control = new ListBox ();

            Assert.Throws<InvalidEnumArgumentException> (() => control.SelectionMode = value);
        }

        // ----- ClearSelected / SetSelected / GetSelected -----

        [Fact]
        public void ClearSelected_DeselectsAll ()
        {
            using var control = new ListBox { SelectionMode = SelectionMode.MultiSimple };
            control.Items.AddRange ("a", "b", "c");
            control.SetSelected (0, true);
            control.SetSelected (2, true);
            Assert.Equal (2, control.SelectedIndices.Count ());

            control.ClearSelected ();

            Assert.Empty (control.SelectedIndices);
            Assert.Empty (control.SelectedItems);
            Assert.Equal (-1, control.SelectedIndex);
        }

        [Fact]
        public void SetSelected_GetSelected_RoundTrips ()
        {
            using var control = new ListBox { SelectionMode = SelectionMode.MultiSimple };
            control.Items.AddRange ("a", "b", "c");

            control.SetSelected (1, true);
            Assert.True (control.GetSelected (1));
            Assert.False (control.GetSelected (0));

            control.SetSelected (1, false);
            Assert.False (control.GetSelected (1));
        }

        [Fact]
        public void SetSelected_OutOfRange_Ignored ()
        {
            using var control = new ListBox ();
            control.Items.Add ("a");

            control.SetSelected (5, true);

            Assert.Empty (control.SelectedIndices);
        }

        // ----- SelectedIndexChanged event -----

        [Fact]
        public void SelectedIndex_SetWithHandler_CallsSelectedIndexChanged ()
        {
            using var control = new ListBox ();
            control.Items.Add ("item1");
            control.Items.Add ("item2");
            control.Items.Add ("item1");

            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.SelectedIndexChanged += handler;

            // Select.
            control.SelectedIndex = 1;
            Assert.Equal (1, control.SelectedIndex);
            Assert.Equal (1, callCount);

            // Select same - no event.
            control.SelectedIndex = 1;
            Assert.Equal (1, callCount);

            // Select different.
            control.SelectedIndex = 0;
            Assert.Equal (2, callCount);

            // Clear selection.
            control.SelectedIndex = -1;
            Assert.Equal (3, callCount);

            // Remove handler.
            control.SelectedIndexChanged -= handler;
            control.SelectedIndex = 1;
            Assert.Equal (3, callCount);
        }

        // ----- Removing items keeps selection in sync -----

        [Fact]
        public void Remove_SelectedItem_ClearsSelection ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("1", "2", "3");

            control.SelectedItem = control.Items[0];
            Assert.Equal (0, control.SelectedIndex);
            Assert.Single (control.SelectedIndices);

            control.Items.Remove (control.Items[0]);

            Assert.Null (control.SelectedItem);
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Empty (control.SelectedIndices);
            Assert.Empty (control.SelectedItems);
        }

        [Fact]
        public void Remove_ItemBeforeSelection_ShiftsSelectedIndex ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");
            control.SelectedIndex = 2;
            Assert.Equal ("c", control.SelectedItem);

            // Removing an item before the selection shifts the index down so it
            // continues to point at the same item.
            control.Items.RemoveAt (0);

            Assert.Equal (1, control.SelectedIndex);
            Assert.Equal ("c", control.SelectedItem);
        }

        [Fact]
        public void Insert_ItemBeforeSelection_ShiftsSelectedIndex ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");
            control.SelectedIndex = 1;
            Assert.Equal ("b", control.SelectedItem);

            control.Items.Insert (0, "z");

            Assert.Equal (2, control.SelectedIndex);
            Assert.Equal ("b", control.SelectedItem);
        }

        [Fact]
        public void Clear_RemovesSelection ()
        {
            using var control = new ListBox ();
            control.Items.AddRange ("a", "b", "c");
            control.SelectedIndex = 1;

            control.Items.Clear ();

            Assert.Equal (-1, control.SelectedIndex);
            Assert.Empty (control.SelectedIndices);
        }

        // ----- FindString / FindStringExact -----

        [Theory]
        [InlineData ("a", -1, 0)]
        [InlineData ("A", 0, 1)]
        [InlineData ("A", 1, 2)]
        [InlineData ("A", 2, 0)]
        [InlineData ("def", -1, 3)]
        [InlineData ("abcd", -1, -1)]
        [InlineData ("NoSuchItem", -1, -1)]
        public void FindString_Invoke_ReturnsExpected (string s, int startIndex, int expected)
        {
            using var control = new ListBox ();
            control.Items.AddRange ("abc", "abc", "ABC", "def", "");

            Assert.Equal (expected, control.FindString (s, startIndex));
        }

        [Fact]
        public void FindString_EmptyList_ReturnsMinusOne ()
        {
            using var control = new ListBox ();

            Assert.Equal (-1, control.FindString ("anything"));
        }

        [Theory]
        [InlineData (-2)]
        [InlineData (1)]
        [InlineData (2)]
        public void FindString_InvalidStartIndex_ThrowsArgumentOutOfRangeException (int startIndex)
        {
            using var control = new ListBox ();
            control.Items.Add ("item");

            Assert.Throws<ArgumentOutOfRangeException> (() => control.FindString ("s", startIndex));
        }

        [Theory]
        [InlineData ("abc", -1, 0)]
        [InlineData ("abc", 0, 1)]
        [InlineData ("ABC", -1, 0)]
        [InlineData ("def", -1, 3)]
        [InlineData ("ab", -1, -1)]
        [InlineData ("NoSuchItem", -1, -1)]
        public void FindStringExact_Invoke_ReturnsExpected (string s, int startIndex, int expected)
        {
            using var control = new ListBox ();
            control.Items.AddRange ("abc", "abc", "ABC", "def", "");

            Assert.Equal (expected, control.FindStringExact (s, startIndex));
        }

        [Fact]
        public void FindStringExact_EmptyList_ReturnsMinusOne ()
        {
            using var control = new ListBox ();

            Assert.Equal (-1, control.FindStringExact ("anything"));
        }

        [Theory]
        [InlineData (-2)]
        [InlineData (1)]
        [InlineData (2)]
        public void FindStringExact_InvalidStartIndex_ThrowsArgumentOutOfRangeException (int startIndex)
        {
            using var control = new ListBox ();
            control.Items.Add ("item");

            Assert.Throws<ArgumentOutOfRangeException> (() => control.FindStringExact ("s", startIndex));
        }
    }
}
