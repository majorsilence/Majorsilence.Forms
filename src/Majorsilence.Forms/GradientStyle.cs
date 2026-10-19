using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>The shape of a <see cref="ControlGradient"/>.</summary>
    public enum ControlGradientKind
    {
        /// <summary>Colours change along a line (CSS <c>linear-gradient()</c>).</summary>
        Linear,

        /// <summary>Colours change outwards from a centre point (CSS <c>radial-gradient()</c>).</summary>
        Radial,

        /// <summary>Colours change around a centre point (CSS <c>conic-gradient()</c>).</summary>
        Conic
    }

    /// <summary>
    /// A CSS gradient -- one layer of a style's <see cref="ControlStyle.BackgroundGradients"/>, painted over the background colour and
    /// inside the control's rounded face. Made by a theme's <c>background-image</c>; <see cref="Css"/> is how the stylesheet wrote it.
    /// </summary>
    public sealed class ControlGradient
    {
        internal enum StopUnit
        {
            Auto,
            Percent,
            Length,
            Angle
        }

        internal readonly struct Stop
        {
            public Stop (SKColor color, double position, StopUnit unit)
            {
                Color = color;
                Position = position;
                Unit = unit;
            }

            public SKColor Color { get; }
            public double Position { get; }
            public StopUnit Unit { get; }
        }

        internal enum RadialSize
        {
            ClosestSide,
            ClosestCorner,
            FarthestSide,
            FarthestCorner,
            Explicit
        }

        // A position (CSS <position>) on one axis: a fraction of the box, or a length from its start edge.
        internal readonly struct Offset
        {
            public Offset (double value, bool isLength)
            {
                Value = value;
                IsLength = isLength;
            }

            public double Value { get; }
            public bool IsLength { get; }

            public static readonly Offset Center = new (0.5, false);

            public float Resolve (float start, float extent) => (float) (start + (IsLength ? Value : Value * extent));
        }

        internal ControlGradient (string css, ControlGradientKind kind, bool repeating, IReadOnlyList<Stop> stops)
        {
            Css = css;
            Kind = kind;
            Repeating = repeating;
            Stops = stops;
        }

        /// <summary>The gradient as the stylesheet wrote it.</summary>
        public string Css { get; }

        /// <summary>Linear, radial or conic.</summary>
        public ControlGradientKind Kind { get; }

        /// <summary>Whether it is a <c>repeating-</c> gradient, which repeats its stops across the box.</summary>
        public bool Repeating { get; }

        internal IReadOnlyList<Stop> Stops { get; }

        // Linear: the angle in CSS degrees (0 points up, 90 right), or a side or corner to aim at.
        internal double Angle { get; init; } = 180;
        internal (bool Top, bool Bottom, bool Left, bool Right)? Corner { get; init; }

        // Radial
        internal bool Circle { get; init; }
        internal RadialSize Size { get; init; } = RadialSize.FarthestCorner;
        internal Offset SizeX { get; init; }
        internal Offset SizeY { get; init; }

        // Radial and conic
        internal Offset X { get; init; } = Offset.Center;
        internal Offset Y { get; init; } = Offset.Center;

        // Conic: where the first stop starts, in CSS degrees.
        internal double From { get; init; }

        /// <inheritdoc/>
        public override string ToString () => Css;

        // ---- geometry ---------------------------------------------------------------------------------

        /// <summary>Makes the Skia shader that paints this gradient over <paramref name="box"/>, or null for an empty box.</summary>
        internal SKShader? CreateShader (SKRect box)
        {
            if (box.Width <= 0 || box.Height <= 0)
                return null;

            return Kind switch {
                ControlGradientKind.Linear => LinearShader (box),
                ControlGradientKind.Radial => RadialShader (box),
                _ => ConicShader (box)
            };
        }

        private SKShader LinearShader (SKRect box)
        {
            double w = box.Width, h = box.Height;
            var angle = Angle;

            // "to top right" aims the gradient line so its middle runs corner to corner (CSS Images 3, linear-gradient()).
            if (Corner is { } c) {
                var a = Math.Atan2 (h, w) * 180 / Math.PI;
                angle = (c.Top, c.Right, c.Bottom, c.Left) switch {
                    (true, true, _, _) => a,
                    (_, true, true, _) => 180 - a,
                    (_, _, true, true) => 180 + a,
                    _ => 360 - a
                };
            }

            var radians = angle * Math.PI / 180;
            var dx = Math.Sin (radians);
            var dy = -Math.Cos (radians);
            var length = Math.Abs (w * Math.Sin (radians)) + Math.Abs (h * Math.Cos (radians));

            var cx = box.MidX;
            var cy = box.MidY;
            var start = new SKPoint ((float) (cx - dx * length / 2), (float) (cy - dy * length / 2));

            var (colors, positions, first, last) = Resolve (length);
            var from = new SKPoint ((float) (start.X + dx * first * length), (float) (start.Y + dy * first * length));
            var to = new SKPoint ((float) (start.X + dx * last * length), (float) (start.Y + dy * last * length));

            if (from == to)
                return SKShader.CreateColor (colors[colors.Length - 1]);

            return SKShader.CreateLinearGradient (from, to, colors, positions, Repeating ? SKShaderTileMode.Repeat : SKShaderTileMode.Clamp);
        }

        private SKShader RadialShader (SKRect box)
        {
            var cx = X.Resolve (box.Left, box.Width);
            var cy = Y.Resolve (box.Top, box.Height);
            var (rx, ry) = Radii (box, cx, cy);

            if (rx <= 0 || ry <= 0)
                return SKShader.CreateColor (Stops[Stops.Count - 1].Color);

            var (colors, positions, first, last) = Resolve (rx);
            var inner = (float) (first * rx);
            var outer = (float) (last * rx);

            if (outer <= inner)
                return SKShader.CreateColor (colors[colors.Length - 1]);

            // An ellipse is a circle of radius rx, squashed vertically by ry/rx about the centre.
            var matrix = SKMatrix.CreateScale (1, ry / rx, cx, cy);
            var mode = Repeating ? SKShaderTileMode.Repeat : SKShaderTileMode.Clamp;

            return inner > 0
                ? SKShader.CreateTwoPointConicalGradient (new SKPoint (cx, cy), inner, new SKPoint (cx, cy), outer, colors, positions, mode, matrix)
                : SKShader.CreateRadialGradient (new SKPoint (cx, cy), outer, colors, positions, mode, matrix);
        }

        // The ending shape's radii, by CSS Images 3's sizing rules (radial-gradient(): "<radial-size>").
        private (float Rx, float Ry) Radii (SKRect box, float cx, float cy)
        {
            var left = cx - box.Left;
            var right = box.Right - cx;
            var top = cy - box.Top;
            var bottom = box.Bottom - cy;

            if (Size == RadialSize.Explicit) {
                var ex = (float) (SizeX.IsLength ? SizeX.Value : SizeX.Value * box.Width);
                var ey = Circle ? ex : (float) (SizeY.IsLength ? SizeY.Value : SizeY.Value * box.Height);
                return (ex, ey);
            }

            var closeX = Math.Min (Math.Abs (left), Math.Abs (right));
            var closeY = Math.Min (Math.Abs (top), Math.Abs (bottom));
            var farX = Math.Max (Math.Abs (left), Math.Abs (right));
            var farY = Math.Max (Math.Abs (top), Math.Abs (bottom));

            if (Circle) {
                return Size switch {
                    RadialSize.ClosestSide => Same (Math.Min (closeX, closeY)),
                    RadialSize.FarthestSide => Same (Math.Max (farX, farY)),
                    RadialSize.ClosestCorner => Same ((float) Math.Sqrt (closeX * closeX + closeY * closeY)),
                    _ => Same ((float) Math.Sqrt (farX * farX + farY * farY)),
                };
            }

            switch (Size) {
                case RadialSize.ClosestSide:
                    return (closeX, closeY);
                case RadialSize.FarthestSide:
                    return (farX, farY);
                default:
                    // A corner size keeps the aspect ratio of the matching side size and passes through that corner.
                    var (sx, sy, dx, dy) = Size == RadialSize.ClosestCorner ? (closeX, closeY, closeX, closeY) : (farX, farY, farX, farY);
                    if (sx <= 0 || sy <= 0)
                        return (0, 0);
                    var ratio = sy / sx;
                    var rx = (float) Math.Sqrt (dx * dx + dy / ratio * (dy / ratio));
                    return (rx, rx * ratio);
            }

            static (float, float) Same (float r) => (r, r);
        }

        private SKShader ConicShader (SKRect box)
        {
            var cx = X.Resolve (box.Left, box.Width);
            var cy = Y.Resolve (box.Top, box.Height);
            var (colors, positions, first, last) = Resolve (360);

            // CSS's 0deg points up and Skia's sweep starts pointing right; both go clockwise. Skia wants its sweep inside 0..360, so
            // the start is a rotation and the sweep runs from 0 for the span the stops cover.
            var sweep = (float) ((last - first) * 360);

            if (sweep <= 0)
                return SKShader.CreateColor (colors[colors.Length - 1]);

            var rotation = SKMatrix.CreateRotationDegrees ((float) (From - 90 + first * 360), cx, cy);
            return SKShader.CreateSweepGradient (new SKPoint (cx, cy), colors, positions, Repeating ? SKShaderTileMode.Repeat : SKShaderTileMode.Clamp, 0, sweep, rotation);
        }

        // Resolves the stops along a gradient line `length` long (pixels, or degrees for a conic) by CSS's fix-up rules: a missing
        // first or last position is the start or end, a position before an earlier one moves up to it, and the positions in between
        // spread evenly. Returns Skia's colours and 0..1 positions over the span the stops cover, and where that span starts and ends
        // as fractions of the line, so a repeating gradient repeats exactly that span.
        private (SKColor[] Colors, float[] Positions, double First, double Last) Resolve (double length)
        {
            var count = Stops.Count;
            var at = new double?[count];

            for (var i = 0; i < count; i++) {
                var stop = Stops[i];
                at[i] = stop.Unit switch {
                    StopUnit.Auto => null,
                    StopUnit.Percent => stop.Position / 100,
                    StopUnit.Angle => stop.Position / 360,
                    _ => length > 0 ? stop.Position / length : 0
                };
            }

            at[0] ??= 0;
            at[count - 1] ??= 1;

            double max = at[0]!.Value;
            for (var i = 1; i < count; i++) {
                if (at[i] is { } p) {
                    if (p < max)
                        at[i] = max;
                    max = at[i]!.Value;
                }
            }

            for (var i = 1; i < count - 1; i++) {
                if (at[i] is not null)
                    continue;

                var next = i + 1;
                while (at[next] is null)
                    next++;

                var from = at[i - 1]!.Value;
                var to = at[next]!.Value;
                for (var k = i; k < next; k++)
                    at[k] = from + (to - from) * (k - i + 1) / (next - i + 1);
            }

            var first = at[0]!.Value;
            var last = at[count - 1]!.Value;
            var span = last - first;

            var colors = new List<SKColor> (count + 2);
            var positions = new List<float> (count + 2);

            for (var i = 0; i < count; i++) {
                var position = span > 0 ? (float) ((at[i]!.Value - first) / span) : 0f;
                var color = Stops[i].Color;

                // CSS interpolates in premultiplied colour and Skia does not, so a fully transparent stop takes its neighbours'
                // colour (at zero alpha) on each side: fading to 'transparent' then fades the colour out instead of through black.
                if (color.Alpha == 0) {
                    var before = i > 0 ? Stops[i - 1].Color.WithAlpha (0) : (SKColor?) null;
                    var after = i < count - 1 ? Stops[i + 1].Color.WithAlpha (0) : (SKColor?) null;

                    colors.Add (before ?? after ?? color);
                    positions.Add (position);

                    if (before is not null && after is not null && before != after) {
                        colors.Add (after.Value);
                        positions.Add (position);
                    }

                    continue;
                }

                colors.Add (color);
                positions.Add (position);
            }

            return (colors.ToArray (), positions.ToArray (), first, span > 0 ? last : first + 1);
        }
    }
}
