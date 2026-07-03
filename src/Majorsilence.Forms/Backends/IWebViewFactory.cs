namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend that can create a native web-rendering
    /// control (a real browser engine — WebView2 on Windows, WKWebView on macOS, WebKitGTK/WPE WebKit on
    /// Linux). Discovered via <c>Platform.Backend as IWebViewFactory</c> — the same optional-capability
    /// pattern <see cref="INativeControlHostBackend"/> uses for hosting native controls in general. A
    /// backend that doesn't support a webview simply doesn't implement this interface (or reports
    /// <see cref="IsSupported"/> as <c>false</c>), and callers fall back to non-webview behavior.
    /// </summary>
    public interface IWebViewFactory
    {
        /// <summary>
        /// Gets whether a webview engine is available at runtime (e.g. the WebView2 runtime is
        /// installed on Windows, or the required native libraries are present on Linux). This is a
        /// cheap, cached probe — safe to call from hot paths such as control construction. Never
        /// throws.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Creates a new native webview handle. Returns <c>null</c> if the engine failed to initialize
        /// (missing runtime, unsupported OS, etc.) — never throws. Callers should treat a <c>null</c>
        /// result the same as <see cref="IsSupported"/> being <c>false</c> and fall back accordingly.
        /// </summary>
        IWebViewHandle? CreateWebView ();
    }

    /// <summary>
    /// A handle to a native webview control, wrapping whatever the underlying UI toolkit provides (e.g.
    /// Avalonia's <c>NativeWebView</c>). All members are safe to call from any thread — implementations
    /// marshal to the UI thread internally.
    /// </summary>
    public interface IWebViewHandle : IDisposable
    {
        /// <summary>
        /// Gets the underlying native toolkit control (an Avalonia <c>Control</c>, etc.) to assign to
        /// <see cref="Majorsilence.Forms.NativeControlHost.NativeControl"/>.
        /// </summary>
        object NativeControl { get; }

        /// <summary>Navigates to the given URL (including <c>file:///</c> and <c>about:blank</c>).</summary>
        void Navigate (Uri url);

        /// <summary>
        /// Loads the given HTML string directly, without a network or file navigation. Equivalent to
        /// WebView2's <c>NavigateToString</c>.
        /// </summary>
        void NavigateToString (string html);

        /// <summary>
        /// Executes the given JavaScript and returns its result as a string (or <c>null</c>/the literal
        /// text <c>"null"</c> for scripts with no return value, depending on the engine). Never blocks
        /// the UI thread — awaited asynchronously.
        /// </summary>
        Task<string?> ExecuteScriptAsync (string script);

        /// <summary>Raised when a navigation completes, successfully or not.</summary>
        event EventHandler<WebViewNavigationCompletedEventArgs>? NavigationCompleted;

        /// <summary>Raised when the hosted page posts a message to the host via its script bridge.</summary>
        event EventHandler<WebViewMessageEventArgs>? WebMessageReceived;
    }

    /// <summary>Provides data for <see cref="IWebViewHandle.NavigationCompleted"/>.</summary>
    public class WebViewNavigationCompletedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of <see cref="WebViewNavigationCompletedEventArgs"/>.</summary>
        public WebViewNavigationCompletedEventArgs (Uri? url, bool isSuccess)
        {
            Url = url;
            IsSuccess = isSuccess;
        }

        /// <summary>Gets the URL that was navigated to (may be <c>null</c> for <c>NavigateToString</c> loads).</summary>
        public Uri? Url { get; }

        /// <summary>Gets whether the navigation completed successfully.</summary>
        public bool IsSuccess { get; }
    }

    /// <summary>Provides data for <see cref="IWebViewHandle.WebMessageReceived"/>.</summary>
    public class WebViewMessageEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of <see cref="WebViewMessageEventArgs"/>.</summary>
        public WebViewMessageEventArgs (string body) => Body = body;

        /// <summary>Gets the message body posted by the page's script bridge.</summary>
        public string Body { get; }
    }
}
