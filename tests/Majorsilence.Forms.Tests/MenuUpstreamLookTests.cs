using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the menus WinForms' default
    // professional renderer draws under Windows 11, measured from it: a bar shaded from #F0F0F0 to #FCFCFC; a hovered item
    // in #B3D7F3 inside #0078D7; the open item a #FCFCFC-#F8F8F8 box in a #808080 line that runs on round its drop-down;
    // a #FDFDFD drop-down with an image margin shaded #FCFCFC-#F8F8F8-#F0F0F0, #BDBDBD separators and GrayText for a
    // disabled item. The theme's flat greys stood for all of them.
    [Collection ("Headless")]
    public class MenuUpstreamLookTests
    {
        private static readonly SKColor Border = new SKColor (0x80, 0x80, 0x80);
        private static readonly SKColor Background = new SKColor (0xFD, 0xFD, 0xFD);
        private static readonly SKColor SelectedFill = new SKColor (0xB3, 0xD7, 0xF3);
        private static readonly SKColor SelectedBorder = new SKColor (0x00, 0x78, 0xD7);

        private sealed class Menus : IDisposable
        {
            internal readonly Form Form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (500, 300) };
            internal readonly MenuStrip Strip = new MenuStrip ();
            internal readonly ToolStripMenuItem File = new ToolStripMenuItem ("&File");
            internal readonly ToolStripMenuItem Edit = new ToolStripMenuItem ("&Edit");
            internal readonly ToolStripSeparator Separator = new ToolStripSeparator ();
            internal readonly ToolStripMenuItem Disabled = new ToolStripMenuItem ("WWWW") {
                Enabled = false, Font = new Majorsilence.Forms.Drawing.Font ("Segoe UI", 16f, Majorsilence.Forms.Drawing.FontStyle.Bold),
            };

            internal Menus (bool chooseFont)
            {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));

                HeadlessRenderer.Use ();
                File.DropDownItems.Add (new ToolStripMenuItem ("Open"));
                File.DropDownItems.Add (new ToolStripMenuItem ("Save"));
                File.DropDownItems.Add (Separator);
                File.DropDownItems.Add (Disabled);
                Edit.DropDownItems.Add (new ToolStripMenuItem ("Copy"));
                Strip.Items.Add (File);
                Strip.Items.Add (Edit);
                Form.Controls.Add (Strip);
                Form.MainMenuStrip = Strip;
                Form.Show ();
                HeadlessRenderer.CapturePng (Form, 500, 300);   // a strip lays its items out when it paints
            }

            internal SKBitmap Capture () => SKBitmap.Decode (HeadlessRenderer.CapturePng (Form, 500, 300));

            // File open, its second item selected from the keyboard.
            internal MenuDropDown OpenFile ()
            {
                HeadlessRenderer.KeyDown (Form, Keys.Alt | Keys.F);
                if (File.OpenDropDown?.SelectedItem is null)
                    HeadlessRenderer.KeyDown (Form, Keys.Down);
                HeadlessRenderer.KeyDown (Form, Keys.Down);
                return File.OpenDropDown!;
            }

            public void Dispose ()
            {
                Form.Close ();
                Form.Dispose ();
                Application.SetDefaultFont (null!);
            }
        }

        private static void Near (SKColor expected, SKColor actual, int tolerance = 1)
            => Assert.True (Math.Abs (expected.Red - actual.Red) <= tolerance && Math.Abs (expected.Green - actual.Green) <= tolerance
                            && Math.Abs (expected.Blue - actual.Blue) <= tolerance, $"expected about {expected}, got {actual}");

        [Fact]
        public void The_bar_is_shaded_from_control_to_near_white ()
        {
            using var menus = new Menus (true);
            using var bitmap = menus.Capture ();
            var y = menus.Strip.Height * bitmap.Height / 300 / 2;
            var right = menus.Strip.Width * bitmap.Width / 500 - 1;

            Near (new SKColor (0xF0, 0xF0, 0xF0), bitmap.GetPixel (0, y));
            Near (new SKColor (0xFC, 0xFC, 0xFC), bitmap.GetPixel (right, y));
            Near (new SKColor (0xF6, 0xF6, 0xF6), bitmap.GetPixel (right / 2, y));
        }

        [Fact]
        public void A_hovered_bar_item_is_the_selection_inside_the_accent ()
        {
            using var menus = new Menus (true);
            var item = menus.Edit.Bounds;
            HeadlessRenderer.MouseMove (menus.Form, item.Left + item.Width / 2, item.Top + item.Height / 2);

            using var bitmap = menus.Capture ();
            var scale = bitmap.Width / 500f;

            Assert.Equal (SelectedBorder, bitmap.GetPixel ((int) (item.Left * scale), (int) ((item.Top + 4) * scale)));
            Assert.Equal (SelectedFill, bitmap.GetPixel ((int) ((item.Left + 2) * scale), (int) ((item.Top + 2) * scale)));
        }

        [Fact]
        public void The_open_bar_item_is_a_near_white_box_in_the_menu_border ()
        {
            using var menus = new Menus (true);
            menus.OpenFile ();
            var item = menus.File.Bounds;

            using var bitmap = menus.Capture ();
            var scale = bitmap.Width / 500f;

            Assert.Equal (Border, bitmap.GetPixel ((int) ((item.Left + item.Width / 2) * scale), (int) (item.Top * scale)));
            Assert.Equal (Border, bitmap.GetPixel ((int) (item.Left * scale), (int) ((item.Top + 4) * scale)));
            Near (new SKColor (0xFC, 0xFC, 0xFC), bitmap.GetPixel ((int) ((item.Left + 3) * scale), (int) ((item.Top + 1) * scale) + 1));
        }

        [Fact]
        public void The_drop_down_is_upstream_s_near_white_box_with_its_margin_selection_and_separator ()
        {
            using var menus = new Menus (true);
            var drop = menus.OpenFile ();
            var selected = drop.SelectedItem!;

            // ToolStripDropDownMenu's layout: items from x 0 and 2px down, the drop-down a pixel wider and 4px taller.
            Assert.Equal (new Point (0, 2), menus.File.DropDownItems[0].Bounds.Location);
            Assert.Equal (menus.File.DropDownItems[0].Bounds.Width + 1, drop.Width);
            Assert.Equal (6, menus.Separator.Bounds.Height);

            using var bitmap = PaintSurface.Render (drop);
            var s = drop.LogicalToDeviceUnits (1);
            var bounds = selected.DeviceBounds;
            var mid = bounds.Top + bounds.Height / 2;

            // The frame, and the gap along its top under the open item.
            Assert.Equal (Border, bitmap.GetPixel (0, mid));
            Assert.Equal (Border, bitmap.GetPixel (bitmap.Width - 1, mid));
            Assert.Equal (Border, bitmap.GetPixel (bitmap.Width / 2, bitmap.Height - 1));
            Assert.Equal (Background, bitmap.GetPixel (2 * s, 0));

            // The image margin, shaded left to right, beside the near-white drop-down.
            var open_mid = menus.File.DropDownItems[0].DeviceBounds.Top + 10 * s;
            Near (new SKColor (0xFC, 0xFC, 0xFC), bitmap.GetPixel (s, open_mid));
            Near (new SKColor (0xF1, 0xF1, 0xF1), bitmap.GetPixel (24 * s, open_mid));
            Assert.Equal (Background, bitmap.GetPixel (bitmap.Width - 3 * s, open_mid));

            // The selected item: the selection blue inside the accent, 2px in from the left and 3px from the right.
            Assert.Equal (SelectedBorder, bitmap.GetPixel (2 * s, mid));
            Assert.Equal (SelectedFill, bitmap.GetPixel (3 * s, mid));
            Assert.Equal (SelectedBorder, bitmap.GetPixel (bitmap.Width - 3 * s, mid));
            Assert.Equal (Background, bitmap.GetPixel (bitmap.Width - 2 * s, mid));

            // The separator: one grey line from where the captions start.
            var line = menus.Separator.DeviceBounds.Top + 3 * s;
            Assert.Equal (new SKColor (0xBD, 0xBD, 0xBD), bitmap.GetPixel (40 * s, line));
            Assert.NotEqual (new SKColor (0xBD, 0xBD, 0xBD), bitmap.GetPixel (20 * s, line));
        }

        [Fact]
        public void A_disabled_drop_down_item_is_gray_text ()
        {
            using var menus = new Menus (true);
            var drop = menus.OpenFile ();

            using var bitmap = PaintSurface.Render (drop);
            var bounds = menus.Disabled.DeviceBounds;
            var darkest = SKColors.White;

            for (var x = bounds.Left + bounds.Width / 4; x < bounds.Right - 4; x++)
                for (var y = bounds.Top + 2; y < bounds.Bottom - 2; y++) {
                    var p = bitmap.GetPixel (x, y);
                    if (p.Red + p.Green + p.Blue < darkest.Red + darkest.Green + darkest.Blue)
                        darkest = p;
                }

            Assert.Equal (SystemColors.GrayText.ToSKColor (), darkest);
        }

        [Fact]
        public void Without_a_chosen_font_the_theme_bar_is_kept_flat ()
        {
            using var menus = new Menus (false);
            using var bitmap = menus.Capture ();
            var y = menus.Strip.Height * bitmap.Height / 300 / 2;

            Assert.Equal (bitmap.GetPixel (0, y), bitmap.GetPixel (menus.Strip.Width * bitmap.Width / 500 - 1, y));
        }
    }
}
