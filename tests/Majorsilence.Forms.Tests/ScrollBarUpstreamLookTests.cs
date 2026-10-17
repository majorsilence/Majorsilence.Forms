using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the scroll bar
    // Windows 11 draws for WinForms: a plain #F0F0F0 track, no arrow boxes, and a 2px #858585 thumb while
    // the pointer is elsewhere. Apps that have not chosen a font keep the theme's scroll bar.
    [Collection ("Headless")]
    public class ScrollBarUpstreamLookTests
    {
        private static readonly SKColor Thumb = new SKColor (0x85, 0x85, 0x85);

        // The columns of the bar that hold the thumb's colour, in logical pixels.
        private static int ThumbColumns (bool chooseFont)
        {
            try {
                if (chooseFont)
                    Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));

                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (100, 200) };
                var bar = new VScrollBar { Location = new Point (10, 10), Size = new Size (17, 150), Maximum = 100, LargeChange = 10 };
                form.Controls.Add (bar);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 100, 200));
                var scale = bitmap.Width / 100f;

                var columns = Enumerable.Range ((int) (bar.Left * scale), (int) (bar.Width * scale))
                    .Count (x => Enumerable.Range ((int) ((bar.Top + 20) * scale), (int) (40 * scale)).Any (y => bitmap.GetPixel (x, y) == Thumb));

                return (int) System.Math.Round (columns / scale);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void The_thumb_is_a_thin_bar_while_the_pointer_is_elsewhere ()
            => Assert.Equal (2, ThumbColumns (true));

        [Fact]
        public void A_disabled_bar_shows_its_arrows ()
        {
            // As Windows 11 draws a disabled bar -- a multiline text box's, with nothing to scroll: greyed
            // arrows at both ends and no thumb. It showed an empty track.
            try {
                Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Segoe UI", 9f));
                HeadlessRenderer.Use ();
                using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (100, 200) };
                var bar = new VScrollBar { Location = new Point (10, 10), Size = new Size (17, 150), Maximum = 100, LargeChange = 10, Enabled = false };
                form.Controls.Add (bar);
                form.Show ();

                using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 100, 200));
                var scale = bitmap.Width / 100f;

                int Ink (int top) => Enumerable.Range ((int) (bar.Left * scale), (int) (bar.Width * scale))
                    .Sum (x => Enumerable.Range ((int) (top * scale), (int) (14 * scale)).Count (y => bitmap.GetPixel (x, y) != ScrollBar.UpstreamTrack));

                Assert.True (Ink (bar.Top + 1) > 0, "the up arrow");
                Assert.True (Ink (bar.Bottom - 15) > 0, "the down arrow");
                Assert.Equal (0, Ink (bar.Top + 60));   // and no thumb between them
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void Without_a_chosen_font_the_theme_scroll_bar_is_kept ()
            => Assert.NotEqual (2, ThumbColumns (false));
    }
}
