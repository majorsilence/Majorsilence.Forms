using System;
using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using SkiaSharp;

namespace Majorsilence.Forms.Headless
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that "presents" by rendering the owning window into an
    /// offscreen SkiaSharp surface. Geometry/appearance are plain in-memory state; input and
    /// chrome operations are no-ops. Mirrors the structure a real (Uno/Avalonia) window host follows.
    /// </summary>
    internal sealed class HeadlessWindowHost : IWindowBackend
    {
        private readonly WindowBase _owner;
        private Size _size = new (800, 600);
        private Point _location;
        private bool _dirty = true;

        public HeadlessWindowHost (WindowBase owner) => _owner = owner;

        // ── Geometry ──
        public Point Location { get => _location; set => _location = value; }
        public Size Size { get => _size; set => _size = value; }
        public Size ClientSize => _size;

        // Defaults to 1.0; set the MF_HEADLESS_SCALE env var to simulate a HiDPI display (e.g. "2").
        public double Scaling =>
            double.TryParse (Environment.GetEnvironmentVariable ("MF_HEADLESS_SCALE"), out var s) && s > 0 ? s : 1.0;

        // ── Lifecycle ──
        // Rendering is on-demand (CapturePng), not on Show — Show only activates the window.
        public void Show () => _owner.OnBackendActivated ();

        public void ShowDialog (IWindowBackend? owner) => Show ();

        public void Hide () { }

        public void Close ()
        {
            if (_owner.OnBackendClosing ())   // true == cancelled
                return;
            _owner.OnBackendClosed ();
        }

        public void Activate () => _owner.OnBackendActivated ();

        // ── Appearance / behaviour ──
        public string Title { set { } }
        public bool Topmost { get; set; }
        public void SetSystemDecorations (bool useSystemDecorations) { }
        public void SetCursor (CursorType cursor) { }
        public void SetIcon (byte[]? iconPng) { }
        public Size MinimumSize { set { } }
        public Size MaximumSize { set { } }
        public bool CanResize { get; set; } = true;
        public bool ShowInTaskbar { get; set; } = true;
        public double Opacity { get; set; } = 1.0;
        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;
        public bool Enabled { get; set; } = true;

        // ── Coordinate conversion ──
        public Point PointToClient (Point screen) => new (screen.X - _location.X, screen.Y - _location.Y);
        public Point PointToScreen (Point client) => new (client.X + _location.X, client.Y + _location.Y);

        // ── Drag (no chrome in headless) ──
        public void BeginMoveDrag () { }
        public void BeginResizeDrag (WindowEdge edge) { }

        // ── Rendering ──
        public void Invalidate () => _dirty = true;

        // ── Pickers (unavailable headless) ──
        public Task<string[]> ShowOpenFileDialog (OpenFileRequest request) => Task.FromResult (Array.Empty<string> ());
        public Task<string?> ShowSaveFileDialog (SaveFileRequest request) => Task.FromResult<string?> (null);
        public Task<string?> ShowOpenFolderDialog (FolderDialogRequest request) => Task.FromResult<string?> (null);

        /// <summary>Renders the current frame into a fresh offscreen surface and returns the snapshot.</summary>
        internal SKImage Render ()
        {
            var scaling = Scaling;
            var physW = Math.Max (1, (int) (_size.Width * scaling));
            var physH = Math.Max (1, (int) (_size.Height * scaling));

            using var surface = SKSurface.Create (new SKImageInfo (physW, physH, SKColorType.Bgra8888, SKAlphaType.Premul));
            _owner.RenderFrame (surface.Canvas, physW, physH, scaling);
            surface.Canvas.Flush ();
            _dirty = false;
            return surface.Snapshot ();
        }

        /// <summary>Renders the current frame and encodes it as PNG bytes.</summary>
        internal byte[] CapturePng ()
        {
            using var image = Render ();
            using var data = image.Encode (SKEncodedImageFormat.Png, 100);
            return data.ToArray ();
        }
    }
}
