// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ButtonTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ButtonTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/ImeMode/Moq plumbing). They pin the
    // default property values, the Text/TextAlign/DialogResult/FlatStyle get-set semantics, the
    // TextChanged event, and the PerformClick -> OnClick -> Click pipeline.
    //
    // Where Majorsilence.Forms deliberately differs from WinForms (left-aligned text/image defaults,
    // null Text not coerced to string.Empty, no enum-argument validation) the tests assert
    // Majorsilence.Forms' actual behavior; those divergences are described in the porting report.
    //
    // A small SubButton subclass mirrors the upstream test helper so the protected OnClick can be
    // exercised deterministically without rendering.
    public class ButtonTests
    {
        private sealed class SubButton : Button
        {
            // MF's OnClick takes MouseEventArgs; expose a parameterless invoker that supplies a
            // synthetic left-click, matching the upstream OnClick (EventArgs) helper.
            public void InvokeClick () =>
                OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));
        }

        [Fact]
        public void Ctor_Default ()
        {
            using var control = new Button ();

            Assert.False (control.AutoEllipsis);
            Assert.False (control.AutoSize);
            Assert.Equal (DialogResult.None, control.DialogResult);
            Assert.Equal (FlatStyle.Standard, control.FlatStyle);
            Assert.NotNull (control.FlatAppearance);
            Assert.Null (control.Image);
            Assert.Equal (ContentAlignment.MiddleLeft, control.ImageAlign);
            Assert.Equal (-1, control.ImageIndex);
            Assert.Empty (control.ImageKey);
            Assert.Null (control.ImageList);
            Assert.Empty (control.Text);
            Assert.Equal (ContentAlignment.MiddleLeft, control.TextAlign);
            Assert.Equal (TextImageRelation.ImageBeforeText, control.TextImageRelation);
            Assert.False (control.UseCompatibleTextRendering);
            Assert.Equal (new Size (100, 30), control.Size);
        }

        [Theory]
        // NOTE: WinForms coerces a null Text to string.Empty; Majorsilence.Forms does not (the
        // coercion would have to live in the base Control.Text setter, which is out of scope
        // here). Only non-null values are exercised so the test reflects MF's real behavior.
        [InlineData ("", "")]
        [InlineData ("text", "text")]
        public void Text_Set_GetReturnsExpected (string value, string expected)
        {
            using var control = new Button { Text = value };

            Assert.Equal (expected, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (expected, control.Text);
        }

        [Fact]
        public void Text_SetWithHandler_CallsTextChanged ()
        {
            using var control = new Button ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.TextChanged += handler;

            // Set different.
            control.Text = "text";
            Assert.Equal ("text", control.Text);
            Assert.Equal (1, callCount);

            // Set same. No change, no event.
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
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void TextAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new Button { TextAlign = value };

            Assert.Equal (value, control.TextAlign);

            // Set same.
            control.TextAlign = value;
            Assert.Equal (value, control.TextAlign);
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void ImageAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new Button { ImageAlign = value };

            Assert.Equal (value, control.ImageAlign);

            // Set same.
            control.ImageAlign = value;
            Assert.Equal (value, control.ImageAlign);
        }

        [Theory]
        [InlineData (TextImageRelation.Overlay)]
        [InlineData (TextImageRelation.ImageAboveText)]
        [InlineData (TextImageRelation.TextBeforeImage)]
        public void TextImageRelation_Set_GetReturnsExpected (TextImageRelation value)
        {
            using var control = new Button { TextImageRelation = value };

            Assert.Equal (value, control.TextImageRelation);

            // Set same.
            control.TextImageRelation = value;
            Assert.Equal (value, control.TextImageRelation);
        }

        [Theory]
        [InlineData (DialogResult.None)]
        [InlineData (DialogResult.OK)]
        [InlineData (DialogResult.Cancel)]
        [InlineData (DialogResult.Abort)]
        [InlineData (DialogResult.Retry)]
        [InlineData (DialogResult.Ignore)]
        [InlineData (DialogResult.Yes)]
        [InlineData (DialogResult.No)]
        public void DialogResult_Set_GetReturnsExpected (DialogResult value)
        {
            using var control = new Button { DialogResult = value };

            Assert.Equal (value, control.DialogResult);

            // Set same.
            control.DialogResult = value;
            Assert.Equal (value, control.DialogResult);
        }

        [Theory]
        [InlineData (FlatStyle.Flat)]
        [InlineData (FlatStyle.Popup)]
        [InlineData (FlatStyle.Standard)]
        [InlineData (FlatStyle.System)]
        public void FlatStyle_Set_GetReturnsExpected (FlatStyle value)
        {
            using var control = new Button { FlatStyle = value };

            Assert.Equal (value, control.FlatStyle);

            // Set same.
            control.FlatStyle = value;
            Assert.Equal (value, control.FlatStyle);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoEllipsis_Set_GetReturnsExpected (bool value)
        {
            using var control = new Button { AutoEllipsis = value };

            Assert.Equal (value, control.AutoEllipsis);

            // Set same.
            control.AutoEllipsis = value;
            Assert.Equal (value, control.AutoEllipsis);

            // Set different.
            control.AutoEllipsis = !value;
            Assert.Equal (!value, control.AutoEllipsis);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void UseCompatibleTextRendering_Set_GetReturnsExpected (bool value)
        {
            using var control = new Button { UseCompatibleTextRendering = value };

            Assert.Equal (value, control.UseCompatibleTextRendering);
        }

        [Fact]
        public void ImageIndex_Set_GetReturnsExpected ()
        {
            using var control = new Button { ImageIndex = 2 };

            Assert.Equal (2, control.ImageIndex);

            // Set same.
            control.ImageIndex = 2;
            Assert.Equal (2, control.ImageIndex);
        }

        [Fact]
        public void ImageKey_Set_GetReturnsExpected ()
        {
            using var control = new Button { ImageKey = "key" };

            Assert.Equal ("key", control.ImageKey);

            // Set same.
            control.ImageKey = "key";
            Assert.Equal ("key", control.ImageKey);
        }

        [Fact]
        public void OnClick_Invoke_CallsClick ()
        {
            using var control = new SubButton ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                callCount++;
            };

            // Call with handler.
            control.Click += handler;
            control.InvokeClick ();
            Assert.Equal (1, callCount);

            // Remove handler.
            control.Click -= handler;
            control.InvokeClick ();
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void PerformClick_Invoke_CallsClick ()
        {
            using var control = new Button ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                callCount++;
            };

            // Call with handler.
            control.Click += handler;
            control.PerformClick ();
            Assert.Equal (1, callCount);

            // Remove handler.
            control.Click -= handler;
            control.PerformClick ();
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void ToString_Invoke_ReturnsExpected ()
        {
            using var control = new Button ();

            Assert.Equal ("Majorsilence.Forms.Button, Text: ", control.ToString ());
        }

        [Fact]
        public void ToString_InvokeShortText_ReturnsExpected ()
        {
            using var control = new Button { Text = "Text" };

            Assert.Equal ("Majorsilence.Forms.Button, Text: Text", control.ToString ());
        }
    }
}
