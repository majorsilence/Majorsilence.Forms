// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/TrackBarTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using System.Drawing;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms TrackBarTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility/ImeMode/RightToLeft plumbing).
    // They pin the WinForms range semantics: Minimum/Maximum coerce each other (and Value)
    // rather than throwing, Value-out-of-range throws, SmallChange/LargeChange reject
    // negatives, TickFrequency accepts any value, and ValueChanged fires only on real change.
    public class TrackBarTests
    {
        [Fact]
        public void Ctor_Default ()
        {
            using var control = new TrackBar ();

            Assert.True (control.AutoSize);
            Assert.Equal (0, control.Minimum);
            Assert.Equal (10, control.Maximum);
            Assert.Equal (0, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
            Assert.Equal (1, control.TickFrequency);
            Assert.Equal (TickStyle.BottomRight, control.TickStyle);
            Assert.Equal (Orientation.Horizontal, control.Orientation);
            Assert.False (control.SnapToTicks);
            Assert.Equal (new Size (104, 32), control.Size);
        }

        // ---- Maximum ----

        [Theory]
        [InlineData (0)]
        [InlineData (8)]
        [InlineData (10)]
        [InlineData (11)]
        public void Maximum_Set_GetReturnsExpected (int value)
        {
            using var control = new TrackBar { Maximum = value };

            Assert.Equal (value, control.Maximum);
            Assert.Equal (0, control.Minimum);
            Assert.Equal (0, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);

            // Set same.
            control.Maximum = value;
            Assert.Equal (value, control.Maximum);
        }

        [Fact]
        public void Maximum_SetLessThanValueAndMinimum_SetsValueAndMinimum ()
        {
            using var control = new TrackBar {
                Value = 10,
                Minimum = 8,
                Maximum = 5
            };

            Assert.Equal (5, control.Maximum);
            Assert.Equal (5, control.Minimum);
            Assert.Equal (5, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
        }

        [Fact]
        public void Maximum_SetNegative_SetsValueAndMinimum ()
        {
            using var control = new TrackBar { Maximum = -1 };

            Assert.Equal (-1, control.Maximum);
            Assert.Equal (-1, control.Minimum);
            Assert.Equal (-1, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
        }

        // ---- Minimum ----

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (5)]
        public void Minimum_Set_GetReturnsExpected (int value)
        {
            using var control = new TrackBar {
                Value = 5,
                Minimum = value
            };

            Assert.Equal (10, control.Maximum);
            Assert.Equal (value, control.Minimum);
            Assert.Equal (5, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);

            // Set same.
            control.Minimum = value;
            Assert.Equal (value, control.Minimum);
        }

        [Fact]
        public void Minimum_SetGreaterThanValueAndMaximum_SetsValueAndMaximum ()
        {
            using var control = new TrackBar {
                Value = 10,
                Maximum = 8,
                Minimum = 12
            };

            Assert.Equal (12, control.Maximum);
            Assert.Equal (12, control.Minimum);
            Assert.Equal (12, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);
        }

        // ---- Value ----

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        [InlineData (9)]
        [InlineData (10)]
        public void Value_Set_GetReturnsExpected (int value)
        {
            using var control = new TrackBar { Value = value };

            Assert.Equal (10, control.Maximum);
            Assert.Equal (0, control.Minimum);
            Assert.Equal (value, control.Value);
            Assert.Equal (5, control.LargeChange);
            Assert.Equal (1, control.SmallChange);

            // Set same.
            control.Value = value;
            Assert.Equal (value, control.Value);
        }

        [Theory]
        [InlineData (-1)]
        [InlineData (11)]
        public void Value_SetOutOfRange_ThrowsArgumentOutOfRangeException (int value)
        {
            using var control = new TrackBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = value);
            Assert.Equal (0, control.Value);
        }

        [Fact]
        public void Value_SetWithHandler_CallsValueChanged ()
        {
            using var control = new TrackBar ();
            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Same (EventArgs.Empty, e);
                callCount++;
            };
            control.ValueChanged += handler;

            // Set different.
            control.Value = 1;
            Assert.Equal (1, control.Value);
            Assert.Equal (1, callCount);

            // Set same.
            control.Value = 1;
            Assert.Equal (1, control.Value);
            Assert.Equal (1, callCount);

            // Set different.
            control.Value = 2;
            Assert.Equal (2, control.Value);
            Assert.Equal (2, callCount);

            // Remove handler.
            control.ValueChanged -= handler;
            control.Value = 1;
            Assert.Equal (1, control.Value);
            Assert.Equal (2, callCount);
        }

        // ---- LargeChange ----

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        [InlineData (11)]
        public void LargeChange_Set_GetReturnsExpected (int value)
        {
            using var control = new TrackBar { LargeChange = value };

            Assert.Equal (value, control.LargeChange);

            // Set same.
            control.LargeChange = value;
            Assert.Equal (value, control.LargeChange);
        }

        [Fact]
        public void LargeChange_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TrackBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.LargeChange = -1);
            Assert.Equal (5, control.LargeChange);
        }

        // ---- SmallChange ----

        [Theory]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (5)]
        [InlineData (11)]
        public void SmallChange_Set_GetReturnsExpected (int value)
        {
            using var control = new TrackBar { SmallChange = value };

            Assert.Equal (value, control.SmallChange);

            // Set same.
            control.SmallChange = value;
            Assert.Equal (value, control.SmallChange);
        }

        [Fact]
        public void SmallChange_SetNegative_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new TrackBar ();

            Assert.Throws<ArgumentOutOfRangeException> (() => control.SmallChange = -1);
            Assert.Equal (1, control.SmallChange);
        }

        // ---- TickFrequency ----

        [Theory]
        [InlineData (-1)]
        [InlineData (0)]
        [InlineData (1)]
        [InlineData (20)]
        [InlineData (int.MaxValue)]
        public void TickFrequency_Set_GetReturnsExpected (int value)
        {
            using var control = new TrackBar { TickFrequency = value };

            Assert.Equal (value, control.TickFrequency);

            // Set same.
            control.TickFrequency = value;
            Assert.Equal (value, control.TickFrequency);
        }

        // ---- TickStyle ----

        [Theory]
        [InlineData (TickStyle.None)]
        [InlineData (TickStyle.TopLeft)]
        [InlineData (TickStyle.BottomRight)]
        [InlineData (TickStyle.Both)]
        public void TickStyle_Set_GetReturnsExpected (TickStyle value)
        {
            using var control = new TrackBar { TickStyle = value };

            Assert.Equal (value, control.TickStyle);

            // Set same.
            control.TickStyle = value;
            Assert.Equal (value, control.TickStyle);
        }

        // ---- Orientation ----

        [Theory]
        [InlineData (Orientation.Horizontal)]
        [InlineData (Orientation.Vertical)]
        public void Orientation_Set_GetReturnsExpected (Orientation value)
        {
            using var control = new TrackBar { Orientation = value };

            Assert.Equal (value, control.Orientation);

            // Set same.
            control.Orientation = value;
            Assert.Equal (value, control.Orientation);
        }
    }
}
