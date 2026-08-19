using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Describes the interior of a graphics shape. Cross-platform replacement for
    /// <c>System.Drawing.Region</c>, backed by a SkiaSharp <see cref="SKRegion"/>.
    /// </summary>
    public sealed partial class Region : IDisposable
    {
        // How far an "infinite" region extends in each direction. SKRegion is integer scanline-based and
        // has no infinite representation, so GDI+'s infinite region is modelled as a very large finite
        // rectangle -- large enough to swallow any real coordinate, small enough that unioning two of
        // them cannot overflow int.
        private const int InfiniteExtent = 1 << 28;

        private SKRegion region;

        /// <summary>Initializes a new infinite region.</summary>
        public Region ()
        {
            region = new SKRegion ();
            region.SetRect (new SKRectI (-InfiniteExtent, -InfiniteExtent, InfiniteExtent, InfiniteExtent));
        }

        /// <summary>Initializes a new region from the specified rectangle.</summary>
        public Region (RectangleF rect) : this (Rectangle.Round (rect)) { }

        /// <summary>Initializes a new region from the specified rectangle.</summary>
        public Region (Rectangle rect)
        {
            region = new SKRegion ();
            region.SetRect (new SKRectI (rect.Left, rect.Top, rect.Right, rect.Bottom));
        }

        /// <summary>Initializes a new region from the specified graphics path.</summary>
        public Region (GraphicsPath path)
        {
            Guard.ThrowIfNull (path);

            region = new SKRegion ();

            // Bounded clip for the same reason as Combine below: rasterizing against the whole coordinate
            // space costs orders of magnitude more than the shape warrants, and a docking library builds
            // one of these per drop guide per mouse move.
            //
            // NOT disposed: ToSKPath hands back the GraphicsPath's own SKPath rather than a copy, so
            // disposing it here frees a path the caller still owns -- which crashed inside Skia on the
            // next use of it (a native SIGSEGV mid-drag, since a drop guide's path is reused every move).
            var skPath = path.ToSKPath ();
            using var clip = new SKRegion ();
            clip.SetRect (BoundsClip (skPath));

            region.SetPath (skPath, clip);
        }

        private Region (SKRegion existing) => region = existing;

        internal SKRegion GetSKRegion () => region;

        /// <summary>Makes this region empty.</summary>
        public void MakeEmpty () => region.SetRect (SKRectI.Empty);

        /// <summary>Makes this region infinite.</summary>
        public void MakeInfinite () => region.SetRect (new SKRectI (-InfiniteExtent, -InfiniteExtent, InfiniteExtent, InfiniteExtent));

        /// <summary>Returns whether this region is empty on the given surface.</summary>
        public bool IsEmpty (object? graphics = null) => region.IsEmpty;

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (PointF point) => region.Contains ((int)point.X, (int)point.Y);

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (Point point) => region.Contains (point.X, point.Y);

        /// <summary>Returns whether any part of the specified rectangle is contained in this region.</summary>
        public bool IsVisible (RectangleF rect)
        {
            var r = Rectangle.Round (rect);
            using var test = new SKRegion ();
            test.SetRect (new SKRectI (r.Left, r.Top, r.Right, r.Bottom));
            return region.Intersects (test);
        }

        /// <summary>Returns whether any part of the specified rectangle is contained in this region.</summary>
        public bool IsVisible (Rectangle rect) => IsVisible ((RectangleF)rect);

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (float x, float y) => region.Contains ((int)x, (int)y);

        /// <summary>Returns whether the specified point is contained in this region.</summary>
        public bool IsVisible (int x, int y) => region.Contains (x, y);

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
            using (var iterator = region.CreateRectIterator ()) {
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
        public void Translate (int dx, int dy) => region.Translate (dx, dy);

        /// <summary>
        /// Offsets this region by the specified amounts. <see cref="SKRegion"/> is integer-based (it
        /// stores scanlines, not geometry), so fractional offsets are rounded, matching how the
        /// rectangle-based constructors already round.
        /// </summary>
        public void Translate (float dx, float dy) => region.Translate ((int)Math.Round (dx), (int)Math.Round (dy));

        /// <summary>
        /// Transforms this region by the specified matrix. Applied by round-tripping through the
        /// region's boundary path, since a region stores scanlines rather than geometry and so cannot
        /// be transformed in place.
        /// </summary>
        public void Transform (Matrix matrix)
        {
            Guard.ThrowIfNull (matrix);

            using var path = region.GetBoundaryPath ();
            using var transformed = new SKPath (path);
            transformed.Transform (matrix.ToSKMatrix ());
            region.SetPath (transformed);
        }

        /// <summary>
        /// Returns whether this region covers an infinite area -- i.e. it is still the region a
        /// parameterless <see cref="Region"/> (or <see cref="MakeInfinite"/>) produces.
        /// </summary>
        public bool IsInfinite (object? graphics = null)
        {
            var b = region.Bounds;
            return b.Left <= -InfiniteExtent && b.Top <= -InfiniteExtent
                && b.Right >= InfiniteExtent && b.Bottom >= InfiniteExtent;
        }

        private void Combine (RectangleF rect, SKRegionOperation op)
        {
            var r = Rectangle.Round (rect);
            region.Op (new SKRectI (r.Left, r.Top, r.Right, r.Bottom), op);
        }

        private void Combine (Region other, SKRegionOperation op)
        {
            Guard.ThrowIfNull (other);
            region.Op (other.region, op);
        }

        private void Combine (GraphicsPath path, SKRegionOperation op)
        {
            Guard.ThrowIfNull (path);

            // SKRegion.Op(SKPath, ...) rasterizes the path against the region's own bounds, which for a
            // freshly-constructed (infinite) region is the whole coordinate space -- so go through an
            // explicit region built from the path instead, which is well-defined in every case.
            // Not disposed -- ToSKPath returns the GraphicsPath's own path, not a copy (see the ctor).
            var skPath = path.ToSKPath ();
            using var other = new SKRegion ();
            using var clip = new SKRegion ();

            // Clipped to the PATH'S OWN BOUNDS, not the infinite extent. SetPath is scanline-based, so its
            // cost follows the clip it is handed: against ±2^28 it walks half a billion rows to rasterize
            // a shape a hundred pixels across. A path cannot cover anything outside its own bounds, so the
            // result is identical and the work becomes proportional to the shape.
            //
            // This is not a micro-optimisation. A docking library rebuilds its drop-guide region on every
            // mouse move, and each of those cost tens to hundreds of milliseconds -- enough that macOS
            // dropped most of the drag's pointer events (8 of 90 arrived in one measurement) and the drag
            // appeared to hang until the mouse stopped.
            clip.SetRect (BoundsClip (skPath));

            other.SetPath (skPath, clip);
            region.Op (other, op);
        }

        // The path's bounds, rounded outwards and inflated a pixel so rounding cannot shave an edge.
        private static SKRectI BoundsClip (SKPath path)
        {
            var b = path.Bounds;

            return new SKRectI (
                (int)Math.Floor (b.Left) - 1, (int)Math.Floor (b.Top) - 1,
                (int)Math.Ceiling (b.Right) + 1, (int)Math.Ceiling (b.Bottom) + 1);
        }

        /// <summary>Gets the bounds of this region.</summary>
        public RectangleF GetBounds (object? graphics = null)
        {
            var b = region.Bounds;
            return new RectangleF (b.Left, b.Top, b.Width, b.Height);
        }

        /// <summary>Creates an exact copy of this region.</summary>
        public Region Clone () => new Region (new SKRegion (region));

        /// <inheritdoc/>
        public void Dispose ()
        {
            region?.Dispose ();
            region = null!;
        }
    }
}
