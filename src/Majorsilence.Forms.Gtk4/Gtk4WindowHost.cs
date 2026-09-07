using System;
using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using SkiaSharp;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Majorsilence.Forms window through a real GTK 4
    /// <c>Gtk.Window</c> whose child is a <c>Gtk.DrawingArea</c>. Each draw pass renders the owning
    /// <see cref="MF.WindowBase"/> via <c>RenderFrame</c> straight into a Cairo image-surface buffer;
    /// GTK event-controller input (click / motion / scroll / key) is translated into the neutral
    /// <c>WindowBase.Handle*</c> path.
    ///
    /// Popups (menus, combo dropdowns) are borderless, non-resizable top-level windows.
    /// </summary>
    internal sealed class Gtk4WindowHost : IWindowBackend, IDisposable
    {
        private readonly MF.WindowBase _owner;
        private readonly bool _isPopup;
        private readonly global::Gtk.Window _window;
        private readonly global::Gtk.DrawingArea _area;

        private Size _size = new (800, 600);
        private Size _clientLogical = new (800, 600);
        private Point _location;
        private bool _systemDecorations;
        private bool _canResize = true;
        private bool _closedDelivered;
        private bool _minimized;
        private double _opacity = 1.0;
        private uint _lastEventTime;
        private (double X, double Y) _lastPointer;

        public Gtk4WindowHost (MF.WindowBase owner, bool isPopup)
        {
            _owner = owner;
            _isPopup = isPopup;
            _systemDecorations = !isPopup;

            MF.Theme.WarmupFonts ();

            _window = global::Gtk.Window.New ();
            _window.SetDefaultSize (_size.Width, _size.Height);
            _window.SetDecorated (!isPopup);
            _window.SetResizable (!isPopup);

            _area = global::Gtk.DrawingArea.New ();
            _area.SetHexpand (true);
            _area.SetVexpand (true);
            _area.SetFocusable (true);
            _area.SetDrawFunc (Render);
            _area.OnResize += (_, e) => { _clientLogical = new Size (e.Width, e.Height); };

            _window.SetChild (_area);

            WireLifecycle ();
            WireInput ();
        }

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

        // ── Input ────────────────────────────────────────────────────────────────

        private void WireInput ()
        {
            var click = global::Gtk.GestureClick.New ();
            click.SetButton (0);   // 0 == report every button
            click.OnPressed += (g, e) => {
                _lastEventTime = ((global::Gtk.EventController) g).GetCurrentEventTime ();
                _lastPointer = (e.X, e.Y);
                _area.GrabFocus ();
                var state = ((global::Gtk.EventController) g).GetCurrentEventState ();
                var (x, y) = Device (e.X, e.Y);
                _owner.HandlePointerPressed (Gtk4KeyInterop.ToButton (((global::Gtk.GestureClick) g).GetCurrentButton ()),
                    x, y, Gtk4KeyInterop.ModifierKeys (state));
            };
            click.OnReleased += (g, e) => {
                _lastEventTime = ((global::Gtk.EventController) g).GetCurrentEventTime ();
                _lastPointer = (e.X, e.Y);
                var state = ((global::Gtk.EventController) g).GetCurrentEventState ();
                var (x, y) = Device (e.X, e.Y);
                _owner.HandlePointerReleased (Gtk4KeyInterop.ToButton (((global::Gtk.GestureClick) g).GetCurrentButton ()),
                    x, y, Gtk4KeyInterop.ModifierKeys (state));
            };
            _area.AddController (click);

            var motion = global::Gtk.EventControllerMotion.New ();
            motion.OnMotion += (m, e) => {
                _lastEventTime = ((global::Gtk.EventController) m).GetCurrentEventTime ();
                _lastPointer = (e.X, e.Y);
                var state = ((global::Gtk.EventController) m).GetCurrentEventState ();
                var (x, y) = Device (e.X, e.Y);
                _owner.HandlePointerMoved (Gtk4KeyInterop.ButtonsFromState (state), x, y, Gtk4KeyInterop.ModifierKeys (state));
            };
            motion.OnLeave += (_, _) => {
                var (x, y) = Device (_lastPointer.X, _lastPointer.Y);
                _owner.HandlePointerExited (MF.MouseButtons.None, x, y, MF.Keys.None);
            };
            _area.AddController (motion);

            var scroll = global::Gtk.EventControllerScroll.New (global::Gtk.EventControllerScrollFlags.BothAxes);
            scroll.OnScroll += (s, e) => {
                var state = ((global::Gtk.EventController) s).GetCurrentEventState ();
                var (x, y) = Device (_lastPointer.X, _lastPointer.Y);
                // GTK: +dy scrolls the content down; WinForms wheel delta is +120 per notch scrolling up.
                var delta = new Point ((int) Math.Round (-e.Dx * 120), (int) Math.Round (-e.Dy * 120));
                _owner.HandlePointerWheel (MF.MouseButtons.None, x, y, delta, Gtk4KeyInterop.ModifierKeys (state));
                return true;
            };
            _area.AddController (scroll);

            var keys = global::Gtk.EventControllerKey.New ();
            keys.OnKeyPressed += (_, e) => {
                var handled = _owner.HandleKeyDown (Gtk4KeyInterop.ToKeys (e.Keyval, e.State));

                // GTK's CharacterReceived equivalent is the IM commit; for a first cut, derive the
                // printable character straight off the key value (mirrors the Uno backend).
                var codepoint = Gdk.Functions.KeyvalToUnicode (e.Keyval);
                if (codepoint != 0) {
                    var ch = char.ConvertFromUtf32 ((int) codepoint);
                    if (!string.IsNullOrEmpty (ch) && !char.IsControl (ch[0]))
                        handled |= _owner.HandleTextInput (ch);
                }
                return handled;
            };
            keys.OnKeyReleased += (_, e) => _owner.HandleKeyUp (Gtk4KeyInterop.ToKeys (e.Keyval, e.State));
            _window.AddController (keys);
        }

        // Widget coords are logical; RenderFrame and the neutral input path take device pixels.
        private (int X, int Y) Device (double x, double y)
        {
            var s = Scaling;
            return ((int) Math.Round (x * s), (int) Math.Round (y * s));
        }

        // ── Rendering ────────────────────────────────────────────────────────────

        private void Render (global::Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            _clientLogical = new Size (width, height);
            var scale = Math.Max (1, area.GetScaleFactor ());
            var physW = Math.Max (1, width * scale);
            var physH = Math.Max (1, height * scale);

            // A fresh image surface per frame: once it is used as a paint source cairo takes a snapshot
            // of it, and marking a snapshotted surface dirty on the next pass trips an assertion. Frames
            // are drawn on resize / explicit Invalidate, not continuously, so the per-paint allocation
            // is the same order of cost as the WPF backend's WriteableBitmap present.
            using var surface = new Cairo.ImageSurface (Cairo.Format.Argb32, physW, physH);
            RenderInto (surface, physW, physH, scale);
            surface.MarkDirty ();

            cr.Save ();
            if (scale != 1)
                cr.Scale (1.0 / scale, 1.0 / scale);
            cr.SetSourceSurface (surface, 0, 0);
            cr.Paint ();
            cr.Restore ();
        }

        private unsafe void RenderInto (Cairo.ImageSurface surface, int physW, int physH, double scale)
        {
            var data = surface.GetData ();
            fixed (byte* ptr = data) {
                // Cairo ARGB32 on a little-endian host is byte-order BGRA, premultiplied — SkiaSharp's Bgra8888/Premul.
                var info = new SKImageInfo (physW, physH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var skSurface = SKSurface.Create (info, (IntPtr) ptr, surface.Stride);
                if (skSurface is null)
                    return;

                skSurface.Canvas.Clear (SKColors.Transparent);
                _owner.RenderFrame (skSurface.Canvas, physW, physH, scale);
                skSurface.Canvas.Flush ();
            }
        }

        // ── Geometry ─────────────────────────────────────────────────────────────
        // GTK 4 removed client-side control (and query) of a top-level's screen position, so Location
        // is a best-effort stored value and the PointTo* conversions assume it. Size/ClientSize track
        // the drawing area's allocation once the window is realized.

        public Point Location { get => _location; set => _location = value; }

        public Size Size {
            get => _window.GetRealized () && _area.GetWidth () > 0
                ? new Size (_area.GetWidth (), _area.GetHeight ())
                : _size;
            set {
                _size = value;
                _window.SetDefaultSize (value.Width, value.Height);
            }
        }

        public Size ClientSize => _window.GetRealized () && _area.GetWidth () > 0
            ? new Size (_area.GetWidth (), _area.GetHeight ())
            : _clientLogical;

        public double Scaling {
            get {
                var s = _area.GetScaleFactor ();
                return s > 0 ? s : 1.0;
            }
        }

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

        public void SetCursor (CursorType cursor) => _area.SetCursorFromName (Gtk4KeyInterop.ToCursorName (cursor));

        // GTK 4 window icons come from a themed icon name, not raw PNG bytes — no-op.
        public void SetIcon (byte[]? iconPng) { }

        public Size MinimumSize {
            set => _area.SetSizeRequest (value.IsEmpty ? -1 : value.Width, value.IsEmpty ? -1 : value.Height);
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
                var (x, y) = _lastPointer;
                toplevel.BeginMove (device!, 1, x, y, _lastEventTime);
            }
        }

        public void BeginResizeDrag (WindowEdge edge)
        {
            if (TryGetToplevel (out var toplevel, out var device)) {
                var (x, y) = _lastPointer;
                toplevel.BeginResize (Gtk4KeyInterop.ToSurfaceEdge (edge), device, 1, x, y, _lastEventTime);
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

        public void Invalidate () => _area.QueueDraw ();

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
