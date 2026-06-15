using System;
using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a MonthCalendar control for selecting a date or range of dates.
    /// Stub in Modern.Forms — renders as a simple label showing the selected date.
    /// </summary>
    public class MonthCalendar : Control
    {
        private DateTime _selectionStart = DateTime.Today;
        private DateTime _selectionEnd = DateTime.Today;

        /// <summary>Gets or sets the start date of the selected range.</summary>
        public DateTime SelectionStart {
            get => _selectionStart;
            set {
                _selectionStart = value;
                if (_selectionEnd < value) _selectionEnd = value;
                DateChanged?.Invoke (this, new DateRangeEventArgs (_selectionStart, _selectionEnd));
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the end date of the selected range.</summary>
        public DateTime SelectionEnd {
            get => _selectionEnd;
            set {
                _selectionEnd = value;
                if (_selectionStart > value) _selectionStart = value;
                DateChanged?.Invoke (this, new DateRangeEventArgs (_selectionStart, _selectionEnd));
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the selected range of dates.</summary>
        public SelectionRange SelectionRange {
            get => new SelectionRange (_selectionStart, _selectionEnd);
            set {
                _selectionStart = value.Start;
                _selectionEnd = value.End;
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the minimum selectable date.</summary>
        public DateTime MinDate { get; set; } = new DateTime (1753, 1, 1);

        /// <summary>Gets or sets the maximum selectable date.</summary>
        public DateTime MaxDate { get; set; } = new DateTime (9998, 12, 31);

        /// <summary>Gets or sets the number of months to display at once. Stub in Modern.Forms.</summary>
        public Size CalendarDimensions { get; set; } = new Size (1, 1);

        /// <summary>Gets or sets the maximum number of days that can be selected. Stub in Modern.Forms.</summary>
        public int MaxSelectionCount { get; set; } = 7;

        /// <summary>Gets or sets the first day of the week. Stub in Modern.Forms.</summary>
        public Day FirstDayOfWeek { get; set; } = Day.Default;

        /// <summary>Gets or sets whether week numbers are shown. Stub in Modern.Forms.</summary>
        public bool ShowWeekNumbers { get; set; }

        /// <summary>Gets or sets whether today's date is circled. Stub in Modern.Forms.</summary>
        public bool ShowToday { get; set; } = true;

        /// <summary>Gets or sets whether today's date is shown at the bottom. Stub in Modern.Forms.</summary>
        public bool ShowTodayCircle { get; set; } = true;

        /// <summary>Gets or sets today's date.</summary>
        public DateTime TodayDate { get; set; } = DateTime.Today;

        /// <summary>Gets or sets whether TodayDate has been set explicitly.</summary>
        public bool TodayDateSet { get; set; }

        /// <summary>Gets the array of bolded dates.</summary>
        public DateTime[] BoldedDates { get; set; } = Array.Empty<DateTime> ();

        /// <summary>Gets the array of annually bolded dates.</summary>
        public DateTime[] AnnuallyBoldedDates { get; set; } = Array.Empty<DateTime> ();

        /// <summary>Gets the array of monthly bolded dates.</summary>
        public DateTime[] MonthlyBoldedDates { get; set; } = Array.Empty<DateTime> ();

        /// <summary>Gets or sets the title foreground color. Stub in Modern.Forms.</summary>
        public Color TitleForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the title background color. Stub in Modern.Forms.</summary>
        public Color TitleBackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the trailing foreground color. Stub in Modern.Forms.</summary>
        public Color TrailingForeColor { get; set; } = Color.Empty;

        /// <summary>Raised when the selected date changes.</summary>
        public event EventHandler<DateRangeEventArgs>? DateChanged;

        /// <summary>Raised when the user makes a selection.</summary>
        public event EventHandler<DateRangeEventArgs>? DateSelected { add { } remove { } }

        /// <summary>Sets the selection range.</summary>
        public void SetDate (DateTime date)
        {
            _selectionStart = date;
            _selectionEnd = date;
            Invalidate ();
        }

        /// <summary>Sets the selection range.</summary>
        public void SetSelectionRange (DateTime lower, DateTime upper)
        {
            _selectionStart = lower;
            _selectionEnd = upper;
            Invalidate ();
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
