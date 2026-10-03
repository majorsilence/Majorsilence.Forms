using System;
using System.IO;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>
    /// What a platform actually does to read a file shipped inside the app: the Android asset manager, the iOS app bundle, or the
    /// folder beside the executable on desktop. Internal so <see cref="FileSystem"/> is the only public surface; a test injects its
    /// own via <see cref="FileSystem.Backend"/>.
    /// </summary>
    internal interface IFileSystemBackend
    {
        /// <summary>Opens a packaged file for reading, or returns null if there is none. <paramref name="name"/> is already validated.</summary>
        Task<Stream?> OpenAppPackageFileAsync (string name);
    }

    /// <summary>
    /// Reads files that were shipped inside the app (a bundled database, a template, a font) the same way on every platform.
    /// Android keeps them in the APK as assets, iOS in the app bundle, and desktop beside the executable; none of those is a path
    /// <see cref="File.OpenRead(string)"/> can use portably.
    /// </summary>
    public static class FileSystem
    {
        internal static IFileSystemBackend Backend { get; set; } = PlatformFileSystem.Create ();

        /// <summary>
        /// A folder the app may write to that survives updates: the per-user local application data folder on desktop, and the
        /// app's private files folder on Android and iOS. Created if it does not exist.
        /// </summary>
        public static string AppDataDirectory {
            get {
                var path = Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace (path))
                    path = AppContext.BaseDirectory;
                try { Directory.CreateDirectory (path); } catch { /* read-only location: callers see the failure when they write */ }
                return path;
            }
        }

        /// <summary>
        /// Opens the packaged file <paramref name="name"/> (a relative path such as <c>blog.sqlite</c> or <c>data/seed.json</c>, with
        /// <c>/</c> or <c>\</c> separators). The caller disposes the stream. Throws <see cref="FileNotFoundException"/> if the app
        /// does not contain it, and <see cref="ArgumentException"/> for an absolute path or one that climbs out with <c>..</c>.
        /// </summary>
        public static async Task<Stream> OpenAppPackageFileAsync (string name)
        {
            var normalized = Normalize (name);
            return await Backend.OpenAppPackageFileAsync (normalized).ConfigureAwait (false)
                ?? throw new FileNotFoundException ($"The app package does not contain '{name}'.", name);
        }

        /// <summary>Gets whether the app package contains <paramref name="name"/>. Returns false for an invalid name rather than throwing.</summary>
        public static async Task<bool> AppPackageFileExistsAsync (string name)
        {
            string normalized;
            try { normalized = Normalize (name); } catch (ArgumentException) { return false; }

            var stream = await Backend.OpenAppPackageFileAsync (normalized).ConfigureAwait (false);
            if (stream is null)
                return false;
            await stream.DisposeAsync ().ConfigureAwait (false);
            return true;
        }

        /// <summary>
        /// Copies the packaged file <paramref name="name"/> into <see cref="AppDataDirectory"/> (or <paramref name="directory"/>) so
        /// code that needs a real path -- SQLite, say -- can open it. Skips the copy when the existing file is already identical in
        /// length and, otherwise, replaces it. Returns the destination path.
        /// </summary>
        public static async Task<string> CopyAppPackageFileAsync (string name, string? directory = null)
        {
            var target = Path.Combine (directory ?? AppDataDirectory, Path.GetFileName (Normalize (name)));
            Directory.CreateDirectory (Path.GetDirectoryName (target)!);

            await using var source = await OpenAppPackageFileAsync (name).ConfigureAwait (false);
            if (source.CanSeek && File.Exists (target) && new FileInfo (target).Length == source.Length)
                return target;

            await using var destination = File.Create (target);
            await source.CopyToAsync (destination).ConfigureAwait (false);
            return target;
        }

        /// <summary>
        /// Validates a packaged-file name and returns it with <c>/</c> separators. Rejects empty names, absolute paths and any
        /// <c>..</c> segment, so a name taken from content can never reach outside the package.
        /// </summary>
        internal static string Normalize (string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace (name);

            var unified = name.Replace ('\\', '/');
            if (unified.StartsWith ('/') || (unified.Length > 1 && unified[1] == ':'))
                throw new ArgumentException ($"'{name}' must be a relative path inside the app package.", nameof (name));

            var segments = unified.Split ('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || Array.IndexOf (segments, "..") >= 0)
                throw new ArgumentException ($"'{name}' must not be empty or climb out of the app package with '..'.", nameof (name));

            return string.Join ('/', segments);
        }
    }
}
