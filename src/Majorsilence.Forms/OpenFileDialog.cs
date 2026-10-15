using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a class for a file open dialog.
    /// </summary>
    public partial class OpenFileDialog : FileDialog
    {
        /// <summary>
        /// Gets or sets whether multiple files can be selected.
        /// </summary>
        public bool AllowMultiple { get; set; }

        /// <summary>
        /// Gets or sets whether multiple files can be selected (WinForms alias for AllowMultiple).
        /// </summary>
        public bool Multiselect {
            get => AllowMultiple;
            set => AllowMultiple = value;
        }

        /// <summary>Gets the filename with no path information (just file name and extension).</summary>
        public string SafeFileName => Path.GetFileName (FileName);

        /// <summary>Gets all selected file names with no path information.</summary>
        public string[] SafeFileNames => FileNames.Select (Path.GetFileName).OfType<string> ().ToArray ();

        /// <inheritdoc/>
        public override void Reset ()
        {
            base.Reset ();
            AllowMultiple = false;
            picked.Clear ();
        }

        /// <inheritdoc/>
        public override async Task<DialogResult> ShowDialogAsync (Form owner)
        {
            while (true) {
                var request = new OpenFileRequest {
                    AllowMultiple = AllowMultiple,
                    InitialDirectory = GetInitialDirectory (),
                    Title = Title,
                    FilterIndex = FilterIndex,
                    Filters = filters
                };

                var files = owner.Backend is IPickedFileBackend picker
                    ? await picker.ShowOpenFilePicker (request)
                    : (await owner.Backend.ShowOpenFileDialog (request)).Select (PickedFile.FromPath).ToArray ();

                picked.Clear ();

                if (files.Length == 0) {
                    filenames.Clear ();
                    return DialogResult.Cancel;
                }

                // Kept by the name FileNames reports (AcceptResult leaves a path-less name as it is), before
                // FileOk runs so its handler can read them. A FileName set afterwards is read from disk.
                foreach (var file in files)
                    if (file.Path is null)
                        picked[file.Name] = file;

                // A FileOk handler that cancels keeps the dialog open: show it again (SVC-23).
                if (AcceptResult (files.Select (f => f.Path ?? f.Name).ToArray (), mustExist: true, i => files[i].Path is not null))
                    return DialogResult.OK;
            }
        }

        // The picked files that have no local path, by the name FileNames reports for them.
        private readonly Dictionary<string, PickedFile> picked = new (StringComparer.Ordinal);

        /// <summary>Gets the selected files, each with a way to read it.</summary>
        /// <remarks>
        /// Not in System.Windows.Forms. On Android, iOS and in the browser a picked file has no path that
        /// <see cref="System.IO.File"/> can open, and <see cref="FileDialog.FileNames"/> holds only its name;
        /// read it through <see cref="PickedFile.OpenReadAsync"/> (or <see cref="OpenFileAsync"/>). On the
        /// desktop each entry is the path in <see cref="FileDialog.FileNames"/>.
        /// </remarks>
        public IReadOnlyList<PickedFile> SelectedFiles
            => FileNames.Select (name => picked.TryGetValue (name, out var file) ? file : PickedFile.FromPath (name)).ToArray ();

        /// <summary>Opens the first selected file for reading, on every platform.</summary>
        /// <remarks>Not in System.Windows.Forms; the asynchronous form of <see cref="OpenFile"/>, which is the
        /// only one that can read a browser file.</remarks>
        public Task<Stream> OpenFileAsync (CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty (FileName))
                throw new InvalidOperationException ("No file has been chosen.");

            return SelectedFiles[0].OpenReadAsync (cancellationToken);
        }
    }
}
