using System;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Continuum.Forms.Backends;
using SkiaSharp.Views.Windows;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Continuum.Forms.Uno
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Continuum.Forms window through a Uno
    /// <c>SKXamlCanvas</c>: its <c>PaintSurface</c> calls <c>WindowBase.RenderFrame</c>, and Uno
    /// pointer/keyboard/character events are translated into the neutral <c>WindowBase.Handle*</c> path.
    ///
    /// Top-level windows use a <see cref="Window"/>. Popups (menus, combo dropdowns) use an in-window
    /// <see cref="Popup"/> overlay parented to the main window's XamlRoot — Uno's macOS Skia head does
    /// not place/strip-chrome secondary <see cref="Window"/>s correctly (AppWindow.Position returns
    /// (0,0), Move uses an AppKit bottom-left origin, and the title bar can't be removed).
    /// </summary>
    internal sealed class UnoWindowHost : IWindowBackend
    {
        private readonly WindowBase _owner;
        private readonly bool _isPopup;
        private readonly UnoWindowHost? _parentHost;
        private readonly CursorCanvas _canvas;

        // Exactly one of these is non-null: _window for top-level windows, _popup for overlays.
        private readonly Window? _window;
        private readonly Popup? _popup;

        private Size _size = new (800, 600);
        private Point _location;
        private bool _systemDecorations;

        private bool PopupMode => _popup is not null;

        public UnoWindowHost (WindowBase owner, bool isPopup, UnoWindowHost? parentHost)
        {
            _owner = owner;
            _isPopup = isPopup;
            _parentHost = parentHost;
            _systemDecorations = !isPopup;

            _canvas = new CursorCanvas ();
            _canvas.PaintSurface += OnPaintSurface;
            // On the Windows/X11 heads the canvas only receives keyboard events while it holds focus; grab it
            // once it's in the visual tree (and on Show / pointer press). On macOS routed key events never
            // reach the canvas, so also hook the native host's KeyDown (see WireMacOSKeyboard).
            _canvas.Loaded += (_, _) => { TryFocus (); WireMacOSKeyboard (); };

            WireInput ();

            if (isPopup && parentHost is not null) {
                // In-window overlay — no separate OS window, so no chrome / positioning quirks.
                _popup = new Popup { Child = _canvas };
            } else {
                _window = new Window { Content = _canvas };
                WireLifecycle ();
                ApplyDecorations ();
            }
        }

        internal Microsoft.UI.Xaml.XamlRoot? CanvasXamlRoot => _canvas.XamlRoot;
        internal double HostScaling => _canvas.XamlRoot?.RasterizationScale ?? 1.0;

        private Microsoft.UI.Windowing.OverlappedPresenter? Presenter
            => _window?.AppWindow?.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;

        // ── Geometry ──
        public Point Location {
            get {
                if (PopupMode)
                    return _location;
                try {
                    var p = _window!.AppWindow?.Position;
                    if (p is not null)
                        return new Point (p.Value.X, p.Value.Y);
                } catch { }
                return _location;
            }
            set { _location = value; if (!PopupMode) TryMove (value); }
        }

        public Size Size {
            get => _size;
            set {
                _size = value;
                if (PopupMode) {
                    _canvas.Width = value.Width;
                    _canvas.Height = value.Height;
                } else {
                    TryResize (value);
                }
            }
        }

        public Size ClientSize => _size;

        public double Scaling => _canvas.XamlRoot?.RasterizationScale ?? _parentHost?.HostScaling ?? 1.0;

        private void TryResize (Size size)
        {
            var s = Scaling;
            try { _window!.AppWindow?.Resize (new Windows.Graphics.SizeInt32 { Width = (int) (size.Width * s), Height = (int) (size.Height * s) }); } catch { }
        }

        private void TryMove (Point location)
        {
            try { _window!.AppWindow?.Move (new Windows.Graphics.PointInt32 { X = location.X, Y = location.Y }); } catch { }
        }

        // ── Lifecycle ──
        public void Show ()
        {
            if (PopupMode) {
                ShowPopup ();
                return;
            }

            ApplyDecorations ();
            _window!.Activate ();
            TryFocus ();
            // Activation/focus and the native host can settle a tick later; retry on the dispatcher too.
            _canvas.DispatcherQueue?.TryEnqueue (() => { TryFocus (); WireMacOSKeyboard (); });
            WireMacOSKeyboard ();
        }

        // On the macOS Skia head the canvas never receives routed XAML key events (focus is correct, but the
        // managed input pipeline doesn't surface KeyDown to an SKXamlCanvas). The native MacOSWindowHost does
        // expose a KeyDown event, so hook that directly (via reflection — the host type is internal). No-op on
        // other heads (the type isn't present), where the routed handlers above carry the keyboard.
        private bool _macKeyboardWired;

        private void WireMacOSKeyboard ()
        {
            if (_macKeyboardWired || _window is null)
                return;
            try {
                var hostType = Type.GetType ("Uno.UI.Runtime.Skia.MacOS.MacOSWindowHost, Uno.UI.Runtime.Skia.MacOS");
                if (hostType is null)
                    return;   // not the macOS head

                var windowsField = hostType.GetField ("_windows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var winField = hostType.GetField ("_winUIWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var keyDownEvt = hostType.GetEvent ("KeyDown");
                var keyUpEvt = hostType.GetEvent ("KeyUp");
                if (windowsField?.GetValue (null) is not System.Collections.IDictionary dict || winField is null || keyDownEvt is null)
                    return;

                foreach (var value in dict.Values) {
                    if (value is null)
                        continue;
                    var tryGet = value.GetType ().GetMethod ("TryGetTarget");
                    if (tryGet is null)
                        continue;
                    var args = new object?[] { null };
                    if (tryGet.Invoke (value, args) is not true || args[0] is not { } host)
                        continue;
                    if (!ReferenceEquals (winField.GetValue (host), _window))
                        continue;

                    keyDownEvt.AddEventHandler (host, new Windows.Foundation.TypedEventHandler<object, Windows.UI.Core.KeyEventArgs> (OnMacKeyDown));
                    keyUpEvt?.AddEventHandler (host, new Windows.Foundation.TypedEventHandler<object, Windows.UI.Core.KeyEventArgs> (OnMacKeyUp));
                    _macKeyboardWired = true;
                    return;
                }
            } catch {
                // Reflection into the internal host is best-effort; the routed handlers remain as a fallback.
            }
        }

        private void OnMacKeyDown (object sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (_owner.HandleKeyDown (UnoKeyInterop.ToKeys (e.VirtualKey))) e.Handled = true;
        }

        private void OnMacKeyUp (object sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (_owner.HandleKeyUp (UnoKeyInterop.ToKeys (e.VirtualKey))) e.Handled = true;
        }

        // Give the drawing canvas keyboard focus so KeyDown/KeyUp/character events route to it (Windows/X11).
        private void TryFocus ()
        {
            try { _canvas.Focus (FocusState.Programmatic); } catch { }
        }

        private void ShowPopup ()
        {
            _canvas.Width = _size.Width;
            _canvas.Height = _size.Height;

            var xamlRoot = _parentHost?.CanvasXamlRoot;
            if (xamlRoot is not null)
                _popup!.XamlRoot = xamlRoot;

            // _location is physical, relative to the parent window's origin (Control.PointToScreen adds
            // the parent window position, which is (0,0) on Uno macOS). Popup offsets are logical and
            // window-relative, so subtract the parent's reported position and divide by scaling.
            var scaling = _parentHost?.HostScaling ?? 1.0;
            if (scaling <= 0) scaling = 1.0;
            var parentPos = _parentHost?.Location ?? Point.Empty;

            _popup!.HorizontalOffset = (_location.X - parentPos.X) / scaling;
            _popup!.VerticalOffset = (_location.Y - parentPos.Y) / scaling;

            _popup!.IsOpen = true;
            _owner.OnBackendActivated ();
            TryFocus ();
            _canvas.Invalidate ();
        }

        public void ShowDialog (IWindowBackend? owner) => Show ();

        public void Hide ()
        {
            if (PopupMode) {
                _popup!.IsOpen = false;
                return;
            }
            try { _window!.AppWindow?.Hide (); } catch { }
        }

        public void Close ()
        {
            if (PopupMode) {
                _popup!.IsOpen = false;
                _owner.OnBackendClosed ();
                return;
            }
            _window!.Close ();
        }

        public void Activate ()
        {
            if (PopupMode)
                _owner.OnBackendActivated ();
            else
                _window!.Activate ();
        }

        private void WireLifecycle ()
        {
            _window!.Activated += (_, e) => {
                if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
                    _owner.OnBackendDeactivated ();
                else
                    _owner.OnBackendActivated ();
            };

            _window!.Closed += (_, _) => _owner.OnBackendClosed ();
        }

        // ── Appearance / behaviour ──
        public string Title { set { try { if (_window is not null) _window.Title = value; } catch { } } }
        public bool Topmost { get; set; }

        public void SetSystemDecorations (bool useSystemDecorations)
        {
            _systemDecorations = useSystemDecorations;
            ApplyDecorations ();
        }

        // Borderless when system decorations are off (top-level windows only; popups are chromeless overlays).
        private void ApplyDecorations ()
        {
            if (PopupMode)
                return;
            try {
                if (Presenter is { } p) {
                    if (!_systemDecorations) {
                        p.IsResizable = false;
                        p.IsMaximizable = false;
                        p.IsMinimizable = false;
                    }
                    p.SetBorderAndTitleBar (_systemDecorations, _systemDecorations);
                }
                _window!.ExtendsContentIntoTitleBar = !_systemDecorations;
            } catch { /* presenter not available on every target */ }
        }

        public void SetCursor (CursorType cursor) => _canvas.SetCursorShape (UnoKeyInterop.ToCursorShape (cursor));

        public void SetIcon (byte[]? iconPng)
        {
            if (PopupMode)
                return;
            try {
                if (iconPng is null || iconPng.Length == 0)
                    return;

                var path = System.IO.Path.Combine (System.IO.Path.GetTempPath (), $"mf-uno-icon-{Guid.NewGuid ():N}.png");
                System.IO.File.WriteAllBytes (path, iconPng);
                _window!.AppWindow?.SetIcon (path);
            } catch { /* icon is best-effort */ }
        }

        public Size MinimumSize { set { } }
        public Size MaximumSize { set { } }
        public bool CanResize { get; set; } = true;
        public bool ShowInTaskbar { get; set; } = true;
        public double Opacity { get; set; } = 1.0;
        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;
        public bool Enabled { get; set; } = true;

        // ── Coordinate conversion ──
        // screen coords are physical, window-relative (parent position is (0,0) on Uno macOS); client logical.
        public Point PointToClient (Point screen)
        {
            var pos = Location;
            var s = Scaling;
            return new Point ((int) ((screen.X - pos.X) / s), (int) ((screen.Y - pos.Y) / s));
        }

        public Point PointToScreen (Point client)
        {
            var pos = Location;
            var s = Scaling;
            return new Point (pos.X + (int) (client.X * s), pos.Y + (int) (client.Y * s));
        }

        // ── Drag (custom chrome) — no portable Uno API; see docs/backends.md. ──
        public void BeginMoveDrag () { }
        public void BeginResizeDrag (WindowEdge edge) { }

        // ── File/folder pickers (top-level windows only) ──
        public async Task<string[]> ShowOpenFileDialog (OpenFileRequest request)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker ();
            ApplyFilters (picker.FileTypeFilter, request.Filters);
            InitializeWithWindow (picker);

            if (request.AllowMultiple) {
                var files = await picker.PickMultipleFilesAsync ();
                var paths = new System.Collections.Generic.List<string> ();
                foreach (var f in files)
                    if (f is not null) paths.Add (f.Path);
                return paths.ToArray ();
            }

            var file = await picker.PickSingleFileAsync ();
            return file is null ? Array.Empty<string> () : new[] { file.Path };
        }

        public async Task<string?> ShowSaveFileDialog (SaveFileRequest request)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker ();
            if (!string.IsNullOrEmpty (request.SuggestedFileName))
                picker.SuggestedFileName = request.SuggestedFileName;
            InitializeWithWindow (picker);

            var file = await picker.PickSaveFileAsync ();
            return file?.Path;
        }

        public async Task<string?> ShowOpenFolderDialog (FolderDialogRequest request)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker ();
            picker.FileTypeFilter.Add ("*");
            InitializeWithWindow (picker);

            var folder = await picker.PickSingleFolderAsync ();
            return folder?.Path;
        }

        private static void ApplyFilters (System.Collections.Generic.IList<string> target, System.Collections.Generic.IReadOnlyList<FileDialogFilter> filters)
        {
            foreach (var filter in filters)
                foreach (var pattern in filter.Patterns) {
                    var ext = pattern.StartsWith ("*.", StringComparison.Ordinal) ? pattern[1..] : pattern;
                    if (!target.Contains (ext))
                        target.Add (ext);
                }

            if (target.Count == 0)
                target.Add ("*");
        }

        private void InitializeWithWindow (object picker)
        {
            if (_window is null)
                return;
            try {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle (_window);
                WinRT.Interop.InitializeWithWindow.Initialize (picker, hwnd);
            } catch { /* not required / unsupported on some Skia desktop targets */ }
        }

        // ── Rendering ──
        // On Uno's macOS Skia head SKXamlCanvas.Invalidate() paints synchronously, so an Invalidate()
        // raised *during* a paint (controls routinely invalidate while rendering — e.g. TextBox updating
        // its scrollbars) would re-enter OnPaintSurface and recurse until the stack overflows. Coalesce
        // any such re-entrant invalidations into a single repaint scheduled on the next dispatcher tick,
        // matching the async-invalidation behaviour the Avalonia backend gets for free.
        private bool _painting;
        private bool _invalidatePending;

        public void Invalidate ()
        {
            if (_painting) {
                _invalidatePending = true;
                return;
            }

            _canvas.Invalidate ();
        }

        private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
        {
            var info = e.Info;
            var scaling = Scaling;
            _size = new Size ((int) (info.Width / scaling), (int) (info.Height / scaling));

            _painting = true;
            _invalidatePending = false;
            try {
                _owner.RenderFrame (e.Surface.Canvas, info.Width, info.Height, scaling);
            } finally {
                _painting = false;
            }

            // A control changed something that needs redrawing while we were painting. Repaint on the
            // next tick rather than synchronously, so state settles and we don't re-enter this frame.
            if (_invalidatePending)
                _canvas.DispatcherQueue?.TryEnqueue (() => _canvas.Invalidate ());
        }

        // ── Input ──
        private long _lastPointerRightTicks;

        private void WireInput ()
        {
            _canvas.PointerPressed += (_, e) => { TryFocus (); DispatchPointer (e, _owner.HandlePointerPressed); };
            _canvas.PointerReleased += (_, e) => {
                if (DispatchPointer (e, _owner.HandlePointerReleased) == MouseButtons.Right)
                    _lastPointerRightTicks = Environment.TickCount64;
            };
            _canvas.PointerMoved += (_, e) => DispatchPointer (e, _owner.HandlePointerMoved);
            _canvas.PointerExited += (_, e) => DispatchPointer (e, _owner.HandlePointerExited);

            // Routed key events — the standard path on the Windows/X11 Skia heads. (On the macOS head these
            // never fire for the canvas; WireMacOSKeyboard hooks the native host's KeyDown there instead.)
            _canvas.AddHandler (UIElement.KeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler ((_, e) => {
                    if (!e.Handled && _owner.HandleKeyDown (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true;
                }), handledEventsToo: true);
            _canvas.AddHandler (UIElement.KeyUpEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler ((_, e) => { if (!e.Handled && _owner.HandleKeyUp (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true; }), handledEventsToo: true);
            _canvas.CharacterReceived += (_, e) => { if (_owner.HandleTextInput (e.Character.ToString ())) e.Handled = true; };

            // The canonical context-menu trigger: covers right-click, macOS two-finger/Ctrl secondary
            // click, touch long-press and the keyboard menu key — many of which don't arrive as a
            // pointer right-button on macOS. Synthesize a right-click so Control.OnClick opens the menu.
            _canvas.ContextRequested += OnContextRequested;
        }

        private void OnContextRequested (UIElement sender, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs e)
        {
            // Skip if a real pointer right-release just handled this same gesture (avoid a double-open).
            if (Environment.TickCount64 - _lastPointerRightTicks < 300) {
                e.Handled = true;
                return;
            }

            var scaling = Scaling;
            int x, y;
            if (e.TryGetPosition (_canvas, out var pos)) {
                x = (int) (pos.X * scaling);
                y = (int) (pos.Y * scaling);
            } else {
                return; // no position (rare); let default handling proceed
            }

            _owner.HandlePointerPressed (MouseButtons.Right, x, y, Keys.None);
            _owner.HandlePointerReleased (MouseButtons.Right, x, y, Keys.None);
            e.Handled = true;
        }

        private delegate void PointerAction (MouseButtons button, int x, int y, Keys keys);

        private MouseButtons DispatchPointer (PointerRoutedEventArgs e, PointerAction action)
        {
            var scaling = Scaling;
            var point = e.GetCurrentPoint (_canvas);
            var x = (int) (point.Position.X * scaling);
            var y = (int) (point.Position.Y * scaling);
            var button = UnoKeyInterop.ToButton (point.Properties);
            action (button, x, y, Keys.None);
            return button;
        }

        // SKXamlCanvas with a settable cursor (UIElement.ProtectedCursor is protected).
        private sealed class CursorCanvas : SKXamlCanvas
        {
            public CursorCanvas ()
            {
                // Focusable so it can receive keyboard input; no focus rectangle around the drawing surface.
                IsTabStop = true;
                UseSystemFocusVisuals = false;
                // Stop the XAML focus manager from eating arrow keys for directional navigation before they
                // surface as KeyDown — Continuum.Forms does its own list/tree/arrow navigation.
                XYFocusKeyboardNavigation = Microsoft.UI.Xaml.Input.XYFocusKeyboardNavigationMode.Disabled;
            }

            public void SetCursorShape (Microsoft.UI.Input.InputSystemCursorShape shape)
            {
                try { ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create (shape); } catch { }
            }
        }
    }
}
