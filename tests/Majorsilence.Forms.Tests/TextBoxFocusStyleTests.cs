using System;
using System.Linq;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #366: a themed TextBox lost its border while it had keyboard focus. A focused control resolved its look through Control's own focus style,
    // which is a copy of Control's default and knows nothing of the TextBox rule, so the border and fill fell back to the base colours. A
    // control type with no :focus of its own has to keep looking like itself when focused, and TextBox can now have a :focus rule.
    [Collection ("Headless")]
    public class TextBoxFocusStyleTests : IDisposable
    {
        public TextBoxFocusStyleTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static readonly SKColor Ink = new (0x11, 0x22, 0x33);

        // Left edge of a 240x50 text box at (20, 10), vertically centred: inside a 3px border.
        private static (SKColor Border, SKColor Fill) Sample (Control control, bool focused)
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 80), FormBorderStyle = FormBorderStyle.None };
            control.Location = new Point (20, 10);
            control.Size = new Size (240, 50);
            form.Controls.Add (control);
            form.Show ();

            if (focused)
                control.Focus ();

            Assert.Equal (focused, control.Focused);

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 80));
            var scale = bitmap.Width / 300f;
            return (bitmap.GetPixel ((int)(21 * scale), (int)(35 * scale)), bitmap.GetPixel ((int)(150 * scale), (int)(22 * scale)));
        }

        [Fact]
        public void A_focused_TextBox_keeps_the_border_and_fill_its_theme_gave_it ()
        {
            Theme.LoadFromCss ("TextBox { background-color: #ffffff; border: 3px solid #112233; }");

            var calm = Sample (new TextBox (), focused: false);
            var focused = Sample (new TextBox (), focused: true);

            Assert.Equal (Ink, calm.Border);
            Assert.Equal (calm, focused);
        }

        [Theory]
        [InlineData ("ListBox")]
        [InlineData ("CheckBox")]
        public void A_focused_control_with_no_focus_rule_keeps_the_fill_its_theme_gave_it (string type)
        {
            // Every type with no :focus of its own fell back to Control's look when focused, not just TextBox.
            Theme.LoadFromCss ($"{type} {{ background-color: #ffeedd; }}");
            Control Make () => type switch {
                "ListBox" => new ListBox (),
                _ => new CheckBox (),
            };

            var calm = Sample (Make (), focused: false);
            var focused = Sample (Make (), focused: true);

            Assert.Equal (calm.Fill, focused.Fill);
        }

        [Fact]
        public void A_TextBox_focus_rule_applies_only_while_focused ()
        {
            Theme.LoadFromCss (@"
                TextBox { background-color: #ffffff; border: 3px solid #112233; }
                TextBox:focus { border: 3px solid #aa0000; }
            ");

            var calm = Sample (new TextBox (), focused: false);
            var focused = Sample (new TextBox (), focused: true);

            Assert.Equal (Ink, calm.Border);
            Assert.Equal (new SKColor (0xaa, 0x00, 0x00), focused.Border);
            Assert.Equal (calm.Fill, focused.Fill);      // only the border was restyled; the fill is still the TextBox rule's
        }

        [Fact]
        public void A_TextBox_focus_rule_is_accepted_without_a_diagnostic ()
        {
            var sheet = ThemeStyleSheet.Parse ("TextBox:focus { border: 3px solid #aa0000; }");

            Assert.Empty (sheet.Diagnostics);
            Assert.NotNull (ThemeCssReference.Selectors.First (s => s.Name == "TextBox").FocusStyle);
        }
    }
}
