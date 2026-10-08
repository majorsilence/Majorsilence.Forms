using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Describes the interior of a graphics shape. Cross-platform replacement for
    /// <c>System.Drawing.Region</c>: the shape is kept in float precision as an <see cref="SKPath"/>,
    /// and an integer <see cref="SKRegion"/> is derived from it only where pixels are needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GFX-44: GDI+ regions are float-precision throughout -- <c>new Region (RectangleF)</c> is
    /// <c>GdipCreateRegionRect</c>, <c>Translate (float, float)</c> is <c>GdipTranslateRegion</c> and
    /// <c>Translate (int, int)</c> just forwards to it (dotnet/winforms
    /// src/System.Drawing.Common/src/System/Drawing/Region.cs). This used to be an <c>SKRegion</c>,
    /// which stores integer scanlines, so every fractional rectangle and offset was rounded: a region
    /// translated by 0.4 twice did not move, and a half-pixel region was empty.
    /// </para>
    /// <para>
    /// Three representations, one of them authoritative at a time. A region built only from integer
    /// rectangles and integer offsets stays an <c>SKRegion</c> -- exact, and far cheaper to combine
    /// than path geometry, which is what a region of hundreds of invalidated rectangles needs. The
    /// first fractional or path-shaped operand turns it into an <c>SKPath</c>, combined with
    /// <c>SKPath.Op</c> from then on. And "infinite" is a flag, as GDI+'s infinite region is a node of
    /// its own (<c>GdipIsInfiniteRegion</c>) rather than a large rectangle: it is only materialised as
    /// a ±2^28 rectangle when an operation needs finite geometry (infinite minus something).
    /// </para>
    /// <para>
    /// Pixels: hit testing (<see cref="IsVisible(PointF)"/> and friends), <see cref="GetRegionScans"/>
    /// and window shaping use the derived integer region (a pixel is inside when its centre is).
    /// Clipping and filling through <c>Graphics</c> use the path, without antialiasing, which keeps
    /// GDI+'s hard region edges.
    /// </para>
    /// </remarks>
    public sealed partial class Region : IDisposable
    {
        // How far an infinite region extends when it has to be made finite -- large enough to swallow any
        // real coordinate, small enough that combining two of them cannot overflow int, and exactly
        // representable as a float.
        private const int InfiniteExtent = 1 << 28;

        private static readonly SKRectI infinite_rect = new (-InfiniteExtent, -InfiniteExtent, InfiniteExtent, InfiniteExtent);

        // GDI+'s infinite region. When set, shape and pixels are unused.
        private bool infinite;

        // The float shape. Null while the region is still integral, in which case pixels is authoritative.
        private SKPath? shape;

        // The integer form: authoritative while shape is null, otherwise a cache derived from shape.
        private SKRegion? pixels;

        // The path form of an integral or infinite region, cached for clipping.
        private SKPath? outline;

        /// <summary>Initializes a new infinite region.</summary>
        public Region () => infinite = true;

        /// <summary>Initializes a new region from the specified rectangle.</summary>
        /// <remarks>Not rounded: a fractional rectangle keeps its edges (GFX-44).</remarks>
        public Region (RectangleF rect)
        {
            if (IsIntegral (rect))
                pixels = IntegralRegion (rect);
            else
                shape = RectanglePath (rect);
        }

        /// <summary>Initializes a new region from the specified rectangle.</summary>
        public Region (Rectangle rect)
        {
            pixels = new SKRegion ();
            pixels.SetRect (new SKRectI (rect.Left, rect.Top, rect.Right, rect.Bottom));
        }

        /// <summary>Initializes a new region from the specified graphics path.</summary>
        public Region (GraphicsPath path)
        {
            Guard.ThrowIfNull (path);

            // A copy: ToSKPath hands back the GraphicsPath's own SKPath, which the caller still owns and
            // may change or dispose (disposing it here once crashed inside Skia mid-drag). The fill mode
            // travels with it (GFX-41), so an even-odd path keeps its hole.
            //
            // Nothing is rasterised here. A docking library builds one of these per drop guide per mouse
            // move, so the scanline form is left until something actually needs pixels.
            shape = new SKPath (path.ToSKPath ());
        }

        /// <summary>
        /// The integer form, derived from the shape on first use and cached. The region owns it.
        /// </summary>
        internal SKRegion GetSKRegion ()
        {
            if (pixels is not null)
                return pixels;

            if (infinite) {
                pixels = new SKRegion ();
                pixels.SetRect (infinite_rect);
            } else {
                pixels = Rasterize (shape!, 0);
            }

            return pixels;
        }

        /// <summary>
        /// The float shape to clip or fill with: the path itself, or the outline of an integral or
        /// infinite region. The region owns it.
        /// </summary>
        internal SKPath GetSKPath ()
        {
            if (shape is not null)
                return shape;

            if (outline is null) {
                if (infinite) {
                    outline = new SKPath ();
                    outline.AddRect (infinite_rect);
                } else {
                    // Null for an empty region.
                    outline = pixels!.GetBoundaryPath () ?? new SKPath ();
                }
            }

            return outline;
        }

        /// <summary>Whether the shape has gone to float geometry (a path) rather than integer rectangles. Test seam.</summary>
        internal bool IsShaped => shape is not null;

        /// <summary>Makes this region empty.</summary>
        public void MakeEmpty ()
        {
            Clear ();
            pixels = new SKRegion ();
        }

        /// <summary>Makes this region infinite.</summary>
        public void MakeInfinite ()
        {
            Clear ();
            infinite = true;
        }

        /// <summary>Returns whether this region is empty on the given surface.</summary>
        /// <remarks>A shape with area is not empty however small: a half-pixel region is a region.</remarks>
        public bool IsEmpty (object? graphics = null)
        {
            if (infinite)
                return false;

            if (shape is null)
                return pixels!.IsEmpty;

            if (shape.IsEmpty)
                return true;

            // A path can enclose nothing and still have points (a lone line, a fully cancelled op);
            // simplifying resolves it to the area it actually covers.
            using var simple = shape.Simplify ();
            return simple is null || simple.IsEmpty || simple.TightBounds.Width <= 0 || simple.TightBounds.Height <= 0;
        }

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (PointF point) => IsVisible (point.X, point.Y);

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (Point point) => IsVisible (point.X, point.Y);

        /// <summary>Returns whether any part of the specified rectangle is contained in this region.</summary>
        public bool IsVisible (RectangleF rect)
        {
            var r = Rectangle.Round (rect);
            using var test = new SKRegion ();
            test.SetRect (new SKRectI (r.Left, r.Top, r.Right, r.Bottom));
            return GetSKRegion ().Intersects (test);
        }

        /// <summary>Returns whether any part of the specified rectangle is contained in this region.</summary>
        public bool IsVisible (Rectangle rect) => IsVisible ((RectangleF)rect);

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        /// <remarks>Tested against the pixel the point falls in (floored, so -0.5 is pixel -1).</remarks>
        public bool IsVisible (float x, float y)
            => infinite || GetSKRegion ().Contains ((int)Math.Floor (x), (int)Math.Floor (y));

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (int x, int y) => infinite || GetSKRegion ().Contains (x, y);

        /// <summary>Returns whether any part of the specified rectangle is contained in this region.</summary>
        public bool IsVisible (float x, float y, float width, float height)
            => IsVisible (new RectangleF (x, y, width, height));

        /// <summary>Returns whether any part of the specified rectangle is contained in this region.</summary>
        public bool IsVisible (int x, int y, int width, int height)
            => IsVisible (new RectangleF (x, y, width, height));

        // GDI+ takes a Graphics on these to supply the device resolution. Graphics lives in
        // Majorsilence.Forms, which depends on this assembly rather than the reverse, so it cannot be
        // named here -- but an object? parameter still binds a Graphics argument at the call site, which
        // is what migrated code needs. The argument is unused: regions are in device pixels throughout.

        /// <inheritdoc cref="IsVisible(PointF)"/>
        public bool IsVisible (PointF point, object? graphics) => IsVisible (point);

        /// <inheritdoc cref="IsVisible(Point)"/>
        public bool IsVisible (Point point, object? graphics) => IsVisible (point);

        /// <inheritdoc cref="IsVisible(RectangleF)"/>
        public bool IsVisible (RectangleF rect, object? graphics) => IsVisible (rect);

        /// <inheritdoc cref="IsVisible(Rectangle)"/>
        public bool IsVisible (Rectangle rect, object? graphics) => IsVisible (rect);

        /// <inheritdoc cref="IsVisible(float, float)"/>
        public bool IsVisible (float x, float y, object? graphics) => IsVisible (x, y);

        /// <inheritdoc cref="IsVisible(int, int)"/>
        public bool IsVisible (int x, int y, object? graphics) => IsVisible (x, y);

        /// <inheritdoc cref="IsVisible(float, float, float, float)"/>
        public bool IsVisible (float x, float y, float width, float height, object? graphics)
            => IsVisible (x, y, width, height);

        /// <inheritdoc cref="IsVisible(int, int, int, int)"/>
        public bool IsVisible (int x, int y, int width, int height, object? graphics)
            => IsVisible (x, y, width, height);

        /// <summary>
        /// Returns the rectangles that together make up this region — its scanline decomposition.
        /// </summary>
        /// <param name="matrix">Applied to each rectangle before it is returned, if supplied.</param>
        public RectangleF[] GetRegionScans (Drawing2D.Matrix? matrix)
        {
            var scans = new List<RectangleF> ();
            using (var iterator = GetSKRegion ().CreateRectIterator ()) {
                while (iterator.Next (out var rect))
                    scans.Add (new RectangleF (rect.Left, rect.Top, rect.Width, rect.Height));
            }

            if (matrix is not null) {
                var sk = matrix.ToSKMatrix ();
                for (var i = 0; i < scans.Count; i++) {
                    var mapped = sk.MapRect (new SKRect (scans[i].Left, scans[i].Top, scans[i].Right, scans[i].Bottom));
                    scans[i] = new RectangleF (mapped.Left, mapped.Top, mapped.Width, mapped.Height);
                }
            }

            return [.. scans];
        }

        /// <summary>Updates this region to the union of itself and the specified rectangle.</summary>
        public void Union (RectangleF rect) => Combine (rect, SKRegionOperation.Union);

        /// <summary>Updates this region to the union of itself and the specified rectangle.</summary>
        public void Union (Rectangle rect) => Combine (rect, SKRegionOperation.Union);

        /// <summary>Updates this region to the union of itself and the specified region.</summary>
        public void Union (Region region) => Combine (region, SKRegionOperation.Union);

        /// <summary>Updates this region to the union of itself and the interior of the specified path.</summary>
        public void Union (GraphicsPath path) => Combine (path, SKRegionOperation.Union);

        /// <summary>Updates this region to the intersection of itself and the specified rectangle.</summary>
        public void Intersect (RectangleF rect) => Combine (rect, SKRegionOperation.Intersect);

        /// <summary>Updates this region to the intersection of itself and the specified rectangle.</summary>
        public void Intersect (Rectangle rect) => Combine (rect, SKRegionOperation.Intersect);

        /// <summary>Updates this region to the intersection of itself and the specified region.</summary>
        public void Intersect (Region region) => Combine (region, SKRegionOperation.Intersect);

        /// <summary>Updates this region to the intersection of itself and the interior of the specified path.</summary>
        public void Intersect (GraphicsPath path) => Combine (path, SKRegionOperation.Intersect);

        /// <summary>Updates this region to exclude the specified rectangle.</summary>
        public void Exclude (RectangleF rect) => Combine (rect, SKRegionOperation.Difference);

        /// <summary>Updates this region to exclude the specified rectangle.</summary>
        public void Exclude (Rectangle rect) => Combine (rect, SKRegionOperation.Difference);

        /// <summary>Updates this region to exclude the specified region.</summary>
        public void Exclude (Region region) => Combine (region, SKRegionOperation.Difference);

        /// <summary>Updates this region to exclude the interior of the specified path.</summary>
        public void Exclude (GraphicsPath path) => Combine (path, SKRegionOperation.Difference);

        /// <summary>Updates this region to the union minus the intersection with the specified rectangle.</summary>
        public void Xor (RectangleF rect) => Combine (rect, SKRegionOperation.XOR);

        /// <summary>Updates this region to the union minus the intersection with the specified rectangle.</summary>
        public void Xor (Rectangle rect) => Combine (rect, SKRegionOperation.XOR);

        /// <summary>Updates this region to the union minus the intersection with the specified region.</summary>
        public void Xor (Region region) => Combine (region, SKRegionOperation.XOR);

        /// <summary>Updates this region to the union minus the intersection with the specified path's interior.</summary>
        public void Xor (GraphicsPath path) => Combine (path, SKRegionOperation.XOR);

        /// <summary>Updates this region to the portion of the specified rectangle NOT in this region.</summary>
        public void Complement (RectangleF rect) => Combine (rect, SKRegionOperation.ReverseDifference);

        /// <summary>Updates this region to the portion of the specified rectangle NOT in this region.</summary>
        public void Complement (Rectangle rect) => Combine (rect, SKRegionOperation.ReverseDifference);

        /// <summary>Updates this region to the portion of the specified region NOT in this region.</summary>
        public void Complement (Region region) => Combine (region, SKRegionOperation.ReverseDifference);

        /// <summary>Updates this region to the portion of the specified path's interior NOT in this region.</summary>
        public void Complement (GraphicsPath path) => Combine (path, SKRegionOperation.ReverseDifference);

        /// <summary>Offsets this region by the specified amounts.</summary>
        public void Translate (int dx, int dy)
        {
            if (infinite)
                return;

            if (shape is not null) {
                shape.Offset (dx, dy);
                DropDerived ();
            } else {
                pixels!.Translate (dx, dy);
                DropOutline ();
            }
        }

        /// <summary>Offsets this region by the specified amounts.</summary>
        /// <remarks>
        /// Exact (GFX-44): it used to round each offset to a whole pixel, so a region translated by 0.4
        /// twice stayed put where GDI+'s moves 0.8, and a smooth-scrolled clip walked.
        /// </remarks>
        public void Translate (float dx, float dy)
        {
            if (IsWhole (dx) && IsWhole (dy)) {
                Translate ((int)dx, (int)dy);
                return;
            }

            if (infinite)
                return;

            ToShape ().Offset (dx, dy);
            DropDerived ();
        }

        /// <summary>Transforms this region by the specified matrix.</summary>
        /// <remarks>
        /// The geometry is transformed exactly; an integral region only goes to a path when the matrix is
        /// more than a whole-pixel translation. An infinite or empty region is left as it is (GDI+ leaves
        /// an infinite region infinite under any transform). GFX-45's concern -- rasterising a
        /// transformed path against the whole coordinate space -- is gone with the eager rasterisation:
        /// pixels are derived later, against the shape's own bounds.
        /// </remarks>
        public void Transform (Matrix matrix)
        {
            Guard.ThrowIfNull (matrix);

            if (infinite || IsEmpty ())
                return;

            var m = matrix.ToSKMatrix ();

            if (m.ScaleX == 1 && m.ScaleY == 1 && m.SkewX == 0 && m.SkewY == 0 && m.Persp0 == 0 && m.Persp1 == 0 && m.Persp2 == 1
                && IsWhole (m.TransX) && IsWhole (m.TransY)) {
                Translate ((int)m.TransX, (int)m.TransY);
                return;
            }

            ToShape ().Transform (m);
            DropDerived ();
        }

        /// <summary>
        /// Returns whether this region covers an infinite area: it was made by the parameterless
        /// constructor or <see cref="MakeInfinite"/>, and nothing since has bounded it.
        /// </summary>
        /// <remarks>
        /// A real flag (GFX-44), as GDI+ keeps one; it used to be inferred from the bounds reaching ±2^28.
        /// </remarks>
        public bool IsInfinite (object? graphics = null) => infinite;

        private void Combine (RectangleF rect, SKRegionOperation op)
        {
            using var other = new Region (rect);
            Combine (other, op);
        }

        private void Combine (GraphicsPath path, SKRegionOperation op)
        {
            Guard.ThrowIfNull (path);

            using var other = new Region (path);
            Combine (other, op);
        }

        private void Combine (Region other, SKRegionOperation op)
        {
            Guard.ThrowIfNull (other);

            // The infinite cases are algebra, not geometry: only "infinite minus something" (and Xor,
            // which is the same thing here) needs the infinite region made finite.
            if (infinite || other.infinite) {
                switch (op) {
                case SKRegionOperation.Union:
                    if (!infinite)
                        MakeInfinite ();
                    return;
                case SKRegionOperation.Intersect:
                    if (infinite)
                        CopyFrom (other);
                    return;
                case SKRegionOperation.Difference when other.infinite:
                case SKRegionOperation.ReverseDifference when infinite:
                case SKRegionOperation.XOR when infinite && other.infinite:
                    MakeEmpty ();
                    return;
                }
            }

            // The fast path: two integral regions combine as scanlines, exactly, without path geometry.
            if (shape is null && other.shape is null) {
                var mine = TakePixels ();
                mine.Op (other.GetSKRegion (), op);
                SetPixels (mine);
                return;
            }

            var result = new SKPath ();
            if (GetSKPath ().Op (other.GetSKPath (), ToPathOp (op), result)) {
                SetShape (result);
                return;
            }

            // SKPath.Op gives up on degenerate geometry; the integer answer beats no answer.
            result.Dispose ();
            var fallback = new SKRegion (GetSKRegion ());
            fallback.Op (other.GetSKRegion (), op);
            SetPixels (fallback);
        }

        private static SKPathOp ToPathOp (SKRegionOperation op) => op switch {
            SKRegionOperation.Union => SKPathOp.Union,
            SKRegionOperation.Intersect => SKPathOp.Intersect,
            SKRegionOperation.Difference => SKPathOp.Difference,
            SKRegionOperation.XOR => SKPathOp.Xor,
            _ => SKPathOp.ReverseDifference,
        };

        /// <summary>Gets the bounds of this region.</summary>
        /// <remarks>
        /// The shape's exact bounds, fractional where the shape is. An infinite region reports the ±2^28
        /// rectangle it would be made finite as.
        /// </remarks>
        public RectangleF GetBounds (object? graphics = null)
        {
            if (infinite)
                return new RectangleF (infinite_rect.Left, infinite_rect.Top, infinite_rect.Width, infinite_rect.Height);

            if (shape is not null) {
                if (IsEmpty ())
                    return RectangleF.Empty;

                var t = shape.TightBounds;
                return new RectangleF (t.Left, t.Top, t.Width, t.Height);
            }

            var b = pixels!.Bounds;
            return new RectangleF (b.Left, b.Top, b.Width, b.Height);
        }

        /// <summary>Creates an exact copy of this region.</summary>
        public Region Clone ()
        {
            var copy = new Region ();
            copy.CopyFrom (this);
            return copy;
        }

        /// <inheritdoc/>
        public void Dispose () => Clear ();

        private void CopyFrom (Region other)
        {
            if (ReferenceEquals (this, other))
                return;

            Clear ();
            infinite = other.infinite;

            if (other.shape is not null)
                shape = new SKPath (other.shape);
            else if (!other.infinite)
                pixels = new SKRegion (other.pixels!);
        }

        private void Clear ()
        {
            infinite = false;
            shape?.Dispose ();
            shape = null;
            pixels?.Dispose ();
            pixels = null;
            DropOutline ();
        }

        private void DropOutline ()
        {
            outline?.Dispose ();
            outline = null;
        }

        // After the shape changes: the cached scanlines no longer describe it.
        private void DropDerived ()
        {
            pixels?.Dispose ();
            pixels = null;
        }

        private void SetPixels (SKRegion region)
        {
            if (ReferenceEquals (region, pixels)) {
                DropOutline ();
                return;
            }

            Clear ();
            pixels = region;
        }

        private void SetShape (SKPath path)
        {
            Clear ();
            shape = path;
        }

        // The integral form, detached for in-place combination (an infinite region made finite).
        private SKRegion TakePixels ()
        {
            if (infinite) {
                var finite = new SKRegion ();
                finite.SetRect (infinite_rect);
                return finite;
            }

            return pixels!;
        }

        // Moves an integral (or infinite) region over to float geometry, returning the path to edit.
        private SKPath ToShape ()
        {
            if (shape is not null)
                return shape;

            var path = new SKPath (GetSKPath ());
            SetShape (path);
            return path;
        }

        private static bool IsWhole (float value) => value == Math.Floor (value) && Math.Abs (value) <= InfiniteExtent;

        private static bool IsIntegral (RectangleF rect)
            => IsWhole (rect.X) && IsWhole (rect.Y) && IsWhole (rect.Width) && IsWhole (rect.Height);

        private static SKRegion IntegralRegion (RectangleF rect)
        {
            var region = new SKRegion ();
            region.SetRect (new SKRectI ((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom));
            return region;
        }

        private static SKPath RectanglePath (RectangleF rect)
        {
            var path = new SKPath ();

            // GDI+ treats a rectangle with no positive extent as empty, whichever way round it is given;
            // SKPath would wind it backwards and give it area.
            if (rect.Width > 0 && rect.Height > 0)
                path.AddRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));

            return path;
        }

        // Scanline conversion is linear in the rows it walks, so a shape is rasterised against its own
        // bounds -- never the whole coordinate space. A shape that reaches the ±2^28 infinite extent (one
        // made from "infinite minus something") would still be half a billion rows, so it is turned
        // inside out: the bounding rectangle, minus the rasterised part of it the shape does not cover,
        // which is the small part.
        private static SKRegion Rasterize (SKPath path, int depth)
        {
            var result = new SKRegion ();

            if (path.IsEmpty)
                return result;

            var bounds = path.Bounds;

            if (IsModest (bounds) || depth >= 2) {
                var clipRect = IsModest (bounds) ? BoundsClip (bounds) : SKRectI.Intersect (BoundsClip (bounds), last_resort_clip);
                using var clip = new SKRegion ();
                clip.SetRect (clipRect);
                t_rasterised_rows += clipRect.Height;
                result.SetPath (path, clip);
                return result;
            }

            var outer = BoundsClip (bounds);
            using var outerPath = new SKPath ();
            outerPath.AddRect (outer);

            using var uncovered = outerPath.Op (path, SKPathOp.Difference);
            result.SetRect (outer);

            if (uncovered is not null && !uncovered.IsEmpty) {
                using var hole = Rasterize (uncovered, depth + 1);
                result.Op (hole, SKRegionOperation.Difference);
            }

            return result;
        }

        /// <summary>Scanline rows handed to the rasteriser on this thread. Test seam: the cost the inside-out trick bounds.</summary>
        internal static long RasterisedRows => t_rasterised_rows;

        [ThreadStatic]
        private static long t_rasterised_rows;

        // Only reached for geometry that defeats the inside-out trick twice over; bounded so it cannot hang.
        private static readonly SKRectI last_resort_clip = new (-(1 << 20), -(1 << 20), 1 << 20, 1 << 20);

        private static bool IsModest (SKRect b)
            => b.Height <= 1 << 20 && b.Width <= 1 << 24
            && Math.Abs (b.Left) <= 1 << 24 && Math.Abs (b.Right) <= 1 << 24
            && Math.Abs (b.Top) <= 1 << 24 && Math.Abs (b.Bottom) <= 1 << 24;

        // The bounds, rounded outwards and inflated a pixel so rounding cannot shave an edge; clamped to
        // the infinite extent so a shape reaching it stays in int range.
        private static SKRectI BoundsClip (SKRect b)
        {
            static int Clamp (double v) => (int)Math.Max (-InfiniteExtent, Math.Min (InfiniteExtent, v));

            return new SKRectI (
                Clamp (Math.Floor (b.Left) - 1), Clamp (Math.Floor (b.Top) - 1),
                Clamp (Math.Ceiling (b.Right) + 1), Clamp (Math.Ceiling (b.Bottom) + 1));
        }
    }
}
