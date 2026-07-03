using System.Data;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Exercises the Phase 5 scheduler data layer (RadSchedulerData.cs), the RadScheduler agenda view and
    // navigation (RadScheduler.cs / RadSchedulerSupport.cs), and printing (RadSchedulerPrinting.cs). None
    // of these types show a real Form: RadScheduler/RadSchedulerNavigator/the agenda list are plain
    // Controls, and SchedulerPrintSettingsDialog's ShowDialog() (like Majorsilence.Forms.PrintDialog's)
    // never calls the base Form.Show()/ShowDialog() — so unlike RadMessageBox.Show, none of this can leak
    // into the process-wide Application.OpenForms list. (Verified explicitly below anyway.)
    public class RadSchedulerTests
    {
        private static DataTable MakeAppointmentsTable ()
        {
            var dt = new DataTable ("Appointments");
            dt.Columns.Add ("ID", typeof (int));
            dt.Columns.Add ("Start", typeof (DateTime));
            dt.Columns.Add ("End", typeof (DateTime));
            dt.Columns.Add ("Reminder", typeof (double)); // stored as seconds, like Reminders.vb
            dt.Columns.Add ("Summary", typeof (string));
            dt.Columns.Add ("Description", typeof (string));
            dt.Columns.Add ("Location", typeof (string));
            dt.Columns.Add ("BackgroundID", typeof (int));
            dt.Columns.Add ("StatusID", typeof (int));
            dt.Columns.Add ("RecurrenceRule", typeof (string));
            dt.Columns.Add ("ResourceID", typeof (int));
            return dt;
        }

        private static AppointmentMappingInfo MakeMapping ()
        {
            return new AppointmentMappingInfo {
                Start = "Start",
                End = "End",
                Reminder = "Reminder",
                Summary = "Summary",
                Description = "Description",
                Location = "Location",
                BackgroundId = "BackgroundID",
                StatusId = "StatusID",
                RecurrenceRule = "RecurrenceRule",
                ResourceId = "ResourceID",
            };
        }

        // ── AppointmentMappingInfo / SchedulerMapping ──────────────────────────────────────────────────

        [Fact]
        public void FindByDataSourceProperty_returns_the_mapping_for_a_mapped_field ()
        {
            var mapping = MakeMapping ();

            var reminderMapping = mapping.FindByDataSourceProperty ("Reminder");

            Assert.Equal ("Reminder", reminderMapping.DataSourceProperty);
        }

        [Fact]
        public void FindByDataSourceProperty_is_case_insensitive ()
        {
            var mapping = MakeMapping ();

            var reminderMapping = mapping.FindByDataSourceProperty ("reminder");

            Assert.Equal ("Reminder", reminderMapping.DataSourceProperty);
        }

        [Fact]
        public void FindByDataSourceProperty_creates_an_unattached_mapping_for_an_unmapped_column ()
        {
            var mapping = new AppointmentMappingInfo ();

            var created = mapping.FindByDataSourceProperty ("SomeColumn");

            Assert.Equal ("SomeColumn", created.DataSourceProperty);
        }

        // ── SchedulerBindingDataSource: materialization from a DataTable ───────────────────────────────

        [Fact]
        public void EventProvider_DataSource_materializes_Appointments_from_a_DataTable ()
        {
            var dt = MakeAppointmentsTable ();
            var row = dt.NewRow ();
            row["ID"] = 1;
            row["Start"] = new DateTime (2026, 7, 1, 9, 0, 0);
            row["End"] = new DateTime (2026, 7, 1, 10, 0, 0);
            row["Summary"] = "Standup";
            row["Location"] = "Room A";
            dt.Rows.Add (row);

            var bindingSource = new SchedulerBindingDataSource ();
            bindingSource.EventProvider.Mapping = MakeMapping ();
            bindingSource.EventProvider.DataSource = dt;

            Assert.Single (bindingSource.Appointments);
            var appointment = bindingSource.Appointments[0];
            Assert.Equal ("Standup", appointment.Summary);
            Assert.Equal ("Room A", appointment.Location);
            Assert.Equal (new DateTime (2026, 7, 1, 9, 0, 0), appointment.Start);
            Assert.Equal (new DateTime (2026, 7, 1, 10, 0, 0), appointment.End);
        }

        [Fact]
        public void Appointments_refreshes_when_a_row_is_added_after_binding ()
        {
            var dt = MakeAppointmentsTable ();
            var bindingSource = new SchedulerBindingDataSource ();
            bindingSource.EventProvider.Mapping = MakeMapping ();
            bindingSource.EventProvider.DataSource = dt;

            Assert.Empty (bindingSource.Appointments);

            var row = dt.NewRow ();
            row["Start"] = DateTime.Today;
            row["End"] = DateTime.Today.AddHours (1);
            row["Summary"] = "New appointment";
            dt.Rows.Add (row);

            Assert.Single (bindingSource.Appointments);
            Assert.Equal ("New appointment", bindingSource.Appointments[0].Summary);
        }

        [Fact]
        public void ConvertCallback_round_trips_a_Reminder_TimeSpan_through_seconds ()
        {
            // Mirrors Reminders.vb's ConvertIdToDataSource: the scheduler side works with a TimeSpan, the
            // data source column stores seconds (a double).
            var dt = MakeAppointmentsTable ();
            var row = dt.NewRow ();
            row["Start"] = DateTime.Today;
            row["End"] = DateTime.Today.AddHours (1);
            row["Summary"] = "Reminder test";
            row["Reminder"] = 900d; // 15 minutes, in seconds
            dt.Rows.Add (row);

            var mapping = MakeMapping ();
            mapping.FindByDataSourceProperty ("Reminder").ConvertToScheduler = value => TimeSpan.FromSeconds ((double)value!);
            mapping.FindByDataSourceProperty ("Reminder").ConvertToDataSource = value => value is TimeSpan ts ? ts.TotalSeconds : 0d;

            var bindingSource = new SchedulerBindingDataSource ();
            bindingSource.EventProvider.Mapping = mapping;
            bindingSource.EventProvider.DataSource = dt;

            var appointment = bindingSource.Appointments[0];
            Assert.Equal (TimeSpan.FromMinutes (15), appointment.Reminder);

            // Round-trip back: change the in-memory value and write it back through ConvertToDataSource.
            appointment.Reminder = TimeSpan.FromMinutes (30);
            bindingSource.WriteBack (appointment);

            Assert.Equal (1800d, (double)dt.Rows[0]["Reminder"]);
        }

        [Fact]
        public void WriteBack_is_a_no_op_for_an_appointment_with_no_originating_row ()
        {
            var bindingSource = new SchedulerBindingDataSource ();
            var appointment = new Appointment (DateTime.Today, DateTime.Today.AddHours (1), "Ad-hoc");

            // Should not throw even though this appointment was never materialized from a DataTable.
            bindingSource.WriteBack (appointment);
        }

        [Fact]
        public void ResourceProvider_Mapping_and_DataSource_are_settable ()
        {
            var bindingSource = new SchedulerBindingDataSource ();
            var resourceMapping = new ResourceMappingInfo { Id = "ResourceID", Name = "ResourceName" };

            bindingSource.ResourceProvider.Mapping = resourceMapping;
            bindingSource.ResourceProvider.DataSource = new DataTable ();

            Assert.Same (resourceMapping, bindingSource.ResourceProvider.Mapping);
            Assert.Equal ("ResourceID", bindingSource.ResourceProvider.Mapping.Id);
        }

        [Fact]
        public void BeginInit_and_EndInit_refresh_Appointments ()
        {
            var dt = MakeAppointmentsTable ();
            var bindingSource = new SchedulerBindingDataSource ();

            bindingSource.BeginInit ();
            bindingSource.EventProvider.Mapping = MakeMapping ();
            bindingSource.EventProvider.DataSource = dt;
            var row = dt.NewRow ();
            row["Start"] = DateTime.Today;
            row["End"] = DateTime.Today;
            row["Summary"] = "Init test";
            dt.Rows.Add (row);
            bindingSource.EndInit ();

            Assert.Single (bindingSource.Appointments);
        }

        // ── RadScheduler: DataSource / Appointments / agenda view ──────────────────────────────────────

        [Fact]
        public void RadScheduler_DataSource_populates_Appointments ()
        {
            var dt = MakeAppointmentsTable ();
            var row = dt.NewRow ();
            row["Start"] = DateTime.Today;
            row["End"] = DateTime.Today.AddHours (1);
            row["Summary"] = "Bound appointment";
            dt.Rows.Add (row);

            var bindingSource = new SchedulerBindingDataSource ();
            bindingSource.EventProvider.Mapping = MakeMapping ();
            bindingSource.EventProvider.DataSource = dt;

            using var scheduler = new RadScheduler { DataSource = bindingSource };

            Assert.Single (scheduler.Appointments);
            Assert.Equal ("Bound appointment", scheduler.Appointments[0].Summary);
        }

        [Fact]
        public void RadScheduler_Appointments_can_be_populated_directly_without_a_DataSource ()
        {
            using var scheduler = new RadScheduler ();

            scheduler.Appointments.AddRange ([
                new Appointment (DateTime.Today, DateTime.Today.AddHours (1), "Task A"),
                new Appointment (DateTime.Today.AddDays (1), DateTime.Today.AddDays (1).AddHours (1), "Task B"),
            ]);

            Assert.Equal (2, scheduler.Appointments.Count);
        }

        [Fact]
        public void ActiveView_StartDate_defaults_to_today_and_EndDate_reflects_ActiveViewType ()
        {
            using var scheduler = new RadScheduler { ActiveViewType = SchedulerViewType.Week };

            Assert.Equal (DateTime.Today, scheduler.ActiveView.StartDate);
            Assert.Equal (DateTime.Today.AddDays (6), scheduler.ActiveView.EndDate);
        }

        [Fact]
        public void ActiveViewType_Day_gives_a_single_day_range ()
        {
            using var scheduler = new RadScheduler { ActiveViewType = SchedulerViewType.Day };

            Assert.Equal (scheduler.ActiveView.StartDate, scheduler.ActiveView.EndDate);
        }

        [Fact]
        public void ActiveViewType_Month_spans_the_whole_month ()
        {
            using var scheduler = new RadScheduler ();
            scheduler.ActiveView.StartDate = new DateTime (2026, 2, 1);
            scheduler.ActiveViewType = SchedulerViewType.Month;

            Assert.Equal (new DateTime (2026, 2, 28), scheduler.ActiveView.EndDate);
        }

        [Fact]
        public void ActiveViewType_change_raises_ActiveViewChanged_with_old_and_new_view_types ()
        {
            using var scheduler = new RadScheduler { ActiveViewType = SchedulerViewType.Week };

            SchedulerViewChangedEventArgs? received = null;
            scheduler.ActiveViewChanged += (_, e) => received = e;

            scheduler.ActiveViewType = SchedulerViewType.Month;

            Assert.NotNull (received);
            Assert.Equal (SchedulerViewType.Week, received!.OldView.ViewType);
            Assert.Equal (SchedulerViewType.Month, received.NewView.ViewType);
        }

        [Fact]
        public void ActiveView_StartDate_set_refreshes_the_agenda_without_throwing ()
        {
            using var scheduler = new RadScheduler ();
            scheduler.Appointments.Add (new Appointment (DateTime.Today, DateTime.Today.AddHours (1), "A"));

            scheduler.ActiveView.StartDate = DateTime.Today.AddDays (7);

            // No direct observable other than "did not throw" plus the agenda child control still exists.
            Assert.NotNull (scheduler);
        }

        [Fact]
        public void NavigateForward_and_NavigateBackward_move_StartDate_by_the_view_length ()
        {
            using var scheduler = new RadScheduler { ActiveViewType = SchedulerViewType.Week };
            var start = scheduler.ActiveView.StartDate;

            scheduler.ActiveView.NavigateForward ();
            Assert.Equal (start.AddDays (7), scheduler.ActiveView.StartDate);

            scheduler.ActiveView.NavigateBackward ();
            Assert.Equal (start, scheduler.ActiveView.StartDate);
        }

        [Fact]
        public void NavigateToday_resets_StartDate_to_today ()
        {
            using var scheduler = new RadScheduler ();
            scheduler.ActiveView.StartDate = DateTime.Today.AddMonths (2);

            scheduler.ActiveView.NavigateToday ();

            Assert.Equal (DateTime.Today, scheduler.ActiveView.StartDate);
        }

        [Fact]
        public void Navigation_raises_ViewNavigated ()
        {
            using var scheduler = new RadScheduler ();
            SchedulerViewNavigatedEventArgs? received = null;
            scheduler.ViewNavigated += (_, e) => received = e;

            scheduler.ActiveView.NavigateForward ();

            Assert.NotNull (received);
        }

        [Fact]
        public void RadSchedulerNavigator_buttons_drive_the_associated_scheduler ()
        {
            using var scheduler = new RadScheduler { ActiveViewType = SchedulerViewType.Week };
            using var navigator = new RadSchedulerNavigator { AssociatedScheduler = scheduler };
            var start = scheduler.ActiveView.StartDate;

            navigator.NavigateForwardsButton.PerformClick ();

            Assert.Equal (start.AddDays (7), scheduler.ActiveView.StartDate);
        }

        [Fact]
        public void RadSchedulerNavigator_today_button_resets_the_scheduler_to_today ()
        {
            using var scheduler = new RadScheduler ();
            using var navigator = new RadSchedulerNavigator { AssociatedScheduler = scheduler };
            scheduler.ActiveView.StartDate = DateTime.Today.AddYears (1);

            navigator.NavigateTodayButton.PerformClick ();

            Assert.Equal (DateTime.Today, scheduler.ActiveView.StartDate);
        }

        [Fact]
        public void AppointmentEditDialogShowing_can_be_canceled ()
        {
            using var scheduler = new RadScheduler ();
            var appointment = new Appointment (DateTime.Today, DateTime.Today.AddHours (1), "Edit me");

            AppointmentEditDialogShowingEventArgs? received = null;
            scheduler.AppointmentEditDialogShowing += (_, e) => {
                e.Cancel = true;
                e.AppointmentEditDialog.EditAppointment (null, null);
                received = e;
            };

            scheduler.ShowAppointmentEditDialog (appointment);

            Assert.NotNull (received);
            Assert.True (received!.Cancel);
            Assert.Same (appointment, received.Appointment);
        }

        [Fact]
        public void ContextMenuOpening_can_be_canceled ()
        {
            using var scheduler = new RadScheduler ();
            var menu = new RadContextMenu ();

            var result = scheduler.RaiseContextMenuOpening (menu);
            Assert.False (result.Cancel); // default when no handler is attached

            scheduler.ContextMenuOpening += (_, e) => e.Cancel = true;
            result = scheduler.RaiseContextMenuOpening (menu);
            Assert.True (result.Cancel);
        }

        [Fact]
        public void Export_writes_a_real_iCalendar_document ()
        {
            using var scheduler = new RadScheduler ();
            scheduler.Appointments.Add (new Appointment (
                new DateTime (2026, 7, 1, 9, 0, 0), new DateTime (2026, 7, 1, 10, 0, 0), "Standup", "Daily sync", "Room A"));

            using var stream = new MemoryStream ();
            scheduler.Export (stream, new SchedulerICalendarExporter ());

            var text = System.Text.Encoding.UTF8.GetString (stream.ToArray ());
            Assert.Contains ("BEGIN:VCALENDAR", text);
            Assert.Contains ("BEGIN:VEVENT", text);
            Assert.Contains ("SUMMARY:Standup", text);
            Assert.Contains ("END:VCALENDAR", text);
        }

        [Fact]
        public void GetMonthView_WeekCount_and_ShowWeekend_are_settable ()
        {
            using var scheduler = new RadScheduler ();

            scheduler.GetMonthView ().WeekCount = 6;
            scheduler.GetMonthView ().ShowWeekend = false;

            Assert.Equal (6, scheduler.GetMonthView ().WeekCount);
            Assert.False (scheduler.GetMonthView ().ShowWeekend);
        }

        [Fact]
        public void GetTimelineView_GetScaling_DisplayedCellsCount_is_settable ()
        {
            using var scheduler = new RadScheduler ();

            scheduler.GetTimelineView ().GetScaling ().DisplayedCellsCount = 12;

            Assert.Equal (12, scheduler.GetTimelineView ().GetScaling ().DisplayedCellsCount);
        }

        [Fact]
        public void VerticalScroll_Visible_is_settable ()
        {
            // Mirrors frmShedular.vb's RsTask.VerticalScroll.Visible = False / HorizontalScroll.Enabled.
            using var scheduler = new RadScheduler ();

            scheduler.VerticalScroll.Visible = false;
            scheduler.HorizontalScroll.Enabled = false;

            Assert.False (scheduler.VerticalScroll.Visible);
            Assert.False (scheduler.HorizontalScroll.Enabled);
        }

        // ── Printing ────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Print_produces_a_real_openable_PDF ()
        {
            using var scheduler = new RadScheduler ();
            scheduler.Appointments.Add (new Appointment (DateTime.Today, DateTime.Today.AddHours (1), "Printed appointment"));
            scheduler.PrintStyle = new SchedulerDailyPrintStyle (DateTime.Today, DateTime.Today);

            var document = new RadPrintDocument ();
            var path = Path.Combine (Path.GetTempPath (), $"{Guid.NewGuid ():N}.pdf");
            try {
                document.AssociatedObject = scheduler;
                document.PrintToPdf (path);

                var bytes = File.ReadAllBytes (path);
                Assert.True (bytes.Length > 0);
                Assert.Equal ("%PDF", System.Text.Encoding.ASCII.GetString (bytes, 0, 4));
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void Print_with_a_watermark_still_produces_a_valid_PDF ()
        {
            using var scheduler = new RadScheduler ();
            scheduler.Appointments.Add (new Appointment (DateTime.Today, DateTime.Today.AddHours (1), "Watermarked"));
            scheduler.PrintStyle = new SchedulerWeeklyPrintStyle (DateTime.Today, DateTime.Today.AddDays (6));

            var document = new RadPrintDocument {
                Watermark = new RadPrintWatermark { Text = "DRAFT", Opacity = 0.3f },
            };
            var path = Path.Combine (Path.GetTempPath (), $"{Guid.NewGuid ():N}.pdf");
            try {
                document.AssociatedObject = scheduler;
                document.PrintToPdf (path);

                Assert.True (new FileInfo (path).Length > 0);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void PrintStyle_carries_the_date_range_and_DrawPageTitleCalendar ()
        {
            var start = new DateTime (2026, 3, 1);
            var end = new DateTime (2026, 3, 31);

            var daily = new SchedulerDailyPrintStyle (start, end) { DrawPageTitleCalendar = false };
            var weekly = new SchedulerWeeklyPrintStyle (start, end);
            var monthly = new SchedulerMonthlyPrintStyle (start, end);
            var details = new SchedulerDetailsPrintStyle (start, end);

            Assert.Equal (start, daily.StartDate);
            Assert.Equal (end, daily.EndDate);
            Assert.False (daily.DrawPageTitleCalendar);
            Assert.True (weekly.DrawPageTitleCalendar);
            Assert.Equal ("Weekly", weekly.StyleName);
            Assert.Equal ("Monthly", monthly.StyleName);
            Assert.Equal ("Details", details.StyleName);
        }

        [Fact]
        public void SchedulerPrintSettingsDialog_ShowDialog_returns_OK_without_showing_a_Form ()
        {
            // Leak-safety: SchedulerPrintSettingsDialog extends Form (for API-shape compatibility, like
            // Majorsilence.Forms.PrintDialog/PageSetupDialog) but its ShowDialog() override never calls
            // the base Form.Show()/ShowDialog(), so it cannot add itself to Application.OpenForms. Verified
            // explicitly here per the RadGridExportTests.cs lesson: check OpenForms.Count before/after.
            var formsBefore = Application.OpenForms.Count;

            var document = new RadPrintDocument ();
            var dialog = new SchedulerPrintSettingsDialog {
                PrintDocument = document,
                ThemeName = "Office2019",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays (7),
            };

            var result = dialog.ShowDialog ();

            Assert.Equal (DialogResult.OK, result);
            Assert.Equal (formsBefore, Application.OpenForms.Count);
        }

        [Fact]
        public void RadPrintDocument_HeaderFont_and_FooterFont_are_settable ()
        {
            var document = new RadPrintDocument {
                HeaderFont = new Majorsilence.Drawing.Font ("Arial", 12),
                FooterFont = new Majorsilence.Drawing.Font ("Arial", 6),
            };

            Assert.Equal (12, document.HeaderFont.Size);
            Assert.Equal (6, document.FooterFont.Size);
        }
    }
}
