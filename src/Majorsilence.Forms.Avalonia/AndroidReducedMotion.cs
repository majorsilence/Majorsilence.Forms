#if ANDROID
using System;
using Android.App;
using Android.Database;
using Android.Provider;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Android's reduced-motion signal: the animator duration scale developer setting, at zero when "Remove animations" is on.
    /// Unlike the desktop OSes, Android gives a real push notification for it (a <see cref="ContentObserver"/>), so this needs no
    /// polling.
    /// </summary>
    internal sealed class AndroidReducedMotion : IDisposable
    {
        private readonly Observer observer;

        public AndroidReducedMotion ()
        {
            Current = Read ();
            observer = new Observer (this);
            global::Android.App.Application.Context.ContentResolver!.RegisterContentObserver (
                Settings.Global.GetUriFor (Settings.Global.AnimatorDurationScale)!, false, observer);
        }

        public bool Current { get; private set; }

        public event EventHandler? Changed;

        public void Dispose () => global::Android.App.Application.Context.ContentResolver!.UnregisterContentObserver (observer);

        private static bool Read ()
            => Settings.Global.GetFloat (global::Android.App.Application.Context.ContentResolver, Settings.Global.AnimatorDurationScale, 1f) == 0f;

        private void OnChange ()
        {
            var value = Read ();
            if (value == Current)
                return;

            Current = value;
            Changed?.Invoke (this, EventArgs.Empty);
        }

        private sealed class Observer (AndroidReducedMotion owner) : ContentObserver (null)
        {
            public override void OnChange (bool selfChange) => owner.OnChange ();
        }
    }
}
#endif
