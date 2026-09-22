using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // TSM-41, fixed. The strip renderers paint into a canvas whose coordinates are DEVICE, and
    // `item.Bounds` is LOGICAL because it is also the hit-test space (W6.3, TSM-22). Paint used to hand
    // the logical box straight to the device canvas, so at scaling 2 every strip item was drawn at half
    // its proper size in the strip's top-left quadrant. It now converts through
    // `MenuItem.DeviceBounds`, matching the measure side, which has always converted at its own
    // boundary (`MenuItem.GetPreferredSize`).
    //
    // These were CHARACTERIZATION tests -- they asserted the defect, so that fixing it could not go
    // unnoticed. The fix turned both red, and they are inverted here rather than deleted: the
    // measurement they are built on is the thing that settled the diagnosis, after two rounds of
    // code-reading had got the direction wrong in opposite ways.
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

        // The item is painted where its bounds say, in the canvas' own units -- at every scale. At
        // scaling 1 the two spaces coincide, which is the whole reason this survived: every gate but
        // the scale-2 one is blind to it.
        [Fact]
        public void An_item_is_painted_at_its_device_bounds ()
        {
            var strip = Strip (out var form, out var button);

            using (form) {
                Assert.Equal (button.DeviceBounds, PaintedItemRect (strip, button));
            }
        }

        // The specific regression, stated in the terms the old bug was in: the painted box must scale
        // with the display, not stay at its logical size. Fails if DeviceBounds is reverted to Bounds.
        [Fact]
        public void The_painted_box_grows_with_the_display_scale ()
        {
            var strip = Strip (out var form, out var button);

            using (form) {
                var scale = strip.LogicalToDeviceUnits (10) / 10;
                var painted = PaintedItemRect (strip, button);

                Assert.Equal (button.Bounds.Width * scale, painted.Width);
                Assert.Equal (button.Bounds.Height * scale, painted.Height);

                if (scale > 1)
                    Assert.NotEqual (button.Bounds, painted);
            }
        }

        // What TSM-45 was blocked on: the caption indent is device-converted, so it only makes sense
        // against a device-sized item. It must stay comfortably inside the item at any scale --
        // previously 56 device units against a 62-device item, which put the caption past the edge.
        [Fact]
        public void The_caption_indent_stays_inside_the_item_at_any_scale ()
        {
            var strip = Strip (out var form, out var button);

            using (form) {
                var painted = PaintedItemRect (strip, button);
                var menu_indent = strip.LogicalToDeviceUnits (28);

                Assert.True (menu_indent < painted.Width,
                    $"The drop-down caption indent ({menu_indent}) does not fit inside a painted item " +
                    $"({painted.Width}); TSM-41 has regressed.");
            }
        }
    }
}
