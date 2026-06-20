// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/FlowLayoutPanelTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms FlowLayoutPanelTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility/Ime/layout-event plumbing). They
    // pin the default ctor values, FlowDirection get/set + enum validation, WrapContents get/set,
    // GetFlowBreak/SetFlowBreak round-trip and argument validation, the IExtenderProvider.CanExtend
    // contract, BorderStyle (inherited from Panel) and the Controls collection.
    public class FlowLayoutPanelTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new FlowLayoutPanel ();

            Assert.Equal (FlowDirection.LeftToRight, control.FlowDirection);
            Assert.True (control.WrapContents);
            Assert.Equal (BorderStyle.None, control.BorderStyle);
            Assert.Empty (control.Controls);
            Assert.NotNull (control.LayoutEngine);
            Assert.Same (control.LayoutEngine, control.LayoutEngine);
        }

        [Theory]
        [InlineData (FlowDirection.LeftToRight)]
        [InlineData (FlowDirection.TopDown)]
        [InlineData (FlowDirection.RightToLeft)]
        [InlineData (FlowDirection.BottomUp)]
        public void FlowDirection_Set_GetReturnsExpected (FlowDirection value)
        {
            using var control = new FlowLayoutPanel { FlowDirection = value };

            Assert.Equal (value, control.FlowDirection);

            // Set same.
            control.FlowDirection = value;
            Assert.Equal (value, control.FlowDirection);
        }

        [Fact]
        public void FlowDirection_SetDifferent_GetReturnsExpected ()
        {
            using var control = new FlowLayoutPanel ();

            control.FlowDirection = FlowDirection.TopDown;
            Assert.Equal (FlowDirection.TopDown, control.FlowDirection);

            control.FlowDirection = FlowDirection.BottomUp;
            Assert.Equal (FlowDirection.BottomUp, control.FlowDirection);

            control.FlowDirection = FlowDirection.LeftToRight;
            Assert.Equal (FlowDirection.LeftToRight, control.FlowDirection);
        }

        [Theory]
        [InlineData ((FlowDirection)(-1))]
        [InlineData ((FlowDirection)4)]
        [InlineData ((FlowDirection)int.MaxValue)]
        public void FlowDirection_SetInvalid_ThrowsInvalidEnumArgumentException (FlowDirection value)
        {
            using var control = new FlowLayoutPanel ();

            Assert.Throws<InvalidEnumArgumentException> (() => control.FlowDirection = value);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void WrapContents_Set_GetReturnsExpected (bool value)
        {
            using var control = new FlowLayoutPanel { WrapContents = value };

            Assert.Equal (value, control.WrapContents);

            // Set same.
            control.WrapContents = value;
            Assert.Equal (value, control.WrapContents);

            // Set different.
            control.WrapContents = !value;
            Assert.Equal (!value, control.WrapContents);
        }

        [Fact]
        public void GetFlowBreak_DefaultControl_ReturnsFalse ()
        {
            using var child = new Control ();
            using var control = new FlowLayoutPanel ();

            Assert.False (control.GetFlowBreak (child));
        }

        [Fact]
        public void GetFlowBreak_NullControl_ThrowsArgumentNullException ()
        {
            using var control = new FlowLayoutPanel ();

            Assert.Throws<ArgumentNullException> (() => control.GetFlowBreak (null!));
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void SetFlowBreak_Invoke_GetFlowBreakReturnsExpected (bool value)
        {
            using var child = new Control ();
            using var control = new FlowLayoutPanel ();

            control.SetFlowBreak (child, value);
            Assert.Equal (value, control.GetFlowBreak (child));

            // Set same.
            control.SetFlowBreak (child, value);
            Assert.Equal (value, control.GetFlowBreak (child));

            // Set different.
            control.SetFlowBreak (child, !value);
            Assert.Equal (!value, control.GetFlowBreak (child));
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void SetFlowBreak_NullControl_ThrowsArgumentNullException (bool value)
        {
            using var control = new FlowLayoutPanel ();

            Assert.Throws<ArgumentNullException> (() => control.SetFlowBreak (null!, value));
        }

        [Fact]
        public void CanExtend_ControlWithParent_ReturnsTrue ()
        {
            using var control = new FlowLayoutPanel ();
            using var extendee = new Control { Parent = control };
            IExtenderProvider extenderProvider = control;

            Assert.True (extenderProvider.CanExtend (extendee));
        }

        [Fact]
        public void CanExtend_Null_ReturnsFalse ()
        {
            using var control = new FlowLayoutPanel ();
            IExtenderProvider extenderProvider = control;

            Assert.False (extenderProvider.CanExtend (null!));
        }

        [Fact]
        public void CanExtend_ControlNoParent_ReturnsFalse ()
        {
            using var control = new FlowLayoutPanel ();
            using var extendee = new Control ();
            IExtenderProvider extenderProvider = control;

            Assert.False (extenderProvider.CanExtend (extendee));
        }

        [Fact]
        public void CanExtend_ControlWithDifferentParent_ReturnsFalse ()
        {
            using var control = new FlowLayoutPanel ();
            using var other = new FlowLayoutPanel ();
            using var extendee = new Control { Parent = other };
            IExtenderProvider extenderProvider = control;

            Assert.False (extenderProvider.CanExtend (extendee));
        }

        [Fact]
        public void CanExtend_NonControl_ReturnsFalse ()
        {
            using var control = new FlowLayoutPanel ();
            IExtenderProvider extenderProvider = control;

            Assert.False (extenderProvider.CanExtend (new object ()));
        }

        [Theory]
        [InlineData (BorderStyle.None)]
        [InlineData (BorderStyle.FixedSingle)]
        [InlineData (BorderStyle.Fixed3D)]
        public void BorderStyle_Set_GetReturnsExpected (BorderStyle value)
        {
            using var control = new FlowLayoutPanel { BorderStyle = value };

            Assert.Equal (value, control.BorderStyle);

            // Set same.
            control.BorderStyle = value;
            Assert.Equal (value, control.BorderStyle);
        }

        [Theory]
        [InlineData ((BorderStyle)(-1))]
        [InlineData ((BorderStyle)3)]
        [InlineData ((BorderStyle)int.MaxValue)]
        public void BorderStyle_SetInvalid_ThrowsInvalidEnumArgumentException (BorderStyle value)
        {
            using var control = new FlowLayoutPanel ();

            Assert.Throws<InvalidEnumArgumentException> (() => control.BorderStyle = value);
        }

        [Fact]
        public void Controls_AddChild_Success ()
        {
            using var control = new FlowLayoutPanel ();
            using var child = new Control ();

            Assert.Empty (control.Controls);

            control.Controls.Add (child);
            Assert.Single (control.Controls);
            Assert.Same (child, control.Controls[0]);
            Assert.Same (control, child.Parent);
        }

        [Fact]
        public void Controls_SetChildParent_AddsToControls ()
        {
            using var control = new FlowLayoutPanel ();
            using var child = new Control { Parent = control };

            Assert.Single (control.Controls);
            Assert.Same (child, control.Controls[0]);
        }
    }
}
