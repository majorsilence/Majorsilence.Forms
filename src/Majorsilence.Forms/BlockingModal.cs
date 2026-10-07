using System;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The one check every blocking modal entry point makes before it shows anything (issue #406).
    /// </summary>
    /// <remarks>
    /// It runs before the dialog is shown, not inside <see cref="IPlatformBackend.RunModalLoop"/>, because
    /// by the time the loop is entered the dialog is already on screen, pushed on
    /// <see cref="Application.ModalStack"/> and its owner disabled; throwing from there would leave all of
    /// that behind with nothing left to close it.
    /// </remarks>
    internal static class BlockingModal
    {
        /// <summary>
        /// Whether the active backend can block a caller in a nested modal loop. Resolving the default
        /// backend here is no extra cost: the modal call about to be made resolves it anyway.
        /// </summary>
        internal static bool IsSupported => Platform.Backend is not IModalLoopSupport { CanRunModalLoop: false };

        /// <summary>
        /// Throws <see cref="PlatformNotSupportedException"/> naming <paramref name="asyncAlternative"/>
        /// when the active backend cannot run a blocking modal loop.
        /// </summary>
        /// <param name="api">The blocking call, as the caller wrote it (<c>Form.ShowDialog</c>).</param>
        /// <param name="asyncAlternative">The call to make instead (<c>Form.ShowDialogAsync</c>).</param>
        internal static void ThrowIfUnsupported (string api, string asyncAlternative)
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException (Message (api, asyncAlternative));
        }

        internal static string Message (string api, string asyncAlternative) =>
            $"{api} blocks until the dialog is closed, and the {Platform.Backend.Name} backend on this platform cannot block: " +
            "in the browser, .NET runs on the page's only thread, so a call that does not return also stops the events " +
            $"that would close the dialog. Use {asyncAlternative} and await it instead. " +
            "See \"Browser threading\" in docs/backends.md.";
    }
}
