using Avalonia.Platform.Storage;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a class for a file save dialog.
    /// </summary>
    public class SaveFileDialog : FileDialog
    {
        /// <summary>
        /// Gets or sets the default save extension. For example: "txt".
        /// </summary>
        public string? DefaultExtension { get; set; }

        /// <inheritdoc/>
        public override async Task<DialogResult> ShowDialog (Form owner)
        {
            var parent = owner.AvWindow.StorageProvider;

            IStorageFolder? startLocation = null;
            var initPath = GetInitialDirectory ();
            if (initPath is not null)
                startLocation = await parent.TryGetFolderFromPathAsync (new Uri (initPath));

            var options = new FilePickerSaveOptions {
                DefaultExtension = DefaultExtension ?? DefaultExt,
                SuggestedStartLocation = startLocation,
                SuggestedFileName = FileName,
                Title = Title,
                FileTypeChoices = filters
            };

            var result = await parent.SaveFilePickerAsync (options);

            FileNames.Clear ();

            var file = result?.GetFullPath ();

            if (file is not null)
                FileNames.Add (file);

            return FileNames.Count > 0 ? DialogResult.OK : DialogResult.Cancel;
        }
    }
}
