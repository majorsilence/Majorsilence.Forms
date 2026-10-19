using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Majorsilence.Forms;

namespace CssShowcase
{
    // Every colour, radius, shadow, ring and animation here comes from showcase.css: the form only lays
    // the controls out. The one exception is the title's font size, set in code -- a theme rule styles a
    // control *type*, so all Labels share one size until classes arrive (docs/theming-selectors-plan.md).
    internal sealed class MainForm : Form
    {
        public const int InitialWidth = 760;
        public const int InitialHeight = 560;

        private readonly string sheet_path;
        private readonly Label status;
        private readonly Button primary;
        private FileSystemWatcher? watcher;

        public MainForm (string sheetPath)
        {
            sheet_path = sheetPath;
            Text = "CSS Showcase";
            Size = new Size (InitialWidth, InitialHeight);

            var title = Controls.Add (new Label { Text = "CSS Showcase", Left = 24, Top = 16, Width = 600, Height = 36 });
            title.Style.FontSize = 22;
            Controls.Add (new Label {
                Text = "Hover, press and Tab through the controls. Everything you see is styled by showcase.css.",
                Left = 24, Top = 54, Width = 700, Height = 22,
            });

            // Buttons: hover and press transitions, the focus ring (press Tab), and the disabled fade.
            var buttons = Controls.Add (new Panel { Left = 24, Top = 92, Width = 340, Height = 196, Padding = new Padding (16) });
            buttons.Controls.Add (new Label { Text = "Buttons", Left = 16, Top = 14, Width = 200 });
            primary = buttons.Controls.Add (new Button { Text = "Save changes", Left = 16, Top = 44, Width = 140, Height = 38 });
            var secondary = buttons.Controls.Add (new Button { Text = "Preview", Left = 168, Top = 44, Width = 140, Height = 38 });
            var disabled = buttons.Controls.Add (new Button { Text = "Unavailable", Left = 16, Top = 94, Width = 140, Height = 38, Enabled = false });
            var toggle = buttons.Controls.Add (new CheckBox { Text = "Disable the buttons", Left = 16, Top = 146, Width = 220 });
            toggle.CheckedChanged += (_, _) => {
                primary.Enabled = !toggle.Checked;
                secondary.Enabled = !toggle.Checked;
            };

            // Inputs: an outline focus ring that fades in, and a link whose colour shifts on hover.
            var inputs = Controls.Add (new Panel { Left = 388, Top = 92, Width = 340, Height = 196, Padding = new Padding (16) });
            inputs.Controls.Add (new Label { Text = "Sign in", Left = 16, Top = 14, Width = 200 });
            inputs.Controls.Add (new TextBox { Placeholder = "Email address", Left = 16, Top = 46, Width = 300 });
            inputs.Controls.Add (new TextBox { Placeholder = "Password", Left = 16, Top = 88, Width = 300, UseSystemPasswordChar = true });
            var link = inputs.Controls.Add (new LinkLabel { Text = "Forgot your password?", Left = 16, Top = 136, Width = 220 });
            link.LinkClicked += (_, _) => status!.Text = "The link's colour transition ran on hover; clicking does nothing else.";

            // Progress: a keyframe animation that loops forever (and stops under reduced motion).
            var progress = Controls.Add (new Panel { Left = 24, Top = 312, Width = 704, Height = 132, Padding = new Padding (16) });
            progress.Controls.Add (new Label { Text = "Syncing", Left = 16, Top = 14, Width = 200 });
            var bar = progress.Controls.Add (new ProgressBar { Left = 16, Top = 46, Width = 668, Height = 14, Value = 64 });
            var slider = progress.Controls.Add (new TrackBar { Left = 16, Top = 72, Width = 668, Minimum = 0, Maximum = 100, Value = 64, TickFrequency = 10 });
            slider.ValueChanged += (_, _) => bar.Value = slider.Value;

            status = Controls.Add (new Label { Left = 24, Top = 460, Width = 704, Height = 40 });

            ApplySheet ();
            Watch ();
        }

        /// <summary>What the parser said about the sheet last applied.</summary>
        public IReadOnlyList<ThemeCssDiagnostic> Diagnostics { get; private set; } = Array.Empty<ThemeCssDiagnostic> ();

        /// <summary>Whether the sheet last applied had errors.</summary>
        public bool HasErrors => Diagnostics.Any (d => d.Severity == ThemeCssSeverity.Error);

        /// <summary>The first button's centre in window coordinates, for the headless render to hover.</summary>
        public Point PrimaryButtonCenter {
            get {
                var at = new Point (primary.Width / 2, primary.Height / 2);
                // Up to (not including) the form's root container, whose origin is the client origin.
                for (Control c = primary; c.Parent is not null; c = c.Parent)
                    at.Offset (c.Left, c.Top);
                return at;
            }
        }

        private void ApplySheet ()
        {
            string css;

            try {
                css = File.ReadAllText (sheet_path);
            } catch (IOException ex) {
                status.Text = $"Could not read {sheet_path}: {ex.Message}";
                return;
            }

            // ApplyStyleSheet keeps whatever parsed even when part of the sheet has an error, so a typo
            // while editing breaks one declaration rather than the whole look.
            var sheet = ThemeStyleSheet.Parse (css);
            Diagnostics = sheet.Diagnostics;
            Theme.ApplyStyleSheet (sheet);

            status.Text = Diagnostics.Count == 0
                ? $"{Path.GetFileName (sheet_path)} applied with no diagnostics. Edit and save it to see changes live."
                : string.Join (Environment.NewLine, Diagnostics.Take (2).Select (d => d.ToString ()));
        }

        private void Watch ()
        {
            var directory = Path.GetDirectoryName (sheet_path);
            if (string.IsNullOrEmpty (directory) || !Directory.Exists (directory))
                return;

            watcher = new FileSystemWatcher (directory, Path.GetFileName (sheet_path)) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            // Editors save in two steps (truncate, then write); applying an empty read would flash the
            // default theme, so wait for the content.
            FileSystemEventHandler onChanged = (_, _) => BeginInvoke (() => {
                if (File.Exists (sheet_path) && new FileInfo (sheet_path).Length > 0)
                    ApplySheet ();
            });
            watcher.Changed += onChanged;
            watcher.Created += onChanged;
            watcher.Renamed += (_, _) => BeginInvoke (ApplySheet);
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                watcher?.Dispose ();

            base.Dispose (disposing);
        }
    }
}
