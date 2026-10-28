using System.Globalization;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Week arithmetic a scheduler's views and print styles are built on. Compat for Telerik's <c>DateHelper</c>.</summary>
    /// <remarks>
    /// As Telerik's (measured on UI.for.WinForms 2026.2.520): a week is seven whole days from midnight on its first day,
    /// its end is midnight on the first day of the next week, the time of day is dropped, and the result's
    /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Unspecified"/> whatever the input's was. With the first day
    /// given, the format is not consulted for it; without, it is the format's <see cref="DateTimeFormatInfo.FirstDayOfWeek"/>.
    /// </remarks>
    public static class DateHelper
    {
        /// <summary>Returns midnight on the first day of the week <paramref name="date"/> falls in, the week starting on <paramref name="firstDayOfWeek"/>.</summary>
        /// <remarks>A week that would start before <see cref="DateTime.MinValue"/> starts on it.</remarks>
        public static DateTime GetStartOfWeek (DateTime date, DateTimeFormatInfo dateTimeFormat, DayOfWeek firstDayOfWeek)
        {
            Guard.ThrowIfNull (dateTimeFormat);

            var day = date.Date;
            var offset = ((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7;

            return new DateTime (Math.Max (DateTime.MinValue.Ticks, day.Ticks - (offset * TimeSpan.TicksPerDay)));
        }

        /// <summary>Returns midnight on the first day of the week <paramref name="date"/> falls in, the week starting on the format's first day.</summary>
        public static DateTime GetStartOfWeek (DateTime date, DateTimeFormatInfo dateTimeFormat)
        {
            Guard.ThrowIfNull (dateTimeFormat);

            return GetStartOfWeek (date, dateTimeFormat, dateTimeFormat.FirstDayOfWeek);
        }

        /// <summary>Returns the end of the week <paramref name="date"/> falls in -- midnight on the first day of the next one -- the week starting on <paramref name="firstDayOfWeek"/>.</summary>
        public static DateTime GetEndOfWeek (DateTime date, DateTimeFormatInfo dateTimeFormat, DayOfWeek firstDayOfWeek)
            => GetStartOfWeek (date, dateTimeFormat, firstDayOfWeek).AddDays (7);

        /// <summary>Returns the end of the week <paramref name="date"/> falls in -- midnight on the first day of the next one -- the week starting on the format's first day.</summary>
        public static DateTime GetEndOfWeek (DateTime date, DateTimeFormatInfo dateTimeFormat)
            => GetStartOfWeek (date, dateTimeFormat).AddDays (7);
    }
}
