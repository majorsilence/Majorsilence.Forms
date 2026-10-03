using System;
using System.IO;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>Picks the real backend for this target framework. Desktop reads from beside the executable.</summary>
    internal static class PlatformFileSystem
    {
        public static IFileSystemBackend Create () => new DesktopFileSystemBackend ();
    }

    /// <summary>
    /// Files copied to the output folder (a <c>Content</c> item with <c>CopyToOutputDirectory</c>) live under
    /// <see cref="AppContext.BaseDirectory"/>. On the browser the same lookup is best effort: it finds a file only if the build put it in
    /// the WebAssembly virtual file system.
    /// </summary>
    internal sealed class DesktopFileSystemBackend : IFileSystemBackend
    {
        /// <inheritdoc />
        public Task<Stream?> OpenAppPackageFileAsync (string name)
        {
            var path = Path.Combine (AppContext.BaseDirectory, name);
            if (!File.Exists (path))
                return Task.FromResult<Stream?> (null);

            return Task.FromResult<Stream?> (new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous));
        }
    }
}
