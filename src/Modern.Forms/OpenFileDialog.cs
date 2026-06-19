using Modern.Forms.Backends;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a class for a file open dialog.
    /// </summary>
    public class OpenFileDialog : FileDialog
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
        public string SafeFileName => FileName is not null ? Path.GetFileName (FileName) : string.Empty;

        /// <summary>Gets all selected file names with no path information.</summary>
        public string[] SafeFileNames => FileNames.Select (Path.GetFileName).OfType<string> ().ToArray ();

        /// <inheritdoc/>
        public override void Reset ()
        {
            base.Reset ();
            AllowMultiple = false;
        }

        /// <inheritdoc/>
        public override async Task<DialogResult> ShowDialog (Form owner)
        {
            var request = new OpenFileRequest {
                AllowMultiple = AllowMultiple,
                InitialDirectory = GetInitialDirectory (),
                Title = Title,
                Filters = filters
            };

            var files = await owner.Backend.ShowOpenFileDialog (request);

            FileNames.Clear ();

            if (files.Length > 0)
                FileNames.AddRange (files);

            return FileNames.Count > 0 ? DialogResult.OK : DialogResult.Cancel;
        }
    }
}
