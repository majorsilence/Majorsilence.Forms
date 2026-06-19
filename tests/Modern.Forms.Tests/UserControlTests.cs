// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/UserControlTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms UserControlTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility/ImeMode/Site/Moq plumbing). They
    // pin the property get/set semantics (Text, AutoSize, AutoSizeMode, BorderStyle, AutoValidate),
    // container behavior, and the Load event. Where Modern.Forms intentionally diverges from
    // WinForms (e.g. DefaultSize, default AutoScaleMode), the assertions follow the Modern.Forms
    // source rather than WinForms.
    public class UserControlTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new UserControl ();

            Assert.True (control.TabStop);
            Assert.False (control.AutoSize);
            Assert.Equal (AutoSizeMode.GrowOnly, control.AutoSizeMode);
            Assert.Equal (BorderStyle.None, control.BorderStyle);
            Assert.Equal (AutoValidate.EnablePreventFocusChange, control.AutoValidate);
            Assert.Equal (AutoScaleMode.Font, control.AutoScaleMode);
            Assert.Null (control.ActiveControl);
            Assert.Empty (control.Text);
            Assert.Empty (control.Controls);
            Assert.Same (control.Controls, control.Controls);
            Assert.Null (control.Parent);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("text")]
        [InlineData ("a longer string value")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new UserControl { Text = value };

            Assert.Equal (value, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void Text_SetWithHandler_CallsTextChanged ()
        {
            using var control = new UserControl ();
            var callCount = 0;

            void handler (object? sender, EventArgs e)
            {
                Assert.Same (control, sender);
                Assert.Equal (EventArgs.Empty, e);
                callCount++;
            }

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
        public void AutoSize_Set_GetReturnsExpected (bool value)
        {
            using var control = new UserControl ();

            control.AutoSize = value;
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
            using var control = new UserControl { AutoSize = true };
            var callCount = 0;

            void handler (object? sender, EventArgs e)
            {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            }

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
        [InlineData (AutoSizeMode.GrowAndShrink)]
        [InlineData (AutoSizeMode.GrowOnly)]
        public void AutoSizeMode_Set_GetReturnsExpected (AutoSizeMode value)
        {
            using var control = new UserControl { AutoSizeMode = value };

            Assert.Equal (value, control.AutoSizeMode);

            // Set same.
            control.AutoSizeMode = value;
            Assert.Equal (value, control.AutoSizeMode);
        }

        [Theory]
        [InlineData ((AutoSizeMode) (-1))]
        [InlineData ((AutoSizeMode) 2)]
        public void AutoSizeMode_SetInvalid_ThrowsInvalidEnumArgumentException (AutoSizeMode value)
        {
            using var control = new UserControl ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.AutoSizeMode = value);
        }

        [Theory]
        [InlineData (BorderStyle.None)]
        [InlineData (BorderStyle.FixedSingle)]
        [InlineData (BorderStyle.Fixed3D)]
        public void BorderStyle_Set_GetReturnsExpected (BorderStyle value)
        {
            using var control = new UserControl { BorderStyle = value };

            Assert.Equal (value, control.BorderStyle);

            // Set same.
            control.BorderStyle = value;
            Assert.Equal (value, control.BorderStyle);
        }

        [Theory]
        [InlineData ((BorderStyle) (-1))]
        [InlineData ((BorderStyle) 3)]
        public void BorderStyle_SetInvalid_ThrowsInvalidEnumArgumentException (BorderStyle value)
        {
            using var control = new UserControl ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.BorderStyle = value);
        }

        [Theory]
        [InlineData (AutoValidate.Disable)]
        [InlineData (AutoValidate.EnablePreventFocusChange)]
        [InlineData (AutoValidate.EnableAllowFocusChange)]
        [InlineData (AutoValidate.Inherit)]
        public void AutoValidate_Set_GetReturnsExpected (AutoValidate value)
        {
            using var control = new UserControl { AutoValidate = value };

            Assert.Equal (value, control.AutoValidate);

            // Set same.
            control.AutoValidate = value;
            Assert.Equal (value, control.AutoValidate);
        }

        [Theory]
        [InlineData (AutoScaleMode.None)]
        [InlineData (AutoScaleMode.Font)]
        [InlineData (AutoScaleMode.Dpi)]
        [InlineData (AutoScaleMode.Inherit)]
        public void AutoScaleMode_Set_GetReturnsExpected (AutoScaleMode value)
        {
            using var control = new UserControl { AutoScaleMode = value };

            Assert.Equal (value, control.AutoScaleMode);
        }

        [Fact]
        public void Controls_Add_GetReturnsExpected ()
        {
            using var control = new UserControl ();
            using var child = new Control ();

            control.Controls.Add (child);

            Assert.Single (control.Controls);
            Assert.Same (child, control.Controls[0]);
            Assert.Same (control, child.Parent);
        }

        [Fact]
        public void Controls_AddRemove_Success ()
        {
            using var control = new UserControl ();
            using var child = new Control ();

            control.Controls.Add (child);
            Assert.Single (control.Controls);

            control.Controls.Remove (child);
            Assert.Empty (control.Controls);
            Assert.Null (child.Parent);
        }

        [Fact]
        public void ActiveControl_Set_GetReturnsExpected ()
        {
            using var control = new UserControl ();
            using var child = new Control ();
            control.Controls.Add (child);

            control.ActiveControl = child;
            Assert.Same (child, control.ActiveControl);

            control.ActiveControl = null;
            Assert.Null (control.ActiveControl);
        }

        [Fact]
        public void ValidateChildren_InvokeWithoutChildren_ReturnsTrue ()
        {
            using var control = new UserControl ();
            Assert.True (control.ValidateChildren ());
        }

        [Fact]
        public void ValidateChildren_InvokeWithChildren_ReturnsTrue ()
        {
            using var control = new UserControl ();
            using var child = new Control ();
            control.Controls.Add (child);

            Assert.True (control.ValidateChildren ());
        }

        [Fact]
        public void Load_AddRemoveHandler_Success ()
        {
            using var control = new UserControl ();
            var callCount = 0;

            void handler (object? sender, EventArgs e) => callCount++;

            // Adding and removing the handler must not throw and must not raise on its own.
            control.Load += handler;
            control.Load -= handler;

            Assert.Equal (0, callCount);
        }
    }
}
