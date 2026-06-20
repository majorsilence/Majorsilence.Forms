using Continuum.Forms.Backends;

namespace Continuum.Forms
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
        public override async Task<DialogResult> ShowDialog (Form owner)
        {
            var request = new SaveFileRequest {
                DefaultExtension = DefaultExtension ?? DefaultExt,
                InitialDirectory = GetInitialDirectory (),
                SuggestedFileName = FileName,
                Title = Title,
                Filters = filters
            };

            var file = await owner.Backend.ShowSaveFileDialog (request);

            FileNames.Clear ();

            if (file is not null)
                FileNames.Add (file);

            return FileNames.Count > 0 ? DialogResult.OK : DialogResult.Cancel;
        }
    }
}
