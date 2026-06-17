using Avalonia.Platform.Storage;

namespace Modern.Forms
{
    internal static class WindowKitExtensions
    {
        public static string? GetFullPath (this IStorageFile file)
            => file.Path?.LocalPath;

        public static string? GetFullPath (this IStorageFolder folder)
            => folder.Path?.LocalPath;
    }
}
