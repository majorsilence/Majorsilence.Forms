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
            Assert.Equal (MaskFormat.IncludeLiterals, control.CutCopyMaskFormat);
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
        public void Text_IsFormattedByTheMask ()
        {
            // Inverted with W5.13 (TXT-03). This asserted that the mask did NOT affect the text, which
            // was the whole defect: a phone/SSN field accepted anything and the value an app parsed no
            // longer carried the mask's literal separators.
            using var control = new MaskedTextBox { Mask = "000-000" };

            control.Text = "123456";

            // The default TextMaskFormat is IncludeLiterals, so the separator is part of the value.
            Assert.Equal ("123-456", control.Text);

            // Input that does not fit the mask is rejected rather than stored verbatim. Asserting the
            // rejection rather than the resulting string: what an empty masked field renders for
            // unfilled positions is the BCL provider's business (it pads with spaces when literals are
            // included), and pinning that string here would be transcribing its implementation.
            var rejected = 0;
            control.MaskInputRejected += (_, _) => rejected++;

            control.Text = "abc";

            Assert.DoesNotContain ("a", control.Text);
            Assert.False (control.MaskCompleted);
            Assert.Equal (1, rejected);
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

        // CutCopyMaskFormat is a MaskFormat, as WinForms declares it -- it was a bool here, which no
        // caller holding a MaskFormat could assign to.
        [Theory]
        [InlineData (MaskFormat.IncludeLiterals)]
        [InlineData (MaskFormat.IncludePrompt)]
        [InlineData (MaskFormat.IncludePromptAndLiterals)]
        [InlineData (MaskFormat.ExcludePromptAndLiterals)]
        public void CutCopyMaskFormat_Set_GetReturnsExpected (MaskFormat value)
        {
            using var control = new MaskedTextBox { CutCopyMaskFormat = value };
            Assert.Equal (value, control.CutCopyMaskFormat);

            // Set different.
            var other = value == MaskFormat.IncludeLiterals ? MaskFormat.IncludePrompt : MaskFormat.IncludeLiterals;
            control.CutCopyMaskFormat = other;
            Assert.Equal (other, control.CutCopyMaskFormat);
        }

        [Fact]
        public void MaskedTextProvider_IsNullWithoutAMaskAndDescribesTheMaskWithOne ()
        {
            using var control = new MaskedTextBox ();

            // No mask means no provider, as upstream -- there is nothing for one to describe.
            Assert.Null (control.MaskedTextProvider);

            control.Mask = "000-000";
            control.Text = "123456";

            var provider = control.MaskedTextProvider;

            Assert.NotNull (provider);
            Assert.Equal ("000-000", provider!.Mask);
            Assert.Equal ('_', provider.PromptChar);
            Assert.Equal ("123-456", provider.ToDisplayString ());
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
        public void MaskCompletedAndMaskFull_ReflectWhatIsFilledIn (string mask)
        {
            // Inverted with W5.13 (TXT-03). These were `=> true` unconditionally, so mask validation
            // always passed and the documented `if (!mtb.MaskCompleted) { error }` guard was dead code.
            using var control = new MaskedTextBox { Mask = mask, Text = "1" };

            var maskless = string.IsNullOrEmpty (mask);

            // A box with no mask has nothing to complete, so it answers true -- as upstream does.
            Assert.Equal (maskless, control.MaskCompleted);
            Assert.Equal (maskless, control.MaskFull);
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
