using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog for choosing a file system directory.
    /// </summary>
    public partial class FolderBrowserDialog : FileSystemDialog
    {
        private string selected_path = string.Empty;
        private string description = string.Empty;

        /// <summary>
        /// Gets or sets the selected folder path. Setting null coerces to an empty string,
        /// matching WinForms semantics (the getter never returns null).
        /// </summary>
        /// <remarks>An existing folder set here is where the picker opens when
        /// <see cref="FileSystemDialog.InitialDirectory"/> is not set, as upstream.</remarks>
        public string SelectedPath {
            get => selected_path;
            set => selected_path = value ?? string.Empty;
        }

        /// <summary>Gets or sets the descriptive text shown in the dialog. Setting null coerces to an empty string.</summary>
        /// <remarks>The platform pickers have no prompt line, so the description is shown as the picker's
        /// title -- always with <see cref="UseDescriptionForTitle"/>, and otherwise when
        /// <see cref="FileSystemDialog.Title"/> is empty.</remarks>
        public string Description {
            get => description;
            set => description = value ?? string.Empty;
        }

        /// <summary>Gets or sets whether a New Folder button is shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowNewFolderButton { get; set; } = true;

        /// <summary>Gets or sets whether the description is used as the dialog title, over <see cref="FileSystemDialog.Title"/>.</summary>
        public bool UseDescriptionForTitle { get; set; }

        /// <summary>Gets or sets the root folder at which to start browsing. Stub in Majorsilence.Forms.</summary>
        public Environment.SpecialFolder RootFolder { get; set; } = Environment.SpecialFolder.Desktop;

        /// <summary>Shows the dialog synchronously (blocking call).</summary>
        public DialogResult ShowDialog ()
        {
            // Refused even with no owner, for the reason FileDialog.ShowDialog () gives.
            BlockingModal.ThrowIfUnsupported ("FolderBrowserDialog.ShowDialog", "FolderBrowserDialog.ShowDialogAsync");

            var owner = Application.ModalOwnerCandidates.LastOrDefault ();
            return owner is not null ? ShowDialog (owner) : DialogResult.Cancel;
        }

        /// <summary>Shows the dialog with an IWin32Window owner. Synchronous wrapper.</summary>
        public DialogResult ShowDialog (IWin32Window owner) => ShowDialog ();

        /// <summary>Shows the dialog synchronously with the specified owner, matching System.Windows.Forms semantics.</summary>
        /// <remarks>
        /// Waits by pumping a nested message loop rather than blocking — see
        /// <see cref="FileDialog.ShowDialogSync(Form)"/> for why a blocking wait deadlocks here.
        /// </remarks>
        public DialogResult ShowDialog (Form owner)
        {
            BlockingModal.ThrowIfUnsupported ("FolderBrowserDialog.ShowDialog", "FolderBrowserDialog.ShowDialogAsync");

            return Form.RunModal (ShowDialogAsync (owner));
        }

        /// <summary>Shows the dialog without blocking the caller, owned as <see cref="ShowDialog()"/> would
        /// own it; with no open form to show against it answers Cancel, as that does.</summary>
        public Task<DialogResult> ShowDialogAsync ()
        {
            var owner = Application.ModalOwnerCandidates.LastOrDefault ();
            return owner is not null ? ShowDialogAsync (owner) : Task.FromResult (DialogResult.Cancel);
        }

        /// <summary>Shows the dialog without blocking the caller, owned by the given window -- or, for a
        /// control, by its form.</summary>
        public Task<DialogResult> ShowDialogAsync (IWin32Window owner)
            => (owner as Form ?? (owner as Control)?.FindForm ()) is { } form ? ShowDialogAsync (form) : ShowDialogAsync ();

        /// <summary>
        /// Shows the dialog to the user without blocking the caller.
        /// </summary>
        public async Task<DialogResult> ShowDialogAsync (Form owner)
        {
            var request = BuildRequest ();

            var result = await owner.Backend.ShowOpenFolderDialog (request);

            SelectedPath = result ?? string.Empty;

            return result is null ? DialogResult.Cancel : DialogResult.OK;
        }

        // What the picker is asked to show (SVC-26). Upstream (Dialogs/CommonDialogs/FolderBrowserDialog.cs,
        // SetDialogProperties) puts Description in the dialog -- as its title with UseDescriptionForTitle,
        // otherwise as prompt text above the folder view -- and opens at InitialDirectory, else at the
        // folder SelectedPath names. The platform pickers have a title and a start folder and nothing
        // else, so the description becomes the title whenever no title of our own was set, and the
        // start folder is SelectedPath itself (upstream preselects it inside its parent, which no
        // picker here can express; opening inside it keeps "OK" choosing the same folder).
        internal FolderDialogRequest BuildRequest ()
        {
            var title = UseDescriptionForTitle || Title.Length == 0 ? Description : Title;

            return new FolderDialogRequest {
                InitialDirectory = GetInitialDirectory () ?? ExistingSelectedPath (),
                Title = title.Length == 0 ? Title : title
            };
        }

        private string? ExistingSelectedPath ()
            => selected_path.Length != 0 && Directory.Exists (selected_path) ? Path.GetFullPath (selected_path) : null;

        /// <summary>
        /// Resets the properties of the dialog to their default values.
        /// </summary>
        public void Reset ()
        {
            selected_path = string.Empty;
            description = string.Empty;
            ShowNewFolderButton = true;
            UseDescriptionForTitle = false;
            RootFolder = Environment.SpecialFolder.Desktop;
            InitialDirectory = null;
            Title = string.Empty;
        }
    }
}
