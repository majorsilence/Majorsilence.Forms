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

        // Upstream's date picker is the native control: one darker line all round (#8D8D8D under Windows 11) and, with
        // ShowUpDown, spin buttons with a near-white face in a pale outline. It was drawn as a text box, its pale
        // outline all but invisible on the control colour, and its spin buttons as the theme's flat face.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_date_picker_has_the_native_frame_and_spin_buttons (bool chooseFont)
            => With (chooseFont, form => {
                var picker = new DateTimePicker { Location = new Point (10, 10), Size = new Size (120, 26), ShowUpDown = true };
                form.Controls.Add (picker);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var frame = new SKColor (0x8D, 0x8D, 0x8D);
                var mid = (int) ((picker.Top + picker.Height / 2) * scale);
                var left = bitmap.GetPixel ((int) (picker.Left * scale), mid);
                var top = bitmap.GetPixel ((int) ((picker.Left + 30) * scale), (int) (picker.Top * scale));
                var spin_faces = 0;

                for (var x = (int) ((picker.Right - 20) * scale); x < (int) (picker.Right * scale); x++)
                    for (var y = (int) (picker.Top * scale); y < (int) (picker.Bottom * scale); y++)
                        if (bitmap.GetPixel (x, y) == new SKColor (0xFA, 0xFA, 0xFA))
                            spin_faces++;

                if (chooseFont) {
                    Assert.Equal (frame, left);
                    Assert.Equal (frame, top);
                    Assert.True (spin_faces > 20, $"{spin_faces} spin button face pixels");
                } else {
                    // The theme's own picker: not the native frame. (Its button colours are the theme's and may
                    // include the spin face's near-white, so that is not asserted against.)
                    Assert.NotEqual (frame, left);
                }

                return 0;
            });

        // Upstream's Windows 11 text box underlines itself in the accent while it has the keyboard focus.
        [Fact]
        public void A_focused_text_box_is_underlined_in_the_accent ()
            => With (true, form => {
                var focused = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20), TabIndex = 0 };
                var other = new TextBox { Location = new Point (10, 60), Size = new Size (120, 20), TabIndex = 1 };
                form.Controls.Add (focused);
                form.Controls.Add (other);
                focused.Select ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var accent = new SKColor (0x00, 0x67, 0xC0);
                SKColor Bottom (TextBox box, int up) => bitmap.GetPixel ((int) ((box.Left + box.Width / 2) * scale), (int) (box.Bottom * scale) - up);

                Assert.True (focused.Focused);
                Assert.Equal (accent, Bottom (focused, 1));
                Assert.Equal (accent, Bottom (focused, (int) (2 * scale)));
                Assert.NotEqual (accent, Bottom (other, 1));
                return 0;
            });

        // The native edit control keeps the same margin on both sides: right-aligned text ends as far in from
        // the right edge as left-aligned text starts from the left. Here right-aligned text ran to the edge.
        [Fact]
        public void Right_aligned_text_keeps_the_margin_left_aligned_text_has ()
            => With (true, form => {
                var left = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20), Text = "8888" };
                var right = new TextBox { Location = new Point (10, 60), Size = new Size (120, 20), Text = "8888", TextAlign = HorizontalAlignment.Right };
                form.Controls.Add (left);
                form.Controls.Add (right);
                form.ActiveControl = null;

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;

                // The first and last columns holding dark (text) pixels across a box.
                (int first, int last) Ink (TextBox box)
                {
                    int first = -1, last = -1;

                    for (var x = (int) (box.Left * scale); x < (int) (box.Right * scale); x++)
                        for (var y = (int) ((box.Top + 3) * scale); y < (int) ((box.Bottom - 3) * scale); y++)
                            if (bitmap.GetPixel (x, y).Red < 0x80) {
                                if (first < 0) first = x;
                                last = x;
                                break;
                            }

                    return (first, last);
                }

                var left_inset = Ink (left).first - (int) (left.Left * scale);
                var right_inset = (int) (right.Right * scale) - 1 - Ink (right).last;

                // The glyphs' own side bearings differ by platform font, so the two insets agree only roughly: the
                // right one keeps at least the margin, and sits no further in than the left one does, give or take.
                Assert.True (right_inset >= (int) (2 * scale) && right_inset <= left_inset + (int) (2 * scale), $"left {left_inset} right {right_inset} ink {Ink (right)} box {right.Bounds} scale {scale}");
                return 0;
            });

        // Upstream's themed group box: one pale line round the frame. It was the classic etched pair, a dark
        // grey line with a lighter one inside it.
        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_group_box_frame_is_one_pale_line (bool chooseFont)
            => With (chooseFont, form => {
                var group = new GroupBox { Text = "Earning", Location = new Point (10, 10), Size = new Size (150, 100) };
                form.Controls.Add (group);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var y = (int) ((group.Top + 60) * scale);
                var pale = new SKColor (0xDC, 0xDC, 0xDC);
                var darkest = (byte) 0xFF;
                var has_pale = false;

                // The left side of the frame, a few device pixels in from the box's edge.
                for (var x = (int) (group.Left * scale); x < (int) ((group.Left + 4) * scale); x++) {
                    var c = bitmap.GetPixel (x, y);
                    has_pale |= c == pale;
                    darkest = System.Math.Min (darkest, c.Red);
                }

                if (chooseFont) {
                    Assert.True (has_pale, "no pale frame line");
                    Assert.Equal (0xDC, darkest);
                } else {
                    Assert.True (darkest < 0xDC, "the theme's frame was replaced without a chosen font");
                }

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
