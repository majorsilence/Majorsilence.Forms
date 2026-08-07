using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Drawing.Drawing2D;
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
        DashDotDot,
        /// <summary>A user-defined dash pattern, supplied via <see cref="Pen.DashPattern"/>.</summary>
        Custom
    }

    /// <summary>
    /// Defines the color, width, and dash style used to draw lines and outline shapes on a
    /// drawing surface. Cross-platform replacement for System.Drawing.Pen.
    /// </summary>
    public sealed class Pen : System.IDisposable
    {
        /// <summary>Initializes a new instance of the Pen class.</summary>
        public Pen (Color color, float width = 1f)
        {
            Color = color;
            Width = width;
        }

        /// <summary>
        /// Initializes a new Pen that strokes with the specified brush. A <see cref="SolidBrush"/>
        /// contributes its color directly; other brush types stroke with their own fill (gradient,
        /// hatch, texture) via <see cref="Brush"/>.
        /// </summary>
        public Pen (Brush brush, float width = 1f)
        {
            Brush = brush;
            Color = brush is SolidBrush sb ? sb.Color : Color.Black;
            Width = width;
        }

        /// <summary>
        /// Gets or sets the brush this pen strokes with. When set to anything other than a
        /// <see cref="SolidBrush"/>, the stroke is painted with that brush's shader, so a gradient or
        /// textured outline renders for real rather than collapsing to a flat color.
        /// </summary>
        public Brush? Brush { get; set; }

        /// <summary>Gets or sets the pen color.</summary>
        public Color Color { get; set; }

        /// <summary>Gets or sets the stroke width.</summary>
        public float Width { get; set; }

        /// <summary>Gets or sets the dash style.</summary>
        public DashStyle DashStyle { get; set; } = DashStyle.Solid;

        /// <summary>Gets or sets the join style for the ends of two consecutive line segments.</summary>
        public Drawing2D.LineJoin LineJoin { get; set; } = Drawing2D.LineJoin.Miter;

        /// <summary>Gets or sets the cap style used at the start of lines drawn with this pen.</summary>
        public Drawing2D.LineCap StartCap { get; set; } = Drawing2D.LineCap.Flat;

        /// <summary>Gets or sets the cap style used at the end of lines drawn with this pen.</summary>
        public Drawing2D.LineCap EndCap { get; set; } = Drawing2D.LineCap.Flat;

        /// <summary>Gets or sets the limit of the thickness of a join on a mitered corner.</summary>
        public float MiterLimit { get; set; } = 10f;

        /// <summary>
        /// Gets or sets the alignment of this pen relative to the line it strokes. Stored and
        /// round-tripped; SkiaSharp strokes are always centered on the path, so drawing does not (yet)
        /// change with this value -- an inset/outset stroke would require offsetting the geometry
        /// itself, which is not expressible through <c>SKPaint</c>.
        /// </summary>
        public Drawing2D.PenAlignment Alignment { get; set; } = Drawing2D.PenAlignment.Center;

        /// <summary>
        /// Gets or sets the custom cap used at the start of lines drawn with this pen. Its
        /// <see cref="Drawing2D.CustomLineCap.BaseCap"/> drives the Skia stroke cap -- see
        /// <see cref="Drawing2D.CustomLineCap"/> for why the custom outline itself cannot be stroked.
        /// </summary>
        public Drawing2D.CustomLineCap? CustomStartCap { get; set; }

        /// <summary>Gets or sets the custom cap used at the end of lines drawn with this pen.</summary>
        public Drawing2D.CustomLineCap? CustomEndCap { get; set; }

        /// <summary>Gets or sets the distance from the start of a line to the beginning of a dash pattern.</summary>
        public float DashOffset { get; set; }

        /// <summary>
        /// Gets or sets a user-defined dash pattern, in units of the pen width. Setting a non-null
        /// value switches <see cref="DashStyle"/> to <see cref="DashStyle.Custom"/>; setting null
        /// resets it to <see cref="DashStyle.Solid"/>, matching System.Drawing.Pen.
        /// </summary>
        public float[]? DashPattern
        {
            get => dashPattern;
            set
            {
                dashPattern = value;
                DashStyle = value is not null ? DashStyle.Custom : DashStyle.Solid;
            }
        }

        private float[]? dashPattern;

        /// <summary>Gets or sets the cap used at the ends of the dashes in a dashed line.</summary>
        /// <remarks>
        /// Stored and round-tripped. Skia applies one stroke cap to the whole path, including every
        /// dash segment, so a dash cap that differs from <see cref="StartCap"/>/<see cref="EndCap"/>
        /// cannot be expressed separately and the pen's stroke cap wins.
        /// </remarks>
        public Drawing2D.DashCap DashCap { get; set; } = Drawing2D.DashCap.Flat;

        /// <summary>Gets the kind of fill this pen strokes with, derived from <see cref="Brush"/>.</summary>
        public Drawing2D.PenType PenType => Brush switch {
            HatchBrush => Drawing2D.PenType.HatchFill,
            TextureBrush => Drawing2D.PenType.TextureFill,
            PathGradientBrush => Drawing2D.PenType.PathGradient,
            LinearGradientBrush => Drawing2D.PenType.LinearGradient,
            _ => Drawing2D.PenType.SolidColor,
        };

        /// <summary>
        /// Gets or sets the compound array, which splits the stroke into parallel lines and gaps
        /// expressed as fractions of the pen width.
        /// </summary>
        /// <remarks>
        /// Stored and round-tripped, but not applied: Skia strokes a single ribbon centered on the path
        /// and has no compound-stroke concept, so honoring this would mean generating the parallel
        /// outlines as geometry. Same category as <see cref="Alignment"/>.
        /// </remarks>
        public float[]? CompoundArray { get; set; }

        private readonly BrushTransform transform = new ();

        /// <summary>
        /// Gets or sets a copy of this pen's geometric transform. Assigning null resets it to the identity.
        /// </summary>
        /// <remarks>
        /// Stored and round-tripped. GDI+ applies this to the pen's own geometry (so a scaled pen
        /// strokes wider); <c>SKPaint</c> has no per-pen matrix, and applying it to the canvas would
        /// transform the shape being stroked rather than the stroke. When the pen strokes with a
        /// <see cref="Brush"/>, the transform *is* applied to that brush's shader.
        /// </remarks>
        public Drawing2D.Matrix Transform {
            get => transform.Get ();
            set => transform.Set (value);
        }

        /// <summary>Resets the pen transform to the identity.</summary>
        public void ResetTransform () => transform.Reset ();

        /// <summary>Multiplies the pen transform by <paramref name="matrix"/>.</summary>
        public void MultiplyTransform (Drawing2D.Matrix matrix, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Multiply (matrix, order);

        /// <summary>Translates the pen transform by the specified offsets.</summary>
        public void TranslateTransform (float dx, float dy, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Translate (dx, dy, order);

        /// <summary>Scales the pen transform by the specified factors.</summary>
        public void ScaleTransform (float sx, float sy, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Scale (sx, sy, order);

        /// <summary>Rotates the pen transform by the specified angle, in degrees.</summary>
        public void RotateTransform (float angle, Drawing2D.MatrixOrder order = Drawing2D.MatrixOrder.Prepend)
            => transform.Rotate (angle, order);

        /// <summary>Sets the start cap, end cap and dash cap in one call, matching System.Drawing.Pen.</summary>
        public void SetLineCap (Drawing2D.LineCap startCap, Drawing2D.LineCap endCap, Drawing2D.DashCap dashCap)
        {
            StartCap = startCap;
            EndCap = endCap;
            DashCap = dashCap;
        }

        /// <summary>Creates an independent copy of this pen.</summary>
        public Pen Clone () => new Pen (Color, Width) {
            DashStyle = DashStyle,
            LineJoin = LineJoin,
            StartCap = StartCap,
            EndCap = EndCap,
            MiterLimit = MiterLimit,
            DashOffset = DashOffset,
            Alignment = Alignment,
            CustomStartCap = CustomStartCap,
            CustomEndCap = CustomEndCap,
            dashPattern = dashPattern?.ToArray (),
            Brush = Brush,
            DashCap = DashCap,
            CompoundArray = CompoundArray?.ToArray (),
        };

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
                IsAntialias = true,
                StrokeMiter = MiterLimit,
                StrokeJoin = LineJoin switch {
                    Drawing2D.LineJoin.Round => SKStrokeJoin.Round,
                    Drawing2D.LineJoin.Bevel => SKStrokeJoin.Bevel,
                    _ => SKStrokeJoin.Miter
                },
                // A custom cap's declared base shape stands in for the custom outline, which Skia
                // cannot stroke; otherwise the plain StartCap applies.
                StrokeCap = (CustomStartCap?.BaseCap ?? StartCap) switch {
                    Drawing2D.LineCap.Square => SKStrokeCap.Square,
                    Drawing2D.LineCap.Round => SKStrokeCap.Round,
                    Drawing2D.LineCap.Triangle => SKStrokeCap.Square,
                    _ => SKStrokeCap.Butt
                }
            };

            var w = paint.StrokeWidth;
            var effect = DashStyle switch {
                DashStyle.Dash => SKPathEffect.CreateDash (new[] { 3 * w, 3 * w }, DashOffset),
                DashStyle.Dot => SKPathEffect.CreateDash (new[] { 1 * w, 2 * w }, DashOffset),
                DashStyle.DashDot => SKPathEffect.CreateDash (new[] { 3 * w, 2 * w, 1 * w, 2 * w }, DashOffset),
                DashStyle.DashDotDot => SKPathEffect.CreateDash (new[] { 3 * w, 2 * w, 1 * w, 2 * w, 1 * w, 2 * w }, DashOffset),
                // System.Drawing dash patterns are expressed in units of the pen width.
                DashStyle.Custom when dashPattern is { Length: >= 2 }
                    => SKPathEffect.CreateDash (dashPattern.Select (v => v * w).ToArray (), DashOffset),
                _ => null
            };

            if (effect is not null)
                paint.PathEffect = effect;

            // A non-solid brush strokes with its own shader, so gradient/hatch/texture outlines render
            // for real. SolidBrush contributes nothing here: its color is already on the paint.
            if (Brush is not null and not SolidBrush) {
                using var brushPaint = Brush.CreatePaint ();
                if (brushPaint.Shader is not null)
                    paint.Shader = brushPaint.Shader;
            }

            return paint;
        }
    }
}
