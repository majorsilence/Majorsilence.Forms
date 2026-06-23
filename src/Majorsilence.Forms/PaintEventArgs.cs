using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    ///  Provides data for the Paint event.
    /// </summary>
    public class PaintEventArgs : EventArgs
    {
        /// <summary>
        ///  Initializes a new instance of the PaintEventArgs class.
        /// </summary>
        public PaintEventArgs (SKImageInfo info, SKCanvas canvas, double scaling)
        {
            Info = info;
            Canvas = canvas;
            Scaling = scaling;
        }

        private Graphics? _graphics;

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
                var bounds = Canvas.LocalClipBounds;
                return Rectangle.Round (new System.Drawing.RectangleF (bounds.Left, bounds.Top, bounds.Width, bounds.Height));
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
