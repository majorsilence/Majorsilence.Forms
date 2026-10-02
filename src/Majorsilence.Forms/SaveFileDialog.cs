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

                var file = await owner.Backend.ShowSaveFileDialog (request);

                if (file is null) {
                    filenames.Clear ();
                    return DialogResult.Cancel;
                }

                // A FileOk handler that cancels keeps the dialog open: show it again (SVC-23).
                if (AcceptResult (new[] { file }, mustExist: false))
                    return DialogResult.OK;
            }
        }
    }
}
