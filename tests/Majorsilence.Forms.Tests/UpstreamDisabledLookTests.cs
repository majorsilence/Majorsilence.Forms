using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) draws its disabled controls as
    // WinForms does under Windows 11, as measured from it. Every disabled control's text was the theme's one grey
    // (#AAAAAA), a disabled text box kept the enabled box's dark bottom line, and a disabled tab control greyed tabs
    // the native control draws as it always does.
    [Collection ("Headless")]
    public class UpstreamDisabledLookTests
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

        // The darkest pixel inside a control: its text, in a font heavy enough that some pixels are wholly covered.
        private static SKColor Darkest (Form form, Control control)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
            var scale = bitmap.Width / 300f;
            var darkest = SKColors.White;

            for (var x = (int) ((control.Left + 2) * scale); x < (int) ((control.Right - 2) * scale); x++)
                for (var y = (int) ((control.Top + 2) * scale); y < (int) ((control.Bottom - 2) * scale); y++) {
                    var p = bitmap.GetPixel (x, y);
                    if (p.Red + p.Green + p.Blue < darkest.Red + darkest.Green + darkest.Blue)
                        darkest = p;
                }

            return darkest;
        }

        public static TheoryData<string, uint> DisabledText => new () {
            { "Label", 0xFFA0A0A0 },        // TextRenderer.DisabledTextColor on a light background: ControlDark
            { "GroupBox", 0xFFA0A0A0 },
            { "CheckBox", 0xFF6D6D6D },     // the theme's disabled check box text
            { "RadioButton", 0xFF6D6D6D },
            { "Button", 0xFF838383 },       // the theme's disabled push button text
            { "TextBox", 0xFF6D6D6D },      // GrayText
            { "ComboBox", 0xFF6D6D6D },
        };

        private static Control Make (string type)
        {
            var font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 20f, Majorsilence.Forms.Drawing.FontStyle.Bold);
            Control control = type switch {
                "Label" => new Label { Text = "WWW" },
                "GroupBox" => new GroupBox { Text = "WWW" },
                "CheckBox" => new CheckBox { Text = "WWW" },
                "RadioButton" => new RadioButton { Text = "WWW" },
                "Button" => new Button { Text = "WWW" },
                "TextBox" => new TextBox { Text = "WWW" },
                _ => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList },
            };

            if (control is ComboBox combo) {
                combo.Items.Add ("WWW");
                combo.SelectedIndex = 0;
            }

            control.Font = font;
            control.Location = new Point (10, 10);
            control.Size = new Size (160, 60);
            control.Enabled = false;
            return control;
        }

        [Theory]
        [MemberData (nameof (DisabledText))]
        public void A_disabled_control_s_text_is_upstream_s_colour (string type, uint expected)
            => With (true, form => {
                var control = Make (type);
                form.Controls.Add (control);

                Assert.Equal (new SKColor (expected), Darkest (form, control));
                return 0;
            });

        [Fact]
        public void Without_a_chosen_font_disabled_text_keeps_the_theme_colour ()
            => With (false, form => {
                var label = Make ("Label");
                form.Controls.Add (label);

                Assert.Equal (Theme.ForegroundDisabledColor, Darkest (form, label));
                return 0;
            });

        // The native tab control draws a disabled control's tabs as it draws them enabled.
        [Fact]
        public void A_disabled_tab_control_s_tabs_keep_their_text_colour ()
            => With (true, form => {
                var tabs = new TabControl {
                    Location = new Point (10, 10), Size = new Size (260, 150),
                    Font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 20f, Majorsilence.Forms.Drawing.FontStyle.Bold),
                };
                tabs.TabPages.Add (new TabPage ("WWW"));
                form.Controls.Add (tabs);
                tabs.Enabled = false;

                var strip = new Label { Location = tabs.Location, Size = new Size (tabs.Width, 40) };   // the band the tabs sit in
                Assert.Equal (SKColors.Black, Darkest (form, strip));
                return 0;
            });

        // A disabled text box: the light outline all round, its bottom #EDEDED rather than the enabled box's #838383,
        // with a #FBFBFB line just inside it round the Control face.
        [Fact]
        public void A_disabled_text_box_has_a_light_frame_and_an_inner_line ()
            => With (true, form => {
                var box = new TextBox { Location = new Point (10, 10), Size = new Size (120, 20), Enabled = false };
                form.Controls.Add (box);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                int X (int x) => (int) ((box.Left + x) * scale);
                int Y (int y) => (int) ((box.Top + y) * scale);
                var mid = X (box.Width / 2);

                Assert.Equal (new SKColor (0xEC, 0xEC, 0xEC), bitmap.GetPixel (mid, Y (0)));
                Assert.Equal (new SKColor (0xFB, 0xFB, 0xFB), bitmap.GetPixel (mid, Y (1)));
                Assert.Equal (new SKColor (0xF0, 0xF0, 0xF0), bitmap.GetPixel (mid, Y (box.Height / 2)));
                Assert.Equal (new SKColor (0xFB, 0xFB, 0xFB), bitmap.GetPixel (mid, Y (box.Height - 2)));
                Assert.Equal (new SKColor (0xED, 0xED, 0xED), bitmap.GetPixel (mid, Y (box.Height - 1)));
                Assert.Equal (new SKColor (0xFB, 0xFB, 0xFB), bitmap.GetPixel (X (1), Y (box.Height / 2)));
                return 0;
            });

        // A disabled drop-down list keeps a near-white face (#FAFAFA) in one light outline (#EAEAEA) all round, its
        // chevron pale (#BDBDBD); an editable drop-down a white face in a mid-grey outline (#C8C8C8).
        [Theory]
        [InlineData (ComboBoxStyle.DropDownList, 0xFFFAFAFA, 0xFFEAEAEA)]
        [InlineData (ComboBoxStyle.DropDown, 0xFFFFFFFF, 0xFFC8C8C8)]
        public void A_disabled_combo_has_upstream_s_face_and_frame (ComboBoxStyle style, uint face, uint outline)
            => With (true, form => {
                var combo = new ComboBox { Location = new Point (10, 10), Size = new Size (120, 23), DropDownStyle = style, Enabled = false };
                form.Controls.Add (combo);

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
                var scale = bitmap.Width / 300f;
                var x = (int) ((combo.Left + 40) * scale);

                Assert.Equal (new SKColor (outline), bitmap.GetPixel (x, (int) (combo.Top * scale)));
                Assert.Equal (new SKColor (outline), bitmap.GetPixel (x, (int) ((combo.Bottom - 1) * scale)));
                Assert.Equal (new SKColor (face), bitmap.GetPixel (x, (int) ((combo.Top + 3) * scale)));

                if (style == ComboBoxStyle.DropDownList) {
                    var chevron = 0;
                    for (var cx = (int) ((combo.Right - 16) * scale); cx < (int) ((combo.Right - 2) * scale); cx++)
                        for (var cy = (int) (combo.Top * scale); cy < (int) (combo.Bottom * scale); cy++)
                            if (bitmap.GetPixel (cx, cy) == new SKColor (0xBD, 0xBD, 0xBD))
                                chevron++;
                    Assert.True (chevron >= 8, $"{chevron} pale chevron pixels");
                }

                return 0;
            });
    }
}
