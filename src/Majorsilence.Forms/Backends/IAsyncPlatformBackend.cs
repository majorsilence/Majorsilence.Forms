namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Opt-in capability for backends whose bootstrap is asynchronous and host-driven — currently only
    /// the Avalonia browser/WebAssembly backend, which must finish attaching to the page before any
    /// window can be created and never blocks on a synchronous main loop afterwards (the browser's own
    /// JS event loop drives the UI from then on). Backends that only support the classic synchronous
    /// <see cref="IPlatformBackend.Initialize"/> + <see cref="IPlatformBackend.RunMainLoop"/> path do not
    /// implement this; see <see cref="Majorsilence.Forms.Application.RunBrowserAsync"/>.
    /// </summary>
    public interface IAsyncPlatformBackend
    {
        /// <summary>
        /// Performs one-time async platform initialization, attaching the UI to the host element
        /// identified by <paramref name="hostElementId"/> (e.g. a browser div id). Safe to call
        /// repeatedly (idempotent).
        /// </summary>
        Task InitializeAsync (string hostElementId);
    }
}
