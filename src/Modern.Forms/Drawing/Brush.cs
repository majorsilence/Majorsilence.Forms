using System;
using System.Drawing;
using SkiaSharp;

namespace Modern.Forms.Drawing
{
    /// <summary>
    /// Base class for brushes that fill the interior of shapes and text on a
    /// <see cref="SkiaGraphics"/> surface. Cross-platform replacement for System.Drawing.Brush.
    /// </summary>
    public abstract class Brush
    {
        // Builds a fill SKPaint for this brush. Caller owns disposal.
        internal abstract SKPaint CreatePaint ();
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

        internal override SKPaint CreatePaint ()
        {
            var radians = angleDegrees * (float)Math.PI / 180f;
            var dx = (float)Math.Cos (radians);
            var dy = (float)Math.Sin (radians);

            var start = new SKPoint (rect.Left, rect.Top);
            var end = new SKPoint (rect.Left + rect.Width * dx, rect.Top + rect.Height * dy);

            var shader = SKShader.CreateLinearGradient (
                start, end,
                new[] {
                    new SKColor (color1.R, color1.G, color1.B, color1.A),
                    new SKColor (color2.R, color2.G, color2.B, color2.A)
                },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp);

            return new SKPaint {
                Shader = shader,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
        }
    }
}
