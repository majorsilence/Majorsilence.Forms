using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the Paint event.
    /// </summary>
    public partial class PaintEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the PaintEventArgs class.
        /// </summary>
        public PaintEventArgs (SKImageInfo info, SKCanvas canvas, double scaling)
        {
            Info = info;
            Canvas = canvas;
            Scaling = scaling;
            device_matrix = canvas.TotalMatrix;
        }

        /// <summary>
        /// Initializes a new instance from a graphics surface and a clip rectangle, the shape
        /// System.Windows.Forms uses. Ported code builds one of these to raise its own Paint event --
        /// a control forwarding painting to a handler, or a renderer invoked outside a paint cycle.
        /// </summary>
        public PaintEventArgs (Graphics graphics, System.Drawing.Rectangle clipRect)
        {
            Guard.ThrowIfNull (graphics);

            _graphics = graphics;
            _callerGraphics = true;
            Canvas = graphics.Canvas!;
            Info = new SKImageInfo (Math.Max (clipRect.Width, 0), Math.Max (clipRect.Height, 0));
            Scaling = 1.0;
            device_matrix = Canvas.TotalMatrix;
        }

        private Graphics? _graphics;

        // A Graphics the caller handed in is theirs to keep; one built here is rebuilt per scope, so its
        // ResetTransform baseline is the scope's matrix (logical or device) rather than the first one seen.
        private readonly bool _callerGraphics;

        // The canvas matrix this paint started with: the surface's own device-pixel space.
        private readonly SKMatrix device_matrix;

        // Set by the base OnPaint (Control's or the window's). The Paint event is raised only when it
        // was reached, so an override that does not call base suppresses the handlers, as upstream,
        // where the base OnPaint is what invokes them (EVT-20).
        internal bool PaintEventRequested;

        // ── Logical and device painting (EVT-37, CTL-10) ───────────────────────────────────────────
        //
        // Application paint code -- an OnPaint override, a Paint handler, OnPaintBackground -- draws in
        // the same LOGICAL units as Width, Height, ClientRectangle and MouseEventArgs, so a ported
        // `e.Graphics.DrawRectangle (pen, 0, 0, Width - 1, Height - 1)` frames the control at any display
        // scale. The library's own renderers lay out in device pixels and say so with DeviceSpace ().

        /// <summary>Scales the canvas to logical units until the scope is disposed.</summary>
        internal CanvasScope LogicalSpace ()
        {
            var scope = new CanvasScope (this, Canvas.Save ());
            Canvas.SetMatrix (device_matrix);

            if (Scaling != 1.0)
                Canvas.Scale ((float) Scaling);

            DropOwnGraphics ();
            return scope;
        }

        /// <summary>
        /// Puts the canvas back in device pixels until the scope is disposed -- for the library's own
        /// drawing, which is laid out in device pixels, when it runs inside application paint code
        /// (a renderer invoked from base.OnPaint, say).
        /// </summary>
        internal CanvasScope DeviceSpace ()
        {
            var scope = new CanvasScope (this, Canvas.Save ());
            Canvas.SetMatrix (device_matrix);
            DropOwnGraphics ();
            return scope;
        }

        private void DropOwnGraphics ()
        {
            if (!_callerGraphics)
                _graphics = null;
        }

        internal readonly struct CanvasScope : IDisposable
        {
            private readonly PaintEventArgs args;
            private readonly int count;

            internal CanvasScope (PaintEventArgs args, int count)
            {
                this.args = args;
                this.count = count;
            }

            public void Dispose ()
            {
                args.Canvas.RestoreToCount (count);
                args.DropOwnGraphics ();
            }
        }

        /// <summary>
        /// Gets the canvas needed to paint the control.
        /// </summary>
        public SKCanvas Canvas { get; }

        /// <summary>
        /// WinForms compatibility: gets a <see cref="Graphics"/> object wrapping the Skia canvas.
        /// Allows WinForms-style drawing code to compile without changes.
        /// </summary>
        public Graphics Graphics => _graphics ??= new Graphics (Canvas);

        /// <summary>
        /// Gets information about the image canvas.
        /// </summary>
        public SKImageInfo Info { get; }

        /// <summary>
        /// WinForms compatibility: gets the rectangle in which to paint, in the canvas's current
        /// (control-local) coordinate space. Derived from the canvas clip bounds.
        /// </summary>
        public Rectangle ClipRectangle {
            get {
                // From the exact device clip mapped back through the current matrix: logical inside
                // application paint code, device inside the library's own (EVT-37). LocalClipBounds is
                // outset by a pixel for anti-aliasing, which made this one unit too big at every scale.
                var device = Canvas.DeviceClipBounds;

                if (!Canvas.TotalMatrix.TryInvert (out var inverse))
                    return new Rectangle (device.Left, device.Top, device.Width, device.Height);

                var local = inverse.MapRect (new SKRect (device.Left, device.Top, device.Right, device.Bottom));
                return Rectangle.Round (new System.Drawing.RectangleF (local.Left, local.Top, local.Width, local.Height));
            }
        }

        /// <summary>
        /// Gets the current scale factor of the form.
        /// </summary>
        public double Scaling { get; }

        /// <summary>
        /// Transforms a horizontal or vertical integer coordinate from logical to device units
        /// by scaling it up for current DPI and rounding to nearest integer value.
        /// </summary>
        /// <param name="value">Value in logical units</param>
        /// <returns>Value in device units</returns>
        public int LogicalToDeviceUnits (int value) => (int)Math.Round (Scaling * value);

        /// <summary>
        /// Transforms a Size from logical to device units
        /// by scaling it up for current DPI and rounding to nearest integer value.
        /// </summary>
        /// <param name="value">Value in logical units</param>
        /// <returns>Value in device units</returns>
        public Size LogicalToDeviceUnits (Size value) => new Size (LogicalToDeviceUnits (value.Width), LogicalToDeviceUnits (value.Height));

        /// <summary>
        /// Transforms a Padding from logical to device units
        /// by scaling it up for current DPI and rounding to nearest integer value.
        /// </summary>
        /// <param name="value">Value in logical units</param>
        /// <returns>Value in device units</returns>
        public Padding LogicalToDeviceUnits (Padding value)
        {
            return new Padding (LogicalToDeviceUnits (value.Left),
                                LogicalToDeviceUnits (value.Top),
                                LogicalToDeviceUnits (value.Right),
                                LogicalToDeviceUnits (value.Bottom));
        }
    }
}
