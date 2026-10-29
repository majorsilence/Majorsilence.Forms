using System.Globalization;
using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;
using DrawingFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests;

// The scheduler print styles' DateStartRange/DateEndRange and fonts, what a print job takes from them, and DateHelper's
// week arithmetic -- the values are UI.for.WinForms 2026.2.520's.
public class TelerikSchedulerPrintStyleTests
{
    private static DateTime D (int month, int day, int hour = 0) => new (2026, month, day, hour, 0, 0);

    [Fact]
    public void A_daily_or_details_range_is_whole_days_and_kept_as_given ()
    {
        var daily = new SchedulerDailyPrintStyle (D (3, 4, 10), D (3, 6, 15));
        var details = new SchedulerDetailsPrintStyle (D (3, 4, 10), D (3, 6, 15));

        Assert.Equal ((D (3, 4), D (3, 6)), (daily.DateStartRange, daily.DateEndRange));
        Assert.Equal ((D (3, 4), D (3, 6)), (details.DateStartRange, details.DateEndRange));

        // An end before the start is kept.
        daily.DateEndRange = D (3, 1);
        Assert.Equal (D (3, 1), daily.DateEndRange);
    }

    [Fact]
    public void A_weekly_range_is_whole_weeks_from_Monday_to_the_Monday_after ()
    {
        Assert.Equal ((D (3, 2), D (3, 9)), Range (new SchedulerWeeklyPrintStyle (D (3, 4), D (3, 6))));
        Assert.Equal ((D (2, 23), D (3, 16)), Range (new SchedulerWeeklyPrintStyle (D (3, 1), D (3, 15))));

        var weekly = new SchedulerWeeklyPrintStyle ();
        weekly.DateStartRange = D (3, 8, 23);
        weekly.DateEndRange = D (3, 8);
        Assert.Equal ((D (3, 2), D (3, 9)), Range (weekly));

        // It prints the week up to the Sunday.
        Assert.Equal (D (3, 8), weekly.EndDate);
    }

    [Fact]
    public void A_monthly_range_is_whole_months_ending_a_second_before_midnight ()
    {
        Assert.Equal ((D (3, 1), new DateTime (2026, 3, 31, 23, 59, 59)), Range (new SchedulerMonthlyPrintStyle (D (3, 6), D (3, 4))));
        Assert.Equal ((D (3, 1), new DateTime (2026, 4, 30, 23, 59, 59)), Range (new SchedulerMonthlyPrintStyle (D (3, 15), D (4, 2))));
    }

    private static (DateTime, DateTime) Range (SchedulerPrintStyleBase style) => (style.DateStartRange, style.DateEndRange);

    [Fact]
    public void A_style_made_without_dates_covers_today_and_the_five_days_after ()
    {
        var today = DateTime.Today;

        Assert.Equal ((today, today.AddDays (5)), Range (new SchedulerDailyPrintStyle ()));
        Assert.Equal ((today, today.AddDays (5)), Range (new SchedulerDetailsPrintStyle ()));
        Assert.Equal (Range (new SchedulerWeeklyPrintStyle (today, today.AddDays (5))), Range (new SchedulerWeeklyPrintStyle ()));
    }

    [Fact]
    public void The_fonts_default_as_Teleriks_and_null_goes_back_to_the_default ()
    {
        var style = new SchedulerDailyPrintStyle ();

        Assert.Equal (("Segoe UI", 9f, false), (style.AppointmentFont.FamilyName, style.AppointmentFont.Size, style.AppointmentFont.Bold));
        Assert.Equal (("Segoe UI", 9f, true), (style.DateHeadingFont.FamilyName, style.DateHeadingFont.Size, style.DateHeadingFont.Bold));
        Assert.Equal (("Segoe UI", 22f, true), (style.PageHeadingFont.FamilyName, style.PageHeadingFont.Size, style.PageHeadingFont.Bold));

        var arial = new DrawingFont ("Arial", 12);
        style.AppointmentFont = arial;
        Assert.Same (arial, style.AppointmentFont);

        style.AppointmentFont = null!;
        Assert.Equal ("Segoe UI", style.AppointmentFont.FamilyName);
    }

    private static RadScheduler Scheduler ()
    {
        var scheduler = new RadScheduler ();
        foreach (var day in new[] { 1, 2, 4, 8, 9 })
            scheduler.Appointments.Add (new Appointment (D (3, day, 9), D (3, day, 10), $"on {day}"));
        return scheduler;
    }

    private static string Days (RadScheduler scheduler)
        => string.Join (",", RadSchedulerPrintRenderer.Lines (scheduler, new RadPrintDocument ())
            .Where (l => l.Kind == RadSchedulerPrintRenderer.LineKind.Appointment).Select (l => l.Text.Split ("on ")[1]));

    [Fact]
    public void A_job_prints_the_days_of_the_styles_range ()
    {
        var scheduler = Scheduler ();

        scheduler.PrintStyle = new SchedulerDailyPrintStyle (D (3, 2), D (3, 4));
        Assert.Equal ("2,4", Days (scheduler));

        // A weekly style prints Monday to Sunday, not the Monday its range ends on.
        scheduler.PrintStyle = new SchedulerWeeklyPrintStyle (D (3, 4), D (3, 4));
        Assert.Equal ("2,4,8", Days (scheduler));

        scheduler.PrintStyle.DateStartRange = D (3, 9);
        scheduler.PrintStyle.DateEndRange = D (3, 9);
        Assert.Equal ("9", Days (scheduler));
    }

    [Fact]
    public void A_job_prints_in_the_styles_fonts ()
    {
        var scheduler = Scheduler ();
        var page = new DrawingFont ("Arial", 30, bold: true);
        var heading = new DrawingFont ("Arial", 11, bold: true);
        var appointment = new DrawingFont ("Arial", 7);
        scheduler.PrintStyle = new SchedulerDailyPrintStyle (D (3, 2), D (3, 2)) { PageHeadingFont = page, DateHeadingFont = heading, AppointmentFont = appointment };

        var lines = RadSchedulerPrintRenderer.Lines (scheduler, new RadPrintDocument ());

        Assert.Equal ([RadSchedulerPrintRenderer.LineKind.Title, RadSchedulerPrintRenderer.LineKind.DateHeading, RadSchedulerPrintRenderer.LineKind.Appointment], lines.Select (l => l.Kind));
        Assert.Same (page, lines[0].Font);
        Assert.Same (heading, lines[1].Font);
        Assert.Same (appointment, lines[2].Font);

        scheduler.PrintStyle.DrawPageTitleCalendar = false;
        Assert.DoesNotContain (RadSchedulerPrintRenderer.Lines (scheduler, new RadPrintDocument ()), l => l.Kind == RadSchedulerPrintRenderer.LineKind.Title);
    }

    [Fact]
    public void A_style_set_after_the_document_is_attached_is_the_one_printed ()
    {
        var scheduler = Scheduler ();
        var document = new RadPrintDocument { AssociatedObject = scheduler };
        var printed = new List<int> ();

        // As the application does: attach the document, then choose the style.
        scheduler.PrintStyle = new SchedulerDailyPrintStyle (D (3, 9), D (3, 9));

        using var stream = new MemoryStream ();
        document.PrintPage += (_, e) => printed.Add (1);
        document.PrintToPdf (stream);

        Assert.Equal ("9", Days (scheduler));
        Assert.Single (printed);
        Assert.True (stream.Length > 0);

        // A second job lays itself out again.
        scheduler.PrintStyle = new SchedulerDailyPrintStyle (D (3, 1), D (3, 9));
        using var second = new MemoryStream ();
        document.PrintToPdf (second);
        Assert.True (second.Length > 0);
    }

    [Fact]
    public void A_style_set_after_attaching_decides_the_pages ()
    {
        static int Pages (int lastDay)
        {
            var scheduler = new RadScheduler ();
            for (var day = 1; day <= 28; day++)
                for (var hour = 0; hour < 6; hour++)
                    scheduler.Appointments.Add (new Appointment (D (2, day, hour), D (2, day, hour + 1), $"{day}/{hour}"));

            var document = new RadPrintDocument { AssociatedObject = scheduler };
            scheduler.PrintStyle = new SchedulerDailyPrintStyle (D (2, 1), D (2, lastDay));
            var pages = 0;
            document.PrintPage += (_, _) => pages++;
            using var stream = new MemoryStream ();
            document.PrintToPdf (stream);
            return pages;
        }

        Assert.Equal (1, Pages (1));
        Assert.True (Pages (28) > 1);
    }

    // ── DateHelper ──────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData (4, DayOfWeek.Monday, 2, 9)]
    [InlineData (4, DayOfWeek.Sunday, 1, 8)]
    [InlineData (4, DayOfWeek.Saturday, -28, 7)]
    [InlineData (1, DayOfWeek.Monday, -23, 2)]
    [InlineData (8, DayOfWeek.Sunday, 8, 15)]
    public void DateHelper_weeks_start_on_the_given_day_and_end_at_midnight_a_week_on (int day, DayOfWeek first, int start, int end)
    {
        var format = new CultureInfo ("en-US").DateTimeFormat;
        var date = D (3, day, 15).AddMinutes (30);

        Assert.Equal (start < 0 ? D (2, -start) : D (3, start), DateHelper.GetStartOfWeek (date, format, first));
        Assert.Equal (D (3, end), DateHelper.GetEndOfWeek (date, format, first));
    }

    [Fact]
    public void DateHelper_without_a_first_day_uses_the_formats ()
    {
        var us = new CultureInfo ("en-US").DateTimeFormat;
        var fr = new CultureInfo ("fr-FR").DateTimeFormat;

        Assert.Equal (D (3, 1), DateHelper.GetStartOfWeek (D (3, 4), us));
        Assert.Equal (D (3, 8), DateHelper.GetEndOfWeek (D (3, 4), us));
        Assert.Equal (D (3, 2), DateHelper.GetStartOfWeek (D (3, 4), fr));
        Assert.Equal (D (3, 9), DateHelper.GetEndOfWeek (D (3, 4), fr));
    }

    [Fact]
    public void DateHelper_drops_the_kind_clamps_at_the_first_date_and_needs_a_format ()
    {
        var us = new CultureInfo ("en-US").DateTimeFormat;

        Assert.Equal (DateTimeKind.Unspecified, DateHelper.GetStartOfWeek (new DateTime (2026, 3, 4, 1, 0, 0, DateTimeKind.Utc), us, DayOfWeek.Monday).Kind);
        Assert.Equal (DateTime.MinValue, DateHelper.GetStartOfWeek (DateTime.MinValue.AddDays (3), us, DayOfWeek.Monday));
        Assert.ThrowsAny<ArgumentException> (() => DateHelper.GetEndOfWeek (DateTime.MaxValue.AddDays (-1), us, DayOfWeek.Monday));
        Assert.Throws<ArgumentNullException> (() => DateHelper.GetStartOfWeek (D (3, 4), null!, DayOfWeek.Monday));
    }
}
