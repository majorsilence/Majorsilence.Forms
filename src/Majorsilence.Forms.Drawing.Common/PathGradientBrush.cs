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
        // Not readonly: Clone copies it across without re-deriving it from a path.
        private RectangleF bounds;

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

        /// <summary>Gets the bounding rectangle of the path this gradient was built from.</summary>
        public RectangleF Rectangle => bounds;

        /// <summary>
        /// Gets or sets how this gradient tiles outside its bounds. Applied for real: it selects the
        /// Skia shader tile mode.
        /// </summary>
        public WrapMode WrapMode { get; set; } = WrapMode.Clamp;

        /// <summary>
        /// Gets or sets the focus point where the center color reaches its fullest intensity, as a
        /// fraction (0..1) of the distance from the center to the boundary.
        /// </summary>
        /// <remarks>
        /// Stored and round-tripped, but not applied. GDI+ uses it to inset a second, scaled boundary
        /// inside which the gradient is a flat center color; a Skia radial gradient has a single center
        /// and radius and cannot express that inner focus region, so shaping it would mean synthesizing
        /// the ramp by hand. <see cref="InterpolationColors"/> is the portable way to get the same
        /// effect -- place the center color at a non-zero position.
        /// </remarks>
        public PointF FocusScales { get; set; }

        private readonly BrushTransform transform = new ();

        /// <summary>
        /// Gets or sets a copy of the transform applied to this gradient. Assigning null resets it to
        /// the identity.
        /// </summary>
        public Matrix Transform {
            get => transform.Get ();
            set => transform.Set (value);
        }

        /// <summary>Resets the gradient transform to the identity.</summary>
        public void ResetTransform () => transform.Reset ();

        /// <summary>Multiplies the gradient transform by <paramref name="matrix"/>.</summary>
        public void MultiplyTransform (Matrix matrix, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Multiply (matrix, order);

        /// <summary>Translates the gradient transform by the specified offsets.</summary>
        public void TranslateTransform (float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Translate (dx, dy, order);

        /// <summary>Scales the gradient transform by the specified factors.</summary>
        public void ScaleTransform (float sx, float sy, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Scale (sx, sy, order);

        /// <summary>Rotates the gradient transform by the specified angle, in degrees.</summary>
        public void RotateTransform (float angle, MatrixOrder order = MatrixOrder.Prepend)
            => transform.Rotate (angle, order);

        /// <inheritdoc/>
        public override PathGradientBrush Clone ()
        {
            var clone = new PathGradientBrush (new GraphicsPath ()) {
                CenterColor = CenterColor,
                SurroundColors = (Color[])SurroundColors.Clone (),
                CenterPoint = CenterPoint,
                WrapMode = WrapMode,
                FocusScales = FocusScales,
                bounds = bounds,
                blendColors = blendColors?.ToArray (),
                blendPositions = blendPositions?.ToArray (),
                blend = blend,
            };
            clone.transform.Set (transform.Get ());
            return clone;
        }

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
                WrapMode.ToSKTileMode (),
                transform.ToSKMatrix ());

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
