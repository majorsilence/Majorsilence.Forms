using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The frame a form draws round itself has to survive content that fills the client area. The client
    // area is laid out at the window's full size from the top-left border inward, and the clip meant to
    // keep it off the frame ended one pixel past the right and bottom borders -- with a one-pixel border it
    // excluded nothing there. A docked panel, or a message box's button strip, painted over the right and
    // bottom frame while the left and top stayed, so the window looked cut off on two sides. Seen first on
    // the browser head, where a single-view host draws dialogs on top of their owner.
    [Collection ("Headless")]
    public class FormFrameClipTests
    {
        private static readonly SKColor Fill = new (255, 0, 0);

        [Fact]
        public void Filled_client_area_leaves_the_right_and_bottom_frame_drawn ()
        {
            HeadlessRenderer.Use ();

            // Library-drawn chrome, so there is a frame to look at on every OS (macOS otherwise uses the native one).
            using var form = new Form { UseSystemDecorations = false, Size = new Size (300, 160) };
            form.Controls.Add (new Panel { Dock = DockStyle.Fill, BackColor = Color.Red });
            form.Show ();

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 160));
            var midX = bitmap.Width / 2;
            var midY = bitmap.Height / 2;

            var left = bitmap.GetPixel (0, midY);
            var top = bitmap.GetPixel (midX, 0);

            // There is a frame, and the panel does fill the client area right up to it.
            Assert.NotEqual (Fill, left);
            Assert.Equal (Fill, bitmap.GetPixel (bitmap.Width / 4, midY));

            // The far edges are the frame too, not the panel.
            Assert.Equal (left, bitmap.GetPixel (bitmap.Width - 1, midY));
            Assert.Equal (top, bitmap.GetPixel (midX, bitmap.Height - 1));
        }
    }
}
