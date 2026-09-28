#if ANDROID
using Android.Views;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Android's keep-awake path: <see cref="WindowManagerFlags.KeepScreenOn"/> on the current
    /// <see cref="Android.App.Activity"/>'s own <see cref="Android.Views.Window"/> -- the same
    /// <see cref="AvaloniaPlatformBackend.CurrentAndroidActivity"/> register item F14 already established
    /// for <c>RequestPermission</c>, reused here rather than adding a second "give the framework an
    /// Activity" mechanism.
    /// </summary>
    internal sealed class AndroidKeepAwakeBackend : IKeepScreenAwakeBackend
    {
        private bool enabled;

        public bool KeepScreenAwake {
            get => enabled;
            set {
                if (value == enabled)
                    return;

                var window = AvaloniaPlatformBackend.CurrentAndroidActivity?.Window;
                if (window is null)
                    return;   // never worth a crash: no Activity registered yet (see CurrentAndroidActivity's own remarks)

                if (value)
                    window.AddFlags (WindowManagerFlags.KeepScreenOn);
                else
                    window.ClearFlags (WindowManagerFlags.KeepScreenOn);

                enabled = value;
            }
        }
    }
}
#endif
