using Majorsilence.Drawing.Drawing2D;
using SkiaSharp;


namespace Majorsilence.Drawing
{
    public class Pen : IDisposable
    {
        private SKPaint _paint = null!;
        private Color _color;
        private float _width;
        private float[]? _dashPattern;

        public Pen(Color color) : this(color, 1f)
        {
        }

        public Pen(Color color, float width)
        {
            _color = color;
            _width = width;
            _paint = new SKPaint
            {
                Color = color.ToSkColor(),
                StrokeWidth = width,
                Style = SKPaintStyle.Stroke
            };
        }

        public Pen(Brush brush) : this(brush, 1f)
        {
        }

        public Pen(Brush brush, float width)
        {
            _width = width;
            if (brush is SolidBrush solid)
            {
                _color = solid.Color;
                _paint = new SKPaint
                {
                    Color = solid.Color.ToSkColor(),
                    StrokeWidth = width,
                    Style = SKPaintStyle.Stroke
                };
            }
            else
            {
                // Use the brush's paint as a stroke paint
                var basePaint = brush.ToSkPaint();
                _color = Color.Black;
                _paint = new SKPaint
                {
                    Color = basePaint.Color,
                    StrokeWidth = width,
                    Style = SKPaintStyle.Stroke,
                    Shader = basePaint.Shader
                };
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                _paint.Color = value.ToSkColor();
                _paint.Shader = null; // clear any shader when explicit color is set
            }
        }

        public float Width
        {
            get => _width;
            set
            {
                _width = value;
                _paint.StrokeWidth = value;
            }
        }

        public LineJoin LineJoin { get; set; }
        public LineCap LineCap { get; set; }
        public LineCap StartCap { get; set; }
        public LineCap EndCap { get; set; }
        public Brush? Brush { get; set; }
        public DashStyle DashStyle { get; set; }

        // Custom dash pattern (lengths of dashes and gaps in stroke-width units)
        public float[]? DashPattern
        {
            get => _dashPattern;
            set
            {
                _dashPattern = value;
                DashStyle = value != null ? DashStyle.Custom : DashStyle.Solid;
            }
        }

        public float DashOffset { get; set; }
        public float MiterLimit
        {
            get => _paint.StrokeMiter;
            set => _paint.StrokeMiter = value;
        }

        public void Dispose()
        {
            _paint?.Dispose();
            GC.SuppressFinalize(this);
        }

        public SKPaint ToSkPaint()
        {
            _paint.StrokeJoin = LineJoin switch
            {
                LineJoin.Miter => SKStrokeJoin.Miter,
                LineJoin.Round => SKStrokeJoin.Round,
                LineJoin.Bevel => SKStrokeJoin.Bevel,
                LineJoin.MiterClipped => SKStrokeJoin.Miter,
                _ => _paint.StrokeJoin
            };

            var cap = LineCap != LineCap.Flat ? LineCap : StartCap;
            _paint.StrokeCap = cap switch
            {
                LineCap.Square => SKStrokeCap.Square,
                LineCap.Round => SKStrokeCap.Round,
                _ => SKStrokeCap.Butt
            };

            ApplyDashStyle();

            return _paint;
        }

        private void ApplyDashStyle()
        {
            switch (DashStyle)
            {
                case DashStyle.Solid:
                    _paint.PathEffect = null;
                    break;

                case DashStyle.Dash:
                    _paint.PathEffect = SKPathEffect.CreateDash(
                        new[] { 4f * _width, 2f * _width }, DashOffset);
                    break;

                case DashStyle.Dot:
                    _paint.PathEffect = SKPathEffect.CreateDash(
                        new[] { _width, 2f * _width }, DashOffset);
                    break;

                case DashStyle.DashDot:
                    _paint.PathEffect = SKPathEffect.CreateDash(
                        new[] { 4f * _width, 2f * _width, _width, 2f * _width }, DashOffset);
                    break;

                case DashStyle.DashDotDot:
                    _paint.PathEffect = SKPathEffect.CreateDash(
                        new[] { 4f * _width, 2f * _width, _width, 2f * _width, _width, 2f * _width }, DashOffset);
                    break;

                case DashStyle.Custom:
                    if (_dashPattern != null && _dashPattern.Length >= 2)
                    {
                        // System.Drawing dash patterns are in units of pen width
                        var scaledPattern = _dashPattern.Select(v => v * _width).ToArray();
                        _paint.PathEffect = SKPathEffect.CreateDash(scaledPattern, DashOffset);
                    }
                    break;
            }
        }

        public Pen Clone()
        {
            var clone = new Pen(_color, _width)
            {
                LineJoin = LineJoin,
                LineCap = LineCap,
                StartCap = StartCap,
                EndCap = EndCap,
                DashStyle = DashStyle,
                DashOffset = DashOffset,
                _dashPattern = _dashPattern?.ToArray()
            };
            return clone;
        }
    }
}
