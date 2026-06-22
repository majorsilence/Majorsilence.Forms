using Majorsilence.Forms;

namespace ControlGallery.Panels
{
    public class TimePickerPanel : Panel
    {
        public TimePickerPanel ()
        {
            int y = 20;

            // --- Basic time picker ---
            Controls.Add (new Label { Text = "Start time:", Left = 20, Top = y + 3, Width = 100 });
            var start = Controls.Add (new TimePicker {
                Left = 130, Top = y,
                Width = 120,
                Value = new DateTime (2024, 1, 1, 9, 0, 0)
            });
            var startReadout = Controls.Add (new Label {
                Text = $"= {start.Value:HH:mm}",
                Left = 265, Top = y + 3, Width = 150
            });
            start.TextChanged += (o, e) => {
                startReadout.Text = start.Value.HasValue
                    ? $"= {start.Value.Value:HH:mm}"
                    : "(invalid)";
            };

            y += 45;

            // --- End time picker ---
            Controls.Add (new Label { Text = "End time:", Left = 20, Top = y + 3, Width = 100 });
            var end = Controls.Add (new TimePicker {
                Left = 130, Top = y,
                Width = 120,
                Value = new DateTime (2024, 1, 1, 17, 30, 0)
            });
            var endReadout = Controls.Add (new Label {
                Text = $"= {end.Value:HH:mm}",
                Left = 265, Top = y + 3, Width = 150
            });
            end.TextChanged += (o, e) => {
                endReadout.Text = end.Value.HasValue
                    ? $"= {end.Value.Value:HH:mm}"
                    : "(invalid)";
            };

            y += 45;

            // --- Duration label (live) ---
            var durationLabel = Controls.Add (new Label {
                Text = "",
                Left = 20, Top = y, Width = 400
            });
            UpdateDuration ();

            start.TextChanged += (o, e) => UpdateDuration ();
            end.TextChanged   += (o, e) => UpdateDuration ();

            void UpdateDuration ()
            {
                if (start.Value.HasValue && end.Value.HasValue) {
                    var span = end.Value.Value.TimeOfDay - start.Value.Value.TimeOfDay;
                    durationLabel.Text = span.TotalMinutes >= 0
                        ? $"Duration: {(int)span.TotalHours}h {span.Minutes:00}m"
                        : "End time is before start time";
                }
            }

            y += 45;

            // --- Disabled ---
            Controls.Add (new Label { Text = "Read-only:", Left = 20, Top = y + 3, Width = 100 });
            Controls.Add (new TimePicker {
                Left = 130, Top = y,
                Width = 120,
                Value = new DateTime (2024, 1, 1, 12, 0, 0),
                Enabled = false
            });

            y += 45;

            // --- In a group box ---
            var group = Controls.Add (new GroupBox {
                Text = "Scheduled job",
                Left = 20, Top = y,
                Width = 340, Height = 90
            });

            group.Controls.Add (new Label { Text = "Run at:", Left = 10, Top = 22, Width = 60 });
            group.Controls.Add (new TimePicker {
                Left = 78, Top = 20,
                Width = 110,
                Value = new DateTime (2024, 1, 1, 2, 0, 0)
            });
            group.Controls.Add (new CheckBox { Text = "Enabled", Left = 200, Top = 24, Width = 120 });
        }
    }
}
