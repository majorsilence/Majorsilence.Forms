using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog box that allows the user to choose a font family, size, and style.
    /// </summary>
    public class FontDialog : Form
    {
        private readonly TextBox family_box;
        private readonly NumericUpDown size_box;
        private readonly CheckBox bold_box;
        private readonly CheckBox italic_box;

        // Majorsilence.Forms.Drawing.Font is only supported on Windows, so it is constructed lazily; merely
        // creating the dialog (or running on a non-Windows host) must not throw.
        private Font? selected_font;

        /// <summary>
        /// Initializes a new instance of the FontDialog class.
        /// </summary>
        public FontDialog ()
        {
            Text = "Font";
            AllowMaximize = false;
            AllowMinimize = false;
            Size = new Size (360, 230);

            Controls.Add (new Label { Text = "Family:", Left = 14, Top = 16, Width = 60 });
            family_box = Controls.Add (new TextBox { Left = 80, Top = 14, Width = 250, Text = "Arial" });

            Controls.Add (new Label { Text = "Size:", Left = 14, Top = 52, Width = 60 });
            size_box = Controls.Add (new NumericUpDown { Left = 80, Top = 50, Width = 100, Minimum = 1, Maximum = 512, Value = 9 });

            bold_box = Controls.Add (new CheckBox { Text = "Bold", Left = 80, Top = 86, Width = 90 });
            italic_box = Controls.Add (new CheckBox { Text = "Italic", Left = 180, Top = 86, Width = 90 });

            var ok = Controls.Add (new Button { Text = "OK", Width = 80, Left = 162, Top = 150 });
            ok.Click += (o, e) => {
                selected_font = BuildFont ();
                DialogResult = DialogResult.OK;
            };

            var cancel = Controls.Add (new Button { Text = "Cancel", Width = 80, Left = 250, Top = 150 });
            cancel.Click += (o, e) => DialogResult = DialogResult.Cancel;
        }

        private Font BuildFont ()
        {
            var style = FontStyle.Regular;

            if (bold_box.Checked)
                style |= FontStyle.Bold;
            if (italic_box.Checked)
                style |= FontStyle.Italic;

            var name = string.IsNullOrWhiteSpace (family_box.Text) ? "Arial" : family_box.Text;

            try {
                return new Font (name, (float)size_box.Value, style);
            } catch {
                return new Font ("Arial", (float)size_box.Value, style);
            }
        }

        /// <summary>
        /// Gets or sets the selected font.
        /// </summary>
        public new Font Font {
            get => selected_font ??= BuildFont ();
            set {
                if (value is null)
                    return;

                selected_font = value;
                family_box.Text = value.Name;
                size_box.Value = (decimal)Math.Max (1f, Math.Min (512f, value.Size));
                bold_box.Checked = value.Bold;
                italic_box.Checked = value.Italic;
            }
        }

        /// <summary>Gets or sets the selected color (used when ShowColor is enabled).</summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>Gets or sets whether the dialog shows a color choice. Informational.</summary>
        public bool ShowColor { get; set; }

        /// <summary>Gets or sets whether the selected font must be an installed font. Informational.</summary>
        public bool FontMustExist { get; set; }

        /// <summary>Gets or sets whether the user can change the character set. Stub in Majorsilence.Forms.</summary>
        public bool AllowScriptChange { get; set; } = true;

        /// <summary>Gets or sets whether simulated fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool AllowSimulations { get; set; } = true;

        /// <summary>Gets or sets whether vector fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool AllowVectorFonts { get; set; } = true;

        /// <summary>Gets or sets whether vertical fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool AllowVerticalFonts { get; set; } = true;

        /// <summary>Gets or sets whether only fixed-pitch fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool FixedPitchOnly { get; set; }

        private int min_size;
        private int max_size;

        /// <summary>Gets or sets the maximum font size the user can select. A value of 0 means no maximum.</summary>
        public int MaxSize {
            get => max_size;
            set {
                if (value < 0)
                    value = 0;

                max_size = value;

                if (max_size != 0 && max_size < min_size)
                    min_size = max_size;
            }
        }

        /// <summary>Gets or sets the minimum font size the user can select. A value of 0 means no minimum.</summary>
        public int MinSize {
            get => min_size;
            set {
                if (value < 0)
                    value = 0;

                min_size = value;

                if (max_size != 0 && max_size < min_size)
                    max_size = min_size;
            }
        }

        /// <summary>Gets or sets whether the Apply button is shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowApply { get; set; }

        /// <summary>Gets or sets whether font effects (strikeout, underline) are shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowEffects { get; set; } = true;

        /// <summary>Gets or sets whether the Help button is shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowHelp { get; set; }

        /// <summary>Raised when the Apply button is clicked. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? Apply { add { } remove { } }

        /// <summary>Resets all dialog options to their default values.</summary>
        public virtual void Reset ()
        {
            selected_font = null;
            family_box.Text = "Arial";
            size_box.Value = 9;
            bold_box.Checked = false;
            italic_box.Checked = false;

            Color = Color.Black;
            ShowColor = false;
            FontMustExist = false;
            AllowScriptChange = true;
            AllowSimulations = true;
            AllowVectorFonts = true;
            AllowVerticalFonts = true;
            FixedPitchOnly = false;
            min_size = 0;
            max_size = 0;
            ShowApply = false;
            ShowEffects = true;
            ShowHelp = false;
        }
    }
}
