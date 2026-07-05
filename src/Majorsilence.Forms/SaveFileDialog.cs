using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
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
        public override void Reset ()
        {
            base.Reset ();
            DefaultExtension = null;
        }

        /// <inheritdoc/>
        public override async Task<DialogResult> ShowDialogAsync (Form owner)
        {
            var request = new SaveFileRequest {
                DefaultExtension = DefaultExtension ?? DefaultExt,
                InitialDirectory = GetInitialDirectory (),
                SuggestedFileName = FileName,
                Title = Title,
                Filters = filters
            };

            var file = await owner.Backend.ShowSaveFileDialog (request);

            filenames.Clear ();

            if (file is not null)
                filenames.Add (file);

            return filenames.Count > 0 ? DialogResult.OK : DialogResult.Cancel;
        }
    }
}
