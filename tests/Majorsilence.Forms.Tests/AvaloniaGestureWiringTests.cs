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
            using var form = new Form { ClientSize = new System.Drawing.Size (200, 200) };
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            panel.Controls.Add (new Label { Location = new System.Drawing.Point (0, 0), Size = new System.Drawing.Size (100, 1000) });
            form.Controls.Add (panel);
            form.Show ();

            var (dx, dy) = AvaloniaGestureWiring.FingerDelta (0, 40, 1);
            form.HandleScrollGesture (50, 50, dx, dy);

            Assert.True (-panel.AutoScrollPosition.Y > 0, "a finger moving up should scroll the content forward");
        }
    }
}
