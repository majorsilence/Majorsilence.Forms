using SkiaSharp;


namespace Majorsilence.Forms.Drawing.Drawing2D
{
    public class GraphicsPath : IDisposable
    {
        private SKPath _path = null!;

        public GraphicsPath()
        {
            _path = new SKPath();
        }

        public GraphicsPath(FillMode fillMode)
        {
            _path = new SKPath
            {
                FillType = fillMode == FillMode.Alternate ? SKPathFillType.EvenOdd : SKPathFillType.Winding
            };
        }

        public FillMode FillMode
        {
            get => _path.FillType == SKPathFillType.EvenOdd ? FillMode.Alternate : FillMode.Winding;
            set => _path.FillType = value == FillMode.Alternate ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        }

        public int PointCount => _path.PointCount;

        public PointF[] PathPoints
        {
            get
            {
                var pts = _path.Points;
                var result = new PointF[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                    result[i] = new PointF(pts[i].X, pts[i].Y);
                return result;
            }
        }

        // --- Lines ---

        public void AddLine(float x1, float y1, float x2, float y2)
        {
            if (_path.IsEmpty)
                _path.MoveTo(x1, y1);
            else
            {
                var last = _path.LastPoint;
                if (last.X != x1 || last.Y != y1)
                    _path.MoveTo(x1, y1);
            }
            _path.LineTo(x2, y2);
        }

        public void AddLine(PointF pt1, PointF pt2) => AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);
        public void AddLine(Point pt1, Point pt2) => AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);

        public void AddLines(PointF[] points)
        {
            if (points == null || points.Length < 2) return;
            _path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
                _path.LineTo(points[i].X, points[i].Y);
        }

        public void AddLines(Point[] points)
        {
            if (points == null || points.Length < 2) return;
            _path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
                _path.LineTo(points[i].X, points[i].Y);
        }

        // --- Rectangles ---

        public void AddRectangle(float x, float y, float width, float height)
        {
            _path.AddRect(new SKRect(x, y, x + width, y + height));
        }

        public void AddRectangle(RectangleF rect)
        {
            _path.AddRect(new SKRect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
        }

        public void AddRectangle(Rectangle rect)
        {
            _path.AddRect(new SKRect(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height));
        }

        public void AddRectangles(RectangleF[] rects)
        {
            foreach (var r in rects)
                AddRectangle(r);
        }

        // --- Ellipses ---

        public void AddEllipse(float x, float y, float width, float height)
        {
            _path.AddOval(new SKRect(x, y, x + width, y + height));
        }

        public void AddEllipse(RectangleF rect) => AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);
        public void AddEllipse(Rectangle rect) => AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);

        // --- Arcs ---

        public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            _path.AddArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle);
        }

        public void AddArc(RectangleF rect, float startAngle, float sweepAngle)
            => AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

        public void AddArc(Rectangle rect, float startAngle, float sweepAngle)
            => AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

        // --- Bezier curves ---

        public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            _path.MoveTo(pt1.X, pt1.Y);
            _path.CubicTo(pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
        }

        public void AddBezier(float x1, float y1, float cx1, float cy1,
                               float cx2, float cy2, float x2, float y2)
        {
            _path.MoveTo(x1, y1);
            _path.CubicTo(cx1, cy1, cx2, cy2, x2, y2);
        }

        public void AddBeziers(PointF[] points)
        {
            if (points == null || points.Length < 4) return;
            _path.MoveTo(points[0].X, points[0].Y);
            for (int i = 1; i + 2 < points.Length; i += 3)
                _path.CubicTo(points[i].X, points[i].Y,
                               points[i + 1].X, points[i + 1].Y,
                               points[i + 2].X, points[i + 2].Y);
        }

        // --- Curves (cardinal spline) ---

        public void AddCurve(PointF[] points, float tension = 0.5f)
        {
            if (points == null || points.Length < 2) return;
            var skPts = Array.ConvertAll(points, p => new SKPoint(p.X, p.Y));
            _path.MoveTo(skPts[0]);
            for (int i = 1; i < skPts.Length; i++)
            {
                var p0 = i > 1 ? skPts[i - 2] : skPts[0];
                var p1 = skPts[i - 1];
                var p2 = skPts[i];
                var p3 = i < skPts.Length - 1 ? skPts[i + 1] : skPts[i];
                var cp1 = new SKPoint(p1.X + (p2.X - p0.X) * tension / 3f,
                                      p1.Y + (p2.Y - p0.Y) * tension / 3f);
                var cp2 = new SKPoint(p2.X - (p3.X - p1.X) * tension / 3f,
                                      p2.Y - (p3.Y - p1.Y) * tension / 3f);
                _path.CubicTo(cp1, cp2, p2);
            }
        }

        public void AddCurve(Point[] points, float tension = 0.5f)
        {
            AddCurve(Array.ConvertAll(points, p => new PointF(p.X, p.Y)), tension);
        }

        // --- Polygons ---

        public void AddPolygon(PointF[] points)
        {
            if (points == null || points.Length < 3) return;
            var skPts = Array.ConvertAll(points, p => new SKPoint(p.X, p.Y));
            _path.AddPoly(skPts, true);
        }

        public void AddPolygon(Point[] points)
        {
            if (points == null || points.Length < 3) return;
            var skPts = Array.ConvertAll(points, p => new SKPoint(p.X, p.Y));
            _path.AddPoly(skPts, true);
        }

        // --- Path composition ---

        public void AddPath(GraphicsPath addingPath, bool connect)
        {
            if (addingPath == null) return;
            var mode = connect ? SKPathAddMode.Append : SKPathAddMode.Extend;
            _path.AddPath(addingPath._path, mode);
        }

        // --- Figure control ---

        public void StartFigure()
        {
            // MoveTo on next add will start a new contour; mark that we may need it
        }

        public void CloseFigure()
        {
            _path.Close();
        }

        public void CloseAllFigures()
        {
            // SkiaSharp closes all open contours when a path is filled
            _path.Close();
        }

        public void Reset()
        {
            _path.Reset();
        }

        // --- Geometry queries ---

        public RectangleF GetBounds()
        {
            var b = _path.Bounds;
            return new RectangleF(b.Left, b.Top, b.Width, b.Height);
        }

        public RectangleF GetBounds(Matrix matrix)
        {
            using var transformed = new SKPath(_path);
            transformed.Transform(matrix.ToSKMatrix());
            var b = transformed.Bounds;
            return new RectangleF(b.Left, b.Top, b.Width, b.Height);
        }

        public bool IsVisible(float x, float y) => _path.Contains(x, y);
        public bool IsVisible(PointF pt) => _path.Contains(pt.X, pt.Y);

        // --- Transform ---

        public void Transform(Matrix matrix)
        {
            _path.Transform(matrix.ToSKMatrix());
        }

        // --- SkiaSharp interop ---

        public SKPath ToSKPath() => _path;

        public void Dispose()
        {
            _path?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
