// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/DateTimePickerTests.cs),
// rewritten for the Majorsilence.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms DateTimePickerTests, adapted to the
    // Majorsilence.Forms API (no Handle/CreateParams/accessibility plumbing, no FormatChanged event,
    // no native SysDateTimePick32 behaviors). They pin the same Value get/set + range
    // coercion/validation, MinDate/MaxDate validation + Value coercion, Format/CustomFormat
    // semantics, ShowCheckBox/Checked/ShowUpDown flags, and the ValueChanged event.
    public class DateTimePickerTests
    {
        // A fixed value safely inside the supported range, used to avoid any reliance on
        // DateTime.Now in assertions.
        private static readonly DateTime SampleDate = new DateTime (2021, 12, 31, 3, 4, 5);

        [Fact]
        public void Ctor_Default ()
        {
            using var control = new DateTimePicker ();

            Assert.Equal (DateTimePickerFormat.Long, control.Format);
            Assert.False (control.ShowCheckBox);
            Assert.False (control.ShowUpDown);
            Assert.True (control.Checked);
            Assert.Equal (new DateTime (1753, 1, 1), control.MinDate);
            Assert.Equal (new DateTime (9998, 12, 31), control.MaxDate);
            Assert.True (control.Value > DateTime.MinValue);
        }

        [Fact]
        public void MinimumDateTime_ReturnsExpected ()
        {
            Assert.Equal (new DateTime (1753, 1, 1), DateTimePicker.MinimumDateTime);
            Assert.Equal (new DateTime (1753, 1, 1), DateTimePicker.MinDateTime);
        }

        [Fact]
        public void MaximumDateTime_ReturnsExpected ()
        {
            Assert.Equal (new DateTime (9998, 12, 31), DateTimePicker.MaximumDateTime);
            Assert.Equal (new DateTime (9998, 12, 31), DateTimePicker.MaxDateTime);
        }

        [Theory]
        [InlineData (DateTimePickerFormat.Long)]
        [InlineData (DateTimePickerFormat.Short)]
        [InlineData (DateTimePickerFormat.Time)]
        [InlineData (DateTimePickerFormat.Custom)]
        public void Format_Set_GetReturnsExpected (DateTimePickerFormat value)
        {
            using var control = new DateTimePicker ();

            control.Format = value;
            Assert.Equal (value, control.Format);

            // Set same.
            control.Format = value;
            Assert.Equal (value, control.Format);
        }

        [Fact]
        public void Value_GetSet_ReturnsExpected ()
        {
            using var control = new DateTimePicker ();

            var initialDate = new DateTime (2022, 1, 1);
            var newDate = new DateTime (2023, 1, 1);

            control.Value = initialDate;
            Assert.Equal (initialDate, control.Value);

            control.Value = newDate;
            Assert.Equal (newDate, control.Value);

            control.Value = DateTimePicker.MinimumDateTime;
            Assert.Equal (DateTimePicker.MinimumDateTime, control.Value);

            control.Value = DateTimePicker.MaximumDateTime;
            Assert.Equal (DateTimePicker.MaximumDateTime, control.Value);
        }

        [Fact]
        public void Value_Set_PreservesTimeComponent ()
        {
            using var control = new DateTimePicker ();

            // The Long format only renders the date portion; the Value getter must still
            // return the full value (including the time) rather than re-parsing Text.
            control.Value = SampleDate;
            Assert.Equal (SampleDate, control.Value);
        }

        [Theory]
        [InlineData ("0001-01-01")]
        [InlineData ("9999-12-31")]
        public void Value_SetInvalid_ThrowsArgumentOutOfRangeException (string value)
        {
            using var control = new DateTimePicker ();
            var date = DateTime.Parse (value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = date);
        }

        [Fact]
        public void Value_SetOutsideMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new DateTimePicker { MinDate = new DateTime (2020, 1, 1) };
            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = new DateTime (2019, 12, 31));
        }

        [Fact]
        public void Value_SetOutsideMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new DateTimePicker { MaxDate = new DateTime (2020, 1, 1) };
            Assert.Throws<ArgumentOutOfRangeException> (() => control.Value = new DateTime (2020, 1, 2));
        }

        [Fact]
        public void MinDate_GetSet_ReturnsExpected ()
        {
            using var control = new DateTimePicker ();

            Assert.Equal (new DateTime (1753, 1, 1), control.MinDate);

            var expectedDate = DateTimePicker.MinimumDateTime.AddDays (1);
            control.MinDate = expectedDate;
            Assert.Equal (expectedDate, control.MinDate);

            // Set same.
            control.MinDate = expectedDate;
            Assert.Equal (expectedDate, control.MinDate);
        }

        [Fact]
        public void MinDate_SetGreaterThanMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new DateTimePicker { MaxDate = new DateTime (2020, 1, 1) };
            Assert.Throws<ArgumentOutOfRangeException> (() => control.MinDate = new DateTime (2020, 1, 2));
        }

        [Fact]
        public void MinDate_SetOutsideSupportedRange_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new DateTimePicker ();
            Assert.Throws<ArgumentOutOfRangeException> (() => control.MinDate = DateTimePicker.MinimumDateTime.AddDays (-1));
            Assert.Throws<ArgumentOutOfRangeException> (() => control.MinDate = DateTimePicker.MaximumDateTime.AddDays (1));
        }

        [Fact]
        public void MinDate_Set_AdjustsValueIfNeeded ()
        {
            using var control = new DateTimePicker ();

            control.Value = DateTimePicker.MinimumDateTime.AddDays (5);
            var newMinDate = DateTimePicker.MinimumDateTime.AddDays (10);
            control.MinDate = newMinDate;

            Assert.Equal (newMinDate, control.MinDate);
            Assert.Equal (newMinDate, control.Value);
        }

        [Fact]
        public void MaxDate_GetSet_ReturnsExpected ()
        {
            using var control = new DateTimePicker ();

            Assert.Equal (new DateTime (9998, 12, 31), control.MaxDate);

            var expectedDate = new DateTime (2022, 12, 31);
            control.MaxDate = expectedDate;
            Assert.Equal (expectedDate, control.MaxDate);

            // Set same.
            control.MaxDate = expectedDate;
            Assert.Equal (expectedDate, control.MaxDate);
        }

        [Fact]
        public void MaxDate_SetLessThanMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var control = new DateTimePicker { MinDate = new DateTime (2020, 1, 1) };
            Assert.Throws<ArgumentOutOfRangeException> (() => control.MaxDate = new DateTime (2019, 12, 31));
        }

        [Theory]
        [InlineData ("0001-01-01")]
        [InlineData ("9999-12-31")]
        public void MaxDate_SetOutsideSupportedRange_ThrowsArgumentOutOfRangeException (string value)
        {
            using var control = new DateTimePicker ();
            var date = DateTime.Parse (value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Throws<ArgumentOutOfRangeException> (() => control.MaxDate = date);
        }

        [Fact]
        public void MaxDate_Set_AdjustsValueIfNeeded ()
        {
            using var control = new DateTimePicker ();

            control.Value = new DateTime (2023, 6, 1);
            var newMaxDate = new DateTime (2022, 12, 31);
            control.MaxDate = newMaxDate;

            Assert.Equal (newMaxDate, control.MaxDate);
            Assert.Equal (newMaxDate, control.Value);
        }

        [Fact]
        public void CustomFormat_GetSet_AffectsText ()
        {
            using var control = new DateTimePicker ();

            control.Value = new DateTime (2021, 12, 31);
            control.Format = DateTimePickerFormat.Custom;

            // Escape literal separators so the rendered text is culture-independent (an
            // unescaped '/' is the culture date separator, not a literal slash).
            control.CustomFormat = "yyyy'/'MM'/'dd";
            Assert.Equal ("yyyy'/'MM'/'dd", control.CustomFormat);
            Assert.Equal ("2021/12/31", control.Text);

            control.CustomFormat = "MM'/'dd'/'yyyy";
            Assert.Equal ("MM'/'dd'/'yyyy", control.CustomFormat);
            Assert.Equal ("12/31/2021", control.Text);
        }

        [Fact]
        public void Format_Custom_RendersValueUsingCustomFormat ()
        {
            using var control = new DateTimePicker {
                CustomFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss"
            };

            control.Value = SampleDate;
            control.Format = DateTimePickerFormat.Custom;

            Assert.Equal ("2021-12-31 03:04:05", control.Text);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void Checked_GetSet_ReturnsExpected (bool showCheckBox)
        {
            using var control = new DateTimePicker { ShowCheckBox = showCheckBox };

            control.Checked = true;
            Assert.True (control.Checked);

            control.Checked = false;
            Assert.False (control.Checked);

            control.Checked = true;
            Assert.True (control.Checked);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowCheckBox_GetSet_ReturnsExpected (bool value)
        {
            using var control = new DateTimePicker { ShowCheckBox = value };
            Assert.Equal (value, control.ShowCheckBox);

            control.ShowCheckBox = !value;
            Assert.Equal (!value, control.ShowCheckBox);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowUpDown_GetSet_ReturnsExpected (bool value)
        {
            using var control = new DateTimePicker { ShowUpDown = value };
            Assert.Equal (value, control.ShowUpDown);

            control.ShowUpDown = !value;
            Assert.Equal (!value, control.ShowUpDown);
        }

        [Fact]
        public void ValueChanged_Event_Raised_OnChange ()
        {
            using var control = new DateTimePicker ();
            control.Value = new DateTime (2022, 1, 1);

            var callCount = 0;
            EventHandler handler = (sender, e) => {
                Assert.Same (control, sender);
                Assert.Equal (EventArgs.Empty, e);
                callCount++;
            };

            control.ValueChanged += handler;
            control.Value = new DateTime (2023, 1, 1);
            Assert.Equal (1, callCount);

            control.ValueChanged -= handler;
            control.Value = new DateTime (2024, 1, 1);
            Assert.Equal (1, callCount);
        }

        [Fact]
        public void ValueChanged_Event_NotRaised_WhenValueUnchanged ()
        {
            using var control = new DateTimePicker ();
            control.Value = new DateTime (2022, 1, 1);

            var callCount = 0;
            control.ValueChanged += (sender, e) => callCount++;

            // Setting the same value should not raise the event.
            control.Value = new DateTime (2022, 1, 1);
            Assert.Equal (0, callCount);
        }
    }
}
