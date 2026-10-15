using Avalonia.Platform.Storage;

namespace Majorsilence.Forms
{
    internal static class StorageExtensions
    {
        // TryGetLocalPath, not Path.LocalPath: an Android content:// URI has a LocalPath ("/document/...")
        // that names nothing on disk, and a browser file has none.
        public static string? GetFullPath (this IStorageFile file)
            => file.TryGetLocalPath ();

        public static string? GetFullPath (this IStorageFolder folder)
            => folder.TryGetLocalPath ();

        // The storage item is kept alive by the closure: on iOS it holds the security-scoped access.
        public static Backends.PickedFile ToPickedFile (this IStorageFile file)
            => new Backends.PickedFile (file.Name, file.GetFullPath (), _ => file.OpenReadAsync ());
    }
}
