#if IOS
using System;
using Foundation;
using UIKit;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// iOS's reduced-motion signal: <see cref="UIAccessibility.IsReduceMotionEnabled"/>. Like Android, iOS gives a real push
    /// notification for it, so this needs no polling.
    /// </summary>
    internal sealed class IosReducedMotion : IDisposable
    {
        private readonly NSObject token;

        public IosReducedMotion ()
        {
            Current = UIAccessibility.IsReduceMotionEnabled;
            token = UIAccessibility.Notifications.ObserveReduceMotionStatusDidChange ((_, _) => {
                var value = UIAccessibility.IsReduceMotionEnabled;
                if (value == Current)
                    return;

                Current = value;
                Changed?.Invoke (this, EventArgs.Empty);
            });
        }

        public bool Current { get; private set; }

        public event EventHandler? Changed;

        public void Dispose () => token.Dispose ();
    }
}
#endif
