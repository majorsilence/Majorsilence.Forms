// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests (TextBoxBaseTests.cs / TextBoxTests.cs under
// src/test/unit/System.Windows.Forms/), rewritten for the Majorsilence.Forms API.
// Original work Copyright (c) .NET Foundation and Contributors.

using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class TextBoxTests
    {
        [Fact]
        public void Text_Set_GetReturnsExpected ()
        {
            using var control = new TextBox { Text = "hello" };
            Assert.Equal ("hello", control.Text);
        }

        [Fact]
        public void MaxLength_DefaultsToZero ()
        {
            // WinForms convention: 0 means "no limit".
            using var control = new TextBox ();
            Assert.Equal (0, control.MaxLength);
        }

        [Theory]
        [InlineData (5)]
        [InlineData (1)]
        [InlineData (100)]
        public void MaxLength_Set_GetReturnsExpected (int value)
        {
            // Regression: the setter previously ignored its argument entirely.
            using var control = new TextBox { MaxLength = value };
            Assert.Equal (value, control.MaxLength);
        }

        [Fact]
        public void MaxLength_SetNegative_TreatedAsNoLimit ()
        {
            using var control = new TextBox { MaxLength = -1 };
            Assert.Equal (0, control.MaxLength);
        }

        [Fact]
        public void MaxLength_LimitsTextLengthOnInput ()
        {
            // With a 3-char limit, only the first 3 characters of injected input are accepted.
            using var form = new Form ();
            var control = new TextBox { Left = 0, Top = 0, Width = 200, Height = 30, MaxLength = 3 };
            form.Controls.Add (control);
            form.Show ();

            Headless.HeadlessRenderer.CapturePng (form, 200, 60);   // layout pass
            Headless.HeadlessRenderer.Click (form, 100, 15);         // focus
            Headless.HeadlessRenderer.TextInput (form, "abcdef");

            Assert.Equal ("abc", control.Text);

            form.Close ();
        }

        [Fact]
        public void MaxLength_Zero_DoesNotLimitText ()
        {
            using var control = new TextBox { MaxLength = 0, Text = "a much longer string than any limit" };
            Assert.Equal ("a much longer string than any limit", control.Text);
        }

        // -------- New behavioral tests ported from upstream TextBoxBaseTests / TextBoxTests --------

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Multiline_Set_GetReturnsExpected (bool value)
        {
            using var control = new TextBox { Multiline = value };
            Assert.Equal (value, control.Multiline);

            // Set same.
            control.Multiline = value;
            Assert.Equal (value, control.Multiline);

            // Set different.
            control.Multiline = !value;
            Assert.Equal (!value, control.Multiline);
        }

        [Fact]
        public void Multiline_DefaultsToFalse ()
        {
            using var control = new TextBox ();
            Assert.False (control.Multiline);
        }

        [Fact]
        public void MultiLine_And_Multiline_AreAliases ()
        {
            using var control = new TextBox ();

            control.MultiLine = true;
            Assert.True (control.Multiline);

            control.Multiline = false;
            Assert.False (control.MultiLine);
        }

        [Fact]
        public void Multiline_True_PreservesNewlinesInText ()
        {
            using var control = new TextBox { Multiline = true, Text = "ab\ncd" };
            Assert.Equal ("ab\ncd", control.Text);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ReadOnly_Set_GetReturnsExpected (bool value)
        {
            using var control = new TextBox { ReadOnly = value };
            Assert.Equal (value, control.ReadOnly);

            // Set same.
            control.ReadOnly = value;
            Assert.Equal (value, control.ReadOnly);

            // Set different.
            control.ReadOnly = !value;
            Assert.Equal (!value, control.ReadOnly);
        }

        [Fact]
        public void ReadOnly_DefaultsToFalse ()
        {
            using var control = new TextBox ();
            Assert.False (control.ReadOnly);
        }

        [Fact]
        public void ReadOnly_True_TextPropertyCanStillBeSet ()
        {
            // ReadOnly blocks interactive editing, but setting Text directly still works (WinForms parity).
            using var control = new TextBox { ReadOnly = true };
            control.Text = "set programmatically";
            Assert.Equal ("set programmatically", control.Text);
        }

        [Theory]
        [InlineData ("", 0)]
        [InlineData ("a", 1)]
        [InlineData ("text", 4)]
        [InlineData ("a\nb", 3)]
        public void TextLength_ReturnsTextLength (string text, int expected)
        {
            using var control = new TextBox { Multiline = true, Text = text };
            Assert.Equal (expected, control.TextLength);
        }

        [Fact]
        public void TextLength_DefaultsToZero ()
        {
            using var control = new TextBox ();
            Assert.Equal (0, control.TextLength);
        }

        [Fact]
        public void Lines_Get_DefaultsToEmpty ()
        {
            using var control = new TextBox ();
            Assert.Empty (control.Lines);
        }

        [Theory]
        [InlineData ("abc", new[] { "abc" })]
        [InlineData ("abc\ndef", new[] { "abc", "def" })]
        [InlineData ("\nabc", new[] { "", "abc" })]
        [InlineData ("abc\n", new[] { "abc", "" })]
        [InlineData ("abc\r\ndef", new[] { "abc", "def" })]
        [InlineData ("abc\rdef", new[] { "abc", "def" })]
        public void Lines_Get_ReturnsExpected (string text, string[] expected)
        {
            using var control = new TextBox { Multiline = true, Text = text };
            Assert.Equal (expected, control.Lines);
        }

        [Fact]
        public void Lines_SetSingle_GetReturnsExpected ()
        {
            using var control = new TextBox { Multiline = true, Lines = new[] { "abc" } };
            Assert.Equal (new[] { "abc" }, control.Lines);
            Assert.Equal ("abc", control.Text);
        }

        [Fact]
        public void Lines_SetMultiple_GetReturnsExpected ()
        {
            using var control = new TextBox { Multiline = true, Lines = new[] { "abc", "def" } };
            Assert.Equal (new[] { "abc", "def" }, control.Lines);
            Assert.Equal ("abc\ndef", control.Text);
        }

        [Fact]
        public void Lines_SetEmpty_GetReturnsEmpty ()
        {
            using var control = new TextBox { Multiline = true, Lines = System.Array.Empty<string> () };
            Assert.Empty (control.Lines);
            Assert.Equal (string.Empty, control.Text);
        }

        [Fact]
        public void AppendText_ToEmpty_SetsText ()
        {
            using var control = new TextBox ();
            control.AppendText ("text");
            Assert.Equal ("text", control.Text);
        }

        [Fact]
        public void AppendText_ToExisting_AppendsAtEnd ()
        {
            using var control = new TextBox { Text = "abc" };
            control.AppendText ("text");
            Assert.Equal ("abctext", control.Text);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        public void AppendText_NullOrEmpty_DoesNotChangeText (string? value)
        {
            using var control = new TextBox { Text = "abc" };
            control.AppendText (value!);
            Assert.Equal ("abc", control.Text);
        }

        [Fact]
        public void Clear_RemovesAllText ()
        {
            using var control = new TextBox { Text = "abc" };
            control.Clear ();
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (0, control.TextLength);
        }

        [Fact]
        public void SelectAll_SelectsEntireText ()
        {
            using var control = new TextBox { Text = "hello" };
            control.SelectAll ();
            Assert.Equal (5, control.SelectionLength);
            Assert.Equal ("hello", control.SelectedText);
        }

        [Fact]
        public void SelectAll_EmptyText_SelectsNothing ()
        {
            using var control = new TextBox ();
            control.SelectAll ();
            Assert.Equal (0, control.SelectionLength);
            Assert.Equal (string.Empty, control.SelectedText);
        }

        [Theory]
        [InlineData ("text", 0, 0, "")]
        [InlineData ("text", 1, 1, "e")]
        [InlineData ("text", 2, 2, "xt")]
        [InlineData ("text", 0, 4, "text")]
        [InlineData ("text", 1, 3, "ext")]
        public void Select_SetsSelection (string text, int start, int length, string expectedSelectedText)
        {
            using var control = new TextBox { Text = text };
            control.Select (start, length);
            Assert.Equal (start, control.SelectionStart);
            Assert.Equal (length, control.SelectionLength);
            Assert.Equal (expectedSelectedText, control.SelectedText);
        }

        [Theory]
        [InlineData ("text", 1, 1)]
        [InlineData ("text", 0, 0)]
        [InlineData ("text", 4, 4)]
        public void SelectionStart_RoundTrips (string text, int value, int expected)
        {
            using var control = new TextBox { Text = text };
            control.SelectionStart = value;
            Assert.Equal (expected, control.SelectionStart);
        }

        [Fact]
        public void SelectionLength_SetWithSelectionStart_UpdatesSelectedText ()
        {
            using var control = new TextBox { Text = "text" };
            control.SelectionStart = 1;
            control.SelectionLength = 2;
            Assert.Equal (1, control.SelectionStart);
            Assert.Equal (2, control.SelectionLength);
            Assert.Equal ("ex", control.SelectedText);
        }

        [Fact]
        public void SelectionLength_Zero_NoSelectedText ()
        {
            using var control = new TextBox { Text = "text" };
            control.Select (1, 0);
            Assert.Equal (0, control.SelectionLength);
            Assert.Equal (string.Empty, control.SelectedText);
        }

        [Fact]
        public void PasswordChar_Set_GetReturnsExpected ()
        {
            using var control = new TextBox { PasswordChar = '*' };
            Assert.Equal ('*', control.PasswordChar);
        }

        [Fact]
        public void PasswordChar_DefaultsToNul ()
        {
            using var control = new TextBox ();
            Assert.Equal ('\0', control.PasswordChar);
        }

        [Fact]
        public void PasswordChar_SetNul_ClearsPasswordCharacter ()
        {
            using var control = new TextBox { PasswordChar = '*' };
            control.PasswordChar = '\0';
            Assert.Equal ('\0', control.PasswordChar);
            Assert.Null (control.PasswordCharacter);
        }

        [Fact]
        public void PasswordChar_DoesNotChangeUnderlyingText ()
        {
            // The masking character only affects display; Text is unaffected.
            using var control = new TextBox { Text = "secret", PasswordChar = '*' };
            Assert.Equal ("secret", control.Text);
        }

        [Fact]
        public void PlaceholderText_DefaultValue ()
        {
            using var control = new TextBox ();
            Assert.Equal (string.Empty, control.PlaceholderText);
        }

        [Fact]
        public void PlaceholderText_Set_GetReturnsExpected ()
        {
            using var control = new TextBox { PlaceholderText = "Enter your name" };
            Assert.Equal ("Enter your name", control.PlaceholderText);
        }

        [Fact]
        public void PlaceholderText_SetNull_CoercedToEmpty ()
        {
            using var control = new TextBox { PlaceholderText = "Text" };
            control.PlaceholderText = null!;
            Assert.Equal (string.Empty, control.PlaceholderText);
        }

        [Fact]
        public void PlaceholderText_And_Placeholder_AreAliases ()
        {
            using var control = new TextBox ();
            control.Placeholder = "abc";
            Assert.Equal ("abc", control.PlaceholderText);

            control.PlaceholderText = "def";
            Assert.Equal ("def", control.Placeholder);
        }

        [Fact]
        public void PlaceholderText_DoesNotAffectText ()
        {
            using var control = new TextBox { PlaceholderText = "hint" };
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (0, control.TextLength);
        }

        [Theory]
        [InlineData (HorizontalAlignment.Left)]
        [InlineData (HorizontalAlignment.Center)]
        [InlineData (HorizontalAlignment.Right)]
        public void TextAlign_Set_GetReturnsExpected (HorizontalAlignment value)
        {
            using var control = new TextBox { TextAlign = value };
            Assert.Equal (value, control.TextAlign);
        }

        [Fact]
        public void TextAlign_DefaultsToLeft ()
        {
            using var control = new TextBox ();
            Assert.Equal (HorizontalAlignment.Left, control.TextAlign);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void WordWrap_Set_GetReturnsExpected (bool value)
        {
            using var control = new TextBox { WordWrap = value };
            Assert.Equal (value, control.WordWrap);

            // Set same.
            control.WordWrap = value;
            Assert.Equal (value, control.WordWrap);
        }

        [Fact]
        public void WordWrap_DefaultsToTrue ()
        {
            using var control = new TextBox ();
            Assert.True (control.WordWrap);
        }
    }
}
