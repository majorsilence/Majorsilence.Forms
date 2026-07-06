using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a dialog for choosing a file system directory.
    /// </summary>
    public class FolderBrowserDialog : FileSystemDialog
    {
        private string selected_path = string.Empty;
        private string description = string.Empty;

        /// <summary>
        /// Gets or sets the selected folder path. Setting null coerces to an empty string,
        /// matching WinForms semantics (the getter never returns null).
        /// </summary>
        public string SelectedPath {
            get => selected_path;
            set => selected_path = value ?? string.Empty;
        }

        /// <summary>Gets or sets the descriptive text above the tree view. Stub in Majorsilence.Forms (used as window title). Setting null coerces to an empty string.</summary>
        public string Description {
            get => description;
            set => description = value ?? string.Empty;
        }

        /// <summary>Gets or sets whether a New Folder button is shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowNewFolderButton { get; set; } = true;

        /// <summary>Gets or sets whether the description is used as the dialog title. Stub in Majorsilence.Forms.</summary>
        public bool UseDescriptionForTitle { get; set; }

        /// <summary>Gets or sets the root folder at which to start browsing. Stub in Majorsilence.Forms.</summary>
        public Environment.SpecialFolder RootFolder { get; set; } = Environment.SpecialFolder.Desktop;

        /// <summary>Shows the dialog synchronously (blocking call).</summary>
        public DialogResult ShowDialog () => AsyncHelper.RunSync (() => ShowDialogAsync (Application.OpenForms.LastOrDefault ()!));

        /// <summary>Shows the dialog with an IWin32Window owner. Synchronous wrapper.</summary>
        public DialogResult ShowDialog (IWin32Window owner) => ShowDialog ();

        /// <summary>Shows the dialog synchronously with the specified owner, matching System.Windows.Forms semantics.</summary>
        public DialogResult ShowDialog (Form owner) => AsyncHelper.RunSync (() => ShowDialogAsync (owner));

        /// <summary>
        /// Shows the dialog to the user without blocking the caller.
        /// </summary>
        public async Task<DialogResult> ShowDialogAsync (Form owner)
        {
            var request = new FolderDialogRequest {
                InitialDirectory = GetInitialDirectory (),
                Title = Title
            };

            var result = await owner.Backend.ShowOpenFolderDialog (request);

            SelectedPath = result ?? string.Empty;

            return result is null ? DialogResult.Cancel : DialogResult.OK;
        }

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
