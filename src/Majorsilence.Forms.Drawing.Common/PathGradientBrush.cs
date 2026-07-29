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

        private Color[]? blendColors;
        private float[]? blendPositions;
        private Blend? blend;

        /// <summary>
        /// Gets or sets the multi-stop color ramp used from the center outwards. When set (with at
        /// least two colors) it replaces the center/surround pair.
        /// </summary>
        public ColorBlend? InterpolationColors {
            get => blendColors is null
                ? null
                : new ColorBlend {
                    Colors = (Color[])blendColors.Clone (),
                    Positions = blendPositions is null ? Array.Empty<float> () : (float[])blendPositions.Clone ()
                };
            set {
                blendColors = value?.Colors is { Length: > 0 } c ? (Color[])c.Clone () : null;
                blendPositions = value?.Positions is { Length: > 0 } p ? (float[])p.Clone () : null;
                blend = null;
            }
        }

        /// <summary>
        /// Gets or sets the falloff (blend factors) from the center color to the surround color.
        /// Setting it replaces any <see cref="InterpolationColors"/> ramp.
        /// </summary>
        public Blend? Blend {
            get => blend;
            set {
                blend = value;
                var edge = SurroundColors is { Length: > 0 } ? SurroundColors[0] : Color.White;
                var expanded = GradientBlendShapes.Expand (value, CenterColor, edge);
                blendColors = expanded?.Colors;
                blendPositions = expanded?.Positions;
            }
        }

        /// <summary>Applies a triangular blend shape between the center and surround colors.</summary>
        /// <param name="focus">Where (0..1) the surround color peaks.</param>
        /// <param name="scale">How much (0..1) of the surround color is reached at the focus.</param>
        public void SetBlendTriangularShape (float focus, float scale = 1f)
            => Blend = GradientBlendShapes.Triangular (focus, scale);

        /// <summary>Applies a sigma-bell blend shape between the center and surround colors.</summary>
        /// <param name="focus">Where (0..1) the surround color peaks.</param>
        /// <param name="scale">How much (0..1) of the surround color is reached at the focus.</param>
        public void SetSigmaBellShape (float focus, float scale = 1f)
            => Blend = GradientBlendShapes.SigmaBell (focus, scale);

        internal override SKPaint CreatePaint ()
        {
            var edge = SurroundColors is { Length: > 0 } ? SurroundColors[0] : Color.White;
            var radius = Math.Max (1f, Math.Max (bounds.Width, bounds.Height) / 2f);

            SKColor[] colors;
            float[]? stops;
            if (blendColors is { Length: >= 2 } ramp) {
                colors = Array.ConvertAll (ramp, ToSK);
                stops = blendPositions is { } pos && pos.Length == ramp.Length ? pos : null;
            } else {
                colors = new[] { ToSK (CenterColor), ToSK (edge) };
                stops = colorPos;
            }

            var shader = SKShader.CreateRadialGradient (
                new SKPoint (CenterPoint.X, CenterPoint.Y),
                radius,
                colors,
                stops,
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
