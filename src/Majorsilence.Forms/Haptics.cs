using System;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Real haptic feedback where the platform has a vibration motor to drive (Android, iOS) and a
    /// reported no-op everywhere else (desktop, browser, Headless) -- register item F13. Every member is
    /// fire-and-forget and never throws: a missing backend, no vibration hardware, or a platform failure
    /// all degrade to silently doing nothing, the same "never worth a crash" contract
    /// <see cref="Media.SoundPlayer"/> and <see cref="Media.AudioPlayer"/> already use for their own cues.
    /// </summary>
    public static class Haptics
    {
        /// <summary>
        /// Gets whether the active backend can actually vibrate anything -- true on Android and iOS, false
        /// everywhere else, including Headless (register item F13's own acceptance criterion). Cheap to
        /// call ahead of building a haptics-dependent UX, the same reason <see cref="Media.AudioPlayer.IsSupported"/> exists.
        /// </summary>
        public static bool IsSupported => Backends.Platform.Backend is Backends.IHapticsBackend;

        /// <summary>Triggers a light tap, for a selection changing or a small UI acknowledgement.</summary>
        public static void Tap ()
        {
            if (Backends.Platform.Backend is Backends.IHapticsBackend haptics)
                haptics.Tap ();
        }

        /// <summary>Triggers a firmer, single pulse, for a more significant action succeeding or landing.</summary>
        public static void Impact ()
        {
            if (Backends.Platform.Backend is Backends.IHapticsBackend haptics)
                haptics.Impact ();
        }

        /// <summary>
        /// Vibrates for approximately <paramref name="duration"/> -- an alert-style buzz, not a discrete
        /// tap. A negative duration is clamped to zero (a no-op) rather than rejected.
        /// </summary>
        public static void Vibrate (TimeSpan duration)
        {
            if (Backends.Platform.Backend is Backends.IHapticsBackend haptics)
                haptics.Vibrate (duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
        }
    }
}
