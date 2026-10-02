using System.Drawing;
using System.Runtime.Versioning;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A collection of extension methods to facilitate working with Skia.
    /// </summary>
    public static class SkiaExtensions
    {
        private static readonly SKColorFilter disabled_matrix = SKColorFilter.CreateColorMatrix (
                [
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0,     0,     0,     1, 0
                ]);
        private static readonly float[] focus_dash_intervals = [1f, 1f];

        /// <summary>
        /// Clips a canvas to the specified rectangle.
        /// </summary>
        public static void Clip (this SKCanvas canvas, Rectangle rectangle) => canvas.ClipRect (rectangle.ToSKRect ());

        /// <summary>
        /// Draws a control's background.
        /// </summary>
        public static void DrawBackground (this SKCanvas canvas, Rectangle bounds, ControlStyle style)
            => canvas.DrawBackground (bounds, style, style.GetBackgroundColor ());

        /// <summary>
        /// Draws a control's background with an explicitly resolved color (e.g. the WinForms-style
        /// ambient background inherited from the parent control); border geometry still comes from
        /// the style.
        /// </summary>
        public static void DrawBackground (this SKCanvas canvas, Rectangle bounds, ControlStyle style, SKColor backgroundColor)
        {
            var radii = CornerRadii.Of (style.Border);

            if (style.GetBoxShadow () is { } shadow) {
                var (fx, fy, fw, fh) = ShadowFace (bounds.Width, bounds.Height, shadow);

                // Shadow first, the control's own face on top -- a positive offset (the only direction
                // alert-buddy's own design brief uses, "4px down and right") leaves the face flush with
                // the near corner and only the shadow's own sliver showing past its far edge, which is
                // what reads as a flat, hard, offset shadow with no blur (#285). The strip the offset
                // claims outside the face is left transparent, exactly like a Transparent control, so
                // whatever sits behind this one shows through it -- the space an app using box-shadow is
                // expected to have reserved for it.
                canvas.Clear (SKColors.Transparent);

                if (radii.Any) {
                    FillRoundedRectangle (canvas, fx + shadow.OffsetX, fy + shadow.OffsetY, fw, fh, shadow.Color, radii, 0);
                    FillRoundedRectangle (canvas, fx, fy, fw - style.Border.GetWidth (), fh - style.Border.GetWidth (), backgroundColor, radii, style.Border.GetWidth ());
                } else {
                    canvas.FillRectangle (fx + shadow.OffsetX, fy + shadow.OffsetY, fw, fh, shadow.Color);
                    canvas.FillRectangle (fx, fy, fw, fh, backgroundColor);
                }

                return;
            }

            if (radii.Any) {
                canvas.Clear (SKColors.Transparent);
                FillRoundedRectangle (canvas, 0, 0, bounds.Width - style.Border.GetWidth (), bounds.Height - style.Border.GetWidth (), backgroundColor, radii, style.Border.GetWidth ());
                return;
            }

            canvas.Clear (backgroundColor);
        }

        // Where box-shadow (#285) puts the control's own "face" once an offset has claimed a strip on
        // its near side: the face shrinks by |offset| and shifts away from the shadow so the two never
        // overlap incorrectly. A positive offset leaves the face flush with the top-left corner (the
        // shadow peeks out past its bottom-right edge); a negative one is the mirror image. Shared by
        // DrawBackground and DrawBorder so the border always lines up with the shrunk face.
        private static (int X, int Y, int Width, int Height) ShadowFace (int width, int height, ControlBoxShadow shadow)
            => (Math.Max (0, -shadow.OffsetX), Math.Max (0, -shadow.OffsetY),
                Math.Max (0, width - Math.Abs (shadow.OffsetX)), Math.Max (0, height - Math.Abs (shadow.OffsetY)));

        /// <summary>
        /// Draws a bitmap.
        /// </summary>
        public static void DrawBitmap (this SKCanvas canvas, SKBitmap bitmap, Rectangle rect, bool disabled = false)
        {
            using var paint = new SKPaint ();

            if (disabled)
                paint.ColorFilter = disabled_matrix;

            canvas.DrawBitmap (bitmap, rect.ToSKRect (), paint);
        }

        /// <summary>
        /// Draws a bitmap.
        /// </summary>
        public static void DrawBitmap (this SKCanvas canvas, SKBitmap bitmap, float x, float y, bool disabled = false)
        {
            using var paint = new SKPaint ();

            if (disabled)
                paint.ColorFilter = disabled_matrix;

            canvas.DrawBitmap (bitmap, x, y, paint);
        }

        /// <summary>
        /// Draws a control's border.
        /// </summary>
        public static void DrawBorder (this SKCanvas canvas, Rectangle bounds, ControlStyle style)
        {
            // If using border radius, currently all border sides are drawn, and all are the same color
            var radii = CornerRadii.Of (style.Border);
            var dashed = style.Border.GetLineStyle () == ControlBorderLineStyle.Dashed;

            // box-shadow (#285) shrinks the control's own face away from the full bounds (see
            // ShadowFace/DrawBackground); the border has to be drawn around that same smaller rect, or
            // it would ring the shadow as well as the face. Without a shadow this is (0, 0, bounds).
            var (fx, fy, fw, fh) = style.GetBoxShadow () is { } shadow ? ShadowFace (bounds.Width, bounds.Height, shadow) : (0, 0, bounds.Width, bounds.Height);

            if (radii.Any) {
                DrawRoundedRectangle (canvas, fx, fy, fw - style.Border.GetWidth (), fh - style.Border.GetWidth (), style.Border.GetColor (), radii, style.Border.GetWidth (), dashed);
                return;
            }

            // Left Border
            if (style.Border.Left.GetWidth () > 0) {
                var left_offset = fx + style.Border.Left.GetWidth () / 2f;
                DrawBorderLine (canvas, left_offset, fy, left_offset, fy + fh, style.Border.Left.GetColor (), style.Border.Left.GetWidth (), dashed);
            }

            // Right Border
            if (style.Border.Right.GetWidth () > 0) {
                var right_offset = fx + fw - style.Border.Right.GetWidth () / 2f;
                DrawBorderLine (canvas, right_offset, fy, right_offset, fy + fh, style.Border.Right.GetColor (), style.Border.Right.GetWidth (), dashed);
            }

            // Top Border
            if (style.Border.Top.GetWidth () > 0) {
                var top_offset = fy + style.Border.Top.GetWidth () / 2f;
                DrawBorderLine (canvas, fx, top_offset, fx + fw, top_offset, style.Border.Top.GetColor (), style.Border.Top.GetWidth (), dashed);
            }

            // Bottom Border
            if (style.Border.Bottom.GetWidth () > 0) {
                var bottom_offset = fy + fh - style.Border.Bottom.GetWidth () / 2f;
                DrawBorderLine (canvas, fx, bottom_offset, fx + fw, bottom_offset, style.Border.Bottom.GetColor (), style.Border.Bottom.GetWidth (), dashed);
            }
        }

        // Per-corner radii (#286). When all four are equal the old uniform helpers draw, so a theme that
        // never uses the new properties renders byte-for-byte as before.
        private readonly record struct CornerRadii (int TopLeft, int TopRight, int BottomRight, int BottomLeft)
        {
            public bool Any => TopLeft > 0 || TopRight > 0 || BottomRight > 0 || BottomLeft > 0;

            public bool Uniform => TopLeft == TopRight && TopRight == BottomRight && BottomRight == BottomLeft;

            public static CornerRadii Of (ControlBorderStyle border)
                => new (border.GetTopLeftRadius (), border.GetTopRightRadius (), border.GetBottomRightRadius (), border.GetBottomLeftRadius ());
        }

        // A dash and a gap each three border-widths long: the CSS 'dashed' look closely enough, and
        // proportional so a thick dashed border is not a row of hairlines.
        private static SKPathEffect DashEffect (float width)
        {
            var dash = Math.Max (3f, width * 3f);

            return SKPathEffect.CreateDash (new[] { dash, dash }, 0);
        }

        private static void DrawBorderLine (SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, int thickness, bool dashed)
        {
            if (!dashed) {
                canvas.DrawLine (x1, y1, x2, y2, color, thickness);
                return;
            }

            using var effect = DashEffect (thickness);
            using var paint = new SKPaint { Color = color, StrokeWidth = thickness, PathEffect = effect };

            canvas.DrawLine (x1, y1, x2, y2, paint);
        }

        private static SKRoundRect RoundRectOf (float x, float y, float width, float height, CornerRadii r)
        {
            var rect = new SKRoundRect ();

            rect.SetRectRadii (new SKRect (x, y, x + width, y + height), new[] {
                new SKPoint (r.TopLeft, r.TopLeft), new SKPoint (r.TopRight, r.TopRight),
                new SKPoint (r.BottomRight, r.BottomRight), new SKPoint (r.BottomLeft, r.BottomLeft)
            });

            return rect;
        }

        private static void DrawRoundedRectangle (SKCanvas canvas, int x, int y, int width, int height, SKColor color, CornerRadii radii, float strokeWidth, bool dashed)
        {
            if (radii.Uniform && !dashed) {
                canvas.DrawRoundedRectangle (x, y, width, height, color, radii.TopLeft, radii.TopLeft, strokeWidth);
                return;
            }

            // Same half-stroke inset as DrawRoundedRectangle, for the same reason.
            var half = strokeWidth * 0.5f;
            using var effect = dashed ? DashEffect (strokeWidth) : null;
            using var paint = new SKPaint { Color = color, IsStroke = true, IsAntialias = true, StrokeWidth = strokeWidth, PathEffect = effect };
            using var rect = RoundRectOf (x + half, y + half, Math.Max (0, width - strokeWidth), Math.Max (0, height - strokeWidth), radii);

            canvas.DrawRoundRect (rect, paint);
        }

        private static void FillRoundedRectangle (SKCanvas canvas, int x, int y, int width, int height, SKColor color, CornerRadii radii, float strokeWidth)
        {
            if (radii.Uniform) {
                canvas.FillRoundedRectangle (x, y, width, height, color, radii.TopLeft, radii.TopLeft, strokeWidth);
                return;
            }

            using var paint = new SKPaint { Color = color, IsStroke = false, IsAntialias = true };
            using var rect = RoundRectOf (x + .5f, y + .5f, width, height, radii);

            canvas.DrawRoundRect (rect, paint);
        }

        /// <summary>
        /// Draws an unfilled circle.
        /// </summary>
        public static void DrawCircle (this SKCanvas canvas, int x, int y, int radius, SKColor color, int strokeWidth = 1)
        {
            using var paint = new SKPaint { Color = color, IsStroke = true, StrokeWidth = strokeWidth, IsAntialias = true };

            canvas.DrawCircle (x, y, radius, paint);
        }

        /// <summary>
        /// Draws a focus rectangle.
        /// </summary>
        public static void DrawFocusRectangle (this SKCanvas canvas, int x, int y, int width, int height, int inset = 0)
        {
            // Draw a white rectangle
            canvas.DrawRectangle (x + inset, y + inset, width - (2 * inset) - 1, height - (2 * inset) - 1, SKColors.White);

            // Draw a black dashed rectangle on top of it
            var effect = SKPathEffect.CreateDash (focus_dash_intervals, 0);
            using var paint = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1, PathEffect = effect };

            canvas.DrawRect (x + inset, y + inset, width - (2 * inset) - 1, height - (2 * inset) - 1, paint);
        }

        /// <summary>
        /// Draws an focus rectangle.
        /// </summary>
        public static void DrawFocusRectangle (this SKCanvas canvas, Rectangle rectangle, int inset = 0)
            => DrawFocusRectangle (canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, inset);

        /// <summary>
        /// Draws a line.
        /// </summary>
        public static void DrawLine (this SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, int thickness = 1)
        {
            using var paint = new SKPaint { Color = color, StrokeWidth = thickness };

            canvas.DrawLine (x1, y1, x2, y2, paint);
        }

        /// <summary>
        /// Draws a path.
        /// </summary>
        public static void DrawPath (this SKCanvas canvas, SKPath path, SKColor color, int thickness = 1)
        {
            using var paint = new SKPaint { Color = color, StrokeWidth = thickness, IsStroke = true };

            canvas.DrawPath (path, paint);
        }

        /// <summary>
        /// Draws an unfilled rectangle.
        /// </summary>
        public static void DrawRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color, int strokeWidth = 1)
        {
            using var paint = new SKPaint { Color = color, IsStroke = true, StrokeWidth = strokeWidth };

            // Inset by half the stroke width so the stroke is fully inside the specified bounds.
            // In Skia's coordinate system, pixel (i,j) occupies [i,i+1)x[j,j+1), so a centered
            // stroke at an integer coordinate straddles a pixel edge and can bleed outside the
            // buffer at fractional DPI scales (e.g. 150%).
            var half = strokeWidth * 0.5f;

            // When the requested rectangle is thinner than the stroke width, subtracting the
            // stroke width from the dimensions can produce zero or negative sizes, which Skia
            // will not render. In those cases, approximate the rectangle as a line or point
            // so thin glyphs (e.g. text carets) still appear.
            if (width <= strokeWidth && height <= strokeWidth) {
                // Degenerate case: both dimensions are very small, render a single point.
                canvas.DrawPoint (x + half, y + half, paint);
            } else if (width <= strokeWidth) {
                // Very thin vertical rectangle: draw a vertical line centered in the bounds.
                canvas.DrawLine (x + half, y + half, x + half, y + height - half, paint);
            } else if (height <= strokeWidth) {
                // Very thin horizontal rectangle: draw a horizontal line centered in the bounds.
                canvas.DrawLine (x + half, y + half, x + width - half, y + half, paint);
            } else {
                // Normal rectangle: inset by the stroke width so the stroke stays inside bounds.
                canvas.DrawRect (x + half, y + half, width - strokeWidth, height - strokeWidth, paint);
            }
        }

        /// <summary>
        /// Draws an unfilled rectangle.
        /// </summary>
        public static void DrawRectangle (this SKCanvas canvas, Rectangle rectangle, SKColor color, int strokeWidth = 1)
            => DrawRectangle (canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, strokeWidth);

        /// <summary>
        /// Draws an unfilled rectangle with rounded corners.
        /// </summary>
        public static void DrawRoundedRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color, int rx = 3, int ry = 3, float strokeWidth = 1)
        {
            using var paint = new SKPaint {
                Color = color,
                IsStroke = true,
                IsAntialias = true,
                StrokeWidth = strokeWidth
            };

            // Inset by half the stroke width so the stroke is fully inside the specified bounds.
            // In Skia's coordinate system, pixel (i,j) occupies [i,i+1)x[j,j+1), so a centered
            // stroke at an integer coordinate straddles a pixel edge and can bleed outside the
            // buffer at fractional DPI scales (e.g. 150%).
            var half = strokeWidth * 0.5f;
            var adjustedWidth = Math.Max (0, width - strokeWidth);
            var adjustedHeight = Math.Max (0, height - strokeWidth);
            canvas.DrawRoundRect (x + half, y + half, adjustedWidth, adjustedHeight, rx, ry, paint);
        }

        /// <summary>
        /// Draws a filled circle.
        /// </summary>
        public static void FillCircle (this SKCanvas canvas, int x, int y, int radius, SKColor color)
        {
            using var paint = new SKPaint { Color = color, IsAntialias = true };

            canvas.DrawCircle (x, y, radius, paint);
        }

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        public static void FillRectangle (this SKCanvas canvas, Rectangle rectangle, SKColor color)
            => FillRectangle (canvas, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color);

        /// <summary>
        /// Draws a filled rectangle.
        /// </summary>
        public static void FillRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color)
        {
            using var paint = new SKPaint { Color = color };

            canvas.DrawRect (x, y, width, height, paint);
        }

        /// <summary>
        /// Draws a filled rectangle with rounded corners.
        /// </summary>
        public static void FillRoundedRectangle (this SKCanvas canvas, int x, int y, int width, int height, SKColor color, int rx = 3, int ry = 3, float strokeWidth = 1)
        {
            using var paint = new SKPaint {
                Color = color,
                IsStroke = false,
                IsAntialias = true,
                StrokeWidth = strokeWidth
            };
            var r = new SKRoundRect ();

            canvas.DrawRoundRect (x + .5f, y + .5f, width, height, rx, ry, paint);
        }

        /// <summary>
        /// Gets the size of the specified bitmap.
        /// </summary>
        public static Size GetSize (this SKBitmap bitmap) => new Size (bitmap.Width, bitmap.Height);

        /// <summary>
        /// Converts an SKImage to a cross-platform <see cref="Bitmap"/>.
        /// </summary>
        public static Bitmap ToBitmap (this SKImage skiaImage)
            => new Bitmap (SKBitmap.FromImage (skiaImage));

        /// <summary>
        /// Converts an SKBitmap to a cross-platform <see cref="Bitmap"/>.
        /// </summary>
        public static Bitmap ToBitmap (this SKBitmap skiaBitmap)
            => new Bitmap (skiaBitmap.Copy ());

        /// <summary>
        /// Converts an SKRect to a Rectangle.
        /// </summary>
        public static Rectangle ToRectangle (this SKRect rect) => new Rectangle ((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height);

        /// <summary>
        /// Converts an SKSize to a Size.
        /// </summary>
        public static Size ToSize (this SKSize size) => new Size ((int)size.Width, (int)size.Height);

        /// <summary>
        /// Converts a Rectangle to an SKRect.
        /// </summary>
        public static SKRect ToSKRect (this Rectangle rect) => new SKRect (rect.X, rect.Y, rect.Right, rect.Bottom);

        /// <summary>
        /// Converts a Size to an SKSize.
        /// </summary>
        public static SKSize ToSKSize (this Size size) => new SKSize (size.Width, size.Height);
    }
}
