using Majorsilence.Forms.Backends;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Avalonia reports wheel movement in lines (1.0 per notch, fractional on a trackpad); WinForms
    // reports multiples of WHEEL_DELTA = 120. The backend used to cast the Avalonia value straight to
    // an int, so a notch arrived as 1 instead of 120 and sub-notch trackpad movement truncated to 0.
    // Consumers use the value as a pixel count or divide it by 120 with integer arithmetic, so the
    // visible effect was scrolling at roughly a hundredth speed -- a few pixels per notch.
    public class WheelDeltaAccumulatorTests
    {
        [Fact]
        public void One_notch_reports_one_wheel_delta ()
        {
            var accumulator = new WheelDeltaAccumulator ();

            Assert.Equal (120, accumulator.Add (0, 1).Y);
        }

        [Fact]
        public void A_negative_notch_reports_a_negative_wheel_delta ()
        {
            var accumulator = new WheelDeltaAccumulator ();

            Assert.Equal (-120, accumulator.Add (0, -1).Y);
        }

        [Fact]
        public void Sub_notch_movement_reports_nothing_yet ()
        {
            var accumulator = new WheelDeltaAccumulator ();

            // A fraction of a unit must not be reported: `e.Delta / 120` would be zero, so a caller
            // would scroll nowhere and the movement would be lost.
            Assert.Equal (0, accumulator.Add (0, 0.3).Y);
        }

        [Fact]
        public void Sub_notch_movement_accumulates_until_it_is_a_whole_notch ()
        {
            var accumulator = new WheelDeltaAccumulator ();

            accumulator.Add (0, 0.3);
            accumulator.Add (0, 0.3);
            accumulator.Add (0, 0.3);

            // 4 x 0.3 = 1.2 lines, so one whole unit is due and 0.2 stays pending.
            Assert.Equal (120, accumulator.Add (0, 0.3).Y);
        }

        [Fact]
        public void Nothing_is_discarded_across_a_long_gesture ()
        {
            var accumulator = new WheelDeltaAccumulator ();
            var total = 0;

            // Ten frames of a third of a notch is 3.33 notches; three whole ones must come out. A
            // rounding-based conversion would drift here, which is the subtle form of the same bug.
            for (var i = 0; i < 10; i++)
                total += accumulator.Add (0, 1 / 3.0).Y;

            Assert.Equal (3 * 120, total);
        }

        [Fact]
        public void Horizontal_and_vertical_accumulate_independently ()
        {
            var accumulator = new WheelDeltaAccumulator ();

            var first = accumulator.Add (0.5, 1);
            Assert.Equal (0, first.X);
            Assert.Equal (120, first.Y);

            var second = accumulator.Add (0.5, 0);
            Assert.Equal (120, second.X);
            Assert.Equal (0, second.Y);
        }
    }
}
