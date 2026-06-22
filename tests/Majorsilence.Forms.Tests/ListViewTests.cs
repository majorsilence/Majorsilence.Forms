// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ListViewTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ListViewTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing). They pin
    // the ListView-level behaviors: the Items collection, Selected*/Checked* projections,
    // View and boolean property round-trips, and the Columns collection. Item-only behavior
    // is covered by ListViewItemTests and is intentionally not duplicated here.
    public class ListViewTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new ListView ();

            Assert.Equal (ItemActivation.Standard, control.Activation);
            Assert.False (control.AllowColumnReorder);
            Assert.True (control.AutoArrange);
            Assert.False (control.CheckBoxes);
            Assert.Empty (control.CheckedIndices);
            Assert.Empty (control.CheckedItems);
            Assert.Empty (control.Columns);
            Assert.Null (control.FocusedItem);
            Assert.False (control.FullRowSelect);
            Assert.False (control.GridLines);
            Assert.Empty (control.Groups);
            Assert.Equal (ColumnHeaderStyle.Clickable, control.HeaderStyle);
            Assert.False (control.HideSelection);
            Assert.False (control.HoverSelection);
            Assert.Empty (control.Items);
            Assert.False (control.LabelEdit);
            Assert.True (control.LabelWrap);
            Assert.Null (control.LargeImageList);
            Assert.Null (control.ListViewItemSorter);
            Assert.True (control.MultiSelect);
            Assert.True (control.Scrollable);
            Assert.Null (control.SelectedItem);
            Assert.Empty (control.SelectedItems);
            Assert.Empty (control.SelectedIndices);
            Assert.False (control.ShowItemToolTips);
            Assert.Null (control.SmallImageList);
            Assert.Equal (SortOrder.None, control.Sorting);
            Assert.Null (control.StateImageList);
            Assert.False (control.VirtualMode);
            Assert.Equal (0, control.VirtualListSize);
            Assert.Equal (View.LargeIcon, control.View);
        }

        [Fact]
        public void Items_Get_SameInstance ()
        {
            using var control = new ListView ();
            Assert.Same (control.Items, control.Items);
        }

        [Fact]
        public void Items_AddString_GetReturnsExpected ()
        {
            using var control = new ListView ();

            var item = control.Items.Add ("text");

            Assert.Equal ("text", item.Text);
            Assert.Equal (1, control.Items.Count);
            Assert.Same (item, control.Items[0]);
            Assert.Same (control, item.Parent);
            Assert.Same (control, item.ListView);
            Assert.Equal (0, item.Index);
        }

        [Fact]
        public void Items_AddItem_SetsParent ()
        {
            using var control = new ListView ();
            var item = new ListViewItem ("item");

            control.Items.Add (item);

            Assert.Equal (1, control.Items.Count);
            Assert.Same (control, item.Parent);
            Assert.Equal (0, control.Items.IndexOf (item));
        }

        [Fact]
        public void Items_AddMultiple_GetReturnsExpected ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");
            var item3 = control.Items.Add ("three");

            Assert.Equal (3, control.Items.Count);
            Assert.Equal (0, item1.Index);
            Assert.Equal (1, item2.Index);
            Assert.Equal (2, item3.Index);
        }

        [Fact]
        public void Items_Remove_ClearsParent ()
        {
            using var control = new ListView ();
            var item = control.Items.Add ("text");

            control.Items.Remove (item);

            Assert.Empty (control.Items);
            Assert.Null (item.Parent);
            Assert.Equal (-1, item.Index);
        }

        [Fact]
        public void Items_ItemRemove_RemovesFromParent ()
        {
            using var control = new ListView ();
            var item = control.Items.Add ("text");

            item.Remove ();

            Assert.Empty (control.Items);
            Assert.Null (item.Parent);
        }

        [Fact]
        public void Items_Clear_RemovesAllAndClearsParent ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");

            control.Items.Clear ();

            Assert.Empty (control.Items);
            Assert.Null (item1.Parent);
            Assert.Null (item2.Parent);
        }

        [Fact]
        public void Items_StringIndexer_ReturnsItemByName ()
        {
            using var control = new ListView ();
            var item = new ListViewItem ("text") { Name = "key" };
            control.Items.Add (item);

            Assert.Same (item, control.Items["key"]);
            Assert.Same (item, control.Items["KEY"]);
            Assert.Null (control.Items["missing"]);
        }

        [Fact]
        public void Items_Find_ReturnsMatchingByName ()
        {
            using var control = new ListView ();
            var item1 = new ListViewItem ("a") { Name = "key" };
            var item2 = new ListViewItem ("b") { Name = "other" };
            control.Items.Add (item1);
            control.Items.Add (item2);

            var found = control.Items.Find ("key", searchAllSubItems: false);

            Assert.Single (found);
            Assert.Same (item1, found[0]);
        }

        [Fact]
        public void SelectedItem_Set_GetReturnsExpected ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");

            control.SelectedItem = item2;

            Assert.Same (item2, control.SelectedItem);
            Assert.True (item2.Selected);
            Assert.False (item1.Selected);
        }

        [Fact]
        public void SelectedItem_SetReplacesPreviousSelection ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");

            control.SelectedItem = item1;
            control.SelectedItem = item2;

            Assert.Same (item2, control.SelectedItem);
            Assert.False (item1.Selected);
            Assert.True (item2.Selected);
        }

        [Fact]
        public void SelectedItem_SetNull_ClearsSelection ()
        {
            using var control = new ListView ();
            var item = control.Items.Add ("one");
            control.SelectedItem = item;

            control.SelectedItem = null;

            Assert.Null (control.SelectedItem);
            Assert.False (item.Selected);
        }

        [Fact]
        public void SelectedItem_ReflectsItemSelected ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");

            item2.Selected = true;

            Assert.Same (item2, control.SelectedItem);
        }

        [Fact]
        public void SelectedItems_ReflectsSelectedState ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");
            var item3 = control.Items.Add ("three");

            item1.Selected = true;
            item3.Selected = true;

            Assert.Equal (new[] { item1, item3 }, control.SelectedItems.ToArray ());
        }

        [Fact]
        public void SelectedIndices_ReflectsSelectedState ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");
            var item3 = control.Items.Add ("three");

            item1.Selected = true;
            item3.Selected = true;

            Assert.Equal (new[] { 0, 2 }, control.SelectedIndices.ToArray ());
        }

        [Fact]
        public void ClearSelection_DeselectsAllItems ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");
            item1.Selected = true;
            item2.Selected = true;

            control.ClearSelection ();

            Assert.False (item1.Selected);
            Assert.False (item2.Selected);
            Assert.Empty (control.SelectedItems);
        }

        [Fact]
        public void CheckedItems_ReflectsCheckedState ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");
            var item3 = control.Items.Add ("three");

            item1.Checked = true;
            item3.Checked = true;

            Assert.Equal (new[] { item1, item3 }, control.CheckedItems.ToArray ());
        }

        [Fact]
        public void CheckedIndices_ReflectsCheckedState ()
        {
            using var control = new ListView ();
            var item1 = control.Items.Add ("one");
            var item2 = control.Items.Add ("two");
            var item3 = control.Items.Add ("three");

            item1.Checked = true;
            item3.Checked = true;

            Assert.Equal (new[] { 0, 2 }, control.CheckedIndices.ToArray ());
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void CheckBoxes_SetWithCheckedItems_PreservesCheckedState (bool value)
        {
            var item1 = new ListViewItem { Checked = true };
            var item2 = new ListViewItem ();
            using var control = new ListView ();
            control.Items.Add (item1);
            control.Items.Add (item2);

            control.CheckBoxes = value;
            Assert.Equal (value, control.CheckBoxes);
            Assert.True (item1.Checked);
            Assert.False (item2.Checked);

            // Set same.
            control.CheckBoxes = value;
            Assert.Equal (value, control.CheckBoxes);
            Assert.True (item1.Checked);
            Assert.False (item2.Checked);

            // Set different.
            control.CheckBoxes = !value;
            Assert.Equal (!value, control.CheckBoxes);
            Assert.True (item1.Checked);
            Assert.False (item2.Checked);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void CheckBoxes_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { CheckBoxes = value };
            Assert.Equal (value, control.CheckBoxes);

            control.CheckBoxes = value;
            Assert.Equal (value, control.CheckBoxes);

            control.CheckBoxes = !value;
            Assert.Equal (!value, control.CheckBoxes);
        }

        [Theory]
        [InlineData (View.LargeIcon)]
        [InlineData (View.SmallIcon)]
        [InlineData (View.List)]
        [InlineData (View.Details)]
        [InlineData (View.Tile)]
        public void View_Set_GetReturnsExpected (View value)
        {
            using var control = new ListView { View = value };
            Assert.Equal (value, control.View);

            // Set same.
            control.View = value;
            Assert.Equal (value, control.View);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void MultiSelect_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { MultiSelect = value };
            Assert.Equal (value, control.MultiSelect);

            control.MultiSelect = !value;
            Assert.Equal (!value, control.MultiSelect);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FullRowSelect_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { FullRowSelect = value };
            Assert.Equal (value, control.FullRowSelect);

            control.FullRowSelect = !value;
            Assert.Equal (!value, control.FullRowSelect);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void GridLines_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { GridLines = value };
            Assert.Equal (value, control.GridLines);

            control.GridLines = !value;
            Assert.Equal (!value, control.GridLines);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoArrange_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { AutoArrange = value };
            Assert.Equal (value, control.AutoArrange);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void HideSelection_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { HideSelection = value };
            Assert.Equal (value, control.HideSelection);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void LabelEdit_Set_GetReturnsExpected (bool value)
        {
            using var control = new ListView { LabelEdit = value };
            Assert.Equal (value, control.LabelEdit);
        }

        [Theory]
        [InlineData (SortOrder.None)]
        [InlineData (SortOrder.Ascending)]
        [InlineData (SortOrder.Descending)]
        public void Sorting_Set_GetReturnsExpected (SortOrder value)
        {
            using var control = new ListView { Sorting = value };
            Assert.Equal (value, control.Sorting);
        }

        [Theory]
        [InlineData (ItemActivation.Standard)]
        [InlineData (ItemActivation.OneClick)]
        [InlineData (ItemActivation.TwoClick)]
        public void Activation_Set_GetReturnsExpected (ItemActivation value)
        {
            using var control = new ListView { Activation = value };
            Assert.Equal (value, control.Activation);
        }

        [Theory]
        [InlineData (ColumnHeaderStyle.None)]
        [InlineData (ColumnHeaderStyle.Nonclickable)]
        [InlineData (ColumnHeaderStyle.Clickable)]
        public void HeaderStyle_Set_GetReturnsExpected (ColumnHeaderStyle value)
        {
            using var control = new ListView { HeaderStyle = value };
            Assert.Equal (value, control.HeaderStyle);
        }

        [Fact]
        public void Columns_Get_SameInstance ()
        {
            using var control = new ListView ();
            Assert.Same (control.Columns, control.Columns);
        }

        [Fact]
        public void Columns_AddText_GetReturnsExpected ()
        {
            using var control = new ListView ();

            var column = control.Columns.Add ("text");

            Assert.Equal ("text", column.Text);
            Assert.Equal (1, control.Columns.Count);
            Assert.Same (column, control.Columns[0]);
            Assert.Equal (0, column.Index);
        }

        [Fact]
        public void Columns_AddTextWidth_GetReturnsExpected ()
        {
            using var control = new ListView ();

            var column = control.Columns.Add ("text", 100);

            Assert.Equal ("text", column.Text);
            Assert.Equal (100, column.Width);
            Assert.Equal (1, control.Columns.Count);
        }

        [Fact]
        public void Columns_AddMultiple_AssignsSequentialIndexes ()
        {
            using var control = new ListView ();

            var c1 = control.Columns.Add ("one");
            var c2 = control.Columns.Add ("two");
            var c3 = control.Columns.Add ("three");

            Assert.Equal (3, control.Columns.Count);
            Assert.Equal (0, c1.Index);
            Assert.Equal (1, c2.Index);
            Assert.Equal (2, c3.Index);
        }

        [Fact]
        public void Groups_AddHeader_GetReturnsExpected ()
        {
            using var control = new ListView ();

            var group = control.Groups.Add ("header");

            Assert.Equal ("header", group.Header);
            Assert.Equal (1, control.Groups.Count);
            Assert.Same (group, control.Groups[0]);
        }

        [Fact]
        public void SmallImageList_Set_GetReturnsExpected ()
        {
            using var control = new ListView ();
            using var imageList = new ImageList ();

            control.SmallImageList = imageList;
            Assert.Same (imageList, control.SmallImageList);

            control.SmallImageList = null;
            Assert.Null (control.SmallImageList);
        }

        [Fact]
        public void LargeImageList_Set_GetReturnsExpected ()
        {
            using var control = new ListView ();
            using var imageList = new ImageList ();

            control.LargeImageList = imageList;
            Assert.Same (imageList, control.LargeImageList);

            control.LargeImageList = null;
            Assert.Null (control.LargeImageList);
        }

        [Fact]
        public void FocusedItem_Set_GetReturnsExpected ()
        {
            using var control = new ListView ();
            var item = control.Items.Add ("text");

            control.FocusedItem = item;
            Assert.Same (item, control.FocusedItem);

            control.FocusedItem = null;
            Assert.Null (control.FocusedItem);
        }

        [Fact]
        public void GetItemRect_ValidIndex_ReturnsItemBounds ()
        {
            using var control = new ListView ();
            var item = control.Items.Add ("text");
            item.SetBounds (1, 2, 3, 4);

            Assert.Equal (new System.Drawing.Rectangle (1, 2, 3, 4), control.GetItemRect (0));
        }

        [Fact]
        public void GetItemRect_InvalidIndex_ReturnsEmpty ()
        {
            using var control = new ListView ();

            Assert.Equal (System.Drawing.Rectangle.Empty, control.GetItemRect (-1));
            Assert.Equal (System.Drawing.Rectangle.Empty, control.GetItemRect (0));
        }

        [Fact]
        public void FindItemWithText_ReturnsMatch ()
        {
            using var control = new ListView ();
            control.Items.Add ("apple");
            var banana = control.Items.Add ("banana");

            Assert.Same (banana, control.FindItemWithText ("banana"));
            Assert.Same (banana, control.FindItemWithText ("BANANA"));
            Assert.Null (control.FindItemWithText ("cherry"));
        }
    }
}
