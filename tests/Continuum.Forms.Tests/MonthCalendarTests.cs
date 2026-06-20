// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Adapted from the dotnet/winforms unit tests
// (src/test/unit/System.Windows.Forms/System/Windows/Forms/MonthCalendarTests.cs),
// rewritten for the Continuum.Forms API. Original work Copyright (c) .NET Foundation and Contributors.

using System;
using Xunit;

namespace Continuum.Forms.Tests
{
    // Behavioral tests ported from the upstream WinForms MonthCalendarTests, adapted to the
    // Continuum.Forms API (no Handle/CreateParams/accessibility plumbing). They pin the same
    // MinDate/MaxDate effective-range and coercion semantics, SelectionStart/SelectionEnd
    // range adjustment driven by MaxSelectionCount, the SetDate/SetSelectionRange validation
    // and clamping rules, and the TodayDate get/set/coercion behavior.
    public class MonthCalendarTests
    {
        private static readonly DateTime MinSupported = new DateTime (1753, 1, 1);
        private static readonly DateTime MaxSupported = new DateTime (9998, 12, 31);

        [Fact]
        public void Ctor_Default ()
        {
            using var calendar = new MonthCalendar ();

            Assert.Equal (Day.Default, calendar.FirstDayOfWeek);
            Assert.Equal (MinSupported, calendar.MinDate);
            Assert.Equal (MaxSupported, calendar.MaxDate);
            Assert.Equal (7, calendar.MaxSelectionCount);
            Assert.Equal (DateTime.Now.Date, calendar.SelectionStart);
            Assert.Equal (DateTime.Now.Date, calendar.SelectionEnd);
            Assert.Equal (DateTime.Now.Date, calendar.SelectionRange.Start);
            Assert.Equal (DateTime.Now.Date, calendar.SelectionRange.End);
            Assert.True (calendar.ShowToday);
            Assert.True (calendar.ShowTodayCircle);
            Assert.False (calendar.ShowWeekNumbers);
            Assert.Equal (DateTime.Now.Date, calendar.TodayDate);
            Assert.False (calendar.TodayDateSet);
        }

        // -- MaxDate --------------------------------------------------------

        public static TheoryData<DateTime, DateTime> MaxDate_Set_Data => new ()
        {
            { new DateTime (1753, 1, 1), new DateTime (1753, 1, 1) },
            { new DateTime (2019, 1, 29), new DateTime (2019, 1, 29) },
            { new DateTime (9998, 12, 31), new DateTime (9998, 12, 31) },
            { new DateTime (9999, 1, 1), new DateTime (9998, 12, 31) },
            { DateTime.MaxValue, new DateTime (9998, 12, 31) },
        };

        [Theory]
        [MemberData (nameof (MaxDate_Set_Data))]
        public void MaxDate_Set_GetReturnsExpected (DateTime value, DateTime expected)
        {
            using var calendar = new MonthCalendar { MaxDate = value };

            Assert.Equal (expected, calendar.MaxDate);

            // Set same.
            calendar.MaxDate = value;
            Assert.Equal (expected, calendar.MaxDate);
        }

        [Fact]
        public void MaxDate_SetClampsSelectionAboveMax_CoercesSelection ()
        {
            using var calendar = new MonthCalendar ();
            var max = new DateTime (1900, 1, 1);

            calendar.MaxDate = max;

            // Default selection (today) is above the new max, so it is pulled back.
            Assert.Equal (max, calendar.MaxDate);
            Assert.Equal (max, calendar.SelectionStart);
            Assert.Equal (max, calendar.SelectionEnd);
        }

        [Fact]
        public void MaxDate_SetLessThanMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.MaxDate = calendar.MinDate.AddTicks (-1));
        }

        // -- MinDate --------------------------------------------------------

        public static TheoryData<DateTime, DateTime> MinDate_Set_Data => new ()
        {
            { DateTime.MinValue, new DateTime (1753, 1, 1) },
            { new DateTime (1753, 1, 1), new DateTime (1753, 1, 1) },
            { new DateTime (2019, 1, 29), new DateTime (2019, 1, 29) },
            { new DateTime (9998, 12, 31), new DateTime (9998, 12, 31) },
        };

        [Theory]
        [MemberData (nameof (MinDate_Set_Data))]
        public void MinDate_Set_GetReturnsExpected (DateTime value, DateTime expected)
        {
            using var calendar = new MonthCalendar { MinDate = value };

            Assert.Equal (expected, calendar.MinDate);

            // Set same.
            calendar.MinDate = value;
            Assert.Equal (expected, calendar.MinDate);
        }

        [Fact]
        public void MinDate_SetClampsSelectionBelowMin_CoercesSelection ()
        {
            using var calendar = new MonthCalendar ();
            var min = new DateTime (9000, 1, 1);

            calendar.MinDate = min;

            // Default selection (today) is below the new min, so it is pushed forward.
            Assert.Equal (min, calendar.MinDate);
            Assert.Equal (min, calendar.SelectionStart);
            Assert.Equal (min, calendar.SelectionEnd);
        }

        [Fact]
        public void MinDate_SetGreaterThanMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.MinDate = calendar.MaxDate.AddTicks (1));
        }

        [Fact]
        public void MinDate_SetLessThanMinimum_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            // MinimumDateTime is 1753-01-01; anything below it throws.
            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.MinDate = MinSupported.AddTicks (-1));
        }

        // -- MaxSelectionCount ----------------------------------------------

        [Theory]
        [InlineData (1)]
        [InlineData (2)]
        [InlineData (7)]
        [InlineData (8)]
        [InlineData (int.MaxValue)]
        public void MaxSelectionCount_Set_GetReturnsExpected (int value)
        {
            using var calendar = new MonthCalendar { MaxSelectionCount = value };

            Assert.Equal (value, calendar.MaxSelectionCount);

            // Set same.
            calendar.MaxSelectionCount = value;
            Assert.Equal (value, calendar.MaxSelectionCount);
        }

        [Theory]
        [InlineData (0)]
        [InlineData (-1)]
        public void MaxSelectionCount_SetLessThanOne_ThrowsArgumentOutOfRangeException (int value)
        {
            using var calendar = new MonthCalendar ();
            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.MaxSelectionCount = value);
        }

        // -- FirstDayOfWeek -------------------------------------------------

        [Theory]
        [InlineData (Day.Monday)]
        [InlineData (Day.Saturday)]
        [InlineData (Day.Sunday)]
        [InlineData (Day.Default)]
        public void FirstDayOfWeek_Set_GetReturnsExpected (Day value)
        {
            using var calendar = new MonthCalendar { FirstDayOfWeek = value };

            Assert.Equal (value, calendar.FirstDayOfWeek);

            // Set same.
            calendar.FirstDayOfWeek = value;
            Assert.Equal (value, calendar.FirstDayOfWeek);
        }

        // -- SelectionStart -------------------------------------------------

        [Fact]
        public void SelectionStart_SetWithinDefaultRange_GetReturnsExpected ()
        {
            using var calendar = new MonthCalendar ();
            var value = new DateTime (2019, 1, 29);

            calendar.SelectionStart = value;

            Assert.Equal (value, calendar.SelectionStart);
            // SelectionEnd snaps forward to MaxSelectionCount-1 (6) days after start.
            Assert.Equal (value.AddDays (6), calendar.SelectionEnd);
        }

        [Fact]
        public void SelectionStart_SetBeyondSelectionEnd_PushesSelectionEndForward ()
        {
            using var calendar = new MonthCalendar { MaxSelectionCount = 1 };
            var value = new DateTime (2019, 1, 29);

            calendar.SelectionStart = value;

            Assert.Equal (value, calendar.SelectionStart);
            Assert.Equal (value, calendar.SelectionEnd);
        }

        [Fact]
        public void SelectionStart_SetLessThanExplicitMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MinDate = new DateTime (2019, 10, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SelectionStart = calendar.MinDate.AddTicks (-1));
        }

        [Fact]
        public void SelectionStart_SetGreaterThanExplicitMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MaxDate = new DateTime (2019, 9, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SelectionStart = calendar.MaxDate.AddTicks (1));
        }

        // -- SelectionEnd ---------------------------------------------------

        [Fact]
        public void SelectionEnd_SetBeforeSelectionStart_PullsSelectionStartBack ()
        {
            using var calendar = new MonthCalendar ();
            calendar.SetDate (new DateTime (2019, 1, 29));
            var value = new DateTime (2019, 1, 10);

            calendar.SelectionEnd = value;

            Assert.Equal (value, calendar.SelectionEnd);
            Assert.Equal (value, calendar.SelectionStart);
        }

        [Fact]
        public void SelectionEnd_SetBeyondMaxSelectionCount_PullsSelectionStartForward ()
        {
            using var calendar = new MonthCalendar ();
            calendar.SetDate (new DateTime (2019, 1, 1));
            var value = new DateTime (2019, 1, 31);

            calendar.SelectionEnd = value;

            Assert.Equal (value, calendar.SelectionEnd);
            // Start is pulled forward so the span equals MaxSelectionCount (7) days.
            Assert.Equal (value.AddDays (1 - 7), calendar.SelectionStart);
        }

        [Fact]
        public void SelectionEnd_SetLessThanExplicitMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MinDate = new DateTime (2019, 10, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SelectionEnd = calendar.MinDate.AddTicks (-1));
        }

        [Fact]
        public void SelectionEnd_SetGreaterThanExplicitMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MaxDate = new DateTime (2019, 9, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SelectionEnd = calendar.MaxDate.AddTicks (1));
        }

        // -- SelectionRange -------------------------------------------------

        [Fact]
        public void SelectionRange_Set_GetReturnsExpected ()
        {
            using var calendar = new MonthCalendar ();
            var start = new DateTime (2019, 1, 1);
            var end = new DateTime (2019, 1, 3);

            calendar.SelectionRange = new SelectionRange (start, end);

            Assert.Equal (start, calendar.SelectionStart);
            Assert.Equal (end, calendar.SelectionEnd);
            Assert.Equal (start, calendar.SelectionRange.Start);
            Assert.Equal (end, calendar.SelectionRange.End);
        }

        // -- SetDate --------------------------------------------------------

        [Fact]
        public void SetDate_Invoke_SetsBothEndpoints ()
        {
            using var calendar = new MonthCalendar ();
            var date = new DateTime (2019, 9, 1);

            calendar.SetDate (date);

            Assert.Equal (date, calendar.SelectionStart);
            Assert.Equal (date, calendar.SelectionEnd);
        }

        [Fact]
        public void SetDate_DateLessThanMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MinDate = new DateTime (2019, 10, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetDate (calendar.MinDate.AddTicks (-1)));
        }

        [Fact]
        public void SetDate_DateGreaterThanMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MaxDate = new DateTime (2019, 9, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetDate (calendar.MaxDate.AddDays (1)));
        }

        // -- SetSelectionRange ----------------------------------------------

        [Fact]
        public void SetSelectionRange_Invoke_SetsExpected ()
        {
            using var calendar = new MonthCalendar ();
            var start = new DateTime (2019, 1, 1);
            var end = new DateTime (2019, 1, 3);

            calendar.SetSelectionRange (start, end);

            Assert.Equal (start, calendar.SelectionStart);
            Assert.Equal (end, calendar.SelectionEnd);
        }

        [Fact]
        public void SetSelectionRange_Date1GreaterThanDate2_SelectsDate1 ()
        {
            using var calendar = new MonthCalendar ();
            var date1 = new DateTime (2019, 1, 3);
            var date2 = new DateTime (2019, 1, 1);

            // WinForms compat: when date1 > date2, the whole range collapses onto date1.
            calendar.SetSelectionRange (date1, date2);

            Assert.Equal (date1, calendar.SelectionStart);
            Assert.Equal (date1, calendar.SelectionEnd);
        }

        [Fact]
        public void SetSelectionRange_DateLessThanMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MinDate = new DateTime (2019, 10, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetSelectionRange (calendar.MinDate.AddTicks (-1), calendar.MinDate));
            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetSelectionRange (calendar.MinDate, calendar.MinDate.AddTicks (-1)));
        }

        [Fact]
        public void SetSelectionRange_DateGreaterThanMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MaxDate = new DateTime (2019, 9, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetSelectionRange (calendar.MaxDate.AddDays (1), calendar.MaxDate));
            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.SetSelectionRange (calendar.MaxDate, calendar.MaxDate.AddDays (1)));
        }

        // -- DateChanged event ----------------------------------------------

        [Fact]
        public void SetDate_RaisesDateChangedOnce ()
        {
            using var calendar = new MonthCalendar ();
            var date = new DateTime (2019, 9, 1);
            var callCount = 0;
            DateRangeEventArgs? args = null;

            calendar.DateChanged += (sender, e) => { callCount++; args = e; };

            calendar.SetDate (date);

            Assert.Equal (1, callCount);
            Assert.NotNull (args);
            Assert.Equal (date, args!.Start);
            Assert.Equal (date, args.End);

            // Setting the same range again does not raise the event.
            calendar.SetDate (date);
            Assert.Equal (1, callCount);
        }

        // -- TodayDate ------------------------------------------------------

        public static TheoryData<DateTime> TodayDate_Set_Data => new ()
        {
            new DateTime (1753, 1, 1),
            new DateTime (2019, 9, 1),
            new DateTime (2019, 9, 1).AddHours (1),
            new DateTime (9998, 12, 31),
        };

        [Theory]
        [MemberData (nameof (TodayDate_Set_Data))]
        public void TodayDate_Set_GetReturnsExpected (DateTime value)
        {
            using var calendar = new MonthCalendar { TodayDate = value };

            Assert.Equal (value.Date, calendar.TodayDate);
            Assert.True (calendar.TodayDateSet);

            // Set same.
            calendar.TodayDate = value;
            Assert.Equal (value.Date, calendar.TodayDate);
            Assert.True (calendar.TodayDateSet);
        }

        [Fact]
        public void TodayDate_SetWithinDefaultRange_DoesNotThrowEvenOutsideEffectiveBounds ()
        {
            // Validation is against the raw (unset) min/max fields, which default to
            // DateTime.MinValue / MaxValue, so any in-range-of-DateTime value is accepted
            // until MinDate/MaxDate are set explicitly.
            using var calendar = new MonthCalendar ();
            var value = MinSupported.AddTicks (-1);

            calendar.TodayDate = value;

            Assert.Equal (value.Date, calendar.TodayDate);
            Assert.True (calendar.TodayDateSet);
        }

        [Fact]
        public void TodayDate_SetLessThanExplicitMinDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MinDate = new DateTime (2019, 10, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.TodayDate = calendar.MinDate.AddTicks (-1));
        }

        [Fact]
        public void TodayDate_SetGreaterThanExplicitMaxDate_ThrowsArgumentOutOfRangeException ()
        {
            using var calendar = new MonthCalendar ();
            calendar.MaxDate = new DateTime (2019, 9, 3);

            Assert.Throws<ArgumentOutOfRangeException> (() => calendar.TodayDate = calendar.MaxDate.AddDays (1));
        }

        // -- Display bool properties ----------------------------------------

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowToday_Set_GetReturnsExpected (bool value)
        {
            using var calendar = new MonthCalendar { ShowToday = value };
            Assert.Equal (value, calendar.ShowToday);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowTodayCircle_Set_GetReturnsExpected (bool value)
        {
            using var calendar = new MonthCalendar { ShowTodayCircle = value };
            Assert.Equal (value, calendar.ShowTodayCircle);
        }

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void ShowWeekNumbers_Set_GetReturnsExpected (bool value)
        {
            using var calendar = new MonthCalendar { ShowWeekNumbers = value };
            Assert.Equal (value, calendar.ShowWeekNumbers);
        }
    }
}
