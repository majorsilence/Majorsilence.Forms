using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's ToolStrip, MenuStrip, StatusStrip and ContextMenuStrip are AutoSize by default. Measured with a WinForms
    // probe (Microsoft Sans Serif 12pt as the app font): a docked strip of text items is as thick as its items need --
    // a ToolStrip 25px, a MenuStrip 24, a StatusStrip 22 -- whatever Size the designer wrote; an undocked one is as big as
    // its items both ways; AutoSize = false keeps the given Size. MF's strips were not AutoSize, so a designer Size stood,
    // a MenuStrip forced its own height even with AutoSize off, and a ContextMenuStrip opened at its items' size whatever
    // AutoSize said. A ported app (Application.SetDefaultFont) gets upstream's sizing; one that chose no font keeps MF's.
    [Collection ("Headless")]
    public class ToolStripAutoSizeTests
    {
        private static void Ported () => Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

        private static ToolStrip Make (string kind) => kind switch {
            "ToolStrip" => new ToolStrip (),
            "MenuStrip" => new MenuStrip (),
            _ => new StatusStrip ()
        };

        private static ToolStripItem Item (string kind, int index) => kind switch {
            "ToolStrip" => new ToolStripButton ("Button" + index),
            "MenuStrip" => new ToolStripMenuItem ("File" + index),
            _ => new ToolStripStatusLabel { Text = "Ready" + index }
        };

        // A strip added the way InitializeComponent adds one: items, then the designer's properties, inside a suspended
        // layout, onto a 300 x 120 panel.
        private static (Form form, ToolStrip strip) Designer (string kind, Action<ToolStrip> properties, int items = 2)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 200) };
            var panel = new Panel { Size = new Size (300, 120) };
            var strip = Make (kind);

            form.Controls.Add (panel);
            panel.SuspendLayout ();
            strip.SuspendLayout ();

            for (var i = 0; i < items; i++)
                strip.Items.Add (Item (kind, i));

            properties (strip);
            panel.Controls.Add (strip);
            strip.ResumeLayout (false);
            strip.PerformLayout ();
            panel.ResumeLayout (false);
            panel.PerformLayout ();

            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 200);

            return (form, strip);
        }

        [Fact]
        public void A_ported_app_s_strips_are_auto_sized_with_upstream_s_default_sizes ()
        {
            try {
                Ported ();

                Assert.True (new ToolStrip ().AutoSize);
                Assert.True (new MenuStrip ().AutoSize);
                Assert.True (new StatusStrip ().AutoSize);
                Assert.True (new ContextMenuStrip ().AutoSize);

                Assert.Equal (new Size (100, 25), new ToolStrip ().Size);
                Assert.Equal (new Size (200, 24), new MenuStrip ().Size);
                Assert.Equal (new Size (200, 22), new StatusStrip ().Size);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // MF's own menu bar and the legacy MainMenu are not ToolStrips upstream, and keep their sizing.
        [Fact]
        public void A_ported_app_s_main_menu_is_not_auto_sized ()
        {
            try {
                Ported ();

                Assert.False (new MainMenu ().AutoSize);
                Assert.False (new Menu ().AutoSize);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Theory]
        [InlineData ("ToolStrip", 40, 25)]
        [InlineData ("ToolStrip", 15, 25)]
        [InlineData ("MenuStrip", 40, 24)]
        [InlineData ("MenuStrip", 15, 24)]
        [InlineData ("StatusStrip", 40, 22)]
        [InlineData ("StatusStrip", 15, 22)]
        public void A_docked_strip_is_as_thick_as_its_items_whatever_size_the_designer_wrote (string kind, int designerHeight, int expected)
        {
            Rectangle bounds;

            try {
                Ported ();
                var (form, strip) = Designer (kind, s => s.Size = new Size (280, designerHeight));

                using (form)
                    bounds = strip.Bounds;
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (300, bounds.Width);
            Assert.Equal (expected, bounds.Height);

            // A status strip stays on the bottom edge.
            Assert.Equal (kind == "StatusStrip" ? 120 - expected : 0, bounds.Y);
        }

        // A status bar written by a designer with an 8.25pt font and a 25px Size, as a real app's are: upstream's is 22px.
        [Fact]
        public void A_designer_status_strip_in_a_small_font_settles_at_upstream_s_height ()
        {
            Rectangle bounds;

            try {
                Ported ();
                var (form, strip) = Designer ("StatusStrip", s => {
                    s.Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f);
                    s.Location = new Point (0, 95);
                    s.Size = new Size (1004, 25);
                });

                using (form)
                    bounds = strip.Bounds;
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Rectangle (0, 98, 300, 22), bounds);
        }

        [Theory]
        [InlineData ("ToolStrip", 40)]
        [InlineData ("ToolStrip", 15)]
        [InlineData ("MenuStrip", 40)]
        [InlineData ("MenuStrip", 15)]
        [InlineData ("StatusStrip", 40)]
        [InlineData ("StatusStrip", 30)]
        public void A_strip_with_AutoSize_off_keeps_the_size_it_was_given (string kind, int height)
        {
            Rectangle bounds;

            try {
                Ported ();
                var (form, strip) = Designer (kind, s => {
                    s.AutoSize = false;
                    s.Size = new Size (280, height);
                });

                using (form)
                    bounds = strip.Bounds;
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Size (300, height), bounds.Size);
        }

        [Theory]
        [InlineData ("ToolStrip", 25)]
        [InlineData ("MenuStrip", 24)]
        public void An_undocked_strip_is_as_big_as_its_items (string kind, int height)
        {
            Rectangle bounds;
            Size preferred;
            int scale;

            try {
                Ported ();
                var (form, strip) = Designer (kind, s => {
                    s.Dock = DockStyle.None;
                    s.Location = new Point (14, 30);
                    s.Size = new Size (250, 40);
                });

                using (form) {
                    scale = strip.LogicalToDeviceUnits (1);
                    bounds = strip.Bounds;
                    preferred = strip.GetPreferredSize (Size.Empty);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Point (14, 30), bounds.Location);
            Assert.Equal (height, bounds.Height);
            Assert.Equal (preferred, bounds.Size);
            Assert.True (bounds.Width < 250, $"width {bounds.Width}");

            // Two "FileN" items in Segoe UI 9pt, the menu font, are 43px each: upstream's bar is 6 + 86 + 2 wide. At scale
            // 2 each item's text is measured in device pixels and rounded up to whole logical ones, which can add one.
            if (kind == "MenuStrip" && OperatingSystem.IsWindows () && scale == 1)
                Assert.Equal (94, bounds.Width);
        }

        // Upstream's empty strip is DefaultSize less its padding, 2px for the end and the grip: 111 x 25.
        [Fact]
        public void An_undocked_empty_tool_strip_is_upstream_s_size ()
        {
            Size size;

            try {
                Ported ();
                var (form, strip) = Designer ("ToolStrip", s => {
                    s.Dock = DockStyle.None;
                    s.Size = new Size (250, 40);
                }, items: 0);

                using (form)
                    size = strip.Size;
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Size (111, 25), size);
        }

        [Fact]
        public void An_undocked_strip_grows_when_an_item_is_added ()
        {
            int before, after;

            try {
                Ported ();
                var (form, strip) = Designer ("ToolStrip", s => s.Dock = DockStyle.None);

                using (form) {
                    before = strip.Width;
                    strip.Items.Add (new ToolStripButton ("Another long button"));
                    after = strip.Width;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.True (after > before + 50, $"{before} -> {after}");
        }

        // A strip once taller than its items shrinks back to them when AutoSize comes on.
        [Fact]
        public void Turning_AutoSize_on_shrinks_a_strip_to_its_items ()
        {
            Size size;

            try {
                Ported ();
                var (form, strip) = Designer ("ToolStrip", s => {
                    s.Dock = DockStyle.None;
                    s.AutoSize = false;
                    s.Size = new Size (200, 50);
                });

                using (form) {
                    HeadlessRenderer.CapturePng (form, 400, 200);   // the items are laid out 50px tall
                    strip.AutoSize = true;
                    size = strip.Size;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (25, size.Height);
            Assert.True (size.Width < 200, $"width {size.Width}");
        }

        [Fact]
        public void A_docked_strip_keeps_its_height_when_set_at_run_time ()
        {
            int height;

            try {
                Ported ();
                var (form, strip) = Designer ("ToolStrip", s => { });

                using (form) {
                    strip.Height = 60;
                    height = strip.Height;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (25, height);
        }

        [Fact]
        public void A_menu_strip_grows_to_an_item_in_a_larger_font ()
        {
            int height;

            try {
                Ported ();
                var (form, strip) = Designer ("MenuStrip", s => { });

                using (form) {
                    strip.Items.Add (new ToolStripMenuItem ("Tall") { Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 20f) });
                    height = strip.Height;
                }
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.True (height > 30, $"height {height}");

            if (OperatingSystem.IsWindows ())
                Assert.Equal (39, height);
        }

        [Fact]
        public void A_context_menu_strip_with_AutoSize_off_opens_at_its_size ()
        {
            Size fixed_size, auto_size;

            try {
                Ported ();
                HeadlessRenderer.Use ();

                using var form = new Form { ClientSize = new Size (400, 300) };
                form.Show ();

                var fixed_menu = new ContextMenuStrip { AutoSize = false };
                var auto_menu = new ContextMenuStrip ();

                foreach (var menu in new[] { fixed_menu, auto_menu }) {
                    menu.Items.Add (new ToolStripMenuItem ("One"));
                    menu.Items.Add (new ToolStripMenuItem ("Two"));
                    menu.Items.Add (new ToolStripMenuItem ("Three"));
                    menu.Size = new Size (200, 120);
                }

                fixed_menu.Show (form, new Point (10, 10));
                fixed_size = fixed_menu.Size;
                fixed_menu.Close ();

                auto_menu.Show (form, new Point (10, 10));
                auto_size = auto_menu.Size;
                auto_menu.Close ();
            } finally {
                Application.SetDefaultFont (null!);
            }

            Assert.Equal (new Size (200, 120), fixed_size);
            Assert.NotEqual (new Size (200, 120), auto_size);
        }

        // An app that never chose a font keeps MF's sizing: no AutoSize, and the Size it was given.
        [Theory]
        [InlineData ("ToolStrip")]
        [InlineData ("MenuStrip")]
        [InlineData ("StatusStrip")]
        public void Without_a_chosen_font_a_strip_keeps_the_size_it_was_given (string kind)
        {
            var (form, strip) = Designer (kind, s => s.Size = new Size (280, 40));

            using (form) {
                Assert.False (strip.AutoSize);
                Assert.Equal (40, strip.Height);
            }

            Assert.False (new ContextMenuStrip ().AutoSize);
            Assert.Equal (new Size (600, 34), new ToolStrip ().Size);
        }
    }
}
