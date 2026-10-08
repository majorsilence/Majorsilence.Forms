using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Animates images that have time-based frames (e.g. animated GIFs). Cross-platform replacement for
    /// System.Drawing.ImageAnimator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model is upstream's (<c>System.Drawing.Common/src/System/Drawing/ImageAnimator.cs</c> and
    /// <c>ImageInfo.cs</c>): <see cref="Animate"/> starts a timer that runs every 40 ms, moves each
    /// animating image along by the elapsed time against its own per-frame delays, and when an image
    /// reaches a new frame marks it dirty and raises its handler. The handler typically invalidates the
    /// control showing the image; <see cref="UpdateFrames()"/>, called from that control's paint,
    /// selects the pending frame. A file's loop count is honoured, ending on the last frame.
    /// </para>
    /// <para>
    /// Upstream raises the handler on its animation thread. Once Majorsilence.Forms has installed its
    /// UI-thread dispatcher (a <c>PictureBox</c> does as it starts animating) the timer's work is
    /// posted to the UI thread instead, so a handler can touch controls directly. Until then it runs on
    /// the timer thread, as upstream's does.
    /// </para>
    /// </remarks>
    public static class ImageAnimator
    {
        // Upstream's tick (ImageAnimator.AnimationDelayMS), and the delay ImageInfo gives a frame
        // whose stored delay is zero.
        internal const int AnimationDelayMS = 40;

        private sealed class AnimationState
        {
            private readonly long[] frame_end_times;
            private readonly long total_animation_time;
            private readonly int loop_count;
            private readonly int frame_count;
            private long frame_timer;
            private int loop;

            public AnimationState (Image image)
            {
                Image = image;
                frame_count = image.GetFrameCount (FrameDimension.Time);
                frame_end_times = new long[frame_count];

                // ImageInfo.cs: delays accumulate into each frame's end time, a zero delay counting as
                // one tick.
                var durations = image.FrameDurations;
                long end = 0;
                for (var f = 0; f < frame_count; f++) {
                    var delay = durations is { Length: > 0 } ? durations[f % durations.Length] : 0;
                    end += delay > 0 ? delay : AnimationDelayMS;
                    frame_end_times[f] = end;
                }
                total_animation_time = end;

                // Upstream's loop count is the file's: 0 loops forever, n plays n + 1 times. Skia reports
                // forever as -1 and a file without a loop count as 0, which GDI+ also plays forever.
                loop_count = image.RepetitionCount > 0 ? image.RepetitionCount : 0;
            }

            public Image Image { get; }

            public List<EventHandler> Handlers { get; } = [];

            public int Frame { get; private set; }

            public bool FrameDirty { get; set; }

            private bool ShouldAnimate => total_animation_time > 0 && (loop_count == 0 || loop <= loop_count);

            /// <summary>Moves the animation on; true when that reached a different frame.</summary>
            /// <remarks>ImageInfo.AdvanceAnimationBy, step for step.</remarks>
            public bool AdvanceBy (long milliseconds)
            {
                if (!ShouldAnimate)
                    return false;

                var oldFrame = Frame;
                frame_timer += milliseconds;

                if (frame_timer > total_animation_time) {
                    loop += (int)(frame_timer / total_animation_time);
                    frame_timer %= total_animation_time;

                    if (!ShouldAnimate) {
                        // Out of loops: hold the last frame.
                        Frame = frame_count - 1;
                        frame_timer = total_animation_time;
                    } else if (Frame > 0 && frame_timer < frame_end_times[Frame - 1]) {
                        Frame = 0;
                    }
                }

                while (Frame < frame_count - 1 && frame_timer > frame_end_times[Frame])
                    Frame++;

                if (Frame == oldFrame)
                    return false;

                FrameDirty = true;
                return true;
            }
        }

        private static readonly Dictionary<Image, AnimationState> animated = [];
        private static readonly object gate = new ();
        private static readonly Stopwatch stopwatch = Stopwatch.StartNew ();
        private static IDisposable? timer;
        private static long last_tick;
        private static int tick_pending;

        /// <summary>
        /// Posts work to the UI thread, or null to run it on the timer thread as upstream does.
        /// Majorsilence.Forms installs <c>Application.RunOnUIThread</c> here.
        /// </summary>
        internal static Action<Action>? UIThreadDispatcher { get; set; }

        /// <summary>The time in milliseconds the animation measures elapsed time by. A test seam.</summary>
        internal static Func<long> Clock { get; set; } = () => stopwatch.ElapsedMilliseconds;

        /// <summary>
        /// Starts the repeating timer that drives animation, given the callback to run on each tick,
        /// and returns what stops it. A test seam: the default is a thread-pool timer at
        /// <see cref="AnimationDelayMS"/>.
        /// </summary>
        internal static Func<Action, IDisposable> TimerFactory { get; set; } =
            callback => new Timer (_ => callback (), null, AnimationDelayMS, AnimationDelayMS);

        /// <summary>Whether the animation timer is running: true while any image is animating.</summary>
        internal static bool IsTimerRunning {
            get { lock (gate) return timer is not null; }
        }

        /// <summary>Returns whether the image has more than one frame along the time dimension.</summary>
        public static bool CanAnimate (Image? image)
            => image is not null && image.GetFrameCount (FrameDimension.Time) > 1;

        /// <summary>
        /// Begins animating the image: <paramref name="onFrameChangedHandler"/> is raised each time the
        /// image reaches a new frame, after which <see cref="UpdateFrames(Image)"/> selects it. Has no
        /// effect on a single-frame image.
        /// </summary>
        public static void Animate (Image image, EventHandler onFrameChangedHandler)
        {
            if (!CanAnimate (image))
                return;

            lock (gate) {
                if (!animated.TryGetValue (image, out var state))
                    animated[image] = state = new AnimationState (image);
                if (onFrameChangedHandler is not null && !state.Handlers.Contains (onFrameChangedHandler))
                    state.Handlers.Add (onFrameChangedHandler);

                if (timer is null) {
                    last_tick = Clock ();
                    timer = TimerFactory (OnTimer);
                }
            }
        }

        /// <summary>Stops animating the image for the specified handler.</summary>
        public static void StopAnimate (Image image, EventHandler onFrameChangedHandler)
        {
            if (image is null)
                return;

            IDisposable? stopped = null;
            lock (gate) {
                if (!animated.TryGetValue (image, out var state))
                    return;
                state.Handlers.Remove (onFrameChangedHandler);
                if (state.Handlers.Count == 0)
                    animated.Remove (image);

                // Upstream's thread outlives its last image; a timer with nothing to animate is only
                // cost, so it stops and the next Animate starts another.
                if (animated.Count == 0) {
                    stopped = timer;
                    timer = null;
                }
            }
            stopped?.Dispose ();
        }

        /// <summary>Selects the pending frame of every animating image whose frame has changed.</summary>
        public static void UpdateFrames ()
        {
            AnimationState[] snapshot;
            lock (gate)
                snapshot = [.. animated.Values];

            foreach (var state in snapshot)
                Apply (state);
        }

        /// <summary>Selects the pending frame of the specified image, if its frame has changed.</summary>
        public static void UpdateFrames (Image image) => ApplyPendingFrame (image);

        /// <summary>
        /// <see cref="UpdateFrames(Image)"/>, reporting whether a new frame was selected -- a caller that
        /// caches the image's pixels needs to know when to refresh them.
        /// </summary>
        internal static bool ApplyPendingFrame (Image? image)
        {
            if (image is null)
                return false;

            AnimationState? state;
            lock (gate)
                animated.TryGetValue (image, out state);

            return state is not null && Apply (state);
        }

        private static bool Apply (AnimationState state)
        {
            int frame;
            lock (gate) {
                if (!state.FrameDirty)
                    return false;
                state.FrameDirty = false;
                frame = state.Frame;
            }

            state.Image.SelectActiveFrame (FrameDimension.Time, frame);
            return true;
        }

        // Runs on the timer's thread. At most one tick is queued on the UI thread at a time, so a busy
        // UI thread falls behind by elapsed time (which Tick measures) rather than by a growing queue.
        private static void OnTimer ()
        {
            if (Interlocked.Exchange (ref tick_pending, 1) == 1)
                return;

            var dispatcher = UIThreadDispatcher;
            if (dispatcher is null) {
                Tick ();
                return;
            }

            try {
                dispatcher (Tick);
            } catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) {
                // No UI thread to post to (none configured yet, or shutting down): try again next tick
                // rather than let the exception end the process from a thread-pool thread.
                Interlocked.Exchange (ref tick_pending, 0);
            }
        }

        /// <summary>One timer tick: moves every animation on by the time since the last one.</summary>
        internal static void Tick ()
        {
            Interlocked.Exchange (ref tick_pending, 0);

            long elapsed;
            lock (gate) {
                var now = Clock ();
                elapsed = now - last_tick;
                last_tick = now;
            }

            AdvanceAnimationBy (elapsed);
        }

        /// <summary>
        /// Moves every animating image on by <paramref name="milliseconds"/>, raising the handlers of
        /// each one that reached a new frame.
        /// </summary>
        internal static void AdvanceAnimationBy (long milliseconds)
        {
            var changed = new List<(Image Image, EventHandler[] Handlers)> ();
            lock (gate) {
                foreach (var state in animated.Values)
                    if (state.AdvanceBy (milliseconds))
                        changed.Add ((state.Image, state.Handlers.ToArray ()));
            }

            // Raised outside the lock, from a snapshot: a handler calling StopAnimate (or painting, which
            // calls UpdateFrames) must neither deadlock nor mutate the list mid-loop.
            foreach (var (image, handlers) in changed)
                foreach (var handler in handlers)
                    handler (image, EventArgs.Empty);
        }
    }
}
