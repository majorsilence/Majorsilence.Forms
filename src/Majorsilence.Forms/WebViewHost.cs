using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Internal <see cref="NativeControlHost"/> specialization that owns an <see cref="IWebViewHandle"/>
    /// for its whole lifetime: creates the handle in the constructor (via <see cref="WebViewSupport"/>),
    /// assigns it to <see cref="NativeControlHost.NativeControl"/>, and disposes it cleanly.
    ///
    /// This type is a composition building block, not a base class for public controls — consumers
    /// (e.g. <see cref="WebBrowser"/>, and later the Telerik <c>RadPdfViewer</c> / <c>RadRichTextEditor</c>
    /// compat controls) should hold a <see cref="WebViewHost"/> as a <c>Dock = DockStyle.Fill</c> child
    /// control, never inherit from it. Composition keeps the "no webview available" fallback simple: a
    /// sibling control (e.g. a plain label, or a WinForms-shaped stub surface) can be shown/hidden next
    /// to the host instead of the whole control hierarchy having to fork on <see cref="IsFunctional"/>.
    /// </summary>
    internal sealed class WebViewHost : NativeControlHost
    {
        private readonly IWebViewHandle? _handle;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebViewHost"/> class, creating the native webview
        /// handle immediately (via <see cref="WebViewSupport.TryCreate"/>). <see cref="IsFunctional"/>
        /// reflects whether that creation succeeded.
        /// </summary>
        public WebViewHost ()
        {
            _handle = WebViewSupport.TryCreate ();
            if (_handle is not null)
                NativeControl = _handle.NativeControl;
        }

        /// <summary>
        /// Gets whether this host has a live webview handle. When <c>false</c> (no platform support, or
        /// engine initialization failed), this control renders as an empty transparent placeholder —
        /// consumers should show their own fallback UI alongside it.
        /// </summary>
        public bool IsFunctional => _handle is not null;

        /// <summary>Gets the underlying webview handle, or <c>null</c> if <see cref="IsFunctional"/> is <c>false</c>.</summary>
        public IWebViewHandle? WebViewHandle => _handle;

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (!_disposed) {
                _disposed = true;
                if (disposing)
                    _handle?.Dispose ();
            }
            base.Dispose (disposing);
        }
    }

    /// <summary>
    /// Static helpers for webview availability and policy, shared by <see cref="WebViewHost"/> and the
    /// Telerik webview-backed compat controls (<c>RadPdfViewer</c>, <c>RadRichTextEditor</c>).
    /// </summary>
    internal static class WebViewSupport
    {
        /// <summary>
        /// AppContext switch name controlling <see cref="AllowSystemViewerFallback"/>. Set via
        /// <c>AppContext.SetSwitch ("Majorsilence.Forms.WebView.AllowSystemViewerFallback", true)</c>
        /// before the fallback is consulted (e.g. in application startup) to opt back in on a backend
        /// where it defaults to <c>false</c>.
        /// </summary>
        public const string AllowSystemViewerFallbackSwitchName = "Majorsilence.Forms.WebView.AllowSystemViewerFallback";

        /// <summary>
        /// Gets whether a webview can currently be created (<see cref="Platform.Backend"/> implements
        /// <see cref="IWebViewFactory"/> and reports <see cref="IWebViewFactory.IsSupported"/>). Cheap
        /// and safe to call repeatedly — never throws.
        /// </summary>
        public static bool IsAvailable {
            get {
                try {
                    return Platform.Backend is IWebViewFactory factory && factory.IsSupported;
                } catch {
                    return false;
                }
            }
        }

        /// <summary>
        /// Attempts to create a new webview handle via the active platform backend. Returns <c>null</c>
        /// if the backend doesn't implement <see cref="IWebViewFactory"/>, reports unsupported, or
        /// creation fails for any reason — never throws.
        /// </summary>
        public static IWebViewHandle? TryCreate ()
        {
            try {
                return Platform.Backend is IWebViewFactory factory && factory.IsSupported
                    ? factory.CreateWebView ()
                    : null;
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Gets whether webview-less compat controls (e.g. <c>RadPdfViewer</c>) may fall back to
        /// spawning the OS's default viewer via <c>Process.Start</c> for content they can't render
        /// inline. Defaults to <c>false</c> on the Headless backend (so automated/CI runs never spawn
        /// external viewer processes); defaults to <c>true</c> elsewhere. Override in either direction
        /// with the <c>Majorsilence.Forms.WebView.AllowSystemViewerFallback</c> AppContext switch — see
        /// <see cref="AllowSystemViewerFallbackSwitchName"/>.
        /// </summary>
        public static bool AllowSystemViewerFallback {
            get {
                if (AppContext.TryGetSwitch (AllowSystemViewerFallbackSwitchName, out var isEnabled))
                    return isEnabled;

                // No explicit override: default false on Headless (Name == "Headless") so CI/tests never
                // spawn OS viewer processes; default true on every other backend (Avalonia, Uno, ...).
                return Platform.Backend.Name != "Headless";
            }
        }
    }
}
