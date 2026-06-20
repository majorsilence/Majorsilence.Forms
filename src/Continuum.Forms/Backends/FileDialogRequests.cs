using System.Collections.Generic;

namespace Continuum.Forms.Backends
{
    /// <summary>A backend-neutral file-type filter (display name + glob patterns like "*.txt").</summary>
    public sealed class FileDialogFilter
    {
        /// <summary>Initializes a new filter.</summary>
        public FileDialogFilter (string name, IReadOnlyList<string> patterns)
        {
            Name = name;
            Patterns = patterns;
        }

        /// <summary>The filter's display name (e.g. "Text Files").</summary>
        public string Name { get; }

        /// <summary>The glob patterns this filter matches (e.g. "*.txt", "*.log").</summary>
        public IReadOnlyList<string> Patterns { get; }
    }

    /// <summary>A backend-neutral request to open one or more files.</summary>
    public sealed class OpenFileRequest
    {
        /// <summary>The dialog title.</summary>
        public string? Title { get; init; }
        /// <summary>The initial directory (full path), or null.</summary>
        public string? InitialDirectory { get; init; }
        /// <summary>Whether multiple files may be selected.</summary>
        public bool AllowMultiple { get; init; }
        /// <summary>The file-type filters.</summary>
        public IReadOnlyList<FileDialogFilter> Filters { get; init; } = System.Array.Empty<FileDialogFilter> ();
    }

    /// <summary>A backend-neutral request to choose a save-file path.</summary>
    public sealed class SaveFileRequest
    {
        /// <summary>The dialog title.</summary>
        public string? Title { get; init; }
        /// <summary>The initial directory (full path), or null.</summary>
        public string? InitialDirectory { get; init; }
        /// <summary>The suggested file name.</summary>
        public string? SuggestedFileName { get; init; }
        /// <summary>The default extension to append (without leading dot).</summary>
        public string? DefaultExtension { get; init; }
        /// <summary>The file-type filters.</summary>
        public IReadOnlyList<FileDialogFilter> Filters { get; init; } = System.Array.Empty<FileDialogFilter> ();
    }

    /// <summary>A backend-neutral request to choose a folder.</summary>
    public sealed class FolderDialogRequest
    {
        /// <summary>The dialog title.</summary>
        public string? Title { get; init; }
        /// <summary>The initial directory (full path), or null.</summary>
        public string? InitialDirectory { get; init; }
    }
}
