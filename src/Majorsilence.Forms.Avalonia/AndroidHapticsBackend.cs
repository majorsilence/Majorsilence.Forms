#if ANDROID
using System;
using Android.Content;
using Android.OS;

// android.permission.VIBRATE is a normal (not dangerous) permission -- no runtime request needed -- but
// it must still be declared, or Vibrator.Vibrate silently does nothing. An assembly-level attribute on
// this framework assembly merges it into any consuming app's manifest automatically, so a host app needs
// no manifest change of its own for Haptics to work, the same "framework first" reasoning #288's
// AppCompat-theme requirement already documents for a different manifest concern.
[assembly: Android.App.UsesPermission (Android.Manifest.Permission.Vibrate)]

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Android's haptics path: <see cref="Vibrator"/>, driven by <see cref="VibrationEffect"/> (API 26+) so
    /// the OS -- not this code -- picks the actual waveform/amplitude for "click"/"heavy click"; a raw
    /// millisecond buzz for <see cref="Tap"/>/<see cref="Impact"/> would just be two indistinguishable
    /// buzzes. <see cref="VibrationEffect.CreatePredefined"/> needs API 29+; between this project's floor
    /// of API 24 (<c>Gallery.Android.csproj</c>'s <c>SupportedOSPlatformVersion</c>) and that, every member
    /// falls back to a plain one-shot buzz of its own length, still real feedback, just not the platform's
    /// distinct predefined shapes. Below API 26 there is no <see cref="VibrationEffect"/> at all, so every
    /// member is a no-op there -- two Android versions that old are not worth a second, cruder
    /// amplitude-only code path for a feature this minor.
    /// </summary>
    internal sealed class AndroidHapticsBackend : IHapticsBackend
    {
        public void Tap () => PlayEffect (Effect.Click, fallbackMs: 20);

        public void Impact () => PlayEffect (Effect.HeavyClick, fallbackMs: 50);

        private enum Effect { Click, HeavyClick }

        public void Vibrate (TimeSpan duration)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast (26))
                return;

            var ms = (long) Math.Max (0, duration.TotalMilliseconds);
            if (ms <= 0)
                return;

            try {
                var vibrator = GetVibrator ();
                if (vibrator is null || !vibrator.HasVibrator)
                    return;

                vibrator.Vibrate (VibrationEffect.CreateOneShot (ms, VibrationEffect.DefaultAmplitude));
            } catch {
                // no vibrator hardware, missing permission, or an OEM quirk: a haptic cue is never worth a crash
            }
        }

        private static void PlayEffect (Effect kind, long fallbackMs)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast (26))
                return;

            try {
                var vibrator = GetVibrator ();
                if (vibrator is null || !vibrator.HasVibrator)
                    return;

                // The VibrationEffect.EffectClick/EffectHeavyClick field access has to stay inside this
                // version-guarded branch, not just the eventual Vibrator.Vibrate call: CA1416 (Android's
                // platform-compat analyzer) flags the field reference itself as reachable from a lower
                // floor otherwise, even though Tap/Impact never pass a raw effect id below API 29 --
                // confirmed by a real build failure, not assumed.
                VibrationEffect? effect;
                if (OperatingSystem.IsAndroidVersionAtLeast (29)) {
                    var effectId = kind == Effect.Click ? VibrationEffect.EffectClick : VibrationEffect.EffectHeavyClick;
                    effect = VibrationEffect.CreatePredefined (effectId);
                } else {
                    effect = VibrationEffect.CreateOneShot (fallbackMs, VibrationEffect.DefaultAmplitude);
                }

                if (effect is not null)
                    vibrator.Vibrate (effect);
            } catch {
            }
        }

        private static Vibrator? GetVibrator ()
        {
            var context = global::Android.App.Application.Context;
            if (context is null)
                return null;

            if (OperatingSystem.IsAndroidVersionAtLeast (31)) {
                var manager = context.GetSystemService (Context.VibratorManagerService) as VibratorManager;
                return manager?.DefaultVibrator;
            }

            return context.GetSystemService (Context.VibratorService) as Vibrator;
        }
    }
}
#endif
