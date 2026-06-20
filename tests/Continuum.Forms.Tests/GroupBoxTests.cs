// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/GroupBoxTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms GroupBoxTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing). They pin
    // the default property values plus the Text/TextChanged, FlatStyle, AutoSize,
    // UseCompatibleTextRendering and TabStop semantics against the real Continuum.Forms surface.
    public class GroupBoxTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new GroupBox ();

            Assert.Equal (new Size (200, 100), control.Size);
            Assert.Equal (200, control.Width);
            Assert.Equal (100, control.Height);
            Assert.Equal (FlatStyle.Standard, control.FlatStyle);
            Assert.False (control.AutoSize);
            Assert.True (control.UseCompatibleTextRendering);
            Assert.Equal (string.Empty, control.Text);
            // GroupBox is a non-selectable container, so it disables TabStop in its ctor
            // (the Control default is true).
            Assert.False (control.TabStop);
        }

        [Fact]
        public void DefaultPadding_IsAccountedForTitleBar ()
        {
            // GroupBox reserves extra top padding for its title; the other sides use the
            // standard 3px padding.
            using var control = new GroupBox ();

            Assert.Equal (new Padding (3, 16, 3, 3), control.Padding);
        }

        [Theory]
        [InlineData (FlatStyle.Flat)]
        [InlineData (FlatStyle.Popup)]
        [InlineData (FlatStyle.Standard)]
        [InlineData (FlatStyle.System)]
        public void FlatStyle_Set_GetReturnsExpected (FlatStyle value)
        {
            using var control = new GroupBox { FlatStyle = value };
            Assert.Equal (value, control.FlatStyle);

            // Set same.
            control.FlatStyle = value;
            Assert.Equal (value, control.FlatStyle);

            // Set different.
            control.FlatStyle = FlatStyle.Standard;
            Assert.Equal (FlatStyle.Standard, control.FlatStyle);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoSize_Set_GetReturnsExpected (bool value)
        {
            using var control = new GroupBox { AutoSize = value };
            Assert.Equal (value, control.AutoSize);

            // Set same.
            control.AutoSize = value;
            Assert.Equal (value, control.AutoSize);

            // Set different.
            control.AutoSize = !value;
            Assert.Equal (!value, control.AutoSize);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void UseCompatibleTextRendering_Set_GetReturnsExpected (bool value)
        {
            using var control = new GroupBox { UseCompatibleTextRendering = value };
            Assert.Equal (value, control.UseCompatibleTextRendering);

            // Set same.
            control.UseCompatibleTextRendering = value;
            Assert.Equal (value, control.UseCompatibleTextRendering);

            // Set different.
            control.UseCompatibleTextRendering = !value;
            Assert.Equal (!value, control.UseCompatibleTextRendering);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("text")]
        [InlineData ("a b c")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new GroupBox { Text = value };
            Assert.Equal (value, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void Text_SetWithHandler_CallsTextChanged ()
        {
            using var control = new GroupBox ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Equal (EventArgs.Empty, e);
                callCount++;
            };
            control.TextChanged += handler;

            // Set different.
            control.Text = "text";
            Assert.Equal ("text", control.Text);
            Assert.Equal (1, callCount);

            // Set same.
            control.Text = "text";
            Assert.Equal ("text", control.Text);
            Assert.Equal (1, callCount);

            // Set different.
            control.Text = "other";
            Assert.Equal ("other", control.Text);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.TextChanged -= handler;
            control.Text = "text";
            Assert.Equal ("text", control.Text);
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void TabStop_Set_GetReturnsExpected (bool value)
        {
            using var control = new GroupBox { TabStop = value };
            Assert.Equal (value, control.TabStop);

            // Set same.
            control.TabStop = value;
            Assert.Equal (value, control.TabStop);

            // Set different.
            control.TabStop = !value;
            Assert.Equal (!value, control.TabStop);
        }

        [Fact]
        public void TabStop_SetWithHandler_CallsTabStopChanged ()
        {
            using var control = new GroupBox { TabStop = true };
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.TabStopChanged += handler;

            // Set different.
            control.TabStop = false;
            Assert.False (control.TabStop);
            Assert.Equal (1, callCount);

            // Set same.
            control.TabStop = false;
            Assert.False (control.TabStop);
            Assert.Equal (1, callCount);

            // Set different.
            control.TabStop = true;
            Assert.True (control.TabStop);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.TabStopChanged -= handler;
            control.TabStop = false;
            Assert.False (control.TabStop);
            Assert.Equal (2, callCount);
        }
    }
}
