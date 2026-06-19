// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (ToolStripMenuItemTests.cs / ContextMenuStripTests.cs),
// rewritten for the Modern.Forms menu API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using SkiaSharp;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests inspired by the upstream WinForms ToolStripMenuItem / ContextMenuStrip tests,
    // but written strictly against the Modern.Forms menu family (MenuItem, Menu, MenuDropDown,
    // ContextMenu, MenuItemCollection, MenuSeparatorItem). Modern.Forms uses its own menu types rather
    // than ToolStripMenuItem/ContextMenuStrip, so these pin Modern.Forms' actual ctor/property/collection
    // semantics. Deterministic only: no rendering, no Handle, and no popup Show calls.
    public class MenuTests
    {
        [Fact]
        public void MenuItem_Ctor_Default ()
        {
            var item = new MenuItem ();

            Assert.Equal (string.Empty, item.Text);
            Assert.True (item.Enabled);
            Assert.False (item.Checked);
            Assert.False (item.HasItems);
            Assert.False (item.Selected);
            Assert.False (item.Hovered);
            Assert.False (item.IsDropDownOpened);
            Assert.Null (item.Parent);
            Assert.Null (item.Image);
            Assert.NotNull (item.Items);
            Assert.Empty (item.Items);
        }

        [Fact]
        public void MenuItem_Ctor_Text ()
        {
            var item = new MenuItem ("File");

            Assert.Equal ("File", item.Text);
            Assert.True (item.Enabled);
            Assert.Null (item.Image);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("Open")]
        [InlineData ("&Save")]
        public void MenuItem_Text_Set_GetReturnsExpected (string value)
        {
            var item = new MenuItem { Text = value };

            Assert.Equal (value, item.Text);

            // Set same.
            item.Text = value;
            Assert.Equal (value, item.Text);
        }

        [Fact]
        public void MenuItem_Ctor_TextOnClick_WiresClickHandler ()
        {
            EventHandler<MouseEventArgs> handler = (s, e) => { };

            var item = new MenuItem ("Click", (SKBitmap?)null, handler);

            Assert.Equal ("Click", item.Text);
            // We can't raise Click without rendering, but the ctor must accept and store the handler.
            // Detaching the same delegate must not throw.
            item.Click -= handler;
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void MenuItem_Enabled_Set_GetReturnsExpected (bool value)
        {
            var item = new MenuItem { Enabled = value };

            Assert.Equal (value, item.Enabled);

            // Set same.
            item.Enabled = value;
            Assert.Equal (value, item.Enabled);

            // Toggle back.
            item.Enabled = !value;
            Assert.Equal (!value, item.Enabled);
        }

        [Fact]
        public void MenuItem_Enabled_DefaultsTrueWhenDetached ()
        {
            // A MenuItem with no owning control should default to enabled, matching WinForms.
            var item = new MenuItem ("Detached");

            Assert.True (item.Enabled);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void MenuItem_Checked_Set_GetReturnsExpected (bool value)
        {
            var item = new MenuItem { Checked = value };

            Assert.Equal (value, item.Checked);

            // Set same.
            item.Checked = value;
            Assert.Equal (value, item.Checked);
        }

        [Fact]
        public void MenuItem_Click_AddRemoveHandler_DoesNotThrow ()
        {
            var item = new MenuItem ("Item");
            EventHandler<MouseEventArgs> handler = (s, e) => { };

            item.Click += handler;
            item.Click -= handler;
        }

        [Fact]
        public void MenuItem_Items_AddMenuItem_SetsParentAndHasItems ()
        {
            var parent = new MenuItem ("Parent");
            var child = new MenuItem ("Child");

            var returned = parent.Items.Add (child);

            Assert.Same (child, returned);
            Assert.True (parent.HasItems);
            Assert.Single (parent.Items);
            Assert.Same (child, parent.Items[0]);
            Assert.Same (parent, child.Parent);
        }

        [Fact]
        public void MenuItem_Items_AddText_CreatesChildWithText ()
        {
            var parent = new MenuItem ("Parent");

            var child = parent.Items.Add ("Child");

            Assert.Equal ("Child", child.Text);
            Assert.True (parent.HasItems);
            Assert.Single (parent.Items);
            Assert.Same (parent, child.Parent);
        }

        [Fact]
        public void MenuItem_Items_Remove_ClearsParentAndHasItems ()
        {
            var parent = new MenuItem ("Parent");
            var child = parent.Items.Add ("Child");

            parent.Items.Remove (child);

            Assert.False (parent.HasItems);
            Assert.Empty (parent.Items);
            Assert.Null (child.Parent);
        }

        [Fact]
        public void MenuItemCollection_AddRange_AddsAllAndSetsParent ()
        {
            var parent = new MenuItem ("Parent");
            var a = new MenuItem ("A");
            var b = new MenuItem ("B");

            parent.Items.AddRange (new[] { a, b });

            Assert.Equal (2, parent.Items.Count);
            Assert.Same (a, parent.Items[0]);
            Assert.Same (b, parent.Items[1]);
            Assert.Same (parent, a.Parent);
            Assert.Same (parent, b.Parent);
        }

        [Fact]
        public void MenuItemCollection_SetItem_UpdatesParents ()
        {
            var parent = new MenuItem ("Parent");
            var original = parent.Items.Add ("Original");
            var replacement = new MenuItem ("Replacement");

            parent.Items[0] = replacement;

            Assert.Single (parent.Items);
            Assert.Same (replacement, parent.Items[0]);
            Assert.Same (parent, replacement.Parent);
            Assert.Null (original.Parent);
        }

        [Fact]
        public void MenuItemCollection_AddGeneric_ReturnsSameTypedInstance ()
        {
            var parent = new MenuItem ("Parent");
            var separator = new MenuSeparatorItem ();

            var returned = parent.Items.Add (separator);

            Assert.Same (separator, returned);
            Assert.IsType<MenuSeparatorItem> (parent.Items[0]);
        }

        [Fact]
        public void MenuSeparatorItem_Ctor_IsMenuItem ()
        {
            var separator = new MenuSeparatorItem ();

            Assert.IsAssignableFrom<MenuItem> (separator);
            Assert.Equal (string.Empty, separator.Text);
        }

        [Fact]
        public void MenuItem_NestedItems_BuildHierarchy ()
        {
            var root = new MenuItem ("Root");
            var sub = root.Items.Add ("Sub");
            var leaf = sub.Items.Add ("Leaf");

            Assert.True (root.HasItems);
            Assert.True (sub.HasItems);
            Assert.False (leaf.HasItems);
            Assert.Same (root, sub.Parent);
            Assert.Same (sub, leaf.Parent);
        }

        [Fact]
        public void Menu_Ctor_Default ()
        {
            using var menu = new Menu ();

            Assert.NotNull (menu.Items);
            Assert.Empty (menu.Items);
            Assert.Null (menu.SelectedItem);
            Assert.False (menu.IsActivated);
            Assert.Equal (DockStyle.Top, menu.Dock);
        }

        [Fact]
        public void Menu_Items_Add_AddsToCollection ()
        {
            using var menu = new Menu ();

            var item = menu.Items.Add ("File");

            Assert.Single (menu.Items);
            Assert.Same (item, menu.Items[0]);
            Assert.Equal ("File", menu.Items[0].Text);
        }

        [Fact]
        public void Menu_Items_AddMultiple_PreservesOrder ()
        {
            using var menu = new Menu ();

            var file = menu.Items.Add ("File");
            var edit = menu.Items.Add ("Edit");
            var view = menu.Items.Add ("View");

            Assert.Equal (3, menu.Items.Count);
            Assert.Same (file, menu.Items[0]);
            Assert.Same (edit, menu.Items[1]);
            Assert.Same (view, menu.Items[2]);
        }

        [Fact]
        public void MenuDropDown_Ctor_Default ()
        {
            using var dropdown = new MenuDropDown ();

            Assert.NotNull (dropdown.Items);
            Assert.Empty (dropdown.Items);
            Assert.Equal (DockStyle.Fill, dropdown.Dock);
            Assert.False (dropdown.Visible);
        }

        [Fact]
        public void MenuDropDown_Items_Add_AddsToCollection ()
        {
            using var dropdown = new MenuDropDown ();

            var item = dropdown.Items.Add ("Open");

            Assert.Single (dropdown.Items);
            Assert.Same (item, dropdown.Items[0]);
        }

        [Fact]
        public void ContextMenu_Ctor_Default ()
        {
            using var menu = new ContextMenu ();

            Assert.NotNull (menu.Items);
            Assert.Empty (menu.Items);
            Assert.Null (menu.SourceControl);
        }

        [Fact]
        public void ContextMenu_MenuItems_IsAliasForItems ()
        {
            using var menu = new ContextMenu ();

            Assert.Same (menu.Items, menu.MenuItems);

            var item = menu.MenuItems.Add ("Cut");

            Assert.Single (menu.Items);
            Assert.Same (item, menu.Items[0]);
        }

        [Fact]
        public void ContextMenu_Items_AddRemove_UpdatesCount ()
        {
            using var menu = new ContextMenu ();

            var cut = menu.Items.Add ("Cut");
            var copy = menu.Items.Add ("Copy");

            Assert.Equal (2, menu.Items.Count);

            menu.Items.Remove (cut);

            Assert.Single (menu.Items);
            Assert.Same (copy, menu.Items[0]);
        }

        [Fact]
        public void ContextMenu_IsMenuDropDown ()
        {
            using var menu = new ContextMenu ();

            Assert.IsAssignableFrom<MenuDropDown> (menu);
        }
    }
}
