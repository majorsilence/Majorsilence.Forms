// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/ProgressBarTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms ProgressBarTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility plumbing). They pin the same
    // Minimum/Maximum/Value clamping semantics, Increment/PerformStep behavior, and the
    // Marquee/argument validation rules.
    public class ProgressBarTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new ProgressBar ();

            Assert.Equal (0, control.Minimum);
            Assert.Equal (100, control.Maximum);
            Assert.Equal (0, control.Value);
            Assert.Equal (10, control.Step);
            Assert.Equal (100, control.MarqueeAnimationSpeed);
            Assert.Equal (ProgressBarStyle.Blocks, control.Style);
            Assert.Equal (new Size (100, 23), control.Size);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (5)]
        [InlineData (100)]
        public void Maximum_Set_GetReturnsExpected (int value)
        {
            using var control = new ProgressBar { Maximum = value };

            Assert.Equal (0, control.Minimum);
            Assert.Equal (value, control.Maximum);
            Assert.Equal (0, control.Value);

            // Set same.
            control.Maximum = value;
            Assert.Equal (value, control.Maximum);
        }

        [Theory]
        [InlineData (4)]
        [InlineData (0)]
        public void Maximum_SetLessThanMinimum_SetsMinimumToMaximum (int value)
        {
            using var control = new ProgressBar { Minimum = 5, Maximum = value };

            Assert.Equal (value, control.Minimum);
            Assert.Equal (value, control.Maximum);
            Assert.Equal (value, control.Value);
        }

        [Theory]
        [InlineData (4)]
        [InlineData (0)]
        public void Maximum_SetLessThanValue_SetsValueToMaximum (int value)
        {
            using var control = new ProgressBar { Value = 5, Maximum = value };

            Assert.Equal (0, control.Minimum);
            Assert.Equal (value, control.Maximum);
            Assert.Equal (value, control.Value);
        }

        [Fact]
        public void Maximum_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new ProgressBar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.Maximum = -1);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (5)]
        public void Minimum_Set_GetReturnsExpected (int value)
        {
            using var control = new ProgressBar { Value = 5, Minimum = value };

            Assert.Equal (value, control.Minimum);
            Assert.Equal (100, control.Maximum);
            Assert.Equal (5, control.Value);

            // Set same.
            control.Minimum = value;
            Assert.Equal (value, control.Minimum);
        }

        [Theory]
        [InlineData (101)]
        [InlineData (int.MaxValue)]
        public void Minimum_SetGreaterThanMaximum_SetsMaximumToMinimum (int value)
        {
            using var control = new ProgressBar { Minimum = value };

            Assert.Equal (value, control.Minimum);
            Assert.Equal (value, control.Maximum);
            Assert.Equal (value, control.Value);
        }

        [Theory]
        [InlineData (6)]
        public void Minimum_SetGreaterThanValue_SetsValueToMinimum (int value)
        {
            using var control = new ProgressBar { Value = 5, Minimum = value };

            Assert.Equal (value, control.Minimum);
            Assert.Equal (100, control.Maximum);
            Assert.Equal (value, control.Value);
        }

        [Fact]
        public void Minimum_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new ProgressBar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.Minimum = -1);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (5)]
        [InlineData (100)]
        public void Value_Set_GetReturnsExpected (int value)
        {
            using var control = new ProgressBar { Value = value };

            Assert.Equal (0, control.Minimum);
            Assert.Equal (100, control.Maximum);
            Assert.Equal (value, control.Value);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (101)]
        public void Value_SetInvalid_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new ProgressBar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = value);
        }

        [Theory]
        [InlineData (0, 100, 100)]
        [InlineData (0, 101, 100)]
        [InlineData (0, 0, 0)]
        [InlineData (0, -1, 0)]
        [InlineData (100, -1, 99)]
        [InlineData (100, -100, 0)]
        [InlineData (100, -101, 0)]
        public void Increment_Invoke_Success (int originalValue, int value, int expectedValue)
        {
            using var control = new ProgressBar { Value = originalValue };

            control.Increment (value);
            Assert.Equal (expectedValue, control.Value);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (1)]
        public void Increment_Marquee_ThrowsInvalidOperationException (int value)
        {
            using var control = new ProgressBar { Style = ProgressBarStyle.Marquee };
            Assert.Throws<InvalidOperationException> (() => control.Increment (value));
        }

        [Theory]
        [InlineData (0, 10, 10)]
        [InlineData (95, 10, 100)]
        [InlineData (100, 10, 100)]
        public void PerformStep_Invoke_Success (int originalValue, int step, int expectedValue)
        {
            using var control = new ProgressBar { Value = originalValue, Step = step };

            control.PerformStep ();
            Assert.Equal (expectedValue, control.Value);
        }

        [Fact]
        public void PerformStep_Marquee_ThrowsInvalidOperationException ()
        {
            using var control = new ProgressBar { Style = ProgressBarStyle.Marquee };
            Assert.Throws<InvalidOperationException> (control.PerformStep);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (100)]
        [InlineData (int.MaxValue)]
        public void MarqueeAnimationSpeed_Set_GetReturnsExpected (int value)
        {
            using var control = new ProgressBar { MarqueeAnimationSpeed = value };
            Assert.Equal (value, control.MarqueeAnimationSpeed);
        }

        [Fact]
        public void MarqueeAnimationSpeed_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new ProgressBar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.MarqueeAnimationSpeed = -1);
        }
    }
}
