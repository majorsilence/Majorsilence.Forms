using System.Globalization;
using System.IO;
using System.Text;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Reads appointments from a stream or text into a <see cref="RadScheduler"/>. Compat for Telerik's <c>ISchedulerImporter</c>.</summary>
    public interface ISchedulerImporter
    {
        /// <summary>Reads the appointments in <paramref name="stream"/> and adds them to <paramref name="scheduler"/>'s.</summary>
        void Import (RadScheduler scheduler, Stream stream);

        /// <summary>Reads the appointments in <paramref name="reader"/> and adds them to <paramref name="scheduler"/>'s.</summary>
        void Import (RadScheduler scheduler, TextReader reader);
    }

    /// <summary>The error an importer raises for text it cannot read. Compat for Telerik's <c>CalendarParseException</c>.</summary>
    public class CalendarParseException : Exception
    {
        /// <summary>Initializes a new instance with no message.</summary>
        public CalendarParseException () { }

        /// <summary>Initializes a new instance with the specified message.</summary>
        public CalendarParseException (string message) : base (message) { }

        /// <summary>Initializes a new instance with the specified message and the error that caused it.</summary>
        public CalendarParseException (string message, Exception innerException) : base (message, innerException) { }
    }

    /// <summary>
    /// Imports iCalendar (RFC 5545, <c>.ics</c>) events as appointments. Compat for Telerik's <c>SchedulerICalendarImporter</c>.
    /// </summary>
    /// <remarks>
    /// Read as Telerik reads it (measured on UI.for.WinForms 2026.2.520). Each VEVENT becomes an appointment added after
    /// the ones the scheduler already has; other components are skipped. Lines folded onto the next one are joined,
    /// names are matched in any case and parameters are ignored, and the escapes <c>\,</c> <c>\;</c> <c>\\</c> and
    /// <c>\n</c> are read, a new line being CR LF. A time ending in Z is UTC and becomes local time; one with a TZID
    /// is in that zone, which the file has to define in a VTIMEZONE; one with neither is taken as it is. A
    /// <c>VALUE=DATE</c> event is a whole-day one that ends on its last day. DURATION stands in for DTEND, and an
    /// event with neither cannot be read. RRULE becomes the <see cref="Appointment.RecurrenceRule"/>; the
    /// X-MICROSOFT-CDO-BUSYSTATUS status and the BACKGROUND colour name (Yellow, Purple, ... as Telerik's
    /// backgrounds are named) give the appointment's <see cref="Appointment.StatusId"/> and
    /// <see cref="Appointment.BackgroundId"/>, anything not recognized leaving the default. Text that is not a
    /// calendar throws <see cref="CalendarParseException"/>, and so does a time that cannot be read.
    /// </remarks>
    public class SchedulerICalendarImporter : ISchedulerImporter
    {
        // Telerik's default backgrounds, by display name, in id order from AppointmentBackground.None (1).
        private static readonly string[] background_names = ["None", "Yellow", "Purple", "Green", "Pink", "Blue", "Olive", "Gray", "Orange", "Lavender", "Raspberry"];

        private static readonly string[] status_names = ["Free", "Busy", "Unavailable", "Tentative"];

        /// <inheritdoc/>
        public void Import (RadScheduler scheduler, Stream stream)
        {
            Guard.ThrowIfNull (stream);

            // The stream is the caller's: it stays open.
            using var reader = new StreamReader (stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            Import (scheduler, reader);
        }

        /// <inheritdoc/>
        public void Import (RadScheduler scheduler, TextReader reader)
        {
            Guard.ThrowIfNull (scheduler);
            Guard.ThrowIfNull (reader);

            var appointments = Read (reader);
            scheduler.Appointments.AddRange (appointments);
            scheduler.RefreshAgenda ();
        }

        private sealed record Property (string Name, Dictionary<string, string> Parameters, string Value);

        private static List<Appointment> Read (TextReader reader)
        {
            var lines = Unfold (reader);
            var calendar = lines.FindIndex (p => Is (p, "BEGIN", "VCALENDAR"));

            if (calendar < 0)
                throw new CalendarParseException ("Cannot find calendar object: VCALENDAR");

            // The zones the file defines, by TZID, with their standard offset for a zone the system does not know.
            var zones = new Dictionary<string, TimeSpan?> (StringComparer.OrdinalIgnoreCase);
            var result = new List<Appointment> ();
            var depth = 0;
            List<Property>? current = null;
            string? zone = null;

            for (var i = calendar + 1; i < lines.Count; i++) {
                var p = lines[i];

                if (p.Name == "BEGIN") {
                    depth++;
                    if (depth == 1 && p.Value.Equals ("VEVENT", StringComparison.OrdinalIgnoreCase))
                        current = [];
                    else if (depth == 1 && p.Value.Equals ("VTIMEZONE", StringComparison.OrdinalIgnoreCase))
                        zone = string.Empty;
                    continue;
                }

                if (p.Name == "END") {
                    if (depth == 0)
                        break;  // END:VCALENDAR

                    depth--;
                    if (depth == 0) {
                        if (current is not null)
                            result.Add (ToAppointment (current, zones));
                        current = null;
                        zone = null;
                    }
                    continue;
                }

                if (current is not null && depth == 1) {
                    current.Add (p);
                } else if (zone is not null) {
                    if (depth == 1 && p.Name == "TZID") {
                        zone = p.Value;
                        zones[zone] = null;
                    } else if (depth == 2 && p.Name == "TZOFFSETTO" && zone.Length > 0 && zones[zone] is null && Offset (p.Value) is { } offset) {
                        zones[zone] = offset;
                    }
                }
            }

            return result;
        }

        private static bool Is (Property p, string name, string value) => p.Name == name && p.Value.Trim ().Equals (value, StringComparison.OrdinalIgnoreCase);

        // Joins folded lines (a line starting with a space or tab continues the one before, less that one character)
        // and splits each into its name, parameters and value.
        private static List<Property> Unfold (TextReader reader)
        {
            var raw = new List<StringBuilder> ();
            string? line;

            while ((line = reader.ReadLine ()) is not null) {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && raw.Count > 0)
                    raw[^1].Append (line, 1, line.Length - 1);
                else if (line.Length > 0)
                    raw.Add (new StringBuilder (line));
            }

            var result = new List<Property> ();

            foreach (var text in raw.Select (b => b.ToString ())) {
                var colon = ValueStart (text);
                if (colon < 0)
                    continue;

                var head = text[..colon].Split (';');
                var parameters = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

                foreach (var parameter in head.Skip (1)) {
                    var equals = parameter.IndexOf ('=');
                    if (equals > 0)
                        parameters[parameter[..equals]] = parameter[(equals + 1)..].Trim ('"');
                }

                result.Add (new Property (head[0].Trim ().ToUpperInvariant (), parameters, text[(colon + 1)..]));
            }

            return result;
        }

        // The colon that ends the name and parameters: the first one outside a quoted parameter value.
        private static int ValueStart (string text)
        {
            var quoted = false;

            for (var i = 0; i < text.Length; i++) {
                if (text[i] == '"')
                    quoted = !quoted;
                else if (text[i] == ':' && !quoted)
                    return i;
            }

            return -1;
        }

        private static Appointment ToAppointment (List<Property> properties, Dictionary<string, TimeSpan?> zones)
        {
            Property? Find (string name) => properties.FirstOrDefault (p => p.Name == name);

            var start_property = Find ("DTSTART") ?? throw new CalendarParseException ("Cannot find property: DTSTART");
            var start = ToDateTime (start_property, zones, out var whole_day);
            DateTime end;

            if (Find ("DTEND") is { } end_property) {
                end = ToDateTime (end_property, zones, out _);

                // A whole-day event's DTEND is the day after its last; the appointment ends on the last.
                if (whole_day)
                    end = end.AddDays (-1);
            } else if (Find ("DURATION") is { } duration) {
                end = start + ToDuration (duration.Value);
            } else {
                throw new CalendarParseException ("Cannot find property: END or DURATION");
            }

            var appointment = new Appointment (start, end, Unescape (Find ("SUMMARY")?.Value ?? string.Empty)) {
                Description = Unescape (Find ("DESCRIPTION")?.Value ?? string.Empty),
                Location = Unescape (Find ("LOCATION")?.Value ?? string.Empty),
                RecurrenceRule = Find ("RRULE")?.Value ?? string.Empty,
                BackgroundId = 1,   // AppointmentBackground.None, as Telerik leaves an event with no BACKGROUND
                StatusId = 2,       // AppointmentStatus.Busy
            };

            if (Find ("BACKGROUND") is { } background && Array.FindIndex (background_names, n => n.Equals (background.Value.Trim (), StringComparison.OrdinalIgnoreCase)) is var b and >= 0)
                appointment.BackgroundId = b + 1;

            if (Find ("X-MICROSOFT-CDO-BUSYSTATUS") is { } status && Array.FindIndex (status_names, n => n.Equals (status.Value.Trim (), StringComparison.OrdinalIgnoreCase)) is var s and >= 0)
                appointment.StatusId = s + 1;

            return appointment;
        }

        private static DateTime ToDateTime (Property property, Dictionary<string, TimeSpan?> zones, out bool wholeDay)
        {
            var value = property.Value.Trim ();
            wholeDay = property.Parameters.TryGetValue ("VALUE", out var kind) && kind.Equals ("DATE", StringComparison.OrdinalIgnoreCase);

            if (value.Length == 8 && DateTime.TryParseExact (value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;

            var utc = value.EndsWith ("Z", StringComparison.OrdinalIgnoreCase);
            var text = utc ? value[..^1] : value;

            if (!DateTime.TryParseExact (text, ["yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                throw new CalendarParseException ("Cannot parse property.");

            if (utc)
                return DateTime.SpecifyKind (time, DateTimeKind.Utc).ToLocalTime ();

            if (!property.Parameters.TryGetValue ("TZID", out var tzid))
                return time;

            // A zone the file does not define is an error, as it is to Telerik.
            if (!zones.TryGetValue (tzid, out var standard))
                throw new CalendarParseException ("Cannot parse property.");

            if (FindZone (tzid) is { } system)
                return TimeZoneInfo.ConvertTime (DateTime.SpecifyKind (time, DateTimeKind.Unspecified), system, TimeZoneInfo.Local);

            return standard is { } offset
                ? DateTime.SpecifyKind (time - offset, DateTimeKind.Utc).ToLocalTime ()
                : time;
        }

        private static TimeZoneInfo? FindZone (string id)
        {
            try {
                return TimeZoneInfo.FindSystemTimeZoneById (id);
            } catch (TimeZoneNotFoundException) {
                return null;
            } catch (InvalidTimeZoneException) {
                return null;
            }
        }

        // A UTC offset: +hhmm, -hh-mm (the form Telerik writes), or with seconds.
        private static TimeSpan? Offset (string value)
        {
            var text = value.Trim ();
            if (text.Length < 5)
                return null;

            var negative = text[0] == '-';
            var digits = new string (text.Where (char.IsDigit).ToArray ());

            if (digits.Length < 4 || !int.TryParse (digits[..2], out var hours) || !int.TryParse (digits[2..4], out var minutes))
                return null;

            var offset = new TimeSpan (hours, minutes, 0);
            return negative ? -offset : offset;
        }

        // An RFC 5545 duration, such as PT2H, P1D or -PT15M.
        private static TimeSpan ToDuration (string value)
        {
            var text = value.Trim ().ToUpperInvariant ();
            var negative = text.StartsWith ('-');
            text = text.TrimStart ('+', '-');

            if (!text.StartsWith ('P'))
                throw new CalendarParseException ("Cannot parse property.");

            var result = TimeSpan.Zero;
            var number = 0;
            var in_time = false;

            foreach (var c in text[1..]) {
                if (char.IsDigit (c)) {
                    number = (number * 10) + (c - '0');
                    continue;
                }

                result += c switch {
                    'T' => TimeSpan.Zero,
                    'W' => TimeSpan.FromDays (7 * number),
                    'D' => TimeSpan.FromDays (number),
                    'H' when in_time => TimeSpan.FromHours (number),
                    'M' when in_time => TimeSpan.FromMinutes (number),
                    'S' when in_time => TimeSpan.FromSeconds (number),
                    _ => throw new CalendarParseException ("Cannot parse property."),
                };

                in_time |= c == 'T';
                number = 0;
            }

            return negative ? -result : result;
        }

        private static string Unescape (string value)
        {
            var result = new StringBuilder (value.Length);

            for (var i = 0; i < value.Length; i++) {
                if (value[i] == '\\' && i + 1 < value.Length) {
                    var next = value[++i];
                    result.Append (next switch {
                        'n' or 'N' => "\r\n",
                        _ => next.ToString (),
                    });
                } else {
                    result.Append (value[i]);
                }
            }

            return result.ToString ();
        }
    }
}
