namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional <see cref="IPlatformBackend"/> capability: says whether
    /// <see cref="IPlatformBackend.RunModalLoop"/> can block its caller while the UI keeps running.
    /// </summary>
    /// <remarks>
    /// A backend that does not implement this is assumed to support a nested modal loop, which every
    /// desktop backend does. The browser (WebAssembly) target does not: .NET runs on the page's one
    /// JavaScript thread, and a call that does not return also stops the event loop that would have
    /// delivered the click closing the dialog (issue #406). When this reports false the blocking modal
    /// APIs -- <see cref="Form.ShowDialog()"/>, <see cref="MessageBox"/>'s <c>Show</c>, the file and folder
    /// pickers' <c>ShowDialog</c>, <see cref="TaskDialog.ShowDialog(TaskDialogPage,TaskDialogStartupLocation)"/>
    /// -- throw <see cref="System.PlatformNotSupportedException"/> naming their async alternative before
    /// anything is shown, rather than leaving a dialog on screen with its owner disabled.
    /// </remarks>
    public interface IModalLoopSupport
    {
        /// <summary>
        /// Whether <see cref="IPlatformBackend.RunModalLoop"/> can block the caller until the dialog
        /// closes. False on the browser target.
        /// </summary>
        bool CanRunModalLoop { get; }
    }
}
