using System.Drawing;
using System.Drawing.Imaging;
using SkiaSharp;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// The System.Windows.Forms control both WinForms hosts draw and receive input through. Each paint
    /// renders the owning Majorsilence.Forms <see cref="MF.WindowBase"/> via <c>RenderFrame</c> into a
    /// SkiaSharp surface backed by a GDI bitmap (the same present path SkiaSharp's own WinForms
    /// SKControl uses — done directly here to avoid that package's type collisions with the core
    /// assembly's SkiaSharp.Views.Desktop compatibility shims and its legacy OpenTK dependency), and
    /// WinForms mouse/keyboard events are translated into the neutral <c>WindowBase.Handle*</c> path.
    ///
    /// WinForms coordinates are already physical device pixels (PerMonitorV2), which is exactly what
    /// the neutral input path takes, so pointer positions pass through unconverted.
    /// </summary>
    internal sealed class SkiaHostControl : WF.Control
    {
        private readonly Func<MF.WindowBase?> _owner;
        private readonly Func<double> _scaling;
        private System.Drawing.Point _lastPointer;
        private Bitmap? _backBuffer;

        /// <summary>Cleared onto the canvas before each frame. Null leaves the canvas untouched, letting
        /// the scene's own background paint everything (the standalone window host does that).</summary>
        [System.ComponentModel.Browsable (false)]
        [System.ComponentModel.DesignerSerializationVisibility (System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal SKColor? ClearColor { get; set; }

        internal SkiaHostControl (Func<MF.WindowBase?> owner, Func<double> scaling)
        {
            _owner = owner;
            _scaling = scaling;

            // The frame is painted in full every time, so skip the background erase (kills flicker)
            // and let WinForms know everything happens in OnPaint.
            SetStyle (WF.ControlStyles.AllPaintingInWmPaint | WF.ControlStyles.UserPaint | WF.ControlStyles.Opaque, true);
            SetStyle (WF.ControlStyles.Selectable, true);
            TabStop = true;
        }

        // ── Painting ─────────────────────────────────────────────────────────────

        protected override void OnPaint (WF.PaintEventArgs e)
        {
            base.OnPaint (e);

            var width = ClientSize.Width;
            var height = ClientSize.Height;
            if (width <= 0 || height <= 0)
                return;

            if (_backBuffer is null || _backBuffer.Width != width || _backBuffer.Height != height) {
                _backBuffer?.Dispose ();
                _backBuffer = new Bitmap (width, height, PixelFormat.Format32bppPArgb);
            }

            var data = _backBuffer.LockBits (new Rectangle (0, 0, width, height), ImageLockMode.WriteOnly, _backBuffer.PixelFormat);
            try {
                var info = new SKImageInfo (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create (info, data.Scan0, data.Stride);
                if (surface is null)
                    return;

                if (ClearColor is { } clear)
                    surface.Canvas.Clear (clear);

                _owner ()?.RenderFrame (surface.Canvas, width, height, _scaling ());
                surface.Canvas.Flush ();
            } finally {
                _backBuffer.UnlockBits (data);
            }

            e.Graphics.DrawImageUnscaled (_backBuffer, 0, 0);
        }

        protected override void OnPaintBackground (WF.PaintEventArgs pevent)
        {
            // Intentionally empty: OnPaint covers every pixel (ControlStyles.Opaque).
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                _backBuffer?.Dispose ();
                _backBuffer = null;
            }
            base.Dispose (disposing);
        }

        // ── Pointer ──────────────────────────────────────────────────────────────

        protected override void OnMouseDown (WF.MouseEventArgs e)
        {
            base.OnMouseDown (e);
            Focus ();
            _lastPointer = e.Location;
            _owner ()?.HandlePointerPressed (WinFormsKeyInterop.ToButton (e.Button), e.X, e.Y, WinFormsKeyInterop.CurrentModifiers ());
        }

        protected override void OnMouseUp (WF.MouseEventArgs e)
        {
            base.OnMouseUp (e);
            _lastPointer = e.Location;
            _owner ()?.HandlePointerReleased (WinFormsKeyInterop.ToButton (e.Button), e.X, e.Y, WinFormsKeyInterop.CurrentModifiers ());
        }

        protected override void OnMouseMove (WF.MouseEventArgs e)
        {
            base.OnMouseMove (e);
            _lastPointer = e.Location;
            _owner ()?.HandlePointerMoved (WinFormsKeyInterop.ToButton (e.Button), e.X, e.Y, WinFormsKeyInterop.CurrentModifiers ());
        }

        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);
            _owner ()?.HandlePointerExited (MF.MouseButtons.None, _lastPointer.X, _lastPointer.Y, WinFormsKeyInterop.CurrentModifiers ());
        }

        protected override void OnMouseWheel (WF.MouseEventArgs e)
        {
            base.OnMouseWheel (e);
            var delta = new System.Drawing.Point (0, WinFormsKeyInterop.NotchesFromWheelDelta (e.Delta));
            _owner ()?.HandlePointerWheel (MF.MouseButtons.None, e.X, e.Y, delta, WinFormsKeyInterop.CurrentModifiers ());
        }

        // ── Keyboard ─────────────────────────────────────────────────────────────

        // Majorsilence.Forms does its own focus traversal and navigation-key handling, so every key —
        // arrows, Tab, Enter, Escape — must reach OnKeyDown instead of being consumed by WinForms'
        // dialog-navigation preprocessing.
        protected override bool IsInputKey (WF.Keys keyData) => true;

        protected override void OnKeyDown (WF.KeyEventArgs e)
        {
            base.OnKeyDown (e);
            if (_owner ()?.HandleKeyDown (WinFormsKeyInterop.ToKeys (e.KeyData)) == true) {
                e.Handled = true;
                // A key the scene consumed (a shortcut, an editing key) must not also arrive as typed
                // text through KeyPress — mirrors how the Avalonia/Uno hosts' "handled" flag suppresses
                // their platform's subsequent text event.
                e.SuppressKeyPress = true;
            }
        }

        protected override void OnKeyUp (WF.KeyEventArgs e)
        {
            base.OnKeyUp (e);
            if (_owner ()?.HandleKeyUp (WinFormsKeyInterop.ToKeys (e.KeyData)) == true)
                e.Handled = true;
        }

        protected override void OnKeyPress (WF.KeyPressEventArgs e)
        {
            base.OnKeyPress (e);
            if (!char.IsControl (e.KeyChar) && _owner ()?.HandleTextInput (e.KeyChar.ToString ()) == true)
                e.Handled = true;
        }
    }
}
