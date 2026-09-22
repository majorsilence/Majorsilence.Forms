using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // NavigationPane laid its items out against ClientRectangle, which is DEVICE, while the layout
    // engine writes LOGICAL bounds -- so every item got a logical width equal to the pane's DEVICE
    // width. At MF_HEADLESS_SCALE=2 that is 151 logical units inside an 80-wide pane: the items
    // overran the control and their painted fill was clipped at its edge.
    //
    // Found while building StripPaintSpaceTests for TSM-41, which had to clamp its expectation to the
    // control to accommodate it. It is the same logical/device confusion as TSM-41, one layer down:
    // that one painted a logical box into a device canvas, this one laid out into a device box and
    // recorded the result as logical. Menu.LayoutItems has always used LogicalClientRectangle; this
    // was the only layout in the assembly measuring against the scaled rectangle.
    //
    // Exact at scaling 1, which is why it survived every gate but the scale-2 one.
    [Collection ("Headless")]
    public class NavigationPaneLayoutTests
    {
        private static NavigationPane Pane (out Form form)
        {
            HeadlessRenderer.Use ();

            var pane = new NavigationPane { Width = 80, Height = 200 };
            pane.Items.Add (new NavigationPaneItem (new SkiaSharp.SKBitmap (16, 16), "One"));
            pane.Items.Add (new NavigationPaneItem (new SkiaSharp.SKBitmap (16, 16), "Two"));

            form = new Form { Width = 400, Height = 320 };
            form.Controls.Add (pane);
            form.Show ();
            PaintSurface.Render (pane).Dispose ();

            return pane;
        }

        [Fact]
        public void An_item_is_no_wider_than_the_pane ()
        {
            var pane = Pane (out var form);

            using (form) {
                foreach (var item in pane.Items)
                    Assert.True (item.Bounds.Width <= pane.Width,
                        $"item is {item.Bounds.Width} logical units wide inside a {pane.Width}-wide pane");
            }
        }

        // The specific regression: the item's width must not track the display scale. It is a logical
        // measurement, and the pane's logical width does not change when the display does.
        [Fact]
        public void An_items_width_does_not_grow_with_the_display_scale ()
        {
            var pane = Pane (out var form);

            using (form) {
                var scale = pane.LogicalToDeviceUnits (10) / 10;
                var width = pane.Items[0].Bounds.Width;

                Assert.True (width < pane.Width * scale || scale == 1,
                    $"item width {width} looks like the pane's DEVICE width ({pane.Width * scale}), not its logical one");
            }
        }

        // And the items still fill the pane: a fix that merely shrank them would pass the two above.
        [Fact]
        public void The_items_still_span_the_panes_width ()
        {
            var pane = Pane (out var form);

            using (form) {
                foreach (var item in pane.Items)
                    Assert.True (item.Bounds.Width > pane.Width / 2,
                        $"item is only {item.Bounds.Width} of a {pane.Width}-wide pane -- VerticalExpand should fill it");
            }
        }

        // Stacked vertically, not overlapping: the other thing a wrong layout rectangle breaks.
        [Fact]
        public void The_items_are_stacked_without_overlapping ()
        {
            var pane = Pane (out var form);

            using (form) {
                var first = pane.Items[0].Bounds;
                var second = pane.Items[1].Bounds;

                Assert.True (second.Top >= first.Bottom,
                    $"items overlap: {first} then {second}");
            }
        }
    }
}
