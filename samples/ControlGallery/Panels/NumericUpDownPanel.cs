using Modern.Forms;

namespace ControlGallery.Panels
{
    public class NumericUpDownPanel : Panel
    {
        public NumericUpDownPanel ()
        {
            int y = 20;

            // --- Basic integer spinner ---
            Controls.Add (new Label { Text = "Basic (0–100):", Left = 20, Top = y + 3, Width = 130 });
            var basic = Controls.Add (new NumericUpDown {
                Left = 160, Top = y,
                Width = 120,
                Minimum = 0,
                Maximum = 100,
                Value = 42
            });
            var basicLabel = Controls.Add (new Label {
                Text = $"Value: {basic.Value}",
                Left = 295, Top = y + 3, Width = 150
            });
            basic.ValueChanged += (o, e) => basicLabel.Text = $"Value: {basic.Value}";

            y += 45;

            // --- Custom range ---
            Controls.Add (new Label { Text = "Port (1–65535):", Left = 20, Top = y + 3, Width = 130 });
            var port = Controls.Add (new NumericUpDown {
                Left = 160, Top = y,
                Width = 120,
                Minimum = 1,
                Maximum = 65535,
                Value = 8080
            });
            Controls.Add (new Label { Text = "(typical port range)", Left = 295, Top = y + 3, Width = 180 });

            y += 45;

            // --- Decimal places ---
            Controls.Add (new Label { Text = "Percentage:", Left = 20, Top = y + 3, Width = 130 });
            var pct = Controls.Add (new NumericUpDown {
                Left = 160, Top = y,
                Width = 120,
                Minimum = 0,
                Maximum = 100,
                DecimalPlaces = 2,
                Value = 33.33m
            });
            Controls.Add (new Label { Text = "2 decimal places", Left = 295, Top = y + 3, Width = 180 });

            y += 45;

            // --- Disabled ---
            Controls.Add (new Label { Text = "Disabled:", Left = 20, Top = y + 3, Width = 130 });
            Controls.Add (new NumericUpDown {
                Left = 160, Top = y,
                Width = 120,
                Value = 7,
                Enabled = false
            });

            y += 60;

            // --- Interactive demo ---
            Controls.Add (new Label {
                Text = "Interactive: adjust width of the bar below",
                Left = 20, Top = y, Width = 400
            });

            y += 30;

            var widthSpinner = Controls.Add (new NumericUpDown {
                Left = 20, Top = y,
                Width = 120,
                Minimum = 10,
                Maximum = 500,
                Value = 200
            });

            var bar = Controls.Add (new ProgressBar {
                Left = 155, Top = y,
                Width = 200,
                Height = 23,
                Minimum = 10,
                Maximum = 500,
                Value = 200
            });

            widthSpinner.ValueChanged += (o, e) => {
                bar.Width = (int)widthSpinner.Value;
                bar.Value = (int)widthSpinner.Value;
            };
        }
    }
}
