using System;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Drawing2D
{
    /// <summary>Specifies the alignment of a Pen relative to the line it strokes. Matches System.Drawing.Drawing2D.PenAlignment.</summary>
    public enum PenAlignment
    {
        /// <summary>The pen is centered on the theoretical line.</summary>
        Center = 0,
        /// <summary>The pen is positioned inside the theoretical line.</summary>
        Inset = 1,
        /// <summary>The pen is positioned outside the theoretical line.</summary>
        Outset = 2,
        /// <summary>Specifies that the pen is positioned to the left of the theoretical line.</summary>
        Left = 3,
        /// <summary>Specifies that the pen is positioned to the right of the theoretical line.</summary>
        Right = 4
    }

    /// <summary>
    /// Specifies the type of a point in a <see cref="GraphicsPath"/>. Matches
    /// System.Drawing.Drawing2D.PathPointType.
    /// </summary>
    public enum PathPointType
    {
        /// <summary>The starting point of a figure.</summary>
        Start = 0,
        /// <summary>A line segment endpoint.</summary>
        Line = 1,
        /// <summary>A control point or endpoint of a cubic Bézier segment.</summary>
        Bezier = 3,
        /// <summary>Masks off the point-type bits from the flag bits.</summary>
        PathTypeMask = 0x07,
        /// <summary>The corresponding segment is dashed.</summary>
        DashMode = 0x10,
        /// <summary>The point is a path marker.</summary>
        PathMarker = 0x20,
        /// <summary>The point is the last point of a closed figure.</summary>
        CloseSubpath = 0x80,
        /// <summary>A cubic Bézier point (alias of <see cref="Bezier"/>).</summary>
        Bezier3 = 3
    }

    /// <summary>
    /// Represents the saved state of a nested drawing container, produced by
    /// <c>Graphics.BeginContainer</c> and consumed by <c>Graphics.EndContainer</c>. Cross-platform
    /// replacement for <c>System.Drawing.Drawing2D.GraphicsContainer</c>.
    /// </summary>
    /// <remarks>
    /// Like <see cref="GraphicsState"/>, this wraps a SkiaSharp canvas save-count so
    /// <c>EndContainer</c> can restore exactly the depth <c>BeginContainer</c> opened -- containers
    /// and <c>Save</c>/<c>Restore</c> share the one canvas stack rather than duplicating it.
    /// </remarks>
    public sealed class GraphicsContainer
    {
        internal int Count { get; }
        internal GraphicsContainer (int count) => Count = count;
    }

    /// <summary>
    /// Encapsulates a custom line cap built from a user-supplied outline. Cross-platform replacement
    /// for <c>System.Drawing.Drawing2D.CustomLineCap</c>.
    /// </summary>
    /// <remarks>
    /// SkiaSharp strokes with a fixed set of caps (butt / round / square) and has no equivalent of
    /// GDI+'s "stroke this arbitrary path at each line end". The cap's geometry and settings are
    /// therefore stored faithfully and round-trip through every property, and the pen's effective
    /// Skia cap is taken from <see cref="BaseCap"/> -- so a custom cap degrades to its declared base
    /// shape when drawn rather than being ignored outright. <see cref="FillPath"/>/
    /// <see cref="StrokePath"/> are retained so rendering code that wants to draw the cap itself can
    /// get at the geometry.
    /// </remarks>
    public class CustomLineCap : IDisposable, ICloneable
    {
        private LineCap strokeStartCap = LineCap.Flat;
        private LineCap strokeEndCap = LineCap.Flat;

        /// <summary>Initializes a new CustomLineCap from a fill outline and/or a stroke outline.</summary>
        public CustomLineCap (GraphicsPath? fillPath, GraphicsPath? strokePath)
            : this (fillPath, strokePath, LineCap.Flat, 0f) { }

        /// <summary>Initializes a new CustomLineCap with the specified base cap.</summary>
        public CustomLineCap (GraphicsPath? fillPath, GraphicsPath? strokePath, LineCap baseCap)
            : this (fillPath, strokePath, baseCap, 0f) { }

        /// <summary>Initializes a new CustomLineCap with the specified base cap and inset.</summary>
        public CustomLineCap (GraphicsPath? fillPath, GraphicsPath? strokePath, LineCap baseCap, float baseInset)
        {
            FillPath = fillPath;
            StrokePath = strokePath;
            BaseCap = baseCap;
            BaseInset = baseInset;
        }

        /// <summary>Gets the fill outline supplied at construction, if any.</summary>
        public GraphicsPath? FillPath { get; internal set; }

        /// <summary>Gets the stroke outline supplied at construction, if any.</summary>
        public GraphicsPath? StrokePath { get; internal set; }

        /// <summary>Gets or sets the cap shape used at the base of this custom cap.</summary>
        public LineCap BaseCap { get; set; }

        /// <summary>Gets or sets the distance between the cap and the line.</summary>
        public float BaseInset { get; set; }

        /// <summary>Gets or sets how lines making up this cap are joined.</summary>
        public LineJoin StrokeJoin { get; set; } = LineJoin.Miter;

        /// <summary>Gets or sets the amount by which the cap's width scales with the pen width.</summary>
        public float WidthScale { get; set; } = 1f;

        /// <summary>Sets the caps used at the start and end of the lines that make up this custom cap.</summary>
        public void SetStrokeCaps (LineCap startCap, LineCap endCap)
        {
            strokeStartCap = startCap;
            strokeEndCap = endCap;
        }

        /// <summary>Gets the caps used at the start and end of the lines that make up this custom cap.</summary>
        public void GetStrokeCaps (out LineCap startCap, out LineCap endCap)
        {
            startCap = strokeStartCap;
            endCap = strokeEndCap;
        }

        /// <summary>Creates a copy of this custom line cap.</summary>
        public virtual object Clone ()
        {
            var clone = new CustomLineCap (FillPath, StrokePath, BaseCap, BaseInset) {
                StrokeJoin = StrokeJoin,
                WidthScale = WidthScale,
            };
            clone.SetStrokeCaps (strokeStartCap, strokeEndCap);
            return clone;
        }

        /// <summary>
        /// Releases the resources used by this cap. The supplied paths are owned by the caller (GDI+
        /// copies them into the cap; here they are referenced), so they are dropped rather than
        /// disposed.
        /// </summary>
        public void Dispose ()
        {
            FillPath = null;
            StrokePath = null;
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>
    /// An arrowhead line cap whose size can be adjusted. Cross-platform replacement for
    /// <c>System.Drawing.Drawing2D.AdjustableArrowCap</c>.
    /// </summary>
    /// <remarks>
    /// The arrow outline is built as a real <see cref="GraphicsPath"/> (available via
    /// <see cref="CustomLineCap.FillPath"/>) so it can be drawn explicitly; see
    /// <see cref="CustomLineCap"/> for why SkiaSharp stroking itself cannot consume it.
    /// </remarks>
    public sealed class AdjustableArrowCap : CustomLineCap
    {
        private float width;
        private float height;

        /// <summary>Initializes a new filled arrow cap of the specified width and height.</summary>
        public AdjustableArrowCap (float width, float height) : this (width, height, true) { }

        /// <summary>Initializes a new arrow cap of the specified width and height.</summary>
        public AdjustableArrowCap (float width, float height, bool isFilled)
            : base (null, null, LineCap.Triangle, 0f)
        {
            this.width = width;
            this.height = height;
            Filled = isFilled;
            Rebuild ();
        }

        /// <summary>Gets or sets the width of the arrow cap.</summary>
        public float Width {
            get => width;
            set { width = value; Rebuild (); }
        }

        /// <summary>Gets or sets the height of the arrow cap.</summary>
        public float Height {
            get => height;
            set { height = value; Rebuild (); }
        }

        /// <summary>Gets or sets the number of units the arrow's midpoint is inset from its base.</summary>
        public float MiddleInset { get; set; }

        /// <summary>Gets or sets whether the arrow is filled.</summary>
        public bool Filled { get; set; }

        // The arrow points along -Y with its tip at the origin, matching GDI+'s cap-space convention
        // (the cap is drawn in a frame whose origin is the line end and whose +Y runs back down the
        // line), so the outline can be transformed straight onto a line end by a renderer.
        private void Rebuild ()
        {
            var path = new GraphicsPath ();
            path.AddPolygon (new[] {
                new PointF (0f, 0f),
                new PointF (-width / 2f, -height),
                new PointF (width / 2f, -height),
            });
            FillPath = path;
        }

        /// <inheritdoc/>
        public override object Clone () => new AdjustableArrowCap (width, height, Filled) {
            MiddleInset = MiddleInset,
            BaseCap = BaseCap,
            BaseInset = BaseInset,
            StrokeJoin = StrokeJoin,
            WidthScale = WidthScale,
        };
    }

    /// <summary>
    /// Walks the subpaths, segment types and points of a <see cref="GraphicsPath"/>. Cross-platform
    /// replacement for <c>System.Drawing.Drawing2D.GraphicsPathIterator</c>.
    /// </summary>
    /// <remarks>
    /// The path is flattened once at construction into the GDI+ point/type representation by walking
    /// SkiaSharp's raw path iterator. Quadratic and conic segments (which GDI+ has no equivalent for
    /// and which SkiaSharp emits for ovals, arcs and round-rects) are elevated to cubic Béziers, so
    /// every curve point reports <see cref="PathPointType.Bezier"/> exactly as GDI+ would.
    /// <see cref="NextMarker(out int, out int)"/> reflects that <see cref="GraphicsPath"/> has no
    /// marker API: with no markers set, GDI+ itself returns the whole path as a single section, which
    /// is what this does.
    /// </remarks>
    public sealed class GraphicsPathIterator : IDisposable
    {
        private readonly PointF[] points;
        private readonly byte[] types;
        private readonly List<(int Start, int End, bool Closed)> subpaths = new ();

        private int subpathCursor;
        private int markerCursor;
        private int typeCursor;
        private int currentSubpathEnd = -1;

        /// <summary>Initializes a new iterator over the specified path.</summary>
        public GraphicsPathIterator (GraphicsPath? path)
        {
            (points, types) = Flatten (path);

            for (var i = 0; i < types.Length; i++) {
                if ((types[i] & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Start) {
                    if (subpaths.Count > 0) {
                        var previous = subpaths[^1];
                        subpaths[^1] = (previous.Start, i - 1, IsClosed (i - 1));
                    }
                    subpaths.Add ((i, types.Length - 1, false));
                }
            }
            if (subpaths.Count > 0) {
                var last = subpaths[^1];
                subpaths[^1] = (last.Start, types.Length - 1, IsClosed (types.Length - 1));
            }

            bool IsClosed (int index)
                => index >= 0 && index < types.Length && (types[index] & (byte)PathPointType.CloseSubpath) != 0;
        }

        /// <summary>Gets the number of points in the path.</summary>
        public int Count => points.Length;

        /// <summary>Gets the number of subpaths (figures) in the path.</summary>
        public int SubpathCount => subpaths.Count;

        /// <summary>Rewinds this iterator to the beginning of the path.</summary>
        public void Rewind ()
        {
            subpathCursor = 0;
            markerCursor = 0;
            typeCursor = 0;
            currentSubpathEnd = -1;
        }

        /// <summary>
        /// Moves to the next subpath and reports its point range. Returns the number of points in
        /// that subpath, or 0 when there are no more.
        /// </summary>
        public int NextSubpath (out int startIndex, out int endIndex, out bool isClosed)
        {
            if (subpathCursor >= subpaths.Count) {
                startIndex = endIndex = 0;
                isClosed = false;
                return 0;
            }

            var (start, end, closed) = subpaths[subpathCursor++];
            startIndex = start;
            endIndex = end;
            isClosed = closed;
            typeCursor = start;
            currentSubpathEnd = end;
            return end - start + 1;
        }

        /// <summary>
        /// Moves to the next subpath, copying it into <paramref name="path"/>. Returns the number of
        /// points copied, or 0 when there are no more subpaths.
        /// </summary>
        public int NextSubpath (GraphicsPath path, out bool isClosed)
        {
            var count = NextSubpath (out var start, out var end, out isClosed);
            path?.Reset ();
            if (count > 0)
                path?.AppendPointTypeData (points, types, start, end);
            return count;
        }

        /// <summary>
        /// Moves to the next marker-delimited section and reports its point range.
        /// <see cref="GraphicsPath"/> exposes no marker API, so the whole path is reported once and
        /// subsequent calls return 0 -- the same result GDI+ gives for a path with no markers.
        /// </summary>
        public int NextMarker (out int startIndex, out int endIndex)
        {
            if (markerCursor > 0 || points.Length == 0) {
                startIndex = endIndex = 0;
                return 0;
            }

            markerCursor = 1;
            startIndex = 0;
            endIndex = points.Length - 1;
            return points.Length;
        }

        /// <summary>Moves to the next marker-delimited section, copying it into <paramref name="path"/>.</summary>
        public int NextMarker (GraphicsPath path)
        {
            var count = NextMarker (out var start, out var end);
            path?.Reset ();
            if (count > 0)
                path?.AppendPointTypeData (points, types, start, end);
            return count;
        }

        /// <summary>
        /// Moves to the next run of same-typed points within the current subpath and reports its
        /// range. Returns the number of points in the run, or 0 at the end of the subpath.
        /// </summary>
        public int NextPathType (out byte pathType, out int startIndex, out int endIndex)
        {
            pathType = 0;
            startIndex = endIndex = 0;

            if (currentSubpathEnd < 0 || typeCursor > currentSubpathEnd || typeCursor >= types.Length)
                return 0;

            // The first point of a figure is always Start; the run's type is the type of the
            // segments that follow it.
            var begin = typeCursor;
            var runType = (byte)(types[Math.Min (begin + 1, currentSubpathEnd)] & (byte)PathPointType.PathTypeMask);
            var index = begin + 1;
            while (index <= currentSubpathEnd &&
                   (types[index] & (byte)PathPointType.PathTypeMask) == runType)
                index++;

            pathType = runType;
            startIndex = begin;
            endIndex = index - 1;
            typeCursor = index;
            return endIndex - startIndex + 1;
        }

        /// <summary>Returns whether the path contains any curve (Bézier) segment.</summary>
        public bool HasCurve ()
        {
            foreach (var t in types) {
                if ((t & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier)
                    return true;
            }
            return false;
        }

        /// <summary>Copies every point and type of the path into the supplied arrays.</summary>
        public int Enumerate (ref PointF[] points, ref byte[] types)
            => CopyData (ref points, ref types, 0, this.points.Length - 1);

        /// <summary>Copies the points and types in the given index range into the supplied arrays.</summary>
        public int CopyData (ref PointF[] points, ref byte[] types, int startIndex, int endIndex)
        {
            if (points is null || types is null || points.Length != types.Length)
                throw new ArgumentException ("The points and types arrays must be non-null and the same length.");

            if (this.points.Length == 0 || startIndex < 0 || endIndex < startIndex || endIndex >= this.points.Length)
                return 0;

            var count = Math.Min (endIndex - startIndex + 1, points.Length);
            Array.Copy (this.points, startIndex, points, 0, count);
            Array.Copy (this.types, startIndex, types, 0, count);
            return count;
        }

        /// <summary>Releases the resources used by this iterator. No unmanaged state is held.</summary>
        public void Dispose () => GC.SuppressFinalize (this);

        // Walks the SKPath once, producing GDI+-shaped point/type arrays.
        private static (PointF[] Points, byte[] Types) Flatten (GraphicsPath? path)
        {
            var sk = path?.ToSKPath ();
            if (sk is null || sk.PointCount == 0)
                return (Array.Empty<PointF> (), Array.Empty<byte> ());

            var outPoints = new List<PointF> (sk.PointCount);
            var outTypes = new List<byte> (sk.PointCount);

            using var iterator = sk.CreateRawIterator ();
            var buffer = new SKPoint[4];

            while (true) {
                var verb = iterator.Next (buffer);
                if (verb == SKPathVerb.Done)
                    break;

                switch (verb) {
                    case SKPathVerb.Move:
                        Emit (buffer[0], PathPointType.Start);
                        break;

                    case SKPathVerb.Line:
                        Emit (buffer[1], PathPointType.Line);
                        break;

                    case SKPathVerb.Quad:
                        EmitQuadAsCubic (buffer[0], buffer[1], buffer[2]);
                        break;

                    case SKPathVerb.Conic: {
                        // Two quads reproduce a conic closely enough for hit-testing/enumeration,
                        // and each elevates losslessly to a cubic.
                        var quads = SKPath.ConvertConicToQuads (buffer[0], buffer[1], buffer[2], iterator.ConicWeight (), 1);
                        for (var i = 0; i + 2 < quads.Length; i += 2)
                            EmitQuadAsCubic (quads[i], quads[i + 1], quads[i + 2]);
                        break;
                    }

                    case SKPathVerb.Cubic:
                        Emit (buffer[1], PathPointType.Bezier);
                        Emit (buffer[2], PathPointType.Bezier);
                        Emit (buffer[3], PathPointType.Bezier);
                        break;

                    case SKPathVerb.Close:
                        if (outTypes.Count > 0)
                            outTypes[^1] |= (byte)PathPointType.CloseSubpath;
                        break;
                }
            }

            return (outPoints.ToArray (), outTypes.ToArray ());

            void Emit (SKPoint p, PathPointType type)
            {
                outPoints.Add (new PointF (p.X, p.Y));
                outTypes.Add ((byte)type);
            }

            void EmitQuadAsCubic (SKPoint p0, SKPoint p1, SKPoint p2)
            {
                var c1 = new SKPoint (p0.X + 2f / 3f * (p1.X - p0.X), p0.Y + 2f / 3f * (p1.Y - p0.Y));
                var c2 = new SKPoint (p2.X + 2f / 3f * (p1.X - p2.X), p2.Y + 2f / 3f * (p1.Y - p2.Y));
                Emit (c1, PathPointType.Bezier);
                Emit (c2, PathPointType.Bezier);
                Emit (p2, PathPointType.Bezier);
            }
        }
    }
}
