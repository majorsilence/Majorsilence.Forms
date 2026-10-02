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
    /// The public API shape matches System.Windows.Forms.WebBrowser for drop-in compatibility, extended
    /// with <see cref="ExecuteScriptAsync"/> and <see cref="WebMessageReceived"/> so callers can drive a
    /// JS/host message bridge (e.g. a page calling <c>window.invokeCSharpAction(...)</c>) without composing
    /// <see cref="WebViewHost"/> directly. <see cref="InvokeScript(string)"/> stays a stub — see its doc
    /// comment for why the synchronous WinForms signature can't safely wrap the async engine call.
    /// </summary>
    public partial class WebBrowser : Control
    {
        private readonly WebViewHost _host;
        private Uri? _url;
        private string _documentText = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="WebBrowser"/> class.</summary>
        public WebBrowser () : this (WebViewSupport.TryCreate ())
        {
        }

        // Test seam: a browser over a given engine handle (see WebViewHost's matching constructor).
        internal WebBrowser (IWebViewHandle? engine)
        {
            _host = Controls.AddImplicitControl (new WebViewHost (engine) { Dock = DockStyle.Fill });

            if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle) {
                handle.NavigationCompleted += OnNavigationCompleted;
                handle.WebMessageReceived += OnWebMessageReceived;
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

        private string _documentTitle = string.Empty;

        /// <summary>Gets the title of the current document.</summary>
        /// <remarks>Read from the page's <c>document.title</c> through the engine's script bridge once
        /// each navigation completes, raising <see cref="DocumentTitleChanged"/> when it differs
        /// (SMP-57). The read is asynchronous, so the title arrives just after
        /// <see cref="DocumentCompleted"/>; empty with no functional webview.</remarks>
        public string DocumentTitle => _documentTitle;

        // SMP-57: the history is the sequence of completed navigations, kept here because the engine
        // seam reports completions but no back/forward stack. Completions are what the engine also
        // reports for a link the user clicks inside the page, so those are recorded too.
        private readonly List<Uri> _history = [];
        private int _historyIndex = -1;
        private int? _pendingTravel;

        /// <summary>Gets whether there is a previous page in the navigation history.</summary>
        /// <remarks>Tracked from the navigations this control has seen complete; see
        /// <see cref="GoBack"/>. Always false with no functional webview.</remarks>
        public bool CanGoBack => _historyIndex > 0;

        /// <summary>Gets whether there is a next page in the navigation history.</summary>
        /// <remarks>See <see cref="CanGoBack"/>.</remarks>
        public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

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
        /// <remarks>Raises <see cref="Navigating"/> first, as upstream does, and does nothing further
        /// when a handler cancels it -- the standard "open this link in the OS browser instead"
        /// pattern. Only navigations started from code raise it: the engine seam does not report a
        /// link the user clicks inside the page before it is followed.</remarks>
        public void Navigate (string urlString)
        {
            var url = new Uri (urlString, UriKind.RelativeOrAbsolute);

            var navigating = new WebBrowserNavigatingEventArgs (url, string.Empty);
            OnNavigating (navigating);

            if (navigating.Cancel) {
                _pendingTravel = null;
                return;
            }

            _url = url;
            ReadyState = WebBrowserReadyState.Loading;

            if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle && _url.IsAbsoluteUri)
                handle.Navigate (_url);
        }

        /// <summary>Navigates to the specified URL.</summary>
        public void Navigate (Uri url) => Navigate (url.ToString ());

        /// <summary>Navigates to the previous page in the history, if there is one.</summary>
        public void GoBack ()
        {
            if (CanGoBack)
                Travel (_historyIndex - 1);
        }

        /// <summary>Navigates to the next page in the history, if there is one.</summary>
        public void GoForward ()
        {
            if (CanGoForward)
                Travel (_historyIndex + 1);
        }

        // A move through the history is an ordinary navigation (upstream's engine raises Navigating for
        // it too) whose completion moves the index instead of appending.
        private void Travel (int index)
        {
            _pendingTravel = index;
            Navigate (_history[index]);
        }

        /// <summary>Raises the <see cref="Navigating"/> event.</summary>
        protected virtual void OnNavigating (WebBrowserNavigatingEventArgs e) => Navigating?.Invoke (this, e);

        /// <summary>Raises the <see cref="Navigated"/> event.</summary>
        protected virtual void OnNavigated (WebBrowserNavigatedEventArgs e) => Navigated?.Invoke (this, e);

        /// <summary>Raises the <see cref="DocumentCompleted"/> event.</summary>
        protected virtual void OnDocumentCompleted (WebBrowserDocumentCompletedEventArgs e) => DocumentCompleted?.Invoke (this, e);

        /// <summary>Raises the <see cref="CanGoBackChanged"/> event.</summary>
        protected virtual void OnCanGoBackChanged (EventArgs e) => CanGoBackChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="CanGoForwardChanged"/> event.</summary>
        protected virtual void OnCanGoForwardChanged (EventArgs e) => CanGoForwardChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="DocumentTitleChanged"/> event.</summary>
        protected virtual void OnDocumentTitleChanged (EventArgs e) => DocumentTitleChanged?.Invoke (this, e);

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

        /// <summary>
        /// Runs the given script in the webview and returns its result asynchronously. Unlike
        /// <see cref="InvokeScript(string)"/> this is a real, non-stubbed call into the underlying
        /// <see cref="IWebViewHandle.ExecuteScriptAsync"/> — safe to await from the UI thread since it
        /// does not block on the engine's own message pump. Returns <c>null</c> when no functional
        /// webview backs this control.
        /// </summary>
        public Task<string?> ExecuteScriptAsync (string script) =>
            _host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle
                ? handle.ExecuteScriptAsync (script)
                : Task.FromResult<string?> (null);

        /// <summary>Raised when the document has finished loading.</summary>
        public event EventHandler<WebBrowserDocumentCompletedEventArgs>? DocumentCompleted;

        /// <summary>Raised when the page posts a message back to the host (e.g. via <c>window.invokeCSharpAction</c>).</summary>
        public event EventHandler<WebViewMessageEventArgs>? WebMessageReceived;

        /// <summary>Raised when a navigation has completed, before <see cref="DocumentCompleted"/>.</summary>
        public event EventHandler<WebBrowserNavigatedEventArgs>? Navigated;

        /// <summary>Raised before a navigation started from code; set <c>Cancel</c> to stop it.</summary>
        public event EventHandler<WebBrowserNavigatingEventArgs>? Navigating;

        /// <summary>Raised when the <see cref="CanGoBack"/> property changes.</summary>
        public event EventHandler? CanGoBackChanged;

        /// <summary>Raised when the <see cref="CanGoForward"/> property changes.</summary>
        public event EventHandler? CanGoForwardChanged;

        /// <summary>Raised when the <see cref="DocumentTitle"/> property changes.</summary>
        public event EventHandler? DocumentTitleChanged;

#pragma warning disable CS0067
        /// <summary>Raised when the StatusText property changes. Never raised: the engine seam reports
        /// no status text, so <c>StatusText</c> is always empty.</summary>
        public event EventHandler? StatusTextChanged;
#pragma warning restore CS0067

        // Upstream's order on a completed navigation: Navigated, then DocumentCompleted.
        private void OnNavigationCompleted (object? sender, WebViewNavigationCompletedEventArgs e)
        {
            var url = e.Url ?? _url;

            if (e.Url is not null)
                _url = e.Url;

            if (e.IsSuccess && url is not null)
                RecordHistory (url);

            _pendingTravel = null;

            ReadyState = WebBrowserReadyState.Complete;
            OnNavigated (new WebBrowserNavigatedEventArgs (url));
            OnDocumentCompleted (new WebBrowserDocumentCompletedEventArgs (url));
            RefreshDocumentTitle ();
        }

        private void RecordHistory (Uri url)
        {
            var could_go_back = CanGoBack;
            var could_go_forward = CanGoForward;

            if (_pendingTravel is { } travel && travel >= 0 && travel < _history.Count) {
                _historyIndex = travel;
            } else if (_historyIndex < 0 || _history[_historyIndex] != url) {
                // A new page drops the forward history, as every browser's does. A completion for the
                // page already current (a reload) is not a new entry.
                _history.RemoveRange (_historyIndex + 1, _history.Count - _historyIndex - 1);
                _history.Add (url);
                _historyIndex = _history.Count - 1;
            }

            if (could_go_back != CanGoBack)
                OnCanGoBackChanged (EventArgs.Empty);

            if (could_go_forward != CanGoForward)
                OnCanGoForwardChanged (EventArgs.Empty);
        }

        private async void RefreshDocumentTitle ()
        {
            if (!_host.IsFunctional || _host.WebViewHandle is not IWebViewHandle handle)
                return;

            string title;

            try {
                title = DecodeScriptString (await handle.ExecuteScriptAsync ("document.title"));
            } catch {
                // A page that will not run script, or an engine torn down mid-read, leaves the title
                // as it was rather than faulting an async void.
                return;
            }

            if (title == _documentTitle)
                return;

            _documentTitle = title;
            OnDocumentTitleChanged (EventArgs.Empty);
        }

        // Engines differ: WebView2 hands back the result JSON-encoded ("\"Title\""), WKWebView and
        // WebKitGTK the bare string. A JSON string literal is decoded; anything else is taken as is.
        internal static string DecodeScriptString (string? result)
        {
            if (result is null || result == "null")
                return string.Empty;

            if (result.Length >= 2 && result[0] == '"' && result[result.Length - 1] == '"') {
                try {
                    using var json = System.Text.Json.JsonDocument.Parse (result);
                    return json.RootElement.GetString () ?? string.Empty;
                } catch (System.Text.Json.JsonException) {
                }
            }

            return result;
        }

        private void OnWebMessageReceived (object? sender, WebViewMessageEventArgs e) =>
            WebMessageReceived?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing && _host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle) {
                handle.NavigationCompleted -= OnNavigationCompleted;
                handle.WebMessageReceived -= OnWebMessageReceived;
            }

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
