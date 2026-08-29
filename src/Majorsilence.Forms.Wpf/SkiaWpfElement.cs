using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
using WI = System.Windows.Input;
using MF = Majorsilence.Forms;
using WPoint = System.Windows.Point;

namespace Majorsilence.Forms.Wpf
{
    /// <summary>
    /// The WPF element both WPF hosts draw and receive input through. Each render pass renders the
    /// owning Majorsilence.Forms <see cref="MF.WindowBase"/> via <c>RenderFrame</c> into a SkiaSharp
    /// surface backed by a <see cref="WriteableBitmap"/> (the same present technique the WinForms
    /// backend's SkiaHostControl uses — done directly here rather than via SkiaSharp.Views.WPF to
    /// avoid that package's collisions with the core assembly's SkiaSharp.Views.Desktop shims), shown
    /// through a child <see cref="Image"/>. WPF mouse/keyboard events are translated into the neutral
    /// <c>WindowBase.Handle*</c> path.
    ///
    /// The seam's <c>RenderFrame</c> takes physical pixels and a scale factor; WPF works in DIPs, so
    /// the back-buffer is sized <c>DIP × dpiScale</c> and given <c>96 × dpiScale</c> DPI so WPF shows
    /// it 1:1 over the element's DIP bounds.
    /// </summary>
    internal sealed class SkiaWpfElement : Grid
    {
        private readonly Func<MF.WindowBase?> _owner;
        private readonly Image _image;
        private WriteableBitmap? _bitmap;
        private WPoint _lastPointer;
        private bool _renderQueued;

        /// <summary>Cleared onto the canvas before each frame; null leaves the scene's own background to paint.</summary>
        internal SKColor? ClearColor { get; set; }

        internal SkiaWpfElement (Func<MF.WindowBase?> owner)
        {
            _owner = owner;
            Focusable = true;
            FocusVisualStyle = null;
            Background = Brushes.Transparent;   // hit-test the whole surface, even fully transparent frames

            _image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode (_image, BitmapScalingMode.NearestNeighbor);
            Children.Add (_image);   // Grid stretches a single child to fill by default

            SizeChanged += (_, _) => RequestRender ();

            MouseDown += OnMouseDownHandler;
            MouseUp += OnMouseUpHandler;
            MouseMove += OnMouseMoveHandler;
            MouseLeave += OnMouseLeaveHandler;
            MouseWheel += OnMouseWheelHandler;
            KeyDown += OnKeyDownHandler;
            KeyUp += OnKeyUpHandler;
            TextInput += OnTextInputHandler;
        }

        /// <summary>The current monitor scale factor (DIP → device pixels).</summary>
        internal double Scaling
        {
            get
            {
                var source = PresentationSource.FromVisual (this);
                var m = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                return m > 0 ? m : 1.0;
            }
        }

        /// <summary>Queues a frame on the UI thread (coalesced).</summary>
        internal void RequestRender ()
        {
            if (!Dispatcher.CheckAccess ())
            {
                Dispatcher.BeginInvoke (new Action (RequestRender));
                return;
            }
            if (_renderQueued)
                return;
            _renderQueued = true;
            Dispatcher.BeginInvoke (System.Windows.Threading.DispatcherPriority.Render, new Action (RenderNow));
        }

        private void RenderNow ()
        {
            _renderQueued = false;

            var scale = Scaling;
            var dipW = ActualWidth;
            var dipH = ActualHeight;
            if (dipW <= 0 || dipH <= 0)
                return;

            var physW = Math.Max (1, (int) Math.Round (dipW * scale));
            var physH = Math.Max (1, (int) Math.Round (dipH * scale));

            if (_bitmap is null || _bitmap.PixelWidth != physW || _bitmap.PixelHeight != physH)
            {
                _bitmap = new WriteableBitmap (physW, physH, 96.0 * scale, 96.0 * scale, PixelFormats.Pbgra32, null);
                _image.Source = _bitmap;
            }

            _bitmap.Lock ();
            try
            {
                var info = new SKImageInfo (physW, physH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create (info, _bitmap.BackBuffer, _bitmap.BackBufferStride);
                if (surface is not null)
                {
                    if (ClearColor is { } clear)
                        surface.Canvas.Clear (clear);
                    _owner ()?.RenderFrame (surface.Canvas, physW, physH, scale);
                    surface.Canvas.Flush ();
                }
                _bitmap.AddDirtyRect (new Int32Rect (0, 0, physW, physH));
            }
            finally
            {
                _bitmap.Unlock ();
            }
        }

        // ── Pointer ──────────────────────────────────────────────────────────────
        // WPF mouse positions are DIPs; RenderFrame and the neutral input path take physical pixels.

        private (int x, int y) Phys (WPoint p)
        {
            var s = Scaling;
            return ((int) Math.Round (p.X * s), (int) Math.Round (p.Y * s));
        }

        private void OnMouseDownHandler (object sender, WI.MouseButtonEventArgs e)
        {
            Focus ();
            CaptureMouse ();
            _lastPointer = e.GetPosition (this);
            var (x, y) = Phys (_lastPointer);
            _owner ()?.HandlePointerPressed (WpfKeyInterop.ToButton (e.ChangedButton), x, y, WpfKeyInterop.CurrentModifiers ());
        }

        private void OnMouseUpHandler (object sender, WI.MouseButtonEventArgs e)
        {
            ReleaseMouseCapture ();
            _lastPointer = e.GetPosition (this);
            var (x, y) = Phys (_lastPointer);
            _owner ()?.HandlePointerReleased (WpfKeyInterop.ToButton (e.ChangedButton), x, y, WpfKeyInterop.CurrentModifiers ());
        }

        private void OnMouseMoveHandler (object sender, WI.MouseEventArgs e)
        {
            _lastPointer = e.GetPosition (this);
            var (x, y) = Phys (_lastPointer);
            _owner ()?.HandlePointerMoved (WpfKeyInterop.CurrentButtons (e), x, y, WpfKeyInterop.CurrentModifiers ());
        }

        private void OnMouseLeaveHandler (object sender, WI.MouseEventArgs e)
        {
            var (x, y) = Phys (_lastPointer);
            _owner ()?.HandlePointerExited (MF.MouseButtons.None, x, y, WpfKeyInterop.CurrentModifiers ());
        }

        private void OnMouseWheelHandler (object sender, WI.MouseWheelEventArgs e)
        {
            var (x, y) = Phys (e.GetPosition (this));
            var delta = new System.Drawing.Point (0, WpfKeyInterop.NotchesFromWheelDelta (e.Delta));
            _owner ()?.HandlePointerWheel (MF.MouseButtons.None, x, y, delta, WpfKeyInterop.CurrentModifiers ());
        }

        // ── Keyboard ─────────────────────────────────────────────────────────────
        // Majorsilence.Forms does its own focus traversal and navigation-key handling, so every key
        // must reach the neutral path rather than WPF's own navigation.

        private void OnKeyDownHandler (object sender, WI.KeyEventArgs e)
        {
            var key = e.Key == WI.Key.System ? e.SystemKey : e.Key;
            var forms = WpfKeyInterop.AddModifiers (WpfKeyInterop.ToFormsKey (key), WI.Keyboard.Modifiers);
            if (_owner ()?.HandleKeyDown (forms) == true)
                e.Handled = true;
        }

        private void OnKeyUpHandler (object sender, WI.KeyEventArgs e)
        {
            var key = e.Key == WI.Key.System ? e.SystemKey : e.Key;
            var forms = WpfKeyInterop.AddModifiers (WpfKeyInterop.ToFormsKey (key), WI.Keyboard.Modifiers);
            if (_owner ()?.HandleKeyUp (forms) == true)
                e.Handled = true;
        }

        private void OnTextInputHandler (object sender, WI.TextCompositionEventArgs e)
        {
            var text = e.Text;
            if (!string.IsNullOrEmpty (text) && !char.IsControl (text[0]) && _owner ()?.HandleTextInput (text) == true)
                e.Handled = true;
        }
    }
}
