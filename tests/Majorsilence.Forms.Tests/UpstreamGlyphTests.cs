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

        private static int AccentPixels (bool chooseFont, Control control)
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
                        if (bitmap.GetPixel (x, y) == Accent)
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

        [Fact]
        public void Without_a_chosen_font_the_theme_glyphs_are_kept ()
            => Assert.Equal (0, AccentPixels (false, new CheckBox { Text = "x", Checked = true, AutoSize = true }));
    }
}
