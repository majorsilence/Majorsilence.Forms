// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/FormTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms FormTests, adapted to the Majorsilence.Forms
    // API. They cover in-memory property get/set round-trips, default constructor values, the
    // DialogResult/AcceptButton/CancelButton state, owner relationships, and the enum validation
    // that now routes through SourceGenerated.EnumValidator. No window is shown — every test
    // exercises only state that is available before Show().
    public class FormTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new Form ();

            Assert.Null (control.AcceptButton);
            Assert.Null (control.CancelButton);
            Assert.Null (control.AccessibleDescription);
            Assert.Null (control.AccessibleName);
            Assert.True (control.ControlBox);
            Assert.Empty (control.Controls);
            Assert.Equal (DialogResult.None, control.DialogResult);
            Assert.Equal (FormBorderStyle.Sizable, control.FormBorderStyle);
            Assert.False (control.HelpButton);
            Assert.False (control.IsMdiChild);
            Assert.False (control.IsMdiContainer);
            Assert.False (control.KeyPreview);
            Assert.Null (control.MainMenuStrip);
            Assert.Empty (control.MdiChildren);
            Assert.Null (control.MdiParent);
            Assert.True (control.MaximizeBox);
            Assert.Equal (Size.Empty, control.MaximumSize);
            Assert.True (control.MinimizeBox);
            Assert.Equal (Size.Empty, control.MinimumSize);
            Assert.False (control.Modal);
            Assert.Equal (1, control.Opacity);
            Assert.Empty (control.OwnedForms);
            Assert.Null (control.Owner);
            Assert.True (control.ShowIcon);
            Assert.True (control.ShowInTaskbar);
            Assert.Equal (SizeGripStyle.Auto, control.SizeGripStyle);
            Assert.Equal (FormStartPosition.WindowsDefaultLocation, control.StartPosition);
            Assert.Empty (control.Text);
            Assert.True (control.TopLevel);
            Assert.False (control.TopMost);
            Assert.Equal (Color.Empty, control.TransparencyKey);
            Assert.False (control.Visible);
            Assert.Equal (FormWindowState.Normal, control.WindowState);
            Assert.Equal (AutoValidate.EnablePreventFocusChange, control.AutoValidate);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("text")]
        [InlineData ("Hello World")]
        public void Text_Set_GetReturnsExpected (string value)
        {
            using var control = new Form { Text = value };

            Assert.Equal (value, control.Text);

            // Set same.
            control.Text = value;
            Assert.Equal (value, control.Text);
        }

        [Fact]
        public void AcceptButton_Set_GetReturnsExpected ()
        {
            using var control = new Form ();
            var button = new Button ();

            control.AcceptButton = button;
            Assert.Same (button, control.AcceptButton);

            control.AcceptButton = null;
            Assert.Null (control.AcceptButton);
        }

        [Fact]
        public void CancelButton_Set_GetReturnsExpected ()
        {
            using var control = new Form ();
            var button = new Button ();

            control.CancelButton = button;
            Assert.Same (button, control.CancelButton);

            control.CancelButton = null;
            Assert.Null (control.CancelButton);
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
            // With no modal parent set, assigning DialogResult simply round-trips (it only triggers
            // a Close when the form is being shown as a dialog).
            using var control = new Form { DialogResult = value };

            Assert.Equal (value, control.DialogResult);

            // Set same.
            control.DialogResult = value;
            Assert.Equal (value, control.DialogResult);
        }

        [Theory]
        [InlineData (FormBorderStyle.None)]
        [InlineData (FormBorderStyle.FixedSingle)]
        [InlineData (FormBorderStyle.Fixed3D)]
        [InlineData (FormBorderStyle.FixedDialog)]
        [InlineData (FormBorderStyle.Sizable)]
        [InlineData (FormBorderStyle.FixedToolWindow)]
        [InlineData (FormBorderStyle.SizableToolWindow)]
        public void FormBorderStyle_Set_GetReturnsExpected (FormBorderStyle value)
        {
            using var control = new Form { FormBorderStyle = value };

            Assert.Equal (value, control.FormBorderStyle);

            // Set same.
            control.FormBorderStyle = value;
            Assert.Equal (value, control.FormBorderStyle);
        }

        [Fact]
        public void FormBorderStyle_SetInvalid_ThrowsInvalidEnumArgumentException ()
        {
            using var control = new Form ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.FormBorderStyle = (FormBorderStyle) (-1));
        }

        [Theory]
        [InlineData (FormWindowState.Normal)]
        [InlineData (FormWindowState.Minimized)]
        [InlineData (FormWindowState.Maximized)]
        public void WindowState_Set_GetReturnsExpected (FormWindowState value)
        {
            using var control = new Form { WindowState = value };

            Assert.Equal (value, control.WindowState);

            // Set same.
            control.WindowState = value;
            Assert.Equal (value, control.WindowState);
        }

        [Fact]
        public void WindowState_SetInvalid_ThrowsInvalidEnumArgumentException ()
        {
            using var control = new Form ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.WindowState = (FormWindowState) (-1));
        }

        [Theory]
        [InlineData (FormStartPosition.Manual)]
        [InlineData (FormStartPosition.CenterScreen)]
        [InlineData (FormStartPosition.WindowsDefaultLocation)]
        [InlineData (FormStartPosition.WindowsDefaultBounds)]
        [InlineData (FormStartPosition.CenterParent)]
        public void StartPosition_Set_GetReturnsExpected (FormStartPosition value)
        {
            using var control = new Form { StartPosition = value };

            Assert.Equal (value, control.StartPosition);

            // Set same.
            control.StartPosition = value;
            Assert.Equal (value, control.StartPosition);
        }

        [Fact]
        public void StartPosition_SetInvalid_ThrowsInvalidEnumArgumentException ()
        {
            using var control = new Form ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.StartPosition = (FormStartPosition) (-1));
        }

        [Theory]
        [InlineData (SizeGripStyle.Auto)]
        [InlineData (SizeGripStyle.Show)]
        [InlineData (SizeGripStyle.Hide)]
        public void SizeGripStyle_Set_GetReturnsExpected (SizeGripStyle value)
        {
            using var control = new Form { SizeGripStyle = value };

            Assert.Equal (value, control.SizeGripStyle);

            // Set same.
            control.SizeGripStyle = value;
            Assert.Equal (value, control.SizeGripStyle);
        }

        [Fact]
        public void SizeGripStyle_SetInvalid_ThrowsInvalidEnumArgumentException ()
        {
            using var control = new Form ();
            Assert.Throws<InvalidEnumArgumentException> (() => control.SizeGripStyle = (SizeGripStyle) (-1));
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ControlBox_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { ControlBox = value };

            Assert.Equal (value, control.ControlBox);

            // Set same.
            control.ControlBox = value;
            Assert.Equal (value, control.ControlBox);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void MaximizeBox_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { MaximizeBox = value };

            Assert.Equal (value, control.MaximizeBox);

            // Set same.
            control.MaximizeBox = value;
            Assert.Equal (value, control.MaximizeBox);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void MinimizeBox_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { MinimizeBox = value };

            Assert.Equal (value, control.MinimizeBox);

            // Set same.
            control.MinimizeBox = value;
            Assert.Equal (value, control.MinimizeBox);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowInTaskbar_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { ShowInTaskbar = value };

            Assert.Equal (value, control.ShowInTaskbar);

            // Set same.
            control.ShowInTaskbar = value;
            Assert.Equal (value, control.ShowInTaskbar);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void TopMost_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { TopMost = value };

            Assert.Equal (value, control.TopMost);

            // Set same.
            control.TopMost = value;
            Assert.Equal (value, control.TopMost);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowIcon_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { ShowIcon = value };

            Assert.Equal (value, control.ShowIcon);

            // Set same.
            control.ShowIcon = value;
            Assert.Equal (value, control.ShowIcon);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void HelpButton_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { HelpButton = value };

            Assert.Equal (value, control.HelpButton);

            // Set same.
            control.HelpButton = value;
            Assert.Equal (value, control.HelpButton);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void KeyPreview_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { KeyPreview = value };

            Assert.Equal (value, control.KeyPreview);

            // Set same.
            control.KeyPreview = value;
            Assert.Equal (value, control.KeyPreview);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void TopLevel_Set_GetReturnsExpected (bool value)
        {
            using var control = new Form { TopLevel = value };

            Assert.Equal (value, control.TopLevel);

            // Set same.
            control.TopLevel = value;
            Assert.Equal (value, control.TopLevel);
        }

        [Theory]
        [InlineData (0.0)]
        [InlineData (0.5)]
        [InlineData (1.0)]
        public void Opacity_Set_GetReturnsExpected (double value)
        {
            using var control = new Form { Opacity = value };

            Assert.Equal (value, control.Opacity);

            // Set same.
            control.Opacity = value;
            Assert.Equal (value, control.Opacity);
        }

        [Theory]
        [InlineData (2.0, 1.0)]
        [InlineData (1.1, 1.0)]
        [InlineData (-0.5, 0.0)]
        [InlineData (-1.0, 0.0)]
        public void Opacity_SetOutOfRange_GetReturnsClamped (double value, double expected)
        {
            using var control = new Form { Opacity = value };

            Assert.Equal (expected, control.Opacity);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("name")]
        public void Name_Set_GetReturnsExpected (string value)
        {
            using var control = new Form { Name = value };

            Assert.Equal (value, control.Name);
        }

        [Fact]
        public void Owner_Set_GetReturnsExpected ()
        {
            using var control = new Form ();
            using var owner = new Form ();

            control.Owner = owner;
            Assert.Same (owner, control.Owner);

            control.Owner = null;
            Assert.Null (control.Owner);
        }

        [Fact]
        public void AddOwnedForm_Invoke_AddsToOwnedFormsAndSetsOwner ()
        {
            using var owner = new Form ();
            using var owned = new Form ();

            owner.AddOwnedForm (owned);

            Assert.Same (owner, owned.Owner);
            Assert.Contains (owned, owner.OwnedForms);
            Assert.Single (owner.OwnedForms);

            // Adding the same form again does not duplicate it.
            owner.AddOwnedForm (owned);
            Assert.Single (owner.OwnedForms);
        }

        [Fact]
        public void RemoveOwnedForm_Invoke_RemovesFromOwnedFormsAndClearsOwner ()
        {
            using var owner = new Form ();
            using var owned = new Form ();

            owner.AddOwnedForm (owned);
            owner.RemoveOwnedForm (owned);

            Assert.Empty (owner.OwnedForms);
            Assert.Null (owned.Owner);
        }

        [Fact]
        public void MinimumSize_Set_GetReturnsExpected ()
        {
            using var control = new Form { MinimumSize = new Size (200, 150) };

            Assert.Equal (new Size (200, 150), control.MinimumSize);
        }

        [Fact]
        public void MaximumSize_Set_GetReturnsExpected ()
        {
            using var control = new Form { MaximumSize = new Size (800, 600) };

            Assert.Equal (new Size (800, 600), control.MaximumSize);
        }

        [Fact]
        public void MinimumSize_SetLargerThanMaximum_GrowsMaximumToMatch ()
        {
            using var control = new Form { MaximumSize = new Size (400, 300) };

            control.MinimumSize = new Size (500, 400);

            Assert.Equal (new Size (500, 400), control.MinimumSize);
            Assert.Equal (new Size (500, 400), control.MaximumSize);
        }

        [Fact]
        public void MaximumSize_SetSmallerThanMinimum_ShrinksMinimumToMatch ()
        {
            using var control = new Form { MinimumSize = new Size (500, 400) };

            control.MaximumSize = new Size (300, 200);

            Assert.Equal (new Size (300, 200), control.MinimumSize);
            Assert.Equal (new Size (300, 200), control.MaximumSize);
        }

        [Fact]
        public void MinimumSizeChanged_Invoke_CallsHandler ()
        {
            using var control = new Form ();
            var callCount = 0;
            control.MinimumSizeChanged += (sender, e) => callCount++;

            control.MinimumSize = new Size (100, 100);
            Assert.Equal (1, callCount);

            // Set same: no additional event.
            control.MinimumSize = new Size (100, 100);
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void MaximumSizeChanged_Invoke_CallsHandler ()
        {
            using var control = new Form ();
            var callCount = 0;
            control.MaximumSizeChanged += (sender, e) => callCount++;

            control.MaximumSize = new Size (900, 700);
            Assert.Equal (1, callCount);

            // Set same: no additional event.
            control.MaximumSize = new Size (900, 700);
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void CenterToScreen_Invoke_SetsStartPosition ()
        {
            using var control = new Form ();

            control.CenterToScreen ();

            Assert.Equal (FormStartPosition.CenterScreen, control.StartPosition);
        }

        [Fact]
        public void CenterToScreen_WhenManual_DoesNotChangeStartPosition ()
        {
            using var control = new Form { StartPosition = FormStartPosition.Manual };

            control.CenterToScreen ();

            Assert.Equal (FormStartPosition.Manual, control.StartPosition);
        }

        [Fact]
        public void ClientSize_TracksSize ()
        {
            using var control = new Form { Size = new Size (640, 480) };

            Assert.Equal (control.Size, control.ClientSize);
        }

        [Fact]
        public void IsMdiContainer_Set_GetReturnsExpected ()
        {
            using var control = new Form { IsMdiContainer = true };

            Assert.True (control.IsMdiContainer);

            control.IsMdiContainer = false;
            Assert.False (control.IsMdiContainer);
        }

        [Fact]
        public void TransparencyKey_Set_GetReturnsExpected ()
        {
            using var control = new Form { TransparencyKey = Color.Red };

            Assert.Equal (Color.Red, control.TransparencyKey);
        }
    }
}
