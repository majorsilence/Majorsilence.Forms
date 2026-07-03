using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat PDF viewer. Composes a <see cref="WebViewHost"/>: on platforms whose native
    /// webview engine renders PDF inline (WebView2's bundled PDFium viewer on Windows, WKWebView on
    /// macOS — see <see cref="CanRenderPdfInline"/>), the loaded document is shown directly in the
    /// webview with the engine's own PDF toolbar (search, zoom, print) standing in for Telerik's. On
    /// platforms without an inline PDF viewer (Linux WebKitGTK/WPE, or no webview at all) the document is
    /// opened in the operating system's default PDF viewer via <see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/>,
    /// gated by <see cref="WebViewSupport.AllowSystemViewerFallback"/>, and a one-line placeholder is
    /// painted in the control's place.
    /// </summary>
    public class RadPdfViewer : Control
    {
        // Dedicated temp folder so a startup sweep can clean up files older than a couple of days without
        // touching unrelated %TEMP% contents (WebView2 can hold a loaded PDF file open, so best-effort
        // deletion sometimes has to wait until the next run).
        private static readonly string TempFolder = System.IO.Path.Combine (System.IO.Path.GetTempPath (), "majorsilence-pdf");

        private readonly WebViewHost _host;
        private readonly Label _fallbackLabel;
        private string? _currentTempFile;

        /// <summary>Initializes a new instance of the <see cref="RadPdfViewer"/> class.</summary>
        public RadPdfViewer ()
        {
            SweepOldTempFiles ();

            var inlineCapable = CanRenderPdfInline;

            _host = Controls.AddImplicitControl (new WebViewHost { Dock = DockStyle.Fill, Visible = inlineCapable });
            _fallbackLabel = Controls.AddImplicitControl (new Label {
                Dock = DockStyle.Fill,
                Text = string.Empty,
                Visible = !inlineCapable,
            });
        }

        /// <summary>
        /// Gets whether the current platform's webview engine can render a PDF inline. This is a static
        /// policy decision, not a runtime probe: WebView2 (Windows) ships PDFium's viewer and WKWebView
        /// (macOS) renders PDF natively, but WebKitGTK/WPE WebKit (Linux) has no inline PDF viewer even
        /// when the webview itself works fine for HTML — so Linux always uses the system-viewer fallback.
        /// </summary>
        public static bool CanRenderPdfInline => OperatingSystem.IsWindows () || OperatingSystem.IsMacOS ();

        /// <summary>Gets whether a functional native webview capable of inline PDF rendering backs this control.</summary>
        public bool IsInlineRenderingActive => CanRenderPdfInline && _host.IsFunctional;

        /// <summary>Gets or sets how the document is read into memory. Majorsilence.Forms always pages the document on demand; stored for API compatibility.</summary>
        public ReadingMode ReadingMode { get; set; } = ReadingMode.OnDemand;

        /// <summary>Gets or sets the interaction mode of the viewer. Majorsilence.Forms always supports panning and text selection; stored for API compatibility.</summary>
        public FixedDocumentViewerMode ViewerMode { get; set; } = FixedDocumentViewerMode.TextSelection;

        /// <summary>Gets the root element of the viewer (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        /// <summary>Gets the path of the temp file backing the currently loaded document, or null if none is loaded.</summary>
        protected string? CurrentDocumentPath => _currentTempFile;

        /// <summary>
        /// Loads a PDF document from a stream. The stream's contents are copied to a temp file under
        /// <c>%TEMP%\majorsilence-pdf\</c> (the caller retains ownership of <paramref name="stream"/> and
        /// may dispose it immediately after this call returns) and the viewer navigates to it.
        /// </summary>
        public void LoadDocument (System.IO.Stream stream)
        {
            System.IO.Directory.CreateDirectory (TempFolder);

            var path = System.IO.Path.Combine (TempFolder, $"{Guid.NewGuid ():N}.pdf");
            using (var file = System.IO.File.Create (path))
                stream.CopyTo (file);

            LoadDocumentCore (path, ownsFile: true);
        }

        /// <summary>Loads a PDF document directly from disk. The file is navigated in place — no temp copy is made.</summary>
        public void LoadDocument (string path)
        {
            LoadDocumentCore (path, ownsFile: false);
        }

        /// <summary>Unloads the current document, navigating the webview to a blank page and deleting any owned temp file.</summary>
        public void UnloadDocument ()
        {
            if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle)
                handle.Navigate (new Uri ("about:blank"));

            _fallbackLabel.Text = string.Empty;

            DeleteCurrentTempFile ();
        }

        private void LoadDocumentCore (string path, bool ownsFile)
        {
            // Loading a new document retires the previous temp file first (best-effort — WebView2 may
            // still hold it locked; DeleteCurrentTempFile retries on Dispose).
            DeleteCurrentTempFile ();

            if (ownsFile)
                _currentTempFile = path;

            if (IsInlineRenderingActive && _host.WebViewHandle is IWebViewHandle handle) {
                handle.Navigate (new Uri (path));
                return;
            }

            if (WebViewSupport.AllowSystemViewerFallback) {
                System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (path) { UseShellExecute = true });
                _fallbackLabel.Text = "Document opened in system PDF viewer.";
            } else {
                _fallbackLabel.Text = string.Empty;
            }
        }

        private void DeleteCurrentTempFile ()
        {
            if (_currentTempFile is null)
                return;

            TryDeleteFile (_currentTempFile);
            _currentTempFile = null;
        }

        private static void TryDeleteFile (string path)
        {
            try {
                if (System.IO.File.Exists (path))
                    System.IO.File.Delete (path);
            } catch {
                // Best-effort: the native webview engine may still hold the file open. Left for the
                // periodic sweep (SweepOldTempFiles) to clean up on a later run.
            }
        }

        private static void SweepOldTempFiles ()
        {
            try {
                if (!System.IO.Directory.Exists (TempFolder))
                    return;

                var cutoff = DateTime.UtcNow.AddDays (-2);
                foreach (var file in System.IO.Directory.EnumerateFiles (TempFolder)) {
                    try {
                        if (System.IO.File.GetLastWriteTimeUtc (file) < cutoff)
                            System.IO.File.Delete (file);
                    } catch {
                        // Ignore individual failures (locked file, permissions, race) and keep sweeping.
                    }
                }
            } catch {
                // Non-fatal: the sweep is best-effort housekeeping, never a load-bearing precondition.
            }
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                DeleteCurrentTempFile ();

            base.Dispose (disposing);
        }
    }

    /// <summary>
    /// Telerik-compat PDF viewer navigator. The native browser engine supplies its own PDF toolbar (search,
    /// zoom, page navigation, print), so this control has no real UI of its own; it renders as a thin,
    /// empty strip purely so layout code that offsets an associated <see cref="RadPdfViewer"/> by this
    /// control's <see cref="Control.Height"/> (the common WinForms pattern — dock the navigator on top,
    /// then position/size the viewer below it) continues to produce sane bounds.
    /// </summary>
    public class RadPdfViewerNavigator : Control
    {
        /// <summary>Initializes a new instance of the <see cref="RadPdfViewerNavigator"/> class.</summary>
        public RadPdfViewerNavigator ()
        {
            Height = 28;
        }

        /// <inheritdoc/>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size (200, 28);

        /// <summary>Gets or sets the <see cref="RadPdfViewer"/> this navigator controls.</summary>
        public RadPdfViewer? AssociatedViewer { get; set; }

        /// <summary>Gets the root element of the navigator (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Specifies how a <see cref="RadPdfViewer"/> reads a document into memory. Compat for Telerik ReadingMode.</summary>
    public enum ReadingMode
    {
        /// <summary>The entire document is read up front.</summary>
        AllAtOnce = 0,
        /// <summary>Pages are read as they are needed. Majorsilence.Forms' webview-backed viewer always behaves this way.</summary>
        OnDemand = 1,
    }

    /// <summary>Specifies the interaction mode of a <see cref="RadPdfViewer"/>. Compat for Telerik FixedDocumentViewerMode.</summary>
    public enum FixedDocumentViewerMode
    {
        /// <summary>No interaction.</summary>
        None = 0,
        /// <summary>Pan/scroll the document.</summary>
        Pan = 1,
        /// <summary>Select text in the document. Majorsilence.Forms' webview-backed viewer always supports both panning and text selection.</summary>
        TextSelection = 2,
    }
}
