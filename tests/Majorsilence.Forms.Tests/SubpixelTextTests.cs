using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Text asks for subpixel edging, but Skia renders subpixel (LCD) text only into a surface that
    // declares its pixel geometry; a canvas over a bare back-buffer bitmap has none, so every glyph fell
    // back to grey antialiasing and read lighter than upstream's ClearType. On Windows, once the app has
    // chosen a font, back buffers declare the geometry. Apps that have not keep the theme's look.
    [Collection ("Headless")]
    public class SubpixelTextTests
    {
        // Counts the pixels whose channels differ: subpixel text has coloured fringes, grey text has none.
        private static int ColouredPixels (bool chooseFont)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 40) };
                form.Controls.Add (new Label { Text = "Fore Color:", Location = new Point (4, 4), AutoSize = true });
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 200, 40));
                var coloured = 0;

                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++) {
                        var c = bitmap.GetPixel (x, y);
                        if (Math.Abs (c.Red - c.Green) > 24 || Math.Abs (c.Green - c.Blue) > 24)
                            coloured++;
                    }

                return coloured;
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void With_a_chosen_font_text_is_drawn_with_subpixel_edges_on_Windows ()
        {
            if (!OperatingSystem.IsWindows ())
                return;

            Assert.True (ColouredPixels (chooseFont: true) > 10, "the label's text should have subpixel colour fringes");
        }

        [Fact]
        public void Without_a_chosen_font_text_keeps_grey_antialiasing ()
        {
            Assert.Equal (0, ColouredPixels (chooseFont: false));
        }
    }
}
