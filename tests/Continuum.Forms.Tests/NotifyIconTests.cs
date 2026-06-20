// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/NotifyIconTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms NotifyIconTests, adapted to the
    // Continuum.Forms API. Continuum.Forms has no native system-tray support, so tray/Handle/design-mode
    // plumbing is omitted. The tests pin the property get/set round-trips (Text incl. MaxTextSize
    // validation, Visible, BalloonTip*, Tag, ContextMenuStrip, Icon), the constructor contract,
    // event add/remove, dispose behavior, and the ShowBalloonTip argument validation that mirrors
    // WinForms.
    public class NotifyIconTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var notifyIcon = new NotifyIcon ();

            Assert.Equal (ToolTipIcon.None, notifyIcon.BalloonTipIcon);
            Assert.Equal (string.Empty, notifyIcon.BalloonTipText);
            Assert.Equal (string.Empty, notifyIcon.BalloonTipTitle);
            Assert.Null (notifyIcon.Container);
            Assert.Null (notifyIcon.ContextMenuStrip);
            Assert.Null (notifyIcon.Icon);
            Assert.Null (notifyIcon.Site);
            Assert.Null (notifyIcon.Tag);
            Assert.Equal (string.Empty, notifyIcon.Text);
            Assert.False (notifyIcon.Visible);
        }

        [Fact]
        public void Ctor_IContainer ()
        {
            using var container = new Container ();
            using var notifyIcon = new NotifyIcon (container);

            Assert.Same (container, notifyIcon.Container);
            Assert.NotNull (notifyIcon.Site);
            Assert.Equal (string.Empty, notifyIcon.Text);
            Assert.False (notifyIcon.Visible);
        }

        [Fact]
        public void Ctor_NullContainer_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException> ("container", () => new NotifyIcon ((IContainer)null!));
        }

        [Theory]
        [InlineData (ToolTipIcon.None)]
        [InlineData (ToolTipIcon.Info)]
        [InlineData (ToolTipIcon.Warning)]
        [InlineData (ToolTipIcon.Error)]
        public void BalloonTipIcon_Set_GetReturnsExpected (ToolTipIcon value)
        {
            using var notifyIcon = new NotifyIcon { BalloonTipIcon = value };
            Assert.Equal (value, notifyIcon.BalloonTipIcon);

            // Set same.
            notifyIcon.BalloonTipIcon = value;
            Assert.Equal (value, notifyIcon.BalloonTipIcon);
        }

        [Theory]
        [InlineData ((ToolTipIcon)(-1))]
        [InlineData ((ToolTipIcon)4)]
        public void BalloonTipIcon_SetInvalidValue_ThrowsInvalidEnumArgumentException (ToolTipIcon value)
        {
            using var notifyIcon = new NotifyIcon ();
            Assert.Throws<InvalidEnumArgumentException> ("value", () => notifyIcon.BalloonTipIcon = value);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void BalloonTipText_Set_GetReturnsExpected (string? value, string expected)
        {
            using var notifyIcon = new NotifyIcon { BalloonTipText = value! };
            Assert.Equal (expected, notifyIcon.BalloonTipText);

            // Set same.
            notifyIcon.BalloonTipText = value!;
            Assert.Equal (expected, notifyIcon.BalloonTipText);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void BalloonTipText_SetWithCustomOldValue_GetReturnsExpected (string? value, string expected)
        {
            using var notifyIcon = new NotifyIcon { BalloonTipText = "OldValue" };

            notifyIcon.BalloonTipText = value!;
            Assert.Equal (expected, notifyIcon.BalloonTipText);

            // Set same.
            notifyIcon.BalloonTipText = value!;
            Assert.Equal (expected, notifyIcon.BalloonTipText);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void BalloonTipTitle_Set_GetReturnsExpected (string? value, string expected)
        {
            using var notifyIcon = new NotifyIcon { BalloonTipTitle = value! };
            Assert.Equal (expected, notifyIcon.BalloonTipTitle);

            // Set same.
            notifyIcon.BalloonTipTitle = value!;
            Assert.Equal (expected, notifyIcon.BalloonTipTitle);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void BalloonTipTitle_SetWithCustomOldValue_GetReturnsExpected (string? value, string expected)
        {
            using var notifyIcon = new NotifyIcon { BalloonTipTitle = "OldValue" };

            notifyIcon.BalloonTipTitle = value!;
            Assert.Equal (expected, notifyIcon.BalloonTipTitle);

            // Set same.
            notifyIcon.BalloonTipTitle = value!;
            Assert.Equal (expected, notifyIcon.BalloonTipTitle);
        }

        [Fact]
        public void ContextMenuStrip_Set_GetReturnsExpected ()
        {
            using var menu = new ContextMenuStrip ();
            using var notifyIcon = new NotifyIcon { ContextMenuStrip = menu };
            Assert.Same (menu, notifyIcon.ContextMenuStrip);

            // Set same.
            notifyIcon.ContextMenuStrip = menu;
            Assert.Same (menu, notifyIcon.ContextMenuStrip);

            // Set null.
            notifyIcon.ContextMenuStrip = null;
            Assert.Null (notifyIcon.ContextMenuStrip);
        }

        [Fact]
        public void ContextMenuStrip_SetWithCustomOldValue_GetReturnsExpected ()
        {
            using var oldMenu = new ContextMenuStrip ();
            using var newMenu = new ContextMenuStrip ();
            using var notifyIcon = new NotifyIcon { ContextMenuStrip = oldMenu };

            notifyIcon.ContextMenuStrip = newMenu;
            Assert.Same (newMenu, notifyIcon.ContextMenuStrip);
        }

        [Fact]
        public void Icon_Set_GetReturnsExpected ()
        {
            using var icon = new Continuum.Drawing.Icon ((SkiaSharp.SKBitmap?)null);
            using var notifyIcon = new NotifyIcon { Icon = icon };
            Assert.Same (icon, notifyIcon.Icon);

            // Set same.
            notifyIcon.Icon = icon;
            Assert.Same (icon, notifyIcon.Icon);

            // Set null.
            notifyIcon.Icon = null;
            Assert.Null (notifyIcon.Icon);
        }

        [Fact]
        public void Icon_SetWithVisible_GetReturnsExpected ()
        {
            using var icon = new Continuum.Drawing.Icon ((SkiaSharp.SKBitmap?)null);
            using var notifyIcon = new NotifyIcon { Visible = true, Icon = icon };
            Assert.Same (icon, notifyIcon.Icon);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("text")]
        public void Tag_Set_GetReturnsExpected (object? value)
        {
            using var notifyIcon = new NotifyIcon { Tag = value };
            Assert.Same (value, notifyIcon.Tag);

            // Set same.
            notifyIcon.Tag = value;
            Assert.Same (value, notifyIcon.Tag);
        }

        [Theory]
        [InlineData (true, null, "")]
        [InlineData (true, "", "")]
        [InlineData (true, "text", "text")]
        [InlineData (false, null, "")]
        [InlineData (false, "", "")]
        [InlineData (false, "text", "text")]
        public void Text_Set_GetReturnsExpected (bool visible, string? value, string expected)
        {
            using var notifyIcon = new NotifyIcon { Visible = visible, Text = value! };
            Assert.Equal (expected, notifyIcon.Text);

            // Set same.
            notifyIcon.Text = value!;
            Assert.Equal (expected, notifyIcon.Text);
        }

        [Fact]
        public void Text_SetMaxLength_GetReturnsExpected ()
        {
            var value = new string ('a', NotifyIcon.MaxTextSize);
            using var notifyIcon = new NotifyIcon { Text = value };
            Assert.Equal (value, notifyIcon.Text);
        }

        [Fact]
        public void Text_SetWithCustomOldValue_GetReturnsExpected ()
        {
            using var notifyIcon = new NotifyIcon { Text = "OldValue" };

            notifyIcon.Text = "text";
            Assert.Equal ("text", notifyIcon.Text);
        }

        [Fact]
        public void Text_SetLongValue_ThrowsArgumentOutOfRangeException ()
        {
            using var notifyIcon = new NotifyIcon ();
            Assert.Throws<ArgumentOutOfRangeException> ("Text", () => notifyIcon.Text = new string ('a', NotifyIcon.MaxTextSize + 1));
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Visible_Set_GetReturnsExpected (bool value)
        {
            using var notifyIcon = new NotifyIcon { Visible = value };
            Assert.Equal (value, notifyIcon.Visible);

            // Set same.
            notifyIcon.Visible = value;
            Assert.Equal (value, notifyIcon.Visible);

            // Set different.
            notifyIcon.Visible = !value;
            Assert.Equal (!value, notifyIcon.Visible);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Visible_SetWithIcon_GetReturnsExpected (bool value)
        {
            using var icon = new Continuum.Drawing.Icon ((SkiaSharp.SKBitmap?)null);
            using var notifyIcon = new NotifyIcon { Icon = icon, Visible = value };
            Assert.Equal (value, notifyIcon.Visible);

            // Set different.
            notifyIcon.Visible = !value;
            Assert.Equal (!value, notifyIcon.Visible);
        }

        [Fact]
        public void BalloonTipClicked_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, EventArgs e) => callCount++;

            notifyIcon.BalloonTipClicked += handler;
            notifyIcon.BalloonTipClicked -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void BalloonTipClosed_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, EventArgs e) => callCount++;

            notifyIcon.BalloonTipClosed += handler;
            notifyIcon.BalloonTipClosed -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void BalloonTipShown_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, EventArgs e) => callCount++;

            notifyIcon.BalloonTipShown += handler;
            notifyIcon.BalloonTipShown -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void Click_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, EventArgs e) => callCount++;

            notifyIcon.Click += handler;
            notifyIcon.Click -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void DoubleClick_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, EventArgs e) => callCount++;

            notifyIcon.DoubleClick += handler;
            notifyIcon.DoubleClick -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void MouseClick_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, MouseEventArgs e) => callCount++;

            notifyIcon.MouseClick += handler;
            notifyIcon.MouseClick -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void MouseDoubleClick_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, MouseEventArgs e) => callCount++;

            notifyIcon.MouseDoubleClick += handler;
            notifyIcon.MouseDoubleClick -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void MouseMove_AddRemoveEvent_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            void handler (object? sender, MouseEventArgs e) => callCount++;

            notifyIcon.MouseMove += handler;
            notifyIcon.MouseMove -= handler;
            Assert.Equal (0, callCount);
        }

        [Fact]
        public void Dispose_Invoke_Success ()
        {
            using var notifyIcon = new NotifyIcon ();
            var callCount = 0;
            notifyIcon.Disposed += (sender, e) => callCount++;

            notifyIcon.Dispose ();
            Assert.Null (notifyIcon.Icon);
            Assert.Equal (1, callCount);

            notifyIcon.Dispose ();
            Assert.Null (notifyIcon.Icon);
            Assert.Equal (2, callCount);
        }

        [Fact]
        public void Dispose_InvokePropertiesSet_RetainsNonIconState ()
        {
            using var menu = new ContextMenuStrip ();
            using var notifyIcon = new NotifyIcon {
                BalloonTipIcon = ToolTipIcon.Error,
                BalloonTipText = "BalloonTipText",
                BalloonTipTitle = "BalloonTipTitle",
                ContextMenuStrip = menu,
                Text = "Text",
                Tag = "Tag",
                Visible = true
            };
            var callCount = 0;
            notifyIcon.Disposed += (sender, e) => callCount++;

            notifyIcon.Dispose ();

            // WinForms leaves these scalar/state properties intact after Dispose; only the Icon is released.
            Assert.Equal (ToolTipIcon.Error, notifyIcon.BalloonTipIcon);
            Assert.Equal ("BalloonTipText", notifyIcon.BalloonTipText);
            Assert.Equal ("BalloonTipTitle", notifyIcon.BalloonTipTitle);
            Assert.Equal ("Tag", notifyIcon.Tag);
            Assert.Null (notifyIcon.Icon);
            Assert.Equal (1, callCount);
        }

        [Theory]
        [InlineData (ToolTipIcon.None)]
        [InlineData (ToolTipIcon.Info)]
        [InlineData (ToolTipIcon.Warning)]
        [InlineData (ToolTipIcon.Error)]
        public void ShowBalloonTip_InvokeInt_Success (ToolTipIcon tipIcon)
        {
            using var notifyIcon = new NotifyIcon {
                BalloonTipTitle = "BalloonTipTitle",
                BalloonTipText = "BalloonTipText",
                BalloonTipIcon = tipIcon
            };

            // No-op in Continuum.Forms (no tray), but must not throw with valid arguments.
            notifyIcon.ShowBalloonTip (0);
        }

        [Theory]
        [InlineData (ToolTipIcon.None)]
        [InlineData (ToolTipIcon.Info)]
        [InlineData (ToolTipIcon.Warning)]
        [InlineData (ToolTipIcon.Error)]
        public void ShowBalloonTip_InvokeIntStringStringToolTipIcon_Success (ToolTipIcon tipIcon)
        {
            using var notifyIcon = new NotifyIcon ();
            notifyIcon.ShowBalloonTip (0, "tipTitle", "tipText", tipIcon);
        }

        [Fact]
        public void ShowBalloonTip_InvokeNegativeTimeout_ThrowsArgumentOutOfRangeException ()
        {
            using var notifyIcon = new NotifyIcon { BalloonTipText = "Text" };
            Assert.Throws<ArgumentOutOfRangeException> ("timeout", () => notifyIcon.ShowBalloonTip (-1));
            Assert.Throws<ArgumentOutOfRangeException> ("timeout", () => notifyIcon.ShowBalloonTip (-1, "Title", "Text", ToolTipIcon.Error));
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        public void ShowBalloonTip_InvokeInvalidText_ThrowsArgumentException (string? tipText)
        {
            using var notifyIcon = new NotifyIcon { BalloonTipText = tipText! };
            Assert.Throws<ArgumentException> (() => notifyIcon.ShowBalloonTip (0));
            Assert.Throws<ArgumentException> (() => notifyIcon.ShowBalloonTip (0, "Title", tipText!, ToolTipIcon.Error));
        }

        [Theory]
        [InlineData ((ToolTipIcon)(-1))]
        [InlineData ((ToolTipIcon)4)]
        public void ShowBalloonTip_InvokeInvalidTipIcon_ThrowsInvalidEnumArgumentException (ToolTipIcon tipIcon)
        {
            using var notifyIcon = new NotifyIcon ();
            Assert.Throws<InvalidEnumArgumentException> ("tipIcon", () => notifyIcon.ShowBalloonTip (0, "Title", "Text", tipIcon));
        }
    }
}
