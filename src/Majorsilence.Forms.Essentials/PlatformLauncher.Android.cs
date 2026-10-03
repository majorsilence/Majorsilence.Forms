using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Android starts a view <see cref="Intent"/>.</summary>
    internal static class PlatformLauncher
    {
        public static ILauncherBackend Create () => new AndroidLauncherBackend ();
    }

    /// <summary>
    /// <c>ACTION_VIEW</c> with <c>FLAG_ACTIVITY_NEW_TASK</c>, since the application context (not an activity) starts it. An
    /// <see cref="ActivityNotFoundException"/> -- no app installed for the scheme -- is a false result, not a crash.
    /// </summary>
    internal sealed class AndroidLauncherBackend : ILauncherBackend
    {
        /// <inheritdoc />
        public bool IsSupported => true;

        /// <inheritdoc />
        public Task<bool> OpenAsync (Uri uri)
        {
            try {
                var intent = new Intent (Intent.ActionView, Android.Net.Uri.Parse (uri.AbsoluteUri));
                intent.AddFlags (ActivityFlags.NewTask);
                Application.Context.StartActivity (intent);
                return Task.FromResult (true);
            } catch (ActivityNotFoundException) {
                return Task.FromResult (false);
            }
        }
    }
}
