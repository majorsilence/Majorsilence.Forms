using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class AvaloniaGestureWiringTests
    {
        [Fact]
        public void FingerMovingUp_IsANegativeDelta ()
        {
            // The recognizer reports +15 logical px when the finger moves up; ScrollableControl scrolls forward on a negative delta.
            var (x, y) = AvaloniaGestureWiring.FingerDelta (0, 15, 2.75);

            Assert.Equal (0, x);
            Assert.Equal (-41, y);
        }

        [Fact]
        public void FingerMovingLeft_IsANegativeDelta ()
        {
            var (x, _) = AvaloniaGestureWiring.FingerDelta (8, 0, 2);

            Assert.Equal (-16, x);
        }

        [Fact]
        public void ScrollableControl_ScrollsForward_OnTheDeltaTheWiringProduces ()
        {
            var form = new Form ();
            try {
                form.Show ();
                var panel = new Panel { Left = 0, Top = 0, Width = 100, Height = 100, AutoScroll = true, AutoScrollMinSize = new System.Drawing.Size (400, 1000) };
                form.Controls.Add (panel);
                panel.PerformLayout ();

                var (dx, dy) = AvaloniaGestureWiring.FingerDelta (0, 15, 1);
                var at = WindowPoint.DeviceIn (panel, 20, 20);
                form.HandleScrollGesture (at.X, at.Y, dx, dy);

                Assert.True (panel.VerticalScrollProperties.Value > 0, "a finger moving up should scroll the content forward");
            } finally {
                form.Close ();
            }
        }
    }
}
