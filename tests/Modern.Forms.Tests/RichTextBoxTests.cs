// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/RichTextBoxTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms RichTextBoxTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility plumbing, and no real RTF rendering
    // engine). Modern.Forms' RichTextBox is a thin subclass of TextBox: most rich-text members are
    // stubs. These tests pin only the members that have real, deterministic behavior — default
    // ctor values, Text round-tripping, Multiline/ReadOnly, the SelectionStart/SelectionLength/
    // SelectedText interaction, plain-text Rtf stripping, the line/char index helpers, and Find.
    public class RichTextBoxTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new RichTextBox ();

            // WinForms RichTextBox defaults that Modern.Forms matches.
            Assert.True (control.Multiline);
            Assert.False (control.ReadOnly);
            Assert.True (control.DetectUrls);
            Assert.True (control.WordWrap);
            Assert.True (control.HideSelection);
            Assert.False (control.EnableAutoDragDrop);
            Assert.False (control.Modified);
            Assert.Equal (0, control.BulletIndent);
            Assert.Equal (1.0f, control.ZoomFactor);
            Assert.Equal (RichTextBoxScrollBars.Both, control.ScrollBars);
            Assert.Equal (Color.Empty, control.SelectionBackColor);
            Assert.Equal (Color.Empty, control.SelectionColor);
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (0, control.TextLength);
            Assert.Equal (string.Empty, control.SelectedText);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("text")]
        [InlineData ("a longer string of text")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new RichTextBox { Text = value };

            Assert.Equal (value, control.Text);
            Assert.Equal (value.Length, control.TextLength);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void Clear_Invoke_EmptiesText ()
        {
            using var control = new RichTextBox { Text = "text" };

            control.Clear ();
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (0, control.TextLength);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Multiline_Set_GetReturnsExpected (bool value)
        {
            using var control = new RichTextBox { Multiline = value };

            Assert.Equal (value, control.Multiline);

            // Set same.
            control.Multiline = value;
            Assert.Equal (value, control.Multiline);

            // Set different.
            control.Multiline = !value;
            Assert.Equal (!value, control.Multiline);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ReadOnly_Set_GetReturnsExpected (bool value)
        {
            using var control = new RichTextBox { ReadOnly = value };

            Assert.Equal (value, control.ReadOnly);

            // Set same.
            control.ReadOnly = value;
            Assert.Equal (value, control.ReadOnly);

            // Set different.
            control.ReadOnly = !value;
            Assert.Equal (!value, control.ReadOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void DetectUrls_Set_GetReturnsExpected (bool value)
        {
            using var control = new RichTextBox { DetectUrls = value };

            Assert.Equal (value, control.DetectUrls);

            control.DetectUrls = value;
            Assert.Equal (value, control.DetectUrls);

            control.DetectUrls = !value;
            Assert.Equal (!value, control.DetectUrls);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (10)]
        public void BulletIndent_Set_GetReturnsExpected (int value)
        {
            using var control = new RichTextBox { BulletIndent = value };

            Assert.Equal (value, control.BulletIndent);

            control.BulletIndent = value;
            Assert.Equal (value, control.BulletIndent);
        }

        [Fact]
        public void Lines_GetSet_RoundTrips ()
        {
            using var control = new RichTextBox { Text = "line1\nline2\nline3" };

            Assert.Equal (new[] { "line1", "line2", "line3" }, control.Lines);

            control.Lines = new[] { "a", "b" };
            Assert.Equal ("a\nb", control.Text);
        }

        // Modern.Forms strips RTF markup down to plain text (it has no RTF renderer). Verify the
        // plain-text result of a simple, single-level RTF payload — matching the upstream
        // Rtf_Set_GetTextExpected "{\rtf1Hello World}" => "Hello World" case.
        [Fact]
        public void Rtf_Set_GetStripsToPlainText ()
        {
            using var control = new RichTextBox { Rtf = "{\\rtf1Hello World}" };

            Assert.Equal ("Hello World", control.Text);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        public void Rtf_SetNullOrEmpty_EmptiesText (string value)
        {
            using var control = new RichTextBox { Rtf = "{\\rtf1Hello World}" };
            Assert.Equal ("Hello World", control.Text);

            control.Rtf = value;
            Assert.Equal (string.Empty, control.Text);
            Assert.Equal (string.Empty, control.Rtf);
        }

        // With a selection start established first, SelectionLength and SelectedText round-trip
        // deterministically. Mirrors upstream SelectionLength_SetWithSelectionStart_Success.
        [Theory]
        [InlineData ("text", 0, 0, "")]
        [InlineData ("text", 1, 1, "e")]
        [InlineData ("text", 2, 2, "ex")]
        [InlineData ("text", 3, 3, "ext")]
        public void SelectionLength_SetWithSelectionStart_GetReturnsExpected (string text, int value, int expectedLength, string expectedSelectedText)
        {
            using var control = new RichTextBox {
                Text = text,
                SelectionStart = 1,
                SelectionLength = value
            };

            Assert.Equal (1, control.SelectionStart);
            Assert.Equal (expectedLength, control.SelectionLength);
            Assert.Equal (expectedSelectedText, control.SelectedText);
        }

        [Theory]
        [InlineData ("text", 0, 0, "")]
        [InlineData ("text", 1, 1, "t")]
        [InlineData ("text", 4, 4, "text")]
        public void SelectionLength_SetWithSelectionStartZero_GetReturnsExpected (string text, int value, int expectedLength, string expectedSelectedText)
        {
            using var control = new RichTextBox {
                Text = text,
                SelectionStart = 0,
                SelectionLength = value
            };

            Assert.Equal (0, control.SelectionStart);
            Assert.Equal (expectedLength, control.SelectionLength);
            Assert.Equal (expectedSelectedText, control.SelectedText);
        }

        [Fact]
        public void SelectAll_Invoke_SelectsEntireText ()
        {
            using var control = new RichTextBox { Text = "text" };

            control.SelectAll ();

            Assert.Equal (0, control.SelectionStart);
            Assert.Equal (4, control.SelectionLength);
            Assert.Equal ("text", control.SelectedText);
        }

        [Fact]
        public void AppendText_Invoke_AppendsAndMovesSelection ()
        {
            using var control = new RichTextBox { Text = "ab" };

            control.AppendText ("cd");

            Assert.Equal ("abcd", control.Text);
            Assert.Equal (4, control.SelectionStart);
        }

        [Theory]
        [InlineData ("hello world", "world", 6)]
        [InlineData ("hello world", "hello", 0)]
        [InlineData ("hello world", "xyz", -1)]
        public void Find_Invoke_ReturnsExpectedIndex (string text, string search, int expected)
        {
            using var control = new RichTextBox { Text = text };

            Assert.Equal (expected, control.Find (search));
        }

        [Theory]
        [InlineData ("aXbXc", "X", 0, 1)]
        [InlineData ("aXbXc", "X", 2, 3)]
        [InlineData ("aXbXc", "X", 4, -1)]
        public void Find_WithStart_ReturnsExpectedIndex (string text, string search, int start, int expected)
        {
            using var control = new RichTextBox { Text = text };

            Assert.Equal (expected, control.Find (search, start));
        }

        [Fact]
        public void Find_WithRange_HonorsEndBound ()
        {
            using var control = new RichTextBox { Text = "abXcdXef" };

            // "X" appears at index 2 and 5; restricting the range to [0,4) only finds the first.
            Assert.Equal (2, control.Find ("X", 0, 4));
            // Restricting to [0,2) excludes both.
            Assert.Equal (-1, control.Find ("X", 0, 2));
        }

        [Theory]
        [InlineData ("line0\nline1\nline2", 0, 0)]
        [InlineData ("line0\nline1\nline2", 3, 0)]
        [InlineData ("line0\nline1\nline2", 6, 1)]
        [InlineData ("line0\nline1\nline2", 12, 2)]
        public void GetLineFromCharIndex_Invoke_ReturnsExpected (string text, int index, int expected)
        {
            using var control = new RichTextBox { Text = text };

            Assert.Equal (expected, control.GetLineFromCharIndex (index));
        }

        [Theory]
        [InlineData ("line0\nline1\nline2", 0, 0)]
        [InlineData ("line0\nline1\nline2", 1, 6)]
        [InlineData ("line0\nline1\nline2", 2, 12)]
        public void GetFirstCharIndexFromLine_Invoke_ReturnsExpected (string text, int lineNumber, int expected)
        {
            using var control = new RichTextBox { Text = text };

            Assert.Equal (expected, control.GetFirstCharIndexFromLine (lineNumber));
        }
    }
}
