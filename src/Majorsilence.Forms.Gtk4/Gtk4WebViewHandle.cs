using System;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Wraps a <c>WebKit.WebView</c> (WebKitGTK 6.0, from <c>GirCore.WebKit-6.0</c>) as an
    /// <see cref="IWebViewHandle"/>. <c>WebKit.WebView</c> is a <c>Gtk.Widget</c>, so
    /// <see cref="NativeControl"/> goes straight through <see cref="INativeControlHostBackend"/> and is
    /// overlaid on the Skia surface like any other native widget — GTK 4 composites it into the same
    /// render tree, so there is no airspace problem.
    ///
    /// Every call is marshalled onto the GTK/GLib UI thread via the platform backend, exactly as the
    /// Avalonia handle marshals via <c>Dispatcher.UIThread</c>: a <c>Gtk.Widget</c> must only be touched
    /// from the main thread, and the async JS bridge completes its task on the GLib main loop.
    /// </summary>
    internal sealed class Gtk4WebViewHandle : IWebViewHandle
    {
        private const string ScriptMessageHandlerName = "majorsilenceForms";

        private readonly IPlatformBackend _backend;
        private readonly global::WebKit.WebView _webView;
        private readonly global::WebKit.UserContentManager _userContent;
        private bool _disposed;

        /// <summary>
        /// Creates the handle and its <c>WebKit.WebView</c>. Must run on the UI thread — the factory
        /// (<see cref="Gtk4PlatformBackend.CreateWebView"/>) marshals and catches construction failures,
        /// returning <c>null</c> rather than propagating them.
        /// </summary>
        public Gtk4WebViewHandle (IPlatformBackend backend)
        {
            _backend = backend;

            global::WebKit.Module.Initialize ();

            _webView = global::WebKit.WebView.New ();
            _webView.OnLoadChanged += OnLoadChanged;
            _webView.OnLoadFailed += OnLoadFailed;

            _userContent = _webView.GetUserContentManager ();
            // The page posts to the host with window.webkit.messageHandlers.majorsilenceForms.postMessage(...).
            _userContent.RegisterScriptMessageHandler (ScriptMessageHandlerName, null);
            _userContent.OnScriptMessageReceived += OnScriptMessageReceived;
        }

        /// <inheritdoc/>
        public object NativeControl => _webView;

        /// <inheritdoc/>
        public void Navigate (Uri url)
        {
            ArgumentNullException.ThrowIfNull (url);
            _backend.Invoke (() => _webView.LoadUri (url.ToString ()));
        }

        /// <inheritdoc/>
        public void NavigateToString (string html)
            => _backend.Invoke (() => _webView.LoadHtml (html ?? string.Empty, null));

        /// <inheritdoc/>
        public async Task<string?> ExecuteScriptAsync (string script)
        {
            if (string.IsNullOrEmpty (script))
                return null;

            // EvaluateJavascriptAsync must be *started* on the UI thread (it touches the widget); it
            // returns a Task backed by a GLib async callback that completes on the main loop, so the
            // await itself is safe from any thread.
            var task = _backend.Invoke (() => _webView.EvaluateJavascriptAsync (script));
            try {
                var value = await task.ConfigureAwait (false);
                return ValueToString (value);
            } catch {
                return null;
            }
        }

        /// <inheritdoc/>
        public event EventHandler<WebViewNavigationCompletedEventArgs>? NavigationCompleted;

        /// <inheritdoc/>
        public event EventHandler<WebViewMessageEventArgs>? WebMessageReceived;

        private void OnLoadChanged (global::WebKit.WebView sender, global::WebKit.WebView.LoadChangedSignalArgs args)
        {
            if (args.LoadEvent == global::WebKit.LoadEvent.Finished)
                NavigationCompleted?.Invoke (this, new WebViewNavigationCompletedEventArgs (TryUri (sender.GetUri ()), isSuccess: true));
        }

        private bool OnLoadFailed (global::WebKit.WebView sender, global::WebKit.WebView.LoadFailedSignalArgs args)
        {
            NavigationCompleted?.Invoke (this, new WebViewNavigationCompletedEventArgs (TryUri (args.FailingUri), isSuccess: false));
            return false;   // false = let WebKit show its default error page
        }

        private void OnScriptMessageReceived (global::WebKit.UserContentManager sender, global::WebKit.UserContentManager.ScriptMessageReceivedSignalArgs args)
        {
            string body;
            try { body = args.Value?.ToString () ?? string.Empty; }
            catch { body = string.Empty; }
            WebMessageReceived?.Invoke (this, new WebViewMessageEventArgs (body));
        }

        private static Uri? TryUri (string? url)
            => !string.IsNullOrEmpty (url) && Uri.TryCreate (url, UriKind.Absolute, out var uri) ? uri : null;

        private static string? ValueToString (global::JavaScriptCore.Value value)
        {
            try {
                if (value is null || value.IsNull () || value.IsUndefined ())
                    return null;
                if (value.IsString ())
                    return value.ToString ();
                var json = value.ToJson (0);
                return string.IsNullOrEmpty (json) ? value.ToString () : json;
            } catch {
                return null;
            }
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;

            try {
                _webView.OnLoadChanged -= OnLoadChanged;
                _webView.OnLoadFailed -= OnLoadFailed;
                _userContent.OnScriptMessageReceived -= OnScriptMessageReceived;
                _userContent.UnregisterScriptMessageHandler (ScriptMessageHandlerName, null);
            } catch {
                // best effort — the widget is torn down when NativeControlHost removes it from the tree
            }
        }
    }
}
