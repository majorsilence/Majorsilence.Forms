using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The gesture decisions WinUI does not make for us. These exist as tests because they cannot be
    // verified any other way here: the Uno backend needs multi-touch hardware to exercise, and each
    // of these rules has a failure mode that looks like "gestures feel wrong" rather than like a
    // crash -- which is the hardest kind of bug to find later.
    public class GestureHeuristicsTests
    {
        // ── Pinch versus pan ────────────────────────────────────────────────────

        [Fact]
        public void A_frame_with_no_scale_or_rotation_change_is_a_pan ()
        {
            Assert.False (GestureHeuristics.IsPinchFrame (1d, 0d));
        }

        [Theory]
        [InlineData (1.0004, 0.0)]
        [InlineData (0.9996, 0.0)]
        [InlineData (1.0, 0.3)]
        [InlineData (1.0, -0.3)]
        public void Two_contacts_drifting_while_dragged_are_still_a_pan (double scale, double rotation)
        {
            // Two fingers dragged across a screen never hold an exactly constant separation. Testing
            // against exact equality classified every one of these as a pinch, and since the two are
            // mutually exclusive, two-finger panning would then never scroll anything.
            Assert.False (GestureHeuristics.IsPinchFrame (scale, rotation));
        }

        [Theory]
        [InlineData (1.05, 0.0)]
        [InlineData (0.95, 0.0)]
        [InlineData (1.0, 3.0)]
        [InlineData (1.0, -3.0)]
        public void A_deliberate_zoom_or_rotate_is_a_pinch (double scale, double rotation)
        {
            Assert.True (GestureHeuristics.IsPinchFrame (scale, rotation));
        }

        // ── Swipe ───────────────────────────────────────────────────────────────

        [Fact]
        public void A_slow_drag_is_not_a_swipe ()
        {
            Assert.False (GestureHeuristics.TryClassifySwipe (100d, 0d, out _));
        }

        [Fact]
        public void A_stationary_release_is_not_a_swipe ()
        {
            Assert.False (GestureHeuristics.TryClassifySwipe (0d, 0d, out _));
        }

        [Theory]
        [InlineData (900d, 0d, SwipeDirection.Right)]
        [InlineData (-900d, 0d, SwipeDirection.Left)]
        [InlineData (0d, 900d, SwipeDirection.Down)]
        [InlineData (0d, -900d, SwipeDirection.Up)]
        public void A_fast_flick_reports_its_direction (double vx, double vy, SwipeDirection expected)
        {
            Assert.True (GestureHeuristics.TryClassifySwipe (vx, vy, out var direction));
            Assert.Equal (expected, direction);
        }

        [Fact]
        public void The_dominant_axis_decides_a_diagonal_flick ()
        {
            Assert.True (GestureHeuristics.TryClassifySwipe (900d, -400d, out var direction));
            Assert.Equal (SwipeDirection.Right, direction);

            Assert.True (GestureHeuristics.TryClassifySwipe (-400d, 900d, out direction));
            Assert.Equal (SwipeDirection.Down, direction);
        }

        [Fact]
        public void A_diagonal_flick_is_measured_by_its_actual_speed ()
        {
            // Neither axis alone clears 500, but the flick travels at about 566 px/s. Testing the
            // axes separately would reject a genuine diagonal swipe.
            Assert.True (GestureHeuristics.TryClassifySwipe (400d, 400d, out _));
        }

        [Fact]
        public void A_swipe_at_exactly_the_threshold_counts ()
        {
            Assert.True (GestureHeuristics.TryClassifySwipe (GestureHeuristics.MinSwipeVelocity, 0d, out _));
        }

        // ── Units ───────────────────────────────────────────────────────────────

        [Fact]
        public void A_realistic_flick_in_winui_units_clears_the_threshold_once_converted ()
        {
            // WinUI reports DIP per millisecond; the neutral SwipeGestureEventArgs is documented as
            // pixels per second. A brisk flick is about 2 DIP/ms -- which is 2000 px/s, well over the
            // threshold, but only *after* converting. Comparing the raw per-millisecond value against
            // a per-second threshold demands 500 DIP/ms, roughly 150x faster than a human can move,
            // so swipe would never have fired on the Uno backend.
            const double winUiVelocity = 2d;
            var perSecond = winUiVelocity * GestureHeuristics.MillisecondsPerSecond;

            Assert.False (GestureHeuristics.TryClassifySwipe (winUiVelocity, 0d, out _));
            Assert.True (GestureHeuristics.TryClassifySwipe (perSecond, 0d, out _));
        }

        [Fact]
        public void The_conversion_factor_is_milliseconds_per_second ()
        {
            Assert.Equal (1000d, GestureHeuristics.MillisecondsPerSecond);
        }
    }
}
