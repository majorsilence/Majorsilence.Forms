using System.IO;
using System.Threading.Tasks;
using Foundation;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. iOS reads from the app bundle.</summary>
    internal static class PlatformFileSystem
    {
        public static IFileSystemBackend Create () => new IosFileSystemBackend ();
    }

    /// <summary>The app bundle (<see cref="NSBundle.MainBundle"/>), which holds a file marked as a bundle resource at its relative path.</summary>
    internal sealed class IosFileSystemBackend : IFileSystemBackend
    {
        /// <inheritdoc />
        public Task<Stream?> OpenAppPackageFileAsync (string name)
        {
            var path = Path.Combine (NSBundle.MainBundle.BundlePath, name);
            if (!File.Exists (path))
                return Task.FromResult<Stream?> (null);

            return Task.FromResult<Stream?> (new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous));
        }
    }
}
