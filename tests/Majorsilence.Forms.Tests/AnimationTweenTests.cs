using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Animation;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Easing, Tween and Animator. A tween has no clock, so it is tested at exact progress; an Animator runs on RequestAnimationFrame, so it is
    // stepped by hand on the Headless clock, which makes the values at t = 0, halfway, the end, completion and cancellation exact.
    public class AnimationTweenTests : IDisposable
    {
        private static readonly TimeSpan Ms100 = TimeSpan.FromMilliseconds (100);
        private static readonly TimeSpan Ms400 = TimeSpan.FromMilliseconds (400);

        public AnimationTweenTests ()
        {
            HeadlessRenderer.Use ();
            HeadlessRenderer.AnimationClock.Reset ();
        }

        public void Dispose ()
        {
            HeadlessRenderer.AnimationClock.Reset ();
            GC.SuppressFinalize (this);
        }

        private static readonly (string Name, EasingFunction Function)[] AllEasings = [
            ("Linear", Easing.Linear), ("QuadIn", Easing.QuadIn), ("QuadOut", Easing.QuadOut), ("QuadInOut", Easing.QuadInOut),
            ("CubicIn", Easing.CubicIn), ("CubicOut", Easing.CubicOut), ("CubicInOut", Easing.CubicInOut),
            ("BackIn", Easing.BackIn), ("BackOut", Easing.BackOut), ("BackInOut", Easing.BackInOut),
            ("BounceIn", Easing.BounceIn), ("BounceOut", Easing.BounceOut), ("BounceInOut", Easing.BounceInOut),
        ];

        // ---- Easing -------------------------------------------------------------------------------

        // Within float error. Assert.Equal with a precision rounds both sides, which fails for a value that sits on a rounding boundary.
        private static void Near (float expected, float actual, float tolerance = 1e-5f)
            => Assert.True (Math.Abs (expected - actual) <= tolerance, $"expected {expected} but was {actual}");

        [Fact]
        public void Every_easing_starts_at_zero_and_ends_at_one ()
        {
            foreach (var (name, function) in AllEasings) {
                Assert.True (Math.Abs (function (0f)) < 1e-6, $"{name} at 0 was {function (0f)}");
                Assert.True (Math.Abs (function (1f) - 1f) < 1e-6, $"{name} at 1 was {function (1f)}");
            }
        }

        [Fact]
        public void Quad_and_cubic_easings_have_their_known_midpoints ()
        {
            Assert.Equal (0.25f, Easing.QuadIn (0.5f), 5);
            Assert.Equal (0.75f, Easing.QuadOut (0.5f), 5);
            Assert.Equal (0.5f, Easing.QuadInOut (0.5f), 5);
            Assert.Equal (0.125f, Easing.CubicIn (0.5f), 5);
            Assert.Equal (0.875f, Easing.CubicOut (0.5f), 5);
            Assert.Equal (0.5f, Easing.CubicInOut (0.5f), 5);
            Assert.Equal (0.5f, Easing.Linear (0.5f), 5);
        }

        [Fact]
        public void An_in_easing_starts_slowly_and_an_out_easing_finishes_slowly ()
        {
            Assert.True (Easing.QuadIn (0.25f) < 0.25f);
            Assert.True (Easing.QuadOut (0.25f) > 0.25f);
            Assert.True (Easing.CubicIn (0.25f) < Easing.QuadIn (0.25f));
            Assert.True (Easing.CubicOut (0.25f) > Easing.QuadOut (0.25f));
        }

        [Fact]
        public void The_in_out_easings_are_symmetrical_about_the_middle ()
        {
            foreach (var t in new[] { 0.1f, 0.25f, 0.4f })
                foreach (var function in new EasingFunction[] { Easing.QuadInOut, Easing.CubicInOut, Easing.BackInOut, Easing.BounceInOut })
                    Assert.Equal (1f - function (1f - t), function (t), 4);
        }

        [Fact]
        public void The_back_easings_leave_the_range_they_overshoot ()
        {
            var samples = Enumerable.Range (0, 101).Select (i => i / 100f).ToArray ();

            Assert.True (samples.Max (Easing.BackOut) > 1f, "BackOut should overshoot 1");
            Assert.True (samples.Min (Easing.BackIn) < 0f, "BackIn should pull back below 0");
            Assert.True (samples.Max (Easing.BackInOut) > 1f && samples.Min (Easing.BackInOut) < 0f);
        }

        [Fact]
        public void The_back_easings_use_the_standard_overshoot_constants ()
        {
            // Both ends are 0 and 1 whatever the constant is, so only interior values can tell them apart. These come from the published
            // formulas with c1 = 1.70158 (c2 = c1 * 1.525, c3 = c1 + 1).
            Near (1.0876975f, Easing.BackOut (0.5f));
            Near (-0.0876975f, Easing.BackIn (0.5f));
            Near (-0.0996818f, Easing.BackInOut (0.25f));
            Near (1.0996818f, Easing.BackInOut (0.75f));
        }

        [Fact]
        public void Bounce_out_has_the_standard_values ()
        {
            // One value in each of its four branches.
            Near (0.47265625f, Easing.BounceOut (0.25f));
            Near (0.765625f, Easing.BounceOut (0.5f));
            Near (0.988125f, Easing.BounceOut (0.9f));
            Near (0.9845311f, Easing.BounceOut (0.95f));
            Assert.Equal (1f - Easing.BounceOut (0.75f), Easing.BounceIn (0.25f), 5);
            Assert.Equal (0.5f, Easing.BounceInOut (0.5f), 5);
        }

        // ---- Tween --------------------------------------------------------------------------------

        [Fact]
        public void A_float_tween_is_at_its_start_halfway_and_at_its_end ()
        {
            var tween = Tween.Of (10f, 30f, Ms400);

            Assert.Equal (10f, tween.ValueAt (TimeSpan.Zero));
            Assert.Equal (20f, tween.ValueAt (TimeSpan.FromMilliseconds (200)), 4);
            Assert.Equal (30f, tween.ValueAt (Ms400));
        }

        [Fact]
        public void A_tween_stays_at_its_ends_outside_its_duration ()
        {
            var tween = Tween.Of (10f, 30f, Ms400);

            Assert.Equal (10f, tween.ValueAt (TimeSpan.FromMilliseconds (-50)));
            Assert.Equal (30f, tween.ValueAt (TimeSpan.FromSeconds (5)));
            Assert.Equal (10f, tween.ValueAtProgress (-3f));
            Assert.Equal (30f, tween.ValueAtProgress (7f));
        }

        [Fact]
        public void A_tween_applies_its_easing ()
        {
            var tween = Tween.Of (0f, 100f, Ms400, Easing.QuadIn);

            Assert.Equal (25f, tween.ValueAt (TimeSpan.FromMilliseconds (200)), 4);
        }

        [Fact]
        public void A_tween_with_no_time_is_at_its_end_at_once ()
        {
            Assert.Equal (9f, Tween.Of (1f, 9f, TimeSpan.Zero).ValueAt (TimeSpan.Zero));
            Assert.Equal (9f, Tween.Of (1f, 9f, TimeSpan.FromSeconds (-1)).ValueAt (TimeSpan.Zero));
        }

        [Fact]
        public void A_point_tween_moves_each_coordinate ()
        {
            var tween = Tween.Of (new PointF (0, 10), new PointF (100, -10), Ms400);

            Assert.Equal (new PointF (50, 0), tween.ValueAt (TimeSpan.FromMilliseconds (200)));
            Assert.Equal (new PointF (100, -10), tween.ValueAt (Ms400));
        }

        [Fact]
        public void A_colour_tween_moves_alpha_red_green_and_blue ()
        {
            var tween = Tween.Of (Color.FromArgb (0, 10, 20, 30), Color.FromArgb (100, 50, 60, 70), Ms400);

            Assert.Equal (Color.FromArgb (0, 10, 20, 30), tween.ValueAt (TimeSpan.Zero));
            Assert.Equal (Color.FromArgb (50, 30, 40, 50), tween.ValueAt (TimeSpan.FromMilliseconds (200)));
            Assert.Equal (Color.FromArgb (100, 50, 60, 70), tween.ValueAt (Ms400));
        }

        [Fact]
        public void A_float_tween_passes_the_overshoot_of_a_back_easing_through ()
        {
            var tween = Tween.Of (0f, 1f, Ms400, Easing.BackOut);

            Assert.True (tween.ValueAtProgress (0.6f) > 1f);
        }

        [Fact]
        public void A_colour_tween_clamps_a_back_overshoot_instead_of_wrapping ()
        {
            var tween = Tween.Of (Color.Black, Color.White, Ms400, Easing.BackOut);

            Assert.Equal (Color.White.ToArgb (), tween.ValueAtProgress (0.6f).ToArgb ());
        }

        [Fact]
        public void A_tween_can_move_any_type_with_your_own_interpolation ()
        {
            var tween = new Tween<int> (0, 10, TimeSpan.FromSeconds (1), (a, b, t) => (int) Math.Round (a + (b - a) * t));

            Assert.Equal (5, tween.ValueAt (TimeSpan.FromMilliseconds (500)));
            Assert.True (tween.IsComplete (TimeSpan.FromSeconds (1)));
            Assert.False (tween.IsComplete (TimeSpan.FromMilliseconds (999)));
        }

        [Fact]
        public void A_tween_refuses_no_interpolation ()
        {
            Assert.Throws<ArgumentNullException> (() => new Tween<int> (0, 1, Ms400, null!));
        }

        // ---- Animator -----------------------------------------------------------------------------

        [Fact]
        public void The_first_frame_is_time_zero_and_the_last_applies_the_end_value ()
        {
            var panel = new Panel ();
            var values = new List<float> ();
            var completed = 0;

            panel.Animate (Tween.Of (0f, 100f, Ms400), values.Add, () => completed++);
            HeadlessRenderer.AnimationClock.Step (1, Ms100);
            Assert.Equal (new[] { 0f }, values);

            HeadlessRenderer.AnimationClock.Step (4, Ms100);

            Assert.Equal (new[] { 0f, 25f, 50f, 75f, 100f }, values);
            Assert.Equal (1, completed);
        }

        [Fact]
        public void Halfway_through_it_applies_the_halfway_value ()
        {
            var panel = new Panel ();
            var values = new List<float> ();
            panel.Animate (Tween.Of (0f, 100f, Ms400), values.Add);

            HeadlessRenderer.AnimationClock.Step (3, Ms100);

            Assert.Equal (50f, values[^1], 4);
        }

        [Fact]
        public void Completion_runs_once_and_no_more_frames_are_asked_for ()
        {
            var panel = new Panel ();
            var completed = 0;
            var handle = panel.Animate (Tween.Of (0f, 1f, Ms400), _ => { }, () => completed++);

            HeadlessRenderer.AnimationClock.Step (10, Ms100);

            Assert.Equal (1, completed);
            Assert.False (handle.IsRunning);
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);
        }

        [Fact]
        public void Cancelling_stops_the_values_and_the_completion ()
        {
            var panel = new Panel ();
            var values = new List<float> ();
            var completed = 0;
            var handle = panel.Animate (Tween.Of (0f, 100f, Ms400), values.Add, () => completed++);
            HeadlessRenderer.AnimationClock.Step (2, Ms100);
            Assert.Equal (2, values.Count);
            Assert.True (handle.IsRunning);

            handle.Cancel ();
            HeadlessRenderer.AnimationClock.Step (5, Ms100);

            Assert.Equal (2, values.Count);
            Assert.Equal (0, completed);
            Assert.False (handle.IsRunning);
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);
        }

        [Fact]
        public void Cancelling_twice_is_harmless ()
        {
            var handle = new Panel ().Animate (Tween.Of (0f, 1f, Ms400), _ => { });

            handle.Cancel ();
            handle.Cancel ();

            Assert.False (handle.IsRunning);
        }

        [Fact]
        public void Cancelling_from_inside_the_apply_callback_stops_the_next_frame ()
        {
            var panel = new Panel ();
            var count = 0;
            AnimationHandle? handle = null;
            handle = panel.Animate (Tween.Of (0f, 1f, Ms400), _ => {
                count++;
                handle!.Cancel ();
            });

            HeadlessRenderer.AnimationClock.Step (1, Ms100);

            // Right after the frame that cancelled it, nothing is queued. A later frame that found it cancelled and stopped would hide a
            // request that should never have been made.
            Assert.Equal (1, count);
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);

            HeadlessRenderer.AnimationClock.Step (2, Ms100);
            Assert.Equal (1, count);
        }

        [Fact]
        public void A_disposed_control_stops_its_animation ()
        {
            var panel = new Panel ();
            var values = new List<float> ();
            var handle = panel.Animate (Tween.Of (0f, 100f, Ms400), values.Add);
            HeadlessRenderer.AnimationClock.Step (1, Ms100);

            panel.Dispose ();
            HeadlessRenderer.AnimationClock.Step (3, Ms100);

            Assert.Single (values);
            Assert.False (handle.IsRunning);
        }

        [Fact]
        public void An_animation_with_no_time_applies_the_end_value_on_the_first_frame_and_completes ()
        {
            var panel = new Panel ();
            var values = new List<float> ();
            var completed = 0;

            panel.Animate (Tween.Of (0f, 100f, TimeSpan.Zero), values.Add, () => completed++);
            HeadlessRenderer.AnimationClock.Step (1, Ms100);

            Assert.Equal (new[] { 100f }, values);
            Assert.Equal (1, completed);
        }

        [Fact]
        public void Two_animations_on_one_control_run_independently ()
        {
            var panel = new Panel ();
            var tween = Tween.Of (0f, 100f, Ms400);
            var first = new List<float> ();
            var second = new List<float> ();

            panel.Animate (tween, first.Add);
            HeadlessRenderer.AnimationClock.Step (2, Ms100);
            var later = panel.Animate (tween, second.Add);
            HeadlessRenderer.AnimationClock.Step (1, Ms100);

            Assert.Equal (new[] { 0f, 25f, 50f }, first);
            Assert.Equal (new[] { 0f }, second);
            Assert.True (later.IsRunning);
        }

        [Fact]
        public void An_exception_from_apply_ends_the_animation_and_comes_out_of_the_frame ()
        {
            var panel = new Panel ();
            var count = 0;
            var handle = panel.Animate (Tween.Of (0f, 1f, Ms400), _ => {
                if (++count == 2)
                    throw new InvalidOperationException ("boom");
            });

            var error = Assert.Throws<InvalidOperationException> (() => HeadlessRenderer.AnimationClock.Step (5, Ms100));

            Assert.Equal ("boom", error.Message);
            Assert.False (handle.IsRunning);
        }

        [Fact]
        public void The_completion_callback_sees_the_animation_as_finished ()
        {
            var panel = new Panel ();
            AnimationHandle? handle = null;
            var runningInside = true;
            handle = panel.Animate (Tween.Of (0f, 1f, Ms400), _ => { }, () => runningInside = handle!.IsRunning);

            HeadlessRenderer.AnimationClock.Step (5, Ms100);

            Assert.False (runningInside);
        }

        [Fact]
        public void A_window_can_be_animated_too_because_a_form_is_not_a_control ()
        {
            using var form = new Form ();
            var values = new List<float> ();
            var completed = 0;

            form.Animate (Tween.Of (0f, 100f, Ms400), values.Add, () => completed++);
            HeadlessRenderer.AnimationClock.Step (5, Ms100);

            Assert.Equal (new[] { 0f, 25f, 50f, 75f, 100f }, values);
            Assert.Equal (1, completed);
        }

        [Fact]
        public void A_window_animation_can_be_cancelled_and_stops_when_the_window_is_disposed ()
        {
            var form = new Form ();
            var values = new List<float> ();
            var handle = form.Animate (Tween.Of (0f, 100f, Ms400), values.Add);
            HeadlessRenderer.AnimationClock.Step (2, Ms100);

            form.Dispose ();
            HeadlessRenderer.AnimationClock.Step (3, Ms100);

            Assert.Equal (2, values.Count);
            Assert.False (handle.IsRunning);

            var second = new Form ();
            var other = second.Animate (Tween.Of (0f, 100f, Ms400), values.Add);
            other.Cancel ();
            HeadlessRenderer.AnimationClock.Step (2, Ms100);
            Assert.Equal (2, values.Count);
            second.Dispose ();
        }

        [Fact]
        public void A_colour_animation_ends_on_the_end_colour ()
        {
            var panel = new Panel ();
            var colours = new List<Color> ();

            panel.Animate (Tween.Of (Color.Black, Color.Red, Ms400), colours.Add);
            HeadlessRenderer.AnimationClock.Step (5, Ms100);

            Assert.Equal (Color.Black.ToArgb (), colours[0].ToArgb ());
            Assert.Equal (Color.Red.ToArgb (), colours[^1].ToArgb ());
        }

        [Fact]
        public void Animate_refuses_missing_arguments ()
        {
            var panel = new Panel ();
            var tween = Tween.Of (0f, 1f, Ms400);

            Assert.Throws<ArgumentNullException> (() => ((Control) null!).Animate (tween, (float _) => { }));
            Assert.Throws<ArgumentNullException> (() => panel.Animate<float> (null!, _ => { }));
            Assert.Throws<ArgumentNullException> (() => panel.Animate (tween, null!));
            Assert.Throws<ArgumentNullException> (() => ((WindowBase) null!).Animate (tween, (float _) => { }));
            using var form = new Form ();
            Assert.Throws<ArgumentNullException> (() => form.Animate<float> (null!, _ => { }));
            Assert.Throws<ArgumentNullException> (() => form.Animate (tween, null!));
        }
    }
}
