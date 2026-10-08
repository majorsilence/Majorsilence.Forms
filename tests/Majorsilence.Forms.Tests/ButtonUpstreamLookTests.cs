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
                var button = new Button { Text = "OK", Location = new Point (10, 5), Size = new Size (90, 28) };
                configure?.Invoke (button);
                form.Controls.Add (button);
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

        [Fact]
        public void Another_button_is_outlined_in_grey ()
            => Assert.Equal (0, Pixels (chooseFont: true, isDefault: false).accent);

        [Fact]
        public void A_button_with_its_own_back_colour_keeps_it ()
            => Assert.Equal (0, Pixels (chooseFont: true, isDefault: true, b => b.BackColor = System.Drawing.Color.LightYellow).face);

        [Fact]
        public void Without_a_chosen_font_the_theme_button_is_kept ()
            => Assert.Equal (0, Pixels (chooseFont: false, isDefault: true).face);
    }
}
