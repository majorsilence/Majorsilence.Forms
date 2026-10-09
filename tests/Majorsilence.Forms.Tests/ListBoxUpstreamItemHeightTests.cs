using System;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets upstream's
    // default ListBox.ItemHeight: the font's GDI line height. The theme's measured text plus 3 made
    // ReportDesigner's toolbar-layout list 21px a row where WinForms' is 15, showing a third fewer rows.
    [Collection ("Headless")]
    public class ListBoxUpstreamItemHeightTests
    {
        private static int ItemHeight (bool chooseFont, string family, float points, int? set = null)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));

                HeadlessRenderer.Use ();
                var list = new ListBox { Font = new Majorsilence.Forms.Drawing.Font (family, points) };
                if (set is { } value)
                    list.ItemHeight = value;

                return list.ItemHeight;
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Theory]
        [InlineData ("Segoe UI", 9f, 15)]
        [InlineData ("Microsoft Sans Serif", 8.25f, 13)]
        [InlineData ("Segoe UI", 11f, 20)]
        public void The_default_item_height_is_upstream_s_on_Windows (string family, float points, int expected)
        {
            // Values measured from WinForms (ListBox.ItemHeight); they come from GDI, so only Windows has them.
            if (!OperatingSystem.IsWindows ())
                return;

            Assert.Equal (expected, ItemHeight (true, family, points));
        }

        [Fact]
        public void The_system_fonts_are_the_shell_s_on_Windows ()
        {
            // Upstream's SystemFonts.MessageBoxFont is Segoe UI 9pt. The theme's Segoe UI Emoji is a pixel
            // taller per line, so an app adopting the system font got 16px list rows instead of 15.
            if (!OperatingSystem.IsWindows ())
                return;

            Assert.Equal ("Segoe UI", SystemFonts.MessageBoxFont.Name);
            Assert.Equal (9f, SystemFonts.MessageBoxFont.SizeInPoints);
            Assert.Equal ("Segoe UI", SystemFonts.MenuFont.Name);
        }

        [Fact]
        public void A_set_item_height_is_kept ()
            => Assert.Equal (24, ItemHeight (true, "Segoe UI", 9f, set: 24));

        [Fact]
        public void Without_a_chosen_font_the_theme_item_height_is_kept ()
            => Assert.NotEqual (15, ItemHeight (false, "Segoe UI", 9f));
    }
}
