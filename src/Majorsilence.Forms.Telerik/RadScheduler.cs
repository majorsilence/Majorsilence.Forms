using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat scheduler. Renders a scrollable <b>agenda view</b> — appointments grouped by day,
    /// most-recent-first within a day sorted by start time — rather than the full Telerik month/week/day
    /// calendar grid (deliberately out of scope; see <c>BACKLOG.md</c>). Consumes a
    /// <see cref="SchedulerBindingDataSource"/> via <see cref="DataSource"/> (materializing appointments from
    /// a bound <see cref="System.Data.DataTable"/>) or a plain, directly-populated <see cref="Appointments"/>
    /// list (the pattern <c>frmShedular.vb</c> uses via <c>Appointments.Add</c>/<c>AddRange</c>). Printing is
    /// covered by <see cref="Print(bool, RadPrintDocument)"/>/<see cref="PrintPreview(RadPrintDocument)"/>
    /// (see <c>RadSchedulerPrinting.cs</c>).
    /// </summary>
    public class RadScheduler : ScrollableControl, ISupportInitializeCompat
    {
        private readonly SchedulerAgendaList _agenda;
        private SchedulerBindingDataSource? _dataSource;
        private SchedulerViewType _activeViewType = SchedulerViewType.Week;

        /// <summary>Initializes a new instance of the <see cref="RadScheduler"/> class.</summary>
        public RadScheduler ()
        {
            AutoScroll = true;

            _agenda = Controls.AddImplicitControl (new SchedulerAgendaList { Dock = DockStyle.Fill });
            _agenda.AppointmentActivated += (_, appt) => AppointmentElementDoubleClick?.Invoke (this, EventArgs.Empty);
            _agenda.AppointmentSelecting += (_, appt) => AppointmentSelecting?.Invoke (this, new SchedulerAppointmentCancelEventArgs (appt));
            _agenda.ScreenTipNeeded += (_, appt) => ScreenTipNeeded?.Invoke (this, new ScreenTipNeededEventArgs (new AppointmentElement (appt)));

            ActiveView = new SchedulerActiveView (this);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 400);

        // ── Data binding ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the <see cref="SchedulerBindingDataSource"/> feeding this scheduler's
        /// <see cref="Appointments"/>. Setting this replaces the appointment list with the data source's
        /// current <see cref="SchedulerBindingDataSource.Appointments"/> and keeps it refreshed whenever the
        /// data source's underlying table changes.
        /// </summary>
        public SchedulerBindingDataSource? DataSource {
            get => _dataSource;
            set {
                if (ReferenceEquals (_dataSource, value))
                    return;

                if (_dataSource != null)
                    _dataSource.DataSourceChanged -= OnBoundDataSourceChanged;

                _dataSource = value;

                if (_dataSource != null)
                    _dataSource.DataSourceChanged += OnBoundDataSourceChanged;

                SyncFromDataSource ();
            }
        }

        private void OnBoundDataSourceChanged (object? sender, EventArgs e) => SyncFromDataSource ();

        private void SyncFromDataSource ()
        {
            Appointments.Clear ();
            if (_dataSource != null)
                Appointments.AddRange (_dataSource.Appointments);
            RefreshAgenda ();
        }

        /// <summary>
        /// Gets the appointments shown by this scheduler. When bound via <see cref="DataSource"/> this list
        /// is kept in sync with the data source; it can also be populated directly
        /// (<c>Appointments.Add</c>/<c>AddRange</c>), the pattern used when a scheduler is not
        /// data-bound (<c>frmShedular.vb</c>'s <c>addTaskAppointments</c>-style helpers).
        /// </summary>
        public SchedulerAppointmentCollection Appointments { get; } = new ();

        /// <summary>Writes an appointment's current field values back to the bound <see cref="DataSource"/> (no-op when not data-bound or the appointment has no originating row).</summary>
        public void CommitAppointment (Appointment appointment) => _dataSource?.WriteBack (appointment);

        /// <summary>Refreshes the agenda view from the current <see cref="Appointments"/> list.</summary>
        public void RefreshAgenda () => _agenda.SetAppointments (Appointments, ActiveView.StartDate, ActiveView.EndDate);

        // ── View ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Gets or sets the active view (only the agenda list is implemented; see <see cref="SchedulerActiveView"/>).</summary>
        public SchedulerActiveView ActiveView { get; private set; }

        /// <summary>Gets or sets the active view type. Compat property — every view type renders as the same agenda list, filtered to the type's usual date range.</summary>
        public SchedulerViewType ActiveViewType {
            get => _activeViewType;
            set {
                if (_activeViewType == value)
                    return;

                var oldView = new SchedulerViewSnapshot (_activeViewType, ActiveView.StartDate, ActiveView.EndDate);
                _activeViewType = value;
                ActiveView.ApplyViewType (value);
                var newView = new SchedulerViewSnapshot (_activeViewType, ActiveView.StartDate, ActiveView.EndDate);

                RefreshAgenda ();
                ActiveViewChanged?.Invoke (this, new SchedulerViewChangedEventArgs (oldView, newView));
            }
        }

        /// <summary>Gets or sets whether inline appointment creation is allowed. Stored for API compatibility — the agenda view is read/print-oriented and does not implement inline creation UI.</summary>
        public bool AllowAppointmentCreateInline { get; set; } = true;

        /// <summary>Gets or sets the appointment title format string (Telerik composite-format compat, e.g. <c>"{2}"</c> for the summary). Applied by <see cref="AppointmentElement.ToolTipText"/>'s default and the agenda row's displayed title when non-empty.</summary>
        public string AppointmentTitleFormat { get; set; } = string.Empty;

        /// <summary>Gets or sets the date/time format provider used for week/month boundary calculations. Defaults to the current culture's format.</summary>
        public System.Globalization.DateTimeFormatInfo DateTimeFormat { get; set; } = System.Globalization.DateTimeFormatInfo.CurrentInfo;

        /// <summary>Gets or sets the theme name. No-op stub — Majorsilence.Forms has no Telerik theme concept.</summary>
        public string ThemeName { get; set; } = string.Empty;

        /// <summary>Gets or sets the currently active print style (date range + <see cref="SchedulerPrintStyleBase.DrawPageTitleCalendar"/>), consumed by <see cref="Print(bool, RadPrintDocument)"/>/<see cref="PrintPreview(RadPrintDocument)"/>.</summary>
        public SchedulerPrintStyleBase? PrintStyle { get; set; }

        /// <summary>Returns a month-view compat object. The agenda list is the only rendered view; this exists so migrated code compiles and can still read/write <see cref="SchedulerMonthView.WeekCount"/>/<see cref="SchedulerMonthView.ShowWeekend"/>.</summary>
        public SchedulerMonthView GetMonthView () => _monthView ??= new SchedulerMonthView ();
        private SchedulerMonthView? _monthView;

        /// <summary>Returns a week-view compat object (see <see cref="GetMonthView"/> remarks).</summary>
        public SchedulerWeekView GetWeekView () => _weekView ??= new SchedulerWeekView ();
        private SchedulerWeekView? _weekView;

        /// <summary>Returns a timeline-view compat object (see <see cref="GetMonthView"/> remarks).</summary>
        public SchedulerTimelineView GetTimelineView () => _timelineView ??= new SchedulerTimelineView ();
        private SchedulerTimelineView? _timelineView;

        /// <summary>Returns a day-view compat object (see <see cref="GetMonthView"/> remarks).</summary>
        public SchedulerDayView GetDayView () => _dayView ??= new SchedulerDayView ();
        private SchedulerDayView? _dayView;

        /// <summary>
        /// Exports the scheduler's appointments using the specified exporter (e.g. an iCalendar exporter).
        /// Majorsilence.Forms implements the iCalendar (.ics) format used by Financial's scheduler export
        /// (<see cref="SchedulerICalendarExporter"/>); other exporter types are accepted for API
        /// compatibility but write nothing.
        /// </summary>
        public void Export (System.IO.Stream stream, object exporter)
        {
            if (exporter is SchedulerICalendarExporter)
                SchedulerICalendarWriter.Write (stream, Appointments);
        }

        // ── Events ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>Raised when the active view or its date range changes.</summary>
        public event EventHandler<SchedulerViewChangedEventArgs>? ActiveViewChanged;

        /// <summary>Raised when a screen tip is needed for an appointment under the pointer.</summary>
        public event EventHandler<ScreenTipNeededEventArgs>? ScreenTipNeeded;

        /// <summary>Raised when the appointment edit dialog is about to be shown. Handlers may set <see cref="AppointmentEditDialogShowingEventArgs.Cancel"/> to suppress it.</summary>
        public event EventHandler<AppointmentEditDialogShowingEventArgs>? AppointmentEditDialogShowing;

        /// <summary>Raised when the scheduler's context menu is opening. Handlers may set <see cref="SchedulerContextMenuOpeningEventArgs.Cancel"/> to suppress it.</summary>
        public event EventHandler<SchedulerContextMenuOpeningEventArgs>? ContextMenuOpening;

        /// <summary>Raised after navigation changes the active date range.</summary>
        public event EventHandler<SchedulerViewNavigatedEventArgs>? ViewNavigated;

        /// <summary>Raised when an appointment is being selected; handlers may cancel via <see cref="SchedulerAppointmentCancelEventArgs.Cancel"/>.</summary>
        public event EventHandler<SchedulerAppointmentCancelEventArgs>? AppointmentSelecting;

        /// <summary>Raised when an appointment is activated (double-clicked).</summary>
        public event EventHandler? AppointmentElementDoubleClick;

        /// <summary>Requests that the appointment edit dialog be shown for the given appointment (or a new one when null), raising <see cref="AppointmentEditDialogShowing"/> first so handlers can cancel/redirect it.</summary>
        public void ShowAppointmentEditDialog (Appointment? appointment)
        {
            var e = new AppointmentEditDialogShowingEventArgs (appointment);
            AppointmentEditDialogShowing?.Invoke (this, e);
        }

        /// <summary>Requests that the scheduler's context menu be shown, raising <see cref="ContextMenuOpening"/> first so handlers can cancel/populate it.</summary>
        public SchedulerContextMenuOpeningEventArgs RaiseContextMenuOpening (RadContextMenu contextMenu)
        {
            var e = new SchedulerContextMenuOpeningEventArgs (contextMenu);
            ContextMenuOpening?.Invoke (this, e);
            return e;
        }

        // Raised by SchedulerActiveView after a NavigateBackward/Forward/Today call changes StartDate.
        internal void RaiseViewNavigated (DateTime oldStart, DateTime newStart)
            => ViewNavigated?.Invoke (this, new SchedulerViewNavigatedEventArgs (oldStart, newStart));

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            Renderers.RenderManager.Render (this, e);
        }

        // ── Printing entry points (see RadSchedulerPrinting.cs for RadPrintDocument/print styles) ─────

        /// <summary>
        /// Prints the agenda for the date range implied by <see cref="PrintStyle"/> (defaulting to
        /// <see cref="ActiveView"/>'s current range when no print style is set) through
        /// <paramref name="document"/>'s PDF pipeline. When <paramref name="showDialog"/> is true, the
        /// document is opened in the system's default viewer after rendering (there is no native print
        /// spooler in Majorsilence.Forms — see <c>PrintPreviewDialog</c>'s equivalent behavior).
        /// </summary>
        public void Print (bool showDialog, RadPrintDocument document)
        {
            ArgumentNullException.ThrowIfNull (document);

            document.AssociatedObject = this;
            var path = document.Print ();

            if (showDialog && WebViewSupport.AllowSystemViewerFallback)
                System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (path) { UseShellExecute = true });
        }

        /// <summary>Renders and opens a print preview of the agenda for <paramref name="document"/> (see <see cref="Print(bool, RadPrintDocument)"/>).</summary>
        public void PrintPreview (RadPrintDocument document) => Print (true, document);
    }

    /// <summary>Strongly-typed collection of <see cref="Appointment"/>s exposed by <see cref="RadScheduler.Appointments"/>, adding the <see cref="AddRange(System.Collections.Generic.IEnumerable{Appointment})"/> helper migrated code relies on.</summary>
    public class SchedulerAppointmentCollection : System.Collections.ObjectModel.Collection<Appointment>
    {
        /// <summary>Adds every appointment in <paramref name="appointments"/> to the collection.</summary>
        public void AddRange (IEnumerable<Appointment> appointments)
        {
            foreach (var a in appointments)
                Add (a);
        }
    }

    /// <summary>
    /// Telerik-compat scheduler navigator: previous/next/today buttons plus (in the real control) a
    /// month calendar. Wires <see cref="AssociatedScheduler"/>'s <see cref="RadScheduler.ActiveView"/> to
    /// the button clicks; the calendar picker itself is not rendered (out of scope — see <c>BACKLOG.md</c>).
    /// </summary>
    public class RadSchedulerNavigator : Control, ISupportInitializeCompat
    {
        private RadScheduler? _associatedScheduler;

        /// <summary>Initializes a new instance of the <see cref="RadSchedulerNavigator"/> class.</summary>
        public RadSchedulerNavigator ()
        {
            Height = 28;

            NavigateBackwardsButton.Click += (_, _) => {
                AssociatedScheduler?.ActiveView.NavigateBackward ();
                NavigateBackwardsClick?.Invoke (this, EventArgs.Empty);
            };
            NavigateForwardsButton.Click += (_, _) => {
                AssociatedScheduler?.ActiveView.NavigateForward ();
                NavigateForwardsClick?.Invoke (this, EventArgs.Empty);
            };
            NavigateTodayButton.Click += (_, _) => AssociatedScheduler?.ActiveView.NavigateToday ();
            MonthViewButton.Click += (_, _) => { if (AssociatedScheduler != null) AssociatedScheduler.ActiveViewType = SchedulerViewType.Month; };
            WeekViewButton.Click += (_, _) => { if (AssociatedScheduler != null) AssociatedScheduler.ActiveViewType = SchedulerViewType.Week; };

            ShowWeekendCheckBox.CheckedChanged += (_, _) => ShowWeekendStateChanged?.Invoke (this,
                new StateChangedEventArgs (ShowWeekendCheckBox.Checked ? ToggleState.On : ToggleState.Off));
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (200, 28);

        /// <summary>Gets or sets the associated scheduler this navigator controls.</summary>
        public RadScheduler? AssociatedScheduler {
            get => _associatedScheduler;
            set => _associatedScheduler = value;
        }

        /// <summary>Gets or sets whether the weekend checkbox is shown. Stored — the checkbox is not painted by this navigator's minimal rendering.</summary>
        public bool ShowWeekendCheckBox_Visible { get; set; }

        /// <summary>Gets the weekend visibility checkbox (compat member; not painted).</summary>
        public CheckBox ShowWeekendCheckBox { get; } = new CheckBox ();

        /// <summary>Gets the "navigate backwards" button (compat member; not painted).</summary>
        public Button NavigateBackwardsButton { get; } = new Button ();

        /// <summary>Gets the "navigate forwards" button (compat member; not painted).</summary>
        public Button NavigateForwardsButton { get; } = new Button ();

        /// <summary>Gets the "navigate to today" button (compat member; not painted).</summary>
        public Button NavigateTodayButton { get; } = new Button ();

        /// <summary>Gets the "switch to month view" button (compat member; not painted).</summary>
        public Button MonthViewButton { get; } = new Button ();

        /// <summary>Gets the "switch to week view" button (compat member; not painted).</summary>
        public Button WeekViewButton { get; } = new Button ();

        /// <summary>Gets or sets the date format string used for compat display purposes.</summary>
        public string DateFormat { get; set; } = string.Empty;

        /// <summary>Gets or sets the navigation step type (day/week/month) used by the backward/forward buttons. Stored — the agenda view always navigates by the active view type's implied range.</summary>
        public NavigationStepTypes NavigationStepType { get; set; } = NavigationStepTypes.Day;

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Raised when navigating backwards. Also fired after <see cref="AssociatedScheduler"/>'s active view has moved back.</summary>
        public event EventHandler? NavigateBackwardsClick;

        /// <summary>Raised when navigating forwards. Also fired after <see cref="AssociatedScheduler"/>'s active view has moved forward.</summary>
        public event EventHandler? NavigateForwardsClick;

        /// <summary>Raised when the weekend visibility checkbox's checked state changes.</summary>
        public event EventHandler<StateChangedEventArgs>? ShowWeekendStateChanged;
    }

    /// <summary>Specifies the step used when navigating a <see cref="RadSchedulerNavigator"/>. Compat for Telerik NavigationStepTypes.</summary>
    public enum NavigationStepTypes
    {
        /// <summary>Navigate one day at a time.</summary>
        Day = 0,
        /// <summary>Navigate one week at a time.</summary>
        Week = 1,
        /// <summary>Navigate one month at a time.</summary>
        Month = 2,
    }

    /// <summary>Specifies a Telerik scheduler view type.</summary>
    public enum SchedulerViewType
    {
        /// <summary>Day view.</summary>
        Day = 0,
        /// <summary>Week view.</summary>
        Week = 1,
        /// <summary>Work-week view.</summary>
        WorkWeek = 2,
        /// <summary>Month view.</summary>
        Month = 3,
        /// <summary>Timeline view.</summary>
        Timeline = 4
    }

    /// <summary>
    /// The scheduler's active view: exposes <see cref="StartDate"/>/<see cref="EndDate"/> (the audited
    /// contract's <c>ActiveView.StartDate</c>) and re-renders the owning scheduler's agenda list whenever
    /// the range changes.
    /// </summary>
    public class SchedulerActiveView
    {
        private readonly RadScheduler _owner;
        private DateTime _startDate = DateTime.Today;

        internal SchedulerActiveView (RadScheduler owner)
        {
            _owner = owner;
            ApplyViewType (owner.ActiveViewType);
        }

        /// <summary>Gets or sets the first date shown by the active view. Setting this re-derives <see cref="EndDate"/> from the scheduler's <see cref="RadScheduler.ActiveViewType"/> and refreshes the agenda.</summary>
        public DateTime StartDate {
            get => _startDate;
            set {
                if (_startDate == value.Date)
                    return;

                _startDate = value.Date;
                _owner.RefreshAgenda ();
            }
        }

        /// <summary>Gets the last date (inclusive) shown by the active view, derived from <see cref="StartDate"/> and the scheduler's <see cref="RadScheduler.ActiveViewType"/>.</summary>
        public DateTime EndDate => ViewType switch {
            SchedulerViewType.Day => StartDate,
            SchedulerViewType.Week or SchedulerViewType.WorkWeek => StartDate.AddDays (6),
            SchedulerViewType.Month => new DateTime (StartDate.Year, StartDate.Month, 1).AddMonths (1).AddDays (-1),
            SchedulerViewType.Timeline => StartDate.AddDays (13),
            _ => StartDate.AddDays (6),
        };

        /// <summary>Gets the view type this active view currently reflects.</summary>
        public SchedulerViewType ViewType { get; private set; }

        internal void ApplyViewType (SchedulerViewType viewType) => ViewType = viewType;

        /// <summary>Moves <see cref="StartDate"/> back by one view-length (a day/week/month depending on <see cref="ViewType"/>) and raises <see cref="RadScheduler.ViewNavigated"/>.</summary>
        public void NavigateBackward () => Navigate (-1);

        /// <summary>Moves <see cref="StartDate"/> forward by one view-length and raises <see cref="RadScheduler.ViewNavigated"/>.</summary>
        public void NavigateForward () => Navigate (1);

        /// <summary>Moves <see cref="StartDate"/> to today and raises <see cref="RadScheduler.ViewNavigated"/>.</summary>
        public void NavigateToday ()
        {
            var old = StartDate;
            StartDate = DateTime.Today;
            _owner.RaiseViewNavigated (old, StartDate);
        }

        private void Navigate (int direction)
        {
            var old = StartDate;
            StartDate = ViewType switch {
                SchedulerViewType.Day => StartDate.AddDays (direction),
                SchedulerViewType.Month => StartDate.AddMonths (direction),
                SchedulerViewType.Timeline => StartDate.AddDays (14 * direction),
                _ => StartDate.AddDays (7 * direction),
            };
            _owner.RaiseViewNavigated (old, StartDate);
        }
    }
}
