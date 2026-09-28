using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // RequestAnimationFrame: a callback aligned to the display frame, so an animation does not depend on a timer. On Headless the frames are
    // stepped by hand, which is what makes an animation test deterministic. The Avalonia implementation is the top level's own frame
    // request and needs a running Avalonia app, so it is not tested here.
    public class AnimationFrameTests : IDisposable
    {
        private static readonly TimeSpan Frame = HeadlessAnimationClock.DefaultFrameInterval;

        public AnimationFrameTests ()
        {
            HeadlessRenderer.Use ();
            HeadlessRenderer.AnimationClock.Reset ();
        }

        public void Dispose ()
        {
            HeadlessRenderer.AnimationClock.Reset ();
            GC.SuppressFinalize (this);
        }

        // ---- the Headless manual clock ------------------------------------------------------------

        [Fact]
        public void Nothing_runs_until_the_clock_is_stepped ()
        {
            var panel = new Panel ();
            var seen = new List<TimeSpan> ();

            panel.RequestAnimationFrame (seen.Add);

            Assert.Empty (seen);
            Assert.Equal (1, HeadlessRenderer.AnimationClock.PendingCount);

            HeadlessRenderer.AnimationClock.Step ();

            Assert.Equal (new[] { Frame }, seen);
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);
        }

        [Fact]
        public void An_animation_that_asks_for_the_next_frame_each_time_steps_ten_frames_deterministically ()
        {
            var panel = new Panel ();
            var seen = new List<TimeSpan> ();
            void OnFrame (TimeSpan timestamp)
            {
                seen.Add (timestamp);
                if (seen.Count < 10)
                    panel.RequestAnimationFrame (OnFrame);
            }
            panel.RequestAnimationFrame (OnFrame);

            HeadlessRenderer.AnimationClock.Step (10);

            Assert.Equal (Enumerable.Range (1, 10).Select (i => TimeSpan.FromTicks (Frame.Ticks * i)), seen);
            Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);
        }

        [Fact]
        public void A_request_made_inside_a_callback_waits_for_the_next_frame ()
        {
            var panel = new Panel ();
            var frames = 0;
            void OnFrame (TimeSpan _)
            {
                frames++;
                panel.RequestAnimationFrame (OnFrame);
            }
            panel.RequestAnimationFrame (OnFrame);

            HeadlessRenderer.AnimationClock.Step ();
            Assert.Equal (1, frames);

            HeadlessRenderer.AnimationClock.Step ();
            Assert.Equal (2, frames);
        }

        [Fact]
        public void Step_advances_by_the_frame_length_it_is_given ()
        {
            var panel = new Panel ();
            var seen = new List<TimeSpan> ();
            void OnFrame (TimeSpan t)
            {
                seen.Add (t);
                panel.RequestAnimationFrame (OnFrame);
            }
            panel.RequestAnimationFrame (OnFrame);

            HeadlessRenderer.AnimationClock.Step (3, TimeSpan.FromMilliseconds (10));

            Assert.Equal (new[] { TimeSpan.FromMilliseconds (10), TimeSpan.FromMilliseconds (20), TimeSpan.FromMilliseconds (30) }, seen);
            Assert.Equal (TimeSpan.FromMilliseconds (30), HeadlessRenderer.AnimationClock.Now);
        }

        [Fact]
        public void Every_request_waiting_at_the_start_of_a_frame_runs_in_that_frame_in_order ()
        {
            var panel = new Panel ();
            var order = new List<string> ();
            panel.RequestAnimationFrame (_ => order.Add ("first"));
            panel.RequestAnimationFrame (_ => order.Add ("second"));

            HeadlessRenderer.AnimationClock.Step ();

            Assert.Equal (new[] { "first", "second" }, order);
        }

        [Fact]
        public void Reset_forgets_waiting_requests_and_puts_time_back_to_zero ()
        {
            var ran = false;
            new Panel ().RequestAnimationFrame (_ => ran = true);
            HeadlessRenderer.AnimationClock.Step (2);

            HeadlessRenderer.AnimationClock.Reset ();
            new Panel ().RequestAnimationFrame (_ => ran = true);
            HeadlessRenderer.AnimationClock.Reset ();
            ran = false;
            HeadlessRenderer.AnimationClock.Step ();

            Assert.False (ran);
            Assert.Equal (Frame, HeadlessRenderer.AnimationClock.Now);
        }

        [Fact]
        public void Step_refuses_no_frames_and_a_frame_that_takes_no_time ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => HeadlessRenderer.AnimationClock.Step (0));
            Assert.Throws<ArgumentOutOfRangeException> (() => HeadlessRenderer.AnimationClock.Step (1, TimeSpan.Zero));
        }

        [Fact]
        public void A_form_and_a_control_on_no_window_use_the_same_clock ()
        {
            using var form = new Form ();
            var seen = new List<string> ();

            form.RequestAnimationFrame (_ => seen.Add ("form"));
            new Panel ().RequestAnimationFrame (_ => seen.Add ("panel"));
            HeadlessRenderer.AnimationClock.Step ();

            Assert.Equal (new[] { "form", "panel" }, seen);
        }

        [Fact]
        public void Asking_for_a_frame_with_no_callback_is_refused ()
        {
            using var form = new Form ();

            Assert.Throws<ArgumentNullException> (() => new Panel ().RequestAnimationFrame (null!));
            Assert.Throws<ArgumentNullException> (() => form.RequestAnimationFrame (null!));
            Assert.Throws<ArgumentNullException> (() => AnimationFrames.RequestOnTimer (null!));
        }

        // ---- routing: the window's own backend first ----------------------------------------------

        // A window backend that offers frames. IWindowBackend is large, so a proxy stands in for it and answers every call with a default.
        public class FrameWindowBackend : DispatchProxy, IAnimationFrameSource
        {
            public List<Action<TimeSpan>> Requests { get; } = [];

            protected override object? Invoke (MethodInfo? targetMethod, object?[]? args)
                => targetMethod is { ReturnType.IsValueType: true } && targetMethod.ReturnType != typeof (void)
                    ? Activator.CreateInstance (targetMethod.ReturnType)
                    : null;

            void IAnimationFrameSource.RequestAnimationFrame (Action<TimeSpan> callback) => Requests.Add (callback);
        }

        [Fact]
        public void A_window_whose_backend_offers_frames_is_asked_before_the_platform ()
        {
            using var form = new Form ();
            var original = form.Backend;
            var backend = DispatchProxy.Create<IWindowBackend, FrameWindowBackend> ();
            var requests = ((FrameWindowBackend) backend).Requests;
            form.Backend = backend;
            try {
                var panel = new Panel ();
                form.Controls.Add (panel);

                form.RequestAnimationFrame (_ => { });
                panel.RequestAnimationFrame (_ => { });

                Assert.Equal (2, requests.Count);
                Assert.Equal (0, HeadlessRenderer.AnimationClock.PendingCount);
            } finally {
                form.Backend = original;
            }
        }

        // ---- the timer fallback for a backend with no frame request -------------------------------

        [Fact]
        public void The_timer_fallback_runs_every_waiting_request_in_one_frame_with_one_timestamp ()
        {
            var seen = new List<TimeSpan> ();
            AnimationFrames.RequestOnTimer (seen.Add);
            AnimationFrames.RequestOnTimer (seen.Add);

            AnimationFrames.RunFrame ();

            Assert.Equal (2, seen.Count);
            Assert.Equal (seen[0], seen[1]);
        }

        [Fact]
        public void The_timer_fallback_serves_a_request_made_during_a_frame_on_the_next_one ()
        {
            var stamps = new List<TimeSpan> ();
            void OnFrame (TimeSpan t)
            {
                stamps.Add (t);
                if (stamps.Count < 3)
                    AnimationFrames.RequestOnTimer (OnFrame);
            }
            AnimationFrames.RequestOnTimer (OnFrame);

            AnimationFrames.RunFrame ();
            Assert.Single (stamps);

            AnimationFrames.RunFrame ();
            Assert.Equal (2, stamps.Count);
            Assert.True (stamps[1] >= stamps[0], "timestamps must not go backwards");
        }

        [Fact]
        public void One_failing_callback_does_not_starve_the_others_in_a_timer_frame ()
        {
            var ran = false;
            AnimationFrames.RequestOnTimer (_ => throw new InvalidOperationException ("boom"));
            AnimationFrames.RequestOnTimer (_ => ran = true);

            var error = Assert.Throws<InvalidOperationException> (AnimationFrames.RunFrame);

            Assert.Equal ("boom", error.Message);
            Assert.True (ran);
        }

        [Fact]
        public void Several_failing_callbacks_are_reported_together ()
        {
            AnimationFrames.RequestOnTimer (_ => throw new InvalidOperationException ("one"));
            AnimationFrames.RequestOnTimer (_ => throw new InvalidOperationException ("two"));

            var error = Assert.Throws<AggregateException> (AnimationFrames.RunFrame);

            Assert.Equal (2, error.InnerExceptions.Count);
        }

        [Fact]
        public void An_empty_timer_frame_does_nothing ()
        {
            AnimationFrames.RunFrame ();
            AnimationFrames.RunFrame ();
        }
    }
}
