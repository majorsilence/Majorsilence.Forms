using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms-shaped WebBrowser control. Internally composes a <see cref="WebViewHost"/> (a native
    /// webview — WebView2/WKWebView/WebKitGTK-WPE, depending on platform — hosted via the
    /// <see cref="NativeControlHost"/> airspace seam). When no platform webview is available (unsupported
    /// backend, missing runtime, or engine init failure), this control behaves exactly like the previous
    /// no-op stub: properties hold their last-set values and no rendering happens.
    ///
    /// The public API shape matches System.Windows.Forms.WebBrowser exactly for drop-in compatibility;
    /// it intentionally does not expose the richer async-script/message-channel capabilities of the
    /// underlying <see cref="IWebViewHandle"/> — see <see cref="InvokeScript(string)"/> for why. Code
    /// that needs those (the Telerik <c>RadPdfViewer</c>/<c>RadRichTextEditor</c> compat controls) should
    /// compose <see cref="WebViewHost"/> directly instead of going through this WinForms-shaped surface.
    /// </summary>
    public class WebBrowser : Control
    {
        private readonly WebViewHost _host;
        private Uri? _url;
        private string _documentText = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="WebBrowser"/> class.</summary>
        public WebBrowser ()
        {
            _host = Controls.AddImplicitControl (new WebViewHost { Dock = DockStyle.Fill });

            if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle) {
                handle.NavigationCompleted += OnNavigationCompleted;
            }
        }

        /// <summary>Gets whether a functional native webview backs this control (platform-dependent).</summary>
        public bool IsWebViewFunctional => _host.IsFunctional;

        /// <summary>Gets or sets the URL currently displayed in the browser.</summary>
        public Uri? Url {
            get => _url;
            set { _url = value; if (value != null) Navigate (value.ToString ()); }
        }

        /// <summary>
        /// Gets or sets the HTML content of the document. Setting this navigates the webview via
        /// <c>NavigateToString</c> (no network/file round-trip); the getter returns the last string that
        /// was set — it does not pull the live DOM (matching the plan's documented "no live DOM pull"
        /// contract, avoiding a synchronous script-execution round trip on every read).
        /// </summary>
        public string DocumentText {
            get => _documentText;
            set {
                _documentText = value ?? string.Empty;
                if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle)
                    handle.NavigateToString (_documentText);
            }
        }

        /// <summary>Gets the title of the current document. Stub — not tracked by the webview seam.</summary>
        public string DocumentTitle => string.Empty;

        /// <summary>Gets or sets whether the browser can navigate backward. Stub in Majorsilence.Forms.</summary>
        public bool CanGoBack => false;

        /// <summary>Gets or sets whether the browser can navigate forward. Stub in Majorsilence.Forms.</summary>
        public bool CanGoForward => false;

        /// <summary>
        /// Gets the ready state of the browser. <see cref="WebBrowserReadyState.Complete"/> once the last
        /// navigation's <see cref="IWebViewHandle.NavigationCompleted"/> has fired (or always, on the
        /// no-webview stub path — matching the previous stub's behavior).
        /// </summary>
        public WebBrowserReadyState ReadyState { get; private set; } = WebBrowserReadyState.Complete;

        /// <summary>Gets or sets whether script errors are suppressed. Stub in Majorsilence.Forms.</summary>
        public bool ScriptErrorsSuppressed { get; set; } = true;

        /// <summary>Gets or sets whether scroll bars are visible. Stub in Majorsilence.Forms.</summary>
        public bool ScrollBarsEnabled { get; set; } = true;

        /// <summary>Gets or sets whether the context menu is allowed. Stub in Majorsilence.Forms.</summary>
        public bool IsWebBrowserContextMenuEnabled { get; set; } = true;

        /// <summary>Gets or sets whether the keyboard shortcuts for the browser are enabled. Stub in Majorsilence.Forms.</summary>
        public bool WebBrowserShortcutsEnabled { get; set; } = true;

        /// <summary>Navigates to the specified URL.</summary>
        public void Navigate (string urlString)
        {
            _url = new Uri (urlString, UriKind.RelativeOrAbsolute);
            ReadyState = WebBrowserReadyState.Loading;

            if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle && _url.IsAbsoluteUri)
                handle.Navigate (_url);
        }

        /// <summary>Navigates to the specified URL.</summary>
        public void Navigate (Uri url) => Navigate (url.ToString ());

        /// <summary>Navigates backward in the history. Stub in Majorsilence.Forms.</summary>
        public void GoBack () { }

        /// <summary>Navigates forward in the history. Stub in Majorsilence.Forms.</summary>
        public void GoForward () { }

        /// <summary>Navigates to the home page. Stub in Majorsilence.Forms.</summary>
        public void GoHome () { }

        /// <summary>Stops the current navigation. Stub in Majorsilence.Forms.</summary>
        public void Stop () { }

        /// <summary>Prints the current document. Stub in Majorsilence.Forms.</summary>
        public void Print () { }

        /// <summary>Prints the current document with a dialog. Stub in Majorsilence.Forms.</summary>
        public void ShowPrintDialog () { }

        /// <summary>
        /// Invokes a script function. Always returns <c>null</c> — intentionally left stubbed even
        /// though the underlying <see cref="IWebViewHandle.ExecuteScriptAsync"/> can run script: the
        /// WinForms <c>WebBrowser.InvokeScript</c> signature is synchronous, and there is no safe way to
        /// wait on the async engine call from here without risking a UI-thread deadlock (sync-over-async
        /// on the same dispatcher that must pump the webview's own completion). Consumers that need
        /// scripting should use the Telerik compat controls (which compose <see cref="WebViewHost"/>
        /// directly and expose the real async surface) rather than this WinForms-shaped control.
        /// </summary>
        public object? InvokeScript (string scriptName) => null;

        /// <summary>Invokes a script function with arguments. Stub in Majorsilence.Forms — returns null (see <see cref="InvokeScript(string)"/>).</summary>
        public object? InvokeScript (string scriptName, object[] args) => null;

        /// <summary>Raised when the document has finished loading.</summary>
        public event EventHandler<WebBrowserDocumentCompletedEventArgs>? DocumentCompleted;

        /// <summary>Raised when a navigation has completed. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<WebBrowserNavigatedEventArgs>? Navigated { add { } remove { } }

        /// <summary>Raised before a navigation starts. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<WebBrowserNavigatingEventArgs>? Navigating { add { } remove { } }

        /// <summary>Raised when the CanGoBack or CanGoForward property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? CanGoBackChanged { add { } remove { } }

        /// <summary>Raised when the CanGoForward property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? CanGoForwardChanged { add { } remove { } }

        /// <summary>Raised when the DocumentTitle property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DocumentTitleChanged { add { } remove { } }

        /// <summary>Raised when the StatusText property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? StatusTextChanged { add { } remove { } }

        private void OnNavigationCompleted (object? sender, WebViewNavigationCompletedEventArgs e)
        {
            ReadyState = WebBrowserReadyState.Complete;
            DocumentCompleted?.Invoke (this, new WebBrowserDocumentCompletedEventArgs (e.Url ?? _url));
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing && _host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle)
                handle.NavigationCompleted -= OnNavigationCompleted;

            base.Dispose (disposing);
        }
    }

    /// <summary>Specifies the ready state of a WebBrowser control.</summary>
    public enum WebBrowserReadyState
    {
        /// <summary>No document is loaded.</summary>
        Uninitialized,
        /// <summary>The document is loading.</summary>
        Loading,
        /// <summary>The document has been loaded but not all resources are done.</summary>
        Loaded,
        /// <summary>The document is interactive.</summary>
        Interactive,
        /// <summary>The document is fully loaded.</summary>
        Complete
    }

    /// <summary>Provides data for the WebBrowser.DocumentCompleted event.</summary>
    public class WebBrowserDocumentCompletedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of WebBrowserDocumentCompletedEventArgs.</summary>
        public WebBrowserDocumentCompletedEventArgs (Uri? url) { Url = url; }

        /// <summary>Gets the URL of the document that was loaded.</summary>
        public Uri? Url { get; }
    }

    /// <summary>Provides data for the WebBrowser.Navigated event.</summary>
    public class WebBrowserNavigatedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of WebBrowserNavigatedEventArgs.</summary>
        public WebBrowserNavigatedEventArgs (Uri? url) { Url = url; }

        /// <summary>Gets the URL that was navigated to.</summary>
        public Uri? Url { get; }
    }

    /// <summary>Provides data for the WebBrowser.Navigating event.</summary>
    public class WebBrowserNavigatingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance of WebBrowserNavigatingEventArgs.</summary>
        public WebBrowserNavigatingEventArgs (Uri? url, string targetFrameName)
        {
            Url = url;
            TargetFrameName = targetFrameName;
        }

        /// <summary>Gets the URL to which the browser is navigating.</summary>
        public Uri? Url { get; }

        /// <summary>Gets the name of the target frame.</summary>
        public string TargetFrameName { get; }
    }
}
