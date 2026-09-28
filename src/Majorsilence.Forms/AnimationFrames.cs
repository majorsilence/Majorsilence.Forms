using System;
using System.Collections.Generic;
using System.Diagnostics;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    // Where RequestAnimationFrame goes. The window's own backend knows its display, so it is asked first; then the platform backend (the
    // Headless backend's manual clock lives there, so a control that is not on a window is still deterministic in a test); and a backend
    // that offers neither gets a timer at about 60 Hz, which is what animation was before there was a frame request.
    internal static class AnimationFrames
    {
        private const double TimerIntervalMilliseconds = 1000.0 / 60.0;

        private static readonly Stopwatch clock = Stopwatch.StartNew ();
        private static readonly List<Action<TimeSpan>> pending = [];
        private static IPlatformTimer? timer;
        private static IPlatformBackend? timerBackend;

        public static void Request (WindowBase? window, Action<TimeSpan> callback)
        {
            Guard.ThrowIfNull (callback);

            if (window?.Backend is IAnimationFrameSource windowSource) {
                windowSource.RequestAnimationFrame (callback);
                return;
            }

            if (Platform.Backend is IAnimationFrameSource platformSource) {
                platformSource.RequestAnimationFrame (callback);
                return;
            }

            RequestOnTimer (callback);
        }

        // The fallback for a backend with no frame request. UI thread only, like the controls it serves.
        public static void RequestOnTimer (Action<TimeSpan> callback)
        {
            Guard.ThrowIfNull (callback);

            pending.Add (callback);

            // A test can swap the backend under a running process, and a timer belongs to the backend that made it.
            if (timer is null || !ReferenceEquals (timerBackend, Platform.Backend)) {
                timer?.Dispose ();
                timerBackend = Platform.Backend;
                timer = timerBackend.CreateTimer ();
                timer.IntervalMilliseconds = TimerIntervalMilliseconds;
                timer.Tick += RunFrame;
            }

            timer.Start ();
        }

        // What a timer tick does. Internal so a test can run one frame without waiting for real time.
        internal static void RunFrame ()
        {
            // Snapshot first: a callback that asks for the next frame must get it on the next tick, not on this one, or an animation
            // would run away inside a single frame.
            var frame = pending.ToArray ();
            pending.Clear ();

            if (frame.Length == 0) {
                timer?.Stop ();
                return;
            }

            var now = clock.Elapsed;
            List<Exception>? failures = null;
            foreach (var callback in frame) {
                try {
                    callback (now);
                } catch (Exception e) {
                    (failures ??= []).Add (e);
                }
            }

            // One callback failing must not starve the others, so they all ran; then the failure is not swallowed.
            if (failures is not null)
                throw failures.Count == 1 ? failures[0] : new AggregateException (failures);
        }
    }
}
