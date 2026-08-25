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
        private bool _painting;
        private bool _invalidatePending;

        internal bool IsDirty = true;

        internal MajorsilenceFormsWindowHost (WindowBase owner)
        {
            _owner = owner;

            // Default to custom chrome (Windows/Linux). On macOS the Form constructor switches to the
            // NATIVE title bar and extends our content up into it (SetSystemDecorations(true) +
            // SetExtendClientIntoTitleBar), so the OS keeps the traffic lights / rounded corners / shadow.
            WindowDecorations = WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = true;

            // On macOS, force an opaque backdrop so the extended title-bar area doesn't pick up the
            // translucent "vibrancy" effect and our content renders solid.
            if (OperatingSystem.IsMacOS ())
                TransparencyLevelHint = new[] { WindowTransparencyLevel.None };

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
                _opened = true;
                EnsureFramebuffer ();
                StartRenderTimer ();
            };
            Closed += (_, _) => { StopRenderTimer (); _owner.OnBackendClosed (); };
            PositionChanged += (_, _) => _owner.OnBackendMoved ();
            Activated += (_, _) => _owner.OnBackendActivated ();
            Deactivated += (_, _) => _owner.OnBackendDeactivated ();

            AvaloniaGestureWiring.Attach (this, _owner, () => RenderScaling);
        }

        private void OnWindowClosing (object? sender, WindowClosingEventArgs e)
        {
            if (_owner.OnBackendClosing ())
                e.Cancel = true;
        }

        private void OnSurfaceSizeChanged (object? sender, SizeChangedEventArgs e)
        {
            // Any real resize means Avalonia's own ClientSize is now the truth, whether it came from the
            // programmatic write that set this or from the user dragging an edge.
            _pendingClientSize = null;

            EnsureFramebuffer ();
        }

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
                // A WriteableBitmap arrives with whatever was in that memory. The paint below is
                // trusted to cover the whole surface, but if it ever does not -- a region outside the
                // laid-out client area, a frame caught mid-resize -- the gap displays uninitialised
                // heap as a dark band with ghosts of unrelated content in it. Cheap to rule out, and
                // showing uninitialised memory is not defensible whatever the cause.
                ClearFramebuffer (_framebuffer);

                _surface.Source = _framebuffer;
                IsDirty = true;
            }

            return _framebuffer is not null;
        }

        private static void ClearFramebuffer (WriteableBitmap framebuffer)
        {
            try {
                using var fb = framebuffer.Lock ();
                var info = new SKImageInfo (fb.Size.Width, fb.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create (info, fb.Address, fb.RowBytes);
                surface?.Canvas.Clear (SKColors.Transparent);
            } catch (Exception ex) {
                // Never let this stop a frame from being drawn: an uncleared buffer is a cosmetic
                // problem, a throw here would be a black window.
                Console.Error.WriteLine ($"[MF] ClearFramebuffer error: {ex}");
            }
        }

        private void StartRenderTimer ()
        {
            _renderTimer = new DispatcherTimer (DispatcherPriority.Render) {
                Interval = TimeSpan.FromMilliseconds (16)
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

            _painting = true;
            _invalidatePending = false;
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
                Console.Error.WriteLine ($"[MF] PaintFrame error: {ex}");
            } finally {
                _painting = false;
            }

            _surface.InvalidateVisual ();

            // A control changed state during rendering — mark dirty so the timer picks it up next tick.
            if (_invalidatePending)
                IsDirty = true;
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

        // See WheelDeltaAccumulator: Avalonia's wheel deltas are in lines, WinForms' are in
        // multiples of 120, and the difference made scrolling roughly a hundredth of its proper speed.
        private readonly Backends.WheelDeltaAccumulator _wheel = new ();

        protected override void OnPointerWheelChanged (AvPointerWheelChangedEventArgs e)
        {
            var delta = _wheel.Add (e.Delta.X, e.Delta.Y);

            // Nothing whole accumulated yet -- raising a zero-delta wheel event would be noise, and
            // WinForms does not report one either.
            if (delta.X == 0 && delta.Y == 0) {
                base.OnPointerWheelChanged (e);
                return;
            }

            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerWheel (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * RenderScaling), (int)(pos.Y * RenderScaling),
                delta,
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
            set {
                Width = value.Width;
                Height = value.Height;

                // Remember what was asked for until Avalonia catches up. Assigning Width/Height is a
                // REQUEST: Avalonia reconciles ClientSize on its next layout pass, so a read-back in the
                // same breath still saw the old size. WinForms resizes through SetWindowPos and reads back
                // the new size immediately, so ported code that sets a size and then uses Width/Height --
                // a very ordinary thing to do -- silently computed against the previous one. (This is the
                // "post-show Form.Size writes are silently ignored" note in the port plan: the write was
                // never ignored, only invisible for one tick.)
                _pendingClientSize = value;
            }
        }

        // Set by the Size setter, cleared as soon as a real resize arrives (see OnSurfaceSizeChanged).
        // Short-lived by construction: the write that sets it is itself what triggers the resize.
        private System.Drawing.Size? _pendingClientSize;

        // Set once the window has actually opened; before that, Avalonia's ClientSize is a default the
        // platform invented, not anything this window was asked to be.
        private bool _opened;

        System.Drawing.Size Backends.IWindowBackend.ClientSize
        {
            get {
                // Before the window opens, answer with the size the caller ASKED for (Width/Height store
                // pending values; the hint above makes client size and window size the same thing here).
                // Avalonia only reconciles ClientSize at open, so reading it early returns its default --
                // and everything that lays out during a Form's constructor (anchor captures, a themed
                // form sizing its root panel) trusted that phantom size. The visible symptom: anchored
                // designer controls collapsed to zero width, because their anchor distances were captured
                // against the phantom and then applied against the real size.
                if (!_opened && !double.IsNaN (Width) && !double.IsNaN (Height) && Width > 0 && Height > 0)
                    return new System.Drawing.Size ((int)Width, (int)Height);

                // A programmatic resize that Avalonia has not applied yet answers with what was asked
                // for, so the read-back is synchronous as it is in WinForms. Only until the resize lands
                // -- after that this reads reality again, which is what keeps a USER dragging the window
                // edge from being reported as the last programmatic size.
                if (_pendingClientSize is { } pending)
                    return pending;

                return new System.Drawing.Size ((int)ClientSize.Width, (int)ClientSize.Height);
            }
        }

        double Backends.IWindowBackend.Scaling => RenderScaling;

        // The native window handle (HWND on Windows) from Avalonia's TopLevel. Unqualified
        // TryGetPlatformHandle resolves to the inherited Avalonia Window method, not this explicit impl.
        System.IntPtr Backends.IWindowBackend.TryGetPlatformHandle ()
            => TryGetPlatformHandle ()?.Handle ?? System.IntPtr.Zero;

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

        // Avalonia's Window exposes exactly this; setting it before Show() is what keeps an overlay
        // from stealing focus from the window being dragged over.
        bool Backends.IWindowBackend.ShowActivated {
            get => ShowActivated;
            set => ShowActivated = value;
        }

        bool Backends.IWindowBackend.Enabled {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        string Backends.IWindowBackend.Title { set => Title = value; }

        bool Backends.IWindowBackend.Topmost {
            get => Topmost;
            set => Topmost = value;
        }

        void Backends.IWindowBackend.SetShaped (bool shaped)
        {
            _shaped = shaped;
            ApplyBackdrop ();
        }

        // A window needs a see-through backdrop when it is shaped (paints only inside a Region) or
        // translucent (Opacity < 1) -- WinForms' layered-window cases. Without it the clip or the alpha
        // is composited over the window's own opaque fill, so a half-transparent drag preview reads as a
        // solid sheet and a shaped overlay as a solid rectangle.
        //
        // macOS is the awkward one twice over: the constructor forces an opaque backdrop (so the extended
        // title-bar area does not pick up vibrancy), and the platform drops transparency again whenever
        // the window is resized -- which a drag overlay is, constantly.
        private void ApplyBackdrop ()
        {
            var seeThrough = _shaped || Opacity < 1.0;

            // Re-declaring transparency is not free -- the platform can rebuild the window's backdrop for
            // it -- and a drag overlay reassigns its Region continuously, which lands here every time. Only
            // act when the state actually changes, or when the platform has dropped what we asked for
            // (macOS does that on resize, which is why OnResized calls back in).
            if (_lastSeeThrough == seeThrough &&
                (!seeThrough || ActualTransparencyLevel == WindowTransparencyLevel.Transparent))
                return;

            _lastSeeThrough = seeThrough;

            TransparencyLevelHint = seeThrough
                ? new[] { WindowTransparencyLevel.Transparent }
                : new[] { WindowTransparencyLevel.None };

            if (seeThrough)
                Background = Brushes.Transparent;
        }


        private bool _shaped;
        private bool? _lastSeeThrough;

        // A shaped window loses its transparency when the platform window is resized -- the overlay a
        // docking drag puts up is created tiny and then stretched over the whole panel, so by the time it
        // matters the backdrop is opaque again. Re-declaring the hint after each resize keeps it.
        protected override void OnResized (WindowResizedEventArgs e)
        {
            base.OnResized (e);

            if ((_shaped || Opacity < 1.0) && ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
                ApplyBackdrop ();
        }

        void Backends.IWindowBackend.SetSystemDecorations (bool useSystemDecorations)
        {
            WindowDecorations = useSystemDecorations ? WindowDecorations.Full : WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = !useSystemDecorations;
        }

        void Backends.IWindowBackend.SetExtendClientIntoTitleBar (bool extend, int titleBarHeight)
        {
            // Keep the native chrome (WindowDecorations.Full) but extend our content up into the title
            // bar; on macOS this yields the full-size content view with floating traffic lights.
            ExtendClientAreaToDecorationsHint = extend;
            ExtendClientAreaTitleBarHeightHint = extend && titleBarHeight > 0 ? titleBarHeight : -1;
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

        void Backends.IWindowBackend.Invalidate ()
        {
            if (_painting) {
                _invalidatePending = true;
                return;
            }
            IsDirty = true;
        }

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
            // Intermediate layout passes (e.g. a Dock=Fill sibling measured before an adjacent
            // Dock=Top/Bottom control settles its own height) can transiently report a negative size;
            // Avalonia's Width/Height setters throw on negative values, so clamp to zero — the next
            // layout pass corrects it, same as the Majorsilence-drawn siblings which clip silently instead.
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
            set {
                Opacity = value;
                ApplyBackdrop ();
            }
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

            // The popup MUST activate. A non-activating (ShowActivated=false/Focusable=false) window
            // on Windows becomes WS_EX_NOACTIVATE, and clicking it yields WM_MOUSEACTIVATE handled as
            // "eat the click" -- the pointer press/release then fall through to the window beneath, so
            // dropdown items can never be clicked (found via a real migrated app, TownSuite frmMainAR:
            // menus opened but selecting an item did nothing; the release landed on the main form, not
            // the popup). Activation is required to receive pointer input.
            //
            // Activating a popup deactivates its parent, whose deactivation would otherwise instantly
            // dismiss the just-opened popup (an earlier attempt disabled activation to avoid this, but
            // that broke item clicks as above). That is now handled generically by checking whether the
            // popup's own WindowBase.IsActive is (or becomes) true rather than inferring it from
            // activate/deactivate event ORDER -- real clicks showed this popup's Activated arriving
            // BEFORE its parent's Deactivated on Linux/Mutter/XWayland, the opposite of what an
            // order-based "activation cancels a pending close" scheme assumes, which made an earlier
            // fix along those lines fail 100% of the time rather than intermittently. See
            // Application.ScheduleClosePopupsOnDeactivate and WindowBase.IsActive.
            ShowActivated = true;
            Focusable = true;

            // On macOS, a borderless window with ExtendClientAreaToDecorationsHint = true (inherited
            // from the base host) is rendered with a translucent "vibrancy" backdrop. For a menu or
            // combo-box popup that shows up as a grey, blurry square instead of the menu. The base
            // host already forces an opaque backdrop on macOS; a popup additionally has no chrome to
            // extend into, so collapse the extended client area too.
            if (OperatingSystem.IsMacOS ()) {
                ExtendClientAreaToDecorationsHint = false;
                TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            }
        }
    }
}
