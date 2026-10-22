using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the push button
    // WinForms draws under the Windows 11 theme: a near-white rounded face, the default button outlined in
    // the accent. Apps that have not chosen a font keep the theme's button.
    [Collection ("Headless")]
    public class ButtonUpstreamLookTests
    {
        private static readonly SKColor Face = new SKColor (0xFD, 0xFD, 0xFD);
        private static readonly SKColor Accent = new SKColor (0x00, 0x78, 0xD4);

        private static (int face, int accent) Pixels (bool chooseFont, bool isDefault, System.Action<Button>? configure = null)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 40) };
                var button = new Button { Text = "OK", Location = new Point (10, 5), Size = new Size (90, 28), TabIndex = 1 };
                configure?.Invoke (button);
                form.Controls.Add (button);

                // Takes the focus on Show, so the button is not the default just for being focused.
                form.Controls.Add (new TextBox { Location = new Point (110, 30), Size = new Size (6, 6), TabIndex = 0 });
                if (isDefault)
                    form.AcceptButton = button;
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 120, 40));
                int face = 0, accent = 0;

                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++) {
                        var c = bitmap.GetPixel (x, y);
                        if (c == Face) face++;
                        else if (c == Accent) accent++;
                    }

                return (face, accent);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void The_default_button_has_a_near_white_face_and_an_accent_outline ()
        {
            var (face, accent) = Pixels (chooseFont: true, isDefault: true);

            Assert.True (face > 1000, $"{face} face pixels");
            Assert.True (accent > 100, $"{accent} accent outline pixels");
        }

        // Upstream lays a push button's caption out inside its 2px border size and a 2px TextImageInset, so a TopLeft
        // caption starts 4px in. Here it was laid out in the whole client area and sat in the corner.
        [Fact]
        public void A_top_left_caption_keeps_upstreams_inset ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 60) };
                var dots = new Button { Text = "***", Location = new Point (10, 10), Size = new Size (40, 24), TextAlign = ContentAlignment.TopLeft };
                var other = new Button { Text = "x", Location = new Point (70, 10), TabIndex = 0 };
                form.Controls.Add (other);
                form.Controls.Add (dots);
                form.Show ();
                other.Select ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 120, 60));
                var scale = bitmap.Width / 120f;
                int left = int.MaxValue, top = int.MaxValue;

                for (var x = (int) (dots.Left * scale); x < (int) (dots.Right * scale); x++)
                    for (var y = (int) (dots.Top * scale); y < (int) (dots.Bottom * scale); y++)
                        if (bitmap.GetPixel (x, y).Red < 0x60) {
                            left = System.Math.Min (left, x - (int) (dots.Left * scale));
                            top = System.Math.Min (top, y - (int) (dots.Top * scale));
                        }

                Assert.True (left >= (int) (4 * scale), $"the caption starts {left}px in");
                Assert.True (top >= (int) (4 * scale), $"the caption starts {top}px down");
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Another_button_is_outlined_in_grey ()
            => Assert.Equal (0, Pixels (chooseFont: true, isDefault: false).accent);

        // As upstream's Form.UpdateDefaultButton: the focused button is the form's default button, whatever
        // AcceptButton says, so it is outlined in the accent. Only the AcceptButton ever was.
        [Fact]
        public void The_focused_button_is_the_default_one ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 60) };
                var ok = new Button { Text = "OK", Location = new Point (10, 5), TabIndex = 1 };
                var cancel = new Button { Text = "Cancel", Location = new Point (110, 5), TabIndex = 2 };
                var field = new TextBox { Location = new Point (210, 5), TabIndex = 0 };
                form.Controls.Add (ok);
                form.Controls.Add (cancel);
                form.Controls.Add (field);
                form.AcceptButton = ok;
                form.Show ();

                Assert.True (ok.IsDefault);   // the text box has the focus: the AcceptButton is the default

                cancel.Select ();
                Assert.True (cancel.IsDefault);
                Assert.False (ok.IsDefault);

                field.Select ();
                Assert.True (ok.IsDefault);
                Assert.False (cancel.IsDefault);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_button_with_its_own_back_colour_keeps_it ()
            => Assert.Equal (0, Pixels (chooseFont: true, isDefault: true, b => b.BackColor = System.Drawing.Color.LightYellow).face);

        // The native (System) button draws both lines of a caption that wraps on a one-line button, centred and clipped
        // at the top and bottom; the themed (Standard) one keeps the first line whole and drops the second, as upstream's
        // TextBoxControl flag does. A System "Show Emails" button showed "Show" alone.
        [Theory]
        [InlineData (FlatStyle.System, 2)]
        [InlineData (FlatStyle.Standard, 1)]
        public void A_caption_that_wraps_on_a_one_line_button_shows_upstream_s_lines (FlatStyle style, int lines)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 40) };
                var button = new Button { Text = "Show Emails", FlatStyle = style, Location = new Point (10, 5), Size = new Size (50, 23), TabIndex = 1 };
                form.Controls.Add (button);
                form.Controls.Add (new TextBox { Location = new Point (110, 30), Size = new Size (6, 6), TabIndex = 0 });
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 120, 40));
                var scale = bitmap.Width / 120f;

                // The runs of rows with ink in them, inside the button's frame.
                var bands = 0;
                var inked = false;
                for (var y = (int) ((button.Top + 2) * scale); y < (int) ((button.Bottom - 2) * scale); y++) {
                    var ink = false;
                    for (var x = (int) ((button.Left + 3) * scale); x < (int) ((button.Right - 3) * scale) && !ink; x++) {
                        var p = bitmap.GetPixel (x, y);
                        ink = p.Red + p.Green + p.Blue < 300;
                    }

                    if (ink && !inked)
                        bands++;
                    inked = ink;
                }

                Assert.Equal (lines, bands);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Without_a_chosen_font_the_theme_button_is_kept ()
            => Assert.Equal (0, Pixels (chooseFont: false, isDefault: true).face);

        // A FlatStyle.System button is the native control: its caption has all but 3px of the button each side, default or
        // not, measured without TextRenderer's padding -- "Show Emails" in Segoe UI 9pt fits a 72px button. It was given
        // the themed adapter's narrower field and wrapped below 78px, so a 75px button showed "Show" alone.
        [Theory]
        [InlineData (false)]
        [InlineData (true)]
        public void A_system_button_s_caption_has_all_but_3px_each_side (bool isDefault)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 40) };
                var system = new Button { Text = "Show Emails", FlatStyle = FlatStyle.System, Location = new Point (10, 5), TabIndex = 1 };
                var standard = new Button { Text = "Show Emails", FlatStyle = FlatStyle.Standard, Location = new Point (100, 5), TabIndex = 2 };
                form.Controls.Add (system);
                form.Controls.Add (standard);
                form.Controls.Add (new TextBox { Location = new Point (190, 30), Size = new Size (6, 6), TabIndex = 0 });
                if (isDefault)
                    form.AcceptButton = system;
                form.Show ();

                var advance = TextMeasurer.MeasureText ("Show Emails", system.GetEffectiveFont (), system.LogicalToDeviceUnits (system.GetEffectiveFontSize ())).Width;
                var width = (int) System.Math.Ceiling (advance / (system.LogicalToDeviceUnits (100) / 100f)) + 6;
                system.Size = new Size (width, 23);
                standard.Size = new Size (width, 23);

                Assert.True (Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (system).TextBounds.Width >= advance,
                    $"a {width}px System button holds its {advance}px caption on one line");

                // The themed (Standard) button keeps the adapter's narrower field.
                Assert.True (Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (standard).TextBounds.Width < advance);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
