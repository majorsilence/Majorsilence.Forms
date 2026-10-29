using System.IO;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadScheduler.Import with SchedulerICalendarImporter, read as UI.for.WinForms 2026.2.520 reads the same files.
public class TelerikSchedulerICalendarImportTests
{
    private static string Calendar (params string[] eventLines)
        => string.Join ("\r\n", new[] { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//test//EN", "BEGIN:VEVENT" }
            .Concat (eventLines).Concat (new[] { "END:VEVENT", "END:VCALENDAR", "" }));

    private static Appointment One (params string[] eventLines)
    {
        using var scheduler = new RadScheduler ();
        scheduler.Import (new MemoryStream (Encoding.UTF8.GetBytes (Calendar (eventLines))), new SchedulerICalendarImporter ());
        return Assert.Single (scheduler.Appointments);
    }

    private static DateTime Utc (int day, int hour, int minute = 0) => new DateTime (2026, 3, day, hour, minute, 0, DateTimeKind.Utc).ToLocalTime ();

    [Fact]
    public void An_event_becomes_an_appointment_with_its_text_unescaped ()
    {
        var a = One ("UID:u1", "DTSTART:20260310T140000Z", "DTEND:20260310T153000Z",
            @"SUMMARY:Utc one\, escaped\; semi \\ back", @"DESCRIPTION:line1\nline2\Nline3", "LOCATION:Room 5");

        Assert.Equal (@"Utc one, escaped; semi \ back", a.Summary);
        Assert.Equal ("line1\r\nline2\r\nline3", a.Description);
        Assert.Equal ("Room 5", a.Location);
    }

    [Fact]
    public void A_utc_time_becomes_local_and_a_floating_one_is_kept ()
    {
        var utc = One ("DTSTART:20260310T140000Z", "DTEND:20260310T153000Z", "SUMMARY:x");
        Assert.Equal (Utc (10, 14), utc.Start);
        Assert.Equal (Utc (10, 15, 30), utc.End);
        Assert.Equal (DateTimeKind.Local, utc.Start.Kind);

        var floating = One ("DTSTART:20260311T090000", "DTEND:20260311T100000", "SUMMARY:x");
        Assert.Equal (new DateTime (2026, 3, 11, 9, 0, 0), floating.Start);
        Assert.Equal (DateTimeKind.Unspecified, floating.Start.Kind);
    }

    [Fact]
    public void A_whole_day_event_ends_on_its_last_day ()
    {
        var a = One ("DTSTART;VALUE=DATE:20260312", "DTEND;VALUE=DATE:20260313", "SUMMARY:All day");
        Assert.Equal ((new DateTime (2026, 3, 12), new DateTime (2026, 3, 12)), (a.Start, a.End));

        // Without VALUE=DATE the dates are kept as they are.
        var b = One ("DTSTART:20260312", "DTEND:20260314", "SUMMARY:dates");
        Assert.Equal ((new DateTime (2026, 3, 12), new DateTime (2026, 3, 14)), (b.Start, b.End));
    }

    [Fact]
    public void A_duration_stands_in_for_the_end_and_an_event_with_neither_cannot_be_read ()
    {
        var a = One ("DTSTART:20260314T090000Z", "DURATION:PT2H", "SUMMARY:Duration");
        Assert.Equal (Utc (14, 11), a.End);

        Assert.Equal (Utc (14, 9).AddDays (1).AddMinutes (30), One ("DTSTART:20260314T090000Z", "DURATION:P1DT30M", "SUMMARY:x").End);

        var e = Assert.Throws<CalendarParseException> (() => One ("DTSTART:20260314T090000Z", "SUMMARY:No end"));
        Assert.Equal ("Cannot find property: END or DURATION", e.Message);
    }

    [Fact]
    public void Folded_lines_are_joined_names_match_in_any_case_and_parameters_are_skipped ()
    {
        Assert.Equal ("Long summary that is folded over two lines",
            One ("DTSTART:20260315T090000Z", "DTEND:20260315T100000Z", "SUMMARY:Long summary that is folded over", "  two lines").Summary);
        Assert.Equal ("lower case name", One ("DTSTART:20260318T090000Z", "DTEND:20260318T100000Z", "summary:lower case name").Summary);
        Assert.Equal ("with param", One ("DTSTART:20260310T140000Z", "DTEND:20260310T153000Z", "SUMMARY;LANGUAGE=en:with param").Summary);
        Assert.Equal (string.Empty, One ("DTSTART:20260318T090000Z", "DTEND:20260318T100000Z").Summary);
    }

    [Theory]
    [InlineData ("YELLOW", 2)]
    [InlineData ("Lavender", 10)]
    [InlineData ("purple", 3)]
    [InlineData ("Important", 1)]   // a background's name in the colour list, not the enumeration's
    [InlineData ("3", 1)]
    [InlineData ("Teal", 1)]
    public void The_background_is_read_by_colour_name (string value, int expected)
        => Assert.Equal (expected, Convert.ToInt32 (One ("DTSTART:20260316T090000Z", "DTEND:20260316T100000Z", "SUMMARY:x", "BACKGROUND:" + value).BackgroundId));

    [Theory]
    [InlineData ("FREE", 1)]
    [InlineData ("busy", 2)]
    [InlineData ("TENTATIVE", 4)]
    [InlineData ("OOF", 2)]
    [InlineData ("WHAT", 2)]
    public void The_busy_status_is_read_and_anything_else_is_busy (string value, int expected)
        => Assert.Equal (expected, Convert.ToInt32 (One ("DTSTART:20260316T090000Z", "DTEND:20260316T100000Z", "SUMMARY:x", "X-MICROSOFT-CDO-BUSYSTATUS:" + value).StatusId));

    [Fact]
    public void An_event_with_neither_is_no_background_and_busy ()
    {
        var a = One ("DTSTART:20260316T090000Z", "DTEND:20260316T100000Z", "SUMMARY:x", "STATUS:TENTATIVE", "TRANSP:TRANSPARENT", "CATEGORIES:Business");

        Assert.Equal (1, Convert.ToInt32 (a.BackgroundId));
        Assert.Equal (2, Convert.ToInt32 (a.StatusId));
    }

    [Fact]
    public void A_recurrence_rule_is_kept ()
        => Assert.Equal ("FREQ=DAILY;COUNT=3", One ("DTSTART:20260317T090000Z", "DTEND:20260317T100000Z", "SUMMARY:r", "RRULE:FREQ=DAILY;COUNT=3").RecurrenceRule);

    [Fact]
    public void A_zone_the_file_does_not_define_cannot_be_read ()
        => Assert.Throws<CalendarParseException> (() => One ("DTSTART;TZID=Eastern Standard Time:20260313T090000", "DTEND;TZID=Eastern Standard Time:20260313T100000", "SUMMARY:x"));

    [Fact]
    public void A_zone_the_file_defines_is_converted_to_local_time ()
    {
        // The form Telerik writes: the zone first, then the events in it. Offsets -05:00 standard (a fixed zone
        // where the system does not know the id).
        var ics = string.Join ("\r\n",
            "BEGIN:VCALENDAR", "VERSION:2.0",
            "BEGIN:VTIMEZONE", "TZID:Test Fixed Zone", "BEGIN:STANDARD", "TZOFFSETFROM:-05-00", "TZOFFSETTO:-05-00", "DTSTART:16010101T020000", "END:STANDARD", "END:VTIMEZONE",
            "BEGIN:VEVENT", "DTSTART;TZID=Test Fixed Zone:20260313T090000", "DTEND;TZID=Test Fixed Zone:20260313T100000", "SUMMARY:zoned", "END:VEVENT",
            "END:VCALENDAR", "");
        using var scheduler = new RadScheduler ();

        scheduler.Import (ics, new SchedulerICalendarImporter ());

        Assert.Equal (Utc (13, 14), Assert.Single (scheduler.Appointments).Start);
    }

    [Fact]
    public void A_file_the_exporter_wrote_reads_back ()
    {
        using var source = new RadScheduler ();
        source.Appointments.Add (new Appointment (new DateTime (2026, 3, 2, 9, 0, 0), new DateTime (2026, 3, 2, 10, 0, 0), "Meet, plan", "notes", "Hall"));
        var exported = new MemoryStream ();
        source.Export (exported, new SchedulerICalendarExporter ());

        using var target = new RadScheduler ();
        exported.Position = 0;
        target.Import (exported, new SchedulerICalendarImporter ());

        var a = Assert.Single (target.Appointments);
        Assert.Equal (("Meet, plan", "notes", "Hall"), (a.Summary, a.Description, a.Location));
        Assert.Equal (new DateTime (2026, 3, 2, 9, 0, 0), a.Start);
        Assert.Equal (new DateTime (2026, 3, 2, 10, 0, 0), a.End);
    }

    [Fact]
    public void Import_adds_to_the_appointments_there_and_skips_other_components ()
    {
        using var scheduler = new RadScheduler ();
        scheduler.Appointments.Add (new Appointment (new DateTime (2026, 1, 1, 9, 0, 0), new DateTime (2026, 1, 1, 10, 0, 0), "existing"));
        var ics = string.Join ("\r\n",
            "BEGIN:VCALENDAR", "VERSION:2.0",
            "BEGIN:VEVENT", "UID:x1", "DTSTART:20260310T140000Z", "DTEND:20260310T150000Z", "SUMMARY:one",
            "BEGIN:VALARM", "TRIGGER:-PT15M", "SUMMARY:alarm text", "END:VALARM", "END:VEVENT",
            "BEGIN:VEVENT", "UID:x2", "DTSTART:20260310T140000Z", "DTEND:20260310T150000Z", "SUMMARY:two", "END:VEVENT",
            "BEGIN:VTODO", "UID:x3", "SUMMARY:todo", "END:VTODO",
            "END:VCALENDAR", "");

        scheduler.Import (ics, new SchedulerICalendarImporter ());
        scheduler.Import (ics, new SchedulerICalendarImporter ());

        Assert.Equal ("existing,one,two,one,two", string.Join (",", scheduler.Appointments.Select (a => a.Summary)));
    }

    [Fact]
    public void Text_that_is_not_a_calendar_throws_and_the_stream_stays_open ()
    {
        using var scheduler = new RadScheduler ();

        var e = Assert.Throws<CalendarParseException> (() => scheduler.Import (new MemoryStream (Encoding.UTF8.GetBytes ("garbage")), new SchedulerICalendarImporter ()));
        Assert.Equal ("Cannot find calendar object: VCALENDAR", e.Message);
        Assert.Throws<CalendarParseException> (() => scheduler.Import (new MemoryStream (), new SchedulerICalendarImporter ()));
        Assert.Throws<ArgumentNullException> (() => scheduler.Import ((Stream)null!, new SchedulerICalendarImporter ()));
        Assert.Throws<ArgumentNullException> (() => scheduler.Import (new MemoryStream (), null!));

        var stream = new MemoryStream (Encoding.UTF8.GetBytes (Calendar ("DTSTART:20260310T140000Z", "DTEND:20260310T150000Z", "SUMMARY:x")));
        scheduler.Import (stream, new SchedulerICalendarImporter ());
        Assert.True (stream.CanRead);
    }

    [Fact]
    public void An_unreadable_time_throws ()
        => Assert.Throws<CalendarParseException> (() => One ("DTSTART:2026-03-10 14:00", "DTEND:20260310T150000Z", "SUMMARY:x"));
}
