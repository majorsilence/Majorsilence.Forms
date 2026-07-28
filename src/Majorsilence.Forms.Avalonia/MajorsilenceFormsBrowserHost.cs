#if BROWSER
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
    /// The browser/WebAssembly counterpart to <see cref="MajorsilenceFormsWindowHost"/>. Avalonia's
    /// browser platform has no OS window manager (<c>BrowserWindowingPlatform.CreateWindow</c> always
    /// throws — see Avalonia's own WindowingPlatform.cs), only a single embeddable view per page
    /// (<c>ISingleViewApplicationLifetime.MainView</c>). So instead of an Avalonia <c>Window</c>, every
    /// Majorsilence.Forms window (the main form, any further top-level forms, and popups like ComboBox
    /// dropdowns/menus) is a <see cref="Canvas"/>:
    ///   - The first non-popup window becomes <see cref="MainHost"/> and registers itself as MainView,
    ///     filling the browser viewport via ordinary Avalonia layout (no explicit Width/Height, so the
    ///     default Stretch alignment fills whatever the single-view TopLevel gives it).
    ///   - Everything else (popups, and any additional top-level forms) is added as an absolutely
    ///     positioned child of <see cref="MainHost"/>'s own Canvas, using the same coordinate space as
    ///     "screen" coordinates -- there is only one "screen" (the page), so PointToScreen/PointToClient
    ///     and Location need no special-casing between the root and its overlay children.
    ///
    /// Known gap: outside-click popup dismissal that relies on real OS window deactivation
    /// (Application.ScheduleClosePopupsOnDeactivate) does not fire here, since a Canvas has no such
    /// concept. Dismissal via clicking elsewhere *inside* the app still works (Control.RaiseMouseDown
    /// closes popups whenever a click lands outside the active popup's own control tree, independent of
    /// window activation) -- only losing focus to something outside the browser tab entirely is unhandled.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage ("Design", "CA1001", Justification = "_framebuffer is disposed in IWindowBackend.Close/DetachedFromVisualTree; there is no owning Window to dispose through here.")]
    internal sealed class MajorsilenceFormsBrowserHost : Canvas, IWindowBackend, INativeControlHostBackend
    {
        internal static MajorsilenceFormsBrowserHost? MainHost { get; private set; }

        private readonly WindowBase _owner;
        private readonly bool _isRoot;
        private System.Drawing.Point _location;
        private System.Drawing.Size _size;

        private WriteableBitmap? _framebuffer;
        private bool _dirty = true;
        private bool _renderPending;
        private bool _painting;
        private bool _invalidatePending;
        private readonly Dictionary<NativeControlHost, AvControl> _overlays = new ();
        private readonly Image _surface;

        internal MajorsilenceFormsBrowserHost (WindowBase owner, bool isPopup)
        {
            _owner = owner;
            _isRoot = !isPopup && MainHost is null;

            Focusable = true;
            Background = Brushes.Transparent;

            _surface = new Image {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            Canvas.SetLeft (_surface, 0);
            Canvas.SetTop (_surface, 0);
            Children.Add (_surface);

            // LayoutUpdated fires after every completed layout pass, so it catches the (asynchronous,
            // relative to construction) moment the single-view TopLevel actually assigns this control a
            // real size -- unlike a one-shot render scheduled from OnAttachedToVisualTree, which can fire
            // before that first real layout pass has run and then never gets asked again.
            LayoutUpdated += (_, _) => ScheduleRender ();

            if (_isRoot) {
                MainHost = this;

                // A bare Canvas has no intrinsic size of its own to request; Stretch alignment is what
                // makes the single-view TopLevel's arrange pass give it the full viewport instead of 0x0.
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

                if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime lifetime)
                    lifetime.MainView = this;
            }
        }

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree (VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree (e);
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

        private double Scale => TopLevel.GetTopLevel (this)?.RenderScaling ?? 1;

        // ── Rendering (identical strategy to MajorsilenceFormsPresenter/MajorsilenceFormsWindowHost:
        // paint the Majorsilence scene into a WriteableBitmap framebuffer, present via a bottom Image
        // child) ──

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

        private void PaintFrame ()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            if (!EnsureFramebuffer () || _framebuffer is null)
                return;

            if (!_dirty && !_owner.adapter.NeedsPaint)
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

                surface.Canvas.Clear (SKColors.Transparent);

                _owner.RenderFrame (surface.Canvas, fb.Size.Width, fb.Size.Height, Scale);
            } catch (Exception ex) {
                Console.Error.WriteLine ($"[CF] BrowserHost PaintFrame error: {ex.Message}");
            } finally {
                _painting = false;
            }

            _surface.Width = Bounds.Width;
            _surface.Height = Bounds.Height;
            _surface.Source = _framebuffer;
            _surface.InvalidateVisual ();

            if (_invalidatePending)
                ScheduleRender ();
        }

        // ── Input forwarding (Avalonia → Majorsilence.Forms; positions scaled to physical pixels) ───────

        protected override void OnPointerPressed (AvPointerPressedEventArgs e)
        {
            Focus ();

            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerPressed (
                AvaloniaKeyInterop.PressedButton (props.PointerUpdateKind),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerPressed (e);
        }

        protected override void OnPointerReleased (AvPointerReleasedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerReleased (
                AvaloniaKeyInterop.ReleasedButton (props.PointerUpdateKind),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerReleased (e);
        }

        protected override void OnPointerMoved (AvPointerEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerMoved (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
                AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers));
            base.OnPointerMoved (e);
        }

        protected override void OnPointerWheelChanged (AvPointerWheelChangedEventArgs e)
        {
            var pos = e.GetPosition (this);
            var props = e.GetCurrentPoint (this).Properties;
            _owner.HandlePointerWheel (
                AvaloniaKeyInterop.ToMouseButtons (props),
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
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
                (int)(pos.X * Scale), (int)(pos.Y * Scale),
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

        // ── IWindowBackend ───────────────────────────────────────────────────────────────────────────

        System.Drawing.Point IWindowBackend.Location {
            get => _location;
            set {
                _location = value;
                if (!_isRoot) {
                    Canvas.SetLeft (this, value.X);
                    Canvas.SetTop (this, value.Y);
                }
            }
        }

        System.Drawing.Size IWindowBackend.Size {
            get => _isRoot ? new System.Drawing.Size ((int)Bounds.Width, (int)Bounds.Height) : _size;
            set {
                _size = value;
                // The root fills the browser viewport via ordinary layout (Stretch); it doesn't own an
                // explicit size to set. Popups/secondary windows size themselves explicitly.
                if (!_isRoot) {
                    Width = value.Width;
                    Height = value.Height;
                }
            }
        }

        System.Drawing.Size IWindowBackend.ClientSize
            => new System.Drawing.Size ((int)Bounds.Width, (int)Bounds.Height);

        double IWindowBackend.Scaling => Scale <= 0 ? 1 : Scale;

        void IWindowBackend.Show ()
        {
            if (!_isRoot && MainHost is not null && !MainHost.Children.Contains (this))
                MainHost.Children.Add (this);
            IsVisible = true;
        }

        void IWindowBackend.ShowDialog (IWindowBackend? owner)
            // No true modal window concept in the browser; the parent-disable + blocking-wait behaviour
            // that makes this act modal is handled above this seam (WindowBase.ShowDialog + RunModalLoop).
            => ((IWindowBackend)this).Show ();

        void IWindowBackend.Hide () => IsVisible = false;

        void IWindowBackend.Close ()
        {
            if (!_isRoot)
                MainHost?.Children.Remove (this);
            _framebuffer?.Dispose ();
            _framebuffer = null;
        }

        void IWindowBackend.Activate () => Focus ();

        bool IWindowBackend.Enabled {
            get => IsEnabled;
            set => IsEnabled = value;
        }

        string IWindowBackend.Title { set { /* no browser tab/window title support yet */ } }

        bool IWindowBackend.Topmost { get => false; set { } }

        void IWindowBackend.SetSystemDecorations (bool useSystemDecorations) { /* no chrome in the browser */ }

        void IWindowBackend.SetCursor (CursorType cursor) => Cursor = MapCursor (cursor);

        void IWindowBackend.SetIcon (byte[]? iconPng) { /* no window icon in the browser */ }

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

        void IWindowBackend.BeginMoveDrag () { /* no window manager to drag against */ }

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

        // ── INativeControlHostBackend ────────────────────────────────────────────────────────────────

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
            control.Width = Math.Max (0, logicalBounds.Width);
            control.Height = Math.Max (0, logicalBounds.Height);
            control.IsVisible = visible;

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
#endif
