using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional: implemented by a platform backend that can answer whether the user has asked for reduced motion (Android's animator
    /// duration scale at zero, iOS's Reduce Motion, Windows' client-area animation setting, macOS's reduce-motion, GNOME's
    /// enable-animations), and tell when that changes. A backend that does not implement it answers "no" through
    /// <see cref="SystemInformation.PrefersReducedMotion"/>, the same conservative default the framework already uses for its other
    /// UI-effect members: it never suppresses an animation nobody asked to suppress.
    /// </summary>
    public interface IReducedMotionSource
    {
        /// <summary>Gets whether the user currently prefers reduced motion.</summary>
        bool PrefersReducedMotion { get; }

        /// <summary>Raised, on the UI thread, when <see cref="PrefersReducedMotion"/> changes.</summary>
        event EventHandler? PrefersReducedMotionChanged;
    }
}
