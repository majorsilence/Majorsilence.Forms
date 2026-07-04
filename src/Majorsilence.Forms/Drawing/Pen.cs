using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Specifies the dash style used when stroking lines and shapes.
    /// </summary>
    public enum DashStyle
    {
        /// <summary>A solid line.</summary>
        Solid,
        /// <summary>A line of dashes.</summary>
        Dash,
        /// <summary>A line of dots.</summary>
        Dot,
        /// <summary>A line of alternating dashes and dots.</summary>
        DashDot,
        /// <summary>A line of alternating dashes and double dots.</summary>
        DashDotDot
    }

    /// <summary>
    /// Defines the color, width, and dash style used to draw lines and outline shapes on a
    /// <see cref="SkiaGraphics"/> surface. Cross-platform replacement for System.Drawing.Pen.
    /// </summary>
    public sealed class Pen : System.IDisposable
    {
        /// <summary>Initializes a new instance of the Pen class.</summary>
        public Pen (Color color, float width = 1f)
        {
            Color = color;
            Width = width;
        }

        /// <summary>Initializes a new Pen from a Brush (uses the brush's first color). Stub in Majorsilence.Forms.</summary>
        public Pen (Brush brush, float width = 1f)
        {
            Color = brush is SolidBrush sb ? sb.Color : Color.Black;
            Width = width;
        }

        /// <summary>Gets or sets the pen color.</summary>
        public Color Color { get; set; }

        /// <summary>Gets or sets the stroke width.</summary>
        public float Width { get; set; }

        /// <summary>Gets or sets the dash style.</summary>
        public DashStyle DashStyle { get; set; } = DashStyle.Solid;

        /// <summary>
        /// Releases resources used by the Pen. Pen itself holds nothing unmanaged -- CreatePaint
        /// hands the caller a fresh, caller-owned SKPaint each time -- so this is a no-op provided
        /// purely for API-shape compatibility with System.Drawing.Pen (so `using (var p = new
        /// Pen(...))`, common in ported WinForms drawing code, compiles unmodified).
        /// </summary>
        public void Dispose ()
        {
        }

        // Builds a stroke SKPaint for this pen. Caller owns disposal.
        internal SKPaint CreatePaint ()
        {
            var paint = new SKPaint {
                Color = new SKColor (Color.R, Color.G, Color.B, Color.A),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Width <= 0 ? 1 : Width,
                IsAntialias = true
            };

            var w = paint.StrokeWidth;
            var effect = DashStyle switch {
                DashStyle.Dash => SKPathEffect.CreateDash (new[] { 3 * w, 3 * w }, 0),
                DashStyle.Dot => SKPathEffect.CreateDash (new[] { 1 * w, 2 * w }, 0),
                DashStyle.DashDot => SKPathEffect.CreateDash (new[] { 3 * w, 2 * w, 1 * w, 2 * w }, 0),
                DashStyle.DashDotDot => SKPathEffect.CreateDash (new[] { 3 * w, 2 * w, 1 * w, 2 * w, 1 * w, 2 * w }, 0),
                _ => null
            };

            if (effect is not null)
                paint.PathEffect = effect;

            return paint;
        }
    }
}
