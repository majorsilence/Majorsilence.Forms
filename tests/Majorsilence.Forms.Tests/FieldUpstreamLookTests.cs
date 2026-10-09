using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets WinForms'
    // Windows 11 text and list boxes: a white field (SystemColors.Window) in a light outline, and a
    // single-line text box as tall as its font makes it (Font.Height + 7, 23px for Segoe UI 9pt).
    [Collection ("Headless")]
    public class FieldUpstreamLookTests
    {
        private static T With<T> (bool chooseFont, System.Func<Form, T> act)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
                form.Show ();
                return act (form);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static SKColor CentrePixel (Form form, Control control)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));

            // The capture is in device pixels, which CI also runs at scale 2 (MF_HEADLESS_SCALE).
            var scale = bitmap.Width / 300f;
            return bitmap.GetPixel ((int) ((control.Left + control.Width / 2) * scale), (int) ((control.Top + 3) * scale));
        }

        [Fact]
        public void A_text_box_is_a_white_field_as_tall_as_its_font ()
            => With (true, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20) };
                form.Controls.Add (box);

                Assert.Equal (box.Font.Height + 7, box.Height);
                Assert.Equal (SKColors.White, CentrePixel (form, box));
                Assert.Equal (System.Drawing.Color.White.ToArgb (), box.BackColor.ToArgb ());
                return 0;
            });

        [Fact]
        public void A_list_box_is_a_white_field ()
            => With (true, form => {
                var list = new ListBox { Location = new Point (10, 50), Size = new Size (120, 100) };
                form.Controls.Add (list);

                Assert.Equal (SKColors.White, CentrePixel (form, list));
                return 0;
            });

        [Fact]
        public void A_read_only_or_coloured_box_keeps_its_colour ()
            => With (true, form => {
                var read_only = new TextBox { ReadOnly = true, Location = new Point (10, 10), Size = new Size (120, 20) };
                var coloured = new TextBox { BackColor = System.Drawing.Color.LightYellow, Location = new Point (10, 50), Size = new Size (120, 20) };
                form.Controls.Add (read_only);
                form.Controls.Add (coloured);

                Assert.NotEqual (SKColors.White, CentrePixel (form, read_only));
                Assert.Equal (System.Drawing.Color.LightYellow.ToArgb (), coloured.BackColor.ToArgb ());
                return 0;
            });

        [Fact]
        public void A_multiline_box_keeps_its_height ()
            => With (true, form => {
                var box = new TextBox { Multiline = true, Location = new Point (10, 10), Size = new Size (120, 60) };
                form.Controls.Add (box);
                Assert.Equal (60, box.Height);
                return 0;
            });

        [Fact]
        public void Without_a_chosen_font_the_theme_field_is_kept ()
            => With (false, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20) };
                form.Controls.Add (box);

                Assert.Equal (20, box.Height);
                Assert.NotEqual (SKColors.White, CentrePixel (form, box));
                return 0;
            });
    }
}
