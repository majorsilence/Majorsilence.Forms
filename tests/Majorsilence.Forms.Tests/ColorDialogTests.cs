// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ColorDialogTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ColorDialogTests, adapted to the
    // Majorsilence.Forms API. Majorsilence.Forms implements ColorDialog as a Form subclass (not a
    // CommonDialog with Win32 Options/Instance/Events plumbing), so only the cross-platform
    // behavioral surface is pinned: default values, the Color empty->black rule, the bool
    // property round-trips, the always-16-slot cloning CustomColors semantics, and Reset.
    // No dialog is ever shown (no ShowDialog / OS interaction).
    public class ColorDialogTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var dialog = new ColorDialog ();

            Assert.True (dialog.AllowFullOpen);
            Assert.False (dialog.AnyColor);
            Assert.Equal (Color.Black, dialog.Color);
            Assert.Equal (Enumerable.Repeat (0x00FFFFFF, 16).ToArray (), dialog.CustomColors);
            Assert.False (dialog.FullOpen);
            Assert.False (dialog.ShowHelp);
            Assert.False (dialog.SolidColorOnly);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AllowFullOpen_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new ColorDialog { AllowFullOpen = value };
            Assert.Equal (value, dialog.AllowFullOpen);

            // Set same.
            dialog.AllowFullOpen = value;
            Assert.Equal (value, dialog.AllowFullOpen);

            // Set different.
            dialog.AllowFullOpen = !value;
            Assert.Equal (!value, dialog.AllowFullOpen);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AnyColor_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new ColorDialog { AnyColor = value };
            Assert.Equal (value, dialog.AnyColor);

            dialog.AnyColor = value;
            Assert.Equal (value, dialog.AnyColor);

            dialog.AnyColor = !value;
            Assert.Equal (!value, dialog.AnyColor);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void FullOpen_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new ColorDialog { FullOpen = value };
            Assert.Equal (value, dialog.FullOpen);

            dialog.FullOpen = value;
            Assert.Equal (value, dialog.FullOpen);

            dialog.FullOpen = !value;
            Assert.Equal (!value, dialog.FullOpen);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowHelp_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new ColorDialog { ShowHelp = value };
            Assert.Equal (value, dialog.ShowHelp);

            dialog.ShowHelp = value;
            Assert.Equal (value, dialog.ShowHelp);

            dialog.ShowHelp = !value;
            Assert.Equal (!value, dialog.ShowHelp);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void SolidColorOnly_Set_GetReturnsExpected (bool value)
        {
            using var dialog = new ColorDialog { SolidColorOnly = value };
            Assert.Equal (value, dialog.SolidColorOnly);

            dialog.SolidColorOnly = value;
            Assert.Equal (value, dialog.SolidColorOnly);

            dialog.SolidColorOnly = !value;
            Assert.Equal (!value, dialog.SolidColorOnly);
        }

        public static TheoryData<Color, Color> Color_Set_TestData () => new () {
            { Color.Empty, Color.Black },
            { Color.Red, Color.Red },
        };

        [Theory]
        [MemberData (nameof (Color_Set_TestData))]
        public void Color_Set_GetReturnsExpected (Color value, Color expected)
        {
            using var dialog = new ColorDialog { Color = value };
            Assert.Equal (expected, dialog.Color);

            // Set same.
            dialog.Color = value;
            Assert.Equal (expected, dialog.Color);
        }

        [Theory]
        [MemberData (nameof (Color_Set_TestData))]
        public void Color_SetWithCustomOldValue_GetReturnsExpected (Color value, Color expected)
        {
            using var dialog = new ColorDialog { Color = Color.Blue };

            dialog.Color = value;
            Assert.Equal (expected, dialog.Color);

            // Set same.
            dialog.Color = value;
            Assert.Equal (expected, dialog.Color);
        }

        [Fact]
        public void CustomColors_Get_ReturnsClone ()
        {
            using var dialog = new ColorDialog ();
            var value1 = dialog.CustomColors;
            var value2 = dialog.CustomColors;

            Assert.NotSame (value1, value2);
            Assert.Equal (value1, value2);

            value1[0] = 1;
            Assert.Equal (0x00FFFFFF, value2[0]);
            Assert.Equal (0x00FFFFFF, dialog.CustomColors[0]);
        }

        public static TheoryData<int[]?, int[]> CustomColors_Set_TestData () => new () {
            { null, Enumerable.Repeat (0x00FFFFFF, 16).ToArray () },
            { System.Array.Empty<int> (), Enumerable.Repeat (0x00FFFFFF, 16).ToArray () },
            { new[] { 1, 2, 3 }, new[] { 1, 2, 3 }.Concat (Enumerable.Repeat (0x00FFFFFF, 13)).ToArray () },
            { Enumerable.Repeat (0, 16).ToArray (), Enumerable.Repeat (0, 16).ToArray () },
            { Enumerable.Repeat (unchecked ((int)0xFFFFFFFF), 16).ToArray (), Enumerable.Repeat (unchecked ((int)0xFFFFFFFF), 16).ToArray () },
            { Enumerable.Repeat (1, 16).ToArray (), Enumerable.Repeat (1, 16).ToArray () },
            { Enumerable.Repeat (1, 20).ToArray (), Enumerable.Repeat (1, 16).ToArray () },
        };

        [Theory]
        [MemberData (nameof (CustomColors_Set_TestData))]
        public void CustomColors_Set_GetReturnsExpected (int[]? value, int[] expected)
        {
            using var dialog = new ColorDialog { CustomColors = value! };
            Assert.Equal (expected, dialog.CustomColors);
            Assert.NotSame (value, dialog.CustomColors);
            Assert.NotSame (dialog.CustomColors, dialog.CustomColors);

            // Set same.
            dialog.CustomColors = value!;
            Assert.Equal (expected, dialog.CustomColors);
            Assert.NotSame (value, dialog.CustomColors);
            Assert.NotSame (dialog.CustomColors, dialog.CustomColors);
        }

        [Theory]
        [MemberData (nameof (CustomColors_Set_TestData))]
        public void CustomColors_SetWithCustomOldValue_GetReturnsExpected (int[]? value, int[] expected)
        {
            using var dialog = new ColorDialog { CustomColors = new int[1] };

            dialog.CustomColors = value!;
            Assert.Equal (expected, dialog.CustomColors);
            Assert.NotSame (value, dialog.CustomColors);
            Assert.NotSame (dialog.CustomColors, dialog.CustomColors);

            // Set same.
            dialog.CustomColors = value!;
            Assert.Equal (expected, dialog.CustomColors);
            Assert.NotSame (value, dialog.CustomColors);
            Assert.NotSame (dialog.CustomColors, dialog.CustomColors);
        }

        [Fact]
        public void CustomColors_Set_GetReturnsClone ()
        {
            var value = Enumerable.Repeat (1, 16).ToArray ();
            using var dialog = new ColorDialog { CustomColors = value };

            value[0] = 0;
            Assert.Equal (Enumerable.Repeat (1, 16).ToArray (), dialog.CustomColors);
        }

        [Fact]
        public void Reset_Invoke_Success ()
        {
            using var dialog = new ColorDialog {
                AllowFullOpen = false,
                AnyColor = true,
                Color = Color.Red,
                CustomColors = new[] { 1, 2, 3 },
                FullOpen = true,
                ShowHelp = true,
                SolidColorOnly = true,
            };

            dialog.Reset ();

            Assert.True (dialog.AllowFullOpen);
            Assert.False (dialog.AnyColor);
            Assert.Equal (Color.Black, dialog.Color);
            Assert.Equal (Enumerable.Repeat (0x00FFFFFF, 16).ToArray (), dialog.CustomColors);
            Assert.False (dialog.FullOpen);
            Assert.False (dialog.ShowHelp);
            Assert.False (dialog.SolidColorOnly);
        }
    }
}
