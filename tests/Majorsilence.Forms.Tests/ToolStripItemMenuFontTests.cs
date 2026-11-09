using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported app's ToolStrip items are measured as upstream's: in the strip's font, SystemFonts.MenuFont (Segoe UI 9pt) unless
    // the strip or item has one, as TextRenderer measures text -- on GDI's grid with its padding. Measured with a WinForms probe
    // (Microsoft Sans Serif 12pt as the app font): "Button0" 53px, "Btn1" beside a 16px image 51, an image-only button 23 (a
    // ToolStripButton's StandardButtonWidth) and a label "Lbl" 23, with no border of its own. MF measured them in the theme's
    // 14px font, rounding device pixels, so "Button0" came out 54 at scale 1, the image 4px further from its text, an image-only
    // button 20 and a label 27.
    [Collection ("Headless")]
    public class ToolStripItemMenuFontTests
    {
        private static void Ported () => Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

        private static Rectangle[] Show (out Padding labelPadding)
        {
            HeadlessRenderer.Use ();
            var image = new Majorsilence.Forms.Drawing.Bitmap (16, 16);
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 200) };
            var strip = new ToolStrip ();
            var label = new ToolStripLabel ("Lbl");

            foreach (var item in new ToolStripItem[] { new ToolStripButton ("Button0"), new ToolStripButton ("Btn1", image), new ToolStripButton ("", image), label })
                strip.Items.Add (item);

            form.Controls.Add (strip);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 200);

            labelPadding = label.Padding;
            return strip.Items.Cast<ToolStripItem> ().Select (i => i.Bounds).ToArray ();
        }

        [Fact]
        public void A_ported_app_s_tool_strip_items_are_upstream_s_widths ()
        {
            Rectangle[] items;
            Padding label_padding;
            int text_width, menu_text_width;

            try {
                Ported ();
                items = Show (out label_padding);
                text_width = TextRenderer.MeasureText ("Button0", SystemFonts.MenuFont).Width;
                menu_text_width = TextRenderer.MeasureText ("Lbl", SystemFonts.MenuFont).Width;
            } finally {
                Application.SetDefaultFont (null!);
            }

            // The text as TextRenderer measures it in the menu font, inside a button's 2px border; a label is its text.
            Assert.Equal (text_width + 4, items[0].Width);
            Assert.Equal (menu_text_width, items[3].Width);
            Assert.Equal (Padding.Empty, label_padding);

            // An image-only button is never narrower than 23.
            Assert.Equal (23, items[2].Width);

            if (OperatingSystem.IsWindows ())
                Assert.Equal (new[] { 53, 51, 23, 23 }, items.Select (i => i.Width));
        }

        [Fact]
        public void Without_a_chosen_font_tool_strip_items_keep_the_theme_s_widths ()
        {
            var items = Show (out var label_padding);

            Assert.Equal (new Padding (2), label_padding);
            Assert.Equal (20, items[2].Width);
        }
    }
}
