using System;
using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Majorsilence.Forms window through a real GTK 4
    /// <c>Gtk.Window</c> whose child is the shared <see cref="Gtk4SkiaSurface"/>. Chrome, geometry and
    /// lifecycle live here; rendering and input translation are the surface's job.
    ///
    /// Popups (menus, combo dropdowns) are borderless, non-resizable top-level windows.
    /// </summary>
    internal sealed class Gtk4WindowHost : IWindowBackend, IDisposable
    {
        private readonly MF.WindowBase _owner;
        private readonly bool _isPopup;
        private readonly global::Gtk.Window _window;
        private readonly Gtk4SkiaSurface _surface;

        private Size _size = new (800, 600);
        private Point _location;
        private bool _systemDecorations;
        private bool _canResize = true;
        private bool _closedDelivered;
        private bool _minimized;
        private double _opacity = 1.0;

        public Gtk4WindowHost (MF.WindowBase owner, bool isPopup)
        {
            _owner = owner;
            _isPopup = isPopup;
            _systemDecorations = !isPopup;

            _window = global::Gtk.Window.New ();
            _window.SetDefaultSize (_size.Width, _size.Height);
            _window.SetDecorated (!isPopup);
            _window.SetResizable (!isPopup);

            _surface = new Gtk4SkiaSurface (() => _owner);
            _window.SetChild (_surface.Widget);

            WireLifecycle ();
        }

        /// <summary>The real GTK window backing this host (used by <see cref="Gtk4HostInterop.ToGtkWindow"/>).</summary>
        internal global::Gtk.Window NativeWindow => _window;

        private void WireLifecycle ()
        {
            _window.OnCloseRequest += (_, _) => {
                if (_owner.OnBackendClosing ())   // true == cancel the close
                    return true;

                if (!_closedDelivered) {
                    _closedDelivered = true;
                    _owner.OnBackendClosed ();
                }
                return false;
            };
        }

        // ── Geometry ─────────────────────────────────────────────────────────────
        // GTK 4 removed client-side control (and query) of a top-level's screen position, so Location
        // is a best-effort stored value and the PointTo* conversions assume it. Size/ClientSize track
        // the drawing area's allocation once the window is realized.

        public Point Location { get => _location; set => _location = value; }

        public Size Size {
            get => _window.GetRealized () && _surface.Widget.GetWidth () > 0
                ? new Size (_surface.Widget.GetWidth (), _surface.Widget.GetHeight ())
                : _size;
            set {
                _size = value;
                _window.SetDefaultSize (value.Width, value.Height);
            }
        }

        public Size ClientSize => _window.GetRealized () && _surface.Widget.GetWidth () > 0
            ? new Size (_surface.Widget.GetWidth (), _surface.Widget.GetHeight ())
            : (_surface.ClientLogical.Width > 0 ? _surface.ClientLogical : _size);

        public double Scaling => _surface.Scaling;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public void Show ()
        {
            _window.SetVisible (true);
            if (ShowActivated && !_isPopup)
                _window.Present ();
            _owner.OnBackendActivated ();
        }

        public void ShowDialog (IWindowBackend? owner)
        {
            if (owner is Gtk4WindowHost host) {
                _window.SetTransientFor (host._window);
                _window.SetModal (true);
            }
            _window.SetVisible (true);
            _window.Present ();
            _owner.OnBackendActivated ();
        }

        public void Hide () => _window.SetVisible (false);

        public void Close ()
        {
            if (_window.GetRealized () || _window.GetVisible ()) {
                _window.Close ();   // routes through OnCloseRequest, which delivers OnBackendClosed
                return;
            }

            // Never shown: GTK emits no close-request, so run the neutral path directly.
            if (_owner.OnBackendClosing ())
                return;
            if (!_closedDelivered) {
                _closedDelivered = true;
                _owner.OnBackendClosed ();
            }
            _window.Destroy ();
        }

        public void Activate ()
        {
            _window.SetVisible (true);
            _window.Present ();
            _owner.OnBackendActivated ();
        }

        public bool ShowActivated { get; set; } = true;

        // ── Appearance / behaviour ───────────────────────────────────────────────

        public string Title { set => _window.SetTitle (value ?? string.Empty); }

        // GTK 4 has no programmatic always-on-top; tracked so the property round-trips.
        public bool Topmost { get; set; }

        public void SetSystemDecorations (bool useSystemDecorations)
        {
            _systemDecorations = useSystemDecorations;
            _window.SetDecorated (!_isPopup && useSystemDecorations);
        }

        public void SetCursor (CursorType cursor) => _surface.Widget.SetCursorFromName (Gtk4KeyInterop.ToCursorName (cursor));

        // GTK 4 window icons come from a themed icon name, not raw PNG bytes — no-op.
        public void SetIcon (byte[]? iconPng) { }

        public Size MinimumSize {
            set => _surface.Widget.SetSizeRequest (value.IsEmpty ? -1 : value.Width, value.IsEmpty ? -1 : value.Height);
        }

        // GTK 4 exposes no maximum-size hint from application code — no-op.
        public Size MaximumSize { set { } }

        public bool CanResize {
            get => _canResize;
            set { _canResize = value; _window.SetResizable (value && !_isPopup); }
        }

        // GTK 4 removed the skip-taskbar hint — tracked so the property round-trips.
        public bool ShowInTaskbar { get; set; } = true;

        public double Opacity {
            get => _opacity;
            set { _opacity = value; _window.SetOpacity (value); }
        }

        public FormWindowState WindowState {
            get => _window.Maximized ? FormWindowState.Maximized
                 : _minimized ? FormWindowState.Minimized
                 : FormWindowState.Normal;
            set {
                switch (value) {
                    case FormWindowState.Maximized:
                        _minimized = false;
                        _window.Maximize ();
                        break;
                    case FormWindowState.Minimized:
                        _minimized = true;
                        _window.Minimize ();
                        break;
                    default:
                        _minimized = false;
                        _window.Unmaximize ();
                        _window.Unminimize ();
                        break;
                }
            }
        }

        public bool Enabled {
            get => _window.GetSensitive ();
            set => _window.SetSensitive (value);
        }

        // ── Coordinate conversion ────────────────────────────────────────────────
        // Client (0,0) is taken to coincide with the stored Location (see the Geometry note above).

        public Point PointToClient (Point screen) => new (screen.X - _location.X, screen.Y - _location.Y);

        public Point PointToScreen (Point client) => new (client.X + _location.X, client.Y + _location.Y);

        // ── Drag (custom chrome) ─────────────────────────────────────────────────
        // Majorsilence.Forms draws its own title bar on Linux, so these carry the caption drag/resize.

        public void BeginMoveDrag ()
        {
            if (TryGetToplevel (out var toplevel, out var device)) {
                var (x, y) = _surface.LastPointer;
                toplevel.BeginMove (device!, 1, x, y, _surface.LastEventTime);
            }
        }

        public void BeginResizeDrag (WindowEdge edge)
        {
            if (TryGetToplevel (out var toplevel, out var device)) {
                var (x, y) = _surface.LastPointer;
                toplevel.BeginResize (Gtk4KeyInterop.ToSurfaceEdge (edge), device, 1, x, y, _surface.LastEventTime);
            }
        }

        private bool TryGetToplevel (out Gdk.Toplevel toplevel, out Gdk.Device? device)
        {
            toplevel = null!;
            device = null;
            try {
                if (_window.GetNative ()?.GetSurface () is not Gdk.Toplevel tl)
                    return false;
                toplevel = tl;
                device = Gdk.Display.GetDefault ()?.GetDefaultSeat ()?.GetPointer ();
                return true;
            } catch {
                return false;
            }
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        public void Invalidate () => _surface.RequestRender ();

        // ── File/folder pickers ──────────────────────────────────────────────────
        // Gtk.FileDialog is async and needs a live loop turn; deferred — WebView-less compat controls
        // and the common-dialog fallbacks already cover the "no picker" case (as on Headless).

        public Task<string[]> ShowOpenFileDialog (OpenFileRequest request) => Task.FromResult (Array.Empty<string> ());

        public Task<string?> ShowSaveFileDialog (SaveFileRequest request) => Task.FromResult<string?> (null);

        public Task<string?> ShowOpenFolderDialog (FolderDialogRequest request) => Task.FromResult<string?> (null);

        public void Dispose ()
        {
            try { _window.Destroy (); } catch { /* already gone */ }
        }
    }
}
