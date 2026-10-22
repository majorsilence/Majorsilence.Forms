using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the check box
    // and radio button WinForms draws under the Windows 11 theme: filled with the accent when set. The
    // theme's square-in-a-square read as a different control beside WinForms'. Apps that have not chosen
    // a font keep the theme's glyphs.
    [Collection ("Headless")]
    public class UpstreamGlyphTests
    {
        private static readonly SKColor Accent = new SKColor (0x00, 0x5F, 0xB8);

        private static int AccentPixels (bool chooseFont, Control control) => Pixels (chooseFont, control, Accent);

        // The pixels within tolerance of a colour: an antialiased 1px circle is the colour exactly at only a few points.
        private static int Pixels (bool chooseFont, Control control, SKColor colour, int tolerance = 0)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (120, 30) };
                control.Location = new Point (4, 4);
                form.Controls.Add (control);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 120, 30));
                var count = 0;

                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++)
                        if (bitmap.GetPixel (x, y) is var p && System.Math.Abs (p.Red - colour.Red) <= tolerance
                            && System.Math.Abs (p.Green - colour.Green) <= tolerance && System.Math.Abs (p.Blue - colour.Blue) <= tolerance)
                            count++;

                return count;
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_checked_check_box_is_filled_with_the_accent ()
            => Assert.True (AccentPixels (true, new CheckBox { Text = "x", Checked = true, AutoSize = true }) > 60);

        [Fact]
        public void A_checked_radio_button_is_filled_with_the_accent ()
            => Assert.True (AccentPixels (true, new RadioButton { Text = "x", Checked = true, AutoSize = true }) > 40);

        // Disabled, WinForms' glyphs are #C3C3C3 (measured under Windows 11): the outline of a clear box or circle, round a
        // #F9F9F9 face, and the fill of a set one. They were #A0A0A0 on the enabled #F3F3F3 face.
        [Theory]
        [InlineData (false, false)]
        [InlineData (true, false)]
        [InlineData (false, true)]
        [InlineData (true, true)]
        public void A_disabled_glyph_is_upstream_s_pale_grey (bool isChecked, bool radio)
        {
            var disabled = new SKColor (0xC3, 0xC3, 0xC3);
            Control Make () => radio
                ? new RadioButton { Text = "", Checked = isChecked, AutoSize = true, Enabled = false }
                : new CheckBox { Text = "", Checked = isChecked, AutoSize = true, Enabled = false };

            // A clear radio button's 1px circle is antialiased all round (its outline is about #C6 to #D1 at scale 1).
            var tolerance = radio && !isChecked ? 15 : 3;
            Assert.True (Pixels (true, Make (), disabled, tolerance) > (isChecked ? 40 : 12), "the glyph is drawn in #C3C3C3");
            Assert.Equal (0, Pixels (true, Make (), new SKColor (0xA0, 0xA0, 0xA0)));

            if (!isChecked)
                Assert.True (Pixels (true, Make (), new SKColor (0xF9, 0xF9, 0xF9)) > 20, "a clear glyph's face is #F9F9F9");
        }

        [Fact]
        public void Without_a_chosen_font_the_theme_glyphs_are_kept ()
            => Assert.Equal (0, AccentPixels (false, new CheckBox { Text = "x", Checked = true, AutoSize = true }));
    }
}
