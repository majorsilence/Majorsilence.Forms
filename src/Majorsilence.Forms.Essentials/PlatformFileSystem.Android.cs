using System.IO;
using System.Threading.Tasks;
using Android.App;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Android reads from the APK's assets.</summary>
    internal static class PlatformFileSystem
    {
        public static IFileSystemBackend Create () => new AndroidFileSystemBackend ();
    }

    /// <summary>
    /// <c>AssetManager.Open</c>. An asset stream is not seekable, so it is copied into memory first: callers get a stream that behaves
    /// like the desktop one (seekable, with a known length), at the cost of holding the file in memory while it is open.
    /// </summary>
    internal sealed class AndroidFileSystemBackend : IFileSystemBackend
    {
        /// <inheritdoc />
        public async Task<Stream?> OpenAppPackageFileAsync (string name)
        {
            try {
                var assets = Application.Context.Assets;
                if (assets is null)
                    return null;

                using var asset = assets.Open (name);
                var buffer = new MemoryStream ();
                await asset.CopyToAsync (buffer).ConfigureAwait (false);
                buffer.Position = 0;
                return buffer;
            } catch (Java.IO.FileNotFoundException) {
                return null;
            } catch (IOException) {
                return null;
            }
        }
    }
}
