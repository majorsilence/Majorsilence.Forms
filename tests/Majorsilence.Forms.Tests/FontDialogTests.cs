// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/FontDialogTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Majorsilence.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms FontDialogTests, adapted to the Majorsilence.Forms
    // API. Majorsilence.Forms implements FontDialog as a Form subclass (not a Win32 CommonDialog), so there
    // is no Options bitmask, Events list, HookProc, or accessibility/IME plumbing to verify; only the
    // cross-platform behavioral surface (property round-trips, size coercion, Reset) is pinned here.
    // No dialog is ever shown (no ShowDialog / OS interaction).
    public class FontDialogTests
    {
        [Fact]
        public void FontDialog_Ctor_Default ()
        {
            using var dialog = new FontDialog ();

            Assert.True (dialog.AllowScriptChange);
            Assert.True (dialog.AllowSimulations);
            Assert.True (dialog.AllowVectorFonts);
            Assert.True (dialog.AllowVerticalFonts);
            Assert.Equal (Color.Black, dialog.Color);
            Assert.False (dialog.FixedPitchOnly);
            Assert.False (dialog.FontMustExist);
            Assert.Equal (0, dialog.MaxSize);
            Assert.Equal (0, dialog.MinSize);
            Assert.False (dialog.ShowApply);
            Assert.False (dialog.ShowColor);
            Assert.True (dialog.ShowEffects);
            Assert.False (dialog.ShowHelp);
        }

        [Fact]
        public void FontDialog_Ctor_Default_Font ()
        {
            using var dialog = new FontDialog ();

            // The default font is Arial 9pt Regular.
            Assert.NotNull (dialog.Font);
            Assert.Equal ("Arial", dialog.Font.Name);
            Assert.Equal (9f, dialog.Font.Size);
            Assert.Equal (FontStyle.Regular, dialog.Font.Style);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_AllowScriptChange_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { AllowScriptChange = value };
            Assert.Equal (value, dialog.AllowScriptChange);

            // Set same.
            dialog.AllowScriptChange = value;
            Assert.Equal (value, dialog.AllowScriptChange);

            // Set different.
            dialog.AllowScriptChange = !value;
            Assert.Equal (!value, dialog.AllowScriptChange);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_AllowSimulations_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { AllowSimulations = value };
            Assert.Equal (value, dialog.AllowSimulations);

            dialog.AllowSimulations = !value;
            Assert.Equal (!value, dialog.AllowSimulations);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_AllowVectorFonts_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { AllowVectorFonts = value };
            Assert.Equal (value, dialog.AllowVectorFonts);

            dialog.AllowVectorFonts = !value;
            Assert.Equal (!value, dialog.AllowVectorFonts);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_AllowVerticalFonts_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { AllowVerticalFonts = value };
            Assert.Equal (value, dialog.AllowVerticalFonts);

            dialog.AllowVerticalFonts = !value;
            Assert.Equal (!value, dialog.AllowVerticalFonts);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_FixedPitchOnly_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { FixedPitchOnly = value };
            Assert.Equal (value, dialog.FixedPitchOnly);

            dialog.FixedPitchOnly = !value;
            Assert.Equal (!value, dialog.FixedPitchOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_FontMustExist_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { FontMustExist = value };
            Assert.Equal (value, dialog.FontMustExist);

            dialog.FontMustExist = !value;
            Assert.Equal (!value, dialog.FontMustExist);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_ShowApply_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { ShowApply = value };
            Assert.Equal (value, dialog.ShowApply);

            dialog.ShowApply = !value;
            Assert.Equal (!value, dialog.ShowApply);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_ShowColor_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { ShowColor = value };
            Assert.Equal (value, dialog.ShowColor);

            // Set same.
            dialog.ShowColor = value;
            Assert.Equal (value, dialog.ShowColor);

            // Set different.
            dialog.ShowColor = !value;
            Assert.Equal (!value, dialog.ShowColor);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_ShowEffects_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { ShowEffects = value };
            Assert.Equal (value, dialog.ShowEffects);

            dialog.ShowEffects = !value;
            Assert.Equal (!value, dialog.ShowEffects);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FontDialog_ShowHelp_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new FontDialog { ShowHelp = value };
            Assert.Equal (value, dialog.ShowHelp);

            dialog.ShowHelp = !value;
            Assert.Equal (!value, dialog.ShowHelp);
        }

        [Fact]
        public void FontDialog_Color_Set_GetReturnsExpected ()
        {
            using var dialog = new FontDialog { Color = Color.Red };
            Assert.Equal (Color.Red, dialog.Color);

            // Set same.
            dialog.Color = Color.Red;
            Assert.Equal (Color.Red, dialog.Color);

            // Set different.
            dialog.Color = Color.FromArgb (1, 2, 3, 4);
            Assert.Equal (Color.FromArgb (1, 2, 3, 4), dialog.Color);
        }

        [Fact]
        public void FontDialog_Font_Set_GetReturnsExpected ()
        {
            using var font = new Font ("Arial", 8.25f, FontStyle.Bold);
            using var dialog = new FontDialog { Font = font };

            // The setter copies the family/size/style onto the dialog's controls, so the
            // resulting Font reflects the assigned values.
            Assert.Equal ("Arial", dialog.Font.Name);
            Assert.Equal (8.25f, dialog.Font.Size, 3);
            Assert.True (dialog.Font.Bold);
            Assert.False (dialog.Font.Italic);
        }

        [Fact]
        public void FontDialog_Font_SetNull_DoesNotChangeValue ()
        {
            using var font = new Font ("Arial", 8.25f);
            using var dialog = new FontDialog { Font = font };

            // Assigning null is a no-op (the existing selection is preserved).
            dialog.Font = null!;
            Assert.Equal ("Arial", dialog.Font.Name);
            Assert.Equal (8.25f, dialog.Font.Size, 3);
        }

        [Theory]
        [InlineData (8f)]
        [InlineData (12f)]
        [InlineData (24f)]
        public void FontDialog_Font_Set_RoundTripsSize (float size)
        {
            using var font = new Font ("Arial", size);
            using var dialog = new FontDialog { Font = font };

            // Setting a Font preserves the assigned size (MF stores the supplied font; the in-dialog
            // size control's [1, 512] range only constrains the displayed editor value, not the value).
            Assert.Equal (size, dialog.Font.Size, 3);
        }

        [Theory]
        [InlineData (-1, 0)]
        [InlineData (0, 0)]
        [InlineData (1, 1)]
        [InlineData (int.MaxValue, int.MaxValue)]
        public void FontDialog_MinSize_Set_GetReturnsExpected (int value, int expected)
        {
            using var dialog = new FontDialog { MinSize = value };
            Assert.Equal (expected, dialog.MinSize);
            Assert.Equal (0, dialog.MaxSize);

            // Set same.
            dialog.MinSize = value;
            Assert.Equal (expected, dialog.MinSize);
            Assert.Equal (0, dialog.MaxSize);
        }

        [Theory]
        [InlineData (-1, 0, 10)]
        [InlineData (0, 0, 10)]
        [InlineData (1, 1, 10)]
        [InlineData (10, 10, 10)]
        [InlineData (int.MaxValue, int.MaxValue, int.MaxValue)]
        public void FontDialog_MinSize_SetWithMaxSize_GetReturnsExpected (int value, int expected, int expectedMaxSize)
        {
            using var dialog = new FontDialog { MaxSize = 10, MinSize = value };
            Assert.Equal (expected, dialog.MinSize);
            Assert.Equal (expectedMaxSize, dialog.MaxSize);

            // Set same.
            dialog.MinSize = value;
            Assert.Equal (expected, dialog.MinSize);
            Assert.Equal (expectedMaxSize, dialog.MaxSize);
        }

        [Theory]
        [InlineData (-1, 0)]
        [InlineData (0, 0)]
        [InlineData (1, 1)]
        [InlineData (int.MaxValue, int.MaxValue)]
        public void FontDialog_MaxSize_Set_GetReturnsExpected (int value, int expected)
        {
            using var dialog = new FontDialog { MaxSize = value };
            Assert.Equal (0, dialog.MinSize);
            Assert.Equal (expected, dialog.MaxSize);

            // Set same.
            dialog.MaxSize = value;
            Assert.Equal (0, dialog.MinSize);
            Assert.Equal (expected, dialog.MaxSize);
        }

        [Theory]
        [InlineData (-1, 0, 10)]
        [InlineData (0, 0, 10)]
        [InlineData (1, 1, 1)]
        [InlineData (10, 10, 10)]
        [InlineData (int.MaxValue, int.MaxValue, 10)]
        public void FontDialog_MaxSize_SetWithMinSize_GetReturnsExpected (int value, int expected, int expectedMinSize)
        {
            using var dialog = new FontDialog { MinSize = 10, MaxSize = value };
            Assert.Equal (expectedMinSize, dialog.MinSize);
            Assert.Equal (expected, dialog.MaxSize);

            // Set same.
            dialog.MaxSize = value;
            Assert.Equal (expectedMinSize, dialog.MinSize);
            Assert.Equal (expected, dialog.MaxSize);
        }

        [Fact]
        public void FontDialog_Reset_Invoke_Success ()
        {
            using var font = new Font ("Times New Roman", 14f, FontStyle.Italic);
            using var dialog = new FontDialog {
                AllowScriptChange = false,
                AllowSimulations = false,
                AllowVectorFonts = false,
                AllowVerticalFonts = false,
                FixedPitchOnly = true,
                Font = font,
                FontMustExist = true,
                MaxSize = 10,
                MinSize = 5,
                ShowApply = true,
                ShowColor = true,
                ShowEffects = false,
                ShowHelp = true,
                Color = Color.Red
            };

            dialog.Reset ();

            Assert.True (dialog.AllowScriptChange);
            Assert.True (dialog.AllowSimulations);
            Assert.True (dialog.AllowVectorFonts);
            Assert.True (dialog.AllowVerticalFonts);
            Assert.Equal (Color.Black, dialog.Color);
            Assert.False (dialog.FixedPitchOnly);
            Assert.False (dialog.FontMustExist);
            Assert.Equal (0, dialog.MaxSize);
            Assert.Equal (0, dialog.MinSize);
            Assert.False (dialog.ShowApply);
            Assert.False (dialog.ShowColor);
            Assert.True (dialog.ShowEffects);
            Assert.False (dialog.ShowHelp);

            // The font is reset to the Arial 9pt Regular default.
            Assert.Equal ("Arial", dialog.Font.Name);
            Assert.Equal (9f, dialog.Font.Size);
            Assert.Equal (FontStyle.Regular, dialog.Font.Style);
        }
    }
}
