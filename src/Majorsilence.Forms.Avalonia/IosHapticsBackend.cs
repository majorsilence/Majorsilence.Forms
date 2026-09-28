#if IOS
using System;
using AudioToolbox;
using UIKit;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// iOS's haptics path: <see cref="UISelectionFeedbackGenerator"/> and
    /// <see cref="UIImpactFeedbackGenerator"/> (UIKit, iOS 10+, well within this project's deployment
    /// target) for the two discrete gestures. Neither generator's <c>Prepare</c> is called first -- these
    /// are occasional alert-app cues, not a rapid game-input stream, so the small latency win is not worth
    /// holding a generator (and its brief haptic-engine wake lock) alive between calls.
    /// </summary>
    /// <remarks>
    /// No public UIKit API takes an explicit duration the way Android's <c>VibrationEffect.CreateOneShot</c>
    /// does: <see cref="Vibrate"/> instead triggers the same fixed-length system-wide buzz iOS itself uses
    /// for a phone call or an alert (<c>AudioServicesPlaySystemSound</c>'s well-known, undocumented-but-stable
    /// <c>kSystemSoundID_Vibrate</c> = 4095 -- the same "reach a real OS asset by a known, empirically
    /// stable numeric identifier" precedent <see cref="IosAudioBackend"/>'s own <c>SystemSoundId</c> already
    /// documents), not a caller-chosen length.
    /// </remarks>
    internal sealed class IosHapticsBackend : IHapticsBackend
    {
        private const uint SystemSoundIdVibrate = 4095;

        public void Tap ()
        {
            try {
                using var generator = new UISelectionFeedbackGenerator ();
                generator.SelectionChanged ();
            } catch {
                // no haptic engine on this device, or a platform failure: never worth a crash
            }
        }

        public void Impact ()
        {
            try {
                // UIImpactFeedbackGenerator (UIImpactFeedbackStyle) is obsoleted from iOS 17.5 in favour of
                // UIFeedbackGenerator.GetFeedbackGenerator, a view-scoped factory -- confirmed by a real
                // CI compile failure (CA1422), not assumed. That replacement needs a UIView to scope the
                // generator to, which this backend has no reference to thread through for a feature this
                // minor; the constructor used here still works on every iOS version, only deprecated, not
                // removed, so the warning is suppressed rather than the API avoided.
#pragma warning disable CA1422
                using var generator = new UIImpactFeedbackGenerator (UIImpactFeedbackStyle.Medium);
#pragma warning restore CA1422
                generator.ImpactOccurred ();
            } catch {
            }
        }

        public void Vibrate (TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                return;

            try {
                using var sound = new SystemSound (SystemSoundIdVibrate);
                sound.PlaySystemSound ();
            } catch {
            }
        }
    }
}
#endif
