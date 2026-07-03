using SkiaSharp;


namespace Majorsilence.Drawing.Drawing2D
{
    public sealed class LinearGradientBrush : Brush
    {
        private readonly Color _color1;
        private readonly Color _color2;
        private Color[]? _interpolationColors;
        private float[]? _interpolationPositions;
        private bool _gammaCorrection;

        public LinearGradientBrush(Color color) : base(color)
        {
            _color1 = color;
            _color2 = color;
        }

        public LinearGradientBrush(PointF pt1, PointF pt2, Color color1, Color color2)
            : base(color1)
        {
            _color1 = color1;
            _color2 = color2;
            SetGradient(new SKPoint(pt1.X, pt1.Y), new SKPoint(pt2.X, pt2.Y), color1, color2);
        }

        public LinearGradientBrush(Point pt1, Point pt2, Color color1, Color color2)
            : base(color1)
        {
            _color1 = color1;
            _color2 = color2;
            SetGradient(new SKPoint(pt1.X, pt1.Y), new SKPoint(pt2.X, pt2.Y), color1, color2);
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle,
            bool isAngleScaleable = false)
            : base(color1)
        {
            _color1 = color1;
            _color2 = color2;
            ComputeAngleGradientPoints(rect, angle, out var pt1, out var pt2);
            SetGradient(pt1, pt2, color1, color2);
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
            : this(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), color1, color2, angle)
        {
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode mode)
            : base(color1)
        {
            _color1 = color1;
            _color2 = color2;
            ComputeModeGradientPoints(rect, mode, out var pt1, out var pt2);
            SetGradient(pt1, pt2, color1, color2);
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode mode)
            : this(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), color1, color2, mode)
        {
        }

        public bool GammaCorrection
        {
            get => _gammaCorrection;
            set => _gammaCorrection = value;
        }

        // Multi-stop gradient colors
        public Color[]? InterpolationColors
        {
            get => _interpolationColors;
            set
            {
                _interpolationColors = value;
                RebuildShader();
            }
        }

        public float[]? InterpolationPositions
        {
            get => _interpolationPositions;
            set
            {
                _interpolationPositions = value;
                RebuildShader();
            }
        }

        private SKPoint _pt1;
        private SKPoint _pt2;

        private void SetGradient(SKPoint pt1, SKPoint pt2, Color color1, Color color2)
        {
            _pt1 = pt1;
            _pt2 = pt2;
            Paint?.Dispose();
            Paint = CreateGradientPaint(pt1, pt2,
                new[] { color1.ToSkColor(), color2.ToSkColor() }, null);
        }

        private void RebuildShader()
        {
            if (_interpolationColors != null && _interpolationColors.Length >= 2)
            {
                var skColors = _interpolationColors.Select(c => c.ToSkColor()).ToArray();
                Paint?.Dispose();
                Paint = CreateGradientPaint(_pt1, _pt2, skColors, _interpolationPositions);
            }
        }

        private static SKPaint CreateGradientPaint(SKPoint pt1, SKPoint pt2,
            SKColor[] colors, float[]? positions)
        {
            var shader = SKShader.CreateLinearGradient(
                pt1, pt2, colors, positions, SKShaderTileMode.Clamp);
            return new SKPaint { Style = SKPaintStyle.Fill, Shader = shader };
        }

        private static void ComputeAngleGradientPoints(RectangleF rect, float angleDeg,
            out SKPoint pt1, out SKPoint pt2)
        {
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;
#if NET8_0_OR_GREATER
            float angleRad = angleDeg * MathF.PI / 180f;
            float cos = MathF.Cos(angleRad);
            float sin = MathF.Sin(angleRad);

            // Project half-diagonal onto gradient direction to ensure corners are covered
            float halfDiag = MathF.Sqrt(rect.Width * rect.Width + rect.Height * rect.Height) / 2f;
#else
            float angleRad = angleDeg * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);

            // Project half-diagonal onto gradient direction to ensure corners are covered
            float halfDiag = (float)Math.Sqrt(rect.Width * rect.Width + rect.Height * rect.Height) / 2f;
#endif
            pt1 = new SKPoint(cx - cos * halfDiag, cy - sin * halfDiag);
            pt2 = new SKPoint(cx + cos * halfDiag, cy + sin * halfDiag);
        }

        private static void ComputeModeGradientPoints(RectangleF rect, LinearGradientMode mode,
            out SKPoint pt1, out SKPoint pt2)
        {
            switch (mode)
            {
                case LinearGradientMode.Vertical:
                    pt1 = new SKPoint(rect.Left + rect.Width / 2f, rect.Top);
                    pt2 = new SKPoint(rect.Left + rect.Width / 2f, rect.Bottom);
                    break;
                case LinearGradientMode.ForwardDiagonal:
                    pt1 = new SKPoint(rect.Left, rect.Top);
                    pt2 = new SKPoint(rect.Right, rect.Bottom);
                    break;
                case LinearGradientMode.BackwardDiagonal:
                    pt1 = new SKPoint(rect.Right, rect.Top);
                    pt2 = new SKPoint(rect.Left, rect.Bottom);
                    break;
                default: // Horizontal
                    pt1 = new SKPoint(rect.Left, rect.Top + rect.Height / 2f);
                    pt2 = new SKPoint(rect.Right, rect.Top + rect.Height / 2f);
                    break;
            }
        }

        internal override SKPaint ToSkPaint() => Paint!;
    }
}
