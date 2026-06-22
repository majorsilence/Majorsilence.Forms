using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a MonthCalendar control for selecting a date or range of dates.
    /// Stub in Majorsilence.Forms — renders as a simple label showing the selected date.
    /// </summary>
    public class MonthCalendar : Control
    {
        /// <summary>The minimum date a MonthCalendar can display (1753-01-01).</summary>
        public static readonly DateTime MinimumDateTime = new DateTime (1753, 1, 1);

        /// <summary>The maximum date a MonthCalendar can display (9998-12-31).</summary>
        public static readonly DateTime MaximumDateTime = new DateTime (9998, 12, 31);

        private const int DefaultMaxSelectionCount = 7;

        // Raw backing fields default to DateTime.MinValue / MaxValue so they can be
        // distinguished from an explicitly-set value; the public MinDate/MaxDate
        // getters clamp them to the effective 1753..9998 range (matching WinForms).
        private DateTime _minDate = DateTime.MinValue;
        private DateTime _maxDate = DateTime.MaxValue;
        private int _maxSelectionCount = DefaultMaxSelectionCount;

        private DateTime _selectionStart = DateTime.Now.Date;
        private DateTime _selectionEnd = DateTime.Now.Date;

        private static DateTime EffectiveMinDate (DateTime minDate) => minDate < MinimumDateTime ? MinimumDateTime : minDate;

        private static DateTime EffectiveMaxDate (DateTime maxDate) => maxDate > MaximumDateTime ? MaximumDateTime : maxDate;

        /// <summary>Gets or sets the start date of the selected range.</summary>
        public DateTime SelectionStart {
            get => _selectionStart;
            set {
                if (_selectionStart == value)
                    return;

                ArgumentOutOfRangeException.ThrowIfLessThan (value, _minDate);
                ArgumentOutOfRangeException.ThrowIfGreaterThan (value, _maxDate);

                // If we've moved SelectionStart beyond SelectionEnd, move SelectionEnd forward.
                if (_selectionEnd < value)
                    _selectionEnd = value;

                // If we've moved SelectionStart too far back from SelectionEnd, move SelectionEnd back.
                if ((_selectionEnd - value).Days >= _maxSelectionCount)
                    _selectionEnd = value.AddDays (_maxSelectionCount - 1);

                SetSelRange (value, _selectionEnd);
            }
        }

        /// <summary>Gets or sets the end date of the selected range.</summary>
        public DateTime SelectionEnd {
            get => _selectionEnd;
            set {
                if (_selectionEnd == value)
                    return;

                ArgumentOutOfRangeException.ThrowIfLessThan (value, _minDate);
                ArgumentOutOfRangeException.ThrowIfGreaterThan (value, _maxDate);

                // If we've moved SelectionEnd before SelectionStart, move SelectionStart back.
                if (_selectionStart > value)
                    _selectionStart = value;

                // If we've moved SelectionEnd too far beyond SelectionStart, move SelectionStart forward.
                if ((value - _selectionStart).Days >= _maxSelectionCount)
                    _selectionStart = value.AddDays (1 - _maxSelectionCount);

                SetSelRange (_selectionStart, value);
            }
        }

        /// <summary>Gets or sets the selected range of dates.</summary>
        public SelectionRange SelectionRange {
            get => new SelectionRange (_selectionStart, _selectionEnd);
            set => SetSelectionRange (value.Start, value.End);
        }

        /// <summary>Gets or sets the minimum selectable date.</summary>
        public DateTime MinDate {
            get => EffectiveMinDate (_minDate);
            set {
                if (value == _minDate)
                    return;

                ArgumentOutOfRangeException.ThrowIfGreaterThan (value, EffectiveMaxDate (_maxDate));
                ArgumentOutOfRangeException.ThrowIfLessThan (value, MinimumDateTime);

                _minDate = value;
                SetRange ();
            }
        }

        /// <summary>Gets or sets the maximum selectable date.</summary>
        public DateTime MaxDate {
            get => EffectiveMaxDate (_maxDate);
            set {
                if (value == _maxDate)
                    return;

                ArgumentOutOfRangeException.ThrowIfLessThan (value, EffectiveMinDate (_minDate));

                _maxDate = value;
                SetRange ();
            }
        }

        /// <summary>Gets or sets the number of months to display at once. Stub in Majorsilence.Forms.</summary>
        public Size CalendarDimensions { get; set; } = new Size (1, 1);

        /// <summary>Gets or sets the maximum number of days that can be selected.</summary>
        public int MaxSelectionCount {
            get => _maxSelectionCount;
            set {
                ArgumentOutOfRangeException.ThrowIfLessThan (value, 1);

                _maxSelectionCount = value;
            }
        }

        /// <summary>Gets or sets the first day of the week. Stub in Majorsilence.Forms.</summary>
        public Day FirstDayOfWeek { get; set; } = Day.Default;

        /// <summary>Gets or sets whether week numbers are shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowWeekNumbers { get; set; }

        /// <summary>Gets or sets whether today's date is circled. Stub in Majorsilence.Forms.</summary>
        public bool ShowToday { get; set; } = true;

        /// <summary>Gets or sets whether today's date is shown at the bottom. Stub in Majorsilence.Forms.</summary>
        public bool ShowTodayCircle { get; set; } = true;

        private DateTime _todaysDate = DateTime.Now.Date;
        private bool _todayDateSet;

        /// <summary>Gets or sets today's date.</summary>
        public DateTime TodayDate {
            get => _todayDateSet ? _todaysDate : DateTime.Now.Date;
            set {
                if (!_todayDateSet || DateTime.Compare (value, _todaysDate) != 0) {
                    ArgumentOutOfRangeException.ThrowIfGreaterThan (value, _maxDate);
                    ArgumentOutOfRangeException.ThrowIfLessThan (value, _minDate);

                    _todaysDate = value.Date;
                    _todayDateSet = true;
                    Invalidate ();
                }
            }
        }

        /// <summary>Gets whether TodayDate has been set explicitly.</summary>
        public bool TodayDateSet => _todayDateSet;

        /// <summary>Gets the array of bolded dates.</summary>
        public DateTime[] BoldedDates { get; set; } = Array.Empty<DateTime> ();

        /// <summary>Gets the array of annually bolded dates.</summary>
        public DateTime[] AnnuallyBoldedDates { get; set; } = Array.Empty<DateTime> ();

        /// <summary>Gets the array of monthly bolded dates.</summary>
        public DateTime[] MonthlyBoldedDates { get; set; } = Array.Empty<DateTime> ();

        /// <summary>Gets or sets the title foreground color. Stub in Majorsilence.Forms.</summary>
        public Color TitleForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the title background color. Stub in Majorsilence.Forms.</summary>
        public Color TitleBackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the trailing foreground color. Stub in Majorsilence.Forms.</summary>
        public Color TrailingForeColor { get; set; } = Color.Empty;

        /// <summary>Raised when the selected date changes.</summary>
        public event EventHandler<DateRangeEventArgs>? DateChanged;

        /// <summary>Raised when the user makes a selection.</summary>
        public event EventHandler<DateRangeEventArgs>? DateSelected { add { } remove { } }

        /// <summary>
        /// Sets <paramref name="date"/> as the current selected date. The start and end of
        /// the selection range will both be equal to date.
        /// </summary>
        public void SetDate (DateTime date)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (date, _minDate);
            ArgumentOutOfRangeException.ThrowIfGreaterThan (date, _maxDate);

            SetSelectionRange (date, date);
        }

        /// <summary>Sets the selection range to the given dates.</summary>
        public void SetSelectionRange (DateTime date1, DateTime date2)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan (date1, _minDate);
            ArgumentOutOfRangeException.ThrowIfGreaterThan (date1, _maxDate);
            ArgumentOutOfRangeException.ThrowIfLessThan (date2, _minDate);
            ArgumentOutOfRangeException.ThrowIfGreaterThan (date2, _maxDate);

            // If date1 > date2, we just select date2 (compat).
            if (date1 > date2)
                date2 = date1;

            // If the range exceeds maxSelectionCount, compare with the previous range and adjust
            // whichever limit hasn't changed.
            if ((date2 - date1).Days >= _maxSelectionCount) {
                if (date1.Ticks == _selectionStart.Ticks)
                    date1 = date2.AddDays (1 - _maxSelectionCount);
                else
                    date2 = date1.AddDays (_maxSelectionCount - 1);
            }

            SetSelRange (date1, date2);
        }

        // Keeps the current selection within the effective min/max range, then applies it.
        private void SetRange ()
        {
            var minDate = EffectiveMinDate (_minDate);
            var maxDate = EffectiveMaxDate (_maxDate);

            if (_selectionStart < minDate)
                _selectionStart = minDate;
            if (_selectionStart > maxDate)
                _selectionStart = maxDate;
            if (_selectionEnd < minDate)
                _selectionEnd = minDate;
            if (_selectionEnd > maxDate)
                _selectionEnd = maxDate;

            SetSelRange (_selectionStart, _selectionEnd);
        }

        // Applies a new selection range and raises DateChanged if it actually changed.
        private void SetSelRange (DateTime lower, DateTime upper)
        {
            var changed = false;

            if (_selectionStart != lower || _selectionEnd != upper) {
                changed = true;
                _selectionStart = lower;
                _selectionEnd = upper;
            }

            if (changed) {
                DateChanged?.Invoke (this, new DateRangeEventArgs (lower, upper));
                Invalidate ();
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            // Minimal rendering — display the selected date range as text
            var text = _selectionStart == _selectionEnd
                ? _selectionStart.ToShortDateString ()
                : $"{_selectionStart.ToShortDateString ()} – {_selectionEnd.ToShortDateString ()}";
            e.Canvas.DrawText (text, ClientRectangle, this, ContentAlignment.MiddleCenter);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (220, 162);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (Control.DefaultStyle);
    }

    /// <summary>Provides data for the MonthCalendar DateChanged and DateSelected events.</summary>
    public class DateRangeEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the DateRangeEventArgs class.</summary>
        public DateRangeEventArgs (DateTime start, DateTime end)
        {
            Start = start;
            End = end;
        }

        /// <summary>Gets the first date in the range.</summary>
        public DateTime Start { get; }

        /// <summary>Gets the last date in the range.</summary>
        public DateTime End { get; }
    }

    /// <summary>Represents a range of dates.</summary>
    public class SelectionRange
    {
        /// <summary>Initializes a new instance with the given start and end.</summary>
        public SelectionRange (DateTime start, DateTime end)
        {
            Start = start < end ? start : end;
            End = start < end ? end : start;
        }

        /// <summary>Gets or sets the start of the range.</summary>
        public DateTime Start { get; set; }

        /// <summary>Gets or sets the end of the range.</summary>
        public DateTime End { get; set; }
    }

    /// <summary>Specifies the day of the week.</summary>
    public enum Day
    {
        /// <summary>Monday.</summary>
        Monday,
        /// <summary>Tuesday.</summary>
        Tuesday,
        /// <summary>Wednesday.</summary>
        Wednesday,
        /// <summary>Thursday.</summary>
        Thursday,
        /// <summary>Friday.</summary>
        Friday,
        /// <summary>Saturday.</summary>
        Saturday,
        /// <summary>Sunday.</summary>
        Sunday,
        /// <summary>The default day (system setting).</summary>
        Default = 7
    }
}
