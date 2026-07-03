using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    // Event args, view-type compat carriers, and the agenda-list rendering control backing RadScheduler
    // (see RadScheduler.cs). Kept in a separate file so RadScheduler.cs itself stays focused on the
    // scheduler's own public surface.

    /// <summary>Immutable snapshot of a scheduler view's type and date range, used as the <c>OldView</c>/<c>NewView</c> payload of <see cref="SchedulerViewChangedEventArgs"/>.</summary>
    public class SchedulerViewSnapshot
    {
        /// <summary>Initializes a new instance.</summary>
        public SchedulerViewSnapshot (SchedulerViewType viewType, DateTime startDate, DateTime endDate)
        {
            ViewType = viewType;
            StartDate = startDate;
            EndDate = endDate;
        }

        /// <summary>Gets the view type.</summary>
        public SchedulerViewType ViewType { get; }

        /// <summary>Gets the view's first date.</summary>
        public DateTime StartDate { get; }

        /// <summary>Gets the view's last date.</summary>
        public DateTime EndDate { get; }
    }

    /// <summary>Provides data for <see cref="RadScheduler.ActiveViewChanged"/>.</summary>
    public class SchedulerViewChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SchedulerViewChangedEventArgs (SchedulerViewSnapshot oldView, SchedulerViewSnapshot newView)
        {
            OldView = oldView;
            NewView = newView;
        }

        /// <summary>Gets the view before the change.</summary>
        public SchedulerViewSnapshot OldView { get; }

        /// <summary>Gets the view after the change.</summary>
        public SchedulerViewSnapshot NewView { get; }
    }

    /// <summary>Provides data for <see cref="RadScheduler.ViewNavigated"/>.</summary>
    public class SchedulerViewNavigatedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SchedulerViewNavigatedEventArgs (DateTime oldStartDate, DateTime newStartDate)
        {
            OldStartDate = oldStartDate;
            NewStartDate = newStartDate;
        }

        /// <summary>Gets the start date before navigation.</summary>
        public DateTime OldStartDate { get; }

        /// <summary>Gets the start date after navigation.</summary>
        public DateTime NewStartDate { get; }
    }

    /// <summary>Provides data for <see cref="RadScheduler.AppointmentSelecting"/>.</summary>
    public class SchedulerAppointmentCancelEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SchedulerAppointmentCancelEventArgs (Appointment appointment) => Appointment = appointment;

        /// <summary>Gets the appointment being selected.</summary>
        public Appointment Appointment { get; }

        /// <summary>Gets or sets whether the selection should be canceled.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>Provides data for <see cref="RadScheduler.ScreenTipNeeded"/>.</summary>
    public class ScreenTipNeededEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ScreenTipNeededEventArgs (AppointmentElement item) => Item = item;

        /// <summary>Gets the appointment element the screen tip is being requested for.</summary>
        public AppointmentElement Item { get; }
    }

    /// <summary>
    /// Lightweight compat stand-in for a Telerik <c>AppointmentElement</c> — the visual element hosting a
    /// single appointment in the scheduler's view, reached via <see cref="ScreenTipNeededEventArgs.Item"/>.
    /// </summary>
    public class AppointmentElement : RadElement
    {
        /// <summary>Initializes a new instance wrapping the specified appointment.</summary>
        public AppointmentElement (Appointment appointment) => Appointment = appointment;

        /// <summary>Gets the appointment this element represents.</summary>
        public Appointment Appointment { get; }

        /// <summary>Gets or sets the tool tip text shown for this element.</summary>
        public string ToolTipText { get; set; } = string.Empty;
    }

    /// <summary>Handles editing (or creating) an appointment for <see cref="AppointmentEditDialogShowingEventArgs.AppointmentEditDialog"/>.</summary>
    public class AppointmentEditDialog
    {
        /// <summary>Edits the specified appointment (or creates a new one when null). Stub — no dialog UI is implemented; the real Telerik dialog is replaced by the audited pattern of canceling and handling appointment editing at the application level.</summary>
        public void EditAppointment (Appointment? appointment, object? recurrenceEditMode) { }
    }

    /// <summary>Provides data for <see cref="RadScheduler.AppointmentEditDialogShowing"/>.</summary>
    public class AppointmentEditDialogShowingEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public AppointmentEditDialogShowingEventArgs (Appointment? appointment) => Appointment = appointment;

        /// <summary>Gets the appointment about to be edited, or null when creating a new one.</summary>
        public Appointment? Appointment { get; }

        /// <summary>Gets or sets whether the default edit dialog should be suppressed.</summary>
        public bool Cancel { get; set; }

        /// <summary>Gets the edit dialog handle passed to handlers that cancel the default UI and drive editing themselves.</summary>
        public AppointmentEditDialog AppointmentEditDialog { get; } = new ();
    }

    /// <summary>Provides data for <see cref="RadScheduler.ContextMenuOpening"/>.</summary>
    public class SchedulerContextMenuOpeningEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public SchedulerContextMenuOpeningEventArgs (RadContextMenu contextMenu) => ContextMenu = contextMenu;

        /// <summary>Gets the context menu about to be shown.</summary>
        public RadContextMenu ContextMenu { get; }

        /// <summary>Gets or sets whether the context menu should be suppressed.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>Compat carrier returned by <see cref="RadScheduler.GetMonthView"/>. Only <see cref="WeekCount"/>/<see cref="ShowWeekend"/> are read/written by audited usage; the month grid itself is not rendered.</summary>
    public class SchedulerMonthView
    {
        /// <summary>Gets or sets the number of weeks shown. Stored — not rendered.</summary>
        public int WeekCount { get; set; } = 5;

        /// <summary>Gets or sets whether weekend days are shown. Stored — not rendered.</summary>
        public bool ShowWeekend { get; set; } = true;
    }

    /// <summary>Compat carrier returned by <see cref="RadScheduler.GetWeekView"/> (see <see cref="SchedulerMonthView"/> remarks).</summary>
    public class SchedulerWeekView
    {
        /// <summary>Gets or sets whether weekend days are shown. Stored — not rendered.</summary>
        public bool ShowWeekend { get; set; } = true;
    }

    /// <summary>Compat carrier returned by <see cref="RadScheduler.GetDayView"/> (see <see cref="SchedulerMonthView"/> remarks).</summary>
    public class SchedulerDayView
    {
        /// <summary>Gets or sets the number of days shown. Stored — not rendered.</summary>
        public int DayCount { get; set; } = 1;
    }

    /// <summary>Compat carrier returned by <see cref="RadScheduler.GetTimelineView"/> (see <see cref="SchedulerMonthView"/> remarks).</summary>
    public class SchedulerTimelineView
    {
        private SchedulerTimescale? _scaling;

        /// <summary>Returns the timeline's scaling settings (created on first access).</summary>
        public SchedulerTimescale GetScaling () => _scaling ??= new SchedulerTimescale ();

        /// <summary>Sets the timescale unit shown by the timeline. Stored — not rendered.</summary>
        public void ShowTimescale (Timescales timescale) => GetScaling ().Unit = timescale;
    }

    /// <summary>Compat carrier for a scheduler timeline's scaling settings (<c>GetTimelineView().GetScaling()</c>).</summary>
    public class SchedulerTimescale
    {
        /// <summary>Gets or sets the number of cells displayed at once. Stored — not rendered.</summary>
        public int DisplayedCellsCount { get; set; } = 1;

        /// <summary>Gets or sets the timescale unit. Stored — not rendered.</summary>
        public Timescales Unit { get; set; } = Timescales.Days;
    }

    /// <summary>Specifies the unit a scheduler timeline view scales by. Compat for Telerik Timescales.</summary>
    public enum Timescales
    {
        /// <summary>Hour granularity.</summary>
        Hours = 0,
        /// <summary>Day granularity.</summary>
        Days = 1,
        /// <summary>Week granularity.</summary>
        Weeks = 2,
        /// <summary>Month granularity.</summary>
        Months = 3,
    }

    /// <summary>Marker type selecting the iCalendar (.ics) exporter for <see cref="RadScheduler.Export(System.IO.Stream, object)"/>.</summary>
    public class SchedulerICalendarExporter
    {
    }

    // Writes a minimal, real iCalendar (RFC 5545) document — enough for the exported .ics to be opened by
    // any calendar app, mirroring frmShedular.vb's "Export" button (SaveFileDialog + .ics filter).
    internal static class SchedulerICalendarWriter
    {
        public static void Write (System.IO.Stream stream, IEnumerable<Appointment> appointments)
        {
            using var writer = new System.IO.StreamWriter (stream, System.Text.Encoding.UTF8, 1024, leaveOpen: true);
            writer.WriteLine ("BEGIN:VCALENDAR");
            writer.WriteLine ("VERSION:2.0");
            writer.WriteLine ("PRODID:-//Majorsilence.Forms//RadScheduler Compat//EN");

            foreach (var a in appointments) {
                writer.WriteLine ("BEGIN:VEVENT");
                writer.WriteLine ($"DTSTART:{FormatDate (a.Start)}");
                writer.WriteLine ($"DTEND:{FormatDate (a.End)}");
                writer.WriteLine ($"SUMMARY:{Escape (a.Summary)}");
                if (!string.IsNullOrEmpty (a.Description))
                    writer.WriteLine ($"DESCRIPTION:{Escape (a.Description)}");
                if (!string.IsNullOrEmpty (a.Location))
                    writer.WriteLine ($"LOCATION:{Escape (a.Location)}");
                writer.WriteLine ("END:VEVENT");
            }

            writer.WriteLine ("END:VCALENDAR");
        }

        private static string FormatDate (DateTime dt) => dt.ToUniversalTime ().ToString ("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);

        private static string Escape (string text) => text.Replace ("\\", "\\\\").Replace (";", "\\;").Replace (",", "\\,").Replace ("\n", "\\n");
    }

    /// <summary>
    /// Renders <see cref="RadScheduler"/>'s agenda view: appointments grouped by day (day headers followed
    /// by that day's appointments sorted by start time), scrollable via the owning <see cref="ScrollableControl"/>.
    /// A private implementation detail of <see cref="RadScheduler"/> (composed as its sole child), not part
    /// of the Telerik-compat public surface.
    /// </summary>
    internal sealed class SchedulerAgendaList : Control
    {
        private const int HeaderHeight = 24;
        private const int RowHeight = 32;

        private readonly List<(DateTime Day, Appointment? Appointment)> _rows = new ();

        public SchedulerAgendaList () => SetControlBehavior (ControlBehaviors.Selectable, false);

        protected override Size DefaultSize => new Size (400, 300);

        public event EventHandler<Appointment>? AppointmentActivated;
        public event EventHandler<Appointment>? AppointmentSelecting;
        public event EventHandler<Appointment>? ScreenTipNeeded;

        public void SetAppointments (IEnumerable<Appointment> appointments, DateTime rangeStart, DateTime rangeEnd)
        {
            _rows.Clear ();

            var grouped = appointments
                .Where (a => a.Start.Date <= rangeEnd.Date && a.End.Date >= rangeStart.Date)
                .OrderBy (a => a.Start)
                .GroupBy (a => a.Start.Date)
                .OrderBy (g => g.Key);

            foreach (var day in grouped) {
                _rows.Add ((day.Key, null)); // day header row
                foreach (var appointment in day)
                    _rows.Add ((day.Key, appointment));
            }

            if (Parent is ScrollableControl scrollable)
                scrollable.AutoScrollMinSize = new Size (0, _rows.Count * RowHeight);

            Height = Math.Max (DefaultSize.Height, _rows.Sum (r => r.Appointment is null ? HeaderHeight : RowHeight));
            Invalidate ();
        }

        private Rectangle RowBounds (int index)
        {
            var top = 0;
            for (var i = 0; i < index; i++)
                top += _rows[i].Appointment is null ? HeaderHeight : RowHeight;

            var height = _rows[index].Appointment is null ? HeaderHeight : RowHeight;
            return new Rectangle (0, top, Width, height);
        }

        private int RowAt (Point location)
        {
            var top = 0;
            for (var i = 0; i < _rows.Count; i++) {
                var height = _rows[i].Appointment is null ? HeaderHeight : RowHeight;
                if (location.Y >= top && location.Y < top + height)
                    return i;
                top += height;
            }
            return -1;
        }

        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            var index = RowAt (e.Location);
            if (index >= 0 && _rows[index].Appointment is Appointment appointment)
                AppointmentSelecting?.Invoke (this, appointment);
        }

        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            var index = RowAt (e.Location);
            if (index >= 0 && _rows[index].Appointment is Appointment appointment)
                AppointmentActivated?.Invoke (this, appointment);
        }

        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            var index = RowAt (e.Location);
            if (index >= 0 && _rows[index].Appointment is Appointment appointment)
                ScreenTipNeeded?.Invoke (this, appointment);
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (_rows.Count == 0) {
                e.Canvas.DrawText ("No appointments.", ClientRectangle, this, ContentAlignment.MiddleCenter);
                return;
            }

            for (var i = 0; i < _rows.Count; i++) {
                var bounds = RowBounds (i);
                if (bounds.Bottom < 0 || bounds.Top > Height)
                    continue;

                var (day, appointment) = _rows[i];

                if (appointment is null) {
                    e.Canvas.FillRectangle (bounds, Theme.ControlMidColor);
                    var text_bounds = bounds;
                    text_bounds.Inflate (-6, 0);
                    e.Canvas.DrawText (day.ToString ("D", System.Globalization.CultureInfo.CurrentCulture), text_bounds, this, ContentAlignment.MiddleLeft, maxLines: 1);
                } else {
                    var row_bounds = bounds;
                    row_bounds.Inflate (-6, -2);

                    var time_bounds = new Rectangle (row_bounds.Left, row_bounds.Top, 130, row_bounds.Height);
                    var text = $"{appointment.Start:t} - {appointment.End:t}";
                    e.Canvas.DrawText (text, time_bounds, this, ContentAlignment.MiddleLeft, maxLines: 1);

                    var summary_bounds = new Rectangle (time_bounds.Right + 8, row_bounds.Top, row_bounds.Width - time_bounds.Width - 8, row_bounds.Height);
                    e.Canvas.DrawText (appointment.Summary, summary_bounds, this, ContentAlignment.MiddleLeft, maxLines: 1, ellipsis: true);

                    e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1, Theme.BorderLowColor);
                }
            }
        }
    }
}
