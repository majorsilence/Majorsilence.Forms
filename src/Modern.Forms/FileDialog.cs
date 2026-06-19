using Modern.Forms.Backends;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a base class for file dialogs.
    /// </summary>
    public abstract class FileDialog : FileSystemDialog
    {
        internal List<FileDialogFilter> filters = [];
        private string _filter = string.Empty;

        /// <summary>
        /// Adds a file filter choice to the dialog.
        /// </summary>
        /// <param name="name">Name of the filter, for example: "Text Files".</param>
        /// <param name="extensions">File extensions to filter for, for example: "*.txt", "*.log".</param>
        public void AddFilter (string name, params string[] extensions)
        {
            filters.Add (new FileDialogFilter (name, new List<string> (extensions)));
        }

        /// <summary>
        /// Gets or sets the filter string in WinForms format: "Name|*.ext|Name2|*.ext2"
        /// Setting this parses and populates the internal filter list.
        /// </summary>
        public string Filter {
            get => _filter;
            set {
                var newValue = value ?? string.Empty;

                if (!string.IsNullOrEmpty (newValue)) {
                    // WinForms semantics: the filter must consist of name/pattern pairs,
                    // so the number of '|'-separated parts must be even (and non-zero).
                    var formatLength = newValue.Split ('|').Length;
                    if (formatLength % 2 != 0)
                        throw new ArgumentException ("The provided filter string is invalid.", nameof (value));
                }

                _filter = newValue;
                filters.Clear ();
                if (string.IsNullOrEmpty (_filter)) return;
                var parts = _filter.Split ('|');
                for (var i = 0; i + 1 < parts.Length; i += 2) {
                    var name = parts[i].Trim ();
                    var patterns = parts[i + 1].Trim ().Split (';')
                        .Select (p => p.Trim ())
                        .ToList ();
                    filters.Add (new FileDialogFilter (name, patterns));
                }
            }
        }

        /// <summary>Gets or sets the index of the currently selected filter (1-based). Stub in Modern.Forms.</summary>
        public int FilterIndex { get; set; } = 1;

        /// <summary>Gets or sets the default extension added to file names without an extension.</summary>
        public string DefaultExt { get; set; } = string.Empty;

        /// <summary>Gets or sets whether to add the extension automatically. Stub in Modern.Forms.</summary>
        public bool AddExtension { get; set; } = true;

        /// <summary>Gets or sets whether shortcuts should be dereferenced. Stub in Modern.Forms.</summary>
        public bool DereferenceLinks { get; set; } = true;

        /// <summary>Gets or sets a help button. Stub in Modern.Forms.</summary>
        public bool ShowHelp { get; set; }

        /// <summary>Gets or sets whether the dialog verifies that the file exists before returning. Stub.</summary>
        public bool CheckFileExists { get; set; } = true;

        /// <summary>Gets or sets whether the dialog verifies that the path exists before returning. Stub.</summary>
        public bool CheckPathExists { get; set; } = true;

        /// <summary>Gets or sets whether the dialog supports read-only files. Stub.</summary>
        public bool ShowReadOnly { get; set; }

        /// <summary>Gets or sets whether the read-only checkbox is initially checked. Stub.</summary>
        public bool ReadOnlyChecked { get; set; }

        /// <summary>Gets or sets whether the dialog box creates a file if the user specifies a nonexistent file. Stub.</summary>
        public bool CreatePrompt { get; set; }

        /// <summary>Gets or sets whether the dialog box prompts the user before overwriting an existing file. Stub.</summary>
        public bool OverwritePrompt { get; set; } = true;

        /// <summary>Gets or sets whether to restore the current directory after closing. Stub.</summary>
        public bool RestoreDirectory { get; set; }

        /// <summary>Gets or sets whether to support network browsing. Stub.</summary>
        public bool SupportMultiDottedExtensions { get; set; }

        /// <summary>Gets or sets whether the dialog validates file names. Stub in Modern.Forms.</summary>
        public bool ValidateNames { get; set; } = true;

        /// <summary>Resets properties to default values.</summary>
        public virtual void Reset ()
        {
            filters.Clear ();
            _filter = string.Empty;
            FilterIndex = 1;
            DefaultExt = string.Empty;
            FileNames.Clear ();
            Title = string.Empty;
            InitialDirectory = null;
            AddExtension = true;
            DereferenceLinks = true;
            ShowHelp = false;
            CheckFileExists = true;
            CheckPathExists = true;
            ShowReadOnly = false;
            ReadOnlyChecked = false;
            CreatePrompt = false;
            OverwritePrompt = true;
            RestoreDirectory = false;
            SupportMultiDottedExtensions = false;
            ValidateNames = true;
        }

        /// <summary>
        /// Gets or sets the selected files. If there are multiple files selected, the first one is returned.
        /// </summary>
        public string? FileName {
            get => FileNames.Count > 0 ? Path.GetFullPath (FileNames[0]) : null;
            set {
                FileNames.Clear ();

                if (value != null)
                    FileNames.Add (Path.GetFullPath (value));
            }
        }

        /// <summary>
        /// Gets or sets the selected files.
        /// </summary>
        public List<string> FileNames { get; } = [];

        /// <summary>Shows the dialog synchronously with the first open form as the owner. WinForms compatibility.</summary>
        public DialogResult ShowDialog ()
        {
            var owner = Application.OpenForms.FirstOrDefault ();
            return owner is not null ? ShowDialogSync (owner) : DialogResult.Cancel;
        }

        /// <summary>Shows the dialog synchronously. WinForms compatibility (calls ShowDialogSync).</summary>
        public DialogResult ShowDialog (IWin32Window owner) => ShowDialog ();

        /// <summary>Shows the dialog asynchronously with the specified owner form.</summary>
        public abstract Task<DialogResult> ShowDialog (Form owner);

        /// <summary>Shows the dialog synchronously with the specified form owner.</summary>
        public DialogResult ShowDialogSync (Form owner) => AsyncHelper.RunSync (() => ShowDialog (owner));
    }
}
