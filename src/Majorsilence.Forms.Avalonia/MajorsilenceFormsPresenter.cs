using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Majorsilence.Forms.Backends;

using System.Collections.Generic;

using AvPoint = Avalonia.Point;
using AvControl = Avalonia.Controls.Control;
using AvCursor = Avalonia.Input.Cursor;
using AvPointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using AvPointerReleasedEventArgs = Avalonia.Input.PointerReleasedEventArgs;
using AvPointerEventArgs = Avalonia.Input.PointerEventArgs;
using AvPointerWheelChangedEventArgs = Avalonia.Input.PointerWheelEventArgs;
using AvKeyEventArgs = Avalonia.Input.KeyEventArgs;
using AvTextInputEventArgs = Avalonia.Input.TextInputEventArgs;

namespace Majorsilence.Forms
{
    /// <summary>
    /// An Avalonia control that embeds a Majorsilence.Forms scene inside an existing Avalonia window or
    /// panel. Drop it into your Avalonia visual tree (XAML or code) and assign <see cref="Content"/> a
    /// Majorsilence.Forms control tree; it renders into the host window's surface and forwards Avalonia
    /// input into the Majorsilence.Forms pipeline. No top-level OS window is created.
    ///
    /// Majorsilence.Forms is rendered on the UI thread into a <see cref="WriteableBitmap"/> framebuffer
    /// (matching the proven, thread-safe path used by the standalone window host) and that framebuffer
    /// is composited into the host window's surface in <c>Render</c>. Popups (combo dropdowns,
    /// menus, tooltips) and modal dialogs opened from the embedded content continue to work because they
    /// create their own small top-level windows positioned via <c>PointToScreen</c>.
    ///
    /// Derives from <see cref="Canvas"/> so it can also overlay native Avalonia controls hosted inside the
    /// Majorsilence scene via <see cref="NativeControlHost"/> (the airspace interop model).
    /// </summary>
    public class MajorsilenceFormsPresenter : Canvas, IWindowBackend, INativeControlHostBackend, IDisposable
    {
        private readonly HostedSurface _host;
        private WriteableBitmap? _framebuffer;
        private bool _dirty = true;
        private bool _renderPending;
        private bool _painting;
        private bool _invalidatePending;
        private readonly Dictionary<NativeControlHost, AvControl> _overlays = new ();

        // The Majorsilence scene is drawn into this Image (a hit-test-invisible bottom child) since Panel.Render
        // is sealed and can't be overridden. Native overlays are siblings layered above it.
        private readonly Image _surface;

        /// <summary>
        /// Initializes a new instance of the <see cref="MajorsilenceFormsPresenter"/> class.
        /// </summary>
        public MajorsilenceFormsPresenter ()
        {
            Focusable = true;

            // A transparent background makes the whole presenter hit-testable so pointer events reach the
            // embedded scene even over areas the Majorsilence content leaves transparent.
            Background = Brushes.Transparent;

            _surface = new Image {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false   // let pointer events fall through to this Canvas (and overlays)
            };
            Canvas.SetLeft (_surface, 0);
            Canvas.SetTop (_surface, 0);
            Children.Add (_surface);

            // Resolve/initialise the Avalonia backend so popups, dialogs and timers spawned by the
            // embedded content route through Avalonia. The default backend resolves to this assembly.
            Platform.Backend.Initialize ();

            _host = new HostedSurface (this);
        }

        /// <summary>
        /// Gets or sets the root Majorsilence.Forms control hosted by this presenter. Setting it docks the
        /// control to fill the presenter.
        /// </summary>
        public Control? Content {
            get => _host.Content;
            set => _host.Content = value;
        }

        /// <summary>Gets the underlying hosted surface (advanced scenarios: multiple roots, events).</summary>
        public HostedSurface Surface => _host;

        /// <summary>Disposes the hosted surface and framebuffer. Call when the host removes this presenter.</summary>
        public void Dispose ()
        {
            _host.Dispose ();
            _framebuffer?.Dispose ();
            _framebuffer = null;
            GC.SuppressFinalize (this);
        }

        // The render/DPI scale of the host window. Plain Avalonia controls don't expose RenderScaling
        // (that lives on TopLevel), so read it from the hosting top-level.
        private double Scale => TopLevel.GetTopLevel (this)?.RenderScaling ?? 1;

        /// <summary>
        /// Gets or sets whether the embedded scene automatically follows the host Avalonia application's
        /// theme (light/dark + accent). Defaults to true. Because Majorsilence's Theme is global, the last
        /// presenter to sync wins when several are present with differing host themes.
        /// </summary>
        public bool SyncTheme { get; set; } = true;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree (VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree (e);

            if (SyncTheme)
                MajorsilenceFormsTheme.FollowHost ();

            // Trigger the initial paint now that we have layout/scale information.
            ScheduleRender ();
        }

        /// <inheritdoc/>
        protected override void OnDetachedFromVisualTree (VisualTreeAttachmentEventArgs e)
        {
            _renderPending = false;
            _framebuffer?.Dispose ();
            _framebuffer = null;
            base.OnDetachedFromVisualTree (e);
        }

        // Schedules one PaintFrame on the next render-priority dispatcher tick.
        // _renderPending coalesces concurrent Invalidate() calls into a single repaint.
        private void ScheduleRender ()
        {
            if (_renderPending)
                return;
            _renderPending = true;
            Dispatcher.UIThread.Post (() => {
                _renderPending = false;
                PaintFrame ();
            }, DispatcherPriority.Render);
        }

        // ── Rendering ─────────────────────────────────────────────────────────────

        // Creates/resizes the framebuffer so its PHYSICAL pixel size matches the control's logical size
        // × the current render scaling. Returns true when a usable framebuffer exists.
        private bool EnsureFramebuffer ()
        {
            var scaling = Scale <= 0 ? 1 : Scale;

            var physW = Math.Max (1, (int)Math.Round (Bounds.Width * scaling));
            var physH = Math.Max (1, (int)Math.Round (Bounds.Height * scaling));

            if (_framebuffer is null || _framebuffer.PixelSize.Width != physW || _framebuffer.PixelSize.Height != physH) {
                _framebuffer?.Dispose ();
                _framebuffer = new WriteableBitmap (
                    new PixelSize (physW, physH),
                    new Vector (96 * scaling, 96 * scaling),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
                _dirty = true;
            }

            return _framebuffer is not null;
        }

        // Renders the Majorsilence.Forms scene into the framebuffer on the UI thread, then asks Avalonia to
        // composite it. Skips work when nothing is dirty.
        private void PaintFrame ()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            if (!EnsureFramebuffer () || _framebuffer is null)
                return;

            if (!_dirty && !_host.adapter.NeedsPaint)
                return;

            _dirty = false;

            _painting = true;
            _invalidatePending = false;
            try {
                using var fb = _framebuffer.Lock ();
                var info = new SKImageInfo (fb.Size.Width, fb.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create (info, fb.Address, fb.RowBytes);

                if (surface is null)
                    return;

                // Clear to transparent so the host's own background shows through where the embedded
                // scene draws nothing (HostedSurface defaults to a transparent background).
                surface.Canvas.Clear (SKColors.Transparent);

                _host.RenderFrame (surface.Canvas, fb.Size.Width, fb.Size.Height, Scale);
            } catch (Exception ex) {
                Console.Error.WriteLine ($"[CF] Presenter PaintFrame error: {ex.Message}");
            } finally {
                _painting = false;
            }

            // Present the framebuffer through the bottom Image child (sized to the presenter), and keep any
            // native overlays aligned afterwards via their own paint-driven Sync.
            _surface.Width = Bounds.Width;
            _surface.Height = Bounds.Height;
            _surface.Source = _framebuffer;
            _surface.InvalidateVisual ();

            if (_invalidatePending)
                ScheduleRender ();
        }

        // ── Input forwarding (Avalonia → Majorsilence.Forms; positions scaled to physical pixels) ───────

        /// <inheritdoc/>
        protected override void OnPointerPressed (AvPointerPressedEventArgs e)
        {
            Focus ();

            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _host.HandlePointerPressed (
                AvaloniaKeyInterop.PressedButton (props.PointerUpdateKind),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerPressed (e);
        }

        /// <inheritdoc/>
        protected override void OnPointerReleased (AvPointerReleasedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _host.HandlePointerReleased (
                AvaloniaKeyInterop.ReleasedButton (props.PointerUpdateKind),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerReleased (e);
        }

        /// <inheritdoc/>
        protected override void OnPointerMoved (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _host.HandlePointerMoved (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerMoved (e);
        }

        /// <inheritdoc/>
        protected override void OnPointerWheelChanged (AvPointerWheelChangedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _host.HandlePointerWheel (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                new System.Drawing.Point ((int)e.Delta.X, (int)e.Delta.Y),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerWheelChanged (e);
        }

        /// <inheritdoc/>
        protected override void OnPointerExited (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _host.HandlePointerExited (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerExited (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (AvKeyEventArgs e)
        {
            if (_host.HandleKeyDown (AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers)))
                e.Handled = true;
            base.OnKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (AvKeyEventArgs e)
        {
            if (_host.HandleKeyUp (AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers)))
                e.Handled = true;
            base.OnKeyUp (e);
        }

        /// <inheritdoc/>
        protected override void OnTextInput (AvTextInputEventArgs e)
        {
            if (_host.HandleTextInput (e.Text ?? string.Empty))
                e.Handled = true;
            base.OnTextInput (e);
        }

        // ── IWindowBackend (the embedded region acts as the "window") ─────────────────────────────────

        System.Drawing.Point IWindowBackend.Location {
            get {
                var top = TopLevel.GetTopLevel (this);
                if (top is null) return System.Drawing.Point.Empty;
                var p = top.PointToScreen (this.TranslatePoint (new AvPoint (0, 0), top) ?? default);
                return new System.Drawing.Point (p.X, p.Y);
            }
            set { /* Embedded region position is owned by the host layout. */ }
        }

        System.Drawing.Size IWindowBackend.Size {
            get => new System.Drawing.Size ((int)Bounds.Width, (int)Bounds.Height);
            set { /* Size is owned by the host layout. */ }
        }

        System.Drawing.Size IWindowBackend.ClientSize
            => new System.Drawing.Size ((int)Bounds.Width, (int)Bounds.Height);

        double IWindowBackend.Scaling => Scale <= 0 ? 1 : Scale;

        void IWindowBackend.Show () { /* The control is shown by the host visual tree. */ }

        void IWindowBackend.ShowDialog (IWindowBackend? owner) { /* Embedded surfaces are not shown modally. */ }

        void IWindowBackend.Hide () => IsVisible = false;

        void IWindowBackend.Close () { /* Lifetime is owned by the host. */ }

        void IWindowBackend.Activate () => Focus ();

        bool IWindowBackend.Enabled {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        string IWindowBackend.Title { set { /* no window title */ } }

        bool IWindowBackend.Topmost { get => false; set { } }

        void IWindowBackend.SetSystemDecorations (bool useSystemDecorations) { /* host owns chrome */ }

        void IWindowBackend.SetCursor (CursorType cursor) => Cursor = MapCursor (cursor);

        // Cursor property is Avalonia.Input.Cursor (aliased AvCursor below to avoid the Majorsilence.Forms.Cursor collision).

        void IWindowBackend.SetIcon (byte[]? iconPng) { /* host owns the window icon */ }

        System.Drawing.Size IWindowBackend.MinimumSize { set { } }

        System.Drawing.Size IWindowBackend.MaximumSize { set { } }

        bool IWindowBackend.CanResize { get => false; set { } }

        bool IWindowBackend.ShowInTaskbar { get => false; set { } }

        double IWindowBackend.Opacity { get => Opacity; set => Opacity = value; }

        FormWindowState IWindowBackend.WindowState { get => FormWindowState.Normal; set { } }

        System.Drawing.Point IWindowBackend.PointToClient (System.Drawing.Point screen)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return screen;
            var inTop = top.PointToClient (new PixelPoint (screen.X, screen.Y));
            var local = top.TranslatePoint (inTop, this) ?? inTop;
            return new System.Drawing.Point ((int)local.X, (int)local.Y);
        }

        System.Drawing.Point IWindowBackend.PointToScreen (System.Drawing.Point client)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return client;
            var inTop = this.TranslatePoint (new AvPoint (client.X, client.Y), top) ?? new AvPoint (client.X, client.Y);
            var screen = top.PointToScreen (inTop);
            return new System.Drawing.Point (screen.X, screen.Y);
        }

        void IWindowBackend.BeginMoveDrag () { /* the embedded region does not move the host window */ }

        void IWindowBackend.BeginResizeDrag (Backends.WindowEdge edge) { /* not resizable */ }

        void IWindowBackend.Invalidate ()
        {
            if (_painting) {
                _invalidatePending = true;
                return;
            }
            _dirty = true;
            ScheduleRender ();
        }

        // ── INativeControlHostBackend (native Avalonia controls hosted inside the Majorsilence scene) ─────

        void INativeControlHostBackend.AttachNativeControl (NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not AvControl control)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, control))
                Children.Remove (existing);

            _overlays[host] = control;
            if (!Children.Contains (control))
                Children.Add (control);
        }

        void INativeControlHostBackend.UpdateNativeControl (NativeControlHost host, System.Drawing.Rectangle logicalBounds, System.Drawing.Rectangle clipBounds, bool visible)
        {
            if (!_overlays.TryGetValue (host, out var control))
                return;

            Canvas.SetLeft (control, logicalBounds.X);
            Canvas.SetTop (control, logicalBounds.Y);
            // Intermediate layout passes can transiently report a negative size; Avalonia's Width/Height
            // setters throw on negative values, so clamp to zero — the next layout pass corrects it (see
            // the matching fix/comment in MajorsilenceFormsWindowHost.UpdateNativeControl).
            control.Width = Math.Max (0, logicalBounds.Width);
            control.Height = Math.Max (0, logicalBounds.Height);
            control.IsVisible = visible;

            // Clip to the visible viewport (local to the control). Null when fully visible.
            control.Clip = clipBounds == logicalBounds
                ? null
                : new RectangleGeometry (new Rect (
                    clipBounds.X - logicalBounds.X, clipBounds.Y - logicalBounds.Y,
                    Math.Max (0, clipBounds.Width), Math.Max (0, clipBounds.Height)));
        }

        void INativeControlHostBackend.DetachNativeControl (NativeControlHost host)
        {
            if (_overlays.Remove (host, out var control))
                Children.Remove (control);
        }

        // ── File/folder pickers (delegated to the host TopLevel's storage provider) ──────────────────

        private static Avalonia.Platform.Storage.FilePickerFileType[] MapFilters (System.Collections.Generic.IReadOnlyList<Backends.FileDialogFilter> filters)
            => filters.Select (f => new Avalonia.Platform.Storage.FilePickerFileType (f.Name) {
                Patterns = f.Patterns.ToList ()
            }).ToArray ();

        private async System.Threading.Tasks.Task<Avalonia.Platform.Storage.IStorageFolder?> ResolveStartFolder (string? initialDirectory)
        {
            var top = TopLevel.GetTopLevel (this);
            return top is null || initialDirectory is null
                ? null
                : await top.StorageProvider.TryGetFolderFromPathAsync (new System.Uri (initialDirectory));
        }

        async System.Threading.Tasks.Task<string[]> IWindowBackend.ShowOpenFileDialog (Backends.OpenFileRequest request)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return Array.Empty<string> ();

            var result = await top.StorageProvider.OpenFilePickerAsync (new Avalonia.Platform.Storage.FilePickerOpenOptions {
                AllowMultiple = request.AllowMultiple,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                Title = request.Title,
                FileTypeFilter = MapFilters (request.Filters)
            });
            return result.Select (f => f.GetFullPath ()).WhereNotNull ().ToArray ();
        }

        async System.Threading.Tasks.Task<string?> IWindowBackend.ShowSaveFileDialog (Backends.SaveFileRequest request)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return null;

            var result = await top.StorageProvider.SaveFilePickerAsync (new Avalonia.Platform.Storage.FilePickerSaveOptions {
                DefaultExtension = request.DefaultExtension,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                SuggestedFileName = request.SuggestedFileName,
                Title = request.Title,
                FileTypeChoices = MapFilters (request.Filters)
            });
            return result?.GetFullPath ();
        }

        async System.Threading.Tasks.Task<string?> IWindowBackend.ShowOpenFolderDialog (Backends.FolderDialogRequest request)
        {
            var top = TopLevel.GetTopLevel (this);
            if (top is null) return null;

            var result = await top.StorageProvider.OpenFolderPickerAsync (new Avalonia.Platform.Storage.FolderPickerOpenOptions {
                AllowMultiple = false,
                SuggestedStartLocation = await ResolveStartFolder (request.InitialDirectory),
                Title = request.Title
            });
            return result.Select (f => f.GetFullPath ()).WhereNotNull ().FirstOrDefault ();
        }

        private static readonly System.Collections.Generic.Dictionary<CursorType, AvCursor> _cursorCache = new ();

        private static AvCursor MapCursor (CursorType cursor)
        {
            if (_cursorCache.TryGetValue (cursor, out var cached))
                return cached;

            var type = cursor switch {
                CursorType.Arrow => StandardCursorType.Arrow,
                CursorType.AppStarting => StandardCursorType.AppStarting,
                CursorType.Cross => StandardCursorType.Cross,
                CursorType.Hand => StandardCursorType.Hand,
                CursorType.Help => StandardCursorType.Help,
                CursorType.Ibeam => StandardCursorType.Ibeam,
                CursorType.No => StandardCursorType.No,
                CursorType.UpArrow => StandardCursorType.UpArrow,
                CursorType.Wait => StandardCursorType.Wait,
                CursorType.SizeAll => StandardCursorType.SizeAll,
                CursorType.SizeNorthSouth => StandardCursorType.SizeNorthSouth,
                CursorType.SizeWestEast => StandardCursorType.SizeWestEast,
                CursorType.TopSide => StandardCursorType.TopSide,
                CursorType.BottomSide => StandardCursorType.BottomSide,
                CursorType.LeftSide => StandardCursorType.LeftSide,
                CursorType.RightSide => StandardCursorType.RightSide,
                CursorType.TopLeftCorner => StandardCursorType.TopLeftCorner,
                CursorType.TopRightCorner => StandardCursorType.TopRightCorner,
                CursorType.BottomLeftCorner => StandardCursorType.BottomLeftCorner,
                CursorType.BottomRightCorner => StandardCursorType.BottomRightCorner,
                CursorType.DragCopy => StandardCursorType.DragCopy,
                CursorType.DragLink => StandardCursorType.DragLink,
                CursorType.DragMove => StandardCursorType.DragMove,
                _ => StandardCursorType.Arrow
            };

            var avCursor = new AvCursor (type);
            _cursorCache[cursor] = avCursor;
            return avCursor;
        }
    }
}
