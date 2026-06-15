using System;
using System.Drawing;
using SkiaSharp;
using ContentAlignment = Modern.Forms.ContentAlignment;

namespace Modern.Drawing
{
    /// <summary>
    /// Represents an opaque snapshot of the drawing state of a <see cref="SkiaGraphics"/>.
    /// </summary>
    public readonly struct GraphicsState
    {
        internal int Count { get; }
        internal GraphicsState (int count) => Count = count;
    }

    /// <summary>
    /// A GDI+-style 2D drawing surface backed by a SkiaSharp <see cref="SKCanvas"/>. Lets rendering
    /// code written against System.Drawing.Graphics-style APIs draw cross-platform onto any Skia
    /// surface (an on-screen control's canvas, an off-screen bitmap, or a PDF page).
    /// </summary>
    public sealed class SkiaGraphics
    {
        private readonly SKCanvas canvas;

        /// <summary>Initializes a new instance wrapping the specified canvas.</summary>
        public SkiaGraphics (SKCanvas canvas)
        {
            this.canvas = canvas ?? throw new ArgumentNullException (nameof (canvas));
        }

        /// <summary>Gets the underlying SkiaSharp canvas.</summary>
        public SKCanvas Canvas => canvas;

        /// <summary>Gets or sets the horizontal resolution, in DPI, used for unit conversions.</summary>
        public float DpiX { get; set; } = 96f;

        /// <summary>Gets or sets the vertical resolution, in DPI, used for unit conversions.</summary>
        public float DpiY { get; set; } = 96f;

        // --- State / transforms ---

        /// <summary>Saves the current graphics state and returns a token to restore it.</summary>
        public GraphicsState Save () => new GraphicsState (canvas.Save ());

        /// <summary>Restores the graphics state to the specified saved token.</summary>
        public void Restore (GraphicsState state) => canvas.RestoreToCount (state.Count);

        /// <summary>Translates the coordinate system origin.</summary>
        public void TranslateTransform (float dx, float dy) => canvas.Translate (dx, dy);

        /// <summary>Scales the coordinate system.</summary>
        public void ScaleTransform (float sx, float sy) => canvas.Scale (sx, sy);

        /// <summary>Rotates the coordinate system by the specified angle, in degrees.</summary>
        public void RotateTransform (float angleDegrees) => canvas.RotateDegrees (angleDegrees);

        /// <summary>Resets the transform to the identity matrix.</summary>
        public void ResetTransform () => canvas.SetMatrix (SKMatrix.Identity);

        // --- Clipping ---

        /// <summary>Intersects the clip region with the specified rectangle.</summary>
        public void SetClip (RectangleF rect) => canvas.ClipRect (rect.ToSKRect (), SKClipOperation.Intersect, true);

        /// <summary>Intersects the clip region with the specified rectangle.</summary>
        public void IntersectClip (RectangleF rect) => canvas.ClipRect (rect.ToSKRect (), SKClipOperation.Intersect, true);

        // --- Fills ---

        /// <summary>Fills the interior of a rectangle.</summary>
        public void FillRectangle (Brush brush, RectangleF rect)
        {
            using var paint = brush.CreatePaint ();
            canvas.DrawRect (rect.ToSKRect (), paint);
        }

        /// <summary>Fills the interior of an ellipse defined by a bounding rectangle.</summary>
        public void FillEllipse (Brush brush, RectangleF rect)
        {
            using var paint = brush.CreatePaint ();
            canvas.DrawOval (rect.ToSKRect (), paint);
        }

        /// <summary>Fills the interior of a polygon defined by an array of points.</summary>
        public void FillPolygon (Brush brush, PointF[] points)
        {
            if (points is null || points.Length < 2)
                return;

            using var path = CreatePolygonPath (points, close: true);
            using var paint = brush.CreatePaint ();
            canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a pie section defined by a bounding rectangle and start/sweep angles.</summary>
        public void FillPie (Brush brush, RectangleF rect, float startAngle, float sweepAngle)
        {
            using var path = new SKPath ();
            path.MoveTo (rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            path.ArcTo (rect.ToSKRect (), startAngle, sweepAngle, false);
            path.Close ();

            using var paint = brush.CreatePaint ();
            canvas.DrawPath (path, paint);
        }

        // --- Strokes ---

        /// <summary>Draws the outline of a rectangle.</summary>
        public void DrawRectangle (Pen pen, RectangleF rect)
        {
            using var paint = pen.CreatePaint ();
            canvas.DrawRect (rect.ToSKRect (), paint);
        }

        /// <summary>Draws a line between two points.</summary>
        public void DrawLine (Pen pen, float x1, float y1, float x2, float y2)
        {
            using var paint = pen.CreatePaint ();
            canvas.DrawLine (x1, y1, x2, y2, paint);
        }

        /// <summary>Draws a series of connected line segments.</summary>
        public void DrawLines (Pen pen, PointF[] points)
        {
            if (points is null || points.Length < 2)
                return;

            using var path = CreatePolygonPath (points, close: false);
            using var paint = pen.CreatePaint ();
            canvas.DrawPath (path, paint);
        }

        /// <summary>Draws the outline of an ellipse defined by a bounding rectangle.</summary>
        public void DrawEllipse (Pen pen, RectangleF rect)
        {
            using var paint = pen.CreatePaint ();
            canvas.DrawOval (rect.ToSKRect (), paint);
        }

        /// <summary>Draws the outline of a polygon defined by an array of points.</summary>
        public void DrawPolygon (Pen pen, PointF[] points)
        {
            if (points is null || points.Length < 2)
                return;

            using var path = CreatePolygonPath (points, close: true);
            using var paint = pen.CreatePaint ();
            canvas.DrawPath (path, paint);
        }

        /// <summary>Draws the outline of a pie section.</summary>
        public void DrawPie (Pen pen, RectangleF rect, float startAngle, float sweepAngle)
        {
            using var path = new SKPath ();
            path.MoveTo (rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
            path.ArcTo (rect.ToSKRect (), startAngle, sweepAngle, false);
            path.Close ();

            using var paint = pen.CreatePaint ();
            canvas.DrawPath (path, paint);
        }

        // --- Images ---

        /// <summary>Draws a bitmap stretched to fill the specified rectangle.</summary>
        public void DrawImage (SKBitmap bitmap, RectangleF rect)
        {
            if (bitmap is null)
                return;

            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap (bitmap, rect.ToSKRect (), paint);
        }

        /// <summary>Draws a bitmap at the specified location at its native size.</summary>
        public void DrawImage (SKBitmap bitmap, float x, float y)
        {
            if (bitmap is null)
                return;

            canvas.DrawBitmap (bitmap, x, y);
        }

        // --- Text ---

        /// <summary>Draws a string at the specified point (top-left origin).</summary>
        public void DrawString (string text, Font font, Brush brush, PointF point)
        {
            if (string.IsNullOrEmpty (text))
                return;

            var sk_font = font.GetSKFont ();
            using var paint = brush.CreatePaint ();

            var metrics = sk_font.Metrics;
            var baseline = point.Y - metrics.Ascent;

            canvas.DrawText (text, point.X, baseline, sk_font, paint);
        }

        /// <summary>Draws a string within the specified rectangle using the given alignment.</summary>
        public void DrawString (string text, Font font, Brush brush, RectangleF rect, ContentAlignment alignment = ContentAlignment.TopLeft)
        {
            if (string.IsNullOrEmpty (text))
                return;

            var size = MeasureString (text, font);

            var x = alignment switch {
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => rect.Left + (rect.Width - size.Width) / 2f,
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => rect.Right - size.Width,
                _ => rect.Left
            };

            var y = alignment switch {
                ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => rect.Top + (rect.Height - size.Height) / 2f,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => rect.Bottom - size.Height,
                _ => rect.Top
            };

            DrawString (text, font, brush, new PointF (x, y));
        }

        /// <summary>Measures the size of the specified string when rendered with the given font.</summary>
        public SizeF MeasureString (string text, Font font)
        {
            if (string.IsNullOrEmpty (text))
                return SizeF.Empty;

            var sk_font = font.GetSKFont ();
            var measured = Modern.Forms.TextMeasurer.MeasureText (text, sk_font.Typeface, (int)Math.Round (font.Size));

            return new SizeF (measured.Width, measured.Height);
        }

        /// <summary>Clears the entire surface to the specified color.</summary>
        public void Clear (Color color) => canvas.Clear (new SKColor (color.R, color.G, color.B, color.A));

        private static SKPath CreatePolygonPath (PointF[] points, bool close)
        {
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);

            for (var i = 1; i < points.Length; i++)
                path.LineTo (points[i].X, points[i].Y);

            if (close)
                path.Close ();

            return path;
        }
    }

    internal static class SkiaGraphicsExtensions
    {
        public static SKRect ToSKRect (this RectangleF rect)
            => new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}
