using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The default button -- the focused one, or the AcceptButton while no button has focus -- lays its caption out a
    // pixel further out each way than the others (upstream's OnePixExtraBorder). Measured (.NET 10, GDI text), 12pt
    // "Cancel Add" on 97 x 28 buttons: the default button's text field ran 3..94 and showed the caption on one line;
    // the others' ran 4..93 and wrapped it to "Cancel". Here every button had the narrower field, so a "Cancel &Add"
    // button showed "Cancel" even with the focus.
    [Collection ("Headless")]
    public class ButtonDefaultTextFieldTests
    {
        private static Majorsilence.Forms.Drawing.Font Font12 => new ("Microsoft Sans Serif", 12f);

        private static float Advance (Button button, string text)
            => TextMeasurer.MeasureText (text, button.GetEffectiveFont (), button.LogicalToDeviceUnits (button.GetEffectiveFontSize ())).Width;

        // The width of the caption's ink inside the button, in device pixels.
        private static int InkWidth (Form form, Button button)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, form.ClientSize.Width, form.ClientSize.Height));
            var scale = bitmap.Width / (float) form.ClientSize.Width;

            int left = int.MaxValue, right = -1;
            for (var y = (int) ((button.Top + 3) * scale); y < (int) ((button.Bottom - 3) * scale); y++)
                for (var x = (int) ((button.Left + 3) * scale); x < (int) ((button.Right - 3) * scale); x++) {
                    var p = bitmap.GetPixel (x, y);
                    if (p.Red + p.Green + p.Blue < 300) {
                        left = System.Math.Min (left, x);
                        right = System.Math.Max (right, x);
                    }
                }

            return right < 0 ? 0 : right - left + 1;
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
                Assert.Equal (field.Top - one, wider.Top);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_caption_that_only_fits_the_default_field_shows_whole_on_the_focused_button ()
        {
            try {
                Application.SetDefaultFont (Font12);
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (240, 40) };
                var focused = new Button { Text = "Cancel &Add", Font = Font12, Location = new Point (0, 6), TabIndex = 0 };
                var other = new Button { Text = "Cancel &Add", Font = Font12, Location = new Point (120, 6), TabIndex = 1 };
                form.Controls.Add (focused);
                form.Controls.Add (other);
                form.Show ();

                // A pixel narrower than the caption in the usual field, a pixel wider in the default one.
                var device = focused.LogicalToDeviceUnits (100) / 100f;
                var advance = Advance (focused, "Cancel Add");
                var width = (int) System.Math.Ceiling (advance / device) + 2 * (4 + (int) System.Math.Ceiling (focused.Font.Height / 6.0)) - 1;
                focused.Size = other.Size = new Size (width, 28);
                focused.Focus ();

                Assert.True (focused.IsDefault);
                Assert.False (other.IsDefault);

                var whole = InkWidth (form, focused);
                var wrapped = InkWidth (form, other);

                Assert.True (whole > Advance (focused, "Cancel A"), $"{whole}px of ink: the focused button shows \"Cancel Add\"");
                Assert.True (wrapped < Advance (other, "Cancel A"), $"{wrapped}px of ink: the other button shows \"Cancel\"");
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
