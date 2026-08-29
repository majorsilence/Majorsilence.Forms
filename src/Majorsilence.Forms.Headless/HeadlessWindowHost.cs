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
        // Tracked so a test can tell whether a form actually raised an OS window -- the whole question
        // behind Form.TopLevel.
        public bool IsShown { get; private set; }

        public void Show ()
        {
            IsShown = true;
            _owner.OnBackendActivated ();
        }

        // The owner backend the last modal show was given, so a test can confirm the modal path
        // establishes an owner relationship (on the real backends that is the native WM_TRANSIENT_FOR
        // link that keeps a dialog stacked above its parent).
        public IWindowBackend? LastDialogOwner { get; private set; }

        public void ShowDialog (IWindowBackend? owner)
        {
            LastDialogOwner = owner;
            Show ();
        }

        public void Hide () => IsShown = false;

        public void Close ()
        {
            if (_owner.OnBackendClosing ())   // true == cancelled
                return;
            _owner.OnBackendClosed ();
        }

        // Deliberately raises the window, as the real platforms do: Activate orders a window on screen
        // even when it was never shown. Modelling that is what lets a test catch code activating a window
        // its form does not own -- which strands it, because the platform's later Hide then does nothing.
        public void Activate ()
        {
            IsShown = true;
            _owner.OnBackendActivated ();
        }

        /// <summary>Whether Show() also activates. Recorded so tests can assert on it.</summary>
        public bool ShowActivated { get; set; } = true;

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
        //
        // Client (0,0) is offset from the window's own origin by whatever chrome the platform draws
        // above/left of the client area -- a native title bar, typically. Headless has none by default,
        // so the two coincide and every existing measurement is unchanged; ChromeOffset lets a test
        // simulate a platform that does, which is the only way to catch code that treats a window's
        // Location as its client origin. Real windows differ by ~32px there, which is enough for a drag
        // overlay to hit-test its drop guides clean past where they were drawn.
        public static Size ChromeOffset { get; set; }

        public Point PointToClient (Point screen) =>
            new (screen.X - _location.X - ChromeOffset.Width, screen.Y - _location.Y - ChromeOffset.Height);

        public Point PointToScreen (Point client) =>
            new (client.X + _location.X + ChromeOffset.Width, client.Y + _location.Y + ChromeOffset.Height);

        // ── Drag (no chrome in headless) ──
        //
        // Counted rather than ignored: whether a caption drag actually moved the window is the whole
        // question when an application claims that gesture for itself.
        public static int MoveDragCount { get; set; }

        public void BeginMoveDrag () => MoveDragCount++;
        public void BeginResizeDrag (WindowEdge edge) { }

        // ── Rendering ── (headless renders on demand via Render(), so there is nothing to schedule)
        public void Invalidate () { }

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
