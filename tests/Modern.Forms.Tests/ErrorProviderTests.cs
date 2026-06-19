// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ErrorProviderTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.ComponentModel;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ErrorProviderTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility/Moq/data-binding plumbing). They pin
    // the SetError/GetError round-trip and Clear semantics, BlinkRate/BlinkStyle interaction and
    // validation, per-control icon alignment/padding round-trips, HasErrors, and the simple
    // property round-trips that Modern.Forms supports.
    public class ErrorProviderTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var provider = new ErrorProvider ();

            Assert.Equal (250, provider.BlinkRate);
            Assert.Equal (ErrorBlinkStyle.BlinkIfDifferentError, provider.BlinkStyle);
            Assert.Null (provider.ContainerControl);
            Assert.Equal (string.Empty, provider.DataMember);
            Assert.Null (provider.DataSource);
            Assert.Null (provider.Tag);
            Assert.False (provider.RightToLeft);
            Assert.False (provider.HasErrors);
        }

        [Fact]
        public void Ctor_IContainer ()
        {
            using var container = new Container ();
            using var provider = new ErrorProvider (container);

            Assert.Equal (250, provider.BlinkRate);
            Assert.Equal (ErrorBlinkStyle.BlinkIfDifferentError, provider.BlinkStyle);
            Assert.Same (container, provider.Container);
        }

        [Fact]
        public void Ctor_NullContainer_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException> (() => new ErrorProvider ((IContainer)null!));
        }

        [Theory]
        [InlineData (0, ErrorBlinkStyle.NeverBlink)]
        [InlineData (1, ErrorBlinkStyle.BlinkIfDifferentError)]
        [InlineData (250, ErrorBlinkStyle.BlinkIfDifferentError)]
        public void BlinkRate_Set_GetReturnsExpected (int value, ErrorBlinkStyle expectedBlinkStyle)
        {
            using var provider = new ErrorProvider { BlinkRate = value };

            Assert.Equal (value, provider.BlinkRate);
            Assert.Equal (expectedBlinkStyle, provider.BlinkStyle);

            // Set same.
            provider.BlinkRate = value;
            Assert.Equal (value, provider.BlinkRate);
            Assert.Equal (expectedBlinkStyle, provider.BlinkStyle);

            // Set blink style.
            provider.BlinkStyle = ErrorBlinkStyle.BlinkIfDifferentError;
            Assert.Equal (value, provider.BlinkRate);
            Assert.Equal (expectedBlinkStyle, provider.BlinkStyle);
        }

        [Fact]
        public void BlinkRate_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentOutOfRangeException> (() => provider.BlinkRate = -1);
        }

        [Theory]
        [InlineData (ErrorBlinkStyle.BlinkIfDifferentError)]
        [InlineData (ErrorBlinkStyle.AlwaysBlink)]
        [InlineData (ErrorBlinkStyle.NeverBlink)]
        public void BlinkStyle_Set_GetReturnsExpected (ErrorBlinkStyle value)
        {
            using var provider = new ErrorProvider { BlinkStyle = value };

            Assert.Equal (value, provider.BlinkStyle);
            Assert.Equal (250, provider.BlinkRate);

            // Set same.
            provider.BlinkStyle = value;
            Assert.Equal (value, provider.BlinkStyle);
            Assert.Equal (250, provider.BlinkRate);
        }

        [Theory]
        [InlineData (ErrorBlinkStyle.BlinkIfDifferentError)]
        [InlineData (ErrorBlinkStyle.AlwaysBlink)]
        [InlineData (ErrorBlinkStyle.NeverBlink)]
        public void BlinkStyle_SetAlreadyBlink_GetReturnsExpected (ErrorBlinkStyle value)
        {
            using var provider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.AlwaysBlink };

            provider.BlinkStyle = value;
            Assert.Equal (value, provider.BlinkStyle);
            Assert.Equal (250, provider.BlinkRate);

            // Set same.
            provider.BlinkStyle = value;
            Assert.Equal (value, provider.BlinkStyle);
            Assert.Equal (250, provider.BlinkRate);
        }

        [Theory]
        [InlineData (ErrorBlinkStyle.BlinkIfDifferentError)]
        [InlineData (ErrorBlinkStyle.AlwaysBlink)]
        [InlineData (ErrorBlinkStyle.NeverBlink)]
        public void BlinkStyle_SetWithZeroBlinkRate_GetReturnsExpected (ErrorBlinkStyle value)
        {
            using var provider = new ErrorProvider {
                BlinkRate = 0,
                BlinkStyle = value
            };

            Assert.Equal (ErrorBlinkStyle.NeverBlink, provider.BlinkStyle);
            Assert.Equal (0, provider.BlinkRate);

            // Set same.
            provider.BlinkStyle = value;
            Assert.Equal (ErrorBlinkStyle.NeverBlink, provider.BlinkStyle);
            Assert.Equal (0, provider.BlinkRate);
        }

        [Theory]
        [InlineData ((ErrorBlinkStyle)(-1))]
        [InlineData ((ErrorBlinkStyle)3)]
        public void BlinkStyle_SetInvalidValue_ThrowsInvalidEnumArgumentException (ErrorBlinkStyle value)
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<InvalidEnumArgumentException> (() => provider.BlinkStyle = value);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("")]
        [InlineData ("dataMember")]
        public void DataMember_Set_GetReturnsExpected (string? value)
        {
            using var provider = new ErrorProvider { DataMember = value! };
            Assert.Same (value, provider.DataMember);

            // Set same.
            provider.DataMember = value!;
            Assert.Same (value, provider.DataMember);
        }

        [Fact]
        public void DataSource_Set_GetReturnsExpected ()
        {
            var value = new object ();
            using var provider = new ErrorProvider { DataSource = value };
            Assert.Same (value, provider.DataSource);

            // Set same.
            provider.DataSource = value;
            Assert.Same (value, provider.DataSource);

            // Set null.
            provider.DataSource = null;
            Assert.Null (provider.DataSource);
        }

        [Theory]
        [InlineData (null)]
        [InlineData ("tag")]
        public void Tag_Set_GetReturnsExpected (object? value)
        {
            using var provider = new ErrorProvider { Tag = value };
            Assert.Same (value, provider.Tag);

            // Set same.
            provider.Tag = value;
            Assert.Same (value, provider.Tag);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void RightToLeft_Set_GetReturnsExpected (bool value)
        {
            using var provider = new ErrorProvider { RightToLeft = value };
            Assert.Equal (value, provider.RightToLeft);

            // Set same.
            provider.RightToLeft = value;
            Assert.Equal (value, provider.RightToLeft);

            // Set different.
            provider.RightToLeft = !value;
            Assert.Equal (!value, provider.RightToLeft);
        }

        [Fact]
        public void RightToLeft_SetWithHandler_CallsRightToLeftChanged ()
        {
            using var provider = new ErrorProvider { RightToLeft = true };
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (provider, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            provider.RightToLeftChanged += handler;

            // Set different.
            provider.RightToLeft = false;
            Assert.False (provider.RightToLeft);
            Assert.Equal (1, callCount);

            // Set same.
            provider.RightToLeft = false;
            Assert.False (provider.RightToLeft);
            Assert.Equal (1, callCount);

            // Set different.
            provider.RightToLeft = true;
            Assert.True (provider.RightToLeft);
            Assert.Equal (2, callCount);

            // Remove handler.
            provider.RightToLeftChanged -= handler;
            provider.RightToLeft = false;
            Assert.False (provider.RightToLeft);
            Assert.Equal (2, callCount);
        }

        [Theory]
        [InlineData (null, "")]
        [InlineData ("", "")]
        [InlineData ("value", "value")]
        public void SetError_Invoke_GetErrorReturnsExpected (string? value, string expected)
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, value!);
            Assert.Equal (expected, provider.GetError (control));

            // Call again.
            provider.SetError (control, value!);
            Assert.Equal (expected, provider.GetError (control));

            // Set empty.
            provider.SetError (control, string.Empty);
            Assert.Equal (string.Empty, provider.GetError (control));
        }

        [Fact]
        public void SetError_NullControl_ThrowsArgumentNullException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentNullException> (() => provider.SetError (null!, "value"));
        }

        [Fact]
        public void GetError_InvokeWithoutError_ReturnsEmpty ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            Assert.Equal (string.Empty, provider.GetError (control));

            // Call again.
            Assert.Equal (string.Empty, provider.GetError (control));
        }

        [Fact]
        public void GetError_NullControl_ThrowsArgumentNullException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentNullException> (() => provider.GetError (null!));
        }

        [Fact]
        public void Clear_InvokeMultipleTimesWithItems_Success ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, "error");
            Assert.Equal ("error", provider.GetError (control));

            provider.Clear ();
            Assert.Equal (string.Empty, provider.GetError (control));

            provider.Clear ();
            Assert.Equal (string.Empty, provider.GetError (control));
        }

        [Fact]
        public void Clear_InvokeMultipleTimesWithoutItems_Nop ()
        {
            using var provider = new ErrorProvider ();
            provider.Clear ();
            provider.Clear ();
        }

        [Fact]
        public void HasErrors_NoControlSet_ReturnsFalse ()
        {
            using var provider = new ErrorProvider ();
            Assert.False (provider.HasErrors);
        }

        [Fact]
        public void HasErrors_SomeControlsSet_ReturnsTrue ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, "error");
            Assert.True (provider.HasErrors);
        }

        [Fact]
        public void HasErrors_Cleared_ReturnsFalse ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, "Some error");
            Assert.True (provider.HasErrors);

            provider.Clear ();
            Assert.False (provider.HasErrors);
        }

        [Fact]
        public void HasErrors_AfterErrorSetNull_ReturnsFalse ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, "Some error");
            Assert.True (provider.HasErrors);

            provider.SetError (control, null!);
            Assert.False (provider.HasErrors);
        }

        [Fact]
        public void HasErrors_AfterErrorSetEmptyString_ReturnsFalse ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, "Some error");
            Assert.True (provider.HasErrors);

            provider.SetError (control, string.Empty);
            Assert.False (provider.HasErrors);
        }

        [Fact]
        public void HasErrors_SetErrorMultiple_ReturnsTrue ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetError (control, "Some error");
            provider.SetError (control, "Some error");
            provider.SetError (control, "Some error");
            Assert.True (provider.HasErrors);
        }

        [Fact]
        public void HasErrors_AfterGetError_ReturnsFalse ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            _ = provider.GetError (control);
            Assert.False (provider.HasErrors);
        }

        [Theory]
        [InlineData (ErrorIconAlignment.TopLeft)]
        [InlineData (ErrorIconAlignment.TopRight)]
        [InlineData (ErrorIconAlignment.MiddleLeft)]
        [InlineData (ErrorIconAlignment.MiddleRight)]
        [InlineData (ErrorIconAlignment.BottomLeft)]
        [InlineData (ErrorIconAlignment.BottomRight)]
        public void SetIconAlignment_Invoke_GetIconAlignmentReturnsExpected (ErrorIconAlignment value)
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetIconAlignment (control, value);
            Assert.Equal (value, provider.GetIconAlignment (control));

            // Call again.
            provider.SetIconAlignment (control, value);
            Assert.Equal (value, provider.GetIconAlignment (control));
        }

        [Fact]
        public void GetIconAlignment_InvokeWithoutError_ReturnsMiddleRight ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            Assert.Equal (ErrorIconAlignment.MiddleRight, provider.GetIconAlignment (control));

            // Call again.
            Assert.Equal (ErrorIconAlignment.MiddleRight, provider.GetIconAlignment (control));
        }

        [Fact]
        public void GetIconAlignment_NullControl_ThrowsArgumentNullException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentNullException> (() => provider.GetIconAlignment (null!));
        }

        [Fact]
        public void SetIconAlignment_NullControl_ThrowsArgumentNullException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentNullException> (() => provider.SetIconAlignment (null!, ErrorIconAlignment.MiddleRight));
        }

        [Theory]
        [InlineData ((ErrorIconAlignment)(-1))]
        [InlineData ((ErrorIconAlignment)6)]
        public void SetIconAlignment_InvalidValue_ThrowsInvalidEnumArgumentException (ErrorIconAlignment value)
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();
            Assert.Throws<InvalidEnumArgumentException> (() => provider.SetIconAlignment (control, value));
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        public void SetIconPadding_Invoke_GetIconPaddingReturnsExpected (int value)
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            provider.SetIconPadding (control, value);
            Assert.Equal (value, provider.GetIconPadding (control));

            // Call again.
            provider.SetIconPadding (control, value);
            Assert.Equal (value, provider.GetIconPadding (control));
        }

        [Fact]
        public void GetIconPadding_InvokeWithoutError_ReturnsZero ()
        {
            using var provider = new ErrorProvider ();
            using var control = new Control ();

            Assert.Equal (0, provider.GetIconPadding (control));

            // Call again.
            Assert.Equal (0, provider.GetIconPadding (control));
        }

        [Fact]
        public void GetIconPadding_NullControl_ThrowsArgumentNullException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentNullException> (() => provider.GetIconPadding (null!));
        }

        [Fact]
        public void SetIconPadding_NullControl_ThrowsArgumentNullException ()
        {
            using var provider = new ErrorProvider ();
            Assert.Throws<ArgumentNullException> (() => provider.SetIconPadding (null!, 0));
        }
    }
}
