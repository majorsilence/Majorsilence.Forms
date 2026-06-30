using SkiaSharp;


namespace Majorsilence.Drawing.Drawing2D
{
    public class Matrix : IDisposable
    {
        private SKMatrix _matrix;

        public Matrix()
        {
            _matrix = SKMatrix.CreateIdentity();
        }

        public Matrix(float m11, float m12, float m21, float m22, float dx, float dy)
        {
            // GDI+ element order: [M11=ScaleX, M12=SkewY, M21=SkewX, M22=ScaleY, dx=TransX, dy=TransY]
            _matrix = new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1);
        }

        internal Matrix(SKMatrix skMatrix)
        {
            _matrix = skMatrix;
        }

        // Elements in GDI+ order: [M11, M12, M21, M22, dx, dy]
        public float[] Elements => new float[]
        {
            _matrix.ScaleX,  // M11
            _matrix.SkewY,   // M12
            _matrix.SkewX,   // M21
            _matrix.ScaleY,  // M22
            _matrix.TransX,  // dx
            _matrix.TransY   // dy
        };

        public bool IsIdentity => _matrix.IsIdentity;

        public bool IsInvertible
        {
            get { return _matrix.TryInvert(out _); }
        }

        public void Reset()
        {
            _matrix = SKMatrix.CreateIdentity();
        }

        public void Translate(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
        {
            var t = SKMatrix.CreateTranslation(dx, dy);
            _matrix = order == MatrixOrder.Append
                ? SKMatrix.Concat(_matrix, t)
                : SKMatrix.Concat(t, _matrix);
        }

        public void Scale(float scaleX, float scaleY, MatrixOrder order = MatrixOrder.Prepend)
        {
            var s = SKMatrix.CreateScale(scaleX, scaleY);
            _matrix = order == MatrixOrder.Append
                ? SKMatrix.Concat(_matrix, s)
                : SKMatrix.Concat(s, _matrix);
        }

        public void Rotate(float angle, MatrixOrder order = MatrixOrder.Prepend)
        {
            var r = SKMatrix.CreateRotationDegrees(angle);
            _matrix = order == MatrixOrder.Append
                ? SKMatrix.Concat(_matrix, r)
                : SKMatrix.Concat(r, _matrix);
        }

        public void RotateAt(float angle, PointF point, MatrixOrder order = MatrixOrder.Prepend)
        {
            var r = SKMatrix.CreateRotationDegrees(angle, point.X, point.Y);
            _matrix = order == MatrixOrder.Append
                ? SKMatrix.Concat(_matrix, r)
                : SKMatrix.Concat(r, _matrix);
        }

        public void Multiply(Matrix matrix, MatrixOrder order = MatrixOrder.Prepend)
        {
            _matrix = order == MatrixOrder.Append
                ? SKMatrix.Concat(_matrix, matrix._matrix)
                : SKMatrix.Concat(matrix._matrix, _matrix);
        }

        public bool Invert()
        {
            if (_matrix.TryInvert(out var inv))
            {
                _matrix = inv;
                return true;
            }
            return false;
        }

        public void TransformPoints(PointF[] pts)
        {
            if (pts == null) return;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = _matrix.MapPoint(pts[i].X, pts[i].Y);
                pts[i] = new PointF(p.X, p.Y);
            }
        }

        public void TransformPoints(Point[] pts)
        {
            if (pts == null) return;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = _matrix.MapPoint(pts[i].X, pts[i].Y);
                pts[i] = new Point((int)p.X, (int)p.Y);
            }
        }

        public void TransformVectors(PointF[] pts)
        {
            if (pts == null) return;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = _matrix.MapVector(pts[i].X, pts[i].Y);
                pts[i] = new PointF(p.X, p.Y);
            }
        }

        public Matrix Clone() => new Matrix(_matrix);

        public SKMatrix ToSKMatrix() => _matrix;

        public void Dispose() { GC.SuppressFinalize(this); }
    }

    public enum MatrixOrder
    {
        Prepend,
        Append
    }
}
