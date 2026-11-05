using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Radio buttons, check boxes and labels with BackColor = Transparent on a white panel, the way a ported options
    // strip styles them. Once an app chooses its font, back buffers get subpixel text; drawn into a buffer that stays
    // transparent, such text came out near-black with coloured fringes, so grey captions read smeared and bold, as if
    // drawn several times over. Each caption must keep its own colour: over white, antialiasing only lightens it.
    [Collection ("Headless")]
    public class TransparentGhostTextTests
    {
        private static readonly Color Ink = Color.FromArgb (0x64, 0x72, 0x80);

        // Pixels inside the caption's part of each control that are clearly darker than the ink, and the ink pixels.
        private static (int dark, int ink) Scan (bool chooseFont)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (360, 110) };
                var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
                var segoe = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f);

                Control[] controls = {
                    new RadioButton { Text = "Horizontal", Location = new Point (10, 10), Size = new Size (110, 28), FlatStyle = FlatStyle.Standard, Font = segoe },
                    new CheckBox { Text = "Inactive", Location = new Point (130, 10), Size = new Size (110, 28), Font = segoe },
                    new Label { Text = "Split mode", Location = new Point (10, 60), Size = new Size (110, 24), Font = segoe },
                };

                foreach (var control in controls) {
                    control.BackColor = Color.Transparent;
                    control.ForeColor = Ink;
                    panel.Controls.Add (control);
                }

                form.Controls.Add (panel);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, form.ClientSize.Width, form.ClientSize.Height));
                var scale = bitmap.Width / (float) form.ClientSize.Width;
                var ink_sum = Ink.R + Ink.G + Ink.B;
                int dark = 0, ink = 0;

                foreach (var control in controls) {
                    // Skip the radio's and check box's glyph: only the caption is measured.
                    var left = control is Label ? control.Left : control.Left + 20;

                    for (var y = (int) (control.Top * scale); y < (int) (control.Bottom * scale); y++)
                        for (var x = (int) (left * scale); x < (int) (control.Right * scale); x++) {
                            var c = bitmap.GetPixel (x, y);
                            var sum = c.Red + c.Green + c.Blue;

                            if (sum < ink_sum - 45)
                                dark++;
                            else if (sum < 600)
                                ink++;
                        }
                }

                return (dark, ink);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Captions_on_transparent_controls_keep_their_colour_in_ported_mode ()
        {
            var (dark, ink) = Scan (chooseFont: true);

            Assert.True (ink > 30, $"{ink} caption pixels");
            Assert.Equal (0, dark);
        }

        [Fact]
        public void Captions_on_transparent_controls_keep_their_colour_without_a_chosen_font ()
        {
            var (dark, ink) = Scan (chooseFont: false);

            Assert.True (ink > 30, $"{ink} caption pixels");
            Assert.Equal (0, dark);
        }
    }
}
