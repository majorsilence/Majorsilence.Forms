using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend that can trigger real haptic feedback
    /// (Android's <c>Vibrator</c>, iOS's <c>UIImpactFeedbackGenerator</c>/<c>UISelectionFeedbackGenerator</c>).
    /// Unlike <see cref="IAudioBackend"/>, a backend with nothing to offer here (desktop, browser, Headless)
    /// simply does not implement this interface at all, rather than implementing it with a no-op body --
    /// <see cref="Haptics.IsSupported"/> is exactly that interface-presence check, and a device with no
    /// vibration motor has no "supported but does nothing" middle state worth representing.
    /// </summary>
    public interface IHapticsBackend
    {
        /// <summary>A light tap, for a selection changing or a small UI acknowledgement.</summary>
        void Tap ();

        /// <summary>A firmer, single pulse, for a more significant action succeeding or landing.</summary>
        void Impact ();

        /// <summary>Vibrates for approximately <paramref name="duration"/> -- an alert-style buzz, not a discrete tap.</summary>
        void Vibrate (TimeSpan duration);
    }
}
