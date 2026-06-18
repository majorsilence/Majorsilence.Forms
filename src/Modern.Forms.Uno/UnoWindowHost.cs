using System;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Modern.Forms.Backends;
using SkiaSharp.Views.Windows;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Modern.Forms.Uno
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Modern.Forms window through a Uno
    /// <c>SKXamlCanvas</c>: its <c>PaintSurface</c> calls <c>WindowBase.RenderFrame</c>, and Uno
    /// pointer/keyboard/character events are translated into the neutral <c>WindowBase.Handle*</c> path.
    /// </summary>
    internal sealed class UnoWindowHost : IWindowBackend
    {
        private readonly WindowBase _owner;
        private readonly Window _window;
        private readonly CursorCanvas _canvas;
        private Size _size = new (800, 600);
        private Point _location;

        public UnoWindowHost (WindowBase owner, bool isPopup)
        {
            _owner = owner;

            _canvas = new CursorCanvas ();
            _canvas.PaintSurface += OnPaintSurface;

            _window = new Window { Content = _canvas };

            WireInput ();
            WireLifecycle ();
        }

        // ── Geometry ──
        public Point Location {
            get => _location;
            set { _location = value; TryMove (value); }
        }

        public Size Size {
            get => _size;
            set { _size = value; TryResize (value); }
        }

        public Size ClientSize => _size;

        public double Scaling => _canvas.XamlRoot?.RasterizationScale ?? 1.0;

        private void TryResize (Size size)
        {
            try { _window.AppWindow?.Resize (new Windows.Graphics.SizeInt32 { Width = size.Width, Height = size.Height }); } catch { }
        }

        private void TryMove (Point location)
        {
            try { _window.AppWindow?.Move (new Windows.Graphics.PointInt32 { X = location.X, Y = location.Y }); } catch { }
        }

        // ── Lifecycle ──
        public void Show () => _window.Activate ();
        public void ShowDialog (IWindowBackend? owner) => _window.Activate ();
        public void Hide () { try { _window.AppWindow?.Hide (); } catch { } }
        public void Close () => _window.Close ();
        public void Activate () => _window.Activate ();

        private void WireLifecycle ()
        {
            _window.Activated += (_, e) => {
                if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
                    _owner.OnBackendDeactivated ();
                else
                    _owner.OnBackendActivated ();
            };

            _window.Closed += (_, _) => _owner.OnBackendClosed ();
        }

        // ── Appearance / behaviour ──
        public string Title { set { try { _window.Title = value; } catch { } } }
        public bool Topmost { get; set; }
        public void SetSystemDecorations (bool useSystemDecorations)
        {
            try { _window.ExtendsContentIntoTitleBar = !useSystemDecorations; } catch { }
        }
        public void SetCursor (CursorType cursor) => _canvas.SetCursorShape (UnoKeyInterop.ToCursorShape (cursor));
        public void SetIcon (byte[]? iconPng)
        {
            try {
                if (iconPng is null || iconPng.Length == 0)
                    return;

                // AppWindow.SetIcon takes a file path; persist the PNG to a temp file.
                var path = System.IO.Path.Combine (System.IO.Path.GetTempPath (), $"mf-uno-icon-{Guid.NewGuid ():N}.png");
                System.IO.File.WriteAllBytes (path, iconPng);
                _window.AppWindow?.SetIcon (path);
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
        public Point PointToClient (Point screen) => new (screen.X - _location.X, screen.Y - _location.Y);
        public Point PointToScreen (Point client) => new (client.X + _location.X, client.Y + _location.Y);

        // ── Drag (custom chrome) ──
        public void BeginMoveDrag () { /* TODO: AppWindow drag via the title bar region. */ }
        public void BeginResizeDrag (WindowEdge edge) { /* TODO: platform-specific resize drag. */ }

        // ── File/folder pickers (WinUI Windows.Storage.Pickers, bound to this window) ──
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
                    // "*.txt" -> ".txt"; bare "*" -> "*".
                    var ext = pattern.StartsWith ("*.", StringComparison.Ordinal) ? pattern[1..] : pattern;
                    if (!target.Contains (ext))
                        target.Add (ext);
                }

            if (target.Count == 0)
                target.Add ("*");
        }

        private void InitializeWithWindow (object picker)
        {
            try {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle (_window);
                WinRT.Interop.InitializeWithWindow.Initialize (picker, hwnd);
            } catch { /* Not required / unsupported on some Skia desktop targets. */ }
        }

        // ── Rendering ──
        public void Invalidate () => _canvas.Invalidate ();

        private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
        {
            var info = e.Info;
            var scaling = Scaling;
            _size = new Size ((int) (info.Width / scaling), (int) (info.Height / scaling));
            _owner.RenderFrame (e.Surface.Canvas, info.Width, info.Height, scaling);
        }

        // ── Input ──
        private void WireInput ()
        {
            _canvas.PointerPressed += (_, e) => DispatchPointer (e, _owner.HandlePointerPressed);
            _canvas.PointerReleased += (_, e) => DispatchPointer (e, _owner.HandlePointerReleased);
            _canvas.PointerMoved += (_, e) => DispatchPointer (e, _owner.HandlePointerMoved);
            _canvas.PointerExited += (_, e) => DispatchPointer (e, _owner.HandlePointerExited);
            _canvas.KeyDown += (_, e) => { if (_owner.HandleKeyDown (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true; };
            _canvas.KeyUp += (_, e) => { if (_owner.HandleKeyUp (UnoKeyInterop.ToKeys (e.Key))) e.Handled = true; };
            _canvas.CharacterReceived += (_, e) => { if (_owner.HandleTextInput (e.Character.ToString ())) e.Handled = true; };
        }

        private delegate void PointerAction (MouseButtons button, int x, int y, Keys keys);

        private void DispatchPointer (PointerRoutedEventArgs e, PointerAction action)
        {
            var scaling = Scaling;
            var point = e.GetCurrentPoint (_canvas);
            var x = (int) (point.Position.X * scaling);
            var y = (int) (point.Position.Y * scaling);
            action (UnoKeyInterop.ToButton (point.Properties), x, y, Keys.None);
        }

        // SKXamlCanvas with a settable cursor (UIElement.ProtectedCursor is protected).
        private sealed class CursorCanvas : SKXamlCanvas
        {
            public void SetCursorShape (Microsoft.UI.Input.InputSystemCursorShape shape)
            {
                try { ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create (shape); } catch { }
            }
        }
    }
}
