using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// A brush that fills with a gradient radiating from a center point out to a surrounding boundary.
    /// Cross-platform replacement for System.Drawing.Drawing2D.PathGradientBrush, approximated with a
    /// Skia radial gradient (center color → first surround color).
    /// </summary>
    public sealed class PathGradientBrush : Brush
    {
        private readonly RectangleF bounds;

        /// <summary>Initializes a new PathGradientBrush for the polygon defined by the points.</summary>
        public PathGradientBrush (PointF[] points)
        {
            bounds = BoundsOf (points);
            CenterPoint = new PointF (bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
        }

        /// <summary>Initializes a new PathGradientBrush for the polygon defined by the points.</summary>
        public PathGradientBrush (Point[] points)
            : this (points.Select (p => new PointF (p.X, p.Y)).ToArray ()) { }

        /// <summary>Initializes a new PathGradientBrush for the bounds of the specified path.</summary>
        public PathGradientBrush (GraphicsPath path)
        {
            bounds = path?.GetBounds () ?? default;
            CenterPoint = new PointF (bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
        }

        /// <summary>Gets or sets the color at the center of the gradient.</summary>
        public Color CenterColor { get; set; } = Color.Black;

        /// <summary>Gets or sets the colors at the outer boundary; the first is used for the radial edge.</summary>
        public Color[] SurroundColors { get; set; } = new[] { Color.White };

        /// <summary>Gets or sets the center point of the gradient.</summary>
        public PointF CenterPoint { get; set; }

        private static readonly float[] colorPos = new[] { 0f, 1f };

        internal override SKPaint CreatePaint ()
        {
            var edge = SurroundColors is { Length: > 0 } ? SurroundColors[0] : Color.White;
            var radius = Math.Max (1f, Math.Max (bounds.Width, bounds.Height) / 2f);

            var shader = SKShader.CreateRadialGradient (
                new SKPoint (CenterPoint.X, CenterPoint.Y),
                radius,
                new[] { ToSK (CenterColor), ToSK (edge) },
                colorPos,
                SKShaderTileMode.Clamp);

            return new SKPaint { Shader = shader, Style = SKPaintStyle.Fill, IsAntialias = true };
        }

        private static SKColor ToSK (Color c) => new (c.R, c.G, c.B, c.A);

        private static RectangleF BoundsOf (PointF[] points)
        {
            if (points is null || points.Length == 0)
                return default;
            float minX = points.Min (p => p.X), minY = points.Min (p => p.Y);
            float maxX = points.Max (p => p.X), maxY = points.Max (p => p.Y);
            return new RectangleF (minX, minY, maxX - minX, maxY - minY);
        }
    }
}
