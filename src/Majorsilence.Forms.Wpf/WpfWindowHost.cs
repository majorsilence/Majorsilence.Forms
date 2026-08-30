using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Majorsilence.Forms.Backends;
using MF = Majorsilence.Forms;
using WpfWindow = System.Windows.Window;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Wpf
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Majorsilence.Forms window through a real WPF
    /// <see cref="WpfWindow"/> whose content is a <see cref="SkiaWpfElement"/>: each render pass calls
    /// <c>WindowBase.RenderFrame</c>, and WPF mouse/keyboard events are translated into the neutral
    /// <c>WindowBase.Handle*</c> path.
    ///
    /// Popups (menus, combo dropdowns, tooltips) are borderless, non-activating top-most windows.
    /// </summary>
    internal sealed class WpfWindowHost : IWindowBackend, INativeControlHostBackend, IDisposable
    {
        private readonly MF.WindowBase _owner;
        private readonly bool _isPopup;
        private readonly WpfWindow _window;
        private readonly SkiaWpfElement _skia;
        private readonly Canvas _overlayLayer;
        private readonly Dictionary<MF.NativeControlHost, FrameworkElement> _overlays = new ();

        private Size _size = new (800, 600);
        private bool _systemDecorations;
        private bool _canResize = true;
        private bool _closedDelivered;
        private double _chromeWidth, _chromeHeight;   // Window.ActualWidth − client, measured once realized.

        internal WpfWindow NativeWindow => _window;

        private static bool _fontWarmedUp;

        private static void EnsureFontsWarmedUp ()
        {
            if (_fontWarmedUp) return;
            _fontWarmedUp = true;
            MF.Theme.WarmupFonts ();
        }

        public WpfWindowHost (MF.WindowBase owner, bool isPopup)
        {
            EnsureFontsWarmedUp ();
            _owner = owner;
            _isPopup = isPopup;
            _systemDecorations = !isPopup;

            _skia = new SkiaWpfElement (() => _owner);
            _overlayLayer = new Canvas { IsHitTestVisible = true, Background = null };

            var root = new Grid ();
            root.Children.Add (_skia);
            root.Children.Add (_overlayLayer);

            _window = new WpfWindow
            {
                Content = root,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowInTaskbar = !isPopup,
                Topmost = isPopup,
                ShowActivated = !isPopup,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                Width = _size.Width,
                Height = _size.Height,
            };

            ApplyDecorations ();
            WireLifecycle ();
        }

        private void WireLifecycle ()
        {
            _window.Activated += (_, _) => _owner.OnBackendActivated ();
            _window.Deactivated += (_, _) => _owner.OnBackendDeactivated ();
            _window.LocationChanged += (_, _) => _owner.OnBackendMoved ();
            _window.StateChanged += (_, _) => _skia.RequestRender ();

            _window.SourceInitialized += (_, _) =>
            {
                _window.Dispatcher.BeginInvoke (new Action (() =>
                {
                    _chromeWidth = Math.Max (0, _window.ActualWidth - _skia.ActualWidth);
                    _chromeHeight = Math.Max (0, _window.ActualHeight - _skia.ActualHeight);
                    ApplyClientSize ();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };

            _window.Closing += (_, e) =>
            {
                if (_owner.OnBackendClosing ())   // true == cancel
                    e.Cancel = true;
            };
            _window.Closed += (_, _) =>
            {
                if (_closedDelivered)
                    return;
                _closedDelivered = true;
                _owner.OnBackendClosed ();
            };
        }

        // ── Geometry ─────────────────────────────────────────────────────────────
        // WPF works in DIPs. The seam's Size/ClientSize are logical (== DIPs here); Location/PointTo*
        // screen coordinates are physical pixels, so Scaling converts those.

        public Point Location
        {
            get
            {
                var s = Scaling;
                return new Point ((int) Math.Round (_window.Left * s), (int) Math.Round (_window.Top * s));
            }
            set
            {
                var s = Scaling;
                _window.Left = value.X / s;
                _window.Top = value.Y / s;
            }
        }

        public Size Size
        {
            get
            {
                if (_skia.ActualWidth > 0)
                    return new Size ((int) Math.Round (_skia.ActualWidth), (int) Math.Round (_skia.ActualHeight));
                return _size;
            }
            set
            {
                _size = value;
                ApplyClientSize ();
            }
        }

        public Size ClientSize => Size;

        public double Scaling => _skia.Scaling;

        private void ApplyClientSize ()
        {
            _window.Width = _size.Width + _chromeWidth;
            _window.Height = _size.Height + _chromeHeight;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public void Show ()
        {
            _window.ShowActivated = !_isPopup && ShowActivated;
            _window.Show ();
            if (_isPopup)
                _owner.OnBackendActivated ();
        }

        public void ShowDialog (IWindowBackend? owner)
        {
            // Majorsilence.Forms' own modal machinery (parent-disable + RunModalLoop) lives above the
            // seam; the backend only needs to show the window with the right owner for z-order.
            if (owner is WpfWindowHost host)
                _window.Owner = host._window;
            _window.Show ();
        }

        public void Hide () => _window.Hide ();

        public void Close ()
        {
            if (_window.IsLoaded || _window.IsVisible)
            {
                _window.Close ();
                return;
            }

            // Never shown: WPF Window.Close on an unshown window still raises Closing/Closed, but guard
            // against the not-delivered case anyway.
            if (_owner.OnBackendClosing ())
                return;
            if (!_closedDelivered)
            {
                _closedDelivered = true;
                _owner.OnBackendClosed ();
            }
            _window.Close ();
        }

        public void Activate ()
        {
            if (!_window.IsVisible)
                _window.Show ();
            _window.Activate ();
        }

        public bool ShowActivated { get; set; } = true;

        // ── Appearance / behaviour ───────────────────────────────────────────────

        public string Title { set => _window.Title = value ?? string.Empty; }

        public bool Topmost
        {
            get => _window.Topmost;
            set => _window.Topmost = value;
        }

        public void SetSystemDecorations (bool useSystemDecorations)
        {
            _systemDecorations = useSystemDecorations;
            ApplyDecorations ();
        }

        private void ApplyDecorations ()
        {
            if (_isPopup || !_systemDecorations)
            {
                _window.WindowStyle = WindowStyle.None;
                _window.ResizeMode = _isPopup || !_canResize ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
                _window.AllowsTransparency = _isPopup;
                return;
            }

            _window.WindowStyle = WindowStyle.SingleBorderWindow;
            _window.ResizeMode = _canResize ? ResizeMode.CanResize : ResizeMode.NoResize;
        }

        public void SetCursor (CursorType cursor) => _skia.Cursor = WpfKeyInterop.ToCursor (cursor);

        public void SetIcon (byte[]? iconPng)
        {
            if (_isPopup)
                return;

            if (iconPng is null || iconPng.Length == 0)
            {
                _window.Icon = null;
                return;
            }

            try
            {
                using var stream = new System.IO.MemoryStream (iconPng);
                _window.Icon = BitmapFrame.Create (stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch
            {
                // Icon is best-effort.
            }
        }

        public Size MinimumSize
        {
            set
            {
                _window.MinWidth = value.IsEmpty ? 0 : value.Width + _chromeWidth;
                _window.MinHeight = value.IsEmpty ? 0 : value.Height + _chromeHeight;
            }
        }

        public Size MaximumSize
        {
            set
            {
                _window.MaxWidth = value.IsEmpty ? double.PositiveInfinity : value.Width + _chromeWidth;
                _window.MaxHeight = value.IsEmpty ? double.PositiveInfinity : value.Height + _chromeHeight;
            }
        }

        public bool CanResize
        {
            get => _canResize;
            set
            {
                _canResize = value;
                ApplyDecorations ();
            }
        }

        public bool ShowInTaskbar
        {
            get => _window.ShowInTaskbar;
            set => _window.ShowInTaskbar = value;
        }

        public double Opacity
        {
            get => _window.Opacity;
            set => _window.Opacity = value;
        }

        // MF.FormWindowState and WPF WindowState share Normal/Minimized/Maximized values.
        public MF.FormWindowState WindowState
        {
            get => (MF.FormWindowState) (int) _window.WindowState;
            set => _window.WindowState = (System.Windows.WindowState) (int) value;
        }

        public bool Enabled
        {
            get => _window.IsEnabled;
            set => _window.IsEnabled = value;
        }

        public IntPtr TryGetPlatformHandle ()
        {
            try { return new WindowInteropHelper (_window).Handle; }
            catch { return IntPtr.Zero; }
        }

        // ── Coordinate conversion ────────────────────────────────────────────────
        // Screen coordinates are physical pixels; client coordinates arrive as physical pixels too.

        public Point PointToClient (Point screen)
        {
            try
            {
                var p = _skia.PointFromScreen (new System.Windows.Point (screen.X, screen.Y));
                var s = Scaling;
                return new Point ((int) Math.Round (p.X * s), (int) Math.Round (p.Y * s));
            }
            catch { return screen; }
        }

        public Point PointToScreen (Point client)
        {
            try
            {
                var s = Scaling;
                var p = _skia.PointToScreen (new System.Windows.Point (client.X / s, client.Y / s));
                return new Point ((int) Math.Round (p.X), (int) Math.Round (p.Y));
            }
            catch { return client; }
        }

        // ── Drag (custom chrome) ─────────────────────────────────────────────────

        public void BeginMoveDrag ()
        {
            try { _window.DragMove (); }
            catch { /* DragMove requires an active left-button press; ignore otherwise. */ }
        }

        public void BeginResizeDrag (WindowEdge edge)
        {
#if !NET48
            // net48 is Windows-only anyway; OperatingSystem.IsWindows is .NET Core 3.0+.
            if (!OperatingSystem.IsWindows ())
                return;
#endif

            var handle = TryGetPlatformHandle ();
            if (handle == IntPtr.Zero)
                return;

            const int WM_SYSCOMMAND = 0x0112;
            const int SC_SIZE = 0xF000;
            var direction = edge switch
            {
                WindowEdge.West => 1, WindowEdge.East => 2,
                WindowEdge.North => 3, WindowEdge.NorthWest => 4, WindowEdge.NorthEast => 5,
                WindowEdge.South => 6, WindowEdge.SouthWest => 7, WindowEdge.SouthEast => 8,
                _ => 0,
            };
            if (direction == 0)
                return;

            _ = ReleaseCapture ();
            _ = SendMessage (handle, WM_SYSCOMMAND, (IntPtr) (SC_SIZE + direction), IntPtr.Zero);
        }

        [DllImport ("user32.dll")]
        private static extern bool ReleaseCapture ();

        [DllImport ("user32.dll")]
        private static extern IntPtr SendMessage (IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // Optional IWindowBackend members with no meaning for the plain WPF host.
        public void SetCaptionRegions (IReadOnlyList<Rectangle> captionRects) { }
        public void SetShaped (bool shaped) { }
        public void SetTextInputActive (bool active, TextInputKind kind) { }
        public void SetExtendClientIntoTitleBar (bool extend, int titleBarHeight) { }

        // ── Rendering ────────────────────────────────────────────────────────────

        public void Invalidate () => _skia.RequestRender ();

        // ── INativeControlHostBackend (real WPF elements inside the Majorsilence scene) ───────────

        void INativeControlHostBackend.AttachNativeControl (MF.NativeControlHost host, object nativeControl)
            => NativeOverlay.Attach (_overlayLayer, _overlays, host, nativeControl);

        void INativeControlHostBackend.UpdateNativeControl (MF.NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible)
            => NativeOverlay.Update (_overlays, host, logicalBounds, clipBounds, visible);

        void INativeControlHostBackend.DetachNativeControl (MF.NativeControlHost host)
            => NativeOverlay.Detach (_overlayLayer, _overlays, host);

        public void Dispose ()
        {
            try { _window.Close (); } catch { }
        }

        // ── File/folder pickers ──────────────────────────────────────────────────

        public Task<string[]> ShowOpenFileDialog (OpenFileRequest request)
            => Task.FromResult (WpfDialogs.ShowOpenFileDialog (_window, request));

        public Task<string?> ShowSaveFileDialog (SaveFileRequest request)
            => Task.FromResult (WpfDialogs.ShowSaveFileDialog (_window, request));

        public Task<string?> ShowOpenFolderDialog (FolderDialogRequest request)
            => Task.FromResult (WpfDialogs.ShowOpenFolderDialog (_window, request));
    }
}
