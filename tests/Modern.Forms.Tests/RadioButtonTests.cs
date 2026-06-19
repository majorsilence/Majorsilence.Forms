// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/RadioButtonTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms RadioButtonTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility/ImeMode plumbing, no Moq/MemberData).
    // They pin the default ctor values, the Checked/CheckedChanged semantics, AutoCheck, the
    // OnClick/PerformClick check behavior, ToString, and the mutual-exclusion (one checked
    // RadioButton unchecks its siblings sharing a parent) that is the defining RadioButton trait.
    //
    // A small SubRadioButton subclass mirrors the upstream test helper so the protected
    // OnClick (MouseEventArgs) logic can be exercised deterministically without rendering.
    public class RadioButtonTests
    {
        private sealed class SubRadioButton : RadioButton
        {
            // MF's RadioButton checks on OnClick (MouseEventArgs); expose a parameterless
            // invoker that supplies a synthetic left-click, matching upstream OnClick (EventArgs).
            public void InvokeClick () =>
                OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));
        }

        [Fact]
        public void Ctor_Default ()
        {
            using var control = new RadioButton ();

            Assert.True (control.AutoCheck);
            Assert.False (control.AutoEllipsis);
            Assert.False (control.Checked);
            Assert.Equal (Appearance.Normal, control.Appearance);
            Assert.Equal (FlatStyle.Standard, control.FlatStyle);
            Assert.NotNull (control.FlatAppearance);
            Assert.Equal (ContentAlignment.MiddleLeft, control.CheckAlign);
            Assert.Equal (ContentAlignment.MiddleLeft, control.GlyphAlign);
            Assert.Equal (ContentAlignment.MiddleLeft, control.TextAlign);
            Assert.Null (control.Image);
            Assert.Equal (ContentAlignment.MiddleLeft, control.ImageAlign);
            Assert.Equal (-1, control.ImageIndex);
            Assert.Empty (control.ImageKey);
            Assert.Null (control.ImageList);
            Assert.Empty (control.Text);
            Assert.Equal (new Size (104, 24), control.Size);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Checked_Set_GetReturnsExpected (bool value)
        {
            using var control = new RadioButton { Checked = value };

            Assert.Equal (value, control.Checked);

            // Set same.
            control.Checked = value;
            Assert.Equal (value, control.Checked);

            // Set different.
            control.Checked = !value;
            Assert.Equal (!value, control.Checked);
        }

        [Fact]
        public void Checked_Set_CallsCheckedChanged ()
        {
            using var control = new RadioButton ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.CheckedChanged += handler;

            control.Checked = true;
            Assert.True (control.Checked);
            Assert.Equal (1, callCount);

            // Set same. No change, no event.
            control.Checked = true;
            Assert.Equal (1, callCount);

            control.Checked = false;
            Assert.False (control.Checked);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.CheckedChanged -= handler;
            control.Checked = true;
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoCheck_Set_GetReturnsExpected (bool value)
        {
            using var control = new RadioButton { AutoCheck = value };

            Assert.Equal (value, control.AutoCheck);

            // Set same.
            control.AutoCheck = value;
            Assert.Equal (value, control.AutoCheck);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void AutoEllipsis_Set_GetReturnsExpected (bool value)
        {
            using var control = new RadioButton { AutoEllipsis = value };

            Assert.Equal (value, control.AutoEllipsis);

            // Set same.
            control.AutoEllipsis = value;
            Assert.Equal (value, control.AutoEllipsis);
        }

        [Theory]
        [InlineData (ContentAlignment.TopLeft)]
        [InlineData (ContentAlignment.MiddleCenter)]
        [InlineData (ContentAlignment.BottomRight)]
        public void CheckAlign_Set_GetReturnsExpected (ContentAlignment value)
        {
            using var control = new RadioButton { CheckAlign = value };

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
            using var control = new RadioButton { TextAlign = value };

            Assert.Equal (value, control.TextAlign);

            // Set same.
            control.TextAlign = value;
            Assert.Equal (value, control.TextAlign);
        }

        [Fact]
        public void OnClick_AutoCheck_SetsChecked ()
        {
            using var control = new SubRadioButton { AutoCheck = true };

            Assert.False (control.Checked);

            control.InvokeClick ();
            Assert.True (control.Checked);

            // Clicking an already-checked AutoCheck RadioButton leaves it checked.
            control.InvokeClick ();
            Assert.True (control.Checked);
        }

        [Fact]
        public void OnClick_AutoCheckFalse_DoesNotSetChecked ()
        {
            using var control = new SubRadioButton { AutoCheck = false };

            control.InvokeClick ();
            Assert.False (control.Checked);
        }

        [Fact]
        public void OnClick_RaisesClick ()
        {
            using var control = new SubRadioButton ();
            var callCount = 0;
            EventHandler<MouseEventArgs> handler = (sender, e) => {
                Assert.Same (control, sender);
                callCount++;
            };

            control.Click += handler;
            control.InvokeClick ();
            Assert.Equal (1, callCount);

            control.Click -= handler;
            control.InvokeClick ();
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void PerformClick_AutoCheck_ChecksAndRaisesClick ()
        {
            using var control = new RadioButton { AutoCheck = true };
            var clickCount = 0;
            control.Click += (sender, e) => clickCount++;

            control.PerformClick ();
            Assert.True (control.Checked);
            Assert.Equal (1, clickCount);

            // Click still fires even though already checked.
            control.PerformClick ();
            Assert.True (control.Checked);
            Assert.Equal (2, clickCount);
        }

        [Fact]
        public void PerformClick_AutoCheckFalse_RaisesClickWithoutChecking ()
        {
            using var control = new RadioButton { AutoCheck = false };
            var clickCount = 0;
            control.Click += (sender, e) => clickCount++;

            control.PerformClick ();
            Assert.False (control.Checked);
            Assert.Equal (1, clickCount);
        }

        [Fact]
        public void Checked_Set_UnchecksSiblingsInSameParent ()
        {
            using var parent = new Panel ();
            var radio1 = new RadioButton ();
            var radio2 = new RadioButton ();
            var radio3 = new RadioButton ();
            parent.Controls.Add (radio1);
            parent.Controls.Add (radio2);
            parent.Controls.Add (radio3);

            radio1.Checked = true;
            Assert.True (radio1.Checked);
            Assert.False (radio2.Checked);
            Assert.False (radio3.Checked);

            // Checking another unchecks the previously-checked one (and any others).
            radio2.Checked = true;
            Assert.False (radio1.Checked);
            Assert.True (radio2.Checked);
            Assert.False (radio3.Checked);

            radio3.Checked = true;
            Assert.False (radio1.Checked);
            Assert.False (radio2.Checked);
            Assert.True (radio3.Checked);
        }

        [Fact]
        public void Checked_SetFalse_DoesNotAffectSiblings ()
        {
            using var parent = new Panel ();
            var radio1 = new RadioButton ();
            var radio2 = new RadioButton ();
            parent.Controls.Add (radio1);
            parent.Controls.Add (radio2);

            radio1.Checked = true;

            // Unchecking the checked one does not check any sibling.
            radio1.Checked = false;
            Assert.False (radio1.Checked);
            Assert.False (radio2.Checked);
        }

        [Fact]
        public void Checked_Set_DoesNotAffectRadioButtonsInDifferentParent ()
        {
            using var parent1 = new Panel ();
            using var parent2 = new Panel ();
            var radio1 = new RadioButton ();
            var radio2 = new RadioButton ();
            parent1.Controls.Add (radio1);
            parent2.Controls.Add (radio2);

            radio1.Checked = true;
            radio2.Checked = true;

            // RadioButtons in separate parents are independent groups.
            Assert.True (radio1.Checked);
            Assert.True (radio2.Checked);
        }

        [Fact]
        public void Checked_Set_OnClickGroupBehavior ()
        {
            // Clicking radios in a shared parent enforces single-selection.
            using var parent = new Panel ();
            var radio1 = new SubRadioButton ();
            var radio2 = new SubRadioButton ();
            parent.Controls.Add (radio1);
            parent.Controls.Add (radio2);

            radio1.InvokeClick ();
            Assert.True (radio1.Checked);
            Assert.False (radio2.Checked);

            radio2.InvokeClick ();
            Assert.False (radio1.Checked);
            Assert.True (radio2.Checked);
        }

        [Fact]
        public void ToString_ReturnsExpected ()
        {
            using var control = new RadioButton ();

            Assert.EndsWith (", Checked: False", control.ToString ());

            control.Checked = true;
            Assert.EndsWith (", Checked: True", control.ToString ());
        }
    }
}
