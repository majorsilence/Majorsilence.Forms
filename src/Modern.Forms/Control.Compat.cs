using System;
using System.Drawing;
using Modern.WindowKit.Threading;
using SkiaSharp;

namespace Modern.Forms
{
    // WinForms-compatibility surface for Control: BackgroundImage, Invoke/BeginInvoke.
    // (BackColor / ForeColor live in Control.cs alongside the other style wrappers.)
    public partial class Control
    {
        private SKBitmap? background_image;
        private ImageLayout background_image_layout = ImageLayout.Tile;
        private ControlStyles control_styles = ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer;

        /// <summary>
        /// Gets the combination of modifier keys (Ctrl/Shift/Alt) currently held down, as captured
        /// from the most recent input event. WinForms compatibility for code that reads the static
        /// Control.ModifierKeys (e.g. detecting Ctrl during a mouse-wheel zoom).
        /// </summary>
        public static Keys ModifierKeys { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating the control is double-buffered. Modern.Forms always renders
        /// each control into its own off-screen surface, so this is effectively always true; the setter
        /// is accepted for WinForms source compatibility.
        /// </summary>
        public bool DoubleBuffered { get; set; } = true;

        /// <summary>
        /// Forces the control to invalidate and immediately repaint.
        /// </summary>
        public void Refresh () => Invalidate ();

        /// <summary>
        /// Sets input focus to the control. Returns true if focus was successfully set.
        /// </summary>
        public bool Focus ()
        {
            Select ();
            return Focused;
        }

        /// <summary>
        /// Computes the location of the specified screen point into client coordinates.
        /// </summary>
        public Point PointToClient (Point point)
        {
            var origin = PointToScreen (Point.Empty);
            return new Point (point.X - origin.X, point.Y - origin.Y);
        }

        /// <summary>
        /// Sets the specified <see cref="ControlStyles"/> flag. Provided for WinForms source
        /// compatibility; the flags are stored but rendering remains Skia-based double-buffered.
        /// </summary>
        public void SetStyle (ControlStyles flag, bool value)
        {
            if (value)
                control_styles |= flag;
            else
                control_styles &= ~flag;
        }

        /// <summary>
        /// Returns whether the specified <see cref="ControlStyles"/> flag is set.
        /// </summary>
        public bool GetStyle (ControlStyles flag) => (control_styles & flag) == flag;

        /// <summary>
        /// Gets or sets the background image displayed in the control.
        /// </summary>
        public SKBitmap? BackgroundImage {
            get => background_image;
            set {
                if (background_image != value) {
                    background_image = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the layout used to position the <see cref="BackgroundImage"/>.
        /// </summary>
        public ImageLayout BackgroundImageLayout {
            get => background_image_layout;
            set {
                if (background_image_layout != value) {
                    background_image_layout = value;
                    Invalidate ();
                }
            }
        }

        // Draws the background image honoring BackgroundImageLayout. Called from OnPaintBackground.
        private void PaintBackgroundImage (PaintEventArgs e)
        {
            var image = background_image;

            if (image is null)
                return;

            // The control paints into its own buffer in control-local coordinates (origin 0,0),
            // so positioning is relative to (0,0) with the scaled width/height.
            var width = ScaledBounds.Width;
            var height = ScaledBounds.Height;

            switch (background_image_layout) {
                case ImageLayout.None: {
                        e.Canvas.DrawBitmap (image, new SKRect (0, 0, image.Width, image.Height));
                        break;
                    }
                case ImageLayout.Center: {
                        var x = (width - image.Width) / 2f;
                        var y = (height - image.Height) / 2f;
                        e.Canvas.DrawBitmap (image, new SKRect (x, y, x + image.Width, y + image.Height));
                        break;
                    }
                case ImageLayout.Stretch: {
                        e.Canvas.DrawBitmap (image, new SKRect (0, 0, width, height));
                        break;
                    }
                case ImageLayout.Zoom: {
                        var scale = Math.Min ((float)width / image.Width, (float)height / image.Height);
                        var w = image.Width * scale;
                        var h = image.Height * scale;
                        var x = (width - w) / 2f;
                        var y = (height - h) / 2f;
                        e.Canvas.DrawBitmap (image, new SKRect (x, y, x + w, y + h));
                        break;
                    }
                case ImageLayout.Tile:
                default: {
                        for (var y = 0; y < height; y += image.Height)
                            for (var x = 0; x < width; x += image.Width)
                                e.Canvas.DrawBitmap (image, new SKRect (x, y, x + image.Width, y + image.Height));
                        break;
                    }
            }
        }

        /// <summary>
        /// Executes the specified delegate asynchronously on the thread that owns the control.
        /// </summary>
        public void BeginInvoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            Dispatcher.UIThread.Post (action);
        }

        /// <summary>
        /// Executes the specified delegate synchronously on the thread that owns the control.
        /// </summary>
        public void Invoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);

            if (Dispatcher.UIThread.CheckAccess ()) {
                action ();
                return;
            }

            Dispatcher.UIThread.InvokeAsync (action).GetAwaiter ().GetResult ();
        }
    }

    /// <summary>
    /// Conversion helpers between <see cref="System.Drawing.Color"/> and <see cref="SKColor"/>.
    /// </summary>
    public static class ColorCompatExtensions
    {
        /// <summary>Converts a <see cref="System.Drawing.Color"/> to an <see cref="SKColor"/>.</summary>
        public static SKColor ToSKColor (this Color color) => new SKColor (color.R, color.G, color.B, color.A);

        /// <summary>Converts an <see cref="SKColor"/> to a <see cref="System.Drawing.Color"/>.</summary>
        public static Color ToDrawingColor (this SKColor color) => Color.FromArgb (color.Alpha, color.Red, color.Green, color.Blue);
    }
}
