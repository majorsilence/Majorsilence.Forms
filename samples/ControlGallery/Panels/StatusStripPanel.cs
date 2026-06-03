using Modern.Forms;

namespace ControlGallery.Panels
{
    public class StatusStripPanel : Panel
    {
        public StatusStripPanel ()
        {
            // --- Labels at the top explaining the demo ---
            Controls.Add (new Label {
                Text = "StatusStrip docked at the bottom of this panel.",
                Left = 20, Top = 20, Width = 500
            });
            Controls.Add (new Label {
                Text = "Use the buttons below to toggle items and simulate progress.",
                Left = 20, Top = 45, Width = 500
            });

            // --- Build the StatusStrip ---
            var strip = new StatusStrip ();

            var statusLabel = new ToolStripStatusLabel {
                Name = "StatusLabel",
                Text = "Ready",
                Size = new System.Drawing.Size (200, 17),
                Visible = true
            };

            var progressBar = new ToolStripProgressBar {
                Name = "ProgressBar",
                Size = new System.Drawing.Size (130, 16),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visible = false
            };

            var activityLabel = new ToolStripStatusLabel {
                Name = "ActivityLabel",
                Text = "",
                Size = new System.Drawing.Size (160, 17),
                Visible = false
            };

            strip.Items.AddRange (new ToolStripItem[] { statusLabel, progressBar, activityLabel });
            Controls.Add (strip);

            // --- Controls that drive the status strip ---
            int y = 90;

            Controls.Add (new Label { Text = "Status text:", Left = 20, Top = y + 3, Width = 90 });
            var textBox = Controls.Add (new TextBox {
                Left = 118, Top = y, Width = 200, Text = "Ready"
            });
            var applyBtn = Controls.Add (new Button {
                Text = "Apply", Left = 328, Top = y, Width = 80
            });
            applyBtn.Click += (o, e) => {
                statusLabel.Text = textBox.Text;
                strip.Invalidate ();
            };

            y += 45;

            Controls.Add (new Label { Text = "Progress:", Left = 20, Top = y + 3, Width = 90 });

            var showProgress = Controls.Add (new Button {
                Text = "Show progress", Left = 118, Top = y, Width = 120
            });
            showProgress.Click += (o, e) => {
                progressBar.Visible = true;
                activityLabel.Visible = true;
                activityLabel.Text = "Working…";
                progressBar.Value = 0;
                strip.Invalidate ();
            };

            var hideProgress = Controls.Add (new Button {
                Text = "Hide progress", Left = 248, Top = y, Width = 120
            });
            hideProgress.Click += (o, e) => {
                progressBar.Visible = false;
                activityLabel.Visible = false;
                statusLabel.Text = "Ready";
                strip.Invalidate ();
            };

            y += 45;

            Controls.Add (new Label { Text = "Progress value:", Left = 20, Top = y + 3, Width = 110 });
            var progressSpinner = Controls.Add (new NumericUpDown {
                Left = 138, Top = y, Width = 100,
                Minimum = 0, Maximum = 100, Value = 0
            });
            var setProgressBtn = Controls.Add (new Button {
                Text = "Set", Left = 248, Top = y, Width = 80
            });
            setProgressBtn.Click += (o, e) => {
                progressBar.Value = (int)progressSpinner.Value;
                activityLabel.Text = progressBar.Value == 100 ? "Done!" : "Working…";
                strip.Invalidate ();
            };

            y += 45;

            // --- Toggle visibility of each item ---
            Controls.Add (new Label { Text = "Toggle items:", Left = 20, Top = y + 3, Width = 100 });

            var toggleStatus = Controls.Add (new CheckBox {
                Text = "Status label",
                Left = 128, Top = y + 1, Width = 120,
                Checked = true
            });
            toggleStatus.CheckedChanged += (o, e) => {
                statusLabel.Visible = toggleStatus.Checked;
                strip.Invalidate ();
            };

            var togglePb = Controls.Add (new CheckBox {
                Text = "Progress bar",
                Left = 258, Top = y + 1, Width = 120,
                Checked = false
            });
            togglePb.CheckedChanged += (o, e) => {
                progressBar.Visible = togglePb.Checked;
                strip.Invalidate ();
            };
        }
    }
}
