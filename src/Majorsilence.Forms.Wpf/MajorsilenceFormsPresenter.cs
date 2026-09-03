using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Majorsilence.Forms.Backends;
using MF = Majorsilence.Forms;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Wpf
{
    /// <summary>
    /// A WPF element that embeds a Majorsilence.Forms scene inside an existing WPF application. Drop it
    /// into any WPF visual tree (or use <c>myMfControl.ToWpfElement()</c>) and assign <see cref="Content"/>
    /// a Majorsilence.Forms control tree; it renders through SkiaSharp into this element and forwards
    /// WPF mouse/keyboard input into the Majorsilence.Forms pipeline. No top-level OS window is created.
    ///
    /// Popups opened from the embedded content (combo dropdowns, menus, tooltips) are real borderless
    /// windows created by <see cref="WpfPlatformBackend"/>, which the presenter installs as the active
    /// backend automatically if none is configured.
    ///
    /// The WPF counterpart of the Avalonia / Uno / WinForms <c>MajorsilenceFormsPresenter</c> classes.
    /// </summary>
    public class MajorsilenceFormsPresenter : Grid, IWindowBackend, INativeControlHostBackend, IDisposable
    {
        private readonly SkiaWpfElement _skia;
        private readonly Canvas _overlayLayer;
        private readonly MF.HostedSurface _host;
        private readonly Dictionary<MF.NativeControlHost, FrameworkElement> _overlays = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="MajorsilenceFormsPresenter"/> class. Installs a
        /// <see cref="WpfPlatformBackend"/> as the active Majorsilence.Forms backend if none is
        /// configured yet.
        /// </summary>
        public MajorsilenceFormsPresenter ()
        {
            WpfPlatformBackend? backend = null;
            try { backend = Platform.Backend as WpfPlatformBackend; } catch { /* none configured yet */ }

            if (backend is null && !ForeignBackendActive ())
            {
                backend = new WpfPlatformBackend ();
                Platform.Backend = backend;
            }
            backend?.Initialize ();

            _skia = new SkiaWpfElement (() => _host);
            _overlayLayer = new Canvas { IsHitTestVisible = true, Background = null };
            Children.Add (_skia);
            Children.Add (_overlayLayer);

            _host = new MF.HostedSurface (this);

            SetClearColorFromBackground ();
            Loaded += (_, _) => { SetClearColorFromBackground (); _skia.RequestRender (); };
            Unloaded += (_, _) => Dispose ();
        }

        private bool _disposed;

        /// <summary>Detaches the embedded scene from the application (idempotent; also runs on Unloaded).</summary>
        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;
            _host.Dispose ();
            GC.SuppressFinalize (this);
        }

        private static bool ForeignBackendActive ()
        {
            try { return Platform.Backend is not null; }
            catch { return false; }
        }

        private void SetClearColorFromBackground ()
        {
            _skia.ClearColor = Background is SolidColorBrush b
                ? new SkiaSharp.SKColor (b.Color.R, b.Color.G, b.Color.B, b.Color.A)
                : (SkiaSharp.SKColor?) null;
        }

        /// <summary>Gets or sets the root Majorsilence.Forms control hosted by this presenter.</summary>
        [Browsable (false)]
        public MF.Control? Content
        {
            get => _host.Content;
            set => _host.Content = value;
        }

        /// <summary>Gets the underlying hosted surface (advanced scenarios: multiple roots, events).</summary>
        [Browsable (false)]
        public MF.HostedSurface Surface => _host;

        /// <inheritdoc/>
        protected override System.Windows.Size ArrangeOverride (System.Windows.Size arrangeSize)
        {
            var result = base.ArrangeOverride (arrangeSize);
            _skia.RequestRender ();   // RenderFrame re-lays-out the scene from the new surface size
            return result;
        }

        // ── IWindowBackend ───────────────────────────────────────────────────────
        // The presenter is the embedded scene's "window": screen coordinates are physical pixels,
        // client coordinates are logical (DIPs).

        /// <summary>Gets the device scale factor of the monitor this presenter is on.</summary>
        public double Scaling => _skia.Scaling;

        Point IWindowBackend.Location
        {
            get
            {
                try
                {
                    if (!IsLoaded)
                        return Point.Empty;
                    var p = _skia.PointToScreen (new System.Windows.Point (0, 0));
                    return new Point ((int) Math.Round (p.X), (int) Math.Round (p.Y));
                }
                catch { return Point.Empty; }
            }
            set { /* owned by the host layout */ }
        }

        Size IWindowBackend.Size
        {
            get => new ((int) Math.Round (_skia.ActualWidth), (int) Math.Round (_skia.ActualHeight));
            set { /* owned by the host layout */ }
        }

        Size IWindowBackend.ClientSize => ((IWindowBackend) this).Size;

        void IWindowBackend.Show () { }
        void IWindowBackend.ShowDialog (IWindowBackend? owner) { }
        void IWindowBackend.Hide () => Visibility = Visibility.Hidden;
        void IWindowBackend.Close () { }
        void IWindowBackend.Activate () => _skia.Focus ();
        bool IWindowBackend.ShowActivated { get; set; } = true;

        string IWindowBackend.Title { set { } }
        bool IWindowBackend.Topmost { get; set; }
        void IWindowBackend.SetSystemDecorations (bool useSystemDecorations) { }
        void IWindowBackend.SetCursor (CursorType cursor) => _skia.Cursor = WpfKeyInterop.ToCursor (cursor);
        void IWindowBackend.SetIcon (byte[]? iconPng) { }
        Size IWindowBackend.MinimumSize { set { } }
        Size IWindowBackend.MaximumSize { set { } }
        bool IWindowBackend.CanResize { get; set; }
        bool IWindowBackend.ShowInTaskbar { get; set; }
        double IWindowBackend.Opacity { get; set; } = 1.0;
        MF.FormWindowState IWindowBackend.WindowState { get; set; } = MF.FormWindowState.Normal;

        bool IWindowBackend.Enabled
        {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        IntPtr IWindowBackend.TryGetPlatformHandle () => IntPtr.Zero;

        Point IWindowBackend.PointToClient (Point screen)
        {
            try
            {
                if (!IsLoaded)
                    return screen;
                var p = _skia.PointFromScreen (new System.Windows.Point (screen.X, screen.Y));
                var s = Scaling;
                return new Point ((int) Math.Round (p.X * s), (int) Math.Round (p.Y * s));
            }
            catch { return screen; }
        }

        Point IWindowBackend.PointToScreen (Point client)
        {
            try
            {
                if (!IsLoaded)
                    return client;
                var s = Scaling;
                var p = _skia.PointToScreen (new System.Windows.Point (client.X / s, client.Y / s));
                return new Point ((int) Math.Round (p.X), (int) Math.Round (p.Y));
            }
            catch { return client; }
        }

        void IWindowBackend.BeginMoveDrag () { }
        void IWindowBackend.BeginResizeDrag (WindowEdge edge) { }
        void IWindowBackend.SetCaptionRegions (IReadOnlyList<Rectangle> captionRects) { }
        void IWindowBackend.SetShaped (bool shaped) { }
        void IWindowBackend.SetTextInputActive (bool active, TextInputKind kind) { }
        void IWindowBackend.SetExtendClientIntoTitleBar (bool extend, int titleBarHeight) { }
        bool IWindowBackend.IsSingleView => false;

        void IWindowBackend.Invalidate () => _skia.RequestRender ();

        // ── INativeControlHostBackend ────────────────────────────────────────────

        void INativeControlHostBackend.AttachNativeControl (MF.NativeControlHost host, object nativeControl)
            => NativeOverlay.Attach (_overlayLayer, _overlays, host, nativeControl);

        void INativeControlHostBackend.UpdateNativeControl (MF.NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible)
            => NativeOverlay.Update (_overlays, host, logicalBounds, clipBounds, visible);

        void INativeControlHostBackend.DetachNativeControl (MF.NativeControlHost host)
            => NativeOverlay.Detach (_overlayLayer, _overlays, host);

        // ── File/folder pickers (WPF common dialogs, owned by the host window) ────

        private System.Windows.Window? OwnerWindow => System.Windows.Window.GetWindow (this);

        Task<string[]> IWindowBackend.ShowOpenFileDialog (OpenFileRequest request)
            => Task.FromResult (WpfDialogs.ShowOpenFileDialog (OwnerWindow, request));

        Task<string?> IWindowBackend.ShowSaveFileDialog (SaveFileRequest request)
            => Task.FromResult (WpfDialogs.ShowSaveFileDialog (OwnerWindow, request));

        Task<string?> IWindowBackend.ShowOpenFolderDialog (FolderDialogRequest request)
            => Task.FromResult (WpfDialogs.ShowOpenFolderDialog (OwnerWindow, request));
    }
}
