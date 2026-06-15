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

namespace Modern.Forms
{
    /// <summary>
    /// Internal Avalonia 12 Window that hosts Modern.Forms rendering and forwards
    /// Avalonia input events into the Modern.Forms event pipeline.
    ///
    /// Rendering strategy: a <see cref="WriteableBitmap"/> is locked each frame,
    /// Skia draws the Modern.Forms scene directly into the framebuffer, and an
    /// <see cref="Avalonia.Controls.Image"/> control displays the result. This mirrors the original
    /// native-framebuffer approach and is reliable across all Avalonia 12 platforms.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage ("Design", "CA1001", Justification = "_framebuffer is disposed in OnClosed; Window lifecycle manages the call.")]
    internal class ModernFormsWindowHost : Window
    {
        private readonly WindowBase _owner;

        internal AvPointerPressedEventArgs? LastPointerPressed;

        // The bitmap is the "framebuffer". We create it at physical pixel size;
        // Avalonia displays it at logical size via the Image control.
        private WriteableBitmap? _framebuffer;
        private readonly AvImage _surface;
        private DispatcherTimer? _renderTimer;

        // Set by InvalidateVisual() calls so the timer knows to repaint.
        internal bool IsDirty = true;

        internal ModernFormsWindowHost (WindowBase owner)
        {
            _owner = owner;

            // Modern.Forms draws its own decorations on Windows/Linux.
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

            // Grid stretches its children to fill available space.
            var grid = new Grid {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add (_surface);
            Content = grid;

            Closing += OnWindowClosing;

            // Resize → recreate framebuffer to match new logical size.
            _surface.SizeChanged += OnSurfaceSizeChanged;

            Opened += (_, _) => {
                // Ensure framebuffer exists before the first timer tick fires.
                // OnSurfaceSizeChanged may not have run yet if layout hasn't completed.
                if (_framebuffer is null) {
                    var scaling = _owner.Scaling;
                    var w = Math.Max (1, (int)(ClientSize.Width  * scaling));
                    var h = Math.Max (1, (int)(ClientSize.Height * scaling));
                    _framebuffer = new WriteableBitmap (
                        new PixelSize (w, h),
                        new Vector (96 * scaling, 96 * scaling),
                        PixelFormat.Bgra8888, AlphaFormat.Premul);
                    _surface.Source = _framebuffer;
                }

                StartRenderTimer ();
            };
            Closed += (_, _) => StopRenderTimer ();
        }

        private void OnWindowClosing (object? sender, WindowClosingEventArgs e) { }

        private void OnSurfaceSizeChanged (object? sender, SizeChangedEventArgs e)
        {
            var scaling = _owner.Scaling;
            var dpi = 96 * scaling;

            // Create at physical pixel size so our Scaled* drawing pipeline maps 1:1.
            // Setting the DPI to match tells Avalonia the bitmap represents the full
            // logical area (so it displays at the right logical size).
            var physW = Math.Max (1, (int)Math.Round (e.NewSize.Width * scaling));
            var physH = Math.Max (1, (int)Math.Round (e.NewSize.Height * scaling));

            if (_framebuffer is null || _framebuffer.PixelSize.Width != physW || _framebuffer.PixelSize.Height != physH) {
                _framebuffer?.Dispose ();
                _framebuffer = new WriteableBitmap (
                    new PixelSize (physW, physH),
                    new Vector (dpi, dpi),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
                _surface.Source = _framebuffer;
                IsDirty = true;
            }
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
            if (_framebuffer is null)
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

                // Adapter and border widths are in LOGICAL pixels.
                var logicalW = (int)Math.Round (physW / scaling);
                var logicalH = (int)Math.Round (physH / scaling);

                var skInfo = new SKImageInfo (physW, physH, SKColorType.Bgra8888, SKAlphaType.Premul);

                using var surface = SKSurface.Create (skInfo, fb.Address, fb.RowBytes);

                if (surface is null)
                    return;

                var canvas = surface.Canvas;

                // Border widths in logical pixels → physical for canvas draw calls.
                var border = _owner.CurrentStyle.Border;
                var borderLeft = border.Left.GetWidth ();
                var borderTop = border.Top.GetWidth ();
                var physBorderLeft = (int)(borderLeft * scaling);
                var physBorderTop  = (int)(borderTop  * scaling);
                var physBorderRight  = (int)(border.Right.GetWidth ()  * scaling);
                var physBorderBottom = (int)(border.Bottom.GetWidth () * scaling);

                if (adapter.Left != borderLeft || adapter.Top != borderTop ||
                    adapter.Width != logicalW || adapter.Height != logicalH) {
                    adapter.SetBounds (borderLeft, borderTop, logicalW, logicalH);
                    adapter.PerformLayout ();
                }

                var e = new PaintEventArgs (skInfo, canvas, scaling);

                _owner.OnPaintBackground (e);
                canvas.DrawBorder (new Rectangle (0, 0, physW, physH), _owner.CurrentStyle);
                _owner.OnPaint (e);

                // Clip canvas to the inner client area (excludes borders).
                canvas.ClipRect (new SKRect (
                    physBorderLeft, physBorderTop,
                    physW - physBorderRight + 1, physH - physBorderBottom + 1));

                adapter.RaisePaintBackground (e);
                adapter.RaisePaint (e);

                canvas.Flush ();
            } catch (Exception ex) {
                Console.Error.WriteLine ($"[MF] PaintFrame error: {ex.Message}");
            }

            _surface.InvalidateVisual ();
        }

        // ── Input forwarding ──────────────────────────────────────────────────

        protected override void OnPointerPressed (AvPointerPressedEventArgs e)
        {
            LastPointerPressed = e;
            _owner.OnAvaloniaPointerPressed (e);
            base.OnPointerPressed (e);
        }

        protected override void OnPointerReleased (AvPointerReleasedEventArgs e)
        {
            _owner.OnAvaloniaPointerReleased (e);
            base.OnPointerReleased (e);
        }

        protected override void OnPointerMoved (AvPointerEventArgs e)
        {
            _owner.OnAvaloniaPointerMoved (e);
            base.OnPointerMoved (e);
        }

        protected override void OnPointerWheelChanged (AvPointerWheelChangedEventArgs e)
        {
            _owner.OnAvaloniaPointerWheel (e);
            base.OnPointerWheelChanged (e);
        }

        protected override void OnPointerExited (AvPointerEventArgs e)
        {
            _owner.OnAvaloniaPointerExited (e);
            base.OnPointerExited (e);
        }

        protected override void OnKeyDown (AvKeyEventArgs e)
        {
            _owner.OnAvaloniaKeyDown (e);
            base.OnKeyDown (e);
        }

        protected override void OnKeyUp (AvKeyEventArgs e)
        {
            _owner.OnAvaloniaKeyUp (e);
            base.OnKeyUp (e);
        }

        protected override void OnTextInput (AvTextInputEventArgs e)
        {
            _owner.OnAvaloniaTextInput (e);
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
    }

    /// <summary>
    /// Popup-specific host with no owner-window chrome.
    /// </summary>
    internal sealed class ModernFormsPopupWindowHost : ModernFormsWindowHost
    {
        internal ModernFormsPopupWindowHost (WindowBase owner) : base (owner)
        {
            Topmost = true;
        }
    }
}
