using System;
using System.Drawing;

namespace Modern.Forms
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

        /// <summary>Gets or sets the color selected by the user.</summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>Gets or sets whether the user can use the dialog to define custom colors. Informational.</summary>
        public bool AllowFullOpen { get; set; } = true;

        /// <summary>Gets or sets whether the dialog is opened in full (custom-color) mode. Informational.</summary>
        public bool FullOpen { get; set; }

        /// <summary>Gets or sets whether the dialog displays all available colors. Informational.</summary>
        public bool AnyColor { get; set; }

        /// <summary>Gets or sets the set of custom colors shown in the dialog. Informational.</summary>
        public int[] CustomColors { get; set; } = Array.Empty<int> ();
    }
}
