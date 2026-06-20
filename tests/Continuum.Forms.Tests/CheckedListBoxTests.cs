// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/CheckedListBoxTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms CheckedListBoxTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility plumbing). They pin the same
    // check-state round-trip, CheckedItems/CheckedIndices, CheckOnClick/ThreeDCheckBoxes, the
    // ItemCheck event, and the argument/enum validation rules.
    public class CheckedListBoxTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var box = new CheckedListBox ();

            Assert.NotNull (box);
            Assert.NotNull (box.Items);
            Assert.NotNull (box.CheckedItems);
            Assert.NotNull (box.CheckedIndices);
            Assert.False (box.CheckOnClick);
            Assert.False (box.ThreeDCheckBoxes);
            Assert.Equal (0, box.Items.Count);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void CheckOnClick_GetSet_ReturnsExpected (bool value)
        {
            using var box = new CheckedListBox { CheckOnClick = value };

            Assert.Equal (value, box.CheckOnClick);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ThreeDCheckBoxes_GetSet_ReturnsExpected (bool value)
        {
            using var box = new CheckedListBox { ThreeDCheckBoxes = value };

            Assert.Equal (value, box.ThreeDCheckBoxes);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ThreeDCheckBoxes_SetWithItems_ReturnsExpected (bool value)
        {
            using var box = new CheckedListBox { ThreeDCheckBoxes = value };

            box.Items.Add ("item1");

            Assert.Equal (value, box.ThreeDCheckBoxes);
        }

        [Fact]
        public void Items_Add_AddsToCollection ()
        {
            using var box = new CheckedListBox ();

            box.Items.Add ("item1");
            box.Items.Add ("item2");

            Assert.Equal (2, box.Items.Count);
            Assert.Equal ("item1", box.Items[0]);
            Assert.Equal ("item2", box.Items[1]);
        }

        [Theory]
        [InlineData (true, CheckState.Checked)]
        [InlineData (false, CheckState.Unchecked)]
        public void Items_AddWithCheckedState_GetItemCheckStateReturnsExpected (bool isChecked, CheckState expected)
        {
            using var box = new CheckedListBox ();

            box.Items.Add ("item1", isChecked);

            Assert.Equal (expected, box.GetItemCheckState (0));
            Assert.Equal (isChecked, box.GetItemChecked (0));
        }

        [Theory]
        [InlineData (CheckState.Unchecked)]
        [InlineData (CheckState.Checked)]
        [InlineData (CheckState.Indeterminate)]
        public void Items_AddWithCheckState_GetItemCheckStateReturnsExpected (CheckState value)
        {
            using var box = new CheckedListBox ();

            box.Items.Add ("item1", value);

            Assert.Equal (value, box.GetItemCheckState (0));
        }

        [Theory]
        [InlineData (true, CheckState.Checked)]
        [InlineData (false, CheckState.Unchecked)]
        public void SetItemChecked_Invoke_GetItemCheckStateReturnsExpected (bool value, CheckState expected)
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            box.SetItemChecked (0, value);

            Assert.Equal (expected, box.GetItemCheckState (0));
            Assert.Equal (value, box.GetItemChecked (0));
        }

        [Fact]
        public void SetItemChecked_RoundTrip_TogglesState ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            Assert.False (box.GetItemChecked (0));

            box.SetItemChecked (0, true);
            Assert.True (box.GetItemChecked (0));

            box.SetItemChecked (0, false);
            Assert.False (box.GetItemChecked (0));
        }

        [Theory]
        [InlineData (CheckState.Unchecked)]
        [InlineData (CheckState.Checked)]
        [InlineData (CheckState.Indeterminate)]
        public void SetItemCheckState_Invoke_GetReturnsExpected (CheckState value)
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            box.SetItemCheckState (0, value);
            Assert.Equal (value, box.GetItemCheckState (0));

            // Set same.
            box.SetItemCheckState (0, value);
            Assert.Equal (value, box.GetItemCheckState (0));
        }

        [Fact]
        public void GetItemChecked_Indeterminate_ReturnsTrue ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            box.SetItemCheckState (0, CheckState.Indeterminate);

            // WinForms: an indeterminate item is considered "checked".
            Assert.True (box.GetItemChecked (0));
        }

        [Theory]
        [InlineData (1)]
        [InlineData (-1)]
        [InlineData (int.MaxValue)]
        public void GetItemCheckState_OutOfRange_ThrowsArgumentOutOfRangeException (int index)
        {
            using var box = new CheckedListBox ();

            var ex = Assert.Throws<ArgumentOutOfRangeException> (() => box.GetItemCheckState (index));
            Assert.Equal ("index", ex.ParamName);
        }

        [Theory]
        [InlineData (1)]
        [InlineData (-1)]
        [InlineData (int.MaxValue)]
        public void SetItemCheckState_OutOfRange_ThrowsArgumentOutOfRangeException (int index)
        {
            using var box = new CheckedListBox ();

            var ex = Assert.Throws<ArgumentOutOfRangeException> (() => box.SetItemCheckState (index, CheckState.Checked));
            Assert.Equal ("index", ex.ParamName);
        }

        [Theory]
        [InlineData ((CheckState)(-1))]
        [InlineData ((CheckState)3)]
        public void SetItemCheckState_InvalidValue_ThrowsInvalidEnumArgumentException (CheckState value)
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            var ex = Assert.Throws<InvalidEnumArgumentException> (() => box.SetItemCheckState (0, value));
            Assert.Equal ("value", ex.ParamName);
        }

        [Fact]
        public void CheckedItems_ReflectsCheckedState ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");
            box.Items.Add ("item2");
            box.Items.Add ("item3");

            Assert.Empty (box.CheckedItems);

            box.SetItemChecked (0, true);
            box.SetItemChecked (2, true);

            Assert.Equal (new object[] { "item1", "item3" }, box.CheckedItems.ToArray ());
        }

        [Fact]
        public void CheckedIndices_ReflectsCheckedState ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");
            box.Items.Add ("item2");
            box.Items.Add ("item3");

            Assert.Empty (box.CheckedIndices);

            box.SetItemChecked (0, true);
            box.SetItemChecked (2, true);

            Assert.Equal (new[] { 0, 2 }, box.CheckedIndices.ToArray ());
        }

        [Fact]
        public void CheckedItems_IncludesIndeterminate ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");
            box.Items.Add ("item2");

            box.SetItemCheckState (1, CheckState.Indeterminate);

            Assert.Equal (new object[] { "item2" }, box.CheckedItems.ToArray ());
            Assert.Equal (new[] { 1 }, box.CheckedIndices.ToArray ());
        }

        [Fact]
        public void SetItemCheckState_RaisesItemCheck ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            var callCount = 0;
            ItemCheckEventArgs? capturedArgs = null;

            void Handler (object? sender, ItemCheckEventArgs e)
            {
                callCount++;
                Assert.Same (box, sender);
                capturedArgs = e;
            }

            box.ItemCheck += Handler;

            box.SetItemCheckState (0, CheckState.Checked);
            Assert.Equal (1, callCount);
            Assert.NotNull (capturedArgs);
            Assert.Equal (0, capturedArgs!.Index);
            Assert.Equal (CheckState.Checked, capturedArgs.NewValue);
            Assert.Equal (CheckState.Unchecked, capturedArgs.CurrentValue);

            box.ItemCheck -= Handler;

            box.SetItemCheckState (0, CheckState.Unchecked);
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void SetItemCheckState_SameValue_DoesNotRaiseItemCheck ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1", CheckState.Checked);

            var callCount = 0;
            box.ItemCheck += (sender, e) => callCount++;

            box.SetItemCheckState (0, CheckState.Checked);

            Assert.Equal (0, callCount);
        }

        [Fact]
        public void ItemCheck_HandlerCanOverrideNewValue ()
        {
            using var box = new CheckedListBox ();
            box.Items.Add ("item1");

            // A handler that vetoes the change by resetting NewValue to the current state.
            box.ItemCheck += (sender, e) => e.NewValue = e.CurrentValue;

            box.SetItemCheckState (0, CheckState.Checked);

            Assert.Equal (CheckState.Unchecked, box.GetItemCheckState (0));
            Assert.False (box.GetItemChecked (0));
        }
    }
}
