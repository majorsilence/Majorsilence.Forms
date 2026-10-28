using System.Drawing;
using Majorsilence.Forms.Printing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat print document for <see cref="RadScheduler"/>. Extends
    /// <see cref="Majorsilence.Forms.Printing.PrintDocument"/> (the same Skia/PDF pipeline every other
    /// printing surface in Majorsilence.Forms uses) with the scheduler-specific
    /// <see cref="AssociatedObject"/>, header/footer fonts, and an optional diagonal <see cref="Watermark"/>.
    /// Rendering is driven by <see cref="RadScheduler.Print(bool, RadPrintDocument)"/>/
    /// <see cref="RadScheduler.PrintPreview(RadPrintDocument)"/>, which set <see cref="AssociatedObject"/>
    /// and hook <see cref="PrintDocument.PrintPage"/> to paint the agenda list for the scheduler's current
    /// <see cref="RadScheduler.PrintStyle"/> date range.
    /// </summary>
    public class RadPrintDocument : PrintDocument
    {
        private RadScheduler? _associatedObject;

        /// <summary>Initializes a new instance of the <see cref="RadPrintDocument"/> class.</summary>
        public RadPrintDocument () => DocumentName = "scheduler";

        /// <summary>
        /// Gets or sets the scheduler this document prints. Setting this hooks <see cref="PrintDocument.PrintPage"/>
        /// to render the agenda list (grouped by day, one row per appointment) for the date range implied
        /// by the scheduler's current <see cref="RadScheduler.PrintStyle"/> (or its <see cref="RadScheduler.ActiveView"/>
        /// range when no print style has been set).
        /// </summary>
        public RadScheduler? AssociatedObject {
            get => _associatedObject;
            set {
                if (ReferenceEquals (_associatedObject, value))
                    return;

                _associatedObject = value;
                RadSchedulerPrintRenderer.Attach (this, value);
            }
        }

        /// <summary>Gets or sets the font used to draw the page header (the print style's title, when <see cref="SchedulerPrintStyleBase.DrawPageTitleCalendar"/> is set).</summary>
        public Majorsilence.Forms.Drawing.Font HeaderFont { get; set; } = new Majorsilence.Forms.Drawing.Font ("Arial", 10);

        /// <summary>Gets or sets the font used to draw the page footer (page number).</summary>
        public Majorsilence.Forms.Drawing.Font FooterFont { get; set; } = new Majorsilence.Forms.Drawing.Font ("Arial", 8);

        /// <summary>Gets or sets the watermark drawn diagonally across every page, or null for no watermark.</summary>
        public RadPrintWatermark? Watermark { get; set; }
    }

    /// <summary>
    /// A text watermark drawn diagonally across every page of a <see cref="RadPrintDocument"/>. Compat for
    /// Telerik's <c>RadPrintWatermark</c>.
    /// </summary>
    public class RadPrintWatermark
    {
        /// <summary>Gets or sets the watermark text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the watermark font.</summary>
        public Majorsilence.Forms.Drawing.Font Font { get; set; } = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 72);

        /// <summary>Gets or sets the watermark's opacity, from 0 (invisible) to 1 (opaque).</summary>
        public float Opacity { get; set; } = 0.2f;

        /// <summary>Gets or sets the watermark color.</summary>
        public Color Color { get; set; } = Color.Gray;
    }

    /// <summary>
    /// Common base for the scheduler print-style carriers (<see cref="SchedulerDailyPrintStyle"/>,
    /// <see cref="SchedulerWeeklyPrintStyle"/>, <see cref="SchedulerMonthlyPrintStyle"/>,
    /// <see cref="SchedulerDetailsPrintStyle"/>): the date range to print, the fonts to print it in, and whether the
    /// page title is drawn. Majorsilence.Forms renders every style as the same agenda list restricted to the date
    /// range -- see <see cref="RadSchedulerPrintRenderer"/>. Compat for Telerik's <c>SchedulerPrintStyle</c>.
    /// </summary>
    /// <remarks>
    /// The range follows Telerik's (measured on UI.for.WinForms 2026.2.520): dates are whole days; a weekly style
    /// widens its range to whole weeks starting on Monday, ending at the Monday after; a monthly one to whole
    /// months, ending at 23:59:59 on the last day. A style made without dates covers today and the five days after.
    /// </remarks>
    public abstract class SchedulerPrintStyleBase
    {
        private DateTime date_start_range;
        private DateTime date_end_range;

        private protected SchedulerPrintStyleBase () : this (DateTime.Today, DateTime.Today.AddDays (5)) { }

        private protected SchedulerPrintStyleBase (DateTime startDate, DateTime endDate)
        {
            DateStartRange = startDate;
            DateEndRange = endDate;
        }

        /// <summary>Gets or sets the start of the date range to print.</summary>
        /// <remarks>Set to a date with a time, it keeps the date; a weekly or monthly style moves it back to the start of its week or month.</remarks>
        public DateTime DateStartRange {
            get => date_start_range;
            set => date_start_range = NormalizeStart (value);
        }

        /// <summary>Gets or sets the end of the date range to print.</summary>
        /// <remarks>Set to a date with a time, it keeps the date; a weekly style moves it on to the Monday after its week,
        /// a monthly one to 23:59:59 on the last day of its month. An end before the start is kept, as Telerik keeps it.</remarks>
        public DateTime DateEndRange {
            get => date_end_range;
            set => date_end_range = NormalizeEnd (value);
        }

        private protected virtual DateTime NormalizeStart (DateTime value) => value.Date;

        private protected virtual DateTime NormalizeEnd (DateTime value) => value.Date;

        /// <summary>Gets the first date included in the printed range (<see cref="DateStartRange"/>).</summary>
        public DateTime StartDate => DateStartRange;

        /// <summary>Gets the last date (inclusive) included in the printed range: the day <see cref="DateEndRange"/> falls on, or for a weekly style the day before it.</summary>
        public DateTime EndDate => LastPrintedDay;

        private protected virtual DateTime LastPrintedDay => DateEndRange.Date;

        private static readonly Majorsilence.Forms.Drawing.Font default_appointment_font = new ("Segoe UI", 9);
        private static readonly Majorsilence.Forms.Drawing.Font default_date_heading_font = new ("Segoe UI", 9, bold: true);
        private static readonly Majorsilence.Forms.Drawing.Font default_page_heading_font = new ("Segoe UI", 22, bold: true);

        private Majorsilence.Forms.Drawing.Font? appointment_font;
        private Majorsilence.Forms.Drawing.Font? date_heading_font;
        private Majorsilence.Forms.Drawing.Font? page_heading_font;

        /// <summary>Gets or sets the font appointments are printed in. Defaults to Segoe UI 9; null goes back to it.</summary>
        public Majorsilence.Forms.Drawing.Font AppointmentFont {
            get => appointment_font ?? default_appointment_font;
            set => appointment_font = value;
        }

        /// <summary>Gets or sets the font each day's heading is printed in. Defaults to Segoe UI 9 bold; null goes back to it.</summary>
        public Majorsilence.Forms.Drawing.Font DateHeadingFont {
            get => date_heading_font ?? default_date_heading_font;
            set => date_heading_font = value;
        }

        /// <summary>Gets or sets the font the page title is printed in. Defaults to Segoe UI 22 bold; null goes back to it.</summary>
        public Majorsilence.Forms.Drawing.Font PageHeadingFont {
            get => page_heading_font ?? default_page_heading_font;
            set => page_heading_font = value;
        }

        /// <summary>Gets or sets whether a page-title calendar band is drawn above the agenda list. Majorsilence.Forms instead draws a plain text title with the date range; the flag is honored as "draw a title" vs. "draw nothing".</summary>
        public bool DrawPageTitleCalendar { get; set; } = true;

        /// <summary>Gets the display name of this print style (used in the page title when <see cref="DrawPageTitleCalendar"/> is set).</summary>
        public abstract string StyleName { get; }
    }

    /// <summary>Prints a day-by-day agenda. Compat for Telerik <c>SchedulerDailyPrintStyle</c>.</summary>
    public sealed class SchedulerDailyPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering today and the five days after.</summary>
        public SchedulerDailyPrintStyle () { }

        /// <summary>Initializes a new instance covering the specified date range.</summary>
        public SchedulerDailyPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        /// <inheritdoc/>
        public override string StyleName => "Daily";
    }

    /// <summary>Prints whole weeks. Compat for Telerik <c>SchedulerWeeklyPrintStyle</c>.</summary>
    public sealed class SchedulerWeeklyPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering the weeks of today and the five days after.</summary>
        public SchedulerWeeklyPrintStyle () { }

        /// <summary>Initializes a new instance covering the weeks the specified dates fall in.</summary>
        public SchedulerWeeklyPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        // Telerik's printed weeks start on Monday whatever the culture (measured with a Sunday-first culture).
        private protected override DateTime NormalizeStart (DateTime value) => StartOfWeek (value);

        private protected override DateTime NormalizeEnd (DateTime value) => StartOfWeek (value).AddDays (7);

        private static DateTime StartOfWeek (DateTime value)
            => value.Date.AddDays (-(((int)value.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));

        private protected override DateTime LastPrintedDay => DateEndRange.Date.AddDays (-1);

        /// <inheritdoc/>
        public override string StyleName => "Weekly";
    }

    /// <summary>Prints whole months. Compat for Telerik <c>SchedulerMonthlyPrintStyle</c>.</summary>
    public sealed class SchedulerMonthlyPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering the month (or months) of today and the five days after.</summary>
        public SchedulerMonthlyPrintStyle () { }

        /// <summary>Initializes a new instance covering the months the specified dates fall in.</summary>
        public SchedulerMonthlyPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        private protected override DateTime NormalizeStart (DateTime value) => new (value.Year, value.Month, 1);

        private protected override DateTime NormalizeEnd (DateTime value)
            => new (value.Year, value.Month, DateTime.DaysInMonth (value.Year, value.Month), 23, 59, 59);

        /// <inheritdoc/>
        public override string StyleName => "Monthly";
    }

    /// <summary>Prints a detailed (timeline-oriented) agenda. Compat for Telerik <c>SchedulerDetailsPrintStyle</c>.</summary>
    public sealed class SchedulerDetailsPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering today and the five days after.</summary>
        public SchedulerDetailsPrintStyle () { }

        /// <summary>Initializes a new instance covering the specified date range.</summary>
        public SchedulerDetailsPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        /// <inheritdoc/>
        public override string StyleName => "Details";
    }

    /// <summary>
    /// Renders a <see cref="RadScheduler"/>'s agenda list to a <see cref="RadPrintDocument"/>'s
    /// <see cref="PrintDocument.PrintPage"/> pages, restricted to the scheduler's
    /// <see cref="RadScheduler.PrintStyle"/> date range (or the active view's range when unset), with an
    /// optional page title and diagonal watermark. Internal -- reached only through
    /// <see cref="RadPrintDocument.AssociatedObject"/>.
    /// </summary>
    /// <remarks>The print style is read when a job starts, not when the document is attached, as Telerik reads it:
    /// code that attaches the document and then sets the style (or its range or fonts) prints what it set.</remarks>
    internal static class RadSchedulerPrintRenderer
    {
        private const float RowHeight = 16f;
        private const float HeaderHeight = 20f;

        /// <summary>One printed line: its text, the font it is printed in, and what it is.</summary>
        internal readonly record struct Line (string Text, Majorsilence.Forms.Drawing.Font Font, LineKind Kind);

        internal enum LineKind { Title, DateHeading, Appointment }

        public static void Attach (RadPrintDocument document, RadScheduler? scheduler)
        {
            if (scheduler is null)
                return;

            List<Line>? lines = null;
            var lineIndex = 0;
            var pageNumber = 0;

            document.PrintPage += (_, e) => {
                // The first page of a job: lay the job out from the print style as it is now.
                if (lines is null) {
                    lines = Lines (scheduler, document);
                    lineIndex = 0;
                    pageNumber = 0;
                }

                pageNumber++;
                var g = e.SkiaGraphics;
                float top = e.MarginBounds.Top;

                // The title heads every page.
                if (lines.Count > 0 && lines[0].Kind == LineKind.Title) {
                    var title = lines[0];
                    var height = Math.Max (HeaderHeight, LineHeight (title.Font));
                    g.DrawString (title.Text, title.Font, Majorsilence.Forms.Drawing.Brushes.Black, new RectangleF (e.MarginBounds.Left, top, e.MarginBounds.Width, height), ContentAlignment.MiddleCenter);
                    top += height;

                    if (lineIndex == 0)
                        lineIndex = 1;
                }

                while (lineIndex < lines.Count && top + Math.Max (RowHeight, LineHeight (lines[lineIndex].Font)) <= e.MarginBounds.Bottom) {
                    var line = lines[lineIndex];
                    var height = Math.Max (RowHeight, LineHeight (line.Font));
                    g.DrawString (line.Text, line.Font, Majorsilence.Forms.Drawing.Brushes.Black, new RectangleF (e.MarginBounds.Left, top, e.MarginBounds.Width, height), ContentAlignment.MiddleLeft);
                    top += height;
                    lineIndex++;
                }

                if (document.Watermark is { Text.Length: > 0 } watermark)
                    DrawWatermark (g, watermark, e.PageBounds);

                var footer_rect = new RectangleF (e.MarginBounds.Left, e.MarginBounds.Bottom - RowHeight, e.MarginBounds.Width, RowHeight);
                g.DrawString ($"Page {pageNumber}", document.FooterFont, Majorsilence.Forms.Drawing.Brushes.Gray, footer_rect, ContentAlignment.MiddleRight);

                e.HasMorePages = lineIndex < lines.Count;

                // The job is done: the next one lays itself out again.
                if (!e.HasMorePages)
                    lines = null;
            };
        }

        // A line of a font, in the page's hundredths of an inch: the point size, with a quarter again for leading.
        private static float LineHeight (Majorsilence.Forms.Drawing.Font font) => font.Size / 72f * 100f * 1.25f;

        /// <summary>The lines a job prints for the scheduler's print style: the title (when drawn), then each day's heading and appointments.</summary>
        /// <remarks>Without a print style it prints the active view's range in the document's header font, as before print styles had fonts.</remarks>
        internal static List<Line> Lines (RadScheduler scheduler, RadPrintDocument document)
        {
            var style = scheduler.PrintStyle;
            var start = style?.StartDate ?? scheduler.ActiveView.StartDate;
            var end = style?.EndDate ?? scheduler.ActiveView.EndDate;
            var lines = new List<Line> ();

            if (style?.DrawPageTitleCalendar ?? true)
                lines.Add (new Line ($"{style?.StyleName ?? "Agenda"}: {start:d} - {end:d}", style?.PageHeadingFont ?? document.HeaderFont, LineKind.Title));

            var grouped = scheduler.Appointments
                .Where (a => a.Start.Date <= end.Date && a.End.Date >= start.Date)
                .OrderBy (a => a.Start)
                .GroupBy (a => a.Start.Date)
                .OrderBy (g => g.Key);

            var heading_font = style?.DateHeadingFont ?? new Majorsilence.Forms.Drawing.Font (document.HeaderFont.FamilyName, document.HeaderFont.Size, bold: true);
            var appointment_font = style?.AppointmentFont ?? document.HeaderFont;

            foreach (var day in grouped) {
                lines.Add (new Line (day.Key.ToString ("D", System.Globalization.CultureInfo.CurrentCulture), heading_font, LineKind.DateHeading));
                foreach (var a in day)
                    lines.Add (new Line ($"{a.Start:t} - {a.End:t}   {a.Summary}", appointment_font, LineKind.Appointment));
            }

            return lines;
        }

        private static void DrawWatermark (Drawing.SkiaGraphics g, RadPrintWatermark watermark, RectangleF pageBounds)
        {
            var state = g.Save ();
            try {
                var brush = new Majorsilence.Forms.Drawing.SolidBrush (Color.FromArgb ((int)(watermark.Opacity * 255), watermark.Color));
                var center_x = pageBounds.Width / 2f;
                var center_y = pageBounds.Height / 2f;

                g.TranslateTransform (center_x, center_y);
                g.RotateTransform (-45);

                var text_rect = new RectangleF (-pageBounds.Width, -pageBounds.Height / 8f, pageBounds.Width * 2, pageBounds.Height / 4f);
                g.DrawString (watermark.Text, watermark.Font, brush, text_rect, ContentAlignment.MiddleCenter);
            } finally {
                g.Restore (state);
            }
        }
    }

    /// <summary>
    /// Minimal print-settings dialog: date range fields plus OK/Cancel. Following the same lower-risk
    /// pattern as <see cref="Majorsilence.Forms.PrintDialog"/>/<see cref="Majorsilence.Forms.PageSetupDialog"/>
    /// (compile-and-approximate — see the header comment in <c>RadInfrastructure.cs</c>), this extends
    /// <see cref="Form"/> for API shape compatibility but <see cref="ShowDialog()"/> does not display any UI:
    /// it applies <see cref="StartDate"/>/<see cref="EndDate"/> to <see cref="PrintDocument"/>'s associated
    /// scheduler's <see cref="RadScheduler.PrintStyle"/> range (when both are set) and returns
    /// <see cref="DialogResult.OK"/> immediately. No Form is ever shown, so — unlike a real dialog — nothing
    /// here can leak into <see cref="Application.OpenForms"/>.
    /// </summary>
    public class SchedulerPrintSettingsDialog : Form
    {
        /// <summary>Gets the root element of the dialog (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the print document being configured.</summary>
        public RadPrintDocument? PrintDocument { get; set; }

        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        /// <summary>Gets or sets the start of the date range to print.</summary>
        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Gets or sets the end of the date range to print.</summary>
        public DateTime EndDate { get; set; } = DateTime.Today;

        /// <summary>
        /// "Shows" the dialog (no UI is actually displayed — see remarks on <see cref="SchedulerPrintSettingsDialog"/>)
        /// and returns <see cref="DialogResult.OK"/>.
        /// </summary>
        public new DialogResult ShowDialog () => DialogResult.OK;

        /// <summary>The awaitable form of <see cref="ShowDialog()"/>: completes at once with OK, as that
        /// returns it. Declared so an awaited call does not show this UI-less dialog as an empty form.</summary>
        public new Task<DialogResult> ShowDialogAsync () => Task.FromResult (ShowDialog ());

        /// <inheritdoc cref="ShowDialogAsync()"/>
        public new Task<DialogResult> ShowDialogAsync (IWin32Window owner) => ShowDialogAsync ();

        /// <inheritdoc cref="ShowDialogAsync()"/>
        public new Task<DialogResult> ShowDialogAsync (Form? parent) => ShowDialogAsync ();
    }
}
