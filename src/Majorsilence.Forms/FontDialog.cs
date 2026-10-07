using System;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog box that allows the user to choose a font family, size, style, effects and colour.
    /// </summary>
    /// <remarks>
    /// A <see cref="Form"/> standing in for the Win32 <c>ChooseFont</c> dialog upstream wraps
    /// (<c>Dialogs/CommonDialogs/FontDialog.cs</c>). The options that dialog acts on act here too (SVC-35):
    /// <see cref="MinSize"/>/<see cref="MaxSize"/> bound the size box (<c>CF_LIMITSIZE</c>),
    /// <see cref="ShowEffects"/> shows Underline/Strikeout and <see cref="ShowColor"/> the colour list
    /// (<c>CF_EFFECTS</c>), <see cref="FontMustExist"/> refuses a family that is not installed
    /// (<c>CF_FORCEFONTEXIST</c>), <see cref="FixedPitchOnly"/> lists only fixed-pitch families, and
    /// <see cref="ShowApply"/> shows an Apply button that raises <see cref="Apply"/>.
    /// </remarks>
    public partial class FontDialog : Form
    {
        private const string DefaultFamily = "Arial";
        private const int DefaultMaximumSize = 512;

        // The sixteen colours ChooseFont's colour list offers (the VGA palette), in its order.
        private static readonly (Color Color, string Name)[] s_font_colors = {
            (Color.Black, "Black"), (Color.Maroon, "Maroon"), (Color.Green, "Green"), (Color.Olive, "Olive"),
            (Color.Navy, "Navy"), (Color.Purple, "Purple"), (Color.Teal, "Teal"), (Color.Gray, "Gray"),
            (Color.Silver, "Silver"), (Color.Red, "Red"), (Color.Lime, "Lime"), (Color.Yellow, "Yellow"),
            (Color.Blue, "Blue"), (Color.Fuchsia, "Fuchsia"), (Color.Aqua, "Aqua"), (Color.White, "White"),
        };

        private readonly ComboBox family_box;
        private readonly NumericUpDown size_box;
        private readonly CheckBox bold_box;
        private readonly CheckBox italic_box;
        private readonly CheckBox underline_box;
        private readonly CheckBox strikeout_box;
        private readonly Label color_label;
        private readonly ComboBox color_box;
        private readonly Label error_label;
        private readonly Button apply_button;

        // Majorsilence.Forms.Drawing.Font is only supported on Windows, so it is constructed lazily; merely
        // creating the dialog (or running on a non-Windows host) must not throw.
        private Font? selected_font;
        private Color color = Color.Black;
        private bool families_listed;

        /// <summary>
        /// Initializes a new instance of the FontDialog class.
        /// </summary>
        public FontDialog ()
        {
            Text = "Font";
            AllowMaximize = false;
            AllowMinimize = false;
            Size = new Size (360, 290);

            Controls.Add (new Label { Text = "Family:", Left = 14, Top = 16, Width = 60 });
            // Editable, as ChooseFont's font box is: a name can be typed as well as picked.
            family_box = Controls.Add (new ComboBox { Left = 80, Top = 14, Width = 250, Text = DefaultFamily });

            Controls.Add (new Label { Text = "Size:", Left = 14, Top = 52, Width = 60 });
            size_box = Controls.Add (new NumericUpDown { Left = 80, Top = 50, Width = 100, Minimum = 1, Maximum = DefaultMaximumSize, Value = 9 });

            bold_box = Controls.Add (new CheckBox { Text = "Bold", Left = 80, Top = 86, Width = 90 });
            italic_box = Controls.Add (new CheckBox { Text = "Italic", Left = 180, Top = 86, Width = 90 });
            underline_box = Controls.Add (new CheckBox { Text = "Underline", Left = 80, Top = 112, Width = 90 });
            strikeout_box = Controls.Add (new CheckBox { Text = "Strikeout", Left = 180, Top = 112, Width = 90 });

            color_label = Controls.Add (new Label { Text = "Color:", Left = 14, Top = 144, Width = 60 });
            color_box = Controls.Add (new ComboBox { Left = 80, Top = 142, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList });

            foreach (var (c, name) in s_font_colors)
                color_box.Items.Add (new ColorItem (c, name));

            error_label = Controls.Add (new Label { Left = 14, Top = 176, Width = 320, Visible = false, ForeColor = Color.Firebrick });

            apply_button = Controls.Add (new Button { Text = "Apply", Width = 80, Left = 74, Top = 210 });
            apply_button.Click += (o, e) => {
                if (Commit ())
                    OnApply (EventArgs.Empty);
            };

            var ok = Controls.Add (new Button { Text = "OK", Width = 80, Left = 162, Top = 210 });
            ok.Click += (o, e) => {
                if (Commit ())
                    DialogResult = DialogResult.OK;
            };

            var cancel = Controls.Add (new Button { Text = "Cancel", Width = 80, Left = 250, Top = 210 });
            cancel.Click += (o, e) => DialogResult = DialogResult.Cancel;

            ShowColorChoice (color);
            UpdateOptionalControls ();
        }

        // The colour list's entries: the colour, shown by name.
        private sealed class ColorItem
        {
            internal ColorItem (Color color, string name)
            {
                Color = color;
                Name = name;
            }

            internal Color Color { get; }

            internal string Name { get; }

            public override string ToString () => Name;
        }

        /// <inheritdoc/>
        protected override void OnLoad (EventArgs e)
        {
            ListFamilies ();
            base.OnLoad (e);
        }

        // The installed families, or only the fixed-pitch ones. Listed when the dialog is first shown
        // rather than built: a FontDialog is often created and never shown, and FixedPitchOnly is set
        // after construction.
        private void ListFamilies ()
        {
            families_listed = true;

            var typed = family_box.Text;
            family_box.Items.Clear ();

            foreach (var name in InstalledFamilies ().OrderBy (n => n, StringComparer.OrdinalIgnoreCase))
                family_box.Items.Add (name);

            family_box.Text = typed;
        }

        private System.Collections.Generic.IEnumerable<string> InstalledFamilies ()
        {
            var names = SkiaSharp.SKFontManager.Default.FontFamilies.Distinct (StringComparer.OrdinalIgnoreCase);

            return FixedPitchOnly ? names.Where (IsFixedPitch) : names;
        }

        private static bool IsFixedPitch (string family)
        {
            using var typeface = SkiaSharp.SKTypeface.FromFamilyName (family);
            return typeface?.IsFixedPitch == true;
        }

        // CF_FORCEFONTEXIST: the family must be one the list offers. An empty list (a host with no font
        // manager to ask) cannot say, so it accepts rather than refusing every name.
        private bool FamilyExists (string name)
        {
            if (!families_listed)
                ListFamilies ();

            if (family_box.Items.Count == 0)
                return true;

            foreach (var item in family_box.Items)
                if (string.Equals (item?.ToString (), name, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        // Takes the dialog's choice into Font and Color, as upstream's UpdateFont/UpdateColor do on OK
        // and on Apply. A family FontMustExist refuses keeps the dialog open with the reason shown, where
        // ChooseFont shows a message box -- a nested modal box a test cannot dismiss.
        private bool Commit ()
        {
            var name = FamilyName ();

            if (FontMustExist && !FamilyExists (name)) {
                error_label.Text = "There is no font with that name. Choose a font from the list of fonts.";
                error_label.Visible = true;
                family_box.Focus ();
                return false;
            }

            error_label.Visible = false;
            selected_font = BuildFont ();

            if (ShowColor && ShowEffects && color_box.SelectedItem is ColorItem item)
                color = item.Color;

            return true;
        }

        private string FamilyName ()
            => string.IsNullOrWhiteSpace (family_box.Text) ? DefaultFamily : family_box.Text.Trim ();

        private Font BuildFont ()
        {
            var style = FontStyle.Regular;

            if (bold_box.Checked)
                style |= FontStyle.Bold;
            if (italic_box.Checked)
                style |= FontStyle.Italic;

            // Underline and Strikeout are kept whether or not ShowEffects shows their boxes: without
            // CF_EFFECTS ChooseFont hands the initial LOGFONT's flags back unchanged, so a font set with
            // Underline still has it after OK. It used to come back without them (SVC-35).
            if (underline_box.Checked)
                style |= FontStyle.Underline;
            if (strikeout_box.Checked)
                style |= FontStyle.Strikeout;

            try {
                return new Font (FamilyName (), (float)size_box.Value, style);
            } catch {
                return new Font (DefaultFamily, (float)size_box.Value, style);
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
                size_box.Value = (decimal)Math.Max ((float)size_box.Minimum, Math.Min ((float)size_box.Maximum, value.Size));
                bold_box.Checked = value.Bold;
                italic_box.Checked = value.Italic;
                underline_box.Checked = value.Underline;
                strikeout_box.Checked = value.Strikeout;
            }
        }

        /// <summary>Gets or sets the selected color, offered in the colour list when <see cref="ShowColor"/> and <see cref="ShowEffects"/> are set.</summary>
        public Color Color {
            get => color;
            set {
                color = value;
                ShowColorChoice (value);
            }
        }

        // Selects the colour in the list, adding it as a custom entry when it is not one of the sixteen.
        private void ShowColorChoice (Color value)
        {
            for (var i = 0; i < color_box.Items.Count; i++) {
                if (color_box.Items[i] is ColorItem item && item.Color.ToArgb () == value.ToArgb ()) {
                    color_box.SelectedIndex = i;
                    return;
                }
            }

            // One custom entry at most, after the sixteen: the colour set last.
            while (color_box.Items.Count > s_font_colors.Length)
                color_box.Items.RemoveAt (color_box.Items.Count - 1);

            color_box.Items.Add (new ColorItem (value, "Custom"));
            color_box.SelectedIndex = color_box.Items.Count - 1;
        }

        private bool show_color;
        private bool show_effects = true;
        private bool show_apply;
        private bool fixed_pitch_only;

        /// <summary>Gets or sets whether the dialog shows a colour choice (inside the effects, as upstream).</summary>
        public bool ShowColor {
            get => show_color;
            set {
                show_color = value;
                UpdateOptionalControls ();
            }
        }

        /// <summary>Gets or sets whether the selected font must be an installed font; OK refuses any other name.</summary>
        public bool FontMustExist { get; set; }

        /// <summary>Gets or sets whether the user can change the character set. Stub in Majorsilence.Forms.</summary>
        public bool AllowScriptChange { get; set; } = true;

        /// <summary>Gets or sets whether simulated fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool AllowSimulations { get; set; } = true;

        /// <summary>Gets or sets whether vector fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool AllowVectorFonts { get; set; } = true;

        /// <summary>Gets or sets whether vertical fonts are shown. Stub in Majorsilence.Forms.</summary>
        public bool AllowVerticalFonts { get; set; } = true;

        /// <summary>Gets or sets whether only fixed-pitch fonts are listed.</summary>
        public bool FixedPitchOnly {
            get => fixed_pitch_only;
            set {
                if (fixed_pitch_only == value)
                    return;

                fixed_pitch_only = value;

                if (families_listed)
                    ListFamilies ();
            }
        }

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

                ApplySizeLimits ();
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

                ApplySizeLimits ();
            }
        }

        // CF_LIMITSIZE: the size box only offers MinSize..MaxSize. Zero means unbounded on that side, as
        // upstream (which passes int.MaxValue for a zero maximum); the box keeps its own range there.
        private void ApplySizeLimits ()
        {
            var min = min_size > 0 ? min_size : 1;
            var max = max_size > 0 ? max_size : Math.Max (DefaultMaximumSize, min);

            // Widest first, then narrowed, so neither setter ever sees min > max.
            size_box.Minimum = 1;
            size_box.Maximum = max;
            size_box.Minimum = min;
        }

        /// <summary>Gets or sets whether the Apply button is shown.</summary>
        public bool ShowApply {
            get => show_apply;
            set {
                show_apply = value;
                UpdateOptionalControls ();
            }
        }

        /// <summary>Gets or sets whether font effects (strikeout, underline, and with <see cref="ShowColor"/> the colour) are shown.</summary>
        public bool ShowEffects {
            get => show_effects;
            set {
                show_effects = value;
                UpdateOptionalControls ();
            }
        }

        /// <summary>Gets or sets whether the Help button is shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowHelp { get; set; }

        // ChooseFont shows the colour list only inside the effects group, so ShowColor without
        // ShowEffects shows nothing (upstream's WM_INITDIALOG hides cmb4 unless ShowColor).
        private void UpdateOptionalControls ()
        {
            underline_box.Visible = show_effects;
            strikeout_box.Visible = show_effects;
            color_label.Visible = show_effects && show_color;
            color_box.Visible = show_effects && show_color;
            apply_button.Visible = show_apply;
        }

        /// <summary>Raised when the Apply button is clicked, after <see cref="Font"/> and <see cref="Color"/> take the dialog's current choice.</summary>
        public event EventHandler? Apply;

        /// <summary>Raises the <see cref="Apply"/> event.</summary>
        protected virtual void OnApply (EventArgs e) => Apply?.Invoke (this, e);

        /// <summary>Resets all dialog options to their default values.</summary>
        public virtual void Reset ()
        {
            selected_font = null;
            family_box.Text = DefaultFamily;
            bold_box.Checked = false;
            italic_box.Checked = false;
            underline_box.Checked = false;
            strikeout_box.Checked = false;
            error_label.Visible = false;

            Color = Color.Black;
            show_color = false;
            FontMustExist = false;
            AllowScriptChange = true;
            AllowSimulations = true;
            AllowVectorFonts = true;
            AllowVerticalFonts = true;
            FixedPitchOnly = false;
            min_size = 0;
            max_size = 0;
            ApplySizeLimits ();
            size_box.Value = 9;
            show_apply = false;
            show_effects = true;
            ShowHelp = false;
            UpdateOptionalControls ();
        }
    }
}
