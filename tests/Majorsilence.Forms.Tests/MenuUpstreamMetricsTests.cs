using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A menu bar keeps the theme's look until the app chooses a font -- on the strip, or app-wide with
    // Application.SetDefaultFont, as a ported WinForms app does. Then it takes upstream's metrics:
    // SystemFonts.MenuFont, 4px item padding, the strip's (6, 2, 0, 2) padding, and a height fitted to
    // its items. ReportDesigner's "File" was 49px against WinForms' 37 and its menu bar 4px short.
    [Collection ("Headless")]
    public class MenuUpstreamMetricsTests
    {
        private static (Form form, MenuStrip menu, ToolStripMenuItem file) Shell (Action<MenuStrip>? configure = null)
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (600, 300) };
            var menu = new MenuStrip ();
            var file = new ToolStripMenuItem ("&File");
            menu.Items.Add (file);
            menu.Items.Add (new ToolStripMenuItem ("&Help"));
            configure?.Invoke (menu);
            form.Controls.Add (menu);
            form.MainMenuStrip = menu;
            form.Show ();
            HeadlessRenderer.CapturePng (form, 600, 300);   // a strip lays its items out when it paints

            return (form, menu, file);
        }

        [Fact]
        public void Without_a_chosen_font_the_menu_keeps_the_theme_metrics ()
        {
            var (form, menu, file) = Shell ();

            using (form) {
                Assert.Null (menu.UpstreamFont);
                Assert.Equal (new Padding (14, 3, 14, 3), file.Padding);
                Assert.Equal (0, file.Bounds.X);
            }
        }

        [Fact]
        public void An_app_wide_font_gives_the_menu_upstream_metrics ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                var (form, menu, file) = Shell ();

                using (form) {
                    var font = SystemFonts.MenuFont;
                    Assert.Equal ((font.Name, font.SizeInPoints), (menu.UpstreamFont!.Name, menu.UpstreamFont.SizeInPoints));

                    Assert.Equal (new Padding (4, 0, 4, 0), file.Padding);
                    Assert.Equal (new Padding (6, 2, 0, 2), menu.Padding);
                    Assert.Equal (6, file.Bounds.X);
                    Assert.Equal (font.Height + 4 + 4, menu.Height);

                    // Text plus GDI's sixth-of-a-line padding each side, the item padding and its 2px border.
                    // Measured and rounded in device pixels, as the renderer does, then taken back to logical:
                    // rounding in logical units disagrees by a pixel at a fractional width under scale 2.
                    var text = TextMeasurer.MeasureText ("File", menu.ItemTypeface, menu.LogicalToDeviceUnits (menu.ItemFontSize)).Width;
                    var extra = menu.LogicalToDeviceUnits (2 * (int) Math.Ceiling (font.Height / 6.0) + 8 + 4);
                    Assert.Equal (menu.DeviceToLogicalUnits ((int) Math.Ceiling (text) + extra), file.Bounds.Width);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_font_set_on_the_strip_gives_it_upstream_metrics_in_that_font ()
        {
            var chosen = new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 12f);
            var (form, menu, file) = Shell (m => m.Font = chosen);

            using (form) {
                Assert.Same (chosen, menu.UpstreamFont);
                Assert.Equal (new Padding (4, 0, 4, 0), file.Padding);
                Assert.Equal (chosen.Height + 4 + 4, menu.Height);
            }
        }

        [Fact]
        public void A_merged_MDI_icon_makes_the_menu_bar_as_tall_as_upstream_s ()
        {
            // The 20px icon in its 24px cell, plus the strip's padding: WinForms' 28px menu bar.
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                var (form, menu, _) = Shell ();

                using (form) {
                    form.IsMdiContainer = true;
                    var child = new Form { Text = "Doc", MdiParent = form, ClientSize = new Size (200, 100) };
                    child.WindowState = FormWindowState.Maximized;
                    child.Show ();
                    HeadlessRenderer.CapturePng (form, 600, 300);

                    Assert.Equal (28, menu.Height);
                    Assert.All (menu.Items.OfType<MdiControlItem> (), i => Assert.Equal (24, i.Bounds.Height));
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
