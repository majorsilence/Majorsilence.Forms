using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog box that allows the user to select a color from a palette.
    /// </summary>
    public class ColorDialog : Form
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
        private const int Margin = 12;

        // WinForms default for an uninitialized custom-color slot.
        private const int DefaultCustomColor = 0x00FFFFFF;

        private Color color = Color.Black;
        private int[] custom_colors = CreateDefaultCustomColors ();

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
                var color = s_palette[i];
                var col = i % Columns;
                var row = i / Columns;

                var swatch = Controls.Add (new Panel {
                    Left = Margin + col * (Swatch + Gap),
                    Top = Margin + row * (Swatch + Gap),
                    Width = Swatch,
                    Height = Swatch
                });

                swatch.Style.BackgroundColor = color.ToSKColor ();
                swatch.Style.Border.Width = 1;

                swatch.Click += (o, e) => {
                    Color = color;
                    DialogResult = DialogResult.OK;
                };
            }

            var buttons_top = Margin + grid_height + 12;

            var ok = Controls.Add (new Button {
                Text = "OK",
                Width = 80,
                Left = Margin + grid_width - 168,
                Top = buttons_top
            });
            ok.Click += (o, e) => DialogResult = DialogResult.OK;

            var cancel = Controls.Add (new Button {
                Text = "Cancel",
                Width = 80,
                Left = Margin + grid_width - 80,
                Top = buttons_top
            });
            cancel.Click += (o, e) => DialogResult = DialogResult.Cancel;

            Size = new Size (Margin * 2 + grid_width + 16, buttons_top + 40 + 40);
        }

        /// <summary>Gets or sets the color selected by the user. Setting <see cref="Color.Empty"/> resets to black.</summary>
        public Color Color {
            get => color;
            set => color = value.IsEmpty ? Color.Black : value;
        }

        /// <summary>Gets or sets whether the user can use the dialog to define custom colors. Informational.</summary>
        public bool AllowFullOpen { get; set; } = true;

        /// <summary>Gets or sets whether the dialog is opened in full (custom-color) mode. Informational.</summary>
        public bool FullOpen { get; set; }

        /// <summary>Gets or sets whether the dialog displays all available colors. Informational.</summary>
        public bool AnyColor { get; set; }

        /// <summary>Gets or sets the set of custom colors shown in the dialog. Always 16 entries; getting returns a copy. Informational.</summary>
        public int[] CustomColors {
            get => (int[])custom_colors.Clone ();
            set => custom_colors = NormalizeCustomColors (value);
        }

        /// <summary>Gets or sets whether the user is restricted to selecting solid colors only. Stub in Majorsilence.Forms.</summary>
        public bool SolidColorOnly { get; set; }

        /// <summary>Gets or sets whether the Help button is displayed. Stub in Majorsilence.Forms.</summary>
        public bool ShowHelp { get; set; }

        /// <summary>Resets the properties of the dialog to their default values.</summary>
        public virtual void Reset ()
        {
            color = Color.Black;
            custom_colors = CreateDefaultCustomColors ();
            AllowFullOpen = true;
            FullOpen = false;
            AnyColor = false;
            SolidColorOnly = false;
            ShowHelp = false;
        }

        private static int[] CreateDefaultCustomColors ()
        {
            var result = new int[16];

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
