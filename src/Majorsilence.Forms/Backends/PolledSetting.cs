using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Turns a value a backend can only read on demand (a system setting with no push notification) into one that raises a change
    /// event, by reading it again on every tick of an <see cref="IPlatformTimer"/> and comparing. Platform-agnostic and independent of
    /// what the reader function actually reads, so it is reused wherever a backend has a setting to poll instead of observe directly
    /// (a native push notification, where the platform offers one, is always preferred over this).
    /// </summary>
    internal sealed class PolledSetting : IDisposable
    {
        private readonly Func<bool> read;
        private readonly IPlatformTimer timer;
        private bool disposed;

        public PolledSetting (Func<bool> read, IPlatformTimer timer, double intervalMilliseconds)
        {
            Guard.ThrowIfNull (read);
            Guard.ThrowIfNull (timer);

            this.read = read;
            this.timer = timer;
            // Read now, so a caller sees the real value on the very first tick's interval rather than a stale default.
            Current = read ();
            timer.IntervalMilliseconds = intervalMilliseconds;
            timer.Tick += OnTick;
            timer.Start ();
        }

        /// <summary>Gets the value as of the last read.</summary>
        public bool Current { get; private set; }

        /// <summary>Raised, on the UI thread (because <see cref="IPlatformTimer.Tick"/> is), when a poll finds <see cref="Current"/> changed.</summary>
        public event EventHandler? Changed;

        private void OnTick ()
        {
            if (disposed)
                return;

            var value = read ();
            if (value == Current)
                return;

            Current = value;
            Changed?.Invoke (this, EventArgs.Empty);
        }

        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            timer.Tick -= OnTick;
            timer.Stop ();
            timer.Dispose ();
        }
    }
}
