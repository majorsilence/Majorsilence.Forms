using System;
using System.Drawing;
using SkiaSharp;

namespace Continuum.Drawing
{
    /// <summary>
    /// Base class for brushes that fill the interior of shapes and text on a
    /// <see cref="SkiaGraphics"/> surface. Cross-platform replacement for System.Drawing.Brush.
    /// </summary>
    public abstract class Brush : IDisposable
    {
        // Builds a fill SKPaint for this brush. Caller owns disposal.
        internal abstract SKPaint CreatePaint ();

        /// <summary>Releases the resources used by this brush. No-op in Continuum.Drawing.</summary>
        public void Dispose () => GC.SuppressFinalize (this);
    }

    /// <summary>
    /// A brush that fills with a single solid color.
    /// </summary>
    public sealed class SolidBrush : Brush
    {
        /// <summary>Initializes a new instance of the SolidBrush class.</summary>
        public SolidBrush (Color color)
        {
            Color = color;
        }

        /// <summary>Gets or sets the fill color.</summary>
        public Color Color { get; set; }

        internal override SKPaint CreatePaint () => new SKPaint {
            Color = new SKColor (Color.R, Color.G, Color.B, Color.A),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
    }

    /// <summary>
    /// A brush that fills with a linear gradient between two colors.
    /// </summary>
    public sealed class LinearGradientBrush : Brush
    {
        private readonly RectangleF rect;
        private readonly Color color1;
        private readonly Color color2;
        private readonly float angleDegrees;

        /// <summary>Initializes a new instance of the LinearGradientBrush class.</summary>
        public LinearGradientBrush (RectangleF rect, Color color1, Color color2, float angleDegrees = 0f)
        {
            this.rect = rect;
            this.color1 = color1;
            this.color2 = color2;
            this.angleDegrees = angleDegrees;
        }

        /// <summary>Initializes a new LinearGradientBrush using two points (WinForms compat overload).</summary>
        public LinearGradientBrush (System.Drawing.Point point1, System.Drawing.Point point2, Color color1, Color color2)
            : this (new RectangleF (point1.X, point1.Y, point2.X - point1.X, point2.Y - point1.Y), color1, color2, 0f)
        {
        }

        /// <summary>Initializes a new LinearGradientBrush using two PointF values (WinForms compat overload).</summary>
        public LinearGradientBrush (System.Drawing.PointF point1, System.Drawing.PointF point2, Color color1, Color color2)
            : this (new RectangleF (point1.X, point1.Y, point2.X - point1.X, point2.Y - point1.Y), color1, color2, 0f)
        {
        }

        internal override SKPaint CreatePaint ()
        {
            var radians = angleDegrees * (float)Math.PI / 180f;
            var dx = (float)Math.Cos (radians);
            var dy = (float)Math.Sin (radians);

            var start = new SKPoint (rect.Left, rect.Top);
            var end = new SKPoint (rect.Left + rect.Width * dx, rect.Top + rect.Height * dy);

            var colors = new SKColor[] {
                new SKColor (color1.R, color1.G, color1.B, color1.A),
                new SKColor (color2.R, color2.G, color2.B, color2.A)
            };
            var stops = new float[] { 0f, 1f };

            var shader = SKShader.CreateLinearGradient (
                start, end,
                colors,
                stops,
                SKShaderTileMode.Clamp);

            return new SKPaint {
                Shader = shader,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
        }
    }

    /// <summary>
    /// A brush that fills with a hatch pattern. Stub in Continuum.Forms — fills with the foreground color.
    /// </summary>
    public sealed class HatchBrush : Brush
    {
        private readonly Color foreColor;

        /// <summary>Initializes a new instance of HatchBrush.</summary>
        public HatchBrush (HatchStyle hatchStyle, Color foreColor) : this (hatchStyle, foreColor, Color.Transparent) { }

        /// <summary>Initializes a new instance of HatchBrush with a background color.</summary>
        public HatchBrush (HatchStyle hatchStyle, Color foreColor, Color backColor)
        {
            HatchStyle = hatchStyle;
            this.foreColor = foreColor;
            BackgroundColor = backColor;
        }

        /// <summary>Gets the hatch style of this brush.</summary>
        public HatchStyle HatchStyle { get; }

        /// <summary>Gets the background color of this brush.</summary>
        public Color BackgroundColor { get; }

        /// <summary>Gets the foreground color of this brush.</summary>
        public Color ForegroundColor => foreColor;

        internal override SKPaint CreatePaint () =>
            new SKPaint { Color = new SKColor (foreColor.R, foreColor.G, foreColor.B, foreColor.A), Style = SKPaintStyle.Fill };
    }

    /// <summary>Specifies the hatch pattern used by a HatchBrush.</summary>
    public enum HatchStyle
    {
        /// <summary>Horizontal lines.</summary>
        Horizontal = 0,
        /// <summary>Vertical lines.</summary>
        Vertical = 1,
        /// <summary>Forward diagonal lines.</summary>
        ForwardDiagonal = 2,
        /// <summary>Backward diagonal lines.</summary>
        BackwardDiagonal = 3,
        /// <summary>Cross (horizontal and vertical lines).</summary>
        Cross = 4,
        /// <summary>Diagonal cross.</summary>
        DiagonalCross = 5,
        /// <summary>Percent 05 pattern.</summary>
        Percent05 = 6,
        /// <summary>Percent 10 pattern.</summary>
        Percent10 = 7,
        /// <summary>Percent 20 pattern.</summary>
        Percent20 = 8,
        /// <summary>Percent 25 pattern.</summary>
        Percent25 = 9,
        /// <summary>Percent 30 pattern.</summary>
        Percent30 = 10,
        /// <summary>Solid fill (100%).</summary>
        Solid = 100
    }
}
