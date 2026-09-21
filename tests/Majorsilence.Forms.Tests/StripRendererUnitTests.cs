using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TSM-41, re-diagnosed. The finding said the strip renderers paint into a canvas whose coordinates
    // are LOGICAL and that the bug is their device-converted constants. Measurement says the opposite:
    // the canvas is DEVICE, the constants are right, and it is `item.Bounds` -- which is logical -- that
    // is handed to the canvas unconverted.
    //
    // The consequence is far worse than the "glyphs drift inward" the finding describes: at scaling 2
    // every strip item is painted at HALF its proper size, in the top-left quadrant of the strip.
    //
    // These are characterization tests. They assert what the code does TODAY, not what it should do,
    // and they exist so that the defect cannot be fixed silently: fixing TSM-41 must turn the two
    // `Documents` tests red, at which point they are inverted rather than deleted. The measurement
    // itself is the valuable part -- it is how the diagnosis was settled, and it is cheap to re-run.
    [Collection ("Headless")]
    public class StripRendererUnitTests
    {
        private static ToolStrip Strip (out Form form, out ToolStripButton button)
        {
            HeadlessRenderer.Use ();

            button = new ToolStripButton { Text = "Open" };

            var strip = new ToolStrip { Width = 300, Height = 30, GripVisible = false };
            strip.Items.Add (button);

            form = new Form { Width = 400, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        // The item's hover fill IS item.Bounds handed straight to FillRectangle, so diffing hovered
        // against unhovered gives the item's painted rectangle in device pixels with no font, padding
        // or chrome in the way. This is the measurement that settled the diagnosis.
        private static Rectangle PaintedItemRect (ToolStrip strip, ToolStripButton button)
        {
            using var cold = PaintSurface.Render (strip);
            button.Hovered = true;
            using var hot = PaintSurface.Render (strip);
            button.Hovered = false;

            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;

            for (var y = 0; y < hot.Height; y++)
                for (var x = 0; x < hot.Width; x++)
                    if (hot.GetPixel (x, y) != cold.GetPixel (x, y)) {
                        if (x < x0) x0 = x;
                        if (y < y0) y0 = y;
                        if (x > x1) x1 = x;
                        if (y > y1) y1 = y;
                    }

            Assert.True (x1 >= 0, "Hovering the item changed no pixels at all.");

            return Rectangle.FromLTRB (x0, y0, x1 + 1, y1 + 1);
        }

        // At scaling 1 there is nothing to tell apart, which is the whole reason this survived: every
        // gate but the scale-2 one is blind to it.
        [Fact]
        public void At_scale_one_the_item_is_painted_at_its_bounds ()
        {
            var strip = Strip (out var form, out var button);

            using (form) {
                if (strip.LogicalToDeviceUnits (10) != 10)
                    return; // running under MF_HEADLESS_SCALE; the scale-2 test below is the one that applies

                Assert.Equal (button.Bounds, PaintedItemRect (strip, button));
            }
        }

        // Documents TSM-41. The painted rectangle equals the LOGICAL bounds measured in DEVICE pixels,
        // which is to say the item is drawn at 1/scale of its proper size. When TSM-41 is fixed this
        // must fail; invert it to expect LogicalToDeviceUnits(button.Bounds) and delete this comment.
        [Fact]
        public void Documents_that_a_scaled_strip_paints_its_items_half_size ()
        {
            var strip = Strip (out var form, out var button);

            using (form) {
                var scale = strip.LogicalToDeviceUnits (10) / 10;

                if (scale == 1)
                    return; // only meaningful under MF_HEADLESS_SCALE=2

                var painted = PaintedItemRect (strip, button);

                Assert.Equal (button.Bounds, painted);

                // And explicitly NOT the rectangle it should occupy.
                var correct = new Rectangle (
                    strip.LogicalToDeviceUnits (button.Bounds.Left), strip.LogicalToDeviceUnits (button.Bounds.Top),
                    strip.LogicalToDeviceUnits (button.Bounds.Width), strip.LogicalToDeviceUnits (button.Bounds.Height));

                Assert.NotEqual (correct, painted);
            }
        }

        // The concrete cost, and why TSM-41 is a prerequisite rather than a cosmetic item: the caption's
        // indent IS device-converted (28 -> 56 at scale 2) while the box it is measured against is not,
        // so the caption is pushed past the item's own right edge. This is what stopped
        // ContextMenuStrip.ShowImageMargin being wired (TSM-45).
        [Fact]
        public void Documents_that_the_caption_indent_outgrows_the_item_when_scaled ()
        {
            var strip = Strip (out var form, out var button);

            using (form) {
                var scale = strip.LogicalToDeviceUnits (10) / 10;

                if (scale == 1)
                    return;

                var painted = PaintedItemRect (strip, button);
                var menu_indent = strip.LogicalToDeviceUnits (28);

                Assert.True (menu_indent > painted.Width * 3 / 4,
                    $"The drop-down caption indent ({menu_indent}) no longer dwarfs the painted item " +
                    $"({painted.Width}) -- if TSM-41 is fixed, retire this test with it.");
            }
        }
    }
}
