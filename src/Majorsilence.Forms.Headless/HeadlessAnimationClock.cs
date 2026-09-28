using System;
using System.Collections.Generic;

namespace Majorsilence.Forms.Headless
{
    /// <summary>
    /// The Headless backend's animation frames, stepped by hand so that an animation test is deterministic: nothing runs until
    /// <see cref="Step"/> is called, and each step is exactly one frame of a chosen length.
    /// </summary>
    public sealed class HeadlessAnimationClock
    {
        private readonly object gate = new ();
        private List<Action<TimeSpan>> pending = [];

        /// <summary>Gets the length of one frame when <see cref="Step"/> is not told otherwise: one sixtieth of a second.</summary>
        public static TimeSpan DefaultFrameInterval { get; } = TimeSpan.FromTicks (TimeSpan.TicksPerSecond / 60);

        /// <summary>Gets the timestamp of the latest frame, which starts at zero.</summary>
        public TimeSpan Now { get; private set; }

        /// <summary>Gets how many frame requests are waiting for the next <see cref="Step"/>.</summary>
        public int PendingCount {
            get {
                lock (gate)
                    return pending.Count;
            }
        }

        internal void Request (Action<TimeSpan> callback)
        {
            ArgumentNullException.ThrowIfNull (callback);

            lock (gate)
                pending.Add (callback);
        }

        /// <summary>
        /// Advances time by <paramref name="frames"/> frames of <paramref name="frameInterval"/> each, and on each runs the requests that
        /// were waiting when the frame began. A request made inside a callback waits for the next frame, as on a real display.
        /// </summary>
        public void Step (int frames = 1, TimeSpan? frameInterval = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero (frames);
            var interval = frameInterval ?? DefaultFrameInterval;
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException (nameof (frameInterval), "A frame has to take some time.");

            for (var frame = 0; frame < frames; frame++) {
                Now += interval;

                List<Action<TimeSpan>> due;
                lock (gate) {
                    due = pending;
                    pending = [];
                }

                foreach (var callback in due)
                    callback (Now);
            }
        }

        /// <summary>Puts the clock back to zero and forgets every waiting request, so one test's animation cannot leak into the next.</summary>
        public void Reset ()
        {
            lock (gate)
                pending = [];

            Now = TimeSpan.Zero;
        }
    }
}
