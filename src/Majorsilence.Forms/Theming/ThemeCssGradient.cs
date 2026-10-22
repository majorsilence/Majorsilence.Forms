using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>One colour stop of a <see cref="ThemeCssGradient"/>.</summary>
    public sealed class ThemeCssGradientStop
    {
        internal ThemeCssGradientStop (uint argb, double position, string unit)
        {
            Argb = argb;
            Position = position;
            Unit = unit;
        }

        /// <summary>The stop's colour as 0xAARRGGBB.</summary>
        public uint Argb { get; }

        /// <summary>Where the stop sits, in <see cref="Unit"/>s: a percentage of the gradient line (0 to 100) for <c>percent</c>, pixels for <c>length</c>, degrees for <c>angle</c>. Meaningless for <c>auto</c>.</summary>
        public double Position { get; }

        /// <summary><c>auto</c> (the stop was written without a position; CSS spaces those evenly), <c>percent</c>, <c>length</c> or <c>angle</c>.</summary>
        public string Unit { get; }
    }

    /// <summary>A position or size along one axis of a gradient: a fraction of the box, or a length in pixels.</summary>
    public readonly struct ThemeCssGradientOffset
    {
        internal ThemeCssGradientOffset (double value, bool isLength)
        {
            Value = value;
            IsLength = isLength;
        }

        /// <summary>The fraction (0 to 1) of the box, or the pixels when <see cref="IsLength"/>.</summary>
        public double Value { get; }

        /// <summary>Whether <see cref="Value"/> is pixels from the start edge rather than a fraction of the box.</summary>
        public bool IsLength { get; }
    }

    /// <summary>
    /// One layer of a <c>background-image</c> in host-neutral terms: what a toolkit needs to build its own gradient brush without a
    /// second CSS parser or Skia. Read it through <see cref="ThemeCssValue.Gradients"/>.
    /// </summary>
    public sealed class ThemeCssGradient
    {
        private readonly ControlGradient source;

        internal ThemeCssGradient (ControlGradient source)
        {
            this.source = source;
            Css = source.Css;
            Kind = source.Kind;
            Repeating = source.Repeating;
            Stops = source.Stops.Select (s => new ThemeCssGradientStop ((uint) s.Color, s.Position, s.Unit switch {
                ControlGradient.StopUnit.Percent => "percent",
                ControlGradient.StopUnit.Length => "length",
                ControlGradient.StopUnit.Angle => "angle",
                _ => "auto"
            })).ToArray ();

            AngleDegrees = source.Angle;
            if (source.Corner is { } c)
                Corner = string.Join (" ", new[] { c.Top ? "top" : c.Bottom ? "bottom" : null, c.Left ? "left" : c.Right ? "right" : null }.Where (s => s is not null));

            Circle = source.Circle;
            SizeKeyword = source.Size switch {
                ControlGradient.RadialSize.ClosestSide => "closest-side",
                ControlGradient.RadialSize.ClosestCorner => "closest-corner",
                ControlGradient.RadialSize.FarthestSide => "farthest-side",
                ControlGradient.RadialSize.Explicit => "explicit",
                _ => "farthest-corner"
            };
            SizeX = new ThemeCssGradientOffset (source.SizeX.Value, source.SizeX.IsLength);
            SizeY = new ThemeCssGradientOffset (source.SizeY.Value, source.SizeY.IsLength);
            CenterX = new ThemeCssGradientOffset (source.X.Value, source.X.IsLength);
            CenterY = new ThemeCssGradientOffset (source.Y.Value, source.Y.IsLength);
            FromDegrees = source.From;
        }

        /// <summary>The gradient as the stylesheet wrote it.</summary>
        public string Css { get; }

        /// <summary>Linear, radial or conic.</summary>
        public ControlGradientKind Kind { get; }

        /// <summary>Whether it is a <c>repeating-</c> gradient.</summary>
        public bool Repeating { get; }

        /// <summary>The colour stops in order.</summary>
        public IReadOnlyList<ThemeCssGradientStop> Stops { get; }

        /// <summary>Linear: the direction in CSS degrees (0 points up, 90 right). Ignored when <see cref="Corner"/> is set.</summary>
        public double AngleDegrees { get; }

        /// <summary>Linear <c>to top right</c> and friends: the side or corner aimed at (<c>top</c>, <c>bottom left</c>, ...), or null.</summary>
        public string? Corner { get; }

        /// <summary>Radial: whether the shape is a circle rather than an ellipse.</summary>
        public bool Circle { get; }

        /// <summary>Radial: <c>closest-side</c>, <c>closest-corner</c>, <c>farthest-side</c>, <c>farthest-corner</c> or <c>explicit</c> (then <see cref="SizeX"/> / <see cref="SizeY"/>).</summary>
        public string SizeKeyword { get; }

        /// <summary>Radial with an explicit size: the horizontal radius.</summary>
        public ThemeCssGradientOffset SizeX { get; }

        /// <summary>Radial with an explicit size: the vertical radius (equal to <see cref="SizeX"/> for a circle).</summary>
        public ThemeCssGradientOffset SizeY { get; }

        /// <summary>Radial and conic: the horizontal centre.</summary>
        public ThemeCssGradientOffset CenterX { get; }

        /// <summary>Radial and conic: the vertical centre.</summary>
        public ThemeCssGradientOffset CenterY { get; }

        /// <summary>Conic: where the first stop starts, in CSS degrees.</summary>
        public double FromDegrees { get; }

        /// <summary>
        /// Paints the gradient into a new transparent bitmap of the given size, exactly as a Majorsilence.Forms control paints it (the
        /// same geometry, stop fix-up and colour interpolation), for a host that cannot build the brush from the model. The caller owns
        /// the bitmap. Returns an empty bitmap for a size that is not positive.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        public SKBitmap Render (int width, int height)
        {
            if (width <= 0 || height <= 0)
                return new SKBitmap ();

            var bitmap = new SKBitmap (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            bitmap.Erase (SKColors.Transparent);

            using var canvas = new SKCanvas (bitmap);
            using var shader = source.CreateShader (new SKRect (0, 0, width, height));
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRect (0, 0, width, height, paint);
            return bitmap;
        }

        /// <inheritdoc/>
        public override string ToString () => Css;
    }

    public sealed partial class ThemeCssValue
    {
        /// <summary>
        /// For <see cref="ThemeCssValueKind.Image"/> and <see cref="ThemeCssValueKind.List"/> of images: the gradient layers, the first on
        /// top, as host-neutral models. Empty for any other value.
        /// </summary>
        public IReadOnlyList<ThemeCssGradient> Gradients
            => Kind == ThemeCssValueKind.Image ? (gradient is null ? System.Array.Empty<ThemeCssGradient> () : new[] { gradient })
                : Kind == ThemeCssValueKind.List ? Items.SelectMany (i => i.Gradients).ToArray ()
                : System.Array.Empty<ThemeCssGradient> ();

        private ThemeCssGradient? gradient;

        internal static ThemeCssValue ImageOf (ControlGradient layer)
            => new (ThemeCssValueKind.Image) { FontStyle = layer.Css, gradient = new ThemeCssGradient (layer) };
    }
}
