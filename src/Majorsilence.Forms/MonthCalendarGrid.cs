using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The grid half of MonthCalendar: layout, hit-testing, mouse selection and keyboard navigation
    // (W5.20c, finding SMP-42, P0).
    //
    // Before this, MonthCalendar's OnPaint drew one centred ToShortDateString() in an otherwise empty
    // 220x162 box, there was no MonthCalendarRenderer, and the class had no OnMouseDown/OnKeyDown at
    // all -- so the control that exists to pick a date could not be used to pick one, and DateSelected
    // was declared `add { } remove { }` so a handler was discarded on subscription.
    //
    // One geometry, three consumers: MonthCalendarRenderer draws it, HitTest maps points onto it, and
    // the mouse handlers select through HitTest. That is what makes the promise in the
    // MidSizeControlParity.cs file header -- "HitTest and GetDisplayRange are computed from the same
    // geometry the renderer lays the control out with" -- true rather than aspirational.
    //
    // Coordinate spaces: the geometry below is in DEVICE pixels, because it is built from
    // ClientRectangle (which is scaled) and handed straight to the paint canvas. MouseEventArgs and
    // HitTest's argument are LOGICAL, like Bounds, so both convert at the boundary -- the same split
    // ListBox.GetIndexAtLocation and TreeView.GetItemAtLocation document. Comparing the two directly
    // picks the cell at index/scale, which on a HiDPI display selects the wrong date.
    public partial class MonthCalendar
    {
        private const int DaysPerWeek = 7;

        // Six, always: a month can span six week rows (a 31-day month starting on the last day of the
        // week), and a fixed row count keeps the grid geometry -- and therefore every cell's position
        // -- independent of which month is showing, which is what stops the control resizing its own
        // rows as the user pages through the year.
        private const int WeeksShown = 6;

        // Null means "follow the selection". Navigation pins it, and SetSelRange re-pins it whenever
        // the selection moves out of view, so a programmatic SetDate still scrolls the calendar to the
        // date it just selected.
        private DateTime? display_month;

        // The fixed end of a drag or a shift-extended keyboard selection. Null until the first
        // selection is made, when it means "wherever SelectionStart is".
        private DateTime? selection_anchor;

        private bool dragging;

        /// <summary>Gets the first day of the month currently drawn at the top left.</summary>
        internal DateTime DisplayMonth
            => display_month ?? new DateTime (_selectionStart.Year, _selectionStart.Month, 1);

        /// <summary>Gets how many months <see cref="CalendarDimensions"/> asks for.</summary>
        internal int MonthsShown
            => Math.Max (1, CalendarDimensions.Width) * Math.Max (1, CalendarDimensions.Height);

        /// <summary>Gets the date drawn in the very first grid cell, which is on or before the first
        /// of <see cref="DisplayMonth"/> depending on <see cref="FirstDayOfWeek"/>.</summary>
        internal DateTime FirstDisplayedCellDate {
            get {
                var first = DisplayMonth;
                var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeekAsDayOfWeek + DaysPerWeek) % DaysPerWeek;

                // A calendar showing January of the first supported year has no earlier days to pad
                // with; clamping here rather than letting AddDays throw keeps the grid drawable at the
                // very bottom of the range.
                return first.AddDays (-Math.Min (offset, (first - DateTime.MinValue).Days));
            }
        }

        /// <summary>Gets the device-pixel bands the control is laid out in.</summary>
        internal MonthCalendarGeometry Geometry {
            get {
                var client = ClientRectangle;

                // One band each for the title and the day-of-week header, six for the week rows, and
                // -- when ShowToday -- one more for the "Today:" strip at the foot. Equal bands is
                // what makes a cell's height independent of ShowWeekNumbers and of the month.
                var bands = 2 + WeeksShown + (ShowToday ? 1 : 0);
                var cell_height = Math.Max (1, client.Height / bands);
                var columns = ShowWeekNumbers ? DaysPerWeek + 1 : DaysPerWeek;
                var cell_width = Math.Max (1, client.Width / columns);

                // An eighth of the width each, matching the arrow bands HitTest reported before this
                // slice, so the two scroll buttons stay where callers already found them.
                var arrow_width = Math.Max (1, client.Width / 8);

                var header_top = client.Top + cell_height;
                var grid_top = header_top + cell_height;
                var grid_height = cell_height * WeeksShown;
                var days_left = client.Left + (ShowWeekNumbers ? cell_width : 0);
                var days_width = cell_width * DaysPerWeek;

                return new MonthCalendarGeometry {
                    Title = new Rectangle (client.Left, client.Top, client.Width, cell_height),
                    PrevButton = new Rectangle (client.Left, client.Top, arrow_width, cell_height),
                    NextButton = new Rectangle (client.Right - arrow_width, client.Top, arrow_width, cell_height),
                    DayHeader = new Rectangle (days_left, header_top, days_width, cell_height),
                    WeekNumberColumn = ShowWeekNumbers
                        ? new Rectangle (client.Left, grid_top, cell_width, grid_height)
                        : Rectangle.Empty,
                    Grid = new Rectangle (days_left, grid_top, days_width, grid_height),
                    TodayBand = ShowToday
                        ? new Rectangle (client.Left, grid_top + grid_height, client.Width,
                                         Math.Max (0, client.Bottom - (grid_top + grid_height)))
                        : Rectangle.Empty,
                    CellWidth = cell_width,
                    CellHeight = cell_height,
                };
            }
        }

        /// <summary>Gets the device-pixel bounds of one grid cell.</summary>
        internal Rectangle GetCellBounds (int week, int column)
        {
            var geometry = Geometry;

            return new Rectangle (geometry.Grid.Left + (column * geometry.CellWidth),
                                  geometry.Grid.Top + (week * geometry.CellHeight),
                                  geometry.CellWidth, geometry.CellHeight);
        }

        /// <summary>Gets the device-pixel bounds of one day-of-week header cell.</summary>
        internal Rectangle GetDayHeaderBounds (int column)
        {
            var geometry = Geometry;

            return new Rectangle (geometry.DayHeader.Left + (column * geometry.CellWidth),
                                  geometry.DayHeader.Top, geometry.CellWidth, geometry.CellHeight);
        }

        /// <summary>Gets the device-pixel bounds of one week-number cell, or an empty rectangle when
        /// <see cref="ShowWeekNumbers"/> is false.</summary>
        internal Rectangle GetWeekNumberBounds (int week)
        {
            var geometry = Geometry;

            if (geometry.WeekNumberColumn.IsEmpty)
                return Rectangle.Empty;

            return new Rectangle (geometry.WeekNumberColumn.Left,
                                  geometry.WeekNumberColumn.Top + (week * geometry.CellHeight),
                                  geometry.CellWidth, geometry.CellHeight);
        }

        /// <summary>Gets the device-pixel bounds of the cell <paramref name="date"/> is drawn in, or
        /// an empty rectangle when it is not on the grid at all.</summary>
        internal Rectangle GetDateCellBounds (DateTime date)
        {
            var index = (int)(date.Date - FirstDisplayedCellDate).TotalDays;

            return index < 0 || index >= WeeksShown * DaysPerWeek
                ? Rectangle.Empty
                : GetCellBounds (index / DaysPerWeek, index % DaysPerWeek);
        }

        /// <summary>Gets the date drawn at grid position (<paramref name="week"/>,
        /// <paramref name="column"/>).</summary>
        internal DateTime GetDateAt (int week, int column)
            => FirstDisplayedCellDate.AddDays ((week * DaysPerWeek) + column);

        /// <summary>Moves the displayed month(s) back or forward without changing the selection.</summary>
        /// <param name="direction">-1 for the previous screen of months, 1 for the next.</param>
        internal void ScrollDisplayedMonths (int direction)
        {
            // WinForms treats ScrollChange == 0 as "one screenful", which is why it is the default.
            var step = ScrollChange > 0 ? ScrollChange : MonthsShown;
            var target = AddMonths (DisplayMonth, direction * step);
            var floor = FirstOfMonth (MinDate);
            var ceiling = FirstOfMonth (MaxDate);

            if (target < floor)
                target = floor;
            if (target > ceiling)
                target = ceiling;

            if (target == DisplayMonth)
                return;

            display_month = target;
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!Enabled)
                return;

            var hit = HitTest (e.Location);

            switch (hit.HitArea) {
                case HitArea.PrevMonthButton:
                    ScrollDisplayedMonths (-1);
                    break;

                case HitArea.NextMonthButton:
                    ScrollDisplayedMonths (1);
                    break;

                case HitArea.TodayLink:
                    SelectAndCommit (TodayDate, TodayDate);
                    break;

                case HitArea.WeekNumbers:
                    // Clicking a week number selects that whole week, as the native control does. It
                    // is clamped by MaxSelectionCount like any other range.
                    SelectAndCommit (hit.Time, hit.Time.AddDays (DaysPerWeek - 1));
                    break;

                case HitArea.Date:
                case HitArea.PrevMonthDate:
                case HitArea.NextMonthDate:
                    // The press only anchors and previews; DateSelected waits for the release, so a
                    // drag across a week raises DateChanged per day and DateSelected exactly once.
                    dragging = true;
                    selection_anchor = hit.Time;
                    ApplyRange (hit.Time, hit.Time);
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (!dragging)
                return;

            var hit = HitTest (e.Location);

            if (hit.HitArea is not (HitArea.Date or HitArea.PrevMonthDate or HitArea.NextMonthDate))
                return;

            var anchor = selection_anchor ?? hit.Time;
            var lower = hit.Time < anchor ? hit.Time : anchor;
            var upper = hit.Time < anchor ? anchor : hit.Time;

            // MaxSelectionCount trims the END the pointer is dragging, never the anchor. Routing this
            // through SetSelectionRange instead would move the anchor, because that method adjusts
            // "whichever limit hasn't changed" -- correct for a programmatic call, wrong for a drag,
            // where the fixed end is the one the user pressed on.
            if ((upper - lower).Days >= MaxSelectionCount) {
                if (hit.Time > anchor)
                    upper = anchor.AddDays (MaxSelectionCount - 1);
                else
                    lower = anchor.AddDays (1 - MaxSelectionCount);
            }

            ApplyRange (lower, upper);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            var was_dragging = dragging;
            dragging = false;

            base.OnMouseUp (e);

            if (was_dragging)
                OnDateSelected (new DateRangeEventArgs (_selectionStart, _selectionEnd));
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            var target = KeyboardTarget (e);

            if (target is null) {
                base.OnKeyDown (e);
                return;
            }

            var date = Clamp (target.Value);

            if (e.Shift) {
                var anchor = Clamp (selection_anchor ?? _selectionStart);
                var lower = date < anchor ? date : anchor;
                var upper = date < anchor ? anchor : date;

                selection_anchor = anchor;

                if ((upper - lower).Days >= MaxSelectionCount) {
                    if (date > anchor)
                        upper = anchor.AddDays (MaxSelectionCount - 1);
                    else
                        lower = anchor.AddDays (1 - MaxSelectionCount);
                }

                ApplyRange (lower, upper);
            } else {
                selection_anchor = date;
                ApplyRange (date, date);
            }

            // Keyboard navigation has to be able to leave the displayed month -- pressing Right on the
            // 31st is the ordinary way to reach the 1st of the next one -- so the view follows the key,
            // not the other way round.
            ScrollInto (date);

            // The native control sends MCN_SELECT for a keyboard commit as well as a mouse one, so a
            // handler that only listens to DateSelected still sees an arrow-key change.
            OnDateSelected (new DateRangeEventArgs (_selectionStart, _selectionEnd));

            e.Handled = true;
            base.OnKeyDown (e);
        }

        // Null for a key this control does not navigate with, so OnKeyDown can leave it to the base.
        private DateTime? KeyboardTarget (KeyEventArgs e)
        {
            // Shift extends from the anchor, so it moves the free end (SelectionEnd); an unmodified key
            // collapses the range and moves from its start.
            var from = e.Shift ? _selectionEnd : _selectionStart;

            switch (e.KeyCode) {
                case Keys.Left:
                    return from.AddDays (-1);
                case Keys.Right:
                    return from.AddDays (1);
                case Keys.Up:
                    return from.AddDays (-DaysPerWeek);
                case Keys.Down:
                    return from.AddDays (DaysPerWeek);
                case Keys.PageUp:
                    return AddMonthsKeepingDay (from, -1);
                case Keys.PageDown:
                    return AddMonthsKeepingDay (from, 1);
                case Keys.Home:
                    return new DateTime (from.Year, from.Month, 1);
                case Keys.End:
                    return new DateTime (from.Year, from.Month, DateTime.DaysInMonth (from.Year, from.Month));
                default:
                    return null;
            }
        }

        // Applies a range without announcing a commit: DateChanged fires (through SetSelRange),
        // DateSelected does not.
        private void ApplyRange (DateTime lower, DateTime upper)
        {
            lower = Clamp (lower);
            upper = Clamp (upper);

            if (upper < lower)
                upper = lower;

            SetSelRange (lower, upper);
        }

        private void SelectAndCommit (DateTime lower, DateTime upper)
        {
            ApplyRange (lower, upper);
            OnDateSelected (new DateRangeEventArgs (_selectionStart, _selectionEnd));
        }

        // The effective getters, not the raw fields: a user gesture must never land the selection
        // somewhere the grid cannot draw it.
        private DateTime Clamp (DateTime date)
            => date < MinDate ? MinDate : date > MaxDate ? MaxDate : date;

        private void ScrollInto (DateTime date)
        {
            var first = FirstOfMonth (date);

            if (first >= DisplayMonth && first < AddMonths (DisplayMonth, MonthsShown))
                return;

            display_month = first;
            Invalidate ();
        }

        private static DateTime FirstOfMonth (DateTime date) => new DateTime (date.Year, date.Month, 1);

        // Month arithmetic that saturates instead of throwing at the ends of DateTime's range, so
        // paging forward from December 9999 is a no-op rather than an exception in a paint path.
        private static DateTime AddMonths (DateTime date, int months)
        {
            // Math.Clamp is unavailable on the netstandard2.0 leg of this project.
            var total = Math.Min (Math.Max ((date.Year * 12) + (date.Month - 1) + months, 12), (9999 * 12) + 11);

            return new DateTime (total / 12, (total % 12) + 1, 1);
        }

        // PageUp/PageDown keep the day of the month where the target month has one, and fall back to
        // its last day where it does not (31 January + 1 month is 28 February, as upstream).
        private static DateTime AddMonthsKeepingDay (DateTime date, int months)
        {
            var month = AddMonths (date, months);

            return month.AddDays (Math.Min (date.Day, DateTime.DaysInMonth (month.Year, month.Month)) - 1);
        }
    }

    /// <summary>The device-pixel bands a <see cref="MonthCalendar"/> is laid out in.</summary>
    /// <remarks>One structure read by the renderer, by <see cref="MonthCalendar.HitTest(Point)"/> and
    /// by the mouse handlers, so what is drawn and what is clickable cannot drift apart (SMP-42).</remarks>
    internal readonly struct MonthCalendarGeometry
    {
        /// <summary>The whole title band, scroll arrows included.</summary>
        internal Rectangle Title { get; init; }

        /// <summary>The previous-month arrow, at the left of the title band.</summary>
        internal Rectangle PrevButton { get; init; }

        /// <summary>The next-month arrow, at the right of the title band.</summary>
        internal Rectangle NextButton { get; init; }

        /// <summary>The row of day-of-week abbreviations.</summary>
        internal Rectangle DayHeader { get; init; }

        /// <summary>The week-number column, empty unless <see cref="MonthCalendar.ShowWeekNumbers"/>.</summary>
        internal Rectangle WeekNumberColumn { get; init; }

        /// <summary>The six-by-seven block of day cells.</summary>
        internal Rectangle Grid { get; init; }

        /// <summary>The "Today:" strip, empty unless <see cref="MonthCalendar.ShowToday"/>.</summary>
        internal Rectangle TodayBand { get; init; }

        /// <summary>The width of one day cell.</summary>
        internal int CellWidth { get; init; }

        /// <summary>The height of one band, and of one day cell.</summary>
        internal int CellHeight { get; init; }
    }
}
