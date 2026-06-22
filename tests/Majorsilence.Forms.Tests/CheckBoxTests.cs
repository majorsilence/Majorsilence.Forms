// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/CheckBoxTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms CheckBoxTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing). They pin the
    // Checked<->CheckState relationship, ThreeState/AutoCheck click cycling, and the
    // CheckedChanged/CheckStateChanged event semantics.
    //
    // A small SubCheckBox subclass mirrors the upstream test helper so the protected
    // OnClick toggle logic can be exercised deterministically without rendering.
    public class CheckBoxTests
    {
        private sealed class SubCheckBox : CheckBox
        {
            // MF's CheckBox toggles on OnClick (MouseEventArgs); expose a parameterless
            // invoker that supplies a synthetic left-click, matching upstream OnClick (EventArgs).
            public void InvokeClick () =>
                OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));
        }

        [Fact]
        public void Ctor_Default ()
        {
            using var control = new CheckBox ();

            Assert.True (control.AutoCheck);
            Assert.False (control.AutoEllipsis);
            Assert.Equal (ContentAlignment.MiddleLeft, control.CheckAlign);
            Assert.Equal (ContentAlignment.MiddleLeft, control.GlyphAlign);
            Assert.False (control.Checked);
            Assert.Equal (CheckState.Unchecked, control.CheckState);
            Assert.Equal (ContentAlignment.MiddleLeft, control.TextAlign);
            Assert.False (control.ThreeState);
            Assert.Empty (control.Text);
            Assert.Equal (new Size (104, 24), control.Size);
        }

        [Theory]
        [InlineData (true, CheckState.Checked)]
        [InlineData (false, CheckState.Unchecked)]
        public void Checked_Set_UpdatesCheckState (bool value, CheckState expected)
        {
            using var control = new CheckBox { Checked = value };

            Assert.Equal (value, control.Checked);
            Assert.Equal (expected, control.CheckState);
        }

        [Theory]
        [InlineData (CheckState.Unchecked, false)]
        [InlineData (CheckState.Checked, true)]
        [InlineData (CheckState.Indeterminate, true)]
        public void CheckState_Set_UpdatesChecked (CheckState value, bool expectedChecked)
        {
            using var control = new CheckBox { CheckState = value };

            Assert.Equal (value, control.CheckState);
            Assert.Equal (expectedChecked, control.Checked);

            // Set same.
            control.CheckState = value;
            Assert.Equal (value, control.CheckState);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoCheck_Set_GetReturnsExpected (bool value)
        {
            using var control = new CheckBox { AutoCheck = value };

            Assert.Equal (value, control.AutoCheck);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ThreeState_Set_GetReturnsExpected (bool value)
        {
            using var control = new CheckBox { ThreeState = value };

            Assert.Equal (value, control.ThreeState);
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void CheckAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new CheckBox { CheckAlign = value };

            Assert.Equal (value, control.CheckAlign);
            Assert.Equal (value, control.GlyphAlign);

            // Set same.
            control.CheckAlign = value;
            Assert.Equal (value, control.CheckAlign);
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void TextAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new CheckBox { TextAlign = value };

            Assert.Equal (value, control.TextAlign);

            // Set same.
            control.TextAlign = value;
            Assert.Equal (value, control.TextAlign);
        }

        [Theory]
        // threeState, startState -> expectedChecked, expectedCheckState
        [InlineData (true, CheckState.Checked, true, CheckState.Indeterminate)]
        [InlineData (true, CheckState.Unchecked, true, CheckState.Checked)]
        [InlineData (true, CheckState.Indeterminate, false, CheckState.Unchecked)]
        [InlineData (false, CheckState.Checked, false, CheckState.Unchecked)]
        [InlineData (false, CheckState.Unchecked, true, CheckState.Checked)]
        [InlineData (false, CheckState.Indeterminate, false, CheckState.Unchecked)]
        public void OnClick_AutoCheck_SetsCorrectCheckState (bool threeState, CheckState checkState, bool expectedChecked, CheckState expectedCheckState)
        {
            using var box = new SubCheckBox {
                AutoCheck = true,
                ThreeState = threeState,
                CheckState = checkState
            };

            box.InvokeClick ();

            Assert.Equal (expectedChecked, box.Checked);
            Assert.Equal (expectedCheckState, box.CheckState);
        }

        [Theory]
        // threeState, state -> expectedChecked (state must be unchanged after click)
        [InlineData (true, CheckState.Checked, true)]
        [InlineData (true, CheckState.Unchecked, false)]
        [InlineData (true, CheckState.Indeterminate, true)]
        [InlineData (false, CheckState.Checked, true)]
        [InlineData (false, CheckState.Unchecked, false)]
        [InlineData (false, CheckState.Indeterminate, true)]
        public void OnClick_AutoCheckFalse_DoesNotChangeCheckState (bool threeState, CheckState checkState, bool expectedChecked)
        {
            using var box = new SubCheckBox {
                AutoCheck = false,
                ThreeState = threeState,
                CheckState = checkState
            };

            box.InvokeClick ();

            Assert.Equal (expectedChecked, box.Checked);
            Assert.Equal (checkState, box.CheckState);
        }

        [Fact]
        public void CheckState_Set_CallsCheckStateChanged ()
        {
            using var control = new CheckBox ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.CheckStateChanged += handler;
            control.CheckState = CheckState.Checked;
            Assert.Equal (1, callCount);

            // Set same. No change, no event.
            control.CheckState = CheckState.Checked;
            Assert.Equal (1, callCount);

            // Remove handler.
            control.CheckStateChanged -= handler;
            control.CheckState = CheckState.Unchecked;
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void CheckState_Set_CallsCheckedChanged ()
        {
            using var control = new CheckBox ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.CheckedChanged += handler;
            control.CheckState = CheckState.Checked;
            Assert.Equal (1, callCount);

            // Remove handler.
            control.CheckedChanged -= handler;
            control.CheckState = CheckState.Unchecked;
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void Checked_Set_CallsCheckedChanged ()
        {
            using var control = new CheckBox ();
            var callCount = 0;
            control.CheckedChanged += (sender, e) => callCount++;

            control.Checked = true;
            Assert.Equal (1, callCount);

            // Set same. No change, no event.
            control.Checked = true;
            Assert.Equal (1, callCount);

            control.Checked = false;
            Assert.Equal (2, callCount);
        }

        [Fact]
        public void CheckState_CheckedToIndeterminate_DoesNotRaiseCheckedChanged ()
        {
            // Both Checked and Indeterminate report Checked == true, so toggling
            // between them must not raise CheckedChanged, but must raise CheckStateChanged.
            using var control = new CheckBox { CheckState = CheckState.Checked };

            var checkedChangedCount = 0;
            var checkStateChangedCount = 0;
            control.CheckedChanged += (sender, e) => checkedChangedCount++;
            control.CheckStateChanged += (sender, e) => checkStateChangedCount++;

            control.CheckState = CheckState.Indeterminate;

            Assert.True (control.Checked);
            Assert.Equal (0, checkedChangedCount);
            Assert.Equal (1, checkStateChangedCount);
        }

        [Fact]
        public void ToString_ReturnsExpected ()
        {
            using var control = new CheckBox ();

            Assert.EndsWith (", CheckState: 0", control.ToString ());

            control.CheckState = CheckState.Indeterminate;
            Assert.EndsWith (", CheckState: 2", control.ToString ());
        }
    }
}
