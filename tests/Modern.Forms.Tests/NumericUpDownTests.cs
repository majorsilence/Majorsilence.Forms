// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/NumericUpDownTests.cs),
// rewritten for the Modern.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Modern.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms NumericUpDownTests, adapted to the
    // Modern.Forms API (no Handle/CreateParams/accessibility/Accelerations/edit-text plumbing).
    // They pin the same default values, Minimum/Maximum/Value coercion, Increment validation,
    // and UpButton/DownButton clamping (including decimal overflow) semantics.
    public class NumericUpDownTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new NumericUpDown ();

            Assert.Equal (0m, control.Value);
            Assert.Equal (0m, control.Minimum);
            Assert.Equal (100m, control.Maximum);
            Assert.Equal (1m, control.Increment);
            Assert.Equal (0, control.DecimalPlaces);
            Assert.False (control.Hexadecimal);
            Assert.False (control.ThousandsSeparator);
        }

        [Fact]
        public void Value_Set_GetReturnsExpected ()
        {
            using var control = new NumericUpDown { Value = 50 };
            Assert.Equal (50m, control.Value);
        }

        [Theory]
        [InlineData (-10)]
        [InlineData (-1)]
        public void Value_SetBelowMinimum_ClampsToMinimum (int value)
        {
            using var control = new NumericUpDown { Minimum = 0, Maximum = 100, Value = value };
            Assert.Equal (0m, control.Value);
        }

        [Theory]
        [InlineData (101)]
        [InlineData (200)]
        public void Value_SetAboveMaximum_ClampsToMaximum (int value)
        {
            using var control = new NumericUpDown { Minimum = 0, Maximum = 100, Value = value };
            Assert.Equal (100m, control.Value);
        }

        [Fact]
        public void Value_SetChanged_RaisesValueChanged ()
        {
            using var control = new NumericUpDown ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };

            control.ValueChanged += handler;
            control.Value = 10;
            Assert.Equal (1, callCount);

            // Setting the same value should not raise again.
            control.Value = 10;
            Assert.Equal (1, callCount);

            control.ValueChanged -= handler;
            control.Value = 20;
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void Minimum_Set_GetReturnsExpected ()
        {
            using var control = new NumericUpDown { Minimum = 10 };
            Assert.Equal (10m, control.Minimum);
        }

        [Fact]
        public void Maximum_Set_GetReturnsExpected ()
        {
            using var control = new NumericUpDown { Maximum = 200 };
            Assert.Equal (200m, control.Maximum);
        }

        [Fact]
        public void Maximum_SetLessThanMinimum_SetsMinimumToMaximum ()
        {
            using var control = new NumericUpDown { Minimum = 100m, Maximum = 50m };

            Assert.Equal (50m, control.Maximum);
            Assert.Equal (50m, control.Minimum);
        }

        [Fact]
        public void Maximum_SetGreaterThanMinimum_KeepsMinimumUnchanged ()
        {
            using var control = new NumericUpDown { Minimum = 50m, Maximum = 100m };

            Assert.Equal (100m, control.Maximum);
            Assert.Equal (50m, control.Minimum);
        }

        [Fact]
        public void Maximum_SetLessThanValue_ClampsValue ()
        {
            using var control = new NumericUpDown { Value = 80, Maximum = 50 };

            Assert.Equal (50m, control.Maximum);
            Assert.Equal (50m, control.Value);
        }

        [Fact]
        public void Minimum_SetGreaterThanMaximum_SetsMaximumToMinimum ()
        {
            using var control = new NumericUpDown { Maximum = 10, Minimum = 20 };

            Assert.Equal (20m, control.Maximum);
            Assert.Equal (20m, control.Minimum);
        }

        [Fact]
        public void Minimum_SetLessThanMaximum_KeepsMaximumUnchanged ()
        {
            using var control = new NumericUpDown { Maximum = 30, Minimum = 10 };

            Assert.Equal (30m, control.Maximum);
            Assert.Equal (10m, control.Minimum);
        }

        [Fact]
        public void Minimum_SetGreaterThanValue_ClampsValue ()
        {
            using var control = new NumericUpDown { Value = 5, Minimum = 10 };

            Assert.Equal (10m, control.Minimum);
            Assert.Equal (10m, control.Value);
        }

        [Theory]
        [InlineData (2)]
        [InlineData (5)]
        [InlineData (0)]
        public void Increment_Set_GetReturnsExpected (int value)
        {
            using var control = new NumericUpDown { Increment = value };
            Assert.Equal ((decimal) value, control.Increment);
        }

        [Fact]
        public void Increment_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new NumericUpDown ();
            var ex = Assert.Throws<ArgumentOutOfRangeException> (() => control.Increment = -1);
            Assert.Equal ("value", ex.ParamName);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (2)]
        [InlineData (10)]
        public void DecimalPlaces_Set_GetReturnsExpected (int value)
        {
            using var control = new NumericUpDown { DecimalPlaces = value };
            Assert.Equal (value, control.DecimalPlaces);
        }

        [Fact]
        public void Hexadecimal_Set_GetReturnsExpected ()
        {
            using var control = new NumericUpDown ();
            Assert.False (control.Hexadecimal);

            control.Hexadecimal = true;
            Assert.True (control.Hexadecimal);

            control.Hexadecimal = false;
            Assert.False (control.Hexadecimal);
        }

        [Fact]
        public void ThousandsSeparator_Set_GetReturnsExpected ()
        {
            using var control = new NumericUpDown ();
            Assert.False (control.ThousandsSeparator);

            control.ThousandsSeparator = true;
            Assert.True (control.ThousandsSeparator);
        }

        [Fact]
        public void UpButton_ValueLessThanMaximum_IncrementsValue ()
        {
            using var control = new NumericUpDown { Value = 99, Maximum = 100 };
            control.UpButton ();
            Assert.Equal (100m, control.Value);
        }

        [Fact]
        public void UpButton_ValueAtMaximum_DoesNotChangeValue ()
        {
            using var control = new NumericUpDown { Value = 100, Maximum = 100, Increment = 1 };
            control.UpButton ();
            Assert.Equal (100m, control.Value);
        }

        [Fact]
        public void UpButton_IncrementBeyondMaximum_ClampsToMaximum ()
        {
            using var control = new NumericUpDown { Minimum = 0, Maximum = 20, Value = 19, Increment = 5 };
            control.UpButton ();
            Assert.Equal (20m, control.Value);
        }

        [Fact]
        public void UpButton_ValueCausesOverflow_SetsValueToMaximum ()
        {
            using var control = new NumericUpDown ();
            control.Maximum = decimal.MaxValue;
            control.Value = decimal.MaxValue;
            control.UpButton ();
            Assert.Equal (decimal.MaxValue, control.Value);
        }

        [Fact]
        public void UpButton_IncrementCausesOverflow_SetsValueToMaximum ()
        {
            using var control = new NumericUpDown ();
            control.Maximum = decimal.MaxValue;
            control.Value = decimal.MaxValue - 1;
            control.Increment = 2;
            control.UpButton ();
            Assert.Equal (decimal.MaxValue, control.Value);
        }

        [Fact]
        public void DownButton_ValueGreaterThanMinimum_DecrementsValue ()
        {
            using var control = new NumericUpDown { Minimum = 10, Value = 11 };
            control.DownButton ();
            Assert.Equal (10m, control.Value);
        }

        [Fact]
        public void DownButton_ValueAtMinimum_DoesNotChangeValue ()
        {
            using var control = new NumericUpDown { Value = 0, Minimum = 0, Increment = 1 };
            control.DownButton ();
            Assert.Equal (0m, control.Value);
        }

        [Fact]
        public void DownButton_ValueEqualToMinimum_ValueRemainsSame ()
        {
            using var control = new NumericUpDown { Minimum = 10, Value = 10 };
            control.DownButton ();
            Assert.Equal (10m, control.Value);
        }

        [Fact]
        public void DownButton_DecrementBelowMinimum_ClampsToMinimum ()
        {
            using var control = new NumericUpDown { Minimum = 10, Value = 15, Increment = 10 };
            control.DownButton ();
            Assert.Equal (10m, control.Value);
        }

        [Fact]
        public void DownButton_ValueCausesOverflow_SetsValueToMinimum ()
        {
            using var control = new NumericUpDown ();
            control.Minimum = decimal.MinValue;
            control.Value = decimal.MinValue;
            control.DownButton ();
            Assert.Equal (decimal.MinValue, control.Value);
        }

        [Fact]
        public void PerformIncrement_BehavesLikeUpButton ()
        {
            using var control = new NumericUpDown { Value = 5, Maximum = 100, Increment = 3 };
            control.PerformIncrement ();
            Assert.Equal (8m, control.Value);
        }

        [Fact]
        public void PerformDecrement_BehavesLikeDownButton ()
        {
            using var control = new NumericUpDown { Value = 5, Minimum = 0, Increment = 3 };
            control.PerformDecrement ();
            Assert.Equal (2m, control.Value);
        }
    }
}
