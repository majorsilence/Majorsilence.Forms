namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility stub for the WebBrowser control.
    /// Majorsilence.Forms does not provide a real browser engine — this stub allows code to compile.
    /// </summary>
    public class WebBrowser : Control
    {
        private Uri? _url;

        /// <summary>Gets or sets the URL currently displayed in the browser.</summary>
        public Uri? Url {
            get => _url;
            set { _url = value; if (value != null) Navigate (value.ToString ()); }
        }

        /// <summary>Gets or sets the HTML content of the document. Stub — no rendering.</summary>
        public string DocumentText { get; set; } = string.Empty;

        /// <summary>Gets the title of the current document. Stub in Majorsilence.Forms.</summary>
        public string DocumentTitle => string.Empty;

        /// <summary>Gets or sets whether the browser can navigate backward. Stub in Majorsilence.Forms.</summary>
        public bool CanGoBack => false;

        /// <summary>Gets or sets whether the browser can navigate forward. Stub in Majorsilence.Forms.</summary>
        public bool CanGoForward => false;

        /// <summary>Gets the ready state of the browser. Stub in Majorsilence.Forms.</summary>
        public WebBrowserReadyState ReadyState => WebBrowserReadyState.Complete;

        /// <summary>Gets or sets whether script errors are suppressed. Stub in Majorsilence.Forms.</summary>
        public bool ScriptErrorsSuppressed { get; set; } = true;

        /// <summary>Gets or sets whether scroll bars are visible. Stub in Majorsilence.Forms.</summary>
        public bool ScrollBarsEnabled { get; set; } = true;

        /// <summary>Gets or sets whether the context menu is allowed. Stub in Majorsilence.Forms.</summary>
        public bool IsWebBrowserContextMenuEnabled { get; set; } = true;

        /// <summary>Gets or sets whether the keyboard shortcuts for the browser are enabled. Stub in Majorsilence.Forms.</summary>
        public bool WebBrowserShortcutsEnabled { get; set; } = true;

        /// <summary>Navigates to the specified URL.</summary>
        public void Navigate (string urlString) { _url = new Uri (urlString, UriKind.RelativeOrAbsolute); }

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

        /// <summary>Invokes a script function. Stub in Majorsilence.Forms — returns null.</summary>
        public object? InvokeScript (string scriptName) => null;

        /// <summary>Invokes a script function with arguments. Stub in Majorsilence.Forms — returns null.</summary>
        public object? InvokeScript (string scriptName, object[] args) => null;

        /// <summary>Raised when the document has finished loading. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<WebBrowserDocumentCompletedEventArgs>? DocumentCompleted { add { } remove { } }

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
