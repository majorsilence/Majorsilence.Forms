using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// A file the user chose in an open-file picker, readable whether or not the platform gives it a path.
    /// </summary>
    /// <remarks>
    /// On the desktop a picked file is a path. On Android it is a <c>content://</c> URI, on iOS a
    /// security-scoped URL, and in the browser a handle with no file system behind it at all: none of those
    /// can be passed to <see cref="File.OpenRead(string)"/>. The backend that made the pick knows how to
    /// read it, so it hands back a way to open it alongside the name.
    /// </remarks>
    public sealed class PickedFile
    {
        private readonly Func<CancellationToken, Task<Stream>> open_read;

        /// <summary>Initializes a picked file.</summary>
        /// <param name="name">The file's name with its extension, as the picker showed it.</param>
        /// <param name="path">The full local path, or null where the platform has none.</param>
        /// <param name="openRead">Opens the file's contents for reading.</param>
        public PickedFile (string name, string? path, Func<CancellationToken, Task<Stream>> openRead)
        {
            Name = name ?? throw new ArgumentNullException (nameof (name));
            Path = path;
            open_read = openRead ?? throw new ArgumentNullException (nameof (openRead));
        }

        /// <summary>A picked file that is a local path.</summary>
        public static PickedFile FromPath (string path)
            => new PickedFile (System.IO.Path.GetFileName (path), path,
                _ => Task.FromResult<Stream> (new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)));

        /// <summary>The file's name with its extension, without any directory.</summary>
        public string Name { get; }

        /// <summary>The full local path, or null when the platform gives the file none (Android, iOS, the browser).</summary>
        public string? Path { get; }

        /// <summary>Opens the file's contents for reading. The caller disposes the stream.</summary>
        public Task<Stream> OpenReadAsync (CancellationToken cancellationToken = default) => open_read (cancellationToken);
    }

    /// <summary>
    /// Implemented by a backend whose open-file picker can hand back files without a local path, so
    /// <c>OpenFileDialog</c> can read them. A backend without it is asked for paths
    /// (<see cref="IWindowBackend.ShowOpenFileDialog"/>) and each is opened from disk.
    /// </summary>
    public interface IPickedFileBackend
    {
        /// <summary>Shows an open-file picker; returns the chosen files (empty if cancelled).</summary>
        Task<PickedFile[]> ShowOpenFilePicker (OpenFileRequest request);
    }
}
