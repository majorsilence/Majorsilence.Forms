using Avalonia.Threading;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Wraps an Avalonia <see cref="Avalonia.Controls.NativeWebView"/> (from the <c>Avalonia.Controls.WebView</c> package)
    /// as an <see cref="IWebViewHandle"/>. All members marshal via <see cref="Dispatcher.UIThread"/>
    /// exactly like the clipboard methods on <see cref="AvaloniaPlatformBackend"/> — <see cref="Avalonia.Controls.NativeWebView"/>
    /// is an Avalonia <c>Control</c> and must only be touched from the UI thread.
    ///
    /// Contract notes discovered during the Phase 0 spike (see scratchpad findings — not shipped with the
    /// repo, kept for implementer context): the real Avalonia type is <c>Avalonia.Controls.NativeWebView</c>
    /// (there is no public type literally named <c>WebView</c>); its <c>NavigateToString(string, Uri)</c>
    /// exists and takes a nullable base-URI, so no temp-file/<c>file:///</c> workaround is needed; its
    /// navigation event args expose <c>Request</c> (a <see cref="Uri"/>) rather than <c>Url</c>, which
    /// this handle adapts into the core <see cref="WebViewNavigationCompletedEventArgs"/> shape.
    /// </summary>
    internal sealed class AvaloniaWebViewHandle : IWebViewHandle
    {
        private readonly Avalonia.Controls.NativeWebView _webView;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvaloniaWebViewHandle"/> class, wrapping a freshly
        /// created <see cref="Avalonia.Controls.NativeWebView"/>. Must run on the UI thread — the factory
        /// (<see cref="AvaloniaPlatformBackend.CreateWebView"/>) is responsible for catching construction
        /// failures and returning <c>null</c> instead of propagating them.
        /// </summary>
        public AvaloniaWebViewHandle ()
        {
            _webView = new Avalonia.Controls.NativeWebView ();
            _webView.NavigationCompleted += OnNavigationCompleted;
            _webView.WebMessageReceived += OnWebMessageReceived;
        }

        /// <inheritdoc/>
        public object NativeControl => _webView;

        /// <inheritdoc/>
        public void Navigate (Uri url)
            => Dispatcher.UIThread.Invoke (() => _webView.Navigate (url));

        /// <inheritdoc/>
        public void NavigateToString (string html)
            // The Avalonia signature takes a (nullable) base URI as a second argument; null is fine — the
            // spike confirmed this loads the HTML directly with no temp-file fallback required.
            => Dispatcher.UIThread.Invoke (() => _webView.NavigateToString (html, null!));

        /// <inheritdoc/>
        public async Task<string?> ExecuteScriptAsync (string script)
        {
            // Never block the UI thread on InvokeScript: if we're already on it, await inline; otherwise
            // hop over via the Task<TResult>-returning InvokeAsync overload (which unwraps the inner task
            // for us) rather than the synchronous .GetAwaiter().GetResult() pattern used for the clipboard
            // API — blocking here would deadlock, since InvokeScript itself needs the UI thread pump to
            // complete before its Task resolves.
            if (Dispatcher.UIThread.CheckAccess ())
                return await _webView.InvokeScript (script).ConfigureAwait (false);

            return await Dispatcher.UIThread.InvokeAsync (() => _webView.InvokeScript (script)).ConfigureAwait (false);
        }

        /// <inheritdoc/>
        public event EventHandler<WebViewNavigationCompletedEventArgs>? NavigationCompleted;

        /// <inheritdoc/>
        public event EventHandler<WebViewMessageEventArgs>? WebMessageReceived;

        private void OnNavigationCompleted (object? sender, Avalonia.Controls.WebViewNavigationCompletedEventArgs e)
            => NavigationCompleted?.Invoke (this, new WebViewNavigationCompletedEventArgs (e.Request, e.IsSuccess));

        private void OnWebMessageReceived (object? sender, Avalonia.Controls.WebMessageReceivedEventArgs e)
            => WebMessageReceived?.Invoke (this, new WebViewMessageEventArgs (e.Body ?? string.Empty));

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;

            _webView.NavigationCompleted -= OnNavigationCompleted;
            _webView.WebMessageReceived -= OnWebMessageReceived;

            // Avalonia.Controls.NativeWebView doesn't implement IDisposable itself; disposing its owning control (removing
            // it from the visual tree, which WebViewHost.Dispose triggers via NativeControlHost) is what
            // tears down the underlying engine. Nothing further to release here.
        }
    }
}
