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

                var files = await owner.Backend.ShowOpenFileDialog (request);

                if (files.Length == 0) {
                    filenames.Clear ();
                    return DialogResult.Cancel;
                }

                // A FileOk handler that cancels keeps the dialog open: show it again (SVC-23).
                if (AcceptResult (files, mustExist: true))
                    return DialogResult.OK;
            }
        }
    }
}
