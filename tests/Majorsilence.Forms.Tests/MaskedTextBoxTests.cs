// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/MaskedTextBoxTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Globalization;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms MaskedTextBoxTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility plumbing). Majorsilence.Forms does NOT
    // enforce the mask; MaskedTextBox behaves as a plain TextBox with WinForms-compatible
    // property surface. These tests pin the ACTUAL Majorsilence.Forms behavior (property round-trips,
    // defaults, and the always-true MaskCompleted/MaskFull stubs), not full WinForms masking.
    public class MaskedTextBoxTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new MaskedTextBox ();

            Assert.Equal (string.Empty, control.Mask);
            Assert.Equal ('_', control.PromptChar);
            Assert.False (control.BeepOnError);
            Assert.False (control.AsciiOnly);
            Assert.False (control.HidePromptOnLeave);
            Assert.False (control.UseSystemPasswordChar);
            Assert.False (control.CutCopyMaskFormat);
            Assert.Equal (MaskFormat.IncludeLiterals, control.TextMaskFormat);
            Assert.Null (control.Culture);
            Assert.Equal (string.Empty, control.Text);

            // MaskCompleted/MaskFull are always-true stubs in Majorsilence.Forms.
            Assert.True (control.MaskCompleted);
            Assert.True (control.MaskFull);

            // Inherited TextBox defaults relied on by WinForms callers.
            Assert.False (control.Multiline);
            Assert.False (control.ReadOnly);
            Assert.False (control.Modified);
            Assert.Equal (HorizontalAlignment.Left, control.TextAlign);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("00000")]
        [InlineData ("00-00")]
        [InlineData ("(000) 000-0000")]
        public void Mask_Set_GetReturnsExpected (string value)
        {
            using var control = new MaskedTextBox { Mask = value };
            Assert.Equal (value, control.Mask);

            // Set same.
            control.Mask = value;
            Assert.Equal (value, control.Mask);
        }

        [Fact]
        public void Mask_SetNull_GetReturnsEmpty ()
        {
            using var control = new MaskedTextBox { Mask = "00000" };

            control.Mask = null!;
            Assert.Equal (string.Empty, control.Mask);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("12345")]
        [InlineData ("Hello, World!")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new MaskedTextBox { Text = value };
            Assert.Equal (value, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void Text_SetUnaffectedByMask ()
        {
            // Majorsilence.Forms does not enforce the mask, so text is stored verbatim.
            using var control = new MaskedTextBox { Mask = "000-000" };

            control.Text = "1234567";
            Assert.Equal ("1234567", control.Text);

            control.Text = "123-45";
            Assert.Equal ("123-45", control.Text);
        }

        [Theory]
        [InlineData ('A')]
        [InlineData ('1')]
        [InlineData ('%')]
        [InlineData ('_')]
        public void PromptChar_Set_GetReturnsExpected (char value)
        {
            using var control = new MaskedTextBox { PromptChar = value };
            Assert.Equal (value, control.PromptChar);

            // Set same.
            control.PromptChar = value;
            Assert.Equal (value, control.PromptChar);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void BeepOnError_Set_GetReturnsExpected (bool value)
        {
            using var control = new MaskedTextBox { BeepOnError = value };
            Assert.Equal (value, control.BeepOnError);

            // Set same.
            control.BeepOnError = value;
            Assert.Equal (value, control.BeepOnError);

            // Set different.
            control.BeepOnError = !value;
            Assert.Equal (!value, control.BeepOnError);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AsciiOnly_Set_GetReturnsExpected (bool value)
        {
            using var control = new MaskedTextBox { AsciiOnly = value };
            Assert.Equal (value, control.AsciiOnly);

            // Set same.
            control.AsciiOnly = value;
            Assert.Equal (value, control.AsciiOnly);

            // Set different.
            control.AsciiOnly = !value;
            Assert.Equal (!value, control.AsciiOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void HidePromptOnLeave_Set_GetReturnsExpected (bool value)
        {
            using var control = new MaskedTextBox { HidePromptOnLeave = value };
            Assert.Equal (value, control.HidePromptOnLeave);

            // Set same.
            control.HidePromptOnLeave = value;
            Assert.Equal (value, control.HidePromptOnLeave);

            // Set different.
            control.HidePromptOnLeave = !value;
            Assert.Equal (!value, control.HidePromptOnLeave);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void UseSystemPasswordChar_Set_GetReturnsExpected (bool value)
        {
            using var control = new MaskedTextBox { UseSystemPasswordChar = value };
            Assert.Equal (value, control.UseSystemPasswordChar);

            // Set same.
            control.UseSystemPasswordChar = value;
            Assert.Equal (value, control.UseSystemPasswordChar);

            // Set different.
            control.UseSystemPasswordChar = !value;
            Assert.Equal (!value, control.UseSystemPasswordChar);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void CutCopyMaskFormat_Set_GetReturnsExpected (bool value)
        {
            using var control = new MaskedTextBox { CutCopyMaskFormat = value };
            Assert.Equal (value, control.CutCopyMaskFormat);

            // Set different.
            control.CutCopyMaskFormat = !value;
            Assert.Equal (!value, control.CutCopyMaskFormat);
        }

        [Theory]
        [InlineData (MaskFormat.IncludeLiterals)]
        [InlineData (MaskFormat.IncludePrompt)]
        [InlineData (MaskFormat.IncludePromptAndLiterals)]
        [InlineData (MaskFormat.ExcludePromptAndLiterals)]
        public void TextMaskFormat_Set_GetReturnsExpected (MaskFormat value)
        {
            using var control = new MaskedTextBox { TextMaskFormat = value };
            Assert.Equal (value, control.TextMaskFormat);

            // Set same.
            control.TextMaskFormat = value;
            Assert.Equal (value, control.TextMaskFormat);
        }

        [Fact]
        public void Culture_Set_GetReturnsExpected ()
        {
            using var control = new MaskedTextBox ();
            var culture = new CultureInfo ("fr-FR");

            control.Culture = culture;
            Assert.Equal (culture, control.Culture);

            // Set different.
            var other = new CultureInfo ("en-US");
            control.Culture = other;
            Assert.Equal (other, control.Culture);

            // Set null (Majorsilence.Forms allows null; WinForms throws).
            control.Culture = null;
            Assert.Null (control.Culture);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("00000")]
        [InlineData ("(000) 000-0000")]
        public void MaskCompletedAndMaskFull_AlwaysTrue (string mask)
        {
            // Majorsilence.Forms does not enforce the mask, so these stubs are always true
            // regardless of mask/text completeness.
            using var control = new MaskedTextBox { Mask = mask, Text = "1" };

            Assert.True (control.MaskCompleted);
            Assert.True (control.MaskFull);
        }

        [Fact]
        public void Lines_Get_ReturnsExpected ()
        {
            using var control = new MaskedTextBox { Text = "Line1\nLine2\nLine3" };
            Assert.Equal (new[] { "Line1", "Line2", "Line3" }, control.Lines);
        }

        [Fact]
        public void ReadOnly_Set_GetReturnsExpected ()
        {
            using var control = new MaskedTextBox ();
            var original = control.ReadOnly;

            control.ReadOnly = !original;
            Assert.Equal (!original, control.ReadOnly);

            control.ReadOnly = original;
            Assert.Equal (original, control.ReadOnly);

            Assert.False (control.Modified);
        }

        [Theory]
        [InlineData (HorizontalAlignment.Left)]
        [InlineData (HorizontalAlignment.Center)]
        [InlineData (HorizontalAlignment.Right)]
        public void TextAlign_Set_GetReturnsExpected (HorizontalAlignment value)
        {
            using var control = new MaskedTextBox { TextAlign = value };
            Assert.Equal (value, control.TextAlign);
        }
    }
}
