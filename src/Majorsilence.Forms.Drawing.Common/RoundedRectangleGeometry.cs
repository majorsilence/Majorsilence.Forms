using System;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Drawing2D
{
    /// <summary>
    /// The one definition of what a corner radius means, shared by <see cref="GraphicsPath.AddRoundedRectangle (RectangleF, float)"/>
    /// and the <c>Graphics.FillRoundedRectangle</c> / <c>DrawRoundedRectangle</c> family, so a filled shape, its outline
    /// and a clip path built from the same numbers cannot disagree.
    /// </summary>
    /// <remarks>
    /// A radius that does not fit is not an error. Skia scales ALL four radii down by the one factor that stops the
    /// tightest side overlapping, which is the CSS rule and keeps the corners' proportions; clamping each corner alone
    /// would turn an over-large radius on one corner of a 40x20 rectangle into a lopsided shape. A negative or
    /// non-finite radius is a caller bug and throws, rather than being quietly read as square corners.
    /// </remarks>
    internal static class RoundedRectangleGeometry
    {
        /// <summary>Builds the rounded rectangle for one radius on every corner. A bad radius is reported as <c>radius</c>.</summary>
        public static SKRoundRect Create (RectangleF rect, float radius)
        {
            ValidateRadius (radius, nameof (radius));
            return Build (rect, radius, radius, radius, radius);
        }

        /// <summary>
        /// Builds the rounded rectangle for four circular radii, given clockwise from the top-left corner (the order
        /// CSS <c>border-radius</c> uses). A bad radius is reported under the name of the corner it was given for.
        /// </summary>
        public static SKRoundRect Create (RectangleF rect, float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            ValidateRadius (topLeft, nameof (topLeft));
            ValidateRadius (topRight, nameof (topRight));
            ValidateRadius (bottomRight, nameof (bottomRight));
            ValidateRadius (bottomLeft, nameof (bottomLeft));
            return Build (rect, topLeft, topRight, bottomRight, bottomLeft);
        }

        private static SKRoundRect Build (RectangleF rect, float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            var rounded = new SKRoundRect ();
            rounded.SetRectRadii (
                new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom),
                [
                    new SKPoint (topLeft, topLeft),
                    new SKPoint (topRight, topRight),
                    new SKPoint (bottomRight, bottomRight),
                    new SKPoint (bottomLeft, bottomLeft),
                ]);

            return rounded;
        }

        private static void ValidateRadius (float radius, string paramName)
        {
            // NaN fails every comparison, so it needs its own test; infinity would reach Skia and collapse the shape.
            if (float.IsNaN (radius) || float.IsInfinity (radius) || radius < 0f)
                throw new ArgumentOutOfRangeException (paramName, radius, "A corner radius must be a finite number that is zero or greater.");
        }
    }
}
