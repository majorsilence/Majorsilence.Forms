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

        // Pixels of the upstream chevron's grey in the combo's right-hand 16px.
        private static int ChevronPixels (Form form, Control combo)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
            var scale = bitmap.Width / 300f;
            var count = 0;

            for (var x = (int) ((combo.Right - 16) * scale); x < (int) (combo.Right * scale); x++)
                for (var y = (int) (combo.Top * scale); y < (int) (combo.Bottom * scale); y++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Red is > 0x50 and < 0x90 && c.Red == c.Green && c.Green == c.Blue)
                        count++;
                }

            return count;
        }

        [Fact]
        public void A_drop_down_list_has_a_near_white_face_and_a_thin_chevron ()
            => With (true, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point (10, 10), Size = new Size (150, 23) };
                form.Controls.Add (combo);

                Assert.Equal (new SKColor (0xFD, 0xFD, 0xFD), CentrePixel (form, combo));
                Assert.True (ChevronPixels (form, combo) > 3, "the chevron is drawn in upstream's grey");
                return 0;
            });

        [Fact]
        public void An_editable_combo_has_a_white_field ()
            => With (true, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Location = new Point (10, 10), Size = new Size (150, 23) };
                form.Controls.Add (combo);

                Assert.Equal (System.Drawing.Color.White.ToArgb (), combo.BackColor.ToArgb ());
                return 0;
            });

        [Fact]
        public void Without_a_chosen_font_the_theme_combo_is_kept ()
            => With (false, form => {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point (10, 10), Size = new Size (150, 23) };
                form.Controls.Add (combo);

                Assert.NotEqual (new SKColor (0xFD, 0xFD, 0xFD), CentrePixel (form, combo));
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
