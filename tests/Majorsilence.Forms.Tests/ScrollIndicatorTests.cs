using System.Drawing;
using System.Reflection;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The scroll bar's thumb is placed from Value across the range up to Maximum - LargeChange + 1. A ScrollableControl used to set Maximum
    // to the largest scroll offset and LargeChange to the viewport, which left that range at (content - 2 x viewport): none at all for a page
    // shorter than two screens, so the thumb sat at the top while the content scrolled, and on a longer page it stopped early.
    public class ScrollIndicatorTests
    {
        private static VerticalScrollBar BarOf (ScrollableControl panel)
            => (VerticalScrollBar) typeof (ScrollableControl).GetField ("vscrollbar", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue (panel)!;

        private static (Form Form, Panel Panel, VerticalScrollBar Bar) Open (int contentHeight)
        {
            var form = new Form ();
            form.Show ();
            var panel = new Panel { Left = 0, Top = 0, Width = 200, Height = 200, AutoScroll = true, AutoScrollMinSize = new Size (100, contentHeight) };
            form.Controls.Add (panel);
            panel.PerformLayout ();
            return (form, panel, BarOf (panel));
        }

        [Theory]
        [InlineData (300)]      // under two screens: the case that had no range at all
        [InlineData (1000)]
        public void TheThumbFollowsTheContent_AndEndsAtTheEndOfTheTrack (int contentHeight)
        {
            var (form, panel, bar) = Open (contentHeight);
            try {
                var start = bar.thumb_drag_position;
                var at = WindowPoint.DeviceIn (panel, 20, 20);

                form.HandleScrollGesture (at.X, at.Y, 0, -panel.LogicalToDeviceUnits (40));
                var partway = bar.thumb_drag_position;
                form.HandleScrollGesture (at.X, at.Y, 0, -panel.LogicalToDeviceUnits (5000));

                Assert.True (partway > start, "a scrolled page should move the thumb");
                Assert.True (bar.thumb_drag_position > partway, "scrolling on to the end should move it again");
                Assert.Equal (bar.EffectiveMaximum, bar.Value);       // the end of the content is the end of the bar's range
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void TheBarCanBeDraggedToTheEndOfTheContent ()
        {
            var (form, panel, bar) = Open (300);
            try {
                var farthestOffset = panel.VerticalScrollProperties.Value;
                var at = WindowPoint.DeviceIn (panel, 20, 20);
                form.HandleScrollGesture (at.X, at.Y, 0, -panel.LogicalToDeviceUnits (5000));
                farthestOffset = panel.VerticalScrollProperties.Value;

                // A user-driven scroll to the end of the bar's range reaches the same place the gesture did.
                var viaBar = (int) typeof (ScrollBar).GetProperty ("EffectiveMaximum", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue (bar)!;
                Assert.Equal (farthestOffset, viaBar);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void TheTouchIndicator_LeavesNoTrailWhereItWas ()
        {
            var form = new Form { ClientSize = new Size (240, 240) };
            try {
                ((HeadlessWindowHost) form.Backend).IsSingleView = true;
                form.Show ();
                var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, AutoScrollMinSize = new Size (100, 900) };
                form.Controls.Add (panel);
                panel.PerformLayout ();

                // The indicator's strip, where the thumb sits at the top while the page is unscrolled.
                var spot = WindowPoint.DeviceIn (panel, panel.Width - 5, 14);
                using var before = SkiaSharp.SKBitmap.Decode (HeadlessRenderer.CapturePng (form));
                var atRest = before.GetPixel (spot.X, spot.Y);

                var at = WindowPoint.DeviceIn (panel, 20, 20);
                form.HandleScrollGesture (at.X, at.Y, 0, -panel.LogicalToDeviceUnits (5000));
                using var after = SkiaSharp.SKBitmap.Decode (HeadlessRenderer.CapturePng (form));
                var later = after.GetPixel (spot.X, spot.Y);

                Assert.NotEqual (SkiaSharp.SKColors.White, atRest);                    // the thumb was drawn at the top
                Assert.Equal (SkiaSharp.SKColors.White, later);                        // and is gone from there once it has moved
            } finally {
                form.Close ();
            }
        }
    }
}
