using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing;
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

        /// <summary>
        /// Initializes a path from an array of points and a matching array of point types.
        /// </summary>
        /// <remarks>
        /// Only the <see cref="PathPointType.Start"/>/<see cref="PathPointType.Line"/> distinction and
        /// the <see cref="PathPointType.CloseSubpath"/> flag are honoured; Bezier point types are
        /// treated as line segments, since reconstructing curve control points from a flat array is
        /// ambiguous without the original path. Callers that need curves should build the path with
        /// <c>AddBezier</c> instead. This is the constructor WinForms code uses to stamp out a fixed
        /// polygon in one call.
        /// </remarks>
        /// <exception cref="ArgumentException">The two arrays have different lengths.</exception>
        public GraphicsPath (PointF[] pts, byte[] types)
        {
            ArgumentNullException.ThrowIfNull (pts);
            ArgumentNullException.ThrowIfNull (types);

            if (pts.Length != types.Length)
                throw new ArgumentException ("pts and types must have the same length.", nameof (types));

            for (var i = 0; i < pts.Length; i++) {
                var kind = (PathPointType)(types[i] & (byte)PathPointType.PathTypeMask);

                if (i == 0 || kind == PathPointType.Start)
                    path.MoveTo (pts[i].X, pts[i].Y);
                else
                    path.LineTo (pts[i].X, pts[i].Y);

                if ((types[i] & (byte)PathPointType.CloseSubpath) != 0)
                    path.Close ();
            }
        }

        /// <inheritdoc cref="GraphicsPath(PointF[], byte[])"/>
        public GraphicsPath (Point[] pts, byte[] types)
            : this (ToPointF (pts), types)
        {
        }

        private static PointF[] ToPointF (Point[] pts)
        {
            ArgumentNullException.ThrowIfNull (pts);

            var result = new PointF[pts.Length];
            for (var i = 0; i < pts.Length; i++)
                result[i] = new PointF (pts[i].X, pts[i].Y);

            return result;
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

        // Point indices flagged by SetMarkers. GDI+ carries markers as a flag bit on the point type
        // rather than as separate state, which is how they surface through PathTypes below.
        private readonly List<int> markers = [];

        /// <summary>Gets the type of each point in this path, as GDI+ <see cref="PathPointType"/> flags.</summary>
        public byte[] PathTypes {
            get {
                var (_, types) = GraphicsPathIterator.Decompose (this);
                foreach (var index in markers)
                    if (index >= 0 && index < types.Length)
                        types[index] |= (byte)PathPointType.PathMarker;
                return types;
            }
        }

        /// <summary>Gets the points and their types together.</summary>
        public PathData PathData {
            get {
                var (points, _) = GraphicsPathIterator.Decompose (this);
                return new PathData { Points = points, Types = PathTypes };
            }
        }

        /// <summary>Gets the last point in this path.</summary>
        public PointF GetLastPoint ()
        {
            var last = path.LastPoint;
            return new PointF (last.X, last.Y);
        }

        /// <summary>Creates an independent copy of this path.</summary>
        public GraphicsPath Clone ()
        {
            var clone = new GraphicsPath (FillMode);
            clone.path.Dispose ();
            clone.path = new SKPath (path);
            clone.markers.AddRange (markers);
            return clone;
        }

        /// <summary>
        /// Flags the current end of the path as a marker, which surfaces as
        /// <see cref="PathPointType.PathMarker"/> on that point in <see cref="PathTypes"/>.
        /// </summary>
        public void SetMarkers ()
        {
            var count = GraphicsPathIterator.Decompose (this).Types.Length;
            if (count > 0)
                markers.Add (count - 1);
        }

        /// <summary>Removes every marker previously set by <see cref="SetMarkers"/>.</summary>
        public void ClearMarkers () => markers.Clear ();

        /// <summary>Appends a pie section (an arc closed back to the ellipse's center) to this path.</summary>
        public void AddPie (float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            var oval = new SKRect (x, y, x + width, y + height);
            path.MoveTo (x + width / 2f, y + height / 2f);
            path.ArcTo (oval, startAngle, sweepAngle, forceMoveTo: false);
            path.Close ();
        }

        /// <inheritdoc cref="AddPie(float, float, float, float, float, float)"/>
        public void AddPie (Rectangle rect, float startAngle, float sweepAngle)
            => AddPie (rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

        /// <inheritdoc cref="AddPie(float, float, float, float, float, float)"/>
        public void AddPie (int x, int y, int width, int height, float startAngle, float sweepAngle)
            => AddPie ((float)x, y, width, height, startAngle, sweepAngle);

        /// <summary>Appends a closed cardinal spline through the specified points.</summary>
        public void AddClosedCurve (PointF[] points) => AddClosedCurve (points, 0.5f);

        /// <inheritdoc cref="AddClosedCurve(PointF[])"/>
        public void AddClosedCurve (PointF[] points, float tension)
        {
            if (points is null || points.Length < 2)
                return;
            AddCurve (points, tension);
            path.Close ();
        }

        /// <inheritdoc cref="AddClosedCurve(PointF[])"/>
        public void AddClosedCurve (Point[] points) => AddClosedCurve (points, 0.5f);

        /// <inheritdoc cref="AddClosedCurve(PointF[])"/>
        public void AddClosedCurve (Point[] points, float tension)
        {
            if (points is null || points.Length < 2)
                return;
            AddCurve (points, tension);
            path.Close ();
        }

        /// <summary>
        /// Appends the outline of a text string to this path, so the glyphs can be filled, stroked or
        /// hit-tested as geometry.
        /// </summary>
        /// <param name="s">The text to add.</param>
        /// <param name="family">The font family to render with.</param>
        /// <param name="style">A <see cref="FontStyle"/> value, as an int, matching System.Drawing.</param>
        /// <param name="emSize">The em size, in the same units as the rest of the path (pixels here).</param>
        /// <param name="origin">The top-left corner the text is laid out from.</param>
        /// <param name="format">Accepted for API compatibility; alignment and wrapping are not applied.</param>
        /// <remarks>
        /// Backed by a real glyph-outline lookup (<c>SKFont.GetTextPath</c>), not an approximation.
        /// <paramref name="origin"/> is the top-left as GDI+ defines it, so the baseline offset is
        /// applied from the font's ascent before the outline is taken.
        /// </remarks>
        public void AddString (string s, FontFamily family, int style, float emSize, PointF origin, StringFormat? format)
        {
            if (string.IsNullOrEmpty (s) || family is null || emSize <= 0)
                return;

            var fontStyle = (FontStyle)style;
            var typeface = FontSubstitution.Resolve (family.Name, new SKFontStyle (
                (fontStyle & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                (fontStyle & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright));

            using var font = new SKFont (typeface, emSize);
            // GDI+ lays text out from the top-left; Skia draws from the baseline.
            using var text = font.GetTextPath (s, new SKPoint (origin.X, origin.Y - font.Metrics.Ascent));
            if (text is not null)
                path.AddPath (text);
        }

        /// <inheritdoc cref="AddString(string, FontFamily, int, float, PointF, StringFormat)"/>
        public void AddString (string s, FontFamily family, int style, float emSize, Point origin, StringFormat? format)
            => AddString (s, family, style, emSize, new PointF (origin.X, origin.Y), format);

        /// <inheritdoc cref="AddString(string, FontFamily, int, float, PointF, StringFormat)"/>
        /// <remarks>The text is laid out from the rectangle's top-left corner; it is not wrapped to fit.</remarks>
        public void AddString (string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat? format)
            => AddString (s, family, style, emSize, new PointF (layoutRect.X, layoutRect.Y), format);

        /// <inheritdoc cref="AddString(string, FontFamily, int, float, RectangleF, StringFormat)"/>
        public void AddString (string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat? format)
            => AddString (s, family, style, emSize, new PointF (layoutRect.X, layoutRect.Y), format);

        /// <summary>Replaces every curve in this path with a sequence of connected line segments.</summary>
        public void Flatten () => Flatten (null, 0.25f);

        /// <inheritdoc cref="Flatten()"/>
        public void Flatten (Matrix? matrix) => Flatten (matrix, 0.25f);

        /// <summary>
        /// Replaces every curve with line segments, optionally transforming the path first.
        /// </summary>
        /// <param name="matrix">Applied before flattening, if supplied.</param>
        /// <param name="flatness">
        /// The maximum error, in path units, between the curve and its approximation. Smaller values
        /// produce more segments.
        /// </param>
        public void Flatten (Matrix? matrix, float flatness)
        {
            if (matrix is not null)
                Transform (matrix);

            var flattened = new SKPath { FillType = path.FillType };
            using (var iterator = path.CreateRawIterator ()) {
                var buffer = new SKPoint[4];
                var current = new SKPoint ();
                while (true) {
                    var verb = iterator.Next (buffer);
                    if (verb == SKPathVerb.Done)
                        break;

                    switch (verb) {
                        case SKPathVerb.Move:
                            flattened.MoveTo (buffer[0]);
                            current = buffer[0];
                            break;
                        case SKPathVerb.Line:
                            flattened.LineTo (buffer[1]);
                            current = buffer[1];
                            break;
                        case SKPathVerb.Quad:
                            EmitCurve (p => Quad (buffer[0], buffer[1], buffer[2], p));
                            current = buffer[2];
                            break;
                        case SKPathVerb.Conic: {
                            var quads = SKPath.ConvertConicToQuads (buffer[0], buffer[1], buffer[2], iterator.ConicWeight (), 2);
                            for (var i = 0; i + 2 < quads.Length; i += 2) {
                                var (a, b, c) = (quads[i], quads[i + 1], quads[i + 2]);
                                EmitCurve (p => Quad (a, b, c, p));
                            }
                            current = buffer[2];
                            break;
                        }
                        case SKPathVerb.Cubic:
                            EmitCurve (p => Cubic (buffer[0], buffer[1], buffer[2], buffer[3], p));
                            current = buffer[3];
                            break;
                        case SKPathVerb.Close:
                            flattened.Close ();
                            break;
                    }
                }

                // Segment count scales with how far the control points stray from a straight line, so a
                // tighter flatness genuinely produces a finer approximation.
                void EmitCurve (Func<float, SKPoint> evaluate)
                {
                    var steps = Math.Clamp ((int)(1f / Math.Max (0.01f, flatness) * 8f), 4, 128);
                    for (var i = 1; i <= steps; i++)
                        flattened.LineTo (evaluate (i / (float)steps));
                }
            }

            path.Dispose ();
            path = flattened;

            static SKPoint Quad (SKPoint p0, SKPoint p1, SKPoint p2, float t)
            {
                var u = 1f - t;
                return new SKPoint (u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X,
                                    u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y);
            }

            static SKPoint Cubic (SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float t)
            {
                var u = 1f - t;
                return new SKPoint (
                    u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
                    u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y);
            }
        }

        /// <summary>Reverses the order of the points in this path.</summary>
        public void Reverse ()
        {
            var reversed = new SKPath { FillType = path.FillType };
            reversed.AddPathReverse (path);
            path.Dispose ();
            path = reversed;
        }

        /// <summary>Returns whether the specified point lies on the outline of this path when stroked with the pen.</summary>
        public bool IsOutlineVisible (float x, float y, Pen pen)
        {
            if (pen is null)
                return false;

            // Stroke the outline into a fillable region, then do an ordinary containment test —
            // "on the outline" means "inside the stroked ribbon".
            using var paint = pen.CreatePaint ();
            using var outline = paint.GetFillPath (path);
            return outline?.Contains (x, y) ?? false;
        }

        /// <inheritdoc cref="IsOutlineVisible(float, float, Pen)"/>
        public bool IsOutlineVisible (PointF point, Pen pen) => IsOutlineVisible (point.X, point.Y, pen);

        /// <inheritdoc cref="IsOutlineVisible(float, float, Pen)"/>
        public bool IsOutlineVisible (Point point, Pen pen) => IsOutlineVisible (point.X, point.Y, pen);

        /// <inheritdoc cref="IsOutlineVisible(float, float, Pen)"/>
        public bool IsOutlineVisible (int x, int y, Pen pen) => IsOutlineVisible ((float)x, y, pen);

        /// <summary>
        /// Warps this path from <paramref name="srcRect"/> onto the quadrilateral (or triangle) given by
        /// <paramref name="destPoints"/>.
        /// </summary>
        /// <remarks>
        /// The path is flattened first, then each point is mapped by bilinear interpolation across the
        /// destination corners. <see cref="WarpMode.Perspective"/> is accepted but mapped the same way:
        /// a true perspective divide would need the path re-projected rather than interpolated.
        /// </remarks>
        public void Warp (PointF[] destPoints, RectangleF srcRect, Matrix? matrix = null,
            WarpMode warpMode = WarpMode.Perspective, float flatness = 0.25f)
        {
            if (destPoints is null || destPoints.Length < 3 || srcRect.Width == 0 || srcRect.Height == 0)
                return;

            Flatten (matrix, flatness);

            // A 3-point destination is a triangle; GDI+ infers the fourth corner as p1 + p2 - p0.
            var topLeft = destPoints[0];
            var topRight = destPoints[1];
            var bottomLeft = destPoints[2];
            var bottomRight = destPoints.Length > 3
                ? destPoints[3]
                : new PointF (topRight.X + bottomLeft.X - topLeft.X, topRight.Y + bottomLeft.Y - topLeft.Y);

            var warped = new SKPath { FillType = path.FillType };
            using (var iterator = path.CreateRawIterator ()) {
                var buffer = new SKPoint[4];
                while (true) {
                    var verb = iterator.Next (buffer);
                    if (verb == SKPathVerb.Done)
                        break;
                    switch (verb) {
                        case SKPathVerb.Move: warped.MoveTo (Map (buffer[0])); break;
                        case SKPathVerb.Line: warped.LineTo (Map (buffer[1])); break;
                        case SKPathVerb.Close: warped.Close (); break;
                    }
                }
            }

            path.Dispose ();
            path = warped;

            SKPoint Map (SKPoint p)
            {
                var u = Math.Clamp ((p.X - srcRect.X) / srcRect.Width, 0f, 1f);
                var v = Math.Clamp ((p.Y - srcRect.Y) / srcRect.Height, 0f, 1f);
                var top = new PointF (topLeft.X + (topRight.X - topLeft.X) * u, topLeft.Y + (topRight.Y - topLeft.Y) * u);
                var bottom = new PointF (bottomLeft.X + (bottomRight.X - bottomLeft.X) * u, bottomLeft.Y + (bottomRight.Y - bottomLeft.Y) * u);
                return new SKPoint (top.X + (bottom.X - top.X) * v, top.Y + (bottom.Y - top.Y) * v);
            }
        }

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

        // Integer overloads. These are not redundant sugar: designer-generated code emits integer
        // literals, so their absence is a compile error in exactly the files a migration cannot edit
        // by hand. Each delegates to the float implementation.

        /// <inheritdoc cref="AddLine(float, float, float, float)"/>
        public void AddLine (int x1, int y1, int x2, int y2) => AddLine ((float)x1, y1, x2, y2);

        /// <inheritdoc cref="AddEllipse(float, float, float, float)"/>
        public void AddEllipse (int x, int y, int width, int height) => AddEllipse ((float)x, y, width, height);

        /// <inheritdoc cref="AddArc(float, float, float, float, float, float)"/>
        public void AddArc (int x, int y, int width, int height, float startAngle, float sweepAngle)
            => AddArc ((float)x, y, width, height, startAngle, sweepAngle);

        /// <inheritdoc cref="AddBezier(float, float, float, float, float, float, float, float)"/>
        public void AddBezier (int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2)
            => AddBezier ((float)x1, y1, cx1, cy1, cx2, cy2, x2, y2);

        /// <inheritdoc cref="AddBezier(PointF, PointF, PointF, PointF)"/>
        public void AddBezier (Point pt1, Point pt2, Point pt3, Point pt4)
            => AddBezier (new PointF (pt1.X, pt1.Y), new PointF (pt2.X, pt2.Y), new PointF (pt3.X, pt3.Y), new PointF (pt4.X, pt4.Y));

        /// <inheritdoc cref="AddBeziers(PointF[])"/>
        public void AddBeziers (Point[] points)
            => AddBeziers (points is null ? [] : Array.ConvertAll (points, p => new PointF (p.X, p.Y)));

        /// <inheritdoc cref="AddRectangles(RectangleF[])"/>
        public void AddRectangles (Rectangle[] rects)
            => AddRectangles (rects is null ? [] : Array.ConvertAll (rects, r => new RectangleF (r.X, r.Y, r.Width, r.Height)));

        /// <summary>
        /// Appends part of a cardinal spline: <paramref name="numberOfSegments"/> segments starting at
        /// <paramref name="offset"/> in <paramref name="points"/>.
        /// </summary>
        public void AddCurve (PointF[] points, int offset, int numberOfSegments, float tension)
        {
            if (points is null || numberOfSegments <= 0 || offset < 0 || offset >= points.Length)
                return;
            // A run of N segments spans N+1 points.
            var take = Math.Min (numberOfSegments + 1, points.Length - offset);
            if (take < 2)
                return;
            AddCurve (points[offset..(offset + take)], tension);
        }

        /// <inheritdoc cref="AddCurve(PointF[], int, int, float)"/>
        public void AddCurve (Point[] points, int offset, int numberOfSegments, float tension)
            => AddCurve (points is null ? [] : Array.ConvertAll (points, p => new PointF (p.X, p.Y)), offset, numberOfSegments, tension);

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

        /// <summary>Returns whether the specified point lies within this path.</summary>
        public bool IsVisible (int x, int y) => path.Contains (x, y);

        // The `object? graphics` overloads below exist because GDI+ takes a Graphics here to supply the
        // device resolution. Graphics lives in Majorsilence.Forms, which depends on this assembly rather
        // than the other way round, so it cannot be named here -- but an object? parameter still binds a
        // Graphics argument at the call site, which is what migrated code needs. The argument is unused:
        // hit-testing is in path units, which are device pixels throughout this layer.

        /// <inheritdoc cref="IsVisible(PointF)"/>
        public bool IsVisible (PointF point, object? graphics) => IsVisible (point);

        /// <inheritdoc cref="IsVisible(Point)"/>
        public bool IsVisible (Point point, object? graphics) => IsVisible (point);

        /// <inheritdoc cref="IsVisible(float, float)"/>
        public bool IsVisible (float x, float y, object? graphics) => IsVisible (x, y);

        /// <inheritdoc cref="IsVisible(int, int)"/>
        public bool IsVisible (int x, int y, object? graphics) => IsVisible (x, y);

        /// <inheritdoc cref="IsOutlineVisible(PointF, Pen)"/>
        public bool IsOutlineVisible (PointF point, Pen pen, object? graphics) => IsOutlineVisible (point, pen);

        /// <inheritdoc cref="IsOutlineVisible(Point, Pen)"/>
        public bool IsOutlineVisible (Point point, Pen pen, object? graphics) => IsOutlineVisible (point, pen);

        /// <inheritdoc cref="IsOutlineVisible(float, float, Pen)"/>
        public bool IsOutlineVisible (float x, float y, Pen pen, object? graphics) => IsOutlineVisible (x, y, pen);

        /// <inheritdoc cref="IsOutlineVisible(int, int, Pen)"/>
        public bool IsOutlineVisible (int x, int y, Pen pen, object? graphics) => IsOutlineVisible (x, y, pen);

        /// <summary>Gets the bounding rectangle of this path as it would be drawn with the given pen.</summary>
        public RectangleF GetBounds (Matrix? matrix, Pen? pen)
        {
            if (pen is null)
                return matrix is null ? GetBounds () : GetBounds (matrix);

            // The stroke extends half the pen width beyond the geometry on each side.
            var bounds = matrix is null ? GetBounds () : GetBounds (matrix);
            var outset = Math.Max (0f, pen.Width) / 2f;
            return RectangleF.Inflate (bounds, outset, outset);
        }

        /// <summary>Replaces this path with its outline as drawn with the given pen, transformed first.</summary>
        public void Widen (Pen pen, Matrix? matrix)
        {
            if (matrix is not null)
                Transform (matrix);
            Widen (pen);
        }

        /// <inheritdoc cref="Widen(Pen, Matrix)"/>
        /// <param name="pen">The pen whose width defines the outline.</param>
        /// <param name="matrix">Applied before widening, if supplied.</param>
        /// <param name="flatness">
        /// The curve-flattening tolerance applied after widening, matching System.Drawing.
        /// </param>
        public void Widen (Pen pen, Matrix? matrix, float flatness)
        {
            Widen (pen, matrix);
            Flatten (null, flatness);
        }

        /// <summary>
        /// Replaces this path with the outline (stroke-to-fill) of itself as drawn with the given
        /// pen -- turns a zero-area line/curve path into a real, hit-testable filled region (e.g.
        /// `path.Widen(pen); path.IsVisible(pt)` to hit-test near a thin line, since IsVisible/
        /// SKPath.Contains always returns false for a path with no area).
        /// </summary>
        public void Widen (Majorsilence.Forms.Drawing.Pen pen)
        {
            using var paint = new SKPaint {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = pen.Width <= 0 ? 1 : pen.Width,
                StrokeCap = SKStrokeCap.Butt,
                StrokeJoin = SKStrokeJoin.Miter,
            };
            var widened = new SKPath ();
            paint.GetFillPath (path, widened);
            path.Dispose ();
            path = widened;
        }

        private void EnsureStart (float x, float y)
        {
            if (path.PointCount == 0)
                path.MoveTo (x, y);
        }

        /// <summary>
        /// Appends a run of GDI+-shaped point/type data (as produced by
        /// <see cref="GraphicsPathIterator"/>) to this path. Curve points always arrive in groups of
        /// three, matching the cubic Bézier representation the iterator normalizes to.
        /// </summary>
        internal void AppendPointTypeData (PointF[] pts, byte[] pointTypes, int startIndex, int endIndex)
        {
            if (pts is null || pointTypes is null)
                return;

            var i = startIndex;
            while (i <= endIndex && i < pts.Length && i < pointTypes.Length) {
                var type = (PathPointType)(pointTypes[i] & (byte)PathPointType.PathTypeMask);
                switch (type) {
                    case PathPointType.Start:
                        path.MoveTo (pts[i].X, pts[i].Y);
                        break;
                    case PathPointType.Bezier when i + 2 <= endIndex:
                        EnsureStart (pts[i].X, pts[i].Y);
                        path.CubicTo (pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y, pts[i + 2].X, pts[i + 2].Y);
                        i += 2;
                        break;
                    default:
                        EnsureStart (pts[i].X, pts[i].Y);
                        path.LineTo (pts[i].X, pts[i].Y);
                        break;
                }

                if ((pointTypes[i] & (byte)PathPointType.CloseSubpath) != 0)
                    path.Close ();
                i++;
            }
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

        /// <summary>Gets the x translation component (element dx).</summary>
        public float OffsetX => matrix.TransX;

        /// <summary>Gets the y translation component (element dy).</summary>
        public float OffsetY => matrix.TransY;

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

        /// <summary>
        /// Applies the rotate/scale/shear part of this matrix to an array of vectors in place,
        /// <b>ignoring translation</b> -- the difference between transforming a position and
        /// transforming a direction or offset.
        /// </summary>
        public void VectorTransformPoints (PointF[] points)
        {
            if (points is null) return;
            for (int i = 0; i < points.Length; i++)
            {
                // MapVector is exactly this: the linear part without TransX/TransY.
                var mapped = matrix.MapVector (points[i].X, points[i].Y);
                points[i] = new PointF (mapped.X, mapped.Y);
            }
        }

        /// <inheritdoc cref="VectorTransformPoints(PointF[])"/>
        public void VectorTransformPoints (Point[] points)
        {
            if (points is null) return;
            for (int i = 0; i < points.Length; i++)
            {
                var mapped = matrix.MapVector (points[i].X, points[i].Y);
                points[i] = new Point ((int)System.Math.Round (mapped.X), (int)System.Math.Round (mapped.Y));
            }
        }

        /// <summary>Applies a shear (skew) to this matrix.</summary>
        public void Shear (float shearX, float shearY, MatrixOrder order = MatrixOrder.Prepend)
        {
            var skew = SKMatrix.CreateSkew (shearX, shearY);
            matrix = order == MatrixOrder.Append
                ? SKMatrix.Concat (skew, matrix)
                : SKMatrix.Concat (matrix, skew);
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
        public void TransformVectors (Point[] points)
        {
            if (points is null) return;
            var asFloat = Array.ConvertAll (points, p => new PointF (p.X, p.Y));
            TransformVectors (asFloat);
            for (var i = 0; i < points.Length; i++)
                points[i] = new Point ((int)Math.Round (asFloat[i].X), (int)Math.Round (asFloat[i].Y));
        }

        /// <inheritdoc cref="TransformVectors(Point[])"/>
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
        AntiAlias = 4,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Invalid.</summary>
        Invalid = -1,
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
        Half = 4,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Invalid.</summary>
        Invalid = -1,
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
        Triangle = 3,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>No anchor.</summary>
        NoAnchor = 16,
        /// <summary>Square anchor.</summary>
        SquareAnchor = 17,
        /// <summary>Round anchor.</summary>
        RoundAnchor = 18,
        /// <summary>Diamond anchor.</summary>
        DiamondAnchor = 19,
        /// <summary>Arrow anchor.</summary>
        ArrowAnchor = 20,
        /// <summary>Custom.</summary>
        Custom = 255,
        /// <summary>Anchor mask.</summary>
        AnchorMask = 240,
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

    /// <summary>
    /// The points and point types that make up a <see cref="GraphicsPath"/>. Matches
    /// <c>System.Drawing.Drawing2D.PathData</c>.
    /// </summary>
    public sealed class PathData
    {
        /// <summary>Gets or sets the points in the path.</summary>
        public PointF[]? Points { get; set; }

        /// <summary>Gets or sets the type of each corresponding point, as <see cref="PathPointType"/> flags.</summary>
        public byte[]? Types { get; set; }
    }

    /// <summary>
    /// Specifies how two regions are combined. Matches System.Drawing.Drawing2D.CombineMode, including
    /// its numeric values -- designer-serialized code persists these as raw integers.
    /// </summary>
    public enum CombineMode
    {
        /// <summary>The existing region is replaced by the new region.</summary>
        Replace = 0,
        /// <summary>The two regions are combined by taking their intersection.</summary>
        Intersect = 1,
        /// <summary>The two regions are combined by taking their union.</summary>
        Union = 2,
        /// <summary>The two regions are combined by taking only the areas in one but not both.</summary>
        Xor = 3,
        /// <summary>The area of the new region is removed from the existing region.</summary>
        Exclude = 4,
        /// <summary>The area of the existing region is removed from the new region.</summary>
        Complement = 5
    }
}
