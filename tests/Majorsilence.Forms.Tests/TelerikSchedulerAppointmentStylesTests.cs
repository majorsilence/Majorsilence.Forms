using System.Data;
using System.Linq;
using System.Globalization;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The appointment backgrounds and statuses RadScheduler draws, and the scheduler options Telerik code sets:
// AllowMouseWheelSupport, Culture, AllowAppointmentMove/Resize and AllowCopyPaste. Colours and defaults are the
// ones UI.for.WinForms 2026.2.520 has.
[Collection ("Headless")]
public class TelerikSchedulerAppointmentStylesTests
{
    private static readonly DateTime Day = new (2026, 3, 2);

    // The agenda lays its rows out in device pixels: a 24-pixel day header, then 32-pixel appointment rows, each
    // with a 130-pixel time column and the appointment block after it, 6 pixels in from either side.
    private const int BlockLeft = 6 + 130 + 8;

    private static int RowMiddle (int row) => 24 + (row * 32) + 16;

    private static RadScheduler Scheduler (params Appointment[] appointments)
    {
        HeadlessRenderer.Use ();

        var scheduler = new RadScheduler { Width = 600, Height = 400, ActiveViewType = SchedulerViewType.Day };
        scheduler.ActiveView.StartDate = Day;
        scheduler.Appointments.AddRange (appointments);
        scheduler.RefreshAgenda ();

        return scheduler;
    }

    // No summary, so no text lands in the pixels a test reads.
    private static Appointment At (int hour, int background, int status)
        => new (Day.AddHours (hour), Day.AddHours (hour + 1), string.Empty) { BackgroundId = background, StatusId = status };

    private static SKBitmap Agenda (RadScheduler scheduler)
    {
        var form = new Form { Width = scheduler.Width + 80, Height = scheduler.Height + 80 };
        form.Controls.Add (scheduler);
        return PaintSurface.Render (scheduler.Agenda);
    }

    private static int Border (RadScheduler scheduler) => Math.Max (1, (int)Math.Round (scheduler.Agenda.Scaling));

    private static void Near (SKColor expected, SKColor actual, int tolerance = 3)
    {
        Assert.True (Math.Abs (expected.Red - actual.Red) <= tolerance && Math.Abs (expected.Green - actual.Green) <= tolerance && Math.Abs (expected.Blue - actual.Blue) <= tolerance,
            $"expected {expected}, got {actual}");
    }

    [Fact]
    public void The_enumerations_have_Teleriks_values ()
    {
        Assert.Equal ([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], Enum.GetValues<AppointmentBackground> ().Select (v => (int)v));
        Assert.Equal ("None,Important,Business,Personal,Vacation,MustAttend,TravelRequired,NeedsPreparation,Birthday,Anniversary,PhoneCall",
            string.Join (",", Enum.GetNames<AppointmentBackground> ()));
        Assert.Equal ("Free=1,Busy=2,Unavailable=3,Tentative=4", string.Join (",", Enum.GetValues<AppointmentStatus> ().Select (v => $"{v}={(int)v}")));
    }

    [Fact]
    public void A_new_appointment_has_no_background_and_is_busy ()
    {
        var appointment = new Appointment (Day, Day.AddHours (1), "x");

        Assert.Equal ((int)AppointmentBackground.None, appointment.BackgroundId);
        Assert.Equal ((int)AppointmentStatus.Busy, appointment.StatusId);
    }

    [Theory]
    [InlineData (AppointmentBackground.Important, 255, 216, 107)]
    [InlineData (AppointmentBackground.Business, 211, 187, 244)]
    [InlineData (AppointmentBackground.Personal, 142, 245, 196)]
    [InlineData (AppointmentBackground.Vacation, 248, 184, 245)]
    [InlineData (AppointmentBackground.MustAttend, 209, 226, 247)]
    [InlineData (AppointmentBackground.TravelRequired, 187, 232, 161)]
    [InlineData (AppointmentBackground.NeedsPreparation, 219, 219, 219)]
    [InlineData (AppointmentBackground.Birthday, 255, 186, 102)]
    [InlineData (AppointmentBackground.Anniversary, 204, 204, 255)]
    [InlineData (AppointmentBackground.PhoneCall, 232, 174, 212)]
    public void An_appointment_is_filled_with_its_backgrounds_colour (AppointmentBackground background, byte r, byte g, byte b)
    {
        using var scheduler = Scheduler (At (9, (int)background, (int)AppointmentStatus.Busy));
        using var bitmap = Agenda (scheduler);

        Near (new SKColor (r, g, b), bitmap.GetPixel (bitmap.Width - 30, RowMiddle (0)));
    }

    [Theory]
    [InlineData ((int)AppointmentBackground.None)]
    [InlineData (0)]
    [InlineData (99)]
    public void No_background_or_an_unknown_one_is_the_pale_blue_gradient (int background)
    {
        using var scheduler = Scheduler (At (9, background, (int)AppointmentStatus.Busy));
        using var bitmap = Agenda (scheduler);
        var border = Border (scheduler);
        var top = 24 + 2 + border;
        var bottom = 24 + 32 - 2 - border - 1;

        Near (new SKColor (232, 241, 252), bitmap.GetPixel (bitmap.Width - 30, top), 4);
        Near (new SKColor (209, 226, 247), bitmap.GetPixel (bitmap.Width - 30, bottom), 4);
    }

    [Fact]
    public void The_block_has_Teleriks_border ()
    {
        using var scheduler = Scheduler (At (9, (int)AppointmentBackground.Important, (int)AppointmentStatus.Busy));
        using var bitmap = Agenda (scheduler);

        Near (new SKColor (110, 153, 210), bitmap.GetPixel (BlockLeft, RowMiddle (0)));
    }

    [Theory]
    [InlineData (AppointmentStatus.Free, 93, 174, 47)]
    [InlineData (AppointmentStatus.Unavailable, 199, 164, 2)]
    public void A_status_other_than_busy_draws_a_strip_in_its_colour (AppointmentStatus status, byte r, byte g, byte b)
    {
        using var scheduler = Scheduler (At (9, (int)AppointmentBackground.Important, (int)status));
        using var bitmap = Agenda (scheduler);
        var border = Border (scheduler);

        Near (new SKColor (r, g, b), bitmap.GetPixel (BlockLeft + border + 1, RowMiddle (0)));
        // Then a border-coloured line between the strip and the background.
        Near (new SKColor (110, 153, 210), bitmap.GetPixel (BlockLeft + border + (AppointmentStyles.StripWidth * border), RowMiddle (0)));
        Near (new SKColor (255, 216, 107), bitmap.GetPixel (BlockLeft + border + (AppointmentStyles.StripWidth * border) + border + 1, RowMiddle (0)));
    }

    [Fact]
    public void Busy_draws_no_strip ()
    {
        using var scheduler = Scheduler (At (9, (int)AppointmentBackground.Important, (int)AppointmentStatus.Busy));
        using var bitmap = Agenda (scheduler);
        var border = Border (scheduler);

        for (var x = BlockLeft + border; x < BlockLeft + border + (AppointmentStyles.StripWidth * border) + border + 2; x++)
            Near (new SKColor (255, 216, 107), bitmap.GetPixel (x, RowMiddle (0)));
    }

    [Fact]
    public void Tentative_is_a_blue_hatch_on_white ()
    {
        using var scheduler = Scheduler (At (9, (int)AppointmentBackground.Important, (int)AppointmentStatus.Tentative));
        using var bitmap = Agenda (scheduler);
        var border = Border (scheduler);
        var strip = Enumerable.Range (24 + 2 + border, 32 - 4 - (2 * border))
            .SelectMany (y => Enumerable.Range (BlockLeft + border, AppointmentStyles.StripWidth * border).Select (x => bitmap.GetPixel (x, y)))
            .ToList ();

        Assert.Contains (strip, c => c.Red == 255 && c.Green == 255 && c.Blue == 255);
        Assert.Contains (strip, c => c.Blue > 200 && c.Red < 120);
        Assert.DoesNotContain (strip, c => c == new SKColor (255, 216, 107));
    }

    [Fact]
    public void An_unknown_status_leaves_the_strip_empty_but_draws_its_line ()
    {
        using var scheduler = Scheduler (At (9, (int)AppointmentBackground.Important, 99));
        using var bitmap = Agenda (scheduler);
        var border = Border (scheduler);

        Near (new SKColor (255, 216, 107), bitmap.GetPixel (BlockLeft + border + 1, RowMiddle (0)));
        Near (new SKColor (110, 153, 210), bitmap.GetPixel (BlockLeft + border + (AppointmentStyles.StripWidth * border), RowMiddle (0)));
    }

    [Fact]
    public void A_bound_background_and_status_are_read_as_numbers_and_an_empty_one_keeps_the_default ()
    {
        var table = new DataTable ();
        table.Columns.Add ("Start", typeof (DateTime));
        table.Columns.Add ("End", typeof (DateTime));
        table.Columns.Add ("Summary", typeof (string));
        table.Columns.Add ("Background", typeof (long));
        table.Columns.Add ("Status", typeof (string));
        table.Rows.Add (Day, Day.AddHours (1), "set", 10L, "4");
        table.Rows.Add (Day, Day.AddHours (1), "empty", DBNull.Value, DBNull.Value);

        var source = new SchedulerBindingDataSource ();
        source.EventProvider.Mapping = new AppointmentMappingInfo { Start = "Start", End = "End", Summary = "Summary", BackgroundId = "Background", StatusId = "Status" };
        source.EventProvider.DataSource = table;

        Assert.Equal ((int)AppointmentBackground.Anniversary, source.Appointments[0].BackgroundId);
        Assert.Equal ((int)AppointmentStatus.Tentative, source.Appointments[0].StatusId);
        Assert.Equal ((int)AppointmentBackground.None, source.Appointments[1].BackgroundId);
        Assert.Equal ((int)AppointmentStatus.Busy, source.Appointments[1].StatusId);
    }

    // ── Options ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_options_default_as_Teleriks_do ()
    {
        using var scheduler = new RadScheduler ();

        Assert.True (scheduler.AllowAppointmentMove);
        Assert.True (scheduler.AllowAppointmentResize);
        Assert.Equal (CopyPasteMode.All, scheduler.AllowCopyPaste);
        Assert.True (scheduler.AllowMouseWheelSupport);
        Assert.Same (CultureInfo.CurrentCulture, scheduler.Culture);
    }

    private static RadScheduler Long (out Panel outer)
    {
        var appointments = Enumerable.Range (0, 40).Select (i => new Appointment (Day.AddMinutes (i * 15), Day.AddMinutes ((i * 15) + 10), $"a{i}")).ToArray ();
        var scheduler = Scheduler (appointments);
        scheduler.Height = 200;

        outer = new Panel { AutoScroll = true, Width = 650, Height = 150 };
        outer.Controls.Add (scheduler);
        outer.Controls.Add (new Label { Top = 600, Text = "below" });

        var form = new Form { Width = 800, Height = 600 };
        form.Controls.Add (outer);
        form.Show ();

        return scheduler;
    }

    private static void Wheel (Control control) => control.RaiseMouseWheel (new MouseEventArgs (MouseButtons.None, 0, 100, 100, -120));

    [Fact]
    public void The_wheel_scrolls_the_appointments ()
    {
        var scheduler = Long (out var outer);

        using (outer.FindForm ()) {
            var before = scheduler.AutoScrollPosition.Y;

            Wheel (outer);

            Assert.True (scheduler.AutoScrollPosition.Y < before);
        }
    }

    [Fact]
    public void With_wheel_support_off_the_wheel_does_nothing_and_is_not_passed_on ()
    {
        var scheduler = Long (out var outer);

        using (outer.FindForm ()) {
            scheduler.AllowMouseWheelSupport = false;
            var inner_before = scheduler.AutoScrollPosition.Y;
            var outer_before = outer.AutoScrollPosition.Y;

            Wheel (outer);

            Assert.Equal (inner_before, scheduler.AutoScrollPosition.Y);
            Assert.Equal (outer_before, outer.AutoScrollPosition.Y);
        }
    }

    [Fact]
    public void Culture_sets_DateTimeFormat_and_null_goes_back_to_the_current_culture ()
    {
        using var scheduler = new RadScheduler ();
        var french = new CultureInfo ("fr-FR");

        scheduler.Culture = french;

        Assert.Same (french, scheduler.Culture);
        Assert.Same (french.DateTimeFormat, scheduler.DateTimeFormat);
        Assert.Equal (DayOfWeek.Monday, scheduler.DateTimeFormat.FirstDayOfWeek);

        scheduler.Culture = null!;

        Assert.Same (CultureInfo.CurrentCulture, scheduler.Culture);
        Assert.Same (CultureInfo.CurrentCulture.DateTimeFormat, scheduler.DateTimeFormat);
    }

    [Fact]
    public void The_day_headers_are_written_in_the_schedulers_culture ()
    {
        static byte[] Header (string culture)
        {
            using var scheduler = Scheduler (At (9, 1, 2));
            scheduler.Culture = new CultureInfo (culture);
            using var bitmap = Agenda (scheduler);
            var header = new SKRectI (0, 0, bitmap.Width, 24);
            using var subset = new SKBitmap ();
            bitmap.ExtractSubset (subset, header);
            return subset.Copy ().Bytes;
        }

        Assert.Equal (Header ("en-US"), Header ("en-US"));
        Assert.NotEqual (Header ("en-US"), Header ("fr-FR"));
    }
}
