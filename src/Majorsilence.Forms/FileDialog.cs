using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a base class for file dialogs.
    /// </summary>
    public abstract partial class FileDialog : FileSystemDialog
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

        /// <summary>Gets or sets the index of the selected filter (1-based).</summary>
        /// <remarks>The filter the dialog opens on, and afterwards the one the chosen file matches (SVC-24).</remarks>
        public int FilterIndex { get; set; } = 1;

        /// <summary>Gets or sets the default extension added to file names without an extension.</summary>
        public string DefaultExt { get; set; } = string.Empty;

        /// <summary>Gets or sets whether a file name with no extension gets the selected filter's (or <see cref="DefaultExt"/>).</summary>
        public bool AddExtension { get; set; } = true;

        /// <summary>Gets or sets whether shortcuts should be dereferenced. Stub in Majorsilence.Forms.</summary>
        public bool DereferenceLinks { get; set; } = true;

        /// <summary>Gets or sets a help button. Stub in Majorsilence.Forms.</summary>
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

        /// <summary>Gets or sets whether the dialog validates file names. Stub in Majorsilence.Forms.</summary>
        public bool ValidateNames { get; set; } = true;

        /// <summary>Resets properties to default values.</summary>
        public virtual void Reset ()
        {
            filters.Clear ();
            _filter = string.Empty;
            FilterIndex = 1;
            DefaultExt = string.Empty;
            filenames.Clear ();
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
        /// Gets or sets the selected file: the first one when several are selected, and
        /// <see cref="string.Empty"/> when there is none.
        /// </summary>
        /// <remarks>
        /// As upstream: stored verbatim and empty when unset (SVC-22). It was run through
        /// <c>Path.GetFullPath</c>, so the designer's <c>FileName = ""</c> threw, a relative name was resolved
        /// against the process's working directory, and a cancelled dialog's <c>FileName.Length</c> threw a
        /// <see cref="NullReferenceException"/>. The pickers return full paths themselves.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public string FileName {
            get => filenames.Count > 0 ? filenames[0] : string.Empty;
            set {
                filenames.Clear ();

                if (!string.IsNullOrEmpty (value))
                    filenames.Add (value);
            }
        }

        // ── After the picker returns (SVC-23, SVC-24) ──────────────────────────────────────────────
        //
        // Upstream's ProcessFileNames and the FileOk hook: give an extensionless name the extension the
        // chosen filter (or DefaultExt) implies, report which filter that was in FilterIndex, then let a
        // FileOk handler veto the result -- a veto keeps the dialog open, so it is shown again.

        // Takes what the picker returned. False when a FileOk handler cancelled.
        private protected bool AcceptResult (IReadOnlyList<string> picked, bool mustExist)
        {
            filenames.Clear ();

            foreach (var file in picked)
                filenames.Add (WithExtension (file, mustExist));

            InferFilterIndex ();

            var e = new System.ComponentModel.CancelEventArgs ();
            OnFileOk (e);
            return !e.Cancel;
        }

        private string WithExtension (string file, bool mustExist)
        {
            if (!AddExtension || Path.HasExtension (file))
                return file;

            foreach (var extension in CandidateExtensions ()) {
                var candidate = file + "." + extension;

                // An open dialog takes the extension only for a file that is really there, as upstream:
                // the user may have picked a file that has no extension on purpose.
                if (!mustExist || File.Exists (candidate) && !File.Exists (file))
                    return candidate;
            }

            return file;
        }

        // The selected filter's concrete extensions, then DefaultExt.
        private IEnumerable<string> CandidateExtensions ()
        {
            if (FilterIndex >= 1 && FilterIndex <= filters.Count)
                foreach (var pattern in filters[FilterIndex - 1].Patterns)
                    if (ConcreteExtension (pattern) is { } extension)
                        yield return extension;

            if (!string.IsNullOrEmpty (DefaultExt))
                yield return DefaultExt.TrimStart ('.');
        }

        // "*.png" -> "png"; "*.*", "*" and anything else with a wildcard in the extension -> null.
        private static string? ConcreteExtension (string pattern)
        {
            var dot = pattern.LastIndexOf ('.');
            if (dot < 0 || dot == pattern.Length - 1)
                return null;

            var extension = pattern.Substring (dot + 1);
            return extension.Contains ('*') || extension.Contains ('?') ? null : extension;
        }

        // The pickers report only the file, not the filter it was picked under, so the filter is read back
        // from the file's extension: export code that branches on FilterIndex ("1 = PNG, 2 = JPEG") then
        // sees the format the user chose. The filter it started on is kept when it matches.
        private void InferFilterIndex ()
        {
            if (filenames.Count == 0 || Path.GetExtension (filenames[0]) is not { Length: > 1 } dotted)
                return;

            var extension = dotted.Substring (1);

            bool Matches (int index) => filters[index].Patterns.Any (p =>
                string.Equals (ConcreteExtension (p), extension, StringComparison.OrdinalIgnoreCase));

            if (FilterIndex >= 1 && FilterIndex <= filters.Count && Matches (FilterIndex - 1))
                return;

            for (var i = 0; i < filters.Count; i++)
                if (Matches (i)) {
                    FilterIndex = i + 1;
                    return;
                }
        }

        // Mutable backing store for the selected files; dialog implementations write here.
        private protected readonly List<string> filenames = [];

        /// <summary>
        /// Gets the selected files. Returned as an array to match System.Windows.Forms.FileDialog.FileNames.
        /// </summary>
        public string[] FileNames => filenames.ToArray ();

        /// <summary>Shows the dialog synchronously, owned by the innermost currently-shown modal
        /// dialog if one is open, otherwise the first open form. WinForms compatibility.</summary>
        public DialogResult ShowDialog ()
        {
            // Refused even with no owner to show against (which answers Cancel): on a backend that
            // cannot block, a Cancel here would read as the user dismissing a picker that never opened.
            BlockingModal.ThrowIfUnsupported ("FileDialog.ShowDialog", "FileDialog.ShowDialogAsync");

            var owner = Application.ActiveModalForm ?? Application.ModalOwnerCandidates.FirstOrDefault ();
            return owner is not null ? ShowDialogSync (owner) : DialogResult.Cancel;
        }

        /// <summary>Shows the dialog synchronously with the specified owner. WinForms compatibility.
        /// Previously discarded the owner argument and fell through to <see cref="ShowDialog()"/>.</summary>
        public DialogResult ShowDialog (IWin32Window owner)
        {
            BlockingModal.ThrowIfUnsupported ("FileDialog.ShowDialog", "FileDialog.ShowDialogAsync");

            var form = owner as Form ?? Application.ActiveModalForm ?? Application.ModalOwnerCandidates.FirstOrDefault ();
            return form is not null ? ShowDialogSync (form) : DialogResult.Cancel;
        }

        /// <summary>Shows the dialog asynchronously with the specified owner form.</summary>
        public abstract Task<DialogResult> ShowDialogAsync (Form owner);

        /// <summary>Shows the dialog without blocking the caller, owned as <see cref="ShowDialog()"/> would
        /// own it; with no open form to show against it answers Cancel, as that does.</summary>
        public Task<DialogResult> ShowDialogAsync ()
        {
            var owner = Application.ActiveModalForm ?? Application.ModalOwnerCandidates.FirstOrDefault ();
            return owner is not null ? ShowDialogAsync (owner) : Task.FromResult (DialogResult.Cancel);
        }

        /// <summary>Shows the dialog without blocking the caller, owned by the given window -- or, for a
        /// control, by its form -- as <see cref="ShowDialog(IWin32Window)"/> would own it.</summary>
        public Task<DialogResult> ShowDialogAsync (IWin32Window owner)
        {
            var form = owner as Form ?? (owner as Control)?.FindForm () ?? Application.ActiveModalForm ?? Application.ModalOwnerCandidates.FirstOrDefault ();
            return form is not null ? ShowDialogAsync (form) : Task.FromResult (DialogResult.Cancel);
        }

        /// <summary>Shows the dialog modally with the given owner and blocks until closed.</summary>
        public DialogResult ShowDialog (Form owner) => ShowDialogSync (owner);

        /// <summary>Shows the dialog synchronously with the specified form owner.</summary>
        /// <remarks>
        /// The task is started here on the calling (UI) thread and then waited on by pumping a nested
        /// message loop, exactly as <see cref="Form.ShowDialog(Form)"/> does. It must not be run through
        /// a blocking sync-over-async wait: the backend's file picker has to be driven from the UI
        /// thread and completes on it, so blocking that thread hangs the application with no dialog
        /// ever on screen.
        /// </remarks>
        public DialogResult ShowDialogSync (Form owner)
        {
            BlockingModal.ThrowIfUnsupported ("FileDialog.ShowDialog", "FileDialog.ShowDialogAsync");

            var result = Form.RunModal (ShowDialogAsync (owner));

            if (result != DialogResult.OK)
                return result;

            // Upstream raises FileOk before the dialog closes and a cancelling handler keeps it open. The
            // native dialog has closed by now, so a cancel turns the answer into Cancel instead (W6.1).
            var ok = new System.ComponentModel.CancelEventArgs ();
            OnFileOk (ok);

            return ok.Cancel ? DialogResult.Cancel : DialogResult.OK;
        }
    }
}
