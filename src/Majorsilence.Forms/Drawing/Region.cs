using System;
using System.Drawing;
using Majorsilence.Drawing.Drawing2D;
using SkiaSharp;

namespace Majorsilence.Drawing
{
    /// <summary>
    /// Describes the interior of a graphics shape. Cross-platform replacement for
    /// <c>System.Drawing.Region</c>, backed by a SkiaSharp <see cref="SKRegion"/>.
    /// </summary>
    public sealed class Region : IDisposable
    {
        private SKRegion region;

        /// <summary>Initializes a new infinite region.</summary>
        public Region ()
        {
            region = new SKRegion ();
            region.SetRect (new SKRectI (-(1 << 28), -(1 << 28), 1 << 28, 1 << 28));
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
            region = new SKRegion ();
            region.SetPath (path.ToSKPath ());
        }

        private Region (SKRegion existing) => region = existing;

        internal SKRegion GetSKRegion () => region;

        /// <summary>Makes this region empty.</summary>
        public void MakeEmpty () => region.SetRect (SKRectI.Empty);

        /// <summary>Makes this region infinite.</summary>
        public void MakeInfinite () => region.SetRect (new SKRectI (-(1 << 28), -(1 << 28), 1 << 28, 1 << 28));

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

        /// <summary>Updates this region to the union of itself and the specified rectangle.</summary>
        public void Union (RectangleF rect) => Combine (rect, SKRegionOperation.Union);

        /// <summary>Updates this region to the intersection of itself and the specified rectangle.</summary>
        public void Intersect (RectangleF rect) => Combine (rect, SKRegionOperation.Intersect);

        /// <summary>Updates this region to exclude the specified rectangle.</summary>
        public void Exclude (RectangleF rect) => Combine (rect, SKRegionOperation.Difference);

        private void Combine (RectangleF rect, SKRegionOperation op)
        {
            var r = Rectangle.Round (rect);
            region.Op (new SKRectI (r.Left, r.Top, r.Right, r.Bottom), op);
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
