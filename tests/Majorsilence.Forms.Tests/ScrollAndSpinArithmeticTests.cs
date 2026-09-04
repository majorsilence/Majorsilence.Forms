using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.20's arithmetic slice (findings SMP-31 P0, SMP-48, SMP-49). Three defects that property
    // tests cannot see, because every property involved reads back exactly what was written:
    //
    //  - a NumericUpDown arrow click stepped by 1 whatever Increment said, because the mouse path
    //    never called the methods that apply it;
    //  - a ScrollBar let a user-driven scroll reach Maximum instead of Maximum - LargeChange + 1, so
    //    a custom-scrolled view ran a whole page past its content;
    //  - one wheel notch moved 120 x SmallChange, because Delta arrives in units of 120.
    [Collection ("Headless")]
    public class ScrollAndSpinArithmeticTests
    {
        // Drives OnMouseClick, which is the arrow path: RaiseMouseDown/Up do not synthesise a click.
        private sealed class ClickableSpinner : NumericUpDown
        {
            internal void ClickAt (Point location)
                => OnMouseClick (new MouseEventArgs (MouseButtons.Left, 1, location.X, location.Y, 0));

            internal Point IncrementCentre => Centre (GetIncrementArea ());
            internal Point DecrementCentre => Centre (GetDecrementArea ());

            private static Point Centre (Rectangle r) => new Point (r.Left + r.Width / 2, r.Top + r.Height / 2);
        }

        // The protected handlers directly, and the arrow coordinates ScrollBarTests already uses: the
        // Raise* entry points route a wheel event through the control tree, which is not the unit under
        // test here, and a point in the middle of the track lands on the THUMB once the value is high
        // enough -- so a "click the track repeatedly" test silently stops scrolling near the end.
        private sealed class WheeledScrollBar : VerticalScrollBar
        {
            internal WheeledScrollBar () { Size = new Size (17, 200); }

            internal void Wheel (int delta)
                => OnMouseWheel (new MouseEventArgs (MouseButtons.None, 0, 8, 100, delta));

            internal void ClickBottomArrow ()
                => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, 8, Height - 2, 0));
        }

        // ---------------- SMP-31 (P0): the arrows honour Increment

        [Theory]
        [InlineData (5)]
        [InlineData (25)]
        public void An_arrow_click_steps_by_Increment (int increment)
        {
            HeadlessRenderer.Use ();
            using var spinner = new ClickableSpinner { Width = 120, Height = 24, Maximum = 1000 };
            spinner.Increment = increment;
            spinner.Value = 0;

            spinner.ClickAt (spinner.IncrementCentre);

            Assert.Equal (increment, spinner.Value);

            spinner.ClickAt (spinner.DecrementCentre);

            Assert.Equal (0, spinner.Value);
        }

        [Fact]
        public void A_fractional_Increment_survives_an_arrow_click ()
        {
            // The currency case: 0.01 steps used to become 1.00 steps, which is the kind of silent
            // corruption that reaches a database before anyone notices.
            HeadlessRenderer.Use ();
            using var spinner = new ClickableSpinner { Width = 120, Height = 24, DecimalPlaces = 2 };
            spinner.Increment = 0.01m;
            spinner.Value = 1.00m;

            spinner.ClickAt (spinner.IncrementCentre);

            Assert.Equal (1.01m, spinner.Value);
        }

        [Fact]
        public void An_arrow_click_still_stops_at_the_bounds ()
        {
            // Proof, as it turns out, not the guard it looked like: the old code clamped to Maximum
            // too, but it also stepped by 1, so from 8 it reached 9 and never the bound.
            HeadlessRenderer.Use ();
            using var spinner = new ClickableSpinner { Width = 120, Height = 24, Minimum = 0, Maximum = 10 };
            spinner.Increment = 4;
            spinner.Value = 8;

            spinner.ClickAt (spinner.IncrementCentre);

            Assert.Equal (10, spinner.Value);
        }

        // ---------------- SMP-48: a user-driven scroll stops one page short of Maximum

        [Fact]
        public void An_arrow_scroll_stops_at_Maximum_minus_LargeChange_plus_one ()
        {
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 100, LargeChange = 10, SmallChange = 1 };

            // More presses than the range needs, so it ends up wherever the clamp puts it.
            for (var i = 0; i < 120; i++)
                bar.ClickBottomArrow ();

            // 100 - 10 + 1: the last page of content starts here, so scrolling further would show
            // blank space past the end. It used to reach 100.
            Assert.Equal (91, bar.Value);
        }

        [Fact]
        public void The_wheel_stops_at_the_same_place ()
        {
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 100, LargeChange = 10, SmallChange = 5 };

            for (var i = 0; i < 40; i++)
                bar.Wheel (-120);

            Assert.Equal (91, bar.Value);
        }

        [Fact]
        public void A_programmatic_assignment_may_still_reach_Maximum ()
        {
            // Upstream clamps only user-driven scrolls; the property setter validates against Maximum
            // and assigns it. Clamping here as well would make a documented assignment silently do
            // something else -- which is what the first version of this fix did.
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 100, LargeChange = 10 };

            bar.Value = 100;

            Assert.Equal (100, bar.Value);
        }

        [Fact]
        public void The_effective_maximum_never_falls_below_Minimum ()
        {
            // GUARD, not proof: a LargeChange wider than the range would otherwise compute a maximum
            // below Minimum, and the clamp would invert.
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 10, Maximum = 12, LargeChange = 50, SmallChange = 1 };

            for (var i = 0; i < 5; i++)
                bar.Wheel (-120);

            Assert.Equal (10, bar.Value);
        }

        // ---------------- SMP-49: one notch is one SmallChange

        [Fact]
        public void One_wheel_notch_moves_by_one_SmallChange ()
        {
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 1000, LargeChange = 10, SmallChange = 3, Value = 100 };

            bar.Wheel (-120);

            Assert.Equal (103, bar.Value);

            bar.Wheel (120);

            Assert.Equal (100, bar.Value);
        }

        [Fact]
        public void Several_notches_in_one_event_move_by_that_many ()
        {
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 1000, LargeChange = 10, SmallChange = 2, Value = 0 };

            bar.Wheel (-360);

            Assert.Equal (6, bar.Value);
        }

        [Fact]
        public void A_partial_notch_accumulates_instead_of_being_lost ()
        {
            // What a precision wheel or a trackpad sends. Rounding each event down to zero would make
            // such a device scroll nothing at all.
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 1000, LargeChange = 10, SmallChange = 1, Value = 0 };

            for (var i = 0; i < 2; i++) {
                bar.Wheel (-40);
                Assert.Equal (0, bar.Value);   // 40 then 80: still inside the first notch
            }

            bar.Wheel (-40);   // 3 x 40 = 120, exactly one notch

            Assert.Equal (1, bar.Value);

            // And the accumulator resets, so the next notch takes another three events.
            bar.Wheel (-40);
            Assert.Equal (1, bar.Value);
        }

        [Fact]
        public void A_wheel_notch_ends_with_EndScroll ()
        {
            // The standard "defer the expensive redraw until scrolling stops" handler waits for this.
            HeadlessRenderer.Use ();
            using var bar = new WheeledScrollBar { Minimum = 0, Maximum = 1000, LargeChange = 10, SmallChange = 1, Value = 10 };
            var types = new System.Collections.Generic.List<ScrollEventType> ();
            bar.Scroll += (_, e) => types.Add (e.Type);

            bar.Wheel (-120);

            Assert.Equal (new[] { ScrollEventType.SmallIncrement, ScrollEventType.EndScroll }, types.ToArray ());
        }
    }
}
