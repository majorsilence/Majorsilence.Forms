using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvImage = Avalonia.Controls.Image;
using Avalonia.Threading;
using SkiaSharp;
using System.Drawing;

using AvKey = Avalonia.Input.Key;
using AvKeyModifiers = Avalonia.Input.KeyModifiers;
using AvPointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using AvPointerReleasedEventArgs = Avalonia.Input.PointerReleasedEventArgs;
using AvPointerEventArgs = Avalonia.Input.PointerEventArgs;
using AvPointerWheelChangedEventArgs = Avalonia.Input.PointerWheelEventArgs;
using AvKeyEventArgs = Avalonia.Input.KeyEventArgs;
using AvTextInputEventArgs = Avalonia.Input.TextInputEventArgs;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Internal Avalonia 12 Window that hosts Majorsilence.Forms rendering and forwards
    /// Avalonia input events into the Majorsilence.Forms event pipeline.
    ///
    /// Rendering strategy: a <see cref="WriteableBitmap"/> is locked each frame,
    /// Skia draws the Majorsilence.Forms scene directly into the framebuffer, and an
    /// <see cref="Avalonia.Controls.Image"/> control displays the result. This mirrors the original
    /// native-framebuffer approach and is reliable across all Avalonia 12 platforms.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage ("Design", "CA1001", Justification = "_framebuffer is disposed in OnClosed; Window lifecycle manages the call.")]
    internal class MajorsilenceFormsWindowHost : Window, Majorsilence.Forms.Backends.IWindowBackend, Majorsilence.Forms.Backends.INativeControlHostBackend
    {
        private readonly WindowBase _owner;

        internal AvPointerPressedEventArgs? LastPointerPressed;

        // The bitmap is the "framebuffer". We create it at physical pixel size;
        // Avalonia displays it at logical size via the Image control.
        private WriteableBitmap? _framebuffer;
        private readonly AvImage _surface;
        private readonly Canvas _overlay;
        private readonly System.Collections.Generic.Dictionary<Majorsilence.Forms.NativeControlHost, Avalonia.Controls.Control> _overlays = new ();
        private DispatcherTimer? _renderTimer;

        // Set by InvalidateVisual() calls so the timer knows to repaint.
        internal bool IsDirty = true;

        internal MajorsilenceFormsWindowHost (WindowBase owner)
        {
            _owner = owner;

            // Majorsilence.Forms draws its own decorations on Windows/Linux.
            // macOS uses native chrome (set in Form constructor).
            WindowDecorations = WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = true;

            // Surface image fills the window. Stretch = Fill maps the framebuffer
            // pixels 1:1 with the window client area at logical pixel resolution.
            _surface = new AvImage {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Stretch = Stretch.Fill
            };

            // Transparent overlay for native controls hosted inside the Majorsilence scene (airspace).
            _overlay = new Canvas {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = true
            };

            // Grid stretches its children to fill available space. The overlay sits above the framebuffer.
            var grid = new Grid {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add (_surface);
            grid.Children.Add (_overlay);
            Content = grid;

            Closing += OnWindowClosing;

            // Resize → recreate framebuffer to match new logical size.
            _surface.SizeChanged += OnSurfaceSizeChanged;

            Opened += (_, _) => {
                EnsureFramebuffer ();
                StartRenderTimer ();
            };
            Closed += (_, _) => { StopRenderTimer (); _owner.OnBackendClosed (); };
            Activated += (_, _) => _owner.OnBackendActivated ();
            Deactivated += (_, _) => _owner.OnBackendDeactivated ();
        }

        private void OnWindowClosing (object? sender, WindowClosingEventArgs e)
        {
            if (_owner.OnBackendClosing ())
                e.Cancel = true;
        }

        private void OnSurfaceSizeChanged (object? sender, SizeChangedEventArgs e) => EnsureFramebuffer ();

        // Creates or resizes the framebuffer so its PHYSICAL pixel size always matches the surface's
        // current logical size × the current render scaling. Called both on layout/size changes and
        // every frame, so it self-corrects when the render scaling changes after the window opens
        // (e.g. a popup shown before its DPI has settled, or a window dragged between displays of
        // different scale). Returns true if a usable framebuffer exists.
        private bool EnsureFramebuffer ()
        {
            var scaling = _owner.Scaling;
            if (scaling <= 0)
                scaling = 1;

            // Prefer the image's laid-out logical size; fall back to the window client size
            // before the first layout pass has run.
            var logicalW = _surface.Bounds.Width  > 0 ? _surface.Bounds.Width  : ClientSize.Width;
            var logicalH = _surface.Bounds.Height > 0 ? _surface.Bounds.Height : ClientSize.Height;

            var physW = Math.Max (1, (int)Math.Round (logicalW * scaling));
            var physH = Math.Max (1, (int)Math.Round (logicalH * scaling));

            if (_framebuffer is null || _framebuffer.PixelSize.Width != physW || _framebuffer.PixelSize.Height != physH) {
                _framebuffer?.Dispose ();
                _framebuffer = new WriteableBitmap (
                    new PixelSize (physW, physH),
                    new Vector (96 * scaling, 96 * scaling),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
                _surface.Source = _framebuffer;
                IsDirty = true;
            }

            return _framebuffer is not null;
        }

        private void StartRenderTimer ()
        {
            _renderTimer = new DispatcherTimer (DispatcherPriority.Render) {
                Interval = TimeSpan.FromMilliseconds (16) // ~60 FPS
            };
            _renderTimer.Tick += (_, _) => PaintFrame ();
            _renderTimer.Start ();
        }

        private void StopRenderTimer ()
        {
            _renderTimer?.Stop ();
            _renderTimer = null;
        }

        // ── Framebuffer paint ──────────────────────────────────────────────────

        private void PaintFrame ()
        {
            // Self-correcting: recreates the framebuffer if the surface size or render scaling
            // changed since the last frame (fixes blurry/half-resolution popups on HiDPI displays
            // where the scaling settles after the window has already opened).
            if (!EnsureFramebuffer () || _framebuffer is null)
                return;

            // Skip if nothing needs painting.
            var adapter = _owner.adapter;
            bool frameDirty = IsDirty || adapter.NeedsPaint;

            if (!frameDirty)
                return;

            IsDirty = false;

            try {
                using var fb = _framebuffer.Lock ();

                var scaling = _owner.Scaling;

                // fb.Size is in PHYSICAL pixels (the bitmap was created at physical size).
                var physW = fb.Size.Width;
                var physH = fb.Size.Height;

                var skInfo = new SKImageInfo (physW, physH, SKColorType.Bgra8888, SKAlphaType.Premul);

                using var surface = SKSurface.Create (skInfo, fb.Address, fb.RowBytes);

                if (surface is null)
                    return;

                // The Majorsilence.Forms paint pipeline is backend-neutral (SkiaSharp); it lives on WindowBase.
                _owner.RenderFrame (surface.Canvas, physW, physH, scaling);
            } catch (Exception ex) {
                Console.Error.WriteLine ($"[MF] PaintFrame error: {ex.Message}");
            }

            _surface.InvalidateVisual ();
        }

        // ── Input forwarding ──────────────────────────────────────────────────

        // These overrides own the Avalonia → Majorsilence.Forms input translation (positions scaled to
        // physical pixels, buttons/keys mapped), then hand neutral values to the owner. No Avalonia
        // input types cross into WindowBase.

        protected override void OnPointerPressed (AvPointerPressedEventArgs e)
        {
            LastPointerPressed = e;
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerPressed (
                AvaloniaKeyInterop.PressedButton (props.PointerUpdateKind),
                (int)(pos.X * RenderScaling), (int)(pos.Y * RenderScaling),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerPressed (e);
        }

        protected override void OnPointerReleased (AvPointerReleasedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerReleased (
                AvaloniaKeyInterop.ReleasedButton (props.PointerUpdateKind),
                (int)(pos.X * RenderScaling), (int)(pos.Y * RenderScaling),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerReleased (e);
        }

        protected override void OnPointerMoved (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerMoved (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * RenderScaling), (int)(pos.Y * RenderScaling),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerMoved (e);
        }

        protected override void OnPointerWheelChanged (AvPointerWheelChangedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerWheel (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * RenderScaling), (int)(pos.Y * RenderScaling),
                new System.Drawing.Point ((int)e.Delta.X, (int)e.Delta.Y),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerWheelChanged (e);
        }

        protected override void OnPointerExited (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerExited (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * RenderScaling), (int)(pos.Y * RenderScaling),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerExited (e);
        }

        protected override void OnKeyDown (AvKeyEventArgs e)
        {
            if (_owner.HandleKeyDown (AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers)))
                e.Handled = true;
            base.OnKeyDown (e);
        }

        protected override void OnKeyUp (AvKeyEventArgs e)
        {
            if (_owner.HandleKeyUp (AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers)))
                e.Handled = true;
            base.OnKeyUp (e);
        }

        protected override void OnTextInput (AvTextInputEventArgs e)
        {
            if (_owner.HandleTextInput (e.Text ?? string.Empty))
                e.Handled = true;
            base.OnTextInput (e);
        }

        // ── Helpers for Form to trigger OS-level drag ─────────────────────────

        internal void StartMoveDrag ()
        {
            if (LastPointerPressed is not null)
                BeginMoveDrag (LastPointerPressed);
        }

        internal void StartResizeDrag (WindowEdge edge)
        {
            if (LastPointerPressed is not null)
                BeginResizeDrag (edge, LastPointerPressed);
        }

        // ── IWindowBackend (explicit: avoids name collisions with the Avalonia Window base) ──────────

        System.Drawing.Point Backends.IWindowBackend.Location {
            get => new System.Drawing.Point (Position.X, Position.Y);
            set => Position = new PixelPoint (value.X, value.Y);
        }

        System.Drawing.Size Backends.IWindowBackend.Size {
            get => new System.Drawing.Size ((int)Width, (int)Height);
            set { Width = value.Width; Height = value.Height; }
        }

        System.Drawing.Size Backends.IWindowBackend.ClientSize
            => new System.Drawing.Size ((int)ClientSize.Width, (int)ClientSize.Height);

        double Backends.IWindowBackend.Scaling => RenderScaling;

        void Backends.IWindowBackend.Show () => Show ();

        void Backends.IWindowBackend.ShowDialog (Backends.IWindowBackend? owner)
        {
            if (owner is MajorsilenceFormsWindowHost ownerHost)
                _ = ShowDialog (ownerHost);
            else
                Show ();
        }

        void Backends.IWindowBackend.Hide () => Hide ();

        void Backends.IWindowBackend.Close () => Close ();

        void Backends.IWindowBackend.Activate () => Activate ();

        bool Backends.IWindowBackend.Enabled {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        string Backends.IWindowBackend.Title { set => Title = value; }

        bool Backends.IWindowBackend.Topmost {
            get => Topmost;
            set => Topmost = value;
        }

        void Backends.IWindowBackend.SetSystemDecorations (bool useSystemDecorations)
        {
            WindowDecorations = useSystemDecorations ? WindowDecorations.Full : WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = !useSystemDecorations;
        }

        void Backends.IWindowBackend.SetCursor (Backends.CursorType cursor) => Cursor = MapCursor (cursor);

        // ── File/folder pickers ──────────────────────────────────────────────────
        private static Avalonia.Platform.Storage.FilePickerFileType[] MapFilters (System.Collections.Generic.IReadOnlyList<Backends.FileDialogFilter> filters)
            => filters.Select (f => new Avalonia.Platform.Storage.FilePickerFileType (f.Name) {
                Patterns = f.Patterns.ToList ()
            }).ToArray ();

        private async System.Threading.Tasks.Task<Avalonia.Platform.Storage.IStorageFolder?> ResolveStartFolder (string? initialDirectory)
            => initialDirectory is null ? null : await StorageProvider.TryGetFolderFromPathAsync (new System.Uri (initialDirectory));

        async System.Threading.Tasks.Task<string[]> Backends.IWindowBackend.ShowOpenFileDialog (Backends.OpenFileRequest request)
        {
            var options = new Avalonia.Platform.Storage.FilePickerOpenOptions {
                AllowMultiple = request.AllowMultiple,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                Title = request.Title,
                FileTypeFilter = MapFilters (request.Filters)
            };

            var result = await StorageProvider.OpenFilePickerAsync (options);
            return result.Select (f => f.GetFullPath ()).WhereNotNull ().ToArray ();
        }

        async System.Threading.Tasks.Task<string?> Backends.IWindowBackend.ShowSaveFileDialog (Backends.SaveFileRequest request)
        {
            var options = new Avalonia.Platform.Storage.FilePickerSaveOptions {
                DefaultExtension = request.DefaultExtension,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                SuggestedFileName = request.SuggestedFileName,
                Title = request.Title,
                FileTypeChoices = MapFilters (request.Filters)
            };

            var result = await StorageProvider.SaveFilePickerAsync (options);
            return result?.GetFullPath ();
        }

        async System.Threading.Tasks.Task<string?> Backends.IWindowBackend.ShowOpenFolderDialog (Backends.FolderDialogRequest request)
        {
            var options = new Avalonia.Platform.Storage.FolderPickerOpenOptions {
                AllowMultiple = false,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                Title = request.Title
            };

            var result = await StorageProvider.OpenFolderPickerAsync (options);
            return result.Select (f => f.GetFullPath ()).WhereNotNull ().FirstOrDefault ();
        }

        private static readonly System.Collections.Generic.Dictionary<Backends.CursorType, Avalonia.Input.Cursor> _cursorCache = new ();

        private static Avalonia.Input.Cursor MapCursor (Backends.CursorType cursor)
        {
            if (_cursorCache.TryGetValue (cursor, out var cached))
                return cached;

            var type = cursor switch {
                Backends.CursorType.Arrow => Avalonia.Input.StandardCursorType.Arrow,
                Backends.CursorType.AppStarting => Avalonia.Input.StandardCursorType.AppStarting,
                Backends.CursorType.Cross => Avalonia.Input.StandardCursorType.Cross,
                Backends.CursorType.Hand => Avalonia.Input.StandardCursorType.Hand,
                Backends.CursorType.Help => Avalonia.Input.StandardCursorType.Help,
                Backends.CursorType.Ibeam => Avalonia.Input.StandardCursorType.Ibeam,
                Backends.CursorType.No => Avalonia.Input.StandardCursorType.No,
                Backends.CursorType.UpArrow => Avalonia.Input.StandardCursorType.UpArrow,
                Backends.CursorType.Wait => Avalonia.Input.StandardCursorType.Wait,
                Backends.CursorType.SizeAll => Avalonia.Input.StandardCursorType.SizeAll,
                Backends.CursorType.SizeNorthSouth => Avalonia.Input.StandardCursorType.SizeNorthSouth,
                Backends.CursorType.SizeWestEast => Avalonia.Input.StandardCursorType.SizeWestEast,
                Backends.CursorType.TopSide => Avalonia.Input.StandardCursorType.TopSide,
                Backends.CursorType.BottomSide => Avalonia.Input.StandardCursorType.BottomSide,
                Backends.CursorType.LeftSide => Avalonia.Input.StandardCursorType.LeftSide,
                Backends.CursorType.RightSide => Avalonia.Input.StandardCursorType.RightSide,
                Backends.CursorType.TopLeftCorner => Avalonia.Input.StandardCursorType.TopLeftCorner,
                Backends.CursorType.TopRightCorner => Avalonia.Input.StandardCursorType.TopRightCorner,
                Backends.CursorType.BottomLeftCorner => Avalonia.Input.StandardCursorType.BottomLeftCorner,
                Backends.CursorType.BottomRightCorner => Avalonia.Input.StandardCursorType.BottomRightCorner,
                Backends.CursorType.DragCopy => Avalonia.Input.StandardCursorType.DragCopy,
                Backends.CursorType.DragLink => Avalonia.Input.StandardCursorType.DragLink,
                Backends.CursorType.DragMove => Avalonia.Input.StandardCursorType.DragMove,
                _ => Avalonia.Input.StandardCursorType.Arrow
            };

            var avCursor = new Avalonia.Input.Cursor (type);
            _cursorCache[cursor] = avCursor;
            return avCursor;
        }

        System.Drawing.Point Backends.IWindowBackend.PointToClient (System.Drawing.Point screen)
        {
            var p = this.PointToClient (new PixelPoint (screen.X, screen.Y));
            return new System.Drawing.Point ((int)p.X, (int)p.Y);
        }

        System.Drawing.Point Backends.IWindowBackend.PointToScreen (System.Drawing.Point client)
        {
            var p = this.PointToScreen (new Avalonia.Point (client.X, client.Y));
            return new System.Drawing.Point (p.X, p.Y);
        }

        void Backends.IWindowBackend.BeginMoveDrag () => StartMoveDrag ();

        void Backends.IWindowBackend.BeginResizeDrag (Backends.WindowEdge edge) => StartResizeDrag (edge switch {
            Backends.WindowEdge.North => WindowEdge.North,
            Backends.WindowEdge.NorthEast => WindowEdge.NorthEast,
            Backends.WindowEdge.East => WindowEdge.East,
            Backends.WindowEdge.SouthEast => WindowEdge.SouthEast,
            Backends.WindowEdge.South => WindowEdge.South,
            Backends.WindowEdge.SouthWest => WindowEdge.SouthWest,
            Backends.WindowEdge.West => WindowEdge.West,
            _ => WindowEdge.NorthWest
        });

        void Backends.IWindowBackend.Invalidate () => IsDirty = true;

        // ── INativeControlHostBackend (native Avalonia controls hosted inside the Majorsilence scene) ─────

        void Backends.INativeControlHostBackend.AttachNativeControl (Majorsilence.Forms.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not Avalonia.Controls.Control control)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, control))
                _overlay.Children.Remove (existing);

            _overlays[host] = control;
            if (!_overlay.Children.Contains (control))
                _overlay.Children.Add (control);
        }

        void Backends.INativeControlHostBackend.UpdateNativeControl (Majorsilence.Forms.NativeControlHost host, System.Drawing.Rectangle logicalBounds, System.Drawing.Rectangle clipBounds, bool visible)
        {
            if (!_overlays.TryGetValue (host, out var control))
                return;

            Canvas.SetLeft (control, logicalBounds.X);
            Canvas.SetTop (control, logicalBounds.Y);
            control.Width = logicalBounds.Width;
            control.Height = logicalBounds.Height;
            control.IsVisible = visible;

            // Clip to the visible viewport (local to the control). Null when fully visible.
            control.Clip = clipBounds == logicalBounds
                ? null
                : new RectangleGeometry (new Rect (
                    clipBounds.X - logicalBounds.X, clipBounds.Y - logicalBounds.Y,
                    clipBounds.Width, clipBounds.Height));
        }

        void Backends.INativeControlHostBackend.DetachNativeControl (Majorsilence.Forms.NativeControlHost host)
        {
            if (_overlays.Remove (host, out var control))
                _overlay.Children.Remove (control);
        }

        void Backends.IWindowBackend.SetIcon (byte[]? iconPng)
            => Icon = iconPng is null ? null : new Avalonia.Controls.WindowIcon (new System.IO.MemoryStream (iconPng));

        System.Drawing.Size Backends.IWindowBackend.MinimumSize {
            set {
                MinWidth = value.IsEmpty ? 0 : value.Width;
                MinHeight = value.IsEmpty ? 0 : value.Height;
            }
        }

        System.Drawing.Size Backends.IWindowBackend.MaximumSize {
            set {
                MaxWidth = value.IsEmpty ? double.PositiveInfinity : value.Width;
                MaxHeight = value.IsEmpty ? double.PositiveInfinity : value.Height;
            }
        }

        bool Backends.IWindowBackend.CanResize {
            get => CanResize;
            set => CanResize = value;
        }

        bool Backends.IWindowBackend.ShowInTaskbar {
            get => ShowInTaskbar;
            set => ShowInTaskbar = value;
        }

        double Backends.IWindowBackend.Opacity {
            get => Opacity;
            set => Opacity = value;
        }

        FormWindowState Backends.IWindowBackend.WindowState {
            get => (FormWindowState)(int)WindowState;
            set => WindowState = (Avalonia.Controls.WindowState)(int)value;
        }
    }

    /// <summary>
    /// Popup-specific host with no owner-window chrome.
    /// </summary>
    internal sealed class MajorsilenceFormsPopupWindowHost : MajorsilenceFormsWindowHost
    {
        internal MajorsilenceFormsPopupWindowHost (WindowBase owner) : base (owner)
        {
            Topmost = true;

            // On macOS, a borderless window with ExtendClientAreaToDecorationsHint = true (inherited
            // from the base host) is rendered with a translucent "vibrancy" backdrop. For a menu or
            // combo-box popup that shows up as a grey, blurry square instead of the menu. The main
            // Form avoids this by switching to native decorations on macOS; a popup has no chrome,
            // so we just make it a plain opaque borderless window.
            if (OperatingSystem.IsMacOS ()) {
                ExtendClientAreaToDecorationsHint = false;
                TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            }
        }
    }
}
