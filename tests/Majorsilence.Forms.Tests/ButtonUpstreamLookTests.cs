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

        // Upstream's themed push button leaves a pixel of its bounds round its outline, in every state. Measured on
        // WinForms (.NET 10, Windows 11): an 88 x 27 Cancel button's top row and an enabled or disabled 130 x 30
        // button's top row show the parent, the outline runs along the second, and likewise at the bottom.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_button_s_outline_is_a_pixel_inside_its_bounds (bool enabled)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 40), BackColor = System.Drawing.Color.FromArgb (255, 0, 255) };
                var button = new Button { Text = string.Empty, Location = new Point (10, 5), Size = new Size (90, 28), TabIndex = 1, Enabled = enabled };
                form.Controls.Add (button);
                form.Controls.Add (new TextBox { Location = new Point (110, 30), Size = new Size (6, 6), TabIndex = 0 });
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 120, 40));
                var px = bitmap.Width / 120;
                var middle = (button.Left + button.Width / 2) * px;
                var magenta = new SKColor (0xFF, 0x00, 0xFF);

                // The parent shows through the button's outermost device pixels, top and bottom, left and right.
                for (var i = 0; i < px; i++) {
                    Assert.Equal (magenta, bitmap.GetPixel (middle, button.Top * px + i));
                    Assert.Equal (magenta, bitmap.GetPixel (middle, button.Bottom * px - 1 - i));
                    Assert.Equal (magenta, bitmap.GetPixel (button.Left * px + i, (button.Top + 14) * px));
                    Assert.Equal (magenta, bitmap.GetPixel (button.Right * px - 1 - i, (button.Top + 14) * px));
                }

                // And the outline is the next pixel in, darker than the face.
                var outline = bitmap.GetPixel (middle, (button.Top + 1) * px);
                Assert.NotEqual (magenta, outline);
                Assert.True (outline.Red < 0xF0, $"the outline should be drawn on the second row, found {outline}");
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
        [InlineData (FlatStyle.System)]
        [InlineData (FlatStyle.Standard)]
        public void A_caption_that_wraps_on_a_one_line_button_shows_upstream_s_lines (FlatStyle style)
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

                // How many lines the caption wraps to depends on the platform's stand-in for the font: the System button shows
                // them all, clipped, where the Standard one keeps the first whole line alone.
                if (style == FlatStyle.System)
                    Assert.True (bands >= 2, $"{bands} line(s) of the caption show");
                else
                    Assert.Equal (1, bands);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // The themed (Standard) button's text field is 4px inside its bounds -- its border and TextImageInset -- and across,
        // a further sixth of the line height either side for TextRenderer's padding. Measured from WinForms: a 12pt "Ok"
        // overflowing a 75 x 23 button starts its line 4px down and is clipped 4px from the bottom. The field was taken from
        // the client area, a pixel further in each way, so the caption was drawn 2px low and clipped 2px early.
        [Fact]
        public void A_standard_button_s_text_field_is_4px_inside_its_bounds ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 60) };
                var font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);
                var button = new Button { Text = "Show Emails Show Emails", Location = new Point (10, 10), Size = new Size (75, 23), Font = font, TabIndex = 1 };
                form.Controls.Add (button);
                form.Controls.Add (new TextBox { Location = new Point (190, 50), Size = new Size (6, 6), TabIndex = 0 });
                form.Show ();

                var field = Majorsilence.Forms.Layout.TextImageLayoutEngine.Layout (button).TextBounds;
                var across = button.LogicalToDeviceUnits (4 + (int) System.Math.Ceiling (font.Height / 6.0));
                var down = button.LogicalToDeviceUnits (4);

                Assert.Equal (down, field.Top);
                Assert.Equal (button.ScaledHeight - down, field.Bottom);
                Assert.Equal (across, field.Left);
                Assert.Equal (button.ScaledWidth - across, field.Right);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // Measured on WinForms (.NET 10), 12pt: a button's image is drawn at its own size, aligned in the field 4px
        // inside the bounds -- without the caption's GDI padding -- and clipped to that field. A 23x21 image on a 25x25
        // button shows its middle 17x17 at 4..20 each way; a 10x8 image aligned MiddleLeft starts 4px in. The image was
        // scaled down to fit the caption's field instead, which squeezed a search glyph into a sliver.
        [Theory]
        [InlineData (23, 21, ContentAlignment.MiddleCenter, "", 25, 25, 4, 20, 4, 20)]
        [InlineData (10, 8, ContentAlignment.MiddleLeft, "", 25, 25, 4, 13, 8, 15)]
        [InlineData (10, 8, ContentAlignment.MiddleLeft, "Go", 75, 30, 4, 13, 11, 18)]
        public void A_standard_button_draws_its_image_unscaled_and_clipped_to_its_field (
            int imageWidth, int imageHeight, ContentAlignment align, string text, int width, int height,
            int left, int right, int top, int bottom)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();
                var magenta = System.Drawing.Color.FromArgb (255, 0, 255);
                using var image = new Majorsilence.Forms.Drawing.Bitmap (imageWidth, imageHeight);
                for (var x = 0; x < imageWidth; x++)
                    for (var y = 0; y < imageHeight; y++)
                        image.SetPixel (x, y, magenta);

                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 50) };
                var button = new Button { Text = text, Location = new Point (10, 10), Size = new Size (width, height), Image = image, ImageAlign = align, TabIndex = 1 };
                form.Controls.Add (button);
                form.Controls.Add (new TextBox { Location = new Point (110, 40), Size = new Size (6, 6), TabIndex = 0 });
                form.Show ();

                var scale = button.LogicalToDeviceUnits (100) / 100.0;
                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 120, 50));
                int l = int.MaxValue, t = int.MaxValue, r = -1, b = -1;
                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++) {
                        var c = bitmap.GetPixel (x, y);
                        if (c.Red > 240 && c.Green < 20 && c.Blue > 240) {
                            l = System.Math.Min (l, x); t = System.Math.Min (t, y); r = System.Math.Max (r, x); b = System.Math.Max (b, y);
                        }
                    }

                // In device pixels: the field scales, the image does not (upstream leaves a button's image at its own
                // pixel size). At 100% that is exactly what WinForms drew.
                var origin = button.LogicalToDeviceUnits (10);
                var inset = button.LogicalToDeviceUnits (4);
                var field = new Rectangle (inset, inset, button.ScaledWidth - 2 * inset, button.ScaledHeight - 2 * inset);
                var visible = Rectangle.Intersect (Majorsilence.Forms.Layout.LayoutUtils.Align (new Size (imageWidth, imageHeight), field, align), field);
                Assert.Equal ((visible.Left, visible.Right - 1, visible.Top, visible.Bottom - 1), (l - origin, r - origin, t - origin, b - origin));

                if (scale == 1)
                    Assert.Equal ((left, right, top, bottom), (l - origin, r - origin, t - origin, b - origin));
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
