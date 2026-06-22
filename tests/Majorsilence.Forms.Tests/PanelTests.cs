// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/PanelTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms PanelTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/Ime plumbing). They pin the
    // default ctor values, BorderStyle get/set + enum validation, AutoSize/TabStop/Text
    // get/set semantics and their change events.
    public class PanelTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new Panel ();

            Assert.False (control.AutoScroll);
            Assert.Equal (Size.Empty, control.AutoScrollMargin);
            Assert.Equal (Size.Empty, control.AutoScrollMinSize);
            Assert.False (control.AutoSize);
            Assert.Equal (BorderStyle.None, control.BorderStyle);
            Assert.Empty (control.Controls);
            Assert.Equal (DockStyle.None, control.Dock);
            Assert.True (control.Enabled);
            Assert.Equal (new Size (200, 100), control.Size);
            Assert.Equal (200, control.Width);
            Assert.Equal (100, control.Height);
            Assert.False (control.TabStop);
            Assert.Empty (control.Text);

            // NOTE: Unlike WinForms (where Visible reflects the intended visibility bit regardless of
            // attachment), Majorsilence.Forms's Visible walks the parent chain (parent?.Visible ?? false), so
            // a parentless control reports false. This is intentional MF behavior, not a gap.
            Assert.False (control.Visible);
        }

        [Theory]
        [InlineData (BorderStyle.None)]
        [InlineData (BorderStyle.FixedSingle)]
        [InlineData (BorderStyle.Fixed3D)]
        public void BorderStyle_Set_GetReturnsExpected (BorderStyle value)
        {
            using var control = new Panel { BorderStyle = value };

            Assert.Equal (value, control.BorderStyle);

            // Set same.
            control.BorderStyle = value;
            Assert.Equal (value, control.BorderStyle);
        }

        [Theory]
        [InlineData ((BorderStyle)(-1))]
        [InlineData ((BorderStyle)3)]
        [InlineData ((BorderStyle)int.MaxValue)]
        public void BorderStyle_SetInvalid_ThrowsInvalidEnumArgumentException (BorderStyle value)
        {
            using var control = new Panel ();

            Assert.Throws<InvalidEnumArgumentException> (() => control.BorderStyle = value);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoSize_Set_GetReturnsExpected (bool value)
        {
            using var control = new Panel { AutoSize = value };

            Assert.Equal (value, control.AutoSize);

            // Set same.
            control.AutoSize = value;
            Assert.Equal (value, control.AutoSize);

            // Set different.
            control.AutoSize = !value;
            Assert.Equal (!value, control.AutoSize);
        }

        [Fact]
        public void AutoSize_SetWithHandler_CallsAutoSizeChanged ()
        {
            using var control = new Panel { AutoSize = true };
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.AutoSizeChanged += handler;

            // Set different.
            control.AutoSize = false;
            Assert.False (control.AutoSize);
            Assert.Equal (1, callCount);

            // Set same.
            control.AutoSize = false;
            Assert.False (control.AutoSize);
            Assert.Equal (1, callCount);

            // Set different.
            control.AutoSize = true;
            Assert.True (control.AutoSize);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.AutoSizeChanged -= handler;
            control.AutoSize = false;
            Assert.False (control.AutoSize);
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void TabStop_Set_GetReturnsExpected (bool value)
        {
            using var control = new Panel { TabStop = value };

            Assert.Equal (value, control.TabStop);

            // Set same.
            control.TabStop = value;
            Assert.Equal (value, control.TabStop);
        }

        [Fact]
        public void TabStop_SetWithHandler_CallsTabStopChanged ()
        {
            using var control = new Panel { TabStop = true };
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

        [Theory]
        [InlineData ("")]
        [InlineData ("text")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new Panel { Text = value };

            Assert.Equal (value, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void Text_SetWithHandler_CallsTextChanged ()
        {
            using var control = new Panel ();
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

            // Remove handler.
            control.TextChanged -= handler;
            control.Text = "other";
            Assert.Equal ("other", control.Text);
            Assert.Equal (1, callCount);
        }
    }
}
