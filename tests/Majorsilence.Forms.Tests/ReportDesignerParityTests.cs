using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Resources.Extensions;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Found running the Majorsilence.Forms ReportDesigner side by side with its System.Windows.Forms
    // original: each of these made the migrated designer look or behave differently from the real one.
    [Collection ("Headless")]
    public class ReportDesignerParityTests : IDisposable
    {
        public ReportDesignerParityTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static SKBitmap Icon (int size)
        {
            var bitmap = new SKBitmap (size, size);
            bitmap.Erase (SKColors.Black);
            return bitmap;
        }

        // ── Toolbar item metrics ─────────────────────────────────────────────────

        [Fact]
        public void An_icon_button_on_a_ToolStrip_is_its_image_plus_a_2px_border_as_in_WinForms ()
        {
            // The 14px menu padding made it 48px, and a designer toolbar ran out of room: ReportDesigner's
            // expression editor and zoom control were pushed off the end and never shown.
            HeadlessRenderer.Use ();

            var strip = new ToolStrip { ImageScalingSize = new Size (20, 20) };
            var button = new ToolStripButton ("New", Icon (20)) { DisplayStyle = ToolStripItemDisplayStyle.Image };
            strip.Items.Add (button);

            Assert.Equal (24, button.GetPreferredSize (Size.Empty).Width);
        }

        [Fact]
        public void A_menu_item_keeps_the_menu_padding ()
        {
            var menu = new MenuStrip ();
            var item = new ToolStripMenuItem ("File");
            menu.Items.Add (item);

            Assert.Equal (new Padding (14, 3, 14, 3), item.Padding);
        }

        [Fact]
        public void An_assigned_padding_wins_over_the_strip_default ()
        {
            var strip = new ToolStrip ();
            var button = new ToolStripButton ("B") { Padding = new Padding (7) };
            strip.Items.Add (button);

            Assert.Equal (new Padding (7), button.Padding);
        }

        [Fact]
        public void An_image_and_text_button_is_wide_enough_for_both_and_the_gap_between_them ()
        {
            HeadlessRenderer.Use ();

            var strip = new ToolStrip { ImageScalingSize = new Size (20, 20) };
            var both = new ToolStripButton ("Text Box", Icon (20));
            var text = new ToolStripButton ("Text Box") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            strip.Items.Add (both);
            strip.Items.Add (text);

            // Image (20) and the 4px gap RenderItem leaves between image and text.
            Assert.Equal (text.GetPreferredSize (Size.Empty).Width + 24, both.GetPreferredSize (Size.Empty).Width);
        }

        // ── Resources written against WinForms-only types ────────────────────────

        [Fact]
        public void A_type_converter_string_entry_is_recovered_as_its_text ()
        {
            // The compiled form of <data name="x.ShortcutKeys" type="System.Windows.Forms.Keys, ...">.
            // The reader cannot resolve Keys (the enum shim does not declare it), so every shortcut in a
            // migrated menu was dropped.
            var stream = new MemoryStream ();
            using (var writer = new PreserializedResourceWriter (stream)) {
                writer.AddResource ("open.ShortcutKeys", "Ctrl+O", "System.Windows.Forms.Keys, System.Windows.Forms");
                writer.Generate ();
            }

            var entry = RawResourcesReader.Read (stream.ToArray (), _ => true)["open.ShortcutKeys"];

            Assert.Equal (RawResourcesReader.RawFormat.TypeConverterString, entry.Format);
            Assert.Equal ("Ctrl+O", ComponentResourceManager.ReadConverterString (entry));
        }

        [Fact]
        public void ApplyResources_reads_a_WinForms_shortcut_and_padding ()
        {
            var mgr = ComponentResourceManager.FromXml ("""
                <?xml version="1.0" encoding="utf-8"?>
                <root>
                  <data name="open.ShortcutKeys" type="System.Windows.Forms.Keys, System.Windows.Forms"><value>Ctrl+Shift+O</value></data>
                  <data name="open.Padding" type="System.Windows.Forms.Padding, System.Windows.Forms"><value>1, 2, 3, 4</value></data>
                </root>
                """);
            var item = new ToolStripMenuItem ();

            mgr.ApplyResources (item, "open");

            Assert.Equal (Keys.Control | Keys.Shift | Keys.O, item.ShortcutKeys);
            Assert.Equal (new Padding (1, 2, 3, 4), item.Padding);
        }

        // ── State styles keep the type's look ────────────────────────────────────

        [Theory]
        [InlineData (true)]
        [InlineData (false)]
        public void A_disabled_or_hovered_ComboBox_keeps_its_border_and_fill (bool disabled)
        {
            // Control's own :disabled / :hover styles know nothing of the ComboBox look, so every
            // disabled toolbar combo in ReportDesigner drew as a bare arrow.
            var combo = new ComboBox { Enabled = !disabled };

            var style = disabled ? combo.CurrentStyle : combo.StyleHover;

            Assert.Equal (combo.Style.Border.GetWidth (), style.Border.GetWidth ());
            Assert.True (combo.Style.Border.GetWidth () > 0);
            Assert.Equal (combo.Style.GetBackgroundColor (), style.GetBackgroundColor ());
        }

        [Fact]
        public void A_Button_still_uses_its_own_disabled_style ()
        {
            var button = new Button { Enabled = false };

            Assert.Same (button.StyleDisabled, button.CurrentStyle);
        }

        // ── Disabled images ──────────────────────────────────────────────────────

        [Fact]
        public void A_disabled_black_glyph_is_drawn_light_grey_as_WinForms_does ()
        {
            // A plain luminance greyscale left black black, so a disabled toolbar of dark glyphs looked
            // enabled. WinForms lifts it by 0.38 and draws it at 70% alpha.
            using var glyph = Icon (4);
            using var surface = new SKBitmap (4, 4);
            surface.Erase (SKColors.White);

            using (var canvas = new SKCanvas (surface))
                canvas.DrawBitmap (glyph, new Rectangle (0, 0, 4, 4), disabled: true);

            var pixel = surface.GetPixel (1, 1);

            // 0.38 * 255 = 97 grey at 70% over white: 97 * 0.7 + 255 * 0.3 = 144.
            Assert.InRange (pixel.Red, 138, 150);
            Assert.Equal (pixel.Red, pixel.Green);
            Assert.Equal (pixel.Red, pixel.Blue);
        }

        // ── Fonts under a page transform ─────────────────────────────────────────

        [Fact]
        public void A_point_sized_font_keeps_its_physical_size_under_PageUnit_Point ()
        {
            // The canvas carries the page transform, and the font was converted from points to pixels
            // on top of it -- ReportDesigner's design surface (PageUnit = Point) drew all text 96/72 too big.
            using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 20f);

            float Width (Majorsilence.Forms.Drawing.GraphicsUnit unit)
            {
                using var bitmap = new SKBitmap (800, 200);
                using var canvas = new SKCanvas (bitmap);
                bitmap.Erase (SKColors.White);

                var g = new Graphics (canvas) { PageUnit = unit };
                g.DrawString ("Chart: Sales by Category", font, new Majorsilence.Forms.Drawing.SolidBrush (Color.Black), 0, 0);

                var right = 0;
                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++)
                        if (bitmap.GetPixel (x, y).Red < 128)
                            right = Math.Max (right, x);

                return right;
            }

            var pixels = Width (Majorsilence.Forms.Drawing.GraphicsUnit.Pixel);
            var points = Width (Majorsilence.Forms.Drawing.GraphicsUnit.Point);

            Assert.InRange (points / pixels, 0.95f, 1.05f);
        }

        // ── Tab headers ──────────────────────────────────────────────────────────

        [Fact]
        public void A_TabControl_sized_for_its_headers_alone_keeps_the_tabs_inside_it ()
        {
            // ReportDesigner's open-document strip is a 21px TabControl; the 31px row hung below it and
            // was clipped, taking the selected tab's underline with it.
            var form = new Form ();
            var tabs = new TabControl { Height = 21, Width = 400 };
            tabs.TabPages.Add (new TabPage ("SimpleTest1.rdl"));
            tabs.TabPages.Add (new TabPage ("ChartExampleBar.rdl"));
            form.Controls.Add (tabs);
            HeadlessRenderer.CapturePng (form, 500, 200);

            var strip = tabs.TabStrip;

            Assert.All (strip.Tabs, tab => Assert.True (tab.Bounds.Bottom <= tabs.Height, $"tab bottom {tab.Bounds.Bottom} > {tabs.Height}"));
        }

        // ── MDI children ─────────────────────────────────────────────────────────

        [Fact]
        public void An_MDI_child_maximized_before_it_is_shown_opens_maximized ()
        {
            // How a tabbed MDI shell opens every document; the state went to the child's unused
            // top-level backend, and the document opened at its restored size.
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);
            var client = parent.MdiClientControl!;

            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.WindowState = FormWindowState.Maximized;
            child.Show ();

            var frame = client.Controls.OfType<MdiChildWindow> ().Single ();
            Assert.Equal (FormWindowState.Maximized, frame.WindowState);
            Assert.Equal (FormWindowState.Maximized, child.WindowState);
            Assert.Equal (client.DisplayRectangle.Size, frame.Size);
        }

        [Fact]
        public void Setting_a_shown_MDI_child_s_WindowState_drives_its_frame ()
        {
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);

            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Show ();
            child.WindowState = FormWindowState.Maximized;

            var frame = parent.MdiClientControl!.Controls.OfType<MdiChildWindow> ().Single ();
            Assert.Equal (FormWindowState.Maximized, frame.WindowState);

            child.WindowState = FormWindowState.Normal;
            Assert.Equal (FormWindowState.Normal, frame.WindowState);
        }
    }
}
