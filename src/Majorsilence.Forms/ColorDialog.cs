using System;
using System.Collections.Generic;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog box that allows the user to select a color from a palette or define a custom one.
    /// </summary>
    /// <remarks>
    /// A <see cref="Form"/> standing in for the Win32 <c>ChooseColor</c> dialog upstream wraps
    /// (<c>Dialogs/CommonDialogs/ColorDialog.cs</c>), with its shape (SVC-36): basic colours, the sixteen
    /// <see cref="CustomColors"/>, and a "Define Custom Colors" pane (offered when
    /// <see cref="AllowFullOpen"/>, open from the start when <see cref="FullOpen"/>) where any RGB value
    /// can be entered and added to the custom colours. A swatch selects; OK commits <see cref="Color"/>
    /// and <see cref="CustomColors"/>, Cancel discards both, as upstream copies them back only on OK.
    /// </remarks>
    public partial class ColorDialog : Form
    {
        private static readonly Color[] s_palette = {
            Color.Black, Color.DimGray, Color.Gray, Color.DarkGray, Color.Silver, Color.LightGray, Color.WhiteSmoke, Color.White,
            Color.Maroon, Color.Red, Color.OrangeRed, Color.Orange, Color.Gold, Color.Yellow, Color.GreenYellow, Color.Lime,
            Color.DarkGreen, Color.Green, Color.SeaGreen, Color.Teal, Color.Turquoise, Color.Cyan, Color.LightCyan, Color.Azure,
            Color.Navy, Color.Blue, Color.RoyalBlue, Color.DodgerBlue, Color.SkyBlue, Color.SteelBlue, Color.SlateGray, Color.LightBlue,
            Color.Indigo, Color.Purple, Color.DarkViolet, Color.MediumPurple, Color.Magenta, Color.Violet, Color.Pink, Color.MistyRose
        };

        private const int Columns = 8;
        private const int Swatch = 26;
        private const int Gap = 4;
        private const int Inset = 12;
        private const int CustomSlots = 16;
        private const int PaneWidth = 180;

        // WinForms default for an uninitialized custom-color slot.
        private const int DefaultCustomColor = 0x00FFFFFF;

        // What OK has committed, and what the dialog currently shows.
        private Color color = Color.Black;
        private int[] custom_colors = CreateDefaultCustomColors ();
        private Color pending_color = Color.Black;
        private int[] pending_custom = CreateDefaultCustomColors ();

        // The custom slot "Add to Custom Colors" writes next. ChooseColor starts at the first and moves on
        // one each time; picking a custom swatch moves it there.
        private int custom_slot;

        private bool allow_full_open = true;
        private bool full_open;
        private bool expanded;

        private readonly List<Panel> basic_swatches = new ();
        private readonly List<Panel> custom_swatches = new ();
        private readonly Button define_button;
        private readonly Panel preview;
        private readonly NumericUpDown red_box;
        private readonly NumericUpDown green_box;
        private readonly NumericUpDown blue_box;
        private readonly Button add_custom_button;
        private readonly Button ok_button;
        private readonly Button cancel_button;
        private readonly Control[] pane_controls;
        private readonly int collapsed_width;
        private bool syncing_rgb;

        /// <summary>
        /// Initializes a new instance of the ColorDialog class.
        /// </summary>
        public ColorDialog ()
        {
            Text = "Color";
            AllowMaximize = false;
            AllowMinimize = false;

            var rows = (int)Math.Ceiling (s_palette.Length / (double)Columns);
            var grid_width = Columns * Swatch + (Columns - 1) * Gap;
            var grid_height = rows * Swatch + (rows - 1) * Gap;

            for (var i = 0; i < s_palette.Length; i++) {
                var c = s_palette[i];
                var swatch = AddSwatch (Inset + i % Columns * (Swatch + Gap), Inset + i / Columns * (Swatch + Gap), c);
                swatch.Click += (o, e) => ChooseSwatch (c);
                basic_swatches.Add (swatch);
            }

            var custom_top = Inset + grid_height + 10;
            Controls.Add (new Label { Text = "Custom colors:", Left = Inset, Top = custom_top, Width = grid_width });
            custom_top += 22;

            for (var i = 0; i < CustomSlots; i++) {
                var slot = i;
                var swatch = AddSwatch (Inset + i % Columns * (Swatch + Gap), custom_top + i / Columns * (Swatch + Gap), FromColorRef (DefaultCustomColor));
                swatch.Click += (o, e) => ChooseCustomSlot (slot);
                custom_swatches.Add (swatch);
            }

            var define_top = custom_top + 2 * (Swatch + Gap) + 6;
            define_button = Controls.Add (new Button { Text = "Define Custom Colors >>", Left = Inset, Top = define_top, Width = grid_width });
            define_button.Click += (o, e) => SetExpanded (true);

            var buttons_top = define_top + 36;

            ok_button = Controls.Add (new Button { Text = "OK", Width = 80, Left = Inset + grid_width - 168, Top = buttons_top });
            ok_button.Click += (o, e) => {
                color = pending_color;
                custom_colors = (int[])pending_custom.Clone ();
                DialogResult = DialogResult.OK;
                ResetView ();
            };

            cancel_button = Controls.Add (new Button { Text = "Cancel", Width = 80, Left = Inset + grid_width - 80, Top = buttons_top });
            cancel_button.Click += (o, e) => {
                DialogResult = DialogResult.Cancel;
                ResetView ();
            };

            // The custom-colour pane, right of the palette.
            var pane_left = Inset * 2 + grid_width;
            preview = Controls.Add (new Panel { Left = pane_left, Top = Inset, Width = PaneWidth - Inset, Height = 60 });
            preview.Style.Border.Width = 1;

            red_box = AddChannel ("Red:", pane_left, Inset + 72);
            green_box = AddChannel ("Green:", pane_left, Inset + 104);
            blue_box = AddChannel ("Blue:", pane_left, Inset + 136);

            add_custom_button = Controls.Add (new Button { Text = "Add to Custom Colors", Left = pane_left, Top = Inset + 172, Width = PaneWidth - Inset });
            add_custom_button.Click += (o, e) => AddCustomColor ();

            pane_controls = CollectPane (pane_left);

            collapsed_width = Inset * 2 + grid_width + 16;
            Size = new Size (collapsed_width, buttons_top + 40 + 40);

            ResetView ();
        }

        private Panel AddSwatch (int left, int top, Color fill)
        {
            var swatch = Controls.Add (new Panel { Left = left, Top = top, Width = Swatch, Height = Swatch });
            swatch.Style.BackgroundColor = fill.ToSKColor ();
            swatch.Style.Border.Width = 1;
            return swatch;
        }

        private NumericUpDown AddChannel (string caption, int left, int top)
        {
            Controls.Add (new Label { Text = caption, Left = left, Top = top + 2, Width = 50 });
            var box = Controls.Add (new NumericUpDown { Left = left + 56, Top = top, Width = 80, Minimum = 0, Maximum = 255 });
            box.ValueChanged += (o, e) => {
                if (!syncing_rgb)
                    SetPending (Color.FromArgb ((int)red_box.Value, (int)green_box.Value, (int)blue_box.Value));
            };
            return box;
        }

        // Everything at or right of the pane's left edge belongs to the pane.
        private Control[] CollectPane (int paneLeft)
        {
            var pane = new List<Control> ();

            foreach (Control c in Controls)
                if (c.Left >= paneLeft)
                    pane.Add (c);

            return pane.ToArray ();
        }

        // The dialog as it opens: the committed colour and custom colours, the pane open only when
        // FullOpen asks and AllowFullOpen permits -- upstream clears CC_FULLOPEN without AllowFullOpen.
        private void ResetView ()
        {
            pending_custom = (int[])custom_colors.Clone ();
            custom_slot = 0;
            SetPending (color);
            ShowCustomSwatches ();
            define_button.Enabled = allow_full_open;
            SetExpanded (allow_full_open && full_open);
        }

        private void SetExpanded (bool value)
        {
            expanded = value && allow_full_open;

            foreach (var c in pane_controls)
                c.Visible = expanded;

            define_button.Enabled = allow_full_open && !expanded;
            Width = expanded ? collapsed_width + PaneWidth : collapsed_width;
        }

        /// <summary>Whether the custom-colour pane is showing.</summary>
        internal bool IsFullOpen => expanded;

        // A swatch click selects the colour; only OK commits it. It used to commit and close on the
        // click, so a colour could never be inspected, adjusted or added to the custom colours.
        internal void ChooseSwatch (Color value) => SetPending (value);

        internal void ChooseCustomSlot (int slot)
        {
            custom_slot = slot;
            SetPending (FromColorRef (pending_custom[slot]));
        }

        // "Add to Custom Colors": the pane's colour goes into the current slot as a COLORREF
        // (0x00BBGGRR, upstream's lpCustColors), and the next add goes to the slot after it.
        internal void AddCustomColor ()
        {
            pending_custom[custom_slot] = ToColorRef (pending_color);
            custom_slot = (custom_slot + 1) % CustomSlots;
            ShowCustomSwatches ();
        }

        internal NumericUpDown RedBox => red_box;
        internal NumericUpDown GreenBox => green_box;
        internal NumericUpDown BlueBox => blue_box;
        internal Button DefineButton => define_button;
        internal Button AddCustomButton => add_custom_button;
        internal Button OkButton => ok_button;
        internal Button CancelButtonControl => cancel_button;

        private void SetPending (Color value)
        {
            pending_color = value;
            preview.Style.BackgroundColor = value.ToSKColor ();

            syncing_rgb = true;

            try {
                red_box.Value = value.R;
                green_box.Value = value.G;
                blue_box.Value = value.B;
            } finally {
                syncing_rgb = false;
            }

            // The selected basic colour is marked, as ChooseColor frames it.
            foreach (var swatch in basic_swatches)
                swatch.Style.Border.Width = SameRgb (swatch, value) ? 3 : 1;
        }

        private static bool SameRgb (Panel swatch, Color value)
        {
            var fill = swatch.Style.BackgroundColor;
            return fill.HasValue && fill.Value.Red == value.R && fill.Value.Green == value.G && fill.Value.Blue == value.B;
        }

        private void ShowCustomSwatches ()
        {
            for (var i = 0; i < CustomSlots; i++)
                custom_swatches[i].Style.BackgroundColor = FromColorRef (pending_custom[i]).ToSKColor ();
        }

        private static Color FromColorRef (int colorRef)
            => Color.FromArgb (colorRef & 0xFF, (colorRef >> 8) & 0xFF, (colorRef >> 16) & 0xFF);

        private static int ToColorRef (Color value) => value.R | (value.G << 8) | (value.B << 16);

        /// <summary>Gets or sets the color selected by the user. Setting <see cref="Color.Empty"/> resets to black.</summary>
        public Color Color {
            get => color;
            set {
                color = value.IsEmpty ? Color.Black : value;
                SetPending (color);
            }
        }

        /// <summary>Gets or sets whether the user can use the dialog to define custom colors.</summary>
        /// <remarks>Off, the "Define Custom Colors" button is disabled and <see cref="FullOpen"/> does not
        /// open the pane: upstream's documentation has AllowFullOpen take precedence.</remarks>
        public bool AllowFullOpen {
            get => allow_full_open;
            set {
                allow_full_open = value;
                ResetView ();
            }
        }

        /// <summary>Gets or sets whether the dialog opens with the custom-color pane showing.</summary>
        public bool FullOpen {
            get => full_open;
            set {
                full_open = value;
                ResetView ();
            }
        }

        /// <summary>Gets or sets whether the dialog displays all available colors.</summary>
        /// <remarks>Accepted and not acted on: <c>CC_ANYCOLOR</c> only matters on a palette-based
        /// display, where the basic colours would otherwise be limited to the system palette. On the
        /// true-colour surfaces this layer draws to, every colour is already available.</remarks>
        public bool AnyColor { get; set; }

        /// <summary>Gets or sets the set of custom colors shown in the dialog, as COLORREF values (0x00BBGGRR). Always 16 entries; getting returns a copy.</summary>
        /// <remarks>Shown in the dialog's custom swatches, and updated with the colours the user adds
        /// when the dialog closes with OK, so an application that persists them round-trips the
        /// user's choices.</remarks>
        public int[] CustomColors {
            get => (int[])custom_colors.Clone ();
            set {
                custom_colors = NormalizeCustomColors (value);
                pending_custom = (int[])custom_colors.Clone ();
                ShowCustomSwatches ();
            }
        }

        /// <summary>Gets or sets whether the user is restricted to selecting solid colors only.</summary>
        /// <remarks>Accepted and not acted on: <c>CC_SOLIDCOLOR</c> stops ChooseColor offering dithered
        /// colours on a palette-based display. Every colour on a true-colour surface is solid.</remarks>
        public bool SolidColorOnly { get; set; }

        /// <summary>Gets or sets whether the Help button is displayed. Stub in Majorsilence.Forms.</summary>
        public bool ShowHelp { get; set; }

        /// <summary>Resets the properties of the dialog to their default values.</summary>
        public virtual void Reset ()
        {
            color = Color.Black;
            custom_colors = CreateDefaultCustomColors ();
            allow_full_open = true;
            full_open = false;
            AnyColor = false;
            SolidColorOnly = false;
            ShowHelp = false;
            ResetView ();
        }

        private static int[] CreateDefaultCustomColors ()
        {
            var result = new int[CustomSlots];

            for (var i = 0; i < result.Length; i++)
                result[i] = DefaultCustomColor;

            return result;
        }

        // WinForms keeps exactly 16 custom-color slots: a null or short array is padded with the
        // default white value, and a longer array is truncated.
        private static int[] NormalizeCustomColors (int[]? value)
        {
            var result = CreateDefaultCustomColors ();

            if (value != null)
                Array.Copy (value, result, Math.Min (value.Length, result.Length));

            return result;
        }
    }
}
