using System;
using System.Threading.Tasks;
using Foundation;
using UIKit;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. iOS asks <see cref="UIApplication"/> to open the URL.</summary>
    internal static class PlatformLauncher
    {
        public static ILauncherBackend Create () => new IosLauncherBackend ();
    }

    /// <summary>
    /// <c>UIApplication.OpenUrl</c>, hopped onto the main thread because UIKit requires it. A URL no installed app can open
    /// completes as false.
    /// </summary>
    internal sealed class IosLauncherBackend : ILauncherBackend
    {
        /// <inheritdoc />
        public bool IsSupported => true;

        /// <inheritdoc />
        public Task<bool> OpenAsync (Uri uri)
        {
            var url = NSUrl.FromString (uri.AbsoluteUri);
            if (url is null)
                return Task.FromResult (false);

            var done = new TaskCompletionSource<bool> ();
            UIApplication.SharedApplication.BeginInvokeOnMainThread (() => {
                try {
                    UIApplication.SharedApplication.OpenUrl (url, new UIApplicationOpenUrlOptions (), accepted => done.TrySetResult (accepted));
                } catch {
                    done.TrySetResult (false);
                }
            });
            return done.Task;
        }
    }
}
