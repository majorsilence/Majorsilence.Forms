using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using Majorsilence.Forms;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;

namespace Majorsilence.Forms.WebDriver
{
    /// <summary>
    /// A minimal <see href="https://www.w3.org/TR/webdriver/">W3C WebDriver</see> HTTP server that
    /// exposes a <see cref="WindowBase"/> for remote automation. Because the WebDriver protocol is just
    /// HTTP+JSON, any WebDriver client — Selenium's <c>RemoteWebDriver</c> in any language, or a custom
    /// client — can drive a Majorsilence.Forms window through this server, cross-platform and headless.
    ///
    /// It is a thin remote shell over <see cref="AutomationSession"/>: locators map to <see cref="By"/>,
    /// clicks/keys go through the neutral input pipeline, and screenshots use the offscreen renderer.
    /// Element actions are marshalled onto the UI thread, so the server may run on its own thread.
    ///
    /// Scope: a single window/session and the commonly-used element commands (find, click, send keys,
    /// clear, text, name, rect, enabled, screenshot). Not a full conformance implementation.
    /// </summary>
    public sealed class WebDriverServer : IDisposable
    {
        // The W3C element-reference key clients expect in find-element responses.
        private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

        private readonly WindowBase _window;
        private readonly HttpListener _listener = new ();
        private readonly Dictionary<string, ElementRef> _elements = new ();
        private Thread? _thread;
        private volatile bool _running;
        private string? _sessionId;
        private int _elementCounter;

        /// <summary>Creates a server bound to the given window. Call <see cref="Start"/> to begin listening.</summary>
        /// <param name="window">The window to automate.</param>
        /// <param name="port">TCP port to listen on (loopback only). Use 0 for tests that pick a free port externally.</param>
        public WebDriverServer (WindowBase window, int port)
        {
            _window = window ?? throw new ArgumentNullException (nameof (window));
            Port = port;
            _listener.Prefixes.Add ($"http://127.0.0.1:{port}/");
        }

        /// <summary>The port the server listens on.</summary>
        public int Port { get; }

        /// <summary>The base URL a WebDriver client should connect to.</summary>
        public Uri Url => new ($"http://127.0.0.1:{Port}/");

        /// <summary>Starts listening on a background thread.</summary>
        public void Start ()
        {
            if (_running)
                return;

            _listener.Start ();
            _running = true;
            _thread = new Thread (Loop) { IsBackground = true, Name = "MajorsilenceWebDriver" };
            _thread.Start ();
        }

        /// <summary>Stops listening and releases the HTTP listener.</summary>
        public void Stop ()
        {
            if (!_running)
                return;

            _running = false;
            try { _listener.Stop (); } catch { /* shutting down */ }
            try { _thread?.Join (1000); } catch { /* best effort */ }
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            Stop ();
            try { _listener.Close (); } catch { /* already closed */ }
        }

        private void Loop ()
        {
            while (_running) {
                HttpListenerContext ctx;
                try {
                    ctx = _listener.GetContext ();
                } catch {
                    return; // listener stopped
                }

                try {
                    Handle (ctx);
                } catch (Exception ex) {
                    TryWriteError (ctx, 500, "unknown error", ex.Message);
                }
            }
        }

        // ── Routing ──

        private void Handle (HttpListenerContext ctx)
        {
            var method = ctx.Request.HttpMethod;
            var segments = ctx.Request.Url!.AbsolutePath.Trim ('/')
                .Split ('/', StringSplitOptions.RemoveEmptyEntries);

            // GET /status
            if (method == "GET" && segments is ["status"]) {
                WriteValue (ctx, new { ready = true, message = "Majorsilence.Forms WebDriver ready" });
                return;
            }

            // POST /session
            if (method == "POST" && segments is ["session"]) {
                _sessionId = "session-" + Guid.NewGuid ().ToString ("N");
                _elements.Clear ();
                WriteValue (ctx, new { sessionId = _sessionId, capabilities = new { browserName = "majorsilence.forms" } });
                return;
            }

            // Everything below is /session/{id}/...
            if (segments.Length < 2 || segments[0] != "session") {
                TryWriteError (ctx, 404, "unknown command", ctx.Request.Url!.AbsolutePath);
                return;
            }

            var sid = segments[1];

            // DELETE /session/{id}
            if (method == "DELETE" && segments.Length == 2) {
                if (sid == _sessionId) { _sessionId = null; _elements.Clear (); }
                WriteValue (ctx, (object?)null);
                return;
            }

            if (sid != _sessionId) {
                TryWriteError (ctx, 404, "invalid session id", sid);
                return;
            }

            // POST /session/{id}/element  and  /elements
            if (method == "POST" && segments.Length == 3 && segments[2] is "element" or "elements") {
                FindElements (ctx, plural: segments[2] == "elements");
                return;
            }

            // GET /session/{id}/screenshot
            if (method == "GET" && segments is [_, _, "screenshot"]) {
                Screenshot (ctx);
                return;
            }

            // GET /session/{id}/source  — XML page source (the tree XPath also runs against)
            if (method == "GET" && segments is [_, _, "source"]) {
                var xml = OnUi (() => Session ().GetPageSource ());
                WriteValue (ctx, xml);
                return;
            }

            // /session/{id}/element/{eid}/...
            if (segments.Length >= 5 && segments[2] == "element") {
                var eid = segments[3];
                var verb = segments[4];

                if (!_elements.TryGetValue (eid, out var elem)) {
                    TryWriteError (ctx, 404, "no such element", eid);
                    return;
                }

                switch (method, verb) {
                    case ("POST", "click"): OnUi (() => Session ().Click (Resolve (elem))); WriteValue (ctx, (object?)null); return;
                    case ("POST", "value"): SendKeys (ctx, elem); return;
                    case ("POST", "clear"): OnUi (() => { var s = Session (); s.Clear (Resolve (elem)); }); WriteValue (ctx, (object?)null); return;
                    case ("GET", "text"): WriteValue (ctx, OnUi (() => Session ().GetText (Resolve (elem)))); return;
                    case ("GET", "name"): WriteValue (ctx, OnUi (() => Resolve (elem).Role)); return;
                    case ("GET", "enabled"): WriteValue (ctx, OnUi (() => Resolve (elem).Enabled)); return;
                    case ("GET", "attribute"): {
                        var attr = segments.Length >= 6 ? segments[5] : string.Empty;
                        WriteValue (ctx, OnUi (() => AttributeOf (Resolve (elem), attr)));
                        return;
                    }
                    case ("GET", "rect"): {
                        var r = OnUi (() => Resolve (elem).Bounds);
                        WriteValue (ctx, new { x = r.X, y = r.Y, width = r.Width, height = r.Height });
                        return;
                    }
                }
            }

            TryWriteError (ctx, 404, "unknown command", ctx.Request.Url!.AbsolutePath);
        }

        // ── Commands ──

        private void FindElements (HttpListenerContext ctx, bool plural)
        {
            var body = ReadJson (ctx);
            var by = ToBy (
                body.TryGetProperty ("using", out var u) ? u.GetString () ?? string.Empty : string.Empty,
                body.TryGetProperty ("value", out var v) ? v.GetString () ?? string.Empty : string.Empty);

            IReadOnlyList<AutomationElement> matches;
            try {
                matches = OnUi (() => Session ().FindAll (by));
            } catch (System.Xml.XPath.XPathException ex) {
                // A malformed XPath selector is a client error, not a server fault.
                TryWriteError (ctx, 400, "invalid selector", ex.Message);
                return;
            }

            if (plural) {
                var refs = new List<object> ();
                for (var i = 0; i < matches.Count; i++)
                    refs.Add (Wrap (Register (by, i, matches[i].AutomationId)));
                WriteValue (ctx, refs);
                return;
            }

            if (matches.Count == 0) {
                TryWriteError (ctx, 404, "no such element", by.Description);
                return;
            }

            WriteValue (ctx, Wrap (Register (by, 0, matches[0].AutomationId)));
        }

        private void SendKeys (HttpListenerContext ctx, ElementRef elem)
        {
            var body = ReadJson (ctx);
            string text = string.Empty;

            if (body.TryGetProperty ("text", out var t) && t.ValueKind == JsonValueKind.String)
                text = t.GetString () ?? string.Empty;
            else if (body.TryGetProperty ("value", out var arr) && arr.ValueKind == JsonValueKind.Array)
                text = string.Concat (arr.EnumerateArray ().Select (e => e.GetString ()));

            OnUi (() => Session ().SendKeys (Resolve (elem), text));
            WriteValue (ctx, (object?)null);
        }

        private void Screenshot (HttpListenerContext ctx)
        {
            var png = OnUi (() => HeadlessRenderer.CapturePng (_window));
            WriteValue (ctx, Convert.ToBase64String (png));
        }

        // ── Element references (re-resolved from a fresh snapshot on every use, so values stay live) ──

        // A reference is re-resolved on every use against a fresh snapshot. Prefer the stable
        // AutomationId (control Name); fall back to the original locator + index. Resolving by the
        // locator's accessible name would be fragile — e.g. a text box's name becomes its typed text.
        private sealed record ElementRef (By By, int Index, string AutomationId);

        private string Register (By by, int index, string automationId)
        {
            var id = "elem-" + Interlocked.Increment (ref _elementCounter);
            _elements[id] = new ElementRef (by, index, automationId);
            return id;
        }

        private AutomationElement Resolve (ElementRef elem)
        {
            if (!string.IsNullOrEmpty (elem.AutomationId)) {
                var byId = Session ().Find (By.Id (elem.AutomationId));
                if (byId != null)
                    return byId;
            }

            var matches = Session ().FindAll (elem.By);
            if (elem.Index >= matches.Count)
                throw new InvalidOperationException ($"stale element reference: {elem.By.Description}[{elem.Index}]");
            return matches[elem.Index];
        }

        private AutomationSession Session () => new (_window);

        private static Dictionary<string, string> Wrap (string elementId) => new () { [ElementKey] = elementId };

        // getAttribute: exposes the same fields as the XML page source, so a locator captured from an
        // attribute resolves identically. Unknown names return null (W3C: absent attribute → null).
        private static string? AttributeOf (AutomationElement e, string name) => name switch {
            "id" => e.AutomationId,
            "name" => e.Name,
            "role" => e.Role,
            "type" => e.ControlType,
            "value" => e.Value ?? string.Empty,
            "enabled" => e.Enabled ? "true" : "false",
            "visible" => e.Visible ? "true" : "false",
            "focused" => e.Focused ? "true" : "false",
            "x" => e.Bounds.X.ToString (System.Globalization.CultureInfo.InvariantCulture),
            "y" => e.Bounds.Y.ToString (System.Globalization.CultureInfo.InvariantCulture),
            "width" => e.Bounds.Width.ToString (System.Globalization.CultureInfo.InvariantCulture),
            "height" => e.Bounds.Height.ToString (System.Globalization.CultureInfo.InvariantCulture),
            _ => null
        };

        // Maps WebDriver locator strategies (and a few custom ones) onto our By locators.
        private static By ToBy (string strategy, string value) => strategy switch {
            "id" => By.Id (value),
            "name" => By.Name (value),
            "tag name" => By.Role (value),
            "role" => By.Role (value),
            "type" => By.Type (value),
            "link text" => By.Text (value),
            "partial link text" => By.Text (value),
            "xpath" => By.XPath (value),
            // Selenium's default strategy is "css selector"; support the common #id / [name='x'] forms.
            "css selector" => CssToBy (value),
            _ => By.Name (value)
        };

        private static By CssToBy (string css)
        {
            if (css.StartsWith ('#'))
                return By.Id (css[1..]);

            var nameAttr = ExtractAttr (css, "name");
            if (nameAttr != null)
                return By.Name (nameAttr);

            return By.Type (css.TrimStart ('.'));
        }

        private static string? ExtractAttr (string css, string attr)
        {
            var marker = "[" + attr + "=";
            var i = css.IndexOf (marker, StringComparison.Ordinal);
            if (i < 0)
                return null;
            var rest = css[(i + marker.Length)..].TrimEnd (']').Trim ('\'', '"');
            return rest;
        }

        // ── UI-thread marshalling ──

        private static T OnUi<T> (Func<T> func) =>
            Platform.Backend.CheckAccess () ? func () : Platform.Backend.Invoke (func);

        private static void OnUi (Action action)
        {
            if (Platform.Backend.CheckAccess ())
                action ();
            else
                Platform.Backend.Invoke (action);
        }

        // ── JSON I/O ──

        private static JsonElement ReadJson (HttpListenerContext ctx)
        {
            using var reader = new StreamReader (ctx.Request.InputStream, Encoding.UTF8);
            var raw = reader.ReadToEnd ();
            if (string.IsNullOrWhiteSpace (raw))
                return JsonDocument.Parse ("{}").RootElement;
            return JsonDocument.Parse (raw).RootElement;
        }

        private static void WriteValue (HttpListenerContext ctx, object? value) =>
            Write (ctx, 200, new Dictionary<string, object?> { ["value"] = value });

        private static void TryWriteError (HttpListenerContext ctx, int status, string error, string message)
        {
            try {
                Write (ctx, status, new Dictionary<string, object?> {
                    ["value"] = new Dictionary<string, object?> { ["error"] = error, ["message"] = message }
                });
            } catch {
                /* connection gone */
            }
        }

        private static void Write (HttpListenerContext ctx, int status, object payload)
        {
            var json = JsonSerializer.Serialize (payload);
            var bytes = Encoding.UTF8.GetBytes (json);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write (bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close ();
        }
    }
}
