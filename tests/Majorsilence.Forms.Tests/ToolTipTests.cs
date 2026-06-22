// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ToolTipTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ToolTipTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility/Site/Moq plumbing). They pin the
    // default property values, the bool/int/string property round-trips, the AutomaticDelay
    // cascade and negative-value validation, and the SetToolTip/GetToolTip/RemoveAll semantics.
    public class ToolTipTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var toolTip = new ToolTip ();

            Assert.True (toolTip.Active);
            Assert.Equal (500, toolTip.AutomaticDelay);
            Assert.Equal (5000, toolTip.AutoPopDelay);
            Assert.Equal (500, toolTip.InitialDelay);
            Assert.Equal (100, toolTip.ReshowDelay);
            Assert.False (toolTip.IsBalloon);
            Assert.False (toolTip.ShowAlways);
            Assert.False (toolTip.StripAmpersands);
            Assert.Equal (ToolTipIcon.None, toolTip.ToolTipIcon);
            Assert.Equal (string.Empty, toolTip.ToolTipTitle);
            Assert.True (toolTip.UseAnimation);
            Assert.True (toolTip.UseFading);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Active_Set_GetReturnsExpected (bool value)
        {
            using var toolTip = new ToolTip { Active = value };
            Assert.Equal (value, toolTip.Active);

            // Set same.
            toolTip.Active = value;
            Assert.Equal (value, toolTip.Active);

            // Set different.
            toolTip.Active = !value;
            Assert.Equal (!value, toolTip.Active);
        }

        [Theory]
        [InlineData (0, 0, 0)]
        [InlineData (1, 10, 0)]
        [InlineData (2, 20, 0)]
        [InlineData (100, 1000, 20)]
        [InlineData (500, 5000, 100)]
        [InlineData (5000, 50000, 1000)]
        public void AutomaticDelay_Set_GetReturnsExpected (int value, int expectedAutoPopDelay, int expectedReshowDelay)
        {
            using var toolTip = new ToolTip {
                InitialDelay = 80,
                AutoPopDelay = 70,
                ReshowDelay = 60,
                AutomaticDelay = value
            };

            Assert.Equal (value, toolTip.AutomaticDelay);
            Assert.Equal (expectedAutoPopDelay, toolTip.AutoPopDelay);
            Assert.Equal (value, toolTip.InitialDelay);
            Assert.Equal (expectedReshowDelay, toolTip.ReshowDelay);

            // Set same.
            toolTip.InitialDelay = 80;
            toolTip.AutoPopDelay = 70;
            toolTip.ReshowDelay = 60;
            toolTip.AutomaticDelay = value;
            Assert.Equal (value, toolTip.AutomaticDelay);
            Assert.Equal (expectedAutoPopDelay, toolTip.AutoPopDelay);
            Assert.Equal (value, toolTip.InitialDelay);
            Assert.Equal (expectedReshowDelay, toolTip.ReshowDelay);
        }

        [Fact]
        public void AutomaticDelay_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var toolTip = new ToolTip ();
            Assert.Throws<ArgumentOutOfRangeException> (() => toolTip.AutomaticDelay = -1);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (2)]
        [InlineData (100)]
        [InlineData (500)]
        [InlineData (5000)]
        public void AutoPopDelay_Set_GetReturnsExpected (int value)
        {
            using var toolTip = new ToolTip { AutoPopDelay = value };

            Assert.Equal (500, toolTip.AutomaticDelay);
            Assert.Equal (value, toolTip.AutoPopDelay);
            Assert.Equal (500, toolTip.InitialDelay);
            Assert.Equal (100, toolTip.ReshowDelay);

            // Set same.
            toolTip.AutoPopDelay = value;
            Assert.Equal (value, toolTip.AutoPopDelay);
        }

        [Fact]
        public void AutoPopDelay_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var toolTip = new ToolTip ();
            Assert.Throws<ArgumentOutOfRangeException> (() => toolTip.AutoPopDelay = -1);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (2)]
        [InlineData (100)]
        [InlineData (500)]
        [InlineData (5000)]
        public void InitialDelay_Set_GetReturnsExpected (int value)
        {
            using var toolTip = new ToolTip { InitialDelay = value };

            Assert.Equal (500, toolTip.AutomaticDelay);
            Assert.Equal (5000, toolTip.AutoPopDelay);
            Assert.Equal (value, toolTip.InitialDelay);
            Assert.Equal (100, toolTip.ReshowDelay);

            // Set same.
            toolTip.InitialDelay = value;
            Assert.Equal (value, toolTip.InitialDelay);
        }

        [Fact]
        public void InitialDelay_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var toolTip = new ToolTip ();
            Assert.Throws<ArgumentOutOfRangeException> (() => toolTip.InitialDelay = -1);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (2)]
        [InlineData (100)]
        [InlineData (500)]
        [InlineData (5000)]
        public void ReshowDelay_Set_GetReturnsExpected (int value)
        {
            using var toolTip = new ToolTip { ReshowDelay = value };

            Assert.Equal (500, toolTip.AutomaticDelay);
            Assert.Equal (5000, toolTip.AutoPopDelay);
            Assert.Equal (500, toolTip.InitialDelay);
            Assert.Equal (value, toolTip.ReshowDelay);

            // Set same.
            toolTip.ReshowDelay = value;
            Assert.Equal (value, toolTip.ReshowDelay);
        }

        [Fact]
        public void ReshowDelay_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var toolTip = new ToolTip ();
            Assert.Throws<ArgumentOutOfRangeException> (() => toolTip.ReshowDelay = -1);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void IsBalloon_Set_GetReturnsExpected (bool value)
        {
            using var toolTip = new ToolTip { IsBalloon = value };
            Assert.Equal (value, toolTip.IsBalloon);

            // Set same.
            toolTip.IsBalloon = value;
            Assert.Equal (value, toolTip.IsBalloon);

            // Set different.
            toolTip.IsBalloon = !value;
            Assert.Equal (!value, toolTip.IsBalloon);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowAlways_Set_GetReturnsExpected (bool value)
        {
            using var toolTip = new ToolTip { ShowAlways = value };
            Assert.Equal (value, toolTip.ShowAlways);

            // Set same.
            toolTip.ShowAlways = value;
            Assert.Equal (value, toolTip.ShowAlways);

            // Set different.
            toolTip.ShowAlways = !value;
            Assert.Equal (!value, toolTip.ShowAlways);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void StripAmpersands_Set_GetReturnsExpected (bool value)
        {
            using var toolTip = new ToolTip { StripAmpersands = value };
            Assert.Equal (value, toolTip.StripAmpersands);

            // Set same.
            toolTip.StripAmpersands = value;
            Assert.Equal (value, toolTip.StripAmpersands);

            // Set different.
            toolTip.StripAmpersands = !value;
            Assert.Equal (!value, toolTip.StripAmpersands);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void UseAnimation_Set_GetReturnsExpected (bool value)
        {
            using var toolTip = new ToolTip { UseAnimation = value };
            Assert.Equal (value, toolTip.UseAnimation);

            // Set same.
            toolTip.UseAnimation = value;
            Assert.Equal (value, toolTip.UseAnimation);

            // Set different.
            toolTip.UseAnimation = !value;
            Assert.Equal (!value, toolTip.UseAnimation);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void UseFading_Set_GetReturnsExpected (bool value)
        {
            using var toolTip = new ToolTip { UseFading = value };
            Assert.Equal (value, toolTip.UseFading);

            // Set same.
            toolTip.UseFading = value;
            Assert.Equal (value, toolTip.UseFading);

            // Set different.
            toolTip.UseFading = !value;
            Assert.Equal (!value, toolTip.UseFading);
        }

        [Theory]
        [InlineData (ToolTipIcon.None)]
        [InlineData (ToolTipIcon.Info)]
        [InlineData (ToolTipIcon.Warning)]
        [InlineData (ToolTipIcon.Error)]
        public void ToolTipIcon_Set_GetReturnsExpected (ToolTipIcon value)
        {
            using var toolTip = new ToolTip { ToolTipIcon = value };
            Assert.Equal (value, toolTip.ToolTipIcon);

            // Set same.
            toolTip.ToolTipIcon = value;
            Assert.Equal (value, toolTip.ToolTipIcon);
        }

        [Theory]
        [InlineData ("")]
        [InlineData ("title")]
        public void ToolTipTitle_Set_GetReturnsExpected (string value)
        {
            using var toolTip = new ToolTip { ToolTipTitle = value };
            Assert.Equal (value, toolTip.ToolTipTitle);

            // Set same.
            toolTip.ToolTipTitle = value;
            Assert.Equal (value, toolTip.ToolTipTitle);
        }

        [Fact]
        public void GetToolTip_NullControl_ReturnsEmpty ()
        {
            using var toolTip = new ToolTip ();
            Assert.Equal (string.Empty, toolTip.GetToolTip (null!));
        }

        [Fact]
        public void GetToolTip_NoSuchControl_ReturnsEmpty ()
        {
            using var toolTip = new ToolTip ();
            using var control = new Control ();
            Assert.Equal (string.Empty, toolTip.GetToolTip (control));
        }

        [Theory]
        [InlineData ("caption")]
        [InlineData ("some longer caption text")]
        public void SetToolTip_Invoke_GetToolTipReturnsExpected (string caption)
        {
            using var toolTip = new ToolTip ();
            using var control = new Control ();

            toolTip.SetToolTip (control, caption);
            Assert.Equal (caption, toolTip.GetToolTip (control));

            // Set same.
            toolTip.SetToolTip (control, caption);
            Assert.Equal (caption, toolTip.GetToolTip (control));
        }

        [Fact]
        public void SetToolTip_OverwritesExisting ()
        {
            using var toolTip = new ToolTip ();
            using var control = new Control ();

            toolTip.SetToolTip (control, "first");
            Assert.Equal ("first", toolTip.GetToolTip (control));

            toolTip.SetToolTip (control, "second");
            Assert.Equal ("second", toolTip.GetToolTip (control));
        }

        [Theory]
        [InlineData ("")]
        [InlineData (null)]
        public void SetToolTip_EmptyCaption_ClearsToolTip (string? caption)
        {
            using var toolTip = new ToolTip ();
            using var control = new Control ();

            toolTip.SetToolTip (control, "caption");
            Assert.Equal ("caption", toolTip.GetToolTip (control));

            toolTip.SetToolTip (control, caption!);
            Assert.Equal (string.Empty, toolTip.GetToolTip (control));
        }

        [Fact]
        public void SetToolTip_NullControl_ThrowsArgumentNullException ()
        {
            using var toolTip = new ToolTip ();
            Assert.Throws<ArgumentNullException> (() => toolTip.SetToolTip (null!, "caption"));
        }

        [Fact]
        public void RemoveAll_InvokeWithTools_GetToolTipReturnsEmpty ()
        {
            using var control = new Control ();
            using var toolTip = new ToolTip ();

            toolTip.SetToolTip (control, "caption");
            toolTip.RemoveAll ();
            Assert.Equal (string.Empty, toolTip.GetToolTip (control));

            // Invoke again.
            toolTip.RemoveAll ();
            Assert.Equal (string.Empty, toolTip.GetToolTip (control));
        }

        [Fact]
        public void RemoveAll_InvokeWithoutTools_Nop ()
        {
            using var toolTip = new ToolTip ();
            toolTip.RemoveAll ();
            toolTip.RemoveAll ();
        }
    }
}
