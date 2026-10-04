using System;
using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using SkiaSharp;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> for one window on the terminal's single screen. A top-level window
    /// always fills the terminal (its size is the terminal's, and resizing it is ignored); a popup keeps the
    /// size and position its owner gives it and is composited over the main window.
    /// </summary>
    internal sealed class TerminalWindowHost : IWindowBackend
    {
        private readonly TerminalPlatformBackend _backend;
        private readonly WindowBase _owner;
        private Size _popupSize = new (200, 100);
        private Point _location;

        public TerminalWindowHost (TerminalPlatformBackend backend, WindowBase owner, bool isPopup)
        {
            _backend = backend;
            _owner = owner;
            IsPopup = isPopup;
        }

        public bool IsPopup { get; }
        public WindowBase Owner => _owner;

        /// <summary>The window's footprint on the screen in device pixels, for deciding which window a pointer event lands on.</summary>
        public Rectangle ScreenBounds {
            get {
                var size = Size;
                return new Rectangle (Location, new Size ((int) Math.Round (size.Width * Scaling), (int) Math.Round (size.Height * Scaling)));
            }
        }

        // ── Geometry ──
        public Point Location { get => IsPopup ? _location : Point.Empty; set => _location = value; }

        public Size Size {
            get => IsPopup ? _popupSize : LogicalFullScreen ();
            set {
                if (IsPopup)
                    _popupSize = value;
                // A top-level window cannot be any size but the terminal's.
            }
        }

        public Size ClientSize => Size;
        public double Scaling => _backend.Scaling;

        private Size LogicalFullScreen ()
        {
            var px = _backend.PixelSize;
            return new Size ((int) Math.Max (1, Math.Round (px.Width / Scaling)), (int) Math.Max (1, Math.Round (px.Height / Scaling)));
        }

        // ── Lifecycle ──
        public bool ShowActivated { get; set; } = true;

        public void Show ()
        {
            _backend.Shown (this, ShowActivated);
            _owner.OnBackendActivated ();
        }

        public void ShowDialog (IWindowBackend? owner) => Show ();

        public void Hide () => _backend.Hidden (this);

        public void Close ()
        {
            if (_owner.OnBackendClosing ())   // true == cancelled
                return;

            _backend.Hidden (this);
            _owner.OnBackendClosed ();
        }

        public void Activate ()
        {
            _backend.Shown (this, activate: true);
            _owner.OnBackendActivated ();
        }

        // ── Appearance / behaviour ──
        public string Title { set { if (!IsPopup) _backend.SetTitle (value ?? string.Empty); } }
        public bool Topmost { get; set; }

        // The terminal draws no window chrome; a Form's own self-drawn title bar is all there is.
        public void SetSystemDecorations (bool useSystemDecorations) { }
        public void SetCursor (CursorType cursor) { }
        public void SetIcon (byte[]? iconPng) { }
        public Size MinimumSize { set { } }
        public Size MaximumSize { set { } }
        public bool CanResize { get; set; }
        public bool ShowInTaskbar { get; set; }
        public double Opacity { get; set; } = 1.0;
        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;
        public bool Enabled { get; set; } = true;

        // ── Coordinate conversion: the screen origin is the terminal's top-left. ──
        public Point PointToClient (Point screen) =>
            new ((int) Math.Round ((screen.X - Location.X) / Scaling), (int) Math.Round ((screen.Y - Location.Y) / Scaling));

        public Point PointToScreen (Point client) =>
            new ((int) Math.Round (client.X * Scaling) + Location.X, (int) Math.Round (client.Y * Scaling) + Location.Y);

        // ── Drag: there is no window manager to move or resize a window. ──
        public void BeginMoveDrag () { }
        public void BeginResizeDrag (WindowEdge edge) { }

        public void Invalidate () => _backend.Invalidate ();

        // ── Pickers: no native dialogs in a terminal; behave as a cancelled pick. ──
        public Task<string[]> ShowOpenFileDialog (OpenFileRequest request) => Task.FromResult (Array.Empty<string> ());
        public Task<string?> ShowSaveFileDialog (SaveFileRequest request) => Task.FromResult<string?> (null);
        public Task<string?> ShowOpenFolderDialog (FolderDialogRequest request) => Task.FromResult<string?> (null);

        /// <summary>Paints this window onto the shared screen canvas at its location.</summary>
        public void RenderInto (SKCanvas screen)
        {
            var scaling = Scaling;
            var size = Size;
            var physW = Math.Max (1, (int) Math.Round (size.Width * scaling));
            var physH = Math.Max (1, (int) Math.Round (size.Height * scaling));

            screen.Save ();
            screen.Translate (Location.X, Location.Y);
            screen.ClipRect (new SKRect (0, 0, physW, physH));
            _owner.RenderFrame (screen, physW, physH, scaling);
            screen.Restore ();
        }
    }
}
