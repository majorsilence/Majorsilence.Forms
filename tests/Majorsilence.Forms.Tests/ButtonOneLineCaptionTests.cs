using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Where only one line of a themed button's caption fits, upstream draws that one line, as many whole characters of
    // it as fit, rather than wrapping it. Measured (.NET 10), 12pt "Cancel Add" on a 28px button: "Cancel Ad" at 97px,
    // "Cancel A" at 85px, "Cancel" at 70px, both lines at 48px high; the focused (default) button's field a pixel larger
    // each way, so "Cancel Add" whole on the focused 97px button. Here the caption wrapped and its second line was
    // dropped, so a "Cancel &Add" button showed "Cancel".
    [Collection ("Headless")]
    public class ButtonOneLineCaptionTests
    {
        private static Majorsilence.Forms.Drawing.Font Font12 => new ("Microsoft Sans Serif", 12f);

        // The caption's inked rows and columns inside the button.
        private static (int bands, int width) Ink (Form form, Button button)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, form.ClientSize.Width, form.ClientSize.Height));
            var scale = bitmap.Width / (float) form.ClientSize.Width;

            int bands = 0, left = int.MaxValue, right = -1;
            var inked = false;
            for (var y = (int) ((button.Top + 3) * scale); y < (int) ((button.Bottom - 3) * scale); y++) {
                var ink = false;
                for (var x = (int) ((button.Left + 3) * scale); x < (int) ((button.Right - 3) * scale); x++) {
                    var p = bitmap.GetPixel (x, y);
                    if (p.Red + p.Green + p.Blue < 300) {
                        ink = true;
                        left = System.Math.Min (left, x);
                        right = System.Math.Max (right, x);
                    }
                }

                if (ink && !inked)
                    bands++;
                inked = ink;
            }

            return (bands, right < 0 ? 0 : right - left + 1);
        }

        private static float Advance (Button button, string text)
            => TextMeasurer.MeasureText (text, button.GetEffectiveFont (), button.LogicalToDeviceUnits (button.GetEffectiveFontSize ())).Width;

        [Fact]
        public void A_caption_too_wide_for_a_one_line_button_shows_one_line_past_its_first_word ()
        {
            try {
                Application.SetDefaultFont (Font12);
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 40) };
                var button = new Button { Text = "Cancel &Add", Font = Font12, Location = new Point (10, 6), TabIndex = 1 };
                form.Controls.Add (button);
                form.Controls.Add (new TextBox { Location = new Point (112, 32), Size = new Size (6, 6), TabIndex = 0 });
                form.Show ();

                // Narrower than the caption, wider than its first word and the next letter.
                var device = button.LogicalToDeviceUnits (100) / 100f;
                var across = 2 * (4 + (int) System.Math.Ceiling (button.Font.Height / 6.0));
                button.Size = new Size ((int) System.Math.Ceiling ((Advance (button, "Cancel Ad") + Advance (button, "Cancel Add")) / 2 / device) + across, 28);
                Assert.False (button.IsDefault);

                var (bands, width) = Ink (form, button);

                Assert.Equal (1, bands);
                Assert.True (width > Advance (button, "Cancel A") - button.LogicalToDeviceUnits (2),
                    $"{width}px of ink: more than \"Cancel\" shows on the line");
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_caption_still_wraps_where_two_lines_fit ()
        {
            try {
                Application.SetDefaultFont (Font12);
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 60) };
                var button = new Button { Text = "Cancel Add", Font = Font12, Location = new Point (10, 6), Size = new Size (70, 48), TabIndex = 1 };
                form.Controls.Add (button);
                form.Controls.Add (new TextBox { Location = new Point (112, 52), Size = new Size (6, 6), TabIndex = 0 });
                form.Show ();

                Assert.Equal (2, Ink (form, button).bands);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void The_default_button_s_text_field_is_a_pixel_larger_each_way ()
        {
            try {
                Application.SetDefaultFont (Font12);
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (220, 40) };
                // A caption wider than either field, so each text box is its whole field.
                var plain = new Button { Text = "Cancel Add Cancel Add", Font = Font12, Location = new Point (0, 6), Size = new Size (97, 28), TabIndex = 1 };
                var accept = new Button { Text = "Cancel Add Cancel Add", Font = Font12, Location = new Point (110, 6), Size = new Size (97, 28), TabIndex = 2 };
                form.Controls.Add (plain);
                form.Controls.Add (accept);
                form.Controls.Add (new TextBox { Location = new Point (212, 32), Size = new Size (6, 6), TabIndex = 0 });
                form.AcceptButton = accept;
                form.Show ();

                Assert.True (accept.IsDefault);
                Assert.False (plain.IsDefault);

                var one = plain.LogicalToDeviceUnits (1);
                var field = Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (plain).TextBounds;
                var wider = Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (accept).TextBounds;

                Assert.Equal (field.Width + 2 * one, wider.Width);
                Assert.Equal (field.Left - one, wider.Left);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
