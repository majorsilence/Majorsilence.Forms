using Avalonia.Platform.Storage;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a dialog for choosing a file system directory.
    /// </summary>
    public class FolderBrowserDialog : FileSystemDialog
    {
        /// <summary>
        /// Gets or sets the selected folder path.
        /// </summary>
        public string? SelectedPath { get; set; }

        /// <summary>Gets or sets the descriptive text above the tree view. Stub in Modern.Forms (used as window title).</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets whether a New Folder button is shown. Stub in Modern.Forms.</summary>
        public bool ShowNewFolderButton { get; set; } = true;

        /// <summary>Gets or sets whether the description is used as the dialog title. Stub in Modern.Forms.</summary>
        public bool UseDescriptionForTitle { get; set; }

        /// <summary>Gets or sets the root folder at which to start browsing. Stub in Modern.Forms.</summary>
        public Environment.SpecialFolder RootFolder { get; set; } = Environment.SpecialFolder.Desktop;

        /// <summary>Shows the dialog synchronously (blocking call).</summary>
        public DialogResult ShowDialog () => AsyncHelper.RunSync (() => ShowDialog (Application.OpenForms.LastOrDefault ()!));

        /// <summary>Shows the dialog with an IWin32Window owner. Synchronous wrapper.</summary>
        public DialogResult ShowDialog (IWin32Window owner) => ShowDialog ();

        /// <summary>
        /// Shows the dialog to the user.
        /// </summary>
        public async Task<DialogResult> ShowDialog (Form owner)
        {
            var parent = owner.AvWindow.StorageProvider;

            IStorageFolder? startLocation = null;
            var initPath = GetInitialDirectory ();
            if (initPath is not null)
                startLocation = await parent.TryGetFolderFromPathAsync (new Uri (initPath));

            var options = new FolderPickerOpenOptions {
                AllowMultiple = false,
                SuggestedStartLocation = startLocation,
                Title = Title
            };

            var result = await parent.OpenFolderPickerAsync (options);

            var paths = result.Select (f => f.GetFullPath ()).WhereNotNull ();

            SelectedPath = paths?.FirstOrDefault ();

            return SelectedPath is null ? DialogResult.Cancel : DialogResult.OK;
        }
    }
}
