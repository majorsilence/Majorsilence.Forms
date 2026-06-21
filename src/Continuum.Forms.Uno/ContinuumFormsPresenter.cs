using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Continuum.Forms.Backends;
using SkiaSharp;
using SkiaSharp.Views.Windows;

using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using CFControl = Continuum.Forms.Control;
using WinPoint = Windows.Foundation.Point;

namespace Continuum.Forms.Uno
{
    /// <summary>
    /// An Uno (Skia) control that embeds a Continuum.Forms scene inside an existing Uno application.
    /// Drop it into your Uno visual tree (XAML or code) and assign <see cref="Content"/> a
    /// Continuum.Forms control tree; it renders through an <c>SKXamlCanvas</c> into the host window and
    /// forwards Uno pointer/keyboard input into the Continuum.Forms pipeline. No top-level OS window is
    /// created. Popups (combo dropdowns, menus, tooltips) opened from the embedded content attach their
    /// overlay to this presenter's XamlRoot.
    ///
    /// Targets Uno 6.0+ on the SkiaSharp backend.
    /// </summary>
    public sealed class ContinuumFormsPresenter : Microsoft.UI.Xaml.Controls.Grid, IWindowBackend, IUnoHostSurface, INativeControlHostBackend
    {
        private readonly HostedSurface _host;
        private readonly CursorCanvas _canvas;
        private bool _painting;
        private bool _invalidatePending;
        private readonly System.Collections.Generic.Dictionary<Continuum.Forms.NativeControlHost, UIElement> _overlays = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="ContinuumFormsPresenter"/> class.
        /// </summary>
        public ContinuumFormsPresenter ()
        {
            // Ensure the Uno backend is the active platform backend, and register this surface so popups
            // opened by the embedded content attach to our XamlRoot rather than spawning OS windows.
            // NOTE: read the backend defensively — the getter auto-resolves to the Avalonia backend by
            // reflection, which throws in an Uno-only app where that assembly isn't referenced.
            UnoPlatformBackend? backend = null;
            try { backend = Platform.Backend as UnoPlatformBackend; } catch { /* no backend configured yet */ }

            if (backend is null) {
                backend = new UnoPlatformBackend ();
                Platform.Backend = backend;
            }
            backend.Initialize ();
            backend.RegisterHostSurface (this);

            _canvas = new CursorCanvas ();
            _canvas.PaintSurface += OnPaintSurface;
            // Grab keyboard focus and wire the macOS native keyboard once the canvas is in the tree.
            _canvas.Loaded += (_, _) => { TryFocus (); WireMacOSKeyboard (); };
            Children.Add (_canvas);

            WireInput ();

            _host = new HostedSurface (this);
        }

        /// <summary>
        /// Gets or sets the root Continuum.Forms control hosted by this presenter. Setting it docks the
        /// control to fill the presenter.
        /// </summary>
        public CFControl? Content {
            get => _host.Content;
            set => _host.Content = value;
        }

        /// <summary>Gets the underlying hosted surface (advanced scenarios: multiple roots, events).</summary>
        public HostedSurface Surface => _host;

        /// <summary>
        /// Gets or sets whether the embedded scene automatically follows the host Uno application's theme
        /// (light/dark + accent). Defaults to true. Because Continuum's Theme is global, the last presenter
        /// to sync wins when several are present with differing host themes.
        /// </summary>
        public bool SyncTheme { get; set; } = true;

        // ── IUnoHostSurface (popup anchoring) ────────────────────────────────────
        public Microsoft.UI.Xaml.XamlRoot? CanvasXamlRoot => _canvas.XamlRoot;
        public double HostScaling => _canvas.XamlRoot?.RasterizationScale ?? 1.0;

        // For popup offset math the presenter reports the window origin; PointToScreen already bakes in
        // the presenter's own offset within the window, so popups land at (presenterOffset + controlPos).
        Point IUnoHostSurface.Location => Point.Empty;

        // ── Rendering ─────────────────────────────────────────────────────────────

        private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
        {
            if (SyncTheme)
                ContinuumFormsTheme.FollowHost (this);

            var info = e.Info;
            var scaling = Scaling;

            _painting = true;
            _invalidatePending = false;
            try {
                // Transparent so the host's own background shows through where the embedded scene draws
                // nothing (HostedSurface defaults to a transparent background).
                e.Surface.Canvas.Clear (SKColors.Transparent);
                _host.RenderFrame (e.Surface.Canvas, info.Width, info.Height, scaling);
            } finally {
                _painting = false;
            }

            if (_invalidatePending)
                _canvas.DispatcherQueue?.TryEnqueue (() => _canvas.Invalidate ());
        }

        // ── Input ─────────────────────────────────────────────────────────────────

        private long _lastPointerRightTicks;

        private void WireInput ()
        {
            _canvas.PointerPressed += (_, e) => {
                try { _canvas.Focus (FocusState.Pointer); } catch { }
                WireMacOSKeyboard ();
                DispatchPointer (e, _host.HandlePointerPressed);
            };
            _canvas.PointerReleased += (_, e) => {
                if (DispatchPointer (e, _host.HandlePointerReleased) == MouseButtons.Right)
                    _lastPointerRightTicks = Environment.TickCount64;
            };
            _canvas.PointerMoved += (_, e) => DispatchPointer (e, _host.HandlePointerMoved);
            _canvas.PointerExited += (_, e) => DispatchPointer (e, _host.HandlePointerExited);
            _canvas.PointerWheelChanged += (_, e) => DispatchWheel (e);

            _canvas.AddHandler (UIElement.KeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler ((_, e) => {
                    if (!e.Handled && _host.HandleKeyDown (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true;
                }),
                handledEventsToo: true);
            _canvas.AddHandler (UIElement.KeyUpEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler ((_, e) => { if (!e.Handled && _host.HandleKeyUp (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true; }),
                handledEventsToo: true);
            // On non-macOS heads, XAML CharacterReceived carries typed text. On the macOS head it does not
            // reach a nested SKXamlCanvas, so we synthesize text from the native KeyDown hook instead (see
            // OnMacKeyDown); suppress this path there to avoid double insertion.
            _canvas.CharacterReceived += (_, e) => {
                if (!_macKeyboardWired && _host.HandleTextInput (e.Character.ToString ())) e.Handled = true;
            };

            _canvas.ContextRequested += OnContextRequested;
        }

        private void OnContextRequested (UIElement sender, ContextRequestedEventArgs e)
        {
            if (Environment.TickCount64 - _lastPointerRightTicks < 300) {
                e.Handled = true;
                return;
            }

            if (!e.TryGetPosition (_canvas, out var pos))
                return;

            var scaling = Scaling;
            var x = (int) (pos.X * scaling);
            var y = (int) (pos.Y * scaling);
            _host.HandlePointerPressed (MouseButtons.Right, x, y, Keys.None);
            _host.HandlePointerReleased (MouseButtons.Right, x, y, Keys.None);
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

        private void DispatchWheel (PointerRoutedEventArgs e)
        {
            var scaling = Scaling;
            var point = e.GetCurrentPoint (_canvas);
            var x = (int) (point.Position.X * scaling);
            var y = (int) (point.Position.Y * scaling);
            _host.HandlePointerWheel (MouseButtons.None, x, y, new Point (0, point.Properties.MouseWheelDelta), Keys.None);
        }

        private void TryFocus ()
        {
            try { _canvas.Focus (FocusState.Programmatic); } catch { }
        }

        // On the macOS Skia head, routed XAML key events don't reach a nested SKXamlCanvas, so hook the
        // native MacOSWindowHost.KeyDown/KeyUp directly (via reflection — the type is internal). Unlike the
        // standalone window host we don't own a Window, so match the host by XamlRoot. No-op on other heads.
        private bool _macKeyboardWired;

        private void WireMacOSKeyboard ()
        {
            if (_macKeyboardWired)
                return;

            var xamlRoot = _canvas.XamlRoot;
            if (xamlRoot is null)
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

                    // Match the host whose window contains our canvas (same XamlRoot).
                    if ((winField.GetValue (host) as Window)?.Content?.XamlRoot != xamlRoot)
                        continue;

                    keyDownEvt.AddEventHandler (host, new Windows.Foundation.TypedEventHandler<object, Windows.UI.Core.KeyEventArgs> (OnMacKeyDown));
                    keyUpEvt?.AddEventHandler (host, new Windows.Foundation.TypedEventHandler<object, Windows.UI.Core.KeyEventArgs> (OnMacKeyUp));
                    _macKeyboardWired = true;
                    return;
                }
            } catch {
                // Reflection into the internal host is best-effort; routed handlers remain as a fallback.
            }
        }

        // Tracks Shift for synthesizing typed characters from the native KeyDown (the macOS head delivers
        // no character event and routes no CharacterReceived to a nested canvas).
        private bool _shiftDown;

        // The native KeyDown is window-level (fires regardless of focus). We route it to Continuum UNLESS
        // keyboard focus is on an element OUTSIDE this presenter (a real host native control) — that way a
        // sibling native control keeps its own typing, but the embedded scene still works even when the
        // SKXamlCanvas can't hold XAML focus (this macOS head drops canvas focus immediately after a click).
        private bool ShouldRouteToContinuum ()
        {
            try {
                var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement (_canvas.XamlRoot) as Microsoft.UI.Xaml.DependencyObject;
                if (focused is null)
                    return true;   // nothing specific focused → treat as ours

                for (Microsoft.UI.Xaml.DependencyObject? d = focused; d is not null; d = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent (d))
                    if (ReferenceEquals (d, this))
                        return true;   // focus is within this presenter → ours

                return false;          // focus is on a sibling/native control → don't steal its input
            } catch {
                return true;
            }
        }

        private void OnMacKeyDown (object sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (!ShouldRouteToContinuum ())
                return;

            var vk = e.VirtualKey;
            if (vk is Windows.System.VirtualKey.Shift or Windows.System.VirtualKey.LeftShift or Windows.System.VirtualKey.RightShift)
                _shiftDown = true;

            _host.HandleKeyDown (UnoKeyInterop.ToKeys (vk));

            // Editing/navigation keys are handled above (OnKeyDown); printable keys produce text here.
            var text = ToText (vk, _shiftDown);
            if (text is not null)
                _host.HandleTextInput (text);

            // Mark handled: we've routed the key into the embedded scene. NOTE: this does NOT stop the macOS
            // "unhandled key" beep — that fires from AppKit's interpretKeyEvents because no NSTextInputClient
            // holds focus (a nested SKXamlCanvas can't, on this head). The beep is a known Uno-macOS
            // limitation tracked separately (see samples/UnoCanvasFocusRepro); typing itself works.
            e.Handled = true;
        }

        private void OnMacKeyUp (object sender, Windows.UI.Core.KeyEventArgs e)
        {
            var vk = e.VirtualKey;
            if (vk is Windows.System.VirtualKey.Shift or Windows.System.VirtualKey.LeftShift or Windows.System.VirtualKey.RightShift)
                _shiftDown = false;

            if (!ShouldRouteToContinuum ())
                return;

            if (_host.HandleKeyUp (UnoKeyInterop.ToKeys (vk)))
                e.Handled = true;
        }

        private static bool CapsLockOn ()
        {
            try {
                return Microsoft.UI.Input.InputKeyboardSource
                    .GetKeyStateForCurrentThread (Windows.System.VirtualKey.CapitalLock)
                    .HasFlag (Windows.UI.Core.CoreVirtualKeyStates.Locked);
            } catch {
                return false;
            }
        }

        // Maps a VirtualKey + Shift/CapsLock to the character it types (US layout). Returns null for
        // non-printable keys. Used only on the macOS head, which gives us no character event.
        private static string? ToText (Windows.System.VirtualKey vk, bool shift)
        {
            var code = (int) vk;

            // Letters A–Z
            if (code is >= 0x41 and <= 0x5A) {
                var upper = shift ^ CapsLockOn ();
                var c = (char) ('a' + (code - 0x41));
                return (upper ? char.ToUpperInvariant (c) : c).ToString ();
            }

            // Top-row digits 0–9 (and shifted symbols)
            if (code is >= 0x30 and <= 0x39) {
                if (!shift)
                    return ((char) ('0' + (code - 0x30))).ToString ();
                return code switch {
                    0x30 => ")", 0x31 => "!", 0x32 => "@", 0x33 => "#", 0x34 => "$",
                    0x35 => "%", 0x36 => "^", 0x37 => "&", 0x38 => "*", 0x39 => "(", _ => null
                };
            }

            // Numpad digits
            if (code is >= 0x60 and <= 0x69)
                return ((char) ('0' + (code - 0x60))).ToString ();

            return code switch {
                0x20 => " ",                       // Space
                0x6A => "*", 0x6B => "+", 0x6D => "-", 0x6E => ".", 0x6F => "/",   // Numpad ops
                0xBA => shift ? ":" : ";",
                0xBB => shift ? "+" : "=",
                0xBC => shift ? "<" : ",",
                0xBD => shift ? "_" : "-",
                0xBE => shift ? ">" : ".",
                0xBF => shift ? "?" : "/",
                0xC0 => shift ? "~" : "`",
                0xDB => shift ? "{" : "[",
                0xDC => shift ? "|" : "\\",
                0xDD => shift ? "}" : "]",
                0xDE => shift ? "\"" : "'",
                _ => null
            };
        }

        // ── IWindowBackend ─────────────────────────────────────────────────────────

        // The presenter's offset within the host window, in logical units.
        private WinPoint WindowOffsetLogical ()
        {
            if (_canvas.XamlRoot?.Content is UIElement root) {
                try { return _canvas.TransformToVisual (root).TransformPoint (new WinPoint (0, 0)); }
                catch { }
            }
            return new WinPoint (0, 0);
        }

        public Point Location {
            get {
                var off = WindowOffsetLogical ();
                var s = Scaling;
                return new Point ((int) (off.X * s), (int) (off.Y * s));
            }
            set { /* position is owned by the host layout */ }
        }

        public Size Size {
            get => new Size ((int) ActualWidth, (int) ActualHeight);
            set { /* size is owned by the host layout */ }
        }

        public Size ClientSize => new Size ((int) ActualWidth, (int) ActualHeight);

        public double Scaling => _canvas.XamlRoot?.RasterizationScale ?? 1.0;

        public void Show () { /* shown by the host visual tree */ }
        public void ShowDialog (IWindowBackend? owner) { /* embedded surfaces are not shown modally */ }
        public void Hide () => Visibility = Visibility.Collapsed;
        public void Close () { /* lifetime owned by the host */ }
        public void Activate () => TryFocus ();

        public string Title { set { } }
        public bool Topmost { get; set; }
        public void SetSystemDecorations (bool useSystemDecorations) { }
        public void SetCursor (CursorType cursor) => _canvas.SetCursorShape (UnoKeyInterop.ToCursorShape (cursor));
        public void SetIcon (byte[]? iconPng) { }
        public Size MinimumSize { set { } }
        public Size MaximumSize { set { } }
        public bool CanResize { get; set; }
        public bool ShowInTaskbar { get; set; }
        public double Opacity { get => base.Opacity; set => base.Opacity = value; }
        public FormWindowState WindowState { get; set; } = FormWindowState.Normal;
        public bool Enabled { get; set; } = true;

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

        public void BeginMoveDrag () { }
        public void BeginResizeDrag (WindowEdge edge) { }

        // ── INativeControlHostBackend (native Uno UIElements hosted inside the Continuum scene) ────────

        void INativeControlHostBackend.AttachNativeControl (Continuum.Forms.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not UIElement element)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, element))
                Children.Remove (existing);

            _overlays[host] = element;
            if (!Children.Contains (element))
                Children.Add (element);
        }

        void INativeControlHostBackend.UpdateNativeControl (Continuum.Forms.NativeControlHost host, System.Drawing.Rectangle logicalBounds, System.Drawing.Rectangle clipBounds, bool visible)
        {
            if (!_overlays.TryGetValue (host, out var element) || element is not FrameworkElement fe)
                return;

            fe.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            fe.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top;
            fe.Margin = new Thickness (logicalBounds.X, logicalBounds.Y, 0, 0);
            fe.Width = logicalBounds.Width;
            fe.Height = logicalBounds.Height;
            fe.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            // Clip to the visible viewport (local to the element). Null when fully visible.
            fe.Clip = clipBounds == logicalBounds
                ? null
                : new Microsoft.UI.Xaml.Media.RectangleGeometry {
                    Rect = new Windows.Foundation.Rect (
                        clipBounds.X - logicalBounds.X, clipBounds.Y - logicalBounds.Y,
                        clipBounds.Width, clipBounds.Height)
                };
        }

        void INativeControlHostBackend.DetachNativeControl (Continuum.Forms.NativeControlHost host)
        {
            if (_overlays.Remove (host, out var element))
                Children.Remove (element);
        }

        public void Invalidate ()
        {
            if (_painting) {
                _invalidatePending = true;
                return;
            }
            _canvas.Invalidate ();
        }

        // ── File/folder pickers (best-effort; embedded heads may require a window handle we don't own) ──
        public async Task<string[]> ShowOpenFileDialog (OpenFileRequest request)
        {
            try {
                var picker = new Windows.Storage.Pickers.FileOpenPicker ();
                ApplyFilters (picker.FileTypeFilter, request.Filters);

                if (request.AllowMultiple) {
                    var files = await picker.PickMultipleFilesAsync ();
                    var paths = new System.Collections.Generic.List<string> ();
                    foreach (var f in files)
                        if (f is not null) paths.Add (f.Path);
                    return paths.ToArray ();
                }

                var file = await picker.PickSingleFileAsync ();
                return file is null ? Array.Empty<string> () : new[] { file.Path };
            } catch {
                return Array.Empty<string> ();
            }
        }

        public async Task<string?> ShowSaveFileDialog (SaveFileRequest request)
        {
            try {
                var picker = new Windows.Storage.Pickers.FileSavePicker ();
                if (!string.IsNullOrEmpty (request.SuggestedFileName))
                    picker.SuggestedFileName = request.SuggestedFileName;
                var file = await picker.PickSaveFileAsync ();
                return file?.Path;
            } catch {
                return null;
            }
        }

        public async Task<string?> ShowOpenFolderDialog (FolderDialogRequest request)
        {
            try {
                var picker = new Windows.Storage.Pickers.FolderPicker ();
                picker.FileTypeFilter.Add ("*");
                var folder = await picker.PickSingleFolderAsync ();
                return folder?.Path;
            } catch {
                return null;
            }
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

        // SKXamlCanvas with a settable cursor and keyboard focus.
        private sealed class CursorCanvas : SKXamlCanvas
        {
            public CursorCanvas ()
            {
                IsTabStop = true;
                UseSystemFocusVisuals = false;
                XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Disabled;
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
            }

            public void SetCursorShape (Microsoft.UI.Input.InputSystemCursorShape shape)
            {
                try { ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create (shape); } catch { }
            }
        }
    }
}
