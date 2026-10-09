using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Holds back the soft-keyboard request a text box makes when a touch lands on it, until the touch is known to be a tap.
    /// </summary>
    /// <remarks>
    /// A press focuses a text box at once, and focus asks for the keyboard. When that press is the start of a swipe the keyboard
    /// should not come up: it covers half the screen, and the next swipe then lands on the keyboard instead of the content.
    /// So the request waits for the release; a drag that claims the gesture drops it.
    /// </remarks>
    internal sealed class TouchKeyboardGate
    {
        private TextInputKind held_kind;

        /// <summary>Gets whether a keyboard request is waiting for the touch to finish.</summary>
        internal bool IsHolding { get; private set; }

        /// <summary>
        /// Takes a text-input request. Returns true when it must be applied now; false when it was held, because
        /// <paramref name="touchPending"/> says a touch is down and has not yet become a drag.
        /// </summary>
        internal bool Request (bool active, TextInputKind kind, bool touchPending)
        {
            if (!active || !touchPending) {
                IsHolding = false;
                return true;
            }

            IsHolding = true;
            held_kind = kind;
            return false;
        }

        /// <summary>The touch ended as a tap: returns the held kind to apply, or null when nothing was held.</summary>
        internal TextInputKind? Tap ()
        {
            if (!IsHolding)
                return null;

            IsHolding = false;
            return held_kind;
        }

        /// <summary>The touch became a drag: the held request is dropped.</summary>
        internal void Drag () => IsHolding = false;
    }
}
