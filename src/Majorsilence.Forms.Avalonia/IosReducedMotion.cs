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
            // The notification is on UIView, not UIAccessibility -- a binding-layout quirk (confirmed against
            // Microsoft's dotnet/macios API docs), not a typo: this exact member did not compile as first written.
            token = UIView.Notifications.ObserveReduceMotionStatusDidChange ((_, _) => {
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
