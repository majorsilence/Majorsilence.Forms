using System.Drawing;
using System.Globalization;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Renders a <see cref="MonthCalendar"/>: a title band with scroll arrows, a day-of-week header,
    /// six week rows of day cells, an optional week-number column and an optional "Today" strip.
    /// </summary>
    /// <remarks>
    /// Added for W5.20c (finding SMP-42, P0). There was no renderer registered for
    /// <c>MonthCalendar</c> at all, so <c>RenderManager</c> walked up to <c>Control</c>, found
    /// nothing, and the control's own <c>OnPaint</c> drew one centred <c>ToShortDateString()</c> in an
    /// empty box. Every geometry decision here comes from <c>MonthCalendar.Geometry</c>, which
    /// <c>HitTest</c> and the mouse handlers read too, so a cell is clickable exactly where it is
    /// drawn.
    /// <para>
    /// <c>CalendarDimensions</c> greater than 1x1 is deliberately not drawn yet: one month is painted
    /// across the whole client area whatever the dimensions say. See the W5.20c entry in
    /// <c>docs/behaviour-gap-plan.md</c>.
    /// </para>
    /// </remarks>
    public class MonthCalendarRenderer : Renderer<MonthCalendar>
    {
        /// <inheritdoc/>
        protected override void Render (MonthCalendar control, PaintEventArgs e)
        {
            var geometry = control.Geometry;
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());
            var foreground = control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;

            RenderTitle (control, e, geometry, font, font_size);
            RenderDayHeader (control, e, geometry, font, font_size, foreground);
            RenderWeekNumbers (control, e, geometry, font, font_size);
            RenderDays (control, e, geometry, font, font_size, foreground);
            RenderTodayBand (control, e, geometry, font, font_size, foreground);
        }

        private static void RenderTitle (MonthCalendar control, PaintEventArgs e, MonthCalendarGeometry geometry, SKTypeface font, int fontSize)
        {
            e.Canvas.FillRectangle (geometry.Title, Resolve (control.TitleBackColor, Theme.ControlHighlightLowColor));

            var caption = control.DisplayMonth.ToString ("MMMM yyyy", CultureInfo.CurrentCulture);
            var text_area = new Rectangle (geometry.PrevButton.Right, geometry.Title.Top,
                                           System.Math.Max (0, geometry.NextButton.Left - geometry.PrevButton.Right),
                                           geometry.Title.Height);

            e.Canvas.DrawText (caption, font, fontSize, text_area,
                               Resolve (control.TitleForeColor, Theme.ForegroundColorOnAccent),
                               ContentAlignment.MiddleCenter, maxLines: 1);

            var arrow_colour = Resolve (control.TitleForeColor, Theme.ForegroundColorOnAccent);

            ControlPaint.DrawArrowGlyph (e, Glyph (geometry.PrevButton, e), arrow_colour, ArrowDirection.Left);
            ControlPaint.DrawArrowGlyph (e, Glyph (geometry.NextButton, e), arrow_colour, ArrowDirection.Right);
        }

        private static void RenderDayHeader (MonthCalendar control, PaintEventArgs e, MonthCalendarGeometry geometry, SKTypeface font, int fontSize, SKColor foreground)
        {
            var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
            var first = (int)control.FirstDayOfWeekAsDayOfWeek;

            // FirstDayOfWeek rotates the header and the grid together: the offset used here is the one
            // MonthCalendar.FirstDisplayedCellDate pads the grid with, so column n always carries the
            // same weekday in both.
            for (var column = 0; column < 7; column++)
                e.Canvas.DrawText (names[(first + column) % 7], font, fontSize, control.GetDayHeaderBounds (column),
                                   foreground, ContentAlignment.MiddleCenter, maxLines: 1);

            e.Canvas.DrawLine (geometry.DayHeader.Left, geometry.DayHeader.Bottom,
                               geometry.DayHeader.Right, geometry.DayHeader.Bottom, Theme.BorderLowColor);
        }

        private static void RenderWeekNumbers (MonthCalendar control, PaintEventArgs e, MonthCalendarGeometry geometry, SKTypeface font, int fontSize)
        {
            if (geometry.WeekNumberColumn.IsEmpty)
                return;

            var calendar = CultureInfo.CurrentCulture.Calendar;

            for (var week = 0; week < 6; week++) {
                var number = calendar.GetWeekOfYear (control.GetDateAt (week, 0),
                                                     CalendarWeekRule.FirstFourDayWeek,
                                                     control.FirstDayOfWeekAsDayOfWeek);

                e.Canvas.DrawText (number.ToString (CultureInfo.CurrentCulture), font, fontSize,
                                   control.GetWeekNumberBounds (week), Theme.ForegroundDisabledColor,
                                   ContentAlignment.MiddleCenter, maxLines: 1);
            }

            e.Canvas.DrawLine (geometry.WeekNumberColumn.Right, geometry.WeekNumberColumn.Top,
                               geometry.WeekNumberColumn.Right, geometry.WeekNumberColumn.Bottom, Theme.BorderLowColor);
        }

        private static void RenderDays (MonthCalendar control, PaintEventArgs e, MonthCalendarGeometry geometry, SKTypeface font, int fontSize, SKColor foreground)
        {
            var displayed = control.DisplayMonth;
            var trailing = Resolve (control.TrailingForeColor, Theme.ForegroundDisabledColor);
            var selection_start = control.SelectionStart.Date;
            var selection_end = control.SelectionEnd.Date;
            var today = control.TodayDate.Date;

            for (var week = 0; week < 6; week++) {
                for (var column = 0; column < 7; column++) {
                    var date = control.GetDateAt (week, column);
                    var cell = control.GetCellBounds (week, column);
                    var in_month = date.Year == displayed.Year && date.Month == displayed.Month;

                    if (date >= selection_start && date <= selection_end)
                        e.Canvas.FillRectangle (cell, Theme.ControlHighlightMidColor);

                    if (control.ShowTodayCircle && date == today)
                        e.Canvas.DrawRectangle (cell, Theme.AccentColor);

                    e.Canvas.DrawText (date.Day.ToString (CultureInfo.CurrentCulture),
                                       control.IsBoldedDate (date) ? Theme.UIFontBold : font, fontSize, cell,
                                       in_month ? foreground : trailing, ContentAlignment.MiddleCenter, maxLines: 1);
                }
            }
        }

        private static void RenderTodayBand (MonthCalendar control, PaintEventArgs e, MonthCalendarGeometry geometry, SKTypeface font, int fontSize, SKColor foreground)
        {
            if (geometry.TodayBand.IsEmpty)
                return;

            e.Canvas.DrawText ($"Today: {control.TodayDate.ToShortDateString ()}", font, fontSize,
                               geometry.TodayBand, foreground, ContentAlignment.MiddleCenter, maxLines: 1);
        }

        // Color.Empty is the "not set, use the theme" sentinel these MonthCalendar colour properties
        // default to, matching how ListViewRenderer treats a per-item ForeColor.
        private static SKColor Resolve (Color color, SKColor fallback)
            => color == Color.Empty ? fallback : color.ToSKColor ();

        // A small centred box inside a scroll-arrow band; ControlPaint draws the chevron within it.
        private static Rectangle Glyph (Rectangle band, PaintEventArgs e)
        {
            var centre = band.GetCenter ();
            var size = e.LogicalToDeviceUnits (8);

            return new Rectangle (centre.X - (size / 2), centre.Y - (size / 2), size, size);
        }
    }
}
