using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a class for a file save dialog.
    /// </summary>
    public partial class SaveFileDialog : FileDialog
    {
        /// <summary>
        /// Gets or sets the default save extension. For example: "txt".
        /// </summary>
        public string? DefaultExtension { get; set; }

        /// <inheritdoc/>
        public override void Reset ()
        {
            base.Reset ();
            DefaultExtension = null;
            saved = null;
        }

        // The chosen file when the platform gives it no local path (Android, iOS, the browser): FileName is then
        // only its name, and it is written through the platform.
        private SavedFile? saved;

        /// <summary>Creates or truncates the chosen file and opens it for writing, wherever the platform keeps it.</summary>
        /// <remarks>
        /// Not in System.Windows.Forms. On Android, iOS and in the browser the chosen file has no path that
        /// <see cref="System.IO.File"/> can create, and <see cref="FileDialog.FileName"/> holds only its name; this
        /// writes it through the platform. On the desktop it creates the file at <see cref="FileDialog.FileName"/>.
        /// </remarks>
        public Task<Stream> OpenFileAsync (CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty (FileName))
                throw new InvalidOperationException ("No file has been chosen.");
            return saved is { } file && file.Name == FileName
                ? file.OpenWriteAsync (cancellationToken)
                : Task.FromResult<Stream> (File.Create (FileName));
        }

        /// <inheritdoc/>
        public override async Task<DialogResult> ShowDialogAsync (Form owner)
        {
            while (true) {
                var request = new SaveFileRequest {
                    DefaultExtension = DefaultExtension ?? DefaultExt,
                    InitialDirectory = GetInitialDirectory (),
                    SuggestedFileName = string.IsNullOrEmpty (FileName) ? null : FileName,
                    Title = Title,
                    FilterIndex = FilterIndex,
                    Filters = filters
                };

                var file = owner.Backend is ISavedFileBackend picker
                    ? await picker.ShowSaveFilePicker (request)
                    : await owner.Backend.ShowSaveFileDialog (request) is { } path ? SavedFile.FromPath (path) : null;

                saved = null;

                if (file is null) {
                    filenames.Clear ();
                    return DialogResult.Cancel;
                }

                // Kept before FileOk runs, so its handler can write it. A FileName set afterwards is a path.
                if (file.Path is null)
                    saved = file;

                // A FileOk handler that cancels keeps the dialog open: show it again (SVC-23).
                if (AcceptResult (new[] { file.Path ?? file.Name }, mustExist: false, _ => file.Path is not null))
                    return DialogResult.OK;
            }
        }
    }
}
