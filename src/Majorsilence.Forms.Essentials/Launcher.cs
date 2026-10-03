using System;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>
    /// What a platform actually does to hand a URI to another app: an Android view <c>Intent</c>, iOS
    /// <c>UIApplication.OpenUrl</c>, or the desktop shell. Internal so <see cref="Launcher"/> is the only public surface; a test
    /// injects its own via <see cref="Launcher.Backend"/>.
    /// </summary>
    internal interface ILauncherBackend
    {
        /// <summary>Whether this platform can hand a URI to another app at all.</summary>
        bool IsSupported { get; }

        /// <summary>Opens an already-validated URI. Returns whether the platform accepted it.</summary>
        Task<bool> OpenAsync (Uri uri);
    }

    /// <summary>
    /// Opens a web page, mail composer or dialer in whatever app the platform picks, from one call that works on Android, iOS and
    /// desktop. <c>Process.Start</c> is not a substitute: it throws or does nothing on Android, iOS and the browser. Like the other
    /// Essentials capabilities, every member degrades to returning false rather than throwing when the platform cannot help.
    /// </summary>
    public static class Launcher
    {
        // The schemes an app can reasonably hand to the OS on a user's behalf. Anything else (file:, javascript:, an app's custom
        // scheme, ms-settings:) is refused rather than forwarded, because the URI is usually content from a document or a feed.
        private static readonly string[] AllowedSchemes = { Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto, "tel", "sms" };

        internal static ILauncherBackend Backend { get; set; } = PlatformLauncher.Create ();

        /// <summary>Gets whether this platform can hand a URI to another app at all.</summary>
        public static bool IsSupported => Backend.IsSupported;

        /// <summary>
        /// Gets whether <paramref name="uri"/> is something <see cref="OpenAsync(Uri)"/> will forward: an absolute <c>http</c>,
        /// <c>https</c>, <c>mailto</c>, <c>tel</c> or <c>sms</c> URI.
        /// </summary>
        public static bool CanOpen (Uri? uri) =>
            uri is { IsAbsoluteUri: true } && Array.IndexOf (AllowedSchemes, uri.Scheme.ToLowerInvariant ()) >= 0;

        /// <summary>
        /// Opens <paramref name="uri"/> in the app the platform chooses. Returns false, without throwing, if the URI is not one
        /// <see cref="CanOpen"/> accepts, the platform cannot launch it, or no app handles it.
        /// </summary>
        public static async Task<bool> OpenAsync (Uri uri)
        {
            ArgumentNullException.ThrowIfNull (uri);
            if (!CanOpen (uri) || !Backend.IsSupported)
                return false;

            try {
                return await Backend.OpenAsync (uri).ConfigureAwait (false);
            } catch {
                // Never worth a crash: a missing browser or a refused intent is the platform saying no.
                return false;
            }
        }

        /// <summary>Opens <paramref name="uri"/> given as text. Returns false if it is not a valid absolute URI.</summary>
        public static Task<bool> OpenAsync (string uri)
        {
            ArgumentNullException.ThrowIfNull (uri);
            return Uri.TryCreate (uri, UriKind.Absolute, out var parsed) ? OpenAsync (parsed) : Task.FromResult (false);
        }
    }
}
