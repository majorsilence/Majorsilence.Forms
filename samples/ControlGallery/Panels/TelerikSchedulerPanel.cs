using System;
using System.Data;
using System.IO;
using Majorsilence.Forms;
using Majorsilence.Forms.Telerik;

namespace ControlGallery.Panels
{
    // Showcases RadScheduler bound to a DataTable through AppointmentMappingInfo/SchedulerBindingDataSource
    // (Majorsilence.Forms.Telerik) — the same binding pattern Financial's frmShedular.vb-style code uses.
    // Appointments render as a scrollable agenda view (grouped by day). Print/PrintPreview render the
    // current agenda range to a real PDF via RadPrintDocument.
    public class TelerikSchedulerPanel : BasePanel
    {
        private readonly RadScheduler scheduler;
        private readonly SchedulerBindingDataSource bindingSource;
        private readonly DataTable appointmentsTable;
        private readonly Label status;

        public TelerikSchedulerPanel ()
        {
            Controls.Add (new Label {
                Text = "RadScheduler bound via AppointmentMappingInfo/SchedulerBindingDataSource to a DataTable — agenda view grouped by day.",
                Left = 10, Top = 10, Width = 780, Height = 20
            });

            appointmentsTable = BuildAppointmentsTable ();

            var mapping = new AppointmentMappingInfo {
                Start = "Start",
                End = "End",
                Summary = "Summary",
                Description = "Description",
                Location = "Location",
            };

            bindingSource = new SchedulerBindingDataSource ();
            bindingSource.EventProvider.Mapping = mapping;
            bindingSource.EventProvider.DataSource = appointmentsTable;

            var navigator = new RadSchedulerNavigator { Left = 10, Top = 34, Width = 780 };
            Controls.Add (navigator);

            scheduler = new RadScheduler {
                Left = 10, Top = 34 + navigator.Height, Width = 780, Height = 360,
                DataSource = bindingSource,
                ActiveViewType = SchedulerViewType.Month,
            };
            navigator.AssociatedScheduler = scheduler;
            Controls.Add (scheduler);

            status = new Label {
                Text = $"Last action: bound {scheduler.Appointments.Count} appointment(s)",
                Left = 10, Top = 40 + navigator.Height + 366, Width = 780
            };
            Controls.Add (status);

            var addButton = new Button { Text = "Add Appointment Today", Left = 10, Top = 68 + navigator.Height + 366, Width = 200, Height = 30 };
            addButton.Click += (_, _) => AddAppointmentToday ();
            Controls.Add (addButton);

            var printButton = new Button { Text = "Print Agenda to PDF", Left = 220, Top = 68 + navigator.Height + 366, Width = 180, Height = 30 };
            printButton.Click += (_, _) => PrintAgenda (showDialog: false);
            Controls.Add (printButton);

            var printPreviewButton = new Button { Text = "Print Preview", Left = 410, Top = 68 + navigator.Height + 366, Width = 150, Height = 30 };
            printPreviewButton.Click += (_, _) => PrintAgenda (showDialog: true);
            Controls.Add (printPreviewButton);
        }

        private static DataTable BuildAppointmentsTable ()
        {
            var table = new DataTable ("Appointments");
            table.Columns.Add ("Start", typeof (DateTime));
            table.Columns.Add ("End", typeof (DateTime));
            table.Columns.Add ("Summary", typeof (string));
            table.Columns.Add ("Description", typeof (string));
            table.Columns.Add ("Location", typeof (string));

            var today = DateTime.Today;
            AddAppointmentRow (table, today.AddHours (9), today.AddHours (9.5), "Daily Standup", "Team sync", "Room A");
            AddAppointmentRow (table, today.AddHours (13), today.AddHours (14), "Client Call", "Quarterly review", "Zoom");
            AddAppointmentRow (table, today.AddDays (1).AddHours (10), today.AddDays (1).AddHours (11), "Design Review", "UI mockups", "Room B");
            AddAppointmentRow (table, today.AddDays (2).AddHours (15), today.AddDays (2).AddHours (16.5), "Sprint Planning", "Plan next sprint", "Room A");
            AddAppointmentRow (table, today.AddDays (5).AddHours (9), today.AddDays (5).AddHours (17), "Conference", "Annual industry conference", "Convention Center");

            return table;
        }

        private static void AddAppointmentRow (DataTable table, DateTime start, DateTime end, string summary, string description, string location)
        {
            var row = table.NewRow ();
            row["Start"] = start;
            row["End"] = end;
            row["Summary"] = summary;
            row["Description"] = description;
            row["Location"] = location;
            table.Rows.Add (row);
        }

        private void AddAppointmentToday ()
        {
            var start = DateTime.Now.AddHours (1);
            AddAppointmentRow (appointmentsTable, start, start.AddHours (1), $"Ad-hoc Meeting {DateTime.Now:HH:mm}", "Added from ControlGallery", "TBD");
            Report ($"Added appointment — {scheduler.Appointments.Count} total now bound");
        }

        private void PrintAgenda (bool showDialog)
        {
            var document = new RadPrintDocument ();
            var path = Path.Combine (Path.GetTempPath (), $"telerik-scheduler-{Guid.NewGuid ():N}.pdf");

            document.AssociatedObject = scheduler;
            document.PrintToPdf (path);

            Report ($"{(showDialog ? "PrintPreview" : "Print")} wrote {path}");
        }

        private void Report (string action) => status.Text = $"Last action: {action}";
    }
}
