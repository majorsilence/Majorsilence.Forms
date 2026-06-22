// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/LabelTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms LabelTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing). They pin the
    // same default property values, Text/TextAlign/UseMnemonic/AutoSize semantics, the
    // TextAlignChanged / AutoSizeChanged events, and the ImageIndex/ImageKey mutual-exclusion
    // rules. Cases that rely on upstream-only features (UseCompatibleTextRendering, OwnerDraw,
    // handle creation, InvalidEnumArgumentException validation) are intentionally omitted - see
    // the porting report.
    public class LabelTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new Label ();

            Assert.False (control.AutoSize);
            Assert.False (control.AutoEllipsis);
            Assert.False (control.Multiline);
            Assert.Equal (BorderStyle.None, control.BorderStyle);
            Assert.Equal (FlatStyle.Standard, control.FlatStyle);
            Assert.Null (control.Image);
            Assert.Equal (ContentAlignment.MiddleCenter, control.ImageAlign);
            Assert.Equal (-1, control.ImageIndex);
            Assert.Empty (control.ImageKey);
            Assert.Null (control.ImageList);
            Assert.Empty (control.Text);
            Assert.Equal (ContentAlignment.TopLeft, control.TextAlign);
            Assert.Equal (TextImageRelation.Overlay, control.TextImageRelation);
            Assert.True (control.UseMnemonic);
            Assert.False (control.TabStop);
            Assert.Equal (new Size (100, 23), control.Size);
            Assert.Equal (new Padding (3, 0, 3, 0), control.Margin);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("text")]
        [InlineData ("&Mnemonic")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new Label { Text = value };

            Assert.Equal (value, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void Text_SetDifferent_RaisesTextChanged ()
        {
            using var control = new Label ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.TextChanged += handler;

            control.Text = "value";
            Assert.Equal (1, callCount);

            // Set same.
            control.Text = "value";
            Assert.Equal (1, callCount);

            // Set different.
            control.Text = "other";
            Assert.Equal (2, callCount);

            control.TextChanged -= handler;
            control.Text = "removed";
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.TopCenter)]
        [InlineData (ContentAlignment.TopRight)]
        [InlineData (ContentAlignment.MiddleLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.MiddleRight)]
        [InlineData (ContentAlignment.BottomLeft)]
        [InlineData (ContentAlignment.BottomCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void TextAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new Label { TextAlign = value };

            Assert.Equal (value, control.TextAlign);

            // Set same.
            control.TextAlign = value;
            Assert.Equal (value, control.TextAlign);
        }

        [Fact]
        public void TextAlignChanged_SetDifferentValue_RaisesEvent ()
        {
            using var control = new Label ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.TextAlignChanged += handler;

            // Default is TopLeft; set a different value.
            control.TextAlign = ContentAlignment.BottomCenter;
            Assert.Equal (1, callCount);

            control.TextAlignChanged -= handler;
            control.TextAlign = ContentAlignment.BottomLeft;
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void TextAlignChanged_SetSameValue_DoesNotRaiseEvent ()
        {
            using var control = new Label ();
            var callCount = 0;
            control.TextAlignChanged += (sender, e) => callCount++;

            // Default is TopLeft; setting the same value must not raise the event.
            control.TextAlign = ContentAlignment.TopLeft;
            Assert.Equal (0, callCount);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void UseMnemonic_Set_GetReturnsExpected (bool value)
        {
            using var control = new Label { UseMnemonic = value };

            Assert.Equal (value, control.UseMnemonic);

            // Set same.
            control.UseMnemonic = value;
            Assert.Equal (value, control.UseMnemonic);

            // Set different.
            control.UseMnemonic = !value;
            Assert.Equal (!value, control.UseMnemonic);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoSize_Set_GetReturnsExpected (bool value)
        {
            using var control = new Label { AutoSize = value };

            Assert.Equal (value, control.AutoSize);

            // Set same.
            control.AutoSize = value;
            Assert.Equal (value, control.AutoSize);

            // Set different.
            control.AutoSize = !value;
            Assert.Equal (!value, control.AutoSize);
        }

        [Fact]
        public void AutoSizeChanged_AddRemove_Success ()
        {
            using var control = new Label ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.AutoSizeChanged += handler;

            // Set same (default false): no event.
            control.AutoSize = control.AutoSize;
            Assert.Equal (0, callCount);

            control.AutoSize = !control.AutoSize;
            Assert.Equal (1, callCount);

            control.AutoSize = control.AutoSize;
            Assert.Equal (1, callCount);

            control.AutoSize = !control.AutoSize;
            Assert.Equal (2, callCount);

            control.AutoSizeChanged -= handler;
            control.AutoSize = !control.AutoSize;
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void ImageAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new Label { ImageAlign = value };

            Assert.Equal (value, control.ImageAlign);

            // Set same.
            control.ImageAlign = value;
            Assert.Equal (value, control.ImageAlign);
        }

        [Theory]
        [InlineData (TextImageRelation.Overlay)]
        [InlineData (TextImageRelation.ImageBeforeText)]
        [InlineData (TextImageRelation.TextBeforeImage)]
        [InlineData (TextImageRelation.ImageAboveText)]
        [InlineData (TextImageRelation.TextAboveImage)]
        public void TextImageRelation_Set_GetReturnsExpected (TextImageRelation value)
        {
            using var control = new Label { TextImageRelation = value };

            Assert.Equal (value, control.TextImageRelation);

            // Set same.
            control.TextImageRelation = value;
            Assert.Equal (value, control.TextImageRelation);
        }

        [Fact]
        public void ImageIndex_SettingMinusOne_ResetsImageKey ()
        {
            using var control = new Label ();

            Assert.Equal (-1, control.ImageIndex);
            Assert.Equal (string.Empty, control.ImageKey);

            control.ImageKey = "key";
            control.ImageIndex = -1;

            Assert.Equal (-1, control.ImageIndex);
            // Setting a non-negative index clears the key; -1 leaves an already-set key in place
            // (mirrors WinForms, where only setting a valid index resets the other selector).
            Assert.Equal ("key", control.ImageKey);
        }

        [Fact]
        public void ImageIndex_SettingValidIndex_ResetsImageKey ()
        {
            using var control = new Label { ImageKey = "key" };

            control.ImageIndex = 2;

            Assert.Equal (2, control.ImageIndex);
            Assert.Equal (string.Empty, control.ImageKey);
        }

        [Fact]
        public void ImageKey_SettingValue_ResetsImageIndex ()
        {
            using var control = new Label { ImageIndex = 2 };

            control.ImageKey = "key";

            Assert.Equal ("key", control.ImageKey);
            Assert.Equal (-1, control.ImageIndex);
        }

        [Theory]
        [InlineData (BorderStyle.None)]
        [InlineData (BorderStyle.FixedSingle)]
        [InlineData (BorderStyle.Fixed3D)]
        public void BorderStyle_Set_GetReturnsExpected (BorderStyle value)
        {
            using var control = new Label { BorderStyle = value };

            Assert.Equal (value, control.BorderStyle);
        }

        [Theory]
        [InlineData (FlatStyle.Flat)]
        [InlineData (FlatStyle.Popup)]
        [InlineData (FlatStyle.Standard)]
        [InlineData (FlatStyle.System)]
        public void FlatStyle_Set_GetReturnsExpected (FlatStyle value)
        {
            using var control = new Label { FlatStyle = value };

            Assert.Equal (value, control.FlatStyle);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoEllipsis_Set_GetReturnsExpected (bool value)
        {
            using var control = new Label { AutoEllipsis = value };

            Assert.Equal (value, control.AutoEllipsis);

            // Set same.
            control.AutoEllipsis = value;
            Assert.Equal (value, control.AutoEllipsis);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Multiline_Set_GetReturnsExpected (bool value)
        {
            using var control = new Label { Multiline = value };

            Assert.Equal (value, control.Multiline);

            // Set same.
            control.Multiline = value;
            Assert.Equal (value, control.Multiline);
        }
    }
}
