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
    /// <see cref="SchedulerDetailsPrintStyle"/>): a date range plus whether the page header should show a
    /// mini calendar title. Majorsilence.Forms renders every style as the same agenda list restricted to
    /// this date range — see <see cref="RadSchedulerPrintRenderer"/>.
    /// </summary>
    public abstract class SchedulerPrintStyleBase
    {
        private protected SchedulerPrintStyleBase (DateTime startDate, DateTime endDate)
        {
            StartDate = startDate;
            EndDate = endDate;
        }

        /// <summary>Gets the first date included in the printed range.</summary>
        public DateTime StartDate { get; }

        /// <summary>Gets the last date (inclusive) included in the printed range.</summary>
        public DateTime EndDate { get; }

        /// <summary>Gets or sets whether a page-title calendar band is drawn above the agenda list. Majorsilence.Forms instead draws a plain text title with the date range; the flag is honored as "draw a title" vs. "draw nothing".</summary>
        public bool DrawPageTitleCalendar { get; set; } = true;

        /// <summary>Gets the display name of this print style (used in the page title when <see cref="DrawPageTitleCalendar"/> is set).</summary>
        public abstract string StyleName { get; }
    }

    /// <summary>Prints a single day's agenda. Compat for Telerik <c>SchedulerDailyPrintStyle</c>.</summary>
    public sealed class SchedulerDailyPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering today only.</summary>
        public SchedulerDailyPrintStyle () : base (DateTime.Today, DateTime.Today) { }

        /// <summary>Initializes a new instance covering the specified date range.</summary>
        public SchedulerDailyPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        /// <inheritdoc/>
        public override string StyleName => "Daily";
    }

    /// <summary>Prints a week's agenda. Compat for Telerik <c>SchedulerWeeklyPrintStyle</c>.</summary>
    public sealed class SchedulerWeeklyPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering the current week.</summary>
        public SchedulerWeeklyPrintStyle () : base (DateTime.Today, DateTime.Today.AddDays (6)) { }

        /// <summary>Initializes a new instance covering the specified date range.</summary>
        public SchedulerWeeklyPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        /// <inheritdoc/>
        public override string StyleName => "Weekly";
    }

    /// <summary>Prints a month's agenda. Compat for Telerik <c>SchedulerMonthlyPrintStyle</c>.</summary>
    public sealed class SchedulerMonthlyPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering the current month.</summary>
        public SchedulerMonthlyPrintStyle () : base (
            new DateTime (DateTime.Today.Year, DateTime.Today.Month, 1),
            new DateTime (DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths (1).AddDays (-1)) { }

        /// <summary>Initializes a new instance covering the specified date range.</summary>
        public SchedulerMonthlyPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        /// <inheritdoc/>
        public override string StyleName => "Monthly";
    }

    /// <summary>Prints a detailed (timeline-oriented) agenda. Compat for Telerik <c>SchedulerDetailsPrintStyle</c>.</summary>
    public sealed class SchedulerDetailsPrintStyle : SchedulerPrintStyleBase
    {
        /// <summary>Initializes a new instance covering the specified date range.</summary>
        public SchedulerDetailsPrintStyle (DateTime startDate, DateTime endDate) : base (startDate, endDate) { }

        /// <inheritdoc/>
        public override string StyleName => "Details";
    }

    /// <summary>
    /// Renders a <see cref="RadScheduler"/>'s agenda list to a <see cref="RadPrintDocument"/>'s
    /// <see cref="PrintDocument.PrintPage"/> pages, restricted to the scheduler's current
    /// <see cref="RadScheduler.PrintStyle"/> date range (or the active view's range when unset), with an
    /// optional page title and diagonal watermark. Internal — reached only through
    /// <see cref="RadPrintDocument.AssociatedObject"/>.
    /// </summary>
    internal static class RadSchedulerPrintRenderer
    {
        private const float RowHeight = 16f;
        private const float HeaderHeight = 20f;

        public static void Attach (RadPrintDocument document, RadScheduler? scheduler)
        {
            if (scheduler is null)
                return;

            var start = scheduler.PrintStyle?.StartDate ?? scheduler.ActiveView.StartDate;
            var end = scheduler.PrintStyle?.EndDate ?? scheduler.ActiveView.EndDate;
            var drawTitle = scheduler.PrintStyle?.DrawPageTitleCalendar ?? true;
            var styleName = scheduler.PrintStyle?.StyleName ?? "Agenda";

            var rows = BuildRows (scheduler, start, end);
            var rowIndex = 0;
            var pageNumber = 0;

            document.PrintPage += (_, e) => {
                pageNumber++;
                var g = e.Graphics;
                var top = e.MarginBounds.Top;

                if (drawTitle) {
                    var title = $"{styleName}: {start:d} - {end:d}";
                    var title_rect = new RectangleF (e.MarginBounds.Left, top, e.MarginBounds.Width, HeaderHeight);
                    g.DrawString (title, document.HeaderFont, Majorsilence.Forms.Drawing.Brushes.Black, title_rect, ContentAlignment.MiddleCenter);
                    top += HeaderHeight;
                }

                while (rowIndex < rows.Count && top + RowHeight <= e.MarginBounds.Bottom) {
                    var (text, isHeader) = rows[rowIndex];
                    var font = isHeader ? new Majorsilence.Forms.Drawing.Font (document.HeaderFont.FamilyName, document.HeaderFont.Size, bold: true) : document.HeaderFont;
                    var row_rect = new RectangleF (e.MarginBounds.Left, top, e.MarginBounds.Width, RowHeight);
                    g.DrawString (text, font, Majorsilence.Forms.Drawing.Brushes.Black, row_rect, ContentAlignment.MiddleLeft);
                    top += RowHeight;
                    rowIndex++;
                }

                if (document.Watermark is { Text.Length: > 0 } watermark)
                    DrawWatermark (g, watermark, e.PageBounds);

                var footer_rect = new RectangleF (e.MarginBounds.Left, e.MarginBounds.Bottom - RowHeight, e.MarginBounds.Width, RowHeight);
                g.DrawString ($"Page {pageNumber}", document.FooterFont, Majorsilence.Forms.Drawing.Brushes.Gray, footer_rect, ContentAlignment.MiddleRight);

                e.HasMorePages = rowIndex < rows.Count;
            };
        }

        private static List<(string Text, bool IsHeader)> BuildRows (RadScheduler scheduler, DateTime start, DateTime end)
        {
            var rows = new List<(string, bool)> ();

            var grouped = scheduler.Appointments
                .Where (a => a.Start.Date <= end.Date && a.End.Date >= start.Date)
                .OrderBy (a => a.Start)
                .GroupBy (a => a.Start.Date)
                .OrderBy (g => g.Key);

            foreach (var day in grouped) {
                rows.Add ((day.Key.ToString ("D", System.Globalization.CultureInfo.CurrentCulture), true));
                foreach (var a in day)
                    rows.Add (($"{a.Start:t} - {a.End:t}   {a.Summary}", false));
            }

            return rows;
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
    }
}
