namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a base class for file system dialogs.
    /// </summary>
    public abstract class FileSystemDialog : System.IDisposable
    {
        /// <summary>
        /// Releases resources used by the dialog. No-op -- FileSystemDialog holds no unmanaged
        /// resources -- provided purely for `using (var dlg = new OpenFileDialog())` source
        /// compatibility with ported WinForms code.
        /// </summary>
        public void Dispose()
        {
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets or sets the initial directory for the dialog.
        /// </summary>
        public string? InitialDirectory { get; set; }

        /// <summary>
        /// Gets or sets the title for the dialog.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        internal string? GetInitialDirectory ()
        {
            if (InitialDirectory is not null && Directory.Exists (InitialDirectory))
                return Path.GetFullPath (InitialDirectory);

            return null;
        }
    }
}
