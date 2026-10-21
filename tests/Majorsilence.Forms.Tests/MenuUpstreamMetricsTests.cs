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
                    // Measured and rounded in device pixels, as the renderer does, then taken back to logical, rounded up
                    // so the item still holds its text (an odd device width under scale 2 would otherwise lose a pixel).
                    var text = TextMeasurer.MeasureText ("File", menu.ItemTypeface, menu.LogicalToDeviceUnits (menu.ItemFontSize)).Width;
                    var extra = menu.LogicalToDeviceUnits (2 * (int) Math.Ceiling (font.Height / 6.0) + 8 + 4);
                    Assert.Equal (menu.DeviceToLogicalUnitsCovering (new Size ((int) Math.Ceiling (text) + extra, 0)).Width, file.Bounds.Width);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // Its drop-downs take upstream's metrics too (ToolStripDropDownMenu): a (0, 1, 0, 1) item padding, items no
        // shorter than a 16px check in its padding (22px for a 9pt font), and the caption between the 25px image
        // margin plus 8px and 9px of padding and the arrow's 18px. They were 14/3-padded with 70px to spare, so each
        // drop-down was taller and wider than upstream's. The caption is measured as TextRenderer measures it, with
        // GDI's padding, and followed by a tab while ShowShortcutKeys is on: WinForms' "Open" in Segoe UI 9pt is
        // 36 + 7 + 60 = 103px wide.
        [Fact]
        public void An_app_wide_font_gives_its_drop_downs_upstream_metrics ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                var (form, menu, file) = Shell ();

                using (form) {
                    var open = new ToolStripMenuItem ("Open");
                    file.DropDownItems.Add (open);
                    file.DropDownItems.Add (new ToolStripMenuItem ("Save"));

                    Assert.Equal (new Padding (0, 1, 0, 1), open.Padding);

                    file.ShowDropDown ();
                    var size = open.GetPreferredSize (Size.Empty);
                    var font = menu.UpstreamFont!;
                    var gdi_padding = GdiPadding (font);
                    var text = TextMeasurer.MeasureText ("Open", menu.ItemTypeface, menu.LogicalToDeviceUnits (menu.ItemFontSize)).Width;
                    var device = (int) Math.Round (text, 0, MidpointRounding.AwayFromZero)
                        + menu.LogicalToDeviceUnits (gdi_padding + TextLineHeight.TabAdvanceOf (font) + gdi_padding + 25 + 8 + 9 + 10 + 8);

                    Assert.Equal (22, size.Height);
                    Assert.Equal (menu.DeviceToLogicalUnitsCovering (new Size (device, 0)).Width, size.Width);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // TextRenderer's padding: a sixth of the line height on each side.
        private static int GdiPadding (Majorsilence.Forms.Drawing.Font font) => 2 * (int) Math.Ceiling (font.Height / 6.0);

        // A designer's MenuStrip.Font = Microsoft Sans Serif 12pt. Upstream sizes its drop-down items from GDI's line
        // height (20px, where Skia's outline metrics give 18): 20 + 2 of text padding + 2 of item padding = 24, against
        // 22 here, and its caption 189px wide with GDI's padding, plus a 9px tab. The drop-down was 18px narrower and
        // 6px shorter than WinForms' for three items.
        [Fact]
        public void A_drop_down_is_sized_from_GDI_s_line_height_and_text_padding ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                var chosen = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);
                var (form, menu, file) = Shell (m => m.Font = chosen);

                using (form) {
                    var item = new ToolStripMenuItem ("Attribute Link To Vendors");
                    var quiet = new ToolStripMenuItem ("Attribute Link To Vendors") { ShowShortcutKeys = false };
                    var saved = new ToolStripMenuItem ("Attribute Link To Vendors") { ShortcutKeys = Keys.Control | Keys.S };
                    file.DropDownItems.Add (item);
                    file.DropDownItems.Add (quiet);
                    file.DropDownItems.Add (saved);
                    file.ShowDropDown ();

                    var line = TextLineHeight.Of (chosen);
                    if (OperatingSystem.IsWindows ())
                        Assert.Equal (20, line);   // GDI's tmHeight; WinForms' item is 24px tall

                    var size = item.GetPreferredSize (Size.Empty);
                    Assert.Equal (Math.Max (line + 2, 20) + 2, size.Height);

                    var gdi_padding = GdiPadding (chosen);
                    var font_size = menu.LogicalToDeviceUnits (menu.ItemFontSize);
                    int Measure (string text) => (int) Math.Round (TextMeasurer.MeasureText (text, menu.ItemTypeface, font_size).Width, 0, MidpointRounding.AwayFromZero);
                    int Logical (int device) => menu.DeviceToLogicalUnitsCovering (new Size (device, 0)).Width;
                    var caption = Measure ("Attribute Link To Vendors") + menu.LogicalToDeviceUnits (gdi_padding);
                    var chrome = menu.LogicalToDeviceUnits (25 + 8 + 9 + 10 + 8);
                    var tab = menu.LogicalToDeviceUnits (TextLineHeight.TabAdvanceOf (chosen) + gdi_padding);

                    Assert.Equal (Logical (caption + tab + chrome), size.Width);
                    Assert.Equal (Logical (caption + chrome), quiet.GetPreferredSize (Size.Empty).Width);
                    Assert.Equal (Logical (caption + tab + Measure (saved.ShortcutDisplayText) + menu.LogicalToDeviceUnits (gdi_padding) + chrome),
                        saved.GetPreferredSize (Size.Empty).Width);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // TextRenderer draws the caption inset by GDI's padding too: WinForms' starts 24 + 8 + 4 = 36px into the item
        // in Microsoft Sans Serif 12pt, where it was drawn 3px further left.
        [Fact]
        public void A_drop_down_caption_starts_after_GDI_s_padding ()
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
                var chosen = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);
                var (form, menu, file) = Shell (m => m.Font = chosen);

                using (form) {
                    var item = new ToolStripMenuItem ("HHHH");
                    file.DropDownItems.Add (item);
                    file.ShowDropDown ();

                    var drop = file.OpenDropDown!;
                    using var bitmap = PaintSurface.Render (drop);
                    var bounds = item.DeviceBounds;
                    var first = Enumerable.Range (bounds.Left, bounds.Width)
                        .First (x => Enumerable.Range (bounds.Top, bounds.Height).Any (y => bitmap.GetPixel (x, y).Red < 128));

                    var inset = menu.LogicalToDeviceUnits (24 + 8 + GdiPadding (chosen) / 2);
                    Assert.InRange (first - bounds.Left, inset, inset + menu.LogicalToDeviceUnits (3));
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Without_a_chosen_font_drop_downs_keep_the_theme_metrics ()
        {
            var (form, _, file) = Shell ();

            using (form) {
                var open = new ToolStripMenuItem ("Open");
                file.DropDownItems.Add (open);

                Assert.Equal (new Padding (14, 3, 14, 3), open.Padding);

                file.ShowDropDown ();
                Assert.True (open.GetPreferredSize (Size.Empty).Width > 70, "the theme's 70px allowance is kept");
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
