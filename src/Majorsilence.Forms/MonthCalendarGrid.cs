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
        internal DateTime FirstDisplayedCellDate => FirstCellDateOf (DisplayMonth);

        // The date in the first cell of the grid for the month starting on `first`.
        private DateTime FirstCellDateOf (DateTime first)
        {
            var offset = ((int)first.DayOfWeek - (int)FirstDayOfWeekAsDayOfWeek + DaysPerWeek) % DaysPerWeek;

            // A calendar showing January of the first supported year has no earlier days to pad
            // with; clamping here rather than letting AddDays throw keeps the grid drawable at the
            // very bottom of the range.
            return first.AddDays (-Math.Min (offset, (first - DateTime.MinValue).Days));
        }

        /// <summary>Gets the columns and rows of months the control lays out (SMP-46).</summary>
        internal Size MonthLayout
            => new Size (Math.Max (1, CalendarDimensions.Width), Math.Max (1, CalendarDimensions.Height));

        /// <summary>Gets the device-pixel bands of the first (top-left) month, with the control's
        /// next-month arrow, which sits at the right of the top row of months.</summary>
        /// <remarks>With one month, which is the default, this is the whole control's layout.</remarks>
        internal MonthCalendarGeometry Geometry {
            get {
                var first = GetMonthGeometry (0);
                var last_in_row = GetMonthGeometry (MonthLayout.Width - 1);

                return first.WithNextButton (last_in_row.NextButton);
            }
        }

        // Upstream's InsertWidthSize, InsertHeightSize and LogicalExtraPadding (MonthCalendar.cs): the
        // gap between two months across and down, and the "fudge factor" GetMinReqRect adds to both
        // dimensions of the whole control. Logical pixels.
        private const int MonthGapWidth = 6;
        private const int MonthGapHeight = 6;
        private const int ExtraPadding = 2;

        // A band -- the title, the day header, one week row, the "Today:" strip -- is a line of the
        // font plus 4, the constant upstream's GetMinReqRect gives the today string's height (from
        // comctl32's month calendar).
        private int BandHeight => TextMeasurer.LogicalLineHeight (this) + 4;

        // A day column is the widest of what goes in one -- a two-digit day, a day-of-week abbreviation,
        // a week number -- plus a four-pixel margin either side.
        private int DayColumnWidth {
            get {
                var widest = TextMeasurer.MeasureText ("00", this).Width;

                foreach (var name in System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames)
                    widest = Math.Max (widest, TextMeasurer.MeasureText (name, this).Width);

                return DeviceToLogicalUnits ((int)Math.Ceiling (widest)) + 8;
            }
        }

        /// <summary>One month in LOGICAL pixels at the current font: a title, a day header and six
        /// weeks of day columns (plus the week-number column), and the "Today:" strip when it is shown
        /// -- what upstream reads from <c>MCM_GETMINREQRECT</c>.</summary>
        internal Size MeasureSingleMonth ()
        {
            var band = BandHeight;
            var columns = ShowWeekNumbers ? DaysPerWeek + 1 : DaysPerWeek;
            var width = columns * DayColumnWidth;

            // The title has to fit between its two scroll arrows, each a band wide.
            var title = DeviceToLogicalUnits ((int)Math.Ceiling (TextMeasurer.MeasureText (TitleCaptionOf (new DateTime (2000, 9, 1)), this).Width));
            width = Math.Max (width, title + (4 * band));

            return new Size (width, ((2 + WeeksShown) * band) + (ShowToday ? band : 0));
        }

        /// <summary>The size the control needs for <paramref name="columns"/> by <paramref name="rows"/>
        /// months, in LOGICAL pixels: upstream's <c>GetMinReqRect</c>.</summary>
        /// <remarks>Each month is <see cref="SingleMonthSize"/>, the months are six pixels apart either
        /// way, the "Today:" strip runs once under them all, the width is never less than the today
        /// string needs, and two pixels of padding go on both dimensions.</remarks>
        internal Size MinimumSizeFor (int columns, int rows)
        {
            var single = MeasureSingleMonth ();
            var today = ShowToday ? BandHeight : 0;
            var calendar_height = single.Height - today;

            var width = ((single.Width + MonthGapWidth) * columns) - MonthGapWidth;
            var height = ((calendar_height + MonthGapHeight) * rows) - MonthGapHeight + today;

            if (ShowToday)
                width = Math.Max (width, DeviceToLogicalUnits ((int)Math.Ceiling (TextMeasurer.MeasureText (TodayCaption, this).Width)) + (2 * BandHeight));

            return new Size (width + ExtraPadding + ChromeSize.Width, height + ExtraPadding + ChromeSize.Height);
        }

        // The border, which the bounds include and the client area does not.
        private Size ChromeSize {
            get {
                var client = DeviceClientRectangle;

                return new Size (Math.Max (0, Width - DeviceToLogicalUnits (client.Width)),
                                 Math.Max (0, Height - DeviceToLogicalUnits (client.Height)));
            }
        }

        /// <summary>The text of the "Today:" strip.</summary>
        internal string TodayCaption => $"Today: {TodayDate.ToShortDateString ()}";

        // Upstream's AdjustSize: the control is exactly as big as its months need.
        private void AdjustSize ()
        {
            var size = MinimumSizeFor (MonthLayout.Width, MonthLayout.Height);

            adjusting_size = true;

            try {
                Size = size;
            } finally {
                adjusting_size = false;
            }

            Invalidate ();
        }

        // Set while AdjustSize applies the size the current dimensions need, which SetBoundsCore must
        // take as it is rather than read back into dimensions.
        private bool adjusting_size;

        /// <inheritdoc/>
        /// <remarks>
        /// As upstream's: a changed width or height is turned into the number of months that fit -- the
        /// columns from the width, the rows from the height -- and the control is sized to exactly that
        /// many months (<c>MonthCalendar.SetBoundsCore</c>, <c>GetPreferredWidth</c>,
        /// <c>GetPreferredHeight</c>). So making the control bigger shows more months, and it is never a
        /// size that would cut one off. The twelve-month cap holds, as the native control's does.
        /// </remarks>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (!adjusting_size) {
                var single = MeasureSingleMonth ();
                var today = ShowToday ? BandHeight : 0;
                var calendar_height = single.Height - today;
                var chrome = ChromeSize;
                var columns = MonthLayout.Width;
                var rows = MonthLayout.Height;

                if (width != Width)
                    columns = Math.Max (1, (width - ExtraPadding - chrome.Width) / Math.Max (1, single.Width));

                if (height != Height)
                    rows = Math.Max (1, (height - chrome.Height - today + MonthGapHeight) / Math.Max (1, calendar_height + MonthGapHeight));

                // The native control never shows more than twelve; the dimension that changed gives way.
                if (columns * rows > 12) {
                    if (columns != MonthLayout.Width)
                        columns = Math.Max (1, 12 / rows);
                    else
                        rows = Math.Max (1, 12 / columns);
                }

                calendar_dimensions = new Size (columns, rows);

                var size = MinimumSizeFor (columns, rows);

                if (width != Width)
                    width = size.Width;

                if (height != Height)
                    height = size.Height;
            }

            base.SetBoundsCore (x, y, width, height, specified);
        }

        /// <summary>Gets the device-pixel bands of the month at <paramref name="index"/> (row-major,
        /// from the top left) when <see cref="CalendarDimensions"/> shows several (SMP-46).</summary>
        /// <remarks>
        /// As upstream lays the months out: blocks of <see cref="SingleMonthSize"/> six pixels apart
        /// either way, inside a one-pixel margin, each with its own title, day-of-week header and six
        /// weeks; the scroll arrows only at the two ends of the top row, the today link once under them
        /// all (<c>MonthCalendar.cs</c>, <c>GetMinReqRect</c>, and the comctl32 month-calendar it hosts).
        /// The control is sized to fit its months (see <see cref="SetBoundsCore"/>), so the blocks are
        /// the client area divided by the dimensions; a control squeezed by its container shrinks the
        /// blocks rather than dropping months.
        /// </remarks>
        internal MonthCalendarGeometry GetMonthGeometry (int index)
        {
            var client = DeviceClientRectangle;
            var layout = MonthLayout;
            var month_column = index % layout.Width;
            var month_row = index / layout.Width;

            var margin = LogicalToDeviceUnits (ExtraPadding / 2);
            var inner = Rectangle.Inflate (client, -margin, -margin);
            var gap_width = LogicalToDeviceUnits (MonthGapWidth);
            var gap_height = LogicalToDeviceUnits (MonthGapHeight);
            var today_height = ShowToday ? LogicalToDeviceUnits (BandHeight) : 0;

            // Equal bands per month -- a title, a day header, six weeks -- so a cell's height is the same
            // whatever the month and ShowWeekNumbers.
            var block_width = Math.Max (1, (inner.Width - (gap_width * (layout.Width - 1))) / layout.Width);
            var months_height = Math.Max (1, inner.Height - today_height - (gap_height * (layout.Height - 1)));
            var cell_height = Math.Max (1, months_height / layout.Height / (2 + WeeksShown));
            var block_height = cell_height * (2 + WeeksShown);
            var columns = ShowWeekNumbers ? DaysPerWeek + 1 : DaysPerWeek;
            var cell_width = Math.Max (1, block_width / columns);

            var block_left = inner.Left + (month_column * (block_width + gap_width));
            var block_right = block_left + block_width;
            var block_top = inner.Top + (month_row * (block_height + gap_height));

            // An eighth of a month's width each, matching the arrow bands HitTest reported before
            // SMP-42, so the two scroll buttons stay where callers already found them.
            var arrow_width = Math.Max (1, block_width / 8);

            var header_top = block_top + cell_height;
            var grid_top = header_top + cell_height;
            var grid_height = cell_height * WeeksShown;
            var days_left = block_left + (ShowWeekNumbers ? cell_width : 0);
            var days_width = cell_width * DaysPerWeek;
            var months_bottom = inner.Top + (layout.Height * block_height) + ((layout.Height - 1) * gap_height);
            var month = AddMonths (DisplayMonth, index);

            return new MonthCalendarGeometry {
                Index = index,
                Month = month,
                FirstCellDate = FirstCellDateOf (month),

                // Only the first month shows the days before it and only the last the days after it:
                // between two months on screen those days are already drawn by the neighbour.
                ShowsLeadingDays = index == 0,
                ShowsTrailingDays = index == MonthsShown - 1,
                Block = new Rectangle (block_left, block_top, block_width, block_height),
                Title = new Rectangle (block_left, block_top, block_width, cell_height),
                ArrowWidth = arrow_width,
                PrevButton = index == 0
                    ? new Rectangle (block_left, block_top, arrow_width, cell_height)
                    : Rectangle.Empty,
                NextButton = month_row == 0 && month_column == layout.Width - 1
                    ? new Rectangle (block_right - arrow_width, block_top, arrow_width, cell_height)
                    : Rectangle.Empty,
                DayHeader = new Rectangle (days_left, header_top, days_width, cell_height),
                WeekNumberColumn = ShowWeekNumbers
                    ? new Rectangle (block_left, grid_top, cell_width, grid_height)
                    : Rectangle.Empty,
                Grid = new Rectangle (days_left, grid_top, days_width, grid_height),
                TodayBand = ShowToday
                    ? new Rectangle (inner.Left, months_bottom, inner.Width, Math.Max (0, inner.Bottom - months_bottom))
                    : Rectangle.Empty,
                CellWidth = cell_width,
                CellHeight = cell_height,
            };
        }

        /// <summary>Gets the device-pixel bounds of one grid cell of the first month.</summary>
        internal Rectangle GetCellBounds (int week, int column) => GetCellBounds (Geometry, week, column);

        /// <summary>Gets the device-pixel bounds of one grid cell of the given month.</summary>
        internal static Rectangle GetCellBounds (MonthCalendarGeometry geometry, int week, int column)
            => new Rectangle (geometry.Grid.Left + (column * geometry.CellWidth),
                              geometry.Grid.Top + (week * geometry.CellHeight),
                              geometry.CellWidth, geometry.CellHeight);

        /// <summary>Gets the device-pixel bounds of one day-of-week header cell of the first month.</summary>
        internal Rectangle GetDayHeaderBounds (int column) => GetDayHeaderBounds (Geometry, column);

        /// <summary>Gets the device-pixel bounds of one day-of-week header cell of the given month.</summary>
        internal static Rectangle GetDayHeaderBounds (MonthCalendarGeometry geometry, int column)
            => new Rectangle (geometry.DayHeader.Left + (column * geometry.CellWidth),
                              geometry.DayHeader.Top, geometry.CellWidth, geometry.CellHeight);

        /// <summary>Gets the device-pixel bounds of one week-number cell, or an empty rectangle when
        /// <see cref="ShowWeekNumbers"/> is false.</summary>
        internal Rectangle GetWeekNumberBounds (int week) => GetWeekNumberBounds (Geometry, week);

        /// <inheritdoc cref="GetWeekNumberBounds(int)"/>
        internal static Rectangle GetWeekNumberBounds (MonthCalendarGeometry geometry, int week)
        {
            if (geometry.WeekNumberColumn.IsEmpty)
                return Rectangle.Empty;

            return new Rectangle (geometry.WeekNumberColumn.Left,
                                  geometry.WeekNumberColumn.Top + (week * geometry.CellHeight),
                                  geometry.CellWidth, geometry.CellHeight);
        }

        /// <summary>Gets the device-pixel bounds of the cell <paramref name="date"/> is drawn in, or
        /// an empty rectangle when it is not on screen at all.</summary>
        /// <remarks>With several months, a date is in its own month's block; a leading or trailing day
        /// is only drawn in the first or last block (SMP-46).</remarks>
        internal Rectangle GetDateCellBounds (DateTime date)
        {
            for (var index = 0; index < MonthsShown; index++) {
                var geometry = GetMonthGeometry (index);
                var cell = (int)(date.Date - geometry.FirstCellDate).TotalDays;

                if (cell >= 0 && cell < WeeksShown * DaysPerWeek && IsDrawn (geometry, date))
                    return GetCellBounds (geometry, cell / DaysPerWeek, cell % DaysPerWeek);
            }

            return Rectangle.Empty;
        }

        /// <summary>Gets whether a block draws <paramref name="date"/>: always for a day of its own
        /// month, and for an adjacent month's day only at the ends of the run of months.</summary>
        internal static bool IsDrawn (MonthCalendarGeometry geometry, DateTime date)
        {
            var month = geometry.Month;

            if (date.Year == month.Year && date.Month == month.Month)
                return true;

            return date < month ? geometry.ShowsLeadingDays : geometry.ShowsTrailingDays;
        }

        /// <summary>Gets the date drawn at grid position (<paramref name="week"/>,
        /// <paramref name="column"/>) of the first month.</summary>
        internal DateTime GetDateAt (int week, int column)
            => FirstDisplayedCellDate.AddDays ((week * DaysPerWeek) + column);

        /// <summary>Gets the date at a grid position of the given month.</summary>
        internal static DateTime GetDateAt (MonthCalendarGeometry geometry, int week, int column)
            => geometry.FirstCellDate.AddDays ((week * DaysPerWeek) + column);

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
        /// <summary>Which month this is, counting row-major from the top left (SMP-46).</summary>
        internal int Index { get; init; }

        /// <summary>The first day of the month drawn in this block.</summary>
        internal DateTime Month { get; init; }

        /// <summary>The date in this block's first grid cell.</summary>
        internal DateTime FirstCellDate { get; init; }

        /// <summary>Whether this block draws the previous month's days that pad its first week.</summary>
        internal bool ShowsLeadingDays { get; init; }

        /// <summary>Whether this block draws the next month's days that pad its last weeks.</summary>
        internal bool ShowsTrailingDays { get; init; }

        /// <summary>The whole block this month occupies: title, day header and grid.</summary>
        internal Rectangle Block { get; init; }

        /// <summary>The width of a scroll-arrow band, which the caption is inset by on both sides.</summary>
        internal int ArrowWidth { get; init; }

        /// <summary>Returns a copy whose next-month arrow is <paramref name="next"/>.</summary>
        internal MonthCalendarGeometry WithNextButton (Rectangle next) => this with { NextButton = next };

        /// <summary>The whole title band, scroll arrows included.</summary>
        internal Rectangle Title { get; init; }

        /// <summary>The previous-month arrow, at the left of the first month's title band; empty on
        /// every other month.</summary>
        internal Rectangle PrevButton { get; init; }

        /// <summary>The next-month arrow, at the right of the last title band of the top row of months;
        /// empty on every other month.</summary>
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
