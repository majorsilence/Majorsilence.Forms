using System;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Drawing2D
{
    /// <summary>
    /// Represents a series of connected lines and curves. Cross-platform replacement for
    /// <c>System.Drawing.Drawing2D.GraphicsPath</c>, backed by a SkiaSharp <see cref="SKPath"/>.
    /// </summary>
    public sealed class GraphicsPath : IDisposable
    {
        private SKPath path = new SKPath ();

        /// <summary>Initializes a new empty graphics path.</summary>
        public GraphicsPath () { }

        /// <summary>Initializes a new graphics path with the specified fill mode.</summary>
        public GraphicsPath (FillMode fillMode)
        {
            FillMode = fillMode;
        }

        /// <summary>Gets or sets the fill mode for this path.</summary>
        public FillMode FillMode { get; set; } = FillMode.Alternate;

        /// <summary>Gets the points that make up this path.</summary>
        public PointF[] PathPoints
        {
            get {
                var pts = path.Points;
                var result = new PointF[pts.Length];
                for (var i = 0; i < pts.Length; i++)
                    result[i] = new PointF (pts[i].X, pts[i].Y);
                return result;
            }
        }

        /// <summary>Gets the number of points in this path.</summary>
        public int PointCount => path.PointCount;

        internal SKPath ToSKPath () => path;

        /// <summary>Appends a line segment to this path.</summary>
        public void AddLine (float x1, float y1, float x2, float y2)
        {
            EnsureStart (x1, y1);
            path.LineTo (x2, y2);
        }

        /// <summary>Appends a line segment to this path.</summary>
        public void AddLine (PointF pt1, PointF pt2) => AddLine (pt1.X, pt1.Y, pt2.X, pt2.Y);

        /// <summary>Appends a line segment using integer Point coordinates.</summary>
        public void AddLine (Point pt1, Point pt2) => AddLine (pt1.X, pt1.Y, pt2.X, pt2.Y);

        /// <summary>Appends a series of connected line segments (PointF overload).</summary>
        public void AddLines (PointF[] points)
        {
            if (points is null || points.Length < 2)
                return;
            EnsureStart (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
                path.LineTo (points[i].X, points[i].Y);
        }

        /// <summary>Appends a series of connected line segments (Point overload).</summary>
        public void AddLines (Point[] points)
        {
            if (points is null || points.Length < 2)
                return;
            EnsureStart (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
                path.LineTo (points[i].X, points[i].Y);
        }

        /// <summary>Appends a rectangle to this path.</summary>
        public void AddRectangle (RectangleF rect) => path.AddRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));

        /// <summary>Appends a rectangle using float coordinates.</summary>
        public void AddRectangle (float x, float y, float width, float height) => AddRectangle (new RectangleF (x, y, width, height));

        /// <summary>Appends a rectangle using integer coordinates.</summary>
        public void AddRectangle (Rectangle rect) => path.AddRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));

        /// <summary>Appends a series of rectangles to this path.</summary>
        public void AddRectangles (RectangleF[] rects)
        {
            if (rects is null) return;
            foreach (var r in rects) AddRectangle (r);
        }

        /// <summary>Appends an ellipse to this path.</summary>
        public void AddEllipse (RectangleF rect) => path.AddOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));

        /// <summary>Appends an ellipse to this path.</summary>
        public void AddEllipse (float x, float y, float width, float height) => AddEllipse (new RectangleF (x, y, width, height));

        /// <summary>Appends an ellipse using integer rectangle coordinates.</summary>
        public void AddEllipse (Rectangle rect) => AddEllipse (new RectangleF (rect.X, rect.Y, rect.Width, rect.Height));

        /// <summary>Appends an elliptical arc to this path.</summary>
        public void AddArc (float x, float y, float width, float height, float startAngle, float sweepAngle)
            => path.AddArc (new SKRect (x, y, x + width, y + height), startAngle, sweepAngle);

        /// <summary>Appends an elliptical arc to this path.</summary>
        public void AddArc (RectangleF rect, float startAngle, float sweepAngle)
            => path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);

        /// <summary>Appends an elliptical arc using an integer rectangle.</summary>
        public void AddArc (Rectangle rect, float startAngle, float sweepAngle)
            => path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);

        /// <summary>Appends a cubic Bézier curve to this path.</summary>
        public void AddBezier (PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            EnsureStart (pt1.X, pt1.Y);
            path.CubicTo (pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
        }

        /// <summary>Appends a cubic Bézier curve using float coordinates.</summary>
        public void AddBezier (float x1, float y1, float cx1, float cy1, float cx2, float cy2, float x2, float y2)
        {
            EnsureStart (x1, y1);
            path.CubicTo (cx1, cy1, cx2, cy2, x2, y2);
        }

        /// <summary>Appends a sequence of cubic Bézier curves (groups of 4 points: anchor, ctrl1, ctrl2, anchor).</summary>
        public void AddBeziers (PointF[] points)
        {
            if (points is null || points.Length < 4)
                return;
            EnsureStart (points[0].X, points[0].Y);
            for (int i = 1; i + 2 < points.Length; i += 3)
                path.CubicTo (points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, points[i + 2].X, points[i + 2].Y);
        }

        /// <summary>Appends a cardinal spline through the specified PointF array.</summary>
        public void AddCurve (PointF[] points) => AddCurve (points, 0.5f);

        /// <summary>Appends a cardinal spline through the specified PointF array with the given tension.</summary>
        public void AddCurve (PointF[] points, float tension)
        {
            if (points is null || points.Length < 2)
                return;
            EnsureStart (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
                path.LineTo (points[i].X, points[i].Y);
        }

        /// <summary>Appends a cardinal spline through the specified Point array.</summary>
        public void AddCurve (Point[] points) => AddCurve (points, 0.5f);

        /// <summary>Appends a cardinal spline through the specified Point array with the given tension.</summary>
        public void AddCurve (Point[] points, float tension)
        {
            if (points is null || points.Length < 2)
                return;
            EnsureStart (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
                path.LineTo (points[i].X, points[i].Y);
        }

        /// <summary>Appends a closed polygon (PointF overload).</summary>
        public void AddPolygon (PointF[] points)
        {
            if (points is null || points.Length == 0)
                return;

            var sk = new SKPoint[points.Length];
            for (var i = 0; i < points.Length; i++)
                sk[i] = new SKPoint (points[i].X, points[i].Y);

            path.AddPoly (sk, true);
        }

        /// <summary>Appends a closed polygon (Point overload).</summary>
        public void AddPolygon (Point[] points)
        {
            if (points is null || points.Length == 0)
                return;

            var sk = new SKPoint[points.Length];
            for (var i = 0; i < points.Length; i++)
                sk[i] = new SKPoint (points[i].X, points[i].Y);

            path.AddPoly (sk, true);
        }

        /// <summary>Appends another path to this path.</summary>
        public void AddPath (GraphicsPath addingPath, bool connect)
        {
            if (addingPath is not null)
                path.AddPath (addingPath.path);
        }

        /// <summary>Starts a new figure without closing the current one.</summary>
        public void StartFigure () { }

        /// <summary>Closes the current figure.</summary>
        public void CloseFigure () => path.Close ();

        /// <summary>Closes all open figures.</summary>
        public void CloseAllFigures () => path.Close ();

        /// <summary>Empties this path.</summary>
        public void Reset ()
        {
            path.Dispose ();
            path = new SKPath ();
        }

        /// <summary>Applies the specified matrix transformation to this path.</summary>
        public void Transform (Matrix matrix)
        {
            if (matrix is not null)
                path.Transform (matrix.ToSKMatrix ());
        }

        /// <summary>Gets the bounding rectangle of this path.</summary>
        public RectangleF GetBounds ()
        {
            var b = path.Bounds;
            return new RectangleF (b.Left, b.Top, b.Width, b.Height);
        }

        /// <summary>Gets the bounding rectangle of this path after applying the specified matrix.</summary>
        public RectangleF GetBounds (Matrix matrix)
        {
            using var copy = new SKPath (path);
            if (matrix is not null)
                copy.Transform (matrix.ToSKMatrix ());
            var b = copy.Bounds;
            return new RectangleF (b.Left, b.Top, b.Width, b.Height);
        }

        /// <summary>Returns whether the specified point lies within this path.</summary>
        public bool IsVisible (PointF point) => path.Contains (point.X, point.Y);

        /// <summary>Returns whether the specified point lies within this path.</summary>
        public bool IsVisible (float x, float y) => path.Contains (x, y);

        /// <summary>Returns whether the specified point lies within this path.</summary>
        public bool IsVisible (Point point) => path.Contains (point.X, point.Y);

        private void EnsureStart (float x, float y)
        {
            if (path.PointCount == 0)
                path.MoveTo (x, y);
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            path?.Dispose ();
            path = null!;
        }
    }

    /// <summary>
    /// Encapsulates a 3x2 affine transformation matrix. Cross-platform replacement for
    /// <c>System.Drawing.Drawing2D.Matrix</c>.
    /// </summary>
    public sealed class Matrix : IDisposable
    {
        private SKMatrix matrix = SKMatrix.Identity;

        /// <summary>Initializes a new identity matrix.</summary>
        public Matrix () { }

        /// <summary>Initializes a new matrix with the specified elements.</summary>
        public Matrix (float m11, float m12, float m21, float m22, float dx, float dy)
        {
            matrix = new SKMatrix { ScaleX = m11, SkewY = m12, SkewX = m21, ScaleY = m22, TransX = dx, TransY = dy, Persp2 = 1 };
        }

        internal Matrix (SKMatrix skMatrix) => matrix = skMatrix;

        internal SKMatrix ToSKMatrix () => matrix;

        /// <summary>Gets the matrix elements.</summary>
        public float[] Elements => new[] { matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY };

        /// <summary>Gets whether this is the identity matrix.</summary>
        public bool IsIdentity => matrix.IsIdentity;

        /// <summary>Gets whether this matrix is invertible.</summary>
        public bool IsInvertible => matrix.TryInvert (out _);

        /// <summary>Resets this matrix to the identity matrix.</summary>
        public void Reset () => matrix = SKMatrix.Identity;

        /// <summary>Applies the specified translation.</summary>
        public void Translate (float offsetX, float offsetY, MatrixOrder order = MatrixOrder.Prepend)
            => matrix = order == MatrixOrder.Append
                ? matrix.PostConcat (SKMatrix.CreateTranslation (offsetX, offsetY))
                : matrix.PreConcat (SKMatrix.CreateTranslation (offsetX, offsetY));

        /// <summary>Applies the specified scale.</summary>
        public void Scale (float scaleX, float scaleY, MatrixOrder order = MatrixOrder.Prepend)
            => matrix = order == MatrixOrder.Append
                ? matrix.PostConcat (SKMatrix.CreateScale (scaleX, scaleY))
                : matrix.PreConcat (SKMatrix.CreateScale (scaleX, scaleY));

        /// <summary>Applies the specified rotation, in degrees.</summary>
        public void Rotate (float angle, MatrixOrder order = MatrixOrder.Prepend)
            => matrix = order == MatrixOrder.Append
                ? matrix.PostConcat (SKMatrix.CreateRotationDegrees (angle))
                : matrix.PreConcat (SKMatrix.CreateRotationDegrees (angle));

        /// <summary>Applies a rotation about the specified point.</summary>
        public void RotateAt (float angle, PointF center, MatrixOrder order = MatrixOrder.Prepend)
        {
            var t1 = SKMatrix.CreateTranslation (-center.X, -center.Y);
            var r  = SKMatrix.CreateRotationDegrees (angle);
            var t2 = SKMatrix.CreateTranslation (center.X, center.Y);
            var combined = t2.PreConcat (r).PreConcat (t1);
            matrix = order == MatrixOrder.Append
                ? matrix.PostConcat (combined)
                : matrix.PreConcat (combined);
        }

        /// <summary>Multiplies this matrix by another.</summary>
        public void Multiply (Matrix m, MatrixOrder order = MatrixOrder.Prepend)
            => matrix = order == MatrixOrder.Append
                ? matrix.PostConcat (m.matrix)
                : matrix.PreConcat (m.matrix);

        /// <summary>Inverts this matrix. Returns false if not invertible.</summary>
        public bool Invert ()
        {
            if (!matrix.TryInvert (out var inv))
                return false;
            matrix = inv;
            return true;
        }

        /// <summary>Transforms an array of PointF values in place using this matrix.</summary>
        public void TransformPoints (PointF[] points)
        {
            if (points is null) return;
            for (int i = 0; i < points.Length; i++)
            {
                var mapped = matrix.MapPoint (new SKPoint (points[i].X, points[i].Y));
                points[i] = new PointF (mapped.X, mapped.Y);
            }
        }

        /// <summary>Transforms an array of Point values in place using this matrix.</summary>
        public void TransformPoints (Point[] points)
        {
            if (points is null) return;
            for (int i = 0; i < points.Length; i++)
            {
                var mapped = matrix.MapPoint (new SKPoint (points[i].X, points[i].Y));
                points[i] = new Point ((int)mapped.X, (int)mapped.Y);
            }
        }

        /// <summary>Applies only the rotation/scale of this matrix to vectors (translation is ignored).</summary>
        public void TransformVectors (PointF[] points)
        {
            if (points is null) return;
            var noTranslate = new SKMatrix {
                ScaleX = matrix.ScaleX, SkewX = matrix.SkewX,
                SkewY  = matrix.SkewY,  ScaleY = matrix.ScaleY,
                TransX = 0, TransY = 0, Persp2 = 1
            };
            for (int i = 0; i < points.Length; i++)
            {
                var mapped = noTranslate.MapPoint (new SKPoint (points[i].X, points[i].Y));
                points[i] = new PointF (mapped.X, mapped.Y);
            }
        }

        /// <summary>Creates an exact copy of this matrix.</summary>
        public Matrix Clone () => new Matrix (matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TransX, matrix.TransY);

        /// <inheritdoc/>
        public void Dispose () { }
    }

    /// <summary>Represents the saved state of a Graphics object. Stub in Majorsilence.Forms.Drawing.</summary>
    public sealed class GraphicsState
    {
        internal int Count { get; }
        internal GraphicsState (int count = 0) => Count = count;
    }

    /// <summary>Specifies the order of matrix transform operations. Matches System.Drawing.Drawing2D.MatrixOrder.</summary>
    public enum MatrixOrder
    {
        /// <summary>The new operation is applied before the existing transform.</summary>
        Prepend = 0,
        /// <summary>The new operation is applied after the existing transform.</summary>
        Append = 1
    }

    /// <summary>Specifies how the interior of a closed path is filled. Matches System.Drawing.Drawing2D.FillMode.</summary>
    public enum FillMode
    {
        /// <summary>The alternate (even-odd) fill rule.</summary>
        Alternate = 0,
        /// <summary>The winding fill rule.</summary>
        Winding = 1
    }

    /// <summary>Specifies whether smoothing (antialiasing) is applied. Matches System.Drawing.Drawing2D.SmoothingMode.</summary>
    public enum SmoothingMode
    {
        /// <summary>The default smoothing mode.</summary>
        Default = 0,
        /// <summary>High speed, low quality.</summary>
        HighSpeed = 1,
        /// <summary>High quality, low speed.</summary>
        HighQuality = 2,
        /// <summary>No antialiasing.</summary>
        None = 3,
        /// <summary>Antialiasing.</summary>
        AntiAlias = 4
    }

    /// <summary>Specifies how pixels are offset during rendering. Matches System.Drawing.Drawing2D.PixelOffsetMode.</summary>
    public enum PixelOffsetMode
    {
        /// <summary>The default pixel offset mode.</summary>
        Default = 0,
        /// <summary>High speed, low quality.</summary>
        HighSpeed = 1,
        /// <summary>High quality, low speed.</summary>
        HighQuality = 2,
        /// <summary>No pixel offset.</summary>
        None = 3,
        /// <summary>Pixels are offset by -0.5 units for high speed antialiasing.</summary>
        Half = 4
    }

    /// <summary>Specifies how images are scaled. Matches System.Drawing.Drawing2D.InterpolationMode.</summary>
    public enum InterpolationMode
    {
        /// <summary>An invalid mode.</summary>
        Invalid = -1,
        /// <summary>The default interpolation mode.</summary>
        Default = 0,
        /// <summary>Low quality.</summary>
        Low = 1,
        /// <summary>High quality.</summary>
        High = 2,
        /// <summary>Bilinear interpolation.</summary>
        Bilinear = 3,
        /// <summary>Bicubic interpolation.</summary>
        Bicubic = 4,
        /// <summary>Nearest-neighbor interpolation.</summary>
        NearestNeighbor = 5,
        /// <summary>High quality bilinear interpolation.</summary>
        HighQualityBilinear = 6,
        /// <summary>High quality bicubic interpolation.</summary>
        HighQualityBicubic = 7
    }

    /// <summary>Specifies how colors are combined. Matches System.Drawing.Drawing2D.CompositingMode.</summary>
    public enum CompositingMode
    {
        /// <summary>Source pixels overwrite background pixels.</summary>
        SourceOver = 0,
        /// <summary>Source pixels replace background pixels.</summary>
        SourceCopy = 1
    }

    /// <summary>Specifies the quality of compositing. Matches System.Drawing.Drawing2D.CompositingQuality.</summary>
    public enum CompositingQuality
    {
        /// <summary>An invalid quality.</summary>
        Invalid = -1,
        /// <summary>The default quality.</summary>
        Default = 0,
        /// <summary>High speed, low quality.</summary>
        HighSpeed = 1,
        /// <summary>High quality, low speed.</summary>
        HighQuality = 2,
        /// <summary>Gamma-corrected blending.</summary>
        GammaCorrected = 3,
        /// <summary>Assume linear values.</summary>
        AssumeLinear = 4
    }

    /// <summary>Specifies the available cap styles for line ends. Matches System.Drawing.Drawing2D.LineCap.</summary>
    public enum LineCap
    {
        /// <summary>A flat cap.</summary>
        Flat = 0,
        /// <summary>A square cap.</summary>
        Square = 1,
        /// <summary>A round cap.</summary>
        Round = 2,
        /// <summary>A triangular cap.</summary>
        Triangle = 3
    }

    /// <summary>Specifies how lines are joined. Matches System.Drawing.Drawing2D.LineJoin.</summary>
    public enum LineJoin
    {
        /// <summary>Mitered join.</summary>
        Miter = 0,
        /// <summary>Beveled join.</summary>
        Bevel = 1,
        /// <summary>Rounded join.</summary>
        Round = 2,
        /// <summary>Clipped mitered join.</summary>
        MiterClipped = 3
    }

    /// <summary>Specifies how a texture or gradient is tiled. Matches System.Drawing.Drawing2D.WrapMode.</summary>
    public enum WrapMode
    {
        /// <summary>Tiles the gradient or texture.</summary>
        Tile = 0,
        /// <summary>Reverses the texture horizontally then tiles.</summary>
        TileFlipX = 1,
        /// <summary>Reverses the texture vertically then tiles.</summary>
        TileFlipY = 2,
        /// <summary>Reverses the texture in both directions then tiles.</summary>
        TileFlipXY = 3,
        /// <summary>The texture or gradient is not tiled.</summary>
        Clamp = 4
    }

    /// <summary>Specifies the direction of a linear gradient. Matches System.Drawing.Drawing2D.LinearGradientMode.</summary>
    public enum LinearGradientMode
    {
        /// <summary>Gradient runs from left to right.</summary>
        Horizontal = 0,
        /// <summary>Gradient runs from top to bottom.</summary>
        Vertical = 1,
        /// <summary>Gradient runs from upper-left to lower-right.</summary>
        ForwardDiagonal = 2,
        /// <summary>Gradient runs from upper-right to lower-left.</summary>
        BackwardDiagonal = 3
    }
}
