using System.Globalization;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat rich text document. Majorsilence.Forms' rich text editor is a webview
    /// <c>contenteditable</c> surface that produces (and consumes) HTML directly, so unlike Telerik's real
    /// <c>RadDocument</c> — a full structured document model (sections/paragraphs/runs) — this is simply a
    /// thin carrier for the HTML string. <see cref="HtmlFormatProvider"/> converts between the two.
    /// </summary>
    public class RadDocument
    {
        /// <summary>Gets or sets the document's content as HTML.</summary>
        public string Html { get; set; } = string.Empty;
    }

    /// <summary>
    /// Telerik-compat HTML format provider. Near-passthrough: Majorsilence.Forms' rich text editor already
    /// works in HTML end-to-end (via the browser's <c>contenteditable</c>/<c>execCommand</c> surface), so
    /// import/export is just wrapping/unwrapping the HTML string in a <see cref="RadDocument"/> — there is
    /// no structured document model to convert to or from.
    /// </summary>
    public class HtmlFormatProvider
    {
        /// <summary>Parses an HTML string into a <see cref="RadDocument"/>.</summary>
        public RadDocument Import (string html) => new RadDocument { Html = html ?? string.Empty };

        /// <summary>Exports a <see cref="RadDocument"/> back to its HTML string.</summary>
        public string Export (RadDocument document) => document?.Html ?? string.Empty;

        /// <summary>Exports a <see cref="RadDocument"/> to HTML using the specified settings. Majorsilence.Forms' editor already produces inline-styled fragment HTML, so the settings are accepted but do not change the output.</summary>
        public string Export (RadDocument document, HtmlExportSettings settings) => Export (document);
    }

    /// <summary>
    /// Telerik-compat HTML export settings. Accepted and stored, but do not change
    /// <see cref="HtmlFormatProvider"/>'s output: the editor's <c>execCommand ('styleWithCSS', true)</c>
    /// setup already produces a fragment of HTML with inline styles, which is exactly what Financial's
    /// <see cref="Telerik.DocumentExportLevel.Fragment"/> + <see cref="Telerik.StylesExportMode.Inline"/>
    /// combination asks for.
    /// </summary>
    public class HtmlExportSettings
    {
        /// <summary>Gets or sets how much of the document is exported. Stored for API compatibility; no effect.</summary>
        public DocumentExportLevel DocumentExportLevel { get; set; } = DocumentExportLevel.Fragment;

        /// <summary>Gets or sets how styles are represented in the exported HTML. Stored for API compatibility; no effect.</summary>
        public StylesExportMode StylesExportMode { get; set; } = StylesExportMode.Inline;
    }

    /// <summary>Specifies how much of a document <see cref="HtmlFormatProvider"/> exports. Compat for Telerik DocumentExportLevel.</summary>
    public enum DocumentExportLevel
    {
        /// <summary>Export a complete HTML document (with <c>&lt;html&gt;</c>/<c>&lt;body&gt;</c> wrapper).</summary>
        Full = 0,
        /// <summary>Export only the body content fragment. Matches Majorsilence.Forms' editor output.</summary>
        Fragment = 1,
    }

    /// <summary>Specifies how styles are represented in exported HTML. Compat for Telerik StylesExportMode.</summary>
    public enum StylesExportMode
    {
        /// <summary>No style information is exported.</summary>
        None = 0,
        /// <summary>Styles are exported as CSS classes.</summary>
        Classes = 1,
        /// <summary>Styles are exported as inline <c>style</c> attributes. Matches Majorsilence.Forms' editor output.</summary>
        Inline = 2,
    }

    /// <summary>
    /// Telerik-compat marker interface for a spell checker attachable to a <see cref="RadRichTextEditor"/>
    /// (mirrors Telerik's <c>ISpellChecker</c>). <see cref="DocumentSpellChecker"/> is the only
    /// implementation Majorsilence.Forms ships.
    /// </summary>
    public interface ISpellChecker
    {
    }

    /// <summary>
    /// Telerik-compat rich text document spell checker. Majorsilence.Forms' rich text editor runs inside a
    /// native webview, whose <c>contenteditable</c> surface already gets the host browser's own native
    /// spellcheck (see <see cref="RadRichTextEditor"/>'s embedded page, which sets <c>spellcheck="true"</c>)
    /// — so <see cref="AddDictionary"/> is a no-op that exists purely so migrated code compiles.
    /// </summary>
    public class DocumentSpellChecker : ISpellChecker
    {
        /// <summary>Adds a dictionary for the specified culture. No-op — see the type's remarks.</summary>
        public void AddDictionary (RadDictionary dictionary, CultureInfo culture) { }
    }

    /// <summary>
    /// Telerik-compat rich text editor. Composes a <see cref="WebViewHost"/> hosting a single embedded
    /// <c>contenteditable</c> HTML page (loaded via <see cref="IWebViewHandle.NavigateToString(string)"/>).
    /// The page exposes a small JS surface (<c>msSetHtml</c>/<c>msExec</c>/<c>msReadOnly</c>) and posts a
    /// <c>{type:'ready'}</c> message once loaded, then a <c>{type:'content', html}</c> message on every
    /// <c>input</c>/<c>blur</c> via the webview's <c>invokeCSharpAction</c> bridge (received here through
    /// <see cref="IWebViewHandle.WebMessageReceived"/>).
    ///
    /// <para>
    /// <see cref="Document"/>'s getter is a plain synchronous read of a field kept current by
    /// <see cref="IWebViewHandle.WebMessageReceived"/> — it deliberately does <b>not</b> pump a nested
    /// message loop to force a fresh read from the page, which would turn every getter call into a
    /// re-entrancy hazard. <see cref="Document"/>'s setter queues the HTML until the page's first
    /// <c>ready</c> message (or flushes immediately if the page is already ready).
    /// </para>
    ///
    /// <para>
    /// When no functional webview is available, the control instead composes a plain
    /// <see cref="Majorsilence.Forms.RichTextBox"/> (Dock = Fill) showing the raw HTML source — crude but
    /// lossless, since <see cref="Document"/> round-trips through the text box's <c>Text</c>.
    /// </para>
    /// </summary>
    public class RadRichTextEditor : Control, ISupportInitializeCompat
    {
        private const string ReadyMessage = "ready";
        private const string ContentMessageType = "content";

        private readonly WebViewHost _host;
        private readonly Majorsilence.Forms.RichTextBox? _fallbackTextBox;

        private string _cachedHtml = string.Empty;
        private string? _pendingHtml;
        private bool _pageReady;
        private bool _isReadOnly;

        /// <summary>Initializes a new instance of the <see cref="RadRichTextEditor"/> class.</summary>
        public RadRichTextEditor ()
        {
            _host = Controls.AddImplicitControl (new WebViewHost { Dock = DockStyle.Fill });

            if (_host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle) {
                handle.WebMessageReceived += OnWebMessageReceived;
                handle.NavigateToString (EditorPageHtml);
            } else {
                _fallbackTextBox = Controls.AddImplicitControl (new Majorsilence.Forms.RichTextBox {
                    Dock = DockStyle.Fill,
                });

                // Note: Majorsilence.Forms.TextBox mutates its document directly for both programmatic and
                // typed edits and never raises TextChanged (see TextBox.cs's Text override and its
                // OnKeyPress handler) — so there is no reliable change-notification hook here to raise
                // DocumentChanged from as the user types in the fallback. Document.get instead always
                // reads _fallbackTextBox.Text live (see the Document property below) rather than relying
                // on a cached field kept current by an event that cannot fire; DocumentChanged is only
                // raised for programmatic Document.set calls in fallback mode.
            }
        }

        /// <summary>Gets whether a functional native webview backs this control (as opposed to the plain-text fallback).</summary>
        public bool IsWebViewFunctional => _host.IsFunctional;

        /// <summary>
        /// Gets or sets the document currently shown in the editor. On the webview path, the getter returns
        /// a synchronous snapshot kept current as the user types (see the type's remarks) — it never blocks
        /// on the webview. On the fallback path (no functional webview), the getter instead reads the
        /// fallback text box's <c>Text</c> live, since Majorsilence.Forms' <c>TextBox</c> has no reliable
        /// change-notification hook to keep a cached snapshot current as the user types (see the
        /// constructor's remarks). The setter pushes the HTML to the page immediately if it has already
        /// signalled ready, or queues it to be flushed as soon as it does.
        /// </summary>
        public RadDocument Document {
            get => new RadDocument { Html = _fallbackTextBox?.Text ?? _cachedHtml };
            set => SetHtml (value?.Html ?? string.Empty);
        }

        /// <summary>Gets or sets whether the editor is read-only.</summary>
        public bool IsReadOnly {
            get => _isReadOnly;
            set {
                if (_isReadOnly == value)
                    return;
                _isReadOnly = value;

                if (_fallbackTextBox != null)
                    _fallbackTextBox.ReadOnly = value;
                else if (_pageReady)
                    _ = ExecuteScriptAsyncSafe ($"msReadOnly({(value ? "true" : "false")})");
            }
        }

        /// <summary>Gets or sets the spell checker attached to the editor. Majorsilence.Forms uses the host browser's native spellcheck; see <see cref="DocumentSpellChecker"/>.</summary>
        public ISpellChecker SpellChecker { get; set; } = new DocumentSpellChecker ();

        /// <summary>Gets the root element of the editor (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        /// <summary>
        /// Raised whenever the editor's content changes. On the webview path this fires for every user
        /// edit (each time the page pushes new HTML on input/blur) as well as programmatic
        /// <see cref="Document"/> assignments. On the fallback path it fires only for programmatic
        /// <see cref="Document"/> assignments — Majorsilence.Forms' <c>TextBox</c> has no reliable
        /// change-notification hook for typed edits (see the constructor's remarks), so live typing in the
        /// fallback text box does not raise this event.
        /// </summary>
        public event EventHandler? DocumentChanged;

        /// <summary>
        /// Executes a rich-text editing command in the underlying page (e.g. <c>"bold"</c>, <c>"italic"</c>,
        /// <c>"foreColor"</c> with a value). This is the entry point <see cref="RichTextEditorRibbonBar"/>'s
        /// buttons call. No-op on the plain-text fallback.
        /// </summary>
        internal void ExecCommand (string command, string? value = null)
        {
            if (!_pageReady)
                return;

            var arg = value is null ? "null" : JsonStringLiteral (value);
            _ = ExecuteScriptAsyncSafe ($"msExec({JsonStringLiteral (command)}, {arg})");
        }

        private void SetHtml (string html)
        {
            if (_fallbackTextBox != null) {
                _fallbackTextBox.Text = html;
                _cachedHtml = html;
                DocumentChanged?.Invoke (this, EventArgs.Empty);
                return;
            }

            _cachedHtml = html;

            if (_pageReady)
                _ = FlushHtmlAsync (html);
            else
                _pendingHtml = html;
        }

        private async System.Threading.Tasks.Task FlushHtmlAsync (string html)
        {
            if (_host.WebViewHandle is not IWebViewHandle handle)
                return;

            var script = $"msSetHtml({JsonStringLiteral (html)})";
            await handle.ExecuteScriptAsync (script).ConfigureAwait (false);
        }

        private async System.Threading.Tasks.Task ExecuteScriptAsyncSafe (string script)
        {
            if (_host.WebViewHandle is IWebViewHandle handle)
                await handle.ExecuteScriptAsync (script).ConfigureAwait (false);
        }

        // Encodes a .NET string as a JSON/JS string literal (including the surrounding quotes). Hand-rolled
        // rather than System.Text.Json.JsonSerializer.Serialize(string) — the generic overload is marked
        // RequiresUnreferencedCode (IL2026) and this project builds with IsTrimmable=true, so using it here
        // would be a trim-warning for something a dozen-line escaper handles directly.
        private static string JsonStringLiteral (string value)
        {
            var sb = new System.Text.StringBuilder (value.Length + 2);
            sb.Append ('"');
            foreach (var c in value) {
                switch (c) {
                case '"': sb.Append ("\\\""); break;
                case '\\': sb.Append ("\\\\"); break;
                case '\n': sb.Append ("\\n"); break;
                case '\r': sb.Append ("\\r"); break;
                case '\t': sb.Append ("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append ("\\u").Append (((int)c).ToString ("x4"));
                    else
                        sb.Append (c);
                    break;
                }
            }
            sb.Append ('"');
            return sb.ToString ();
        }

        private void OnWebMessageReceived (object? sender, WebViewMessageEventArgs e)
        {
            try {
                using var doc = System.Text.Json.JsonDocument.Parse (e.Body);
                var root = doc.RootElement;
                if (!root.TryGetProperty ("type", out var typeProp))
                    return;

                var type = typeProp.GetString ();
                if (type == ReadyMessage) {
                    _pageReady = true;

                    if (_isReadOnly)
                        _ = ExecuteScriptAsyncSafe ("msReadOnly(true)");

                    if (_pendingHtml is string queued) {
                        _pendingHtml = null;
                        _ = FlushHtmlAsync (queued);
                    }
                } else if (type == ContentMessageType && root.TryGetProperty ("html", out var htmlProp)) {
                    _cachedHtml = htmlProp.GetString () ?? string.Empty;
                    DocumentChanged?.Invoke (this, EventArgs.Empty);
                }
            } catch (System.Text.Json.JsonException) {
                // Malformed/unexpected message body — ignore rather than fault the control.
            }
        }

        // The embedded editor page. A minimal contenteditable surface with a JS bridge:
        //   msSetHtml(html) - replaces the editable region's content (used by the Document setter)
        //   msExec(cmd, val) - runs document.execCommand(cmd, false, val) (used by the ribbon bar)
        //   msReadOnly(ro)   - toggles the contenteditable attribute
        // and posts messages back via invokeCSharpAction (the Avalonia.Controls.WebView JS bridge function):
        //   {type:'ready'}            once, after the page finishes loading
        //   {type:'content', html}    on every input/blur while the content is edited
        private const string EditorPageHtml = """
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <style>
              html, body { margin: 0; padding: 0; height: 100%; font-family: sans-serif; font-size: 14px; }
              #editor { min-height: 100%; padding: 8px; outline: none; box-sizing: border-box; }
            </style>
            </head>
            <body>
            <div id="editor" contenteditable="true" spellcheck="true"></div>
            <script>
              document.execCommand('styleWithCSS', true);
              var editor = document.getElementById('editor');

              function post(msg) {
                if (window.invokeCSharpAction) {
                  window.invokeCSharpAction(JSON.stringify(msg));
                }
              }

              function notifyContent() {
                post({ type: 'content', html: editor.innerHTML });
              }

              editor.addEventListener('input', notifyContent);
              editor.addEventListener('blur', notifyContent);

              window.msSetHtml = function (html) {
                editor.innerHTML = html;
              };

              window.msExec = function (cmd, val) {
                document.execCommand(cmd, false, val === null || val === undefined ? null : val);
                notifyContent();
              };

              window.msReadOnly = function (ro) {
                editor.setAttribute('contenteditable', ro ? 'false' : 'true');
              };

              post({ type: 'ready' });
            </script>
            </body>
            </html>
            """;

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing && _host.IsFunctional && _host.WebViewHandle is IWebViewHandle handle)
                handle.WebMessageReceived -= OnWebMessageReceived;

            base.Dispose (disposing);
        }
    }
}
