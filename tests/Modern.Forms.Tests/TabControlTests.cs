// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (TabControlTests.cs and TabPageTests.cs under
// src/test/unit/System.Windows.Forms/System/Windows/Forms/), rewritten for the Modern.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms TabControlTests / TabPageTests, adapted to
    // the Modern.Forms API (no Handle / CreateParams / accessibility plumbing). Modern.Forms has no
    // deferred-handle model, so selection is applied eagerly; tests pin that real behavior while
    // matching WinForms semantics wherever the two models agree (defaults, indexer/Count, SelectedTab
    // <-> SelectedIndex consistency, foreign-page selection clearing, TabPage.Text).
    public class TabControlTests
    {
        [Fact]
        public void TabControl_Ctor_Default ()
        {
            using var control = new TabControl ();

            Assert.Equal (TabAlignment.Top, control.Alignment);
            Assert.Equal (TabAppearance.Normal, control.Appearance);
            Assert.Equal (TabDrawMode.Normal, control.DrawMode);
            Assert.Equal (TabSizeMode.Normal, control.SizeMode);
            Assert.False (control.HotTrack);
            Assert.False (control.Multiline);
            Assert.False (control.ShowToolTips);
            Assert.Null (control.ImageList);
            Assert.NotNull (control.TabPages);
            Assert.Same (control.TabPages, control.TabPages);
            Assert.Empty (control.TabPages);
            Assert.Equal (0, control.TabCount);
            Assert.Equal (1, control.RowCount);

            // No tabs -> nothing selected.
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedTab);
            Assert.Null (control.SelectedTabPage);
        }

        [Fact]
        public void TabPages_Add_UpdatesCountAndIndexer ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");

            var returned = control.TabPages.Add (page1);
            Assert.Same (page1, returned);
            Assert.Equal (1, control.TabPages.Count);
            Assert.Equal (1, control.TabCount);
            Assert.Same (page1, control.TabPages[0]);

            control.TabPages.Add (page2);
            Assert.Equal (2, control.TabPages.Count);
            Assert.Same (page1, control.TabPages[0]);
            Assert.Same (page2, control.TabPages[1]);
        }

        [Fact]
        public void TabPages_AddString_CreatesPageWithText ()
        {
            using var control = new TabControl ();

            var page = control.TabPages.Add ("Hello");

            Assert.Equal ("Hello", page.Text);
            Assert.Equal (1, control.TabPages.Count);
            Assert.Same (page, control.TabPages[0]);
        }

        [Fact]
        public void TabPages_AddKeyAndText_SetsNameAndText ()
        {
            using var control = new TabControl ();

            var page = control.TabPages.Add ("key1", "Display");

            Assert.Equal ("key1", page.Name);
            Assert.Equal ("Display", page.Text);
            Assert.Same (page, control.TabPages["key1"]);
            Assert.True (control.TabPages.ContainsKey ("KEY1"));
            Assert.False (control.TabPages.ContainsKey ("missing"));
        }

        [Fact]
        public void TabPages_AddFirstPage_SelectsIt ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");

            control.TabPages.Add (page1);

            // Adding the first page auto-selects it.
            Assert.Equal (0, control.SelectedIndex);
            Assert.Same (page1, control.SelectedTab);
            Assert.Same (page1, control.SelectedTabPage);
        }

        [Fact]
        public void TabPages_AddSecondPage_DoesNotChangeSelection ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");

            control.TabPages.Add (page1);
            control.TabPages.Add (page2);

            Assert.Equal (0, control.SelectedIndex);
            Assert.Same (page1, control.SelectedTab);
        }

        [Fact]
        public void TabPages_Remove_UpdatesCount ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");
            control.TabPages.Add (page1);
            control.TabPages.Add (page2);

            control.TabPages.Remove (page1);

            Assert.Equal (1, control.TabPages.Count);
            Assert.Same (page2, control.TabPages[0]);
            Assert.DoesNotContain (page1, control.TabPages);
        }

        [Fact]
        public void TabPages_RemoveSelectedLast_ReselectsPrevious ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");
            control.TabPages.Add (page1);
            control.TabPages.Add (page2);
            control.SelectedIndex = 1;

            control.TabPages.RemoveAt (1);

            // Removing the selected trailing tab falls back to the previous one.
            Assert.Equal (1, control.TabPages.Count);
            Assert.Equal (0, control.SelectedIndex);
            Assert.Same (page1, control.SelectedTab);
        }

        [Fact]
        public void RemoveAll_ClearsPages ()
        {
            using var control = new TabControl ();
            control.TabPages.Add (new TabPage ("One"));
            control.TabPages.Add (new TabPage ("Two"));

            control.RemoveAll ();

            Assert.Equal (0, control.TabPages.Count);
            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedTab);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        public void SelectedIndex_SetWithPages_GetReturnsExpected (int value)
        {
            using var control = new TabControl ();
            control.TabPages.Add (new TabPage ("One"));
            control.TabPages.Add (new TabPage ("Two"));

            control.SelectedIndex = value;

            Assert.Equal (value, control.SelectedIndex);
            Assert.Same (control.TabPages[value], control.SelectedTab);

            // Set same is a no-op.
            control.SelectedIndex = value;
            Assert.Equal (value, control.SelectedIndex);
            Assert.Same (control.TabPages[value], control.SelectedTab);
        }

        [Fact]
        public void SelectedIndex_SetMinusOne_ClearsSelection ()
        {
            using var control = new TabControl ();
            control.TabPages.Add (new TabPage ("One"));
            control.TabPages.Add (new TabPage ("Two"));
            control.SelectedIndex = 1;

            control.SelectedIndex = -1;

            Assert.Equal (-1, control.SelectedIndex);
            Assert.Null (control.SelectedTab);
        }

        [Fact]
        public void SelectedIndex_SetBelowMinusOne_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TabControl ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = -2);
        }

        [Fact]
        public void SelectedIndex_SetGreaterThanOrEqualToCount_ThrowsArgumentOutOfRangeException ()
        {
            // Modern.Forms has no deferred-handle model: it validates the index eagerly against the
            // current tab count and throws, where WinForms (handle not yet created) would store it.
            using var control = new TabControl ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = 0);

            control.TabPages.Add (new TabPage ("One"));
            Assert.Throws<ArgumentOutOfRangeException> (() => control.SelectedIndex = 1);
        }

        [Fact]
        public void SelectedTab_SetWithPages_UpdatesSelectedIndex ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");
            control.TabPages.Add (page1);
            control.TabPages.Add (page2);

            control.SelectedTab = page2;
            Assert.Same (page2, control.SelectedTab);
            Assert.Equal (1, control.SelectedIndex);

            // Set same.
            control.SelectedTab = page2;
            Assert.Same (page2, control.SelectedTab);
            Assert.Equal (1, control.SelectedIndex);

            // Set different.
            control.SelectedTab = page1;
            Assert.Same (page1, control.SelectedTab);
            Assert.Equal (0, control.SelectedIndex);
        }

        [Fact]
        public void SelectedTab_SetNull_ClearsSelection ()
        {
            using var control = new TabControl ();
            control.TabPages.Add (new TabPage ("One"));
            control.SelectedTab = null;

            Assert.Null (control.SelectedTab);
            Assert.Equal (-1, control.SelectedIndex);
        }

        [Fact]
        public void SelectedTab_SetForeignPage_ClearsSelection ()
        {
            // WinForms quietly clears the selection when the page is not part of this control.
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var foreign = new TabPage ("Foreign");
            control.TabPages.Add (page1);

            control.SelectedTab = foreign;

            Assert.Null (control.SelectedTab);
            Assert.Equal (-1, control.SelectedIndex);
        }

        [Fact]
        public void SelectTab_ByIndex_SelectsPage ()
        {
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");
            control.TabPages.Add (page1);
            control.TabPages.Add (page2);

            control.SelectTab (1);
            Assert.Same (page2, control.SelectedTab);

            control.SelectTab (page1);
            Assert.Same (page1, control.SelectedTab);
        }

        [Fact]
        public void SelectedIndexChanged_RaisedOnChange ()
        {
            using var control = new TabControl ();
            control.TabPages.Add (new TabPage ("One"));
            control.TabPages.Add (new TabPage ("Two"));

            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.SelectedIndexChanged += handler;

            // Change selection.
            control.SelectedIndex = 1;
            Assert.Equal (1, callCount);

            // Set same -> no event.
            control.SelectedIndex = 1;
            Assert.Equal (1, callCount);

            // Change again.
            control.SelectedIndex = 0;
            Assert.Equal (2, callCount);

            // Remove handler -> no further events.
            control.SelectedIndexChanged -= handler;
            control.SelectedIndex = 1;
            Assert.Equal (2, callCount);
        }

        [Fact]
        public void TabPage_Ctor_Default ()
        {
            using var page = new TabPage ();

            Assert.Equal (string.Empty, page.Text);
            Assert.Equal (DockStyle.Fill, page.Dock);
            Assert.Equal (-1, page.ImageIndex);
            Assert.Equal (string.Empty, page.ImageKey);
            Assert.Equal (string.Empty, page.ToolTipText);
            Assert.True (page.Enabled);
        }

        [Theory]
        [InlineData ("text")]
        [InlineData ("")]
        public void TabPage_Ctor_String (string text)
        {
            using var page = new TabPage (text);

            Assert.Equal (text, page.Text);
            Assert.Equal (DockStyle.Fill, page.Dock);
        }

        [Theory]
        [InlineData ("text", "text")]
        [InlineData ("", "")]
        [InlineData (null, "")]
        public void TabPage_Text_Set_GetReturnsExpected (string? value, string expected)
        {
            using var page = new TabPage { Text = value! };
            Assert.Equal (expected, page.Text);

            // Set same.
            page.Text = value!;
            Assert.Equal (expected, page.Text);
        }

        [Fact]
        public void TabPage_Text_TracksTabStripItem ()
        {
            using var control = new TabControl ();
            using var page = new TabPage ("Initial");
            control.TabPages.Add (page);

            page.Text = "Updated";
            Assert.Equal ("Updated", page.Text);
        }
    }
}
