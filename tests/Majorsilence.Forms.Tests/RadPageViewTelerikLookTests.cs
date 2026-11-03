using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadPageView's strip is laid out and painted as Telerik's is under its default theme, and greys out the same
    // way when the page view is disabled. Measured on UI for WinForms 2026.2.520 (.NET 10), a 380 x 260 page view with
    // "Personal Information" and "Notes", the first selected, in a form whose font is Microsoft Sans Serif 12pt:
    //   - the strip #BFDBFF; the tabs from (7, 5), 28 high in the default Segoe UI 8.25pt (32 in a 12pt page view
    //     Font), and as wide as GDI+ measures the caption plus 12: 82 for "Identification", 111 in 12pt;
    //   - the selected tab framed in #6E99D2, #EBF2FA under its top edge, #D8E6F9 a sixth of the way down; the
    //     captions #15428B, "P" starting at x 16, 15..22 down;
    //   - the content framed in #6E99D2 from y 32, #E9F0F9 inside, a #D4E5FC line outside its right and bottom
    //     edges; the page at (10, 37, 359, 212), or (10, 41, ...) in 12pt;
    //   - the buttons: pale #DCE8FF arrows at 322 and 339 and a #15428B close cross at 356, 11px down; none with
    //     StripButtons = None, the tabs then running on to the strip's end; pressing the cross closes the page;
    //   - disabled: the strip #CBCBCB with the selected tab no different from the rest, captions #7B7B7B, the frame
    //     #A7A7A7 with a #D2D2D2 line outside, #D9D9D9 inside.
    // Here the strip was the tab control's: flat grey tabs in the form's 12pt font, no buttons, and a border round
    // the whole control -- the same enabled or disabled.
    [Collection ("Headless")]
    public class RadPageViewTelerikLookTests
    {
        private sealed class Shot
        {
            public SKBitmap Bitmap = null!;
            public float Scale;

            // The pixel at a logical point, sampling the top-left device pixel of that logical pixel.
            public SKColor At (int x, int y) => Bitmap.GetPixel ((int) Math.Round (x * Scale), (int) Math.Round (y * Scale));

            // The left and vertical extent, logical, of the dark pixels in [x0, x1) x [y0, y1).
            public (int Left, int Top, int Bottom) Ink (int x0, int x1, int y0, int y1)
            {
                int? left = null, top = null, bottom = null;

                for (var x = (int) Math.Round (x0 * Scale); x < (int) Math.Round (x1 * Scale); x++)
                    for (var y = (int) Math.Round (y0 * Scale); y < (int) Math.Round (y1 * Scale); y++) {
                        var p = Bitmap.GetPixel (x, y);

                        if (p.Red + p.Green + p.Blue < 450) {
                            left ??= x;
                            top = Math.Min (top ?? y, y);
                            bottom = Math.Max (bottom ?? y, y);
                        }
                    }

                Assert.True (left.HasValue, "No caption was drawn.");
                return ((int) Math.Round (left!.Value / Scale), (int) Math.Round (top!.Value / Scale), (int) Math.Round (bottom!.Value / Scale));
            }
        }

        private static RadPageView View (bool enabled = true, Majorsilence.Forms.Drawing.Font? font = null, params string[] captions)
        {
            var view = new RadPageView { Location = new Point (0, 0), Size = new Size (380, 260), Enabled = enabled };

            if (font is not null)
                view.Font = font;

            foreach (var caption in captions.Length > 0 ? captions : new[] { "Personal Information", "Notes" })
                view.Pages.Add (new RadPageViewPage { Text = caption });

            return view;
        }

        private static T WithForm<T> (RadPageView view, Func<Form, T> body)
        {
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
                HeadlessRenderer.Use ();

                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (380, 260), BackColor = Color.FromArgb (233, 240, 249) };
                form.Controls.Add (view);
                form.Show ();
                view.SelectedPage = view.Pages[0] as RadPageViewPage;

                return body (form);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static Shot Capture (RadPageView view)
            => WithForm (view, form => new Shot {
                Bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 380, 260)),
                Scale = view.LogicalToDeviceUnits (100) / 100f,
            });

        private static SKColor C (uint rgb) => new SKColor (0xFF000000 | rgb);

        // An antialiased mark's core: within a few levels of the colour.
        private static void Near (SKColor expected, SKColor actual)
            => Assert.True (Math.Abs (expected.Red - actual.Red) <= 3 && Math.Abs (expected.Green - actual.Green) <= 3 && Math.Abs (expected.Blue - actual.Blue) <= 3,
                $"Expected about {expected}, got {actual}.");

        // The darkest pixel in [x0, x1) x [y0, y1).
        private static SKColor Darkest (Shot shot, int x0, int x1, int y0, int y1)
            => Enumerable.Range (x0, x1 - x0).SelectMany (x => Enumerable.Range (y0, y1 - y0).Select (y => shot.At (x, y)))
                .OrderBy (p => p.Red + p.Green + p.Blue).First ();

        [Fact]
        public void The_tabs_and_page_are_where_Telerik_puts_them ()
        {
            WithForm (View (), _ => {
                var view = (RadPageView) _.Controls[0];
                var tabs = view.TabStrip.Tabs;

                Assert.Equal (new Point (7, 5), tabs[0].Bounds.Location);
                Assert.Equal (28, tabs[0].Bounds.Height);
                Assert.Equal (tabs[0].Bounds.Right, tabs[1].Bounds.Left);
                Assert.Equal (new Rectangle (10, 37, 359, 212), view.Pages[0].Bounds);
                return 0;
            });
        }

        [Fact]
        public void A_tab_is_as_wide_as_its_caption_plus_12_and_as_high_as_its_font_plus_13 ()
        {
            var big = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);

            WithForm (View (font: big, captions: new[] { "Identification", "Comment" }), form => {
                var view = (RadPageView) form.Controls[0];
                var tab = view.TabStrip.Tabs[0];

                Assert.Equal (32, tab.Bounds.Height);
                Assert.Equal (41, view.Pages[0].Bounds.Top);

                // GDI+ measures text a little wider than Skia, which hints differently: within 2px of Telerik's 111.
                if (OperatingSystem.IsWindows ())
                    Assert.InRange (tab.Bounds.Width, 109, 113);

                return 0;
            });
        }

        // Measured: Fill without EqualWidth gives each tab its own width and an equal part of the room left over --
        // in a 562px view with no buttons, 233 and 316 in 12pt, filling the 549 up to the strip's end.
        [Fact]
        public void Fill_shares_the_spare_room_between_the_tabs ()
        {
            var view = View (font: new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f), captions: new[] { "Login", "Server/Database" });
            view.Size = new Size (562, 150);
            view.GetChildAt (0).StripButtons = StripViewButtons.None;
            view.GetChildAt (0).ItemFitMode = StripViewItemFitMode.Fill;

            WithForm (view, _ => {
                var tabs = view.TabStrip.Tabs;

                Assert.InRange (tabs[0].Bounds.Width + tabs[1].Bounds.Width, 548, 549);
                Assert.InRange (tabs[1].Bounds.Width - tabs[0].Bounds.Width, 81, 85);
                return 0;
            });
        }

        [Fact]
        public void The_strip_and_selected_tab_are_Telerik_s_blues ()
        {
            var shot = Capture (View ());

            Assert.Equal (C (0xBFDBFF), shot.At (250, 10));
            Assert.Equal (C (0x6E99D2), shot.At (60, 5));
            Assert.Equal (C (0xEBF2FA), shot.At (60, 6));
            Assert.Equal (C (0xD8E6F9), shot.At (60, 10));
            Assert.Equal (C (0x6E99D2), shot.At (7, 12));

            // The selected tab runs down over the content's top edge, which shows beside it.
            Assert.NotEqual (C (0x6E99D2), shot.At (60, 32));
            Assert.Equal (C (0x6E99D2), shot.At (250, 32));
        }

        [Fact]
        public void The_content_is_framed_with_a_pale_line_outside ()
        {
            var shot = Capture (View ());

            Assert.Equal (C (0x6E99D2), shot.At (5, 100));
            Assert.Equal (C (0x6E99D2), shot.At (373, 100));
            Assert.Equal (C (0xD4E5FC), shot.At (374, 100));
            Assert.Equal (C (0x6E99D2), shot.At (100, 253));
            Assert.Equal (C (0xD4E5FC), shot.At (100, 254));
            Assert.Equal (C (0xBFDBFF), shot.At (375, 100));
            Assert.Equal (C (0xE9F0F9), shot.At (7, 100));
        }

        [Fact]
        public void The_captions_are_navy_and_placed_as_Telerik_s ()
        {
            var shot = Capture (View ());
            var slack = shot.Scale > 1 || !OperatingSystem.IsWindows () ? 1 : 0;
            var ink = shot.Ink (8, 126, 6, 32);

            Assert.InRange (ink.Left, 16 - slack, 16 + slack);
            Assert.InRange (ink.Top, 15 - slack, 15 + slack);
            Assert.InRange (ink.Bottom, 22 - slack, 22 + slack);
        }

        [Fact]
        public void The_strip_shows_its_buttons_unless_told_not_to ()
        {
            var shot = Capture (View ());

            // Pale arrows (nothing overflows) and a navy close cross.
            Assert.Equal (C (0xDCE8FF), shot.At (330, 18));
            Assert.Equal (C (0xDCE8FF), shot.At (346, 18));
            Near (C (0x15428B), Darkest (shot, 356, 372, 11, 27));

            var none = View ();
            none.GetChildAt (0).StripButtons = StripViewButtons.None;
            var plain = Capture (none);

            Assert.Equal (C (0xBFDBFF), plain.At (330, 18));
            Assert.Equal (C (0xBFDBFF), plain.At (363, 18));
        }

        [Fact]
        public void Pressing_the_close_button_closes_the_selected_page ()
        {
            WithForm (View (), form => {
                var view = (RadPageView) form.Controls[0];
                var strip = view.TabStrip;
                var at = new Point (363, 18);

                strip.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, 0));
                strip.RaiseMouseUp (new MouseEventArgs (MouseButtons.Left, 1, at.X, at.Y, 0));

                Assert.Single (view.Pages);
                Assert.Equal ("Notes", view.Pages[0].Text);
                return 0;
            });
        }

        [Fact]
        public void Disabled_the_strip_greys_out ()
        {
            var shot = Capture (View (enabled: false));

            Assert.Equal (C (0xCBCBCB), shot.At (250, 10));
            Assert.Equal (C (0xCBCBCB), shot.At (60, 10));
            Assert.Equal (C (0xA7A7A7), shot.At (60, 32));
            Assert.Equal (C (0xA7A7A7), shot.At (5, 100));
            Assert.Equal (C (0xD2D2D2), shot.At (374, 100));
            Assert.Equal (C (0xD9D9D9), shot.At (7, 100));
            Near (C (0x7B7B7B), Darkest (shot, 356, 372, 11, 27));

            // The caption is grey, not navy.
            var caption = Darkest (shot, 8, 126, 6, 32);
            Assert.Equal (caption.Red, caption.Blue);
            Near (C (0x7B7B7B), caption);
        }
    }
}
