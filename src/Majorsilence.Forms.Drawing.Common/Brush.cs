using System;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Base class for brushes that fill the interior of shapes and text on a
    /// drawing surface. Cross-platform replacement for System.Drawing.Brush.
    /// </summary>
    public abstract class Brush : IDisposable
    {
        // Builds a fill SKPaint for this brush. Caller owns disposal.
        internal abstract SKPaint CreatePaint ();

        /// <summary>Creates an exact, independent copy of this brush.</summary>
        /// <remarks>
        /// Declared as returning <see cref="object"/> to match System.Drawing.Brush; every derived brush
        /// overrides it with its own type via a covariant return, so callers rarely need to cast.
        /// </remarks>
        public abstract object Clone ();

        /// <summary>Releases the resources used by this brush. No-op in Majorsilence.Forms.Drawing.</summary>
        public void Dispose () => GC.SuppressFinalize (this);
    }

    /// <summary>
    /// The geometric transform a gradient or texture brush applies to its own content.
    ///
    /// Shared by <see cref="Drawing2D.LinearGradientBrush"/>, <see cref="Drawing2D.PathGradientBrush"/> and
    /// <see cref="TextureBrush"/>, which each expose the same six-member GDI+ transform surface over it.
    /// It becomes the shader's *local* matrix, so it moves the gradient/texture within the filled shape
    /// rather than transforming the shape itself.
    /// </summary>
    internal sealed class BrushTransform
    {
        // Held as the underlying SKMatrix struct rather than a Drawing2D.Matrix: Matrix is IDisposable,
        // and owning one here would make every brush responsible for disposing it (CA1001) for no gain,
        // since the matrix wraps a struct and holds nothing unmanaged.
        private SKMatrix matrix = SKMatrix.Identity;

        /// <summary>A copy, so callers cannot mutate the brush by holding onto the returned matrix.</summary>
        public Drawing2D.Matrix Get () => new (matrix);

        /// <summary>Assigning null resets to the identity, matching <see cref="Reset"/>.</summary>
        public void Set (Drawing2D.Matrix? value) => matrix = value?.ToSKMatrix () ?? SKMatrix.Identity;

        public void Reset () => matrix = SKMatrix.Identity;

        public void Multiply (Drawing2D.Matrix m, Drawing2D.MatrixOrder order) => Mutate (x => x.Multiply (m, order));

        public void Translate (float dx, float dy, Drawing2D.MatrixOrder order) => Mutate (x => x.Translate (dx, dy, order));

        public void Scale (float sx, float sy, Drawing2D.MatrixOrder order) => Mutate (x => x.Scale (sx, sy, order));

        public void Rotate (float angle, Drawing2D.MatrixOrder order) => Mutate (x => x.Rotate (angle, order));

        public SKMatrix ToSKMatrix () => matrix;

        public BrushTransform Clone () => new () { matrix = matrix };

        // The compose operations (with their MatrixOrder semantics) live on Matrix, so borrow one for
        // the duration of the mutation and store the result back.
        private void Mutate (Action<Drawing2D.Matrix> operation)
        {
            var wrapper = new Drawing2D.Matrix (matrix);
            operation (wrapper);
            matrix = wrapper.ToSKMatrix ();
        }
    }

    /// <summary>Maps a GDI+ <see cref="Drawing2D.WrapMode"/> onto the Skia shader tiling it corresponds to.</summary>
    internal static class WrapModeExtensions
    {
        public static SKShaderTileMode ToSKTileMode (this Drawing2D.WrapMode mode) => mode switch {
            Drawing2D.WrapMode.Clamp => SKShaderTileMode.Clamp,
            Drawing2D.WrapMode.TileFlipX or Drawing2D.WrapMode.TileFlipY or Drawing2D.WrapMode.TileFlipXY
                => SKShaderTileMode.Mirror,
            _ => SKShaderTileMode.Repeat,
        };
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

        /// <inheritdoc/>
        public override SolidBrush Clone () => new (Color);

        internal override SKPaint CreatePaint () => new SKPaint {
            Color = new SKColor (Color.R, Color.G, Color.B, Color.A),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
    }
}

// GDI+ splits the brushes across two namespaces: Brush/SolidBrush/TextureBrush sit in System.Drawing,
// while the gradient and hatch brushes sit in System.Drawing.Drawing2D. The types below mirror that
// second group, so a `using System.Drawing.Drawing2D;` rewritten to Majorsilence.Forms.Drawing.Drawing2D
// resolves them exactly as it did before the migration.
namespace Majorsilence.Forms.Drawing.Drawing2D
{
    /// <summary>
    /// A brush that fills with a linear gradient between two colors.
    /// </summary>
    public sealed class LinearGradientBrush : Brush
    {
        private readonly RectangleF rect;
        // Not readonly: LinearColors is a settable property in System.Drawing.
        private Color color1;
        private Color color2;
        private readonly float angleDegrees;
        private readonly BrushTransform transform = new ();

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

        /// <summary>
        /// Initializes a new LinearGradientBrush using a named direction (WinForms compat overload
        /// for System.Drawing.Drawing2D.LinearGradientMode) instead of a raw angle.
        /// </summary>
        public LinearGradientBrush (RectangleF rect, Color color1, Color color2, Drawing2D.LinearGradientMode linearGradientMode)
            : this (rect, color1, color2, linearGradientMode switch {
                Drawing2D.LinearGradientMode.Horizontal => 0f,
                Drawing2D.LinearGradientMode.Vertical => 90f,
                Drawing2D.LinearGradientMode.ForwardDiagonal => 45f,
                Drawing2D.LinearGradientMode.BackwardDiagonal => 135f,
                _ => 0f
            })
        {
        }

        /// <summary>Initializes a new LinearGradientBrush using two PointF values (WinForms compat overload).</summary>
        public LinearGradientBrush (System.Drawing.PointF point1, System.Drawing.PointF point2, Color color1, Color color2)
            : this (new RectangleF (point1.X, point1.Y, point2.X - point1.X, point2.Y - point1.Y), color1, color2, 0f)
        {
        }

        /// <summary>Initializes a new LinearGradientBrush that fills with a single, uniform color.</summary>
        public LinearGradientBrush (Color color)
            : this (new RectangleF (0f, 0f, 1f, 1f), color, color, 0f)
        {
        }

        /// <summary>Initializes a new LinearGradientBrush over an integer rectangle at the given angle.</summary>
        public LinearGradientBrush (Rectangle rect, Color color1, Color color2, float angleDegrees = 0f)
            : this (new RectangleF (rect.X, rect.Y, rect.Width, rect.Height), color1, color2, angleDegrees)
        {
        }

        /// <summary>Initializes a new LinearGradientBrush over an integer rectangle using a named direction.</summary>
        public LinearGradientBrush (Rectangle rect, Color color1, Color color2, Drawing2D.LinearGradientMode linearGradientMode)
            : this (new RectangleF (rect.X, rect.Y, rect.Width, rect.Height), color1, color2, linearGradientMode)
        {
        }

        /// <summary>
        /// Gets or sets whether gamma correction is applied to this gradient. Accepted and round-tripped
        /// for System.Drawing.Drawing2D.LinearGradientBrush compatibility; Skia renders gradients in the
        /// destination color space, so the flag does not currently alter output.
        /// </summary>
        public bool GammaCorrection { get; set; }

        /// <summary>Gets the rectangle this gradient is defined over.</summary>
        public RectangleF Rectangle => rect;

        /// <summary>
        /// Gets or sets how this gradient tiles outside <see cref="Rectangle"/>. Applied for real: it
        /// selects the Skia shader tile mode.
        /// </summary>
        public Drawing2D.WrapMode WrapMode { get; set; } = Drawing2D.WrapMode.Clamp;

        /// <summary>
        /// Gets or sets the starting and ending colors as a two-element array, matching
        /// System.Drawing. Setting fewer than two colors is ignored, as GDI+ does.
        /// </summary>
        public Color[] LinearColors {
            get => [color1, color2];
            set {
                if (value is { Length: >= 2 }) {
                    color1 = value[0];
                    color2 = value[1];
                }
            }
        }

        /// <summary>
        /// Gets or sets a copy of the transform applied to this gradient. Assigning null resets it to
        /// the identity. See <see cref="BrushTransform"/> for how it is applied.
        /// </summary>
        public Drawing2D.Matrix Transform {
            get => transform.Get ();
            set => transform.Set (value);
        }

        /// <summary>Resets the gradient transform to the identity.</summary>
        public void ResetTransform () => transform.Reset ();

        /// <summary>Multiplies the gradient transform by <paramref name="matrix"/>.</summary>
        public void MultiplyTransform (Drawing2D.Matrix matrix, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Multiply (matrix, order);

        /// <summary>Translates the gradient transform by the specified offsets.</summary>
        public void TranslateTransform (float dx, float dy, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Translate (dx, dy, order);

        /// <summary>Scales the gradient transform by the specified factors.</summary>
        public void ScaleTransform (float sx, float sy, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Scale (sx, sy, order);

        /// <summary>Rotates the gradient transform by the specified angle, in degrees.</summary>
        public void RotateTransform (float angle, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Rotate (angle, order);

        /// <inheritdoc/>
        public override LinearGradientBrush Clone ()
        {
            var clone = new LinearGradientBrush (rect, color1, color2, angleDegrees) {
                GammaCorrection = GammaCorrection,
                WrapMode = WrapMode,
                blendColors = blendColors?.ToArray (),
                blendPositions = blendPositions?.ToArray (),
                blend = blend,
            };
            clone.transform.Set (transform.Get ());
            return clone;
        }

        private Color[]? blendColors;
        private float[]? blendPositions;

        /// <summary>
        /// Gets or sets the multi-stop color ramp of this gradient. When set (with at least two
        /// colors) it replaces the two endpoint colors. Setting it clears any <see cref="Blend"/>.
        /// </summary>
        public Drawing2D.ColorBlend? InterpolationColors {
            get => blendColors is null
                ? null
                : new Drawing2D.ColorBlend {
                    Colors = (Color[])blendColors.Clone (),
                    Positions = blendPositions is null ? EvenPositions (blendColors.Length) : (float[])blendPositions.Clone ()
                };
            set {
                blendColors = value?.Colors is { Length: > 0 } c ? (Color[])c.Clone () : null;
                blendPositions = value?.Positions is { Length: > 0 } p ? (float[])p.Clone () : null;
                blend = null;
            }
        }

        /// <summary>
        /// Gets or sets the positions (0..1) of the <see cref="InterpolationColors"/> stops. When null,
        /// the stops are spaced evenly. Majorsilence.Forms.Drawing convenience over
        /// <see cref="Drawing2D.ColorBlend.Positions"/> -- both read and write the same storage.
        /// </summary>
        public float[]? InterpolationPositions {
            get => blendPositions;
            set => blendPositions = value;
        }

        private Drawing2D.Blend? blend;

        /// <summary>
        /// Gets or sets the falloff (blend factors) between the two endpoint colors. Setting it
        /// replaces any <see cref="InterpolationColors"/> ramp; the factors are expanded into color
        /// stops so both features share one gradient path.
        /// </summary>
        public Drawing2D.Blend? Blend {
            get => blend;
            set {
                blend = value;
                var expanded = Drawing2D.GradientBlendShapes.Expand (value, color1, color2);
                blendColors = expanded?.Colors;
                blendPositions = expanded?.Positions;
            }
        }

        /// <summary>
        /// Applies a triangular blend shape: the gradient runs from the starting color at both ends to
        /// the ending color at <paramref name="focus"/>, with a linear ramp on each side.
        /// </summary>
        /// <param name="focus">Where (0..1) the ending color peaks.</param>
        /// <param name="scale">How much (0..1) of the ending color is reached at the focus.</param>
        public void SetBlendTriangularShape (float focus, float scale = 1f)
            => Blend = Drawing2D.GradientBlendShapes.Triangular (focus, scale);

        /// <summary>
        /// Applies a sigma-bell blend shape: as <see cref="SetBlendTriangularShape"/>, but with a
        /// Gaussian-shaped falloff on each side of the focus instead of a straight ramp.
        /// </summary>
        /// <param name="focus">Where (0..1) the ending color peaks.</param>
        /// <param name="scale">How much (0..1) of the ending color is reached at the focus.</param>
        public void SetSigmaBellShape (float focus, float scale = 1f)
            => Blend = Drawing2D.GradientBlendShapes.SigmaBell (focus, scale);

        private static float[] EvenPositions (int count)
        {
            if (count <= 1)
                return count == 1 ? new[] { 0f } : Array.Empty<float> ();
            var result = new float[count];
            for (var i = 0; i < count; i++)
                result[i] = i / (float)(count - 1);
            return result;
        }

        internal override SKPaint CreatePaint ()
        {
            var radians = angleDegrees * (float)Math.PI / 180f;
            var dx = (float)Math.Cos (radians);
            var dy = (float)Math.Sin (radians);

            var start = new SKPoint (rect.Left, rect.Top);
            var end = new SKPoint (rect.Left + rect.Width * dx, rect.Top + rect.Height * dy);

            // A multi-stop blend, when supplied, supersedes the two endpoint colors.
            SKColor[] colors;
            float[]? stops;
            if (blendColors is { Length: >= 2 } ramp)
            {
                colors = Array.ConvertAll (ramp, c => new SKColor (c.R, c.G, c.B, c.A));
                stops = blendPositions is { } pos && pos.Length == ramp.Length ? pos : null;
            }
            else
            {
                colors = [
                    new SKColor (color1.R, color1.G, color1.B, color1.A),
                    new SKColor (color2.R, color2.G, color2.B, color2.A)
                ];
                stops = [0f, 1f];
            }

            var shader = SKShader.CreateLinearGradient (
                start, end,
                colors,
                stops,
                WrapMode.ToSKTileMode (),
                transform.ToSKMatrix ());

            return new SKPaint {
                Shader = shader,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
        }
    }

    /// <summary>
    /// A brush that fills with a hatch pattern using an 8×8 tiled SKShader.
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

        /// <inheritdoc/>
        public override HatchBrush Clone () => new (HatchStyle, foreColor, BackgroundColor);

        internal override SKPaint CreatePaint ()
        {
            var skFore = new SKColor (foreColor.R, foreColor.G, foreColor.B, foreColor.A);
            var skBack = new SKColor (BackgroundColor.R, BackgroundColor.G, BackgroundColor.B, BackgroundColor.A);

            using var tile = new SKBitmap (8, 8, SKColorType.Bgra8888, SKAlphaType.Premul);

            var pat = GetPattern (HatchStyle);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    tile.SetPixel (x, y, (pat[y] & (0x80 >> x)) != 0 ? skFore : skBack);

            var shader = SKShader.CreateBitmap (tile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
            return new SKPaint { Shader = shader, Style = SKPaintStyle.Fill };
        }

        // Each entry is an 8-byte row pattern. Bit 7 (MSB) = pixel x=0, bit 0 (LSB) = pixel x=7.
        private static byte[] GetPattern (HatchStyle style) => style switch {
            HatchStyle.Horizontal            => [0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00],
            HatchStyle.Vertical              => [0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88],
            HatchStyle.ForwardDiagonal       => [0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01],
            HatchStyle.BackwardDiagonal      => [0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80],
            HatchStyle.Cross                 => [0xFF, 0x88, 0x88, 0x88, 0xFF, 0x88, 0x88, 0x88],
            HatchStyle.DiagonalCross         => [0x81, 0x42, 0x24, 0x18, 0x18, 0x24, 0x42, 0x81],
            HatchStyle.Percent05             => [0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            HatchStyle.Percent10             => [0x80, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00],
            HatchStyle.Percent20             => [0x88, 0x00, 0x00, 0x00, 0x88, 0x00, 0x00, 0x00],
            HatchStyle.Percent25             => [0x88, 0x00, 0x22, 0x00, 0x88, 0x00, 0x22, 0x00],
            HatchStyle.Percent30             => [0xAA, 0x00, 0x44, 0x00, 0xAA, 0x00, 0x44, 0x00],
            HatchStyle.Percent40             => [0xAA, 0x44, 0xAA, 0x00, 0xAA, 0x44, 0xAA, 0x00],
            HatchStyle.Percent50             => [0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55],
            HatchStyle.Percent60             => [0x55, 0xBB, 0x55, 0xFF, 0x55, 0xBB, 0x55, 0xFF],
            HatchStyle.Percent70             => [0x55, 0xFF, 0xBB, 0xFF, 0x55, 0xFF, 0xBB, 0xFF],
            HatchStyle.Percent75             => [0x77, 0xFF, 0xDD, 0xFF, 0x77, 0xFF, 0xDD, 0xFF],
            HatchStyle.Percent80             => [0x77, 0xFF, 0xFF, 0xFF, 0xF7, 0xFF, 0xFF, 0xFF],
            HatchStyle.Percent90             => [0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0xF7],
            HatchStyle.LightDownwardDiagonal => [0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01],
            HatchStyle.LightUpwardDiagonal   => [0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80],
            HatchStyle.DarkDownwardDiagonal  => [0xC0, 0x60, 0x30, 0x18, 0x0C, 0x06, 0x03, 0x81],
            HatchStyle.DarkUpwardDiagonal    => [0x03, 0x06, 0x0C, 0x18, 0x30, 0x60, 0xC0, 0x81],
            HatchStyle.WideDownwardDiagonal  => [0xE0, 0x70, 0x38, 0x1C, 0x0E, 0x07, 0x83, 0xC1],
            HatchStyle.WideUpwardDiagonal    => [0x07, 0x0E, 0x1C, 0x38, 0x70, 0xE0, 0xC1, 0x83],
            HatchStyle.LightVertical         => [0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22],
            HatchStyle.LightHorizontal       => [0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            HatchStyle.NarrowVertical        => [0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA],
            HatchStyle.NarrowHorizontal      => [0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0x00],
            HatchStyle.DarkVertical          => [0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC],
            HatchStyle.DarkHorizontal        => [0xFF, 0xFF, 0x00, 0xFF, 0xFF, 0xFF, 0x00, 0xFF],
            HatchStyle.DashedDownwardDiagonal=> [0x80, 0x00, 0x20, 0x00, 0x08, 0x00, 0x02, 0x00],
            HatchStyle.DashedUpwardDiagonal  => [0x01, 0x00, 0x04, 0x00, 0x10, 0x00, 0x40, 0x00],
            HatchStyle.DashedHorizontal      => [0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
            HatchStyle.DashedVertical        => [0x80, 0x80, 0x80, 0x80, 0x00, 0x00, 0x00, 0x00],
            HatchStyle.SmallConfetti         => [0x44, 0x88, 0x22, 0x11, 0x44, 0x88, 0x22, 0x11],
            HatchStyle.LargeConfetti         => [0x40, 0x80, 0x02, 0x10, 0x40, 0x80, 0x02, 0x10],
            HatchStyle.ZigZag                => [0x81, 0x42, 0x24, 0x18, 0x81, 0x42, 0x24, 0x18],
            HatchStyle.Wave                  => [0x1C, 0x22, 0xC1, 0x00, 0x1C, 0x22, 0xC1, 0x00],
            HatchStyle.DiagonalBrick         => [0x20, 0x10, 0x08, 0x04, 0xFE, 0x01, 0x80, 0x40],
            HatchStyle.HorizontalBrick       => [0xFF, 0x80, 0x80, 0x80, 0xFF, 0x08, 0x08, 0x08],
            HatchStyle.Weave                 => [0x55, 0xA0, 0x45, 0x08, 0x15, 0x0A, 0x51, 0x80],
            HatchStyle.Plaid                 => [0xFF, 0xAA, 0xFF, 0xAA, 0x0F, 0x0A, 0x0F, 0x0A],
            HatchStyle.Divot                 => [0x20, 0x20, 0xC0, 0x00, 0x02, 0x02, 0x03, 0x00],
            HatchStyle.DottedGrid            => [0x88, 0x00, 0x00, 0x00, 0x88, 0x00, 0x00, 0x00],
            HatchStyle.DottedDiamond         => [0x80, 0x40, 0x20, 0x40, 0x80, 0x40, 0x20, 0x40],
            HatchStyle.Shingle               => [0x10, 0x08, 0x04, 0x02, 0x01, 0x80, 0x60, 0x10],
            HatchStyle.Trellis               => [0xFF, 0xAA, 0xFF, 0xAA, 0xFF, 0xAA, 0xFF, 0xAA],
            HatchStyle.Sphere                => [0xFF, 0x81, 0xBD, 0xBD, 0xBD, 0xBD, 0x81, 0xFF],
            HatchStyle.SmallGrid             => [0xFF, 0x80, 0x80, 0x80, 0xFF, 0x80, 0x80, 0x80],
            HatchStyle.SmallCheckerBoard     => [0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55, 0xAA, 0x55],
            HatchStyle.LargeCheckerBoard     => [0xF0, 0xF0, 0xF0, 0xF0, 0x0F, 0x0F, 0x0F, 0x0F],
            HatchStyle.OutlinedDiamond       => [0x10, 0x28, 0x44, 0x82, 0x44, 0x28, 0x10, 0x00],
            HatchStyle.SolidDiamond          => [0x10, 0x38, 0x7C, 0xFE, 0x7C, 0x38, 0x10, 0x00],
            HatchStyle.Solid                 => [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF],
            _                                => [0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00],
        };
    }

    /// <summary>Specifies the hatch pattern used by a HatchBrush.</summary>
    public enum HatchStyle
    {
        /// <summary>Horizontal lines.</summary>
        Horizontal = 0,
        /// <summary>Vertical lines.</summary>
        Vertical = 1,
        /// <summary>Lines from upper-left to lower-right (\).</summary>
        ForwardDiagonal = 2,
        /// <summary>Lines from upper-right to lower-left (/).</summary>
        BackwardDiagonal = 3,
        /// <summary>Cross (horizontal and vertical lines).</summary>
        Cross = 4,
        /// <summary>Diagonal cross (both diagonals).</summary>
        DiagonalCross = 5,
        /// <summary>5% foreground density.</summary>
        Percent05 = 6,
        /// <summary>10% foreground density.</summary>
        Percent10 = 7,
        /// <summary>20% foreground density.</summary>
        Percent20 = 8,
        /// <summary>25% foreground density.</summary>
        Percent25 = 9,
        /// <summary>30% foreground density.</summary>
        Percent30 = 10,
        /// <summary>40% foreground density.</summary>
        Percent40 = 11,
        /// <summary>50% foreground density.</summary>
        Percent50 = 12,
        /// <summary>60% foreground density.</summary>
        Percent60 = 13,
        /// <summary>70% foreground density.</summary>
        Percent70 = 14,
        /// <summary>75% foreground density.</summary>
        Percent75 = 15,
        /// <summary>80% foreground density.</summary>
        Percent80 = 16,
        /// <summary>90% foreground density.</summary>
        Percent90 = 17,
        /// <summary>Light downward (forward) diagonal lines.</summary>
        LightDownwardDiagonal = 18,
        /// <summary>Light upward (backward) diagonal lines.</summary>
        LightUpwardDiagonal = 19,
        /// <summary>Dark downward diagonal lines (2-pixel wide).</summary>
        DarkDownwardDiagonal = 20,
        /// <summary>Dark upward diagonal lines (2-pixel wide).</summary>
        DarkUpwardDiagonal = 21,
        /// <summary>Wide downward diagonal lines (3-pixel wide).</summary>
        WideDownwardDiagonal = 22,
        /// <summary>Wide upward diagonal lines (3-pixel wide).</summary>
        WideUpwardDiagonal = 23,
        /// <summary>Light vertical lines.</summary>
        LightVertical = 24,
        /// <summary>Light horizontal lines.</summary>
        LightHorizontal = 25,
        /// <summary>Narrow vertical lines.</summary>
        NarrowVertical = 26,
        /// <summary>Narrow horizontal lines.</summary>
        NarrowHorizontal = 27,
        /// <summary>Dark vertical lines.</summary>
        DarkVertical = 28,
        /// <summary>Dark horizontal lines.</summary>
        DarkHorizontal = 29,
        /// <summary>Dashed downward diagonal lines.</summary>
        DashedDownwardDiagonal = 30,
        /// <summary>Dashed upward diagonal lines.</summary>
        DashedUpwardDiagonal = 31,
        /// <summary>Dashed horizontal lines.</summary>
        DashedHorizontal = 32,
        /// <summary>Dashed vertical lines.</summary>
        DashedVertical = 33,
        /// <summary>Small confetti pattern.</summary>
        SmallConfetti = 34,
        /// <summary>Large confetti pattern.</summary>
        LargeConfetti = 35,
        /// <summary>Zigzag pattern.</summary>
        ZigZag = 36,
        /// <summary>Wave pattern.</summary>
        Wave = 37,
        /// <summary>Diagonal brick pattern.</summary>
        DiagonalBrick = 38,
        /// <summary>Horizontal brick pattern.</summary>
        HorizontalBrick = 39,
        /// <summary>Weave pattern.</summary>
        Weave = 40,
        /// <summary>Plaid pattern.</summary>
        Plaid = 41,
        /// <summary>Divot pattern.</summary>
        Divot = 42,
        /// <summary>Dotted grid pattern.</summary>
        DottedGrid = 43,
        /// <summary>Dotted diamond pattern.</summary>
        DottedDiamond = 44,
        /// <summary>Shingle pattern.</summary>
        Shingle = 45,
        /// <summary>Trellis pattern.</summary>
        Trellis = 46,
        /// <summary>Sphere pattern.</summary>
        Sphere = 47,
        /// <summary>Small grid pattern.</summary>
        SmallGrid = 48,
        /// <summary>Small checkerboard pattern.</summary>
        SmallCheckerBoard = 49,
        /// <summary>Large checkerboard pattern.</summary>
        LargeCheckerBoard = 50,
        /// <summary>Outlined diamond pattern.</summary>
        OutlinedDiamond = 51,
        /// <summary>Solid diamond pattern.</summary>
        SolidDiamond = 52,
        /// <summary>Solid fill (100%).</summary>
        Solid = 100,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Large grid.</summary>
        LargeGrid = 4,
        /// <summary>Min.</summary>
        Min = 0,
        /// <summary>Max.</summary>
        Max = 4,
    }
}
