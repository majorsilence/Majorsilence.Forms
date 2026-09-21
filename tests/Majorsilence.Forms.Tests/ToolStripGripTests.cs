using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the reopened entries -- the strip's grip and image box (TSM-43, TSM-44).
    //
    //   ToolStrip.GripVisible / GripMargin   a strip that asked for a drag grip got no grip AND no
    //                                        space reserved for one
    //   ToolStrip.ImageScalingSize           every item image was drawn at a hard-coded 20, so a strip
    //                                        asking for 24 or 32px icons got 20px ones
    //
    // The grip band is reserved out of the rectangle LAYOUT measures against and painted from the same
    // number, so the space kept free and the space drawn cannot disagree -- which is the bug that a
    // grip painted over the first item would be.
    [Collection ("Headless")]
    public class ToolStripGripTests
    {
        private static ToolStrip Strip (out Form form, out ToolStripButton first)
        {
            HeadlessRenderer.Use ();

            first = new ToolStripButton { Text = "Open" };

            var strip = new ToolStrip { Width = 300, Height = 30 };
            strip.Items.Add (first);

            form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        [Fact]
        public void A_visible_grip_reserves_space_before_the_first_item ()
        {
            var strip = Strip (out var form, out var first);

            using (form) {
                Assert.True (strip.GripVisible);

                var with_grip = first.Bounds.Left;

                strip.GripVisible = false;
                Relayout (strip);

                Assert.True (first.Bounds.Left < with_grip,
                    $"Turning the grip off did not free its space: {with_grip} -> {first.Bounds.Left}");
                Assert.Equal (0, first.Bounds.Left);
            }
        }

        // GripStyle is the knob upstream actually uses; GripVisible is the one this layer declares.
        // Either turning it off has to work, or a designer setting one of them is ignored.
        [Fact]
        public void GripStyle_Hidden_also_frees_the_space ()
        {
            var strip = Strip (out var form, out var first);

            using (form) {
                var with_grip = first.Bounds.Left;

                strip.GripStyle = ToolStripGripStyle.Hidden;
                Relayout (strip);

                Assert.True (first.Bounds.Left < with_grip);
            }
        }

        [Fact]
        public void GripMargin_widens_the_band ()
        {
            var strip = Strip (out var form, out var first);

            using (form) {
                var narrow = first.Bounds.Left;

                strip.GripMargin = new Padding (10);
                Relayout (strip);

                Assert.True (first.Bounds.Left > narrow,
                    $"A wider GripMargin did not move the first item: {narrow} -> {first.Bounds.Left}");
            }
        }

        [Fact]
        public void The_grip_is_actually_drawn_in_the_band ()
        {
            var strip = Strip (out var form, out var first);

            using (form) {
                // A FIXED window, measured while the grip is on and reused after it is off. Letting
                // the window follow the band was the first version's mistake: with the grip off the
                // band is zero-wide, so "no ink in it" was true of an empty rectangle rather than of
                // a strip with no grip. And the window stops short of the bottom edge, because
                // ToolBar's DefaultStyle draws a 1px rule along it that crosses the band and counted
                // as grip ink -- both halves of that first version passed with nothing drawn at all.
                var window = strip.LogicalToDeviceUnits (first.Bounds.Left);
                var drawn = BandInk (strip, window);

                strip.GripVisible = false;
                Relayout (strip);

                Assert.True (drawn > 0, "Nothing was drawn in the reserved grip band.");
                Assert.True (drawn > BandInk (strip, window),
                    "The same window drew as much with the grip turned off.");
            }
        }

        // A status bar and a menu bar are not draggable, and upstream's constructors say so. Without
        // this, turning the grip on by default would have shifted every StatusStrip's items too.
        [Theory]
        [InlineData (typeof (StatusStrip))]
        [InlineData (typeof (MenuStrip))]
        public void Bars_that_are_not_draggable_have_no_grip (System.Type type)
        {
            HeadlessRenderer.Use ();

            var strip = (ToolStrip) System.Activator.CreateInstance (type)!;

            Assert.Equal (ToolStripGripStyle.Hidden, strip.GripStyle);
        }

        // ---------------- ImageScalingSize (TSM-44)

        [Fact]
        public void ImageScalingSize_is_the_box_an_item_image_is_drawn_in ()
        {
            HeadlessRenderer.Use ();

            var button = new ToolStripButton { Image = Swatch () };
            var strip = new ToolStrip { Width = 300, Height = 60 };
            strip.Items.Add (button);

            using var form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();

            strip.ImageScalingSize = new Size (16, 16);
            var small = ImageInk (strip, button);

            strip.ImageScalingSize = new Size (32, 32);
            var large = ImageInk (strip, button);

            Assert.True (large > small, $"A 32px box drew no more image than a 16px one: {large} vs {small}");
        }

        [Fact]
        public void The_default_box_is_the_WinForms_one ()
        {
            HeadlessRenderer.Use ();

            Assert.Equal (new Size (16, 16), new ToolStrip ().ImageScalingSize);
        }

        // LayoutItems runs from the paint pass, so a property that changes the layout is not visible
        // in item.Bounds until the strip has been painted again -- PerformLayout on its own leaves the
        // previous frame's boxes in place.
        private static void Relayout (ToolStrip strip)
        {
            strip.PerformLayout ();
            PaintSurface.Render (strip).Dispose ();
        }

        private static SkiaSharp.SKBitmap Swatch ()
        {
            var bitmap = new SkiaSharp.SKBitmap (64, 64);

            using (var canvas = new SkiaSharp.SKCanvas (bitmap))
                canvas.Clear (new SkiaSharp.SKColor (255, 0, 0));

            return bitmap;
        }

        // Red pixels are the swatch and nothing else on the strip, so counting them measures the box
        // the image was scaled into rather than anything about the item's chrome.
        private static int ImageInk (ToolStrip strip, ToolStripItem item)
        {
            Relayout (strip);

            using var bitmap = PaintSurface.Render (strip);
            var bounds = item.Bounds;
            var ink = 0;

            for (var y = bounds.Top; y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red > 180 && p.Green < 90 && p.Blue < 90)
                        ink++;
                }

            return ink;
        }

        // Ink in the leftmost `width` device pixels, excluding the bottom two rows where the strip's
        // own border rule lives.
        private static int BandInk (ToolStrip strip, int width)
        {
            using var bitmap = PaintSurface.Render (strip);
            var right = System.Math.Min (bitmap.Width, width);
            var bottom = System.Math.Max (0, bitmap.Height - 2);
            var background = bitmap.GetPixel (bitmap.Width - 2, bitmap.Height / 2);
            var ink = 0;

            for (var y = 0; y < bottom; y++)
                for (var x = 0; x < right; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }
    }
}
