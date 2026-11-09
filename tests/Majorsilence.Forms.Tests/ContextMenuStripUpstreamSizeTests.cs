using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported app's ContextMenuStrip opens at upstream's size. Measured with a WinForms probe (Microsoft Sans Serif 12pt as
    // the app font): three "FileN" items open 99 x 70, an empty menu 100 x 25. MF's was 112 x 76 and 400 x 400: its items
    // were measured in the app's 12pt font where upstream's drop-down keeps SystemFonts.MenuFont (Segoe UI 9pt), and each
    // reserved a tab as wide as GDI's extent for one (8px in Segoe UI) where TextRenderer measures a tab as no width,
    // only its padding. An app that chose no font keeps MF's metrics.
    [Collection ("Headless")]
    public class ContextMenuStripUpstreamSizeTests
    {
        private static void Ported () => Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

        private static Size Open (ContextMenuStrip menu)
        {
            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new Size (400, 300) };
            form.Show ();

            menu.Show (form, new Point (10, 10));
            var size = menu.Size;
            menu.Close ();

            return size;
        }

        private static ContextMenuStrip Menu (params string[] captions)
        {
            var menu = new ContextMenuStrip ();

            foreach (var caption in captions)
                menu.Items.Add (new ToolStripMenuItem (caption));

            return menu;
        }

        [Fact]
        public void A_context_menu_opens_at_upstream_s_size ()
        {
            Size size;
            int scale;

            try {
                Ported ();
                var menu = Menu ("File0", "File1", "File2");
                size = Open (menu);
                scale = menu.LogicalToDeviceUnits (1);
            } finally {
                Application.SetDefaultFont (null!);
            }

            // Three items 22px tall (Segoe UI 9pt's line in its padding), 2px above and below.
            Assert.Equal (70, size.Height);

            if (OperatingSystem.IsWindows () && scale == 1)
                Assert.Equal (new Size (99, 70), size);
        }

        // The menu's items are measured in the menu font, not the app's: as wide as they are in an app that chose the menu
        // font itself.
        [Fact]
        public void A_context_menu_measures_its_items_in_the_menu_font ()
        {
            Size ported, own_font;

            try {
                Ported ();
                ported = Open (Menu ("A much longer caption here"));

                var menu = Menu ("A much longer caption here");
                menu.Font = SystemFonts.MenuFont;
                own_font = Open (menu);
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (own_font, ported);
            Assert.Equal (26, ported.Height);
        }

        // ShowShortcutKeys reserves a tab after the caption: TextRenderer's padding and nothing more.
        [Fact]
        public void The_tab_after_a_caption_is_only_GDI_s_padding ()
        {
            Size with_tab, without_tab;
            int padding;

            try {
                Ported ();
                with_tab = Open (Menu ("Copy"));

                var menu = Menu ("Copy");
                ((ToolStripMenuItem) menu.Items[0]).ShowShortcutKeys = false;
                without_tab = Open (menu);

                padding = TextRenderer.HorizontalPadding (SystemFonts.MenuFont);
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.InRange (with_tab.Width - without_tab.Width, padding - 1, padding + 1);
        }

        [Fact]
        public void An_empty_context_menu_opens_at_upstream_s_default_size ()
        {
            Size size;

            try {
                Ported ();
                size = Open (new ContextMenuStrip ());
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Size (100, 25), size);
        }

        [Fact]
        public void Without_a_chosen_font_a_context_menu_keeps_its_metrics ()
        {
            Assert.Equal (Size.Empty, new ContextMenuStrip ().Size);
            Assert.Equal (new Size (400, 400), Open (new ContextMenuStrip ()));
        }
    }
}
