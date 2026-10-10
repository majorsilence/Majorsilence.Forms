namespace Majorsilence.Forms
{
    /// <summary>
    /// Follows one touch at a time. The framework's input model has one pointer: a second finger landing while the first is down would take
    /// the mouse capture, so the first control never saw its release and stayed pressed.
    /// </summary>
    /// <remarks>
    /// The first finger down is the pointer; a second finger's presses, moves and releases are ignored until the first lifts. Two-finger
    /// gestures (pinch) come from Avalonia's recognizers, not from these events, so ignoring them here costs nothing.
    /// </remarks>
    internal sealed class TouchPointerFilter
    {
        // If the followed finger's release never arrives (the window lost focus, a cancelled touch), another finger takes over once it has
        // been silent this long, so input can never stay locked to a finger that is no longer there.
        internal static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds (5);

        private readonly Func<TimeSpan> now;
        private int? active;
        private TimeSpan lastSeen;

        internal TouchPointerFilter (Func<TimeSpan>? now = null)
        {
            this.now = now ?? (() => TimeSpan.FromTicks (System.Diagnostics.Stopwatch.GetTimestamp () * TimeSpan.TicksPerSecond / System.Diagnostics.Stopwatch.Frequency));
        }

        /// <summary>A pointer went down. Returns whether it is the one the framework follows.</summary>
        internal bool Pressed (int pointerId, bool isTouch)
        {
            if (!isTouch)
                return true;

            if (active is null || active == pointerId || now () - lastSeen > StaleAfter) {
                active = pointerId;
                lastSeen = now ();
                return true;
            }

            return false;
        }

        /// <summary>A pointer moved. Returns whether it is the followed one.</summary>
        internal bool Moved (int pointerId, bool isTouch)
        {
            if (!isTouch || active is null)
                return true;

            if (active != pointerId)
                return false;

            lastSeen = now ();
            return true;
        }

        /// <summary>A pointer lifted or was cancelled. Returns whether it was the followed one, which is then released.</summary>
        internal bool Released (int pointerId, bool isTouch)
        {
            if (!isTouch)
                return true;

            if (active is null || active == pointerId) {
                active = null;
                return true;
            }

            return false;
        }
    }
}
