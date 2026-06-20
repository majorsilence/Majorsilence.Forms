using System.Drawing;
using Continuum.Drawing.Drawing2D;
using Continuum.Drawing.Text;
using SkiaSharp;

#pragma warning disable CA1416  // WinForms compat layer — intentionally uses Windows-only System.Drawing APIs

namespace Continuum.Forms
{
    /// <summary>
    /// WinForms compatibility: wraps an <see cref="SKCanvas"/> to provide a GDI-like drawing surface.
    /// Use <see cref="Control.CreateGraphics"/> for text measurement; for painting use <see cref="PaintEventArgs.Canvas"/>.
    /// </summary>
    public sealed class Graphics : IDisposable
    {
        private readonly Control? _control;
        private readonly SKCanvas? _canvas;
        private readonly SKBitmap? _ownedBitmap;
        private bool _disposed;

        internal Graphics (Control? control = null) { _control = control; }

        internal Graphics (SKCanvas canvas, Control? control = null) { _canvas = canvas; _control = control; }

        private Graphics (SKBitmap bitmap)
        {
            _ownedBitmap = bitmap;
            _canvas = new SKCanvas (bitmap);
        }

        /// <summary>Creates a Graphics object for drawing on the specified Continuum.Drawing.Image.</summary>
        public static Graphics FromImage (Continuum.Drawing.Image image)
        {
            // Create a transparent-backed owned bitmap; drawing goes nowhere useful cross-platform.
            var bmp = new SKBitmap (image?.Width ?? 1, image?.Height ?? 1, SKColorType.Bgra8888, SKAlphaType.Premul);
            return new Graphics (bmp);
        }

        /// <summary>Creates a Graphics object for the specified window handle. Returns a no-op instance in Continuum.Forms.</summary>
        public static Graphics FromHwnd (IntPtr hwnd) => new Graphics ();

        /// <summary>Creates a Graphics object from a device context handle. Returns a no-op instance in Continuum.Forms.</summary>
        public static Graphics FromHdc (IntPtr hdc) => new Graphics ();

        // --- Text measurement ---

        /// <summary>Measures the size of the specified string using the given font and size.</summary>
        public SizeF MeasureString (string text, SKTypeface font, int fontSize = -1)
        {
            if (string.IsNullOrEmpty (text)) return SizeF.Empty;
            var sz = fontSize <= 0
                ? TextMeasurer.MeasureText (text, font, Theme.FontSize)
                : TextMeasurer.MeasureText (text, font, fontSize);
            return new SizeF (sz.Width, sz.Height);
        }

        /// <summary>Measures the string constrained to a maximum width.</summary>
        public SizeF MeasureString (string text, SKTypeface font, int maxWidth, int fontSize = -1)
        {
            if (string.IsNullOrEmpty (text)) return SizeF.Empty;
            var proposed = new System.Drawing.Size (maxWidth, int.MaxValue);
            var sz = fontSize <= 0
                ? TextMeasurer.MeasureText (text, font, Theme.FontSize, proposed)
                : TextMeasurer.MeasureText (text, font, fontSize, proposed);
            return new SizeF (sz.Width, sz.Height);
        }

        /// <summary>Measures the string using the control's own font.</summary>
        public SizeF MeasureString (string text, Control control)
        {
            if (string.IsNullOrEmpty (text)) return SizeF.Empty;
            var sz = TextMeasurer.MeasureText (text, control);
            return new SizeF (sz.Width, sz.Height);
        }

        /// <summary>Measures the string with a Continuum.Drawing.Font (maps to SKTypeface at the font's size).</summary>
        public SizeF MeasureString (string text, Continuum.Drawing.Font font)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            return MeasureString (text, face, (int)font.Size);
        }

        /// <summary>Measures the string with a Continuum.Drawing.Font, constrained to a size.</summary>
        public SizeF MeasureString (string text, Continuum.Drawing.Font font, SizeF layoutArea)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            return MeasureString (text, face, (int)layoutArea.Width, (int)font.Size);
        }

        /// <summary>Measures the string with a Continuum.Drawing.Font and StringFormat (format is ignored).</summary>
        public SizeF MeasureString (string text, Continuum.Drawing.Font font, Continuum.Drawing.StringFormat? format)
            => MeasureString (text, font);

        /// <summary>Measures the string with a Continuum.Drawing.Font, constrained to int width (StringFormat ignored).</summary>
        public SizeF MeasureString (string text, Continuum.Drawing.Font font, int width, Continuum.Drawing.StringFormat? format)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            return MeasureString (text, face, width, (int)font.Size);
        }

        /// <summary>Measures the string with a Continuum.Drawing.Font, constrained to SizeF (StringFormat ignored).</summary>
        public SizeF MeasureString (string text, Continuum.Drawing.Font font, SizeF layoutArea, Continuum.Drawing.StringFormat? format)
            => MeasureString (text, font, layoutArea);

        // --- Transform stubs ---

        /// <summary>Gets the dots-per-inch (always 96 in Continuum.Forms).</summary>
        public float DpiX => 96f;

        /// <inheritdoc cref="DpiX"/>
        public float DpiY => 96f;

        /// <summary>Gets the clip bounds (returns control bounds or Empty).</summary>
        public RectangleF ClipBounds => _control is not null
            ? new RectangleF (0, 0, _control.Width, _control.Height)
            : RectangleF.Empty;

        /// <summary>Gets the visible clip bounds. Alias for ClipBounds in Continuum.Forms.</summary>
        public RectangleF VisibleClipBounds => ClipBounds;

        /// <summary>Gets whether the visible clip region is empty. Always false in Continuum.Forms.</summary>
        public bool IsVisibleClipEmpty => false;

        /// <summary>Gets or sets the unit of measure for page coordinates. Stub in Continuum.Forms — always Pixel.</summary>
        public Continuum.Drawing.GraphicsUnit PageUnit { get; set; } = Continuum.Drawing.GraphicsUnit.Pixel;

        /// <summary>Gets or sets the scaling factor for page-to-world coordinates. Stub in Continuum.Forms.</summary>
        public float PageScale { get; set; } = 1f;

        /// <summary>Stub: saves the current graphics state.</summary>
        public Continuum.Drawing.Drawing2D.GraphicsState Save () { _canvas?.Save (); return null!; }

        /// <summary>Stub: restores a previously saved state.</summary>
        public void Restore (Continuum.Drawing.Drawing2D.GraphicsState state) => _canvas?.Restore ();

        /// <summary>Gets or sets the smoothing mode. Stub in Continuum.Forms (always anti-aliased).</summary>
        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;

        /// <summary>Gets or sets the interpolation mode. Stub in Continuum.Forms.</summary>
        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;

        /// <summary>Gets or sets the text rendering hint. Stub in Continuum.Forms.</summary>
        public Continuum.Drawing.Text.TextRenderingHint TextRenderingHint { get; set; } = Continuum.Drawing.Text.TextRenderingHint.SystemDefault;

        /// <summary>Gets or sets the compositing quality. Stub in Continuum.Forms.</summary>
        public Continuum.Drawing.Drawing2D.CompositingQuality CompositingQuality { get; set; } = Continuum.Drawing.Drawing2D.CompositingQuality.Default;

        /// <summary>Gets or sets the pixel offset mode. Stub in Continuum.Forms.</summary>
        public Continuum.Drawing.Drawing2D.PixelOffsetMode PixelOffsetMode { get; set; } = Continuum.Drawing.Drawing2D.PixelOffsetMode.Default;

        /// <summary>Gets or sets the compositing mode. Stub in Continuum.Forms.</summary>
        public Continuum.Drawing.Drawing2D.CompositingMode CompositingMode { get; set; } = Continuum.Drawing.Drawing2D.CompositingMode.SourceOver;

        /// <summary>Applies a scale transform.</summary>
        public void ScaleTransform (float sx, float sy) => _canvas?.Scale (sx, sy);

        /// <summary>Translates the coordinate origin.</summary>
        public void TranslateTransform (float dx, float dy) => _canvas?.Translate (dx, dy);

        /// <summary>Resets all transforms.</summary>
        public void ResetTransform () => _canvas?.ResetMatrix ();

        // --- Clipping ---

        /// <summary>Sets the clipping region to the given rectangle.</summary>
        public void SetClip (Rectangle rect) => _canvas?.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));

        /// <summary>Resets the clipping region.</summary>
        public void ResetClip () { }

        /// <summary>Intersects the clipping region with the given rectangle. Stub in Continuum.Forms.</summary>
        public void IntersectClip (Rectangle rect) => SetClip (rect);

        /// <summary>Intersects the clipping region with the given rectangle. Stub in Continuum.Forms.</summary>
        public void IntersectClip (RectangleF rect) => SetClip (rect);

        /// <summary>Gets or sets the clipping region. Stub in Continuum.Forms — always null.</summary>
        public Continuum.Drawing.Region? Clip { get => null; set { } }

        /// <summary>Excludes a rectangle from the clipping region. Stub in Continuum.Forms.</summary>
        public void ExcludeClip (Rectangle rect) { }

        /// <summary>Excludes a region from the clipping region. Stub in Continuum.Forms.</summary>
        public void ExcludeClip (Continuum.Drawing.Region region) { }

        /// <summary>Returns whether the specified point is within the clipping region. Always returns true in Continuum.Forms.</summary>
        public bool IsVisible (Point point) => true;

        /// <summary>Returns whether the specified rectangle is within the clipping region. Always returns true in Continuum.Forms.</summary>
        public bool IsVisible (Rectangle rect) => true;

        /// <summary>Returns whether the specified rectangle is within the clipping region. Always returns true in Continuum.Forms.</summary>
        public bool IsVisible (RectangleF rect) => true;

        /// <summary>Applies a matrix transform to the current transform. Stub in Continuum.Forms.</summary>
        public void MultiplyTransform (Continuum.Drawing.Drawing2D.Matrix matrix) { }

        /// <summary>Applies a matrix transform to the current transform. Stub in Continuum.Forms.</summary>
        public void MultiplyTransform (Continuum.Drawing.Drawing2D.Matrix matrix, Continuum.Drawing.Drawing2D.MatrixOrder order) { }

        // --- Drawing operations (Skia-backed when canvas is available) ---

        /// <summary>Draws a Continuum.Drawing.Icon at the specified location. Converts to bitmap internally.</summary>
#pragma warning disable CA1416
        public void DrawIcon (Continuum.Drawing.Icon icon, int x, int y)
        {
            if (icon == null || _canvas == null) return;
            using var bmp = icon.ToBitmap ();
            using var skBmp = bmp.ToSKBitmap ();
            if (skBmp != null) _canvas.DrawBitmap (skBmp, new SKPoint (x, y));
        }

        /// <summary>Draws a Continuum.Drawing.Icon stretched to fill the destination rectangle.</summary>
        public void DrawIcon (Continuum.Drawing.Icon icon, Rectangle targetRect)
        {
            if (icon == null || _canvas == null) return;
            using var bmp = icon.ToBitmap ();
            using var skBmp = bmp.ToSKBitmap ();
            if (skBmp != null) _canvas.DrawBitmap (skBmp, new SKRect (targetRect.Left, targetRect.Top, targetRect.Right, targetRect.Bottom));
        }

        /// <summary>Draws an unscaled Continuum.Drawing.Icon at the specified location.</summary>
        public void DrawIconUnstretched (Continuum.Drawing.Icon icon, Rectangle targetRect) => DrawIcon (icon, targetRect.X, targetRect.Y);

        /// <summary>Returns the device context handle. Returns IntPtr.Zero in Continuum.Forms (stub).</summary>
        public IntPtr GetHdc () => IntPtr.Zero;

        /// <summary>Releases the device context handle. No-op in Continuum.Forms (stub).</summary>
        public void ReleaseHdc (IntPtr hdc) { }

        /// <summary>Releases the device context handle. No-op in Continuum.Forms (stub).</summary>
        public void ReleaseHdc () { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Continuum.Forms.</summary>
        public void CopyFromScreen (int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize) { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Continuum.Forms.</summary>
        public void CopyFromScreen (System.Drawing.Point upperLeftSource, System.Drawing.Point upperLeftDestination, Size blockRegionSize) { }

        private static SKColor ToSKColor (System.Drawing.Color c) => new SKColor (c.R, c.G, c.B, c.A);
        private static SKColor BrushColor (Continuum.Drawing.Brush brush)
            => brush is Continuum.Drawing.SolidBrush sb ? ToSKColor (sb.Color) : SKColors.Black;
        private static float PenWidth (Continuum.Drawing.Pen pen) => pen.Width;
        private static SKColor PenColor (Continuum.Drawing.Pen pen) => ToSKColor (pen.Color);

        /// <summary>Clears the canvas with the given color.</summary>
        public void Clear (System.Drawing.Color color) => _canvas?.Clear (ToSKColor (color));

        /// <summary>Fills a rectangle using a Continuum.Drawing.Brush.</summary>
        public void FillRectangle (Continuum.Drawing.Brush brush, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = BrushColor (brush), Style = SKPaintStyle.Fill };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills a rectangle using a Continuum.Drawing.Brush.</summary>
        public void FillRectangle (Continuum.Drawing.Brush brush, RectangleF rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = BrushColor (brush), Style = SKPaintStyle.Fill };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills a rectangle using a Continuum.Drawing.Brush.</summary>
        public void FillRectangle (Continuum.Drawing.Brush brush, float x, float y, float width, float height)
            => FillRectangle (brush, new RectangleF (x, y, width, height));

        /// <summary>Fills a rectangle using a Continuum.Drawing.Brush.</summary>
        public void FillRectangle (Continuum.Drawing.Brush brush, int x, int y, int width, int height)
            => FillRectangle (brush, new Rectangle (x, y, width, height));

        /// <summary>Draws a rectangle outline using a Continuum.Drawing.Pen.</summary>
        public void DrawRectangle (Continuum.Drawing.Pen pen, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a rectangle outline using a Continuum.Drawing.Pen.</summary>
        public void DrawRectangle (Continuum.Drawing.Pen pen, int x, int y, int width, int height)
            => DrawRectangle (pen, new Rectangle (x, y, width, height));

        /// <summary>Draws a line using a Continuum.Drawing.Pen.</summary>
        public void DrawLine (Continuum.Drawing.Pen pen, Point p1, Point p2)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a line using a Continuum.Drawing.Pen.</summary>
        public void DrawLine (Continuum.Drawing.Pen pen, int x1, int y1, int x2, int y2)
            => DrawLine (pen, new Point (x1, y1), new Point (x2, y2));

        /// <summary>Draws a line using a Continuum.Drawing.Pen.</summary>
        public void DrawLine (Continuum.Drawing.Pen pen, PointF p1, PointF p2)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a rectangle with float coordinates.</summary>
        public void DrawRectangle (Continuum.Drawing.Pen pen, float x, float y, float width, float height)
            => DrawRectangle (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a rectangle with a RectangleF.</summary>
        public void DrawRectangle (Continuum.Drawing.Pen pen, RectangleF rect)
            => DrawRectangle (pen, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Draws an arc.</summary>
        public void DrawArc (Continuum.Drawing.Pen pen, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws an arc with float coordinates.</summary>
        public void DrawArc (Continuum.Drawing.Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => DrawArc (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height), startAngle, sweepAngle);

        /// <summary>Draws a pie section.</summary>
        public void DrawPie (Continuum.Drawing.Pen pen, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            path.Close ();
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a pie section.</summary>
        public void FillPie (Continuum.Drawing.Brush brush, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = BrushColor (brush), Style = SKPaintStyle.Fill };
            using var path = new SKPath ();
            path.MoveTo (rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            path.Close ();
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a pie section with float coordinates.</summary>
        public void FillPie (Continuum.Drawing.Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => FillPie (brush, new Rectangle ((int)x, (int)y, (int)width, (int)height), startAngle, sweepAngle);

        /// <summary>Draws a cubic Bezier curve.</summary>
        public void DrawBezier (Continuum.Drawing.Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (pt1.X, pt1.Y);
            path.CubicTo (pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a cubic Bezier curve using integer Point coordinates.</summary>
        public void DrawBezier (Continuum.Drawing.Pen pen, Point pt1, Point pt2, Point pt3, Point pt4)
            => DrawBezier (pen, new PointF (pt1.X, pt1.Y), new PointF (pt2.X, pt2.Y), new PointF (pt3.X, pt3.Y), new PointF (pt4.X, pt4.Y));

        /// <summary>Draws multiple cubic Bezier curves.</summary>
        public void DrawBeziers (Continuum.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 4) return;
            for (int i = 0; i + 3 < points.Length; i += 3)
                DrawBezier (pen, points[i], points[i + 1], points[i + 2], points[i + 3]);
        }

        /// <summary>Draws multiple cubic Bezier curves using integer Point coordinates.</summary>
        public void DrawBeziers (Continuum.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 4) return;
            for (int i = 0; i + 3 < points.Length; i += 3)
                DrawBezier (pen, points[i], points[i + 1], points[i + 2], points[i + 3]);
        }

        /// <summary>Draws a cardinal spline curve through the specified points.</summary>
        public void DrawCurve (Continuum.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a cardinal spline curve using integer Point coordinates.</summary>
        public void DrawCurve (Continuum.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills an ellipse using a Continuum.Drawing.Brush.</summary>
        public void FillEllipse (Continuum.Drawing.Brush brush, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = BrushColor (brush), Style = SKPaintStyle.Fill };
            _canvas.DrawOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills an ellipse using a Continuum.Drawing.Brush.</summary>
        public void FillEllipse (Continuum.Drawing.Brush brush, float x, float y, float width, float height)
            => FillEllipse (brush, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws an ellipse outline using a Continuum.Drawing.Pen.</summary>
        public void DrawEllipse (Continuum.Drawing.Pen pen, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws an ellipse outline using a Continuum.Drawing.Pen (RectangleF overload).</summary>
        public void DrawEllipse (Continuum.Drawing.Pen pen, RectangleF rect)
            => DrawEllipse (pen, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Fills an ellipse using a Continuum.Drawing.Brush (RectangleF overload).</summary>
        public void FillEllipse (Continuum.Drawing.Brush brush, RectangleF rect)
            => FillEllipse (brush, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Fills a closed polygon.</summary>
        public void FillPolygon (Continuum.Drawing.Brush brush, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = new SKPaint { Color = BrushColor (brush), Style = SKPaintStyle.Fill };
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a closed polygon outline.</summary>
        public void DrawPolygon (Continuum.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a closed polygon outline using PointF coordinates.</summary>
        public void DrawPolygon (Continuum.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a closed polygon using PointF coordinates.</summary>
        public void FillPolygon (Continuum.Drawing.Brush brush, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = new SKPaint { Color = BrushColor (brush), Style = SKPaintStyle.Fill };
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws an open polyline.</summary>
        public void DrawLines (Continuum.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            for (int i = 1; i < points.Length; i++)
                _canvas.DrawLine (points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, paint);
        }

        /// <summary>Draws an open polyline using floating-point coordinates.</summary>
        public void DrawLines (Continuum.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            for (int i = 1; i < points.Length; i++)
                _canvas.DrawLine (points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, paint);
        }

        /// <summary>Draws a series of rectangles.</summary>
        public void DrawRectangles (Continuum.Drawing.Pen pen, Rectangle[] rects)
        {
            foreach (var r in rects) DrawRectangle (pen, r);
        }

        /// <summary>Draws a series of rectangles using floating-point coordinates.</summary>
        public void DrawRectangles (Continuum.Drawing.Pen pen, RectangleF[] rects)
        {
            foreach (var r in rects) DrawRectangle (pen, new Rectangle ((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height));
        }

        /// <summary>Fills a series of rectangles.</summary>
        public void FillRectangles (Continuum.Drawing.Brush brush, Rectangle[] rects)
        {
            foreach (var r in rects) FillRectangle (brush, r);
        }

        /// <summary>Fills a series of rectangles using floating-point coordinates.</summary>
        public void FillRectangles (Continuum.Drawing.Brush brush, RectangleF[] rects)
        {
            foreach (var r in rects) FillRectangle (brush, r);
        }

        /// <summary>Draws a string with the given Continuum.Drawing.Font and Brush.</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, float x, float y)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            using var skFont = new SKFont (face, font.Size);
            using var paint = new SKPaint { Color = BrushColor (brush) };
            _canvas.DrawText (text, x, y + font.Size, SKTextAlign.Left, skFont, paint);
        }

        /// <summary>Draws a string at the given PointF.</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, PointF point)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string at the given Point.</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, Point point)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string within the specified rectangle.</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, RectangleF bounds)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            _canvas.Save ();
            _canvas.ClipRect (new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            DrawString (text, font, brush, bounds.Left, bounds.Top);
            _canvas.Restore ();
        }

        /// <summary>Draws a string within the specified rectangle (StringFormat is ignored).</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, RectangleF bounds, Continuum.Drawing.StringFormat? format)
            => DrawString (text, font, brush, bounds);

        /// <summary>Draws a string within the specified rectangle.</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, RectangleF bounds, object? format)
            => DrawString (text, font, brush, bounds);

        /// <summary>Draws a string at the given PointF (StringFormat is ignored).</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, PointF point, Continuum.Drawing.StringFormat? format)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string at the given float coordinates (StringFormat is ignored).</summary>
        public void DrawString (string text, Continuum.Drawing.Font font, Continuum.Drawing.Brush brush, float x, float y, Continuum.Drawing.StringFormat? format)
            => DrawString (text, font, brush, x, y);

        /// <summary>Draws an SKBitmap image at the given rectangle.</summary>
        public void DrawImage (SKBitmap image, Rectangle destRect)
        {
            if (_canvas is null || image is null) return;
            _canvas.DrawBitmap (image, new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom));
        }

        /// <summary>Draws an SKBitmap image at the given position.</summary>
        public void DrawImage (SKBitmap image, int x, int y)
        {
            if (_canvas is null || image is null) return;
            _canvas.DrawBitmap (image, new SKPoint (x, y));
        }

        /// <summary>Draws a focus rectangle (stub — draws a dotted border).</summary>
        public void DrawFocusRectangle (Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            paint.PathEffect = SKPathEffect.CreateDash ([1, 1], 0);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        // --- SKColor overloads (internal usage) ---

        /// <summary>Fills a rectangle with the given SKColor.</summary>
        public void FillRectangle (SKColor color, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a rectangle outline with the given SKColor.</summary>
        public void DrawRectangle (SKColor color, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a line with the given SKColor.</summary>
        public void DrawLine (SKColor color, Point p1, Point p2)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a string with the given SKColor.</summary>
        public void DrawString (string text, SKTypeface font, SKColor color, float x, float y)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            using var skFont = new SKFont (font, Theme.FontSize);
            using var paint = new SKPaint { Color = color };
            _canvas.DrawText (text, x, y + Theme.FontSize, SKTextAlign.Left, skFont, paint);
        }

        /// <summary>Draws a string within bounds with the given SKColor.</summary>
        public void DrawString (string text, SKTypeface font, SKColor color, RectangleF bounds)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            _canvas.Save ();
            _canvas.ClipRect (new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            DrawString (text, font, color, bounds.Left, bounds.Top);
            _canvas.Restore ();
        }

        /// <summary>Draws an SKBitmap at its original size at the given position.</summary>
        public void DrawImageUnscaled (SKBitmap image, int x, int y) => DrawImage (image, x, y);

        /// <summary>Draws an SKBitmap at its original size at the given point.</summary>
        public void DrawImageUnscaled (SKBitmap image, Point point) => DrawImage (image, point.X, point.Y);

        /// <summary>Draws an SKBitmap at its original size, clipped to the given rectangle.</summary>
        public void DrawImageUnscaled (SKBitmap image, Rectangle rect) => DrawImage (image, rect);

        /// <summary>Draws an SKBitmap clipped to the destination rectangle from a source rectangle.</summary>
        public void DrawImage (SKBitmap image, Rectangle destRect, Rectangle srcRect, Continuum.Drawing.GraphicsUnit srcUnit)
        {
            if (_canvas is null || image is null) return;
            var src = new SKRect (srcRect.Left, srcRect.Top, srcRect.Right, srcRect.Bottom);
            var dst = new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
            _canvas.DrawBitmap (image, src, dst);
        }

        /// <summary>Draws an SKBitmap scaled to fill the destination rectangle.</summary>
        public void DrawImage (SKBitmap image, float x, float y, float width, float height)
            => DrawImage (image, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a Continuum.Drawing.Image at the specified location.</summary>
#pragma warning disable CA1416
        public void DrawImage (Continuum.Drawing.Image image, int x, int y)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, x, y);
        }

        /// <summary>Draws a Continuum.Drawing.Image at the specified location and size (int overload).</summary>
        public void DrawImage (Continuum.Drawing.Image image, int x, int y, int width, int height)
            => DrawImage (image, new Rectangle (x, y, width, height));

        /// <summary>Draws a Continuum.Drawing.Image scaled to fill the destination rectangle.</summary>
        public void DrawImage (Continuum.Drawing.Image image, Rectangle destRect)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, destRect);
        }

        /// <summary>Draws a Continuum.Drawing.Image scaled to fill the destination rectangle.</summary>
        public void DrawImage (Continuum.Drawing.Image image, RectangleF destRect)
            => DrawImage (image, Rectangle.Round (destRect));

        /// <summary>Draws a Continuum.Drawing.Image at (x,y) scaled to (width,height).</summary>
        public void DrawImage (Continuum.Drawing.Image image, float x, float y, float width, float height)
            => DrawImage (image, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a Continuum.Drawing.Bitmap at the specified location.</summary>
        public void DrawImage (Continuum.Drawing.Bitmap bitmap, int x, int y) => DrawImage ((Continuum.Drawing.Image)bitmap, x, y);

        /// <summary>Draws a Continuum.Drawing.Bitmap scaled to fill the destination rectangle.</summary>
        public void DrawImage (Continuum.Drawing.Bitmap bitmap, Rectangle destRect) => DrawImage ((Continuum.Drawing.Image)bitmap, destRect);

        /// <summary>Draws a portion of a Continuum.Drawing.Image to the destination rectangle.</summary>
        public void DrawImage (Continuum.Drawing.Image image, Rectangle destRect, Rectangle srcRect, Continuum.Drawing.GraphicsUnit srcUnit)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, destRect, srcRect, srcUnit);
        }

        /// <summary>Draws a Continuum.Drawing.Image unscaled at a point.</summary>
        public void DrawImageUnscaled (Continuum.Drawing.Image image, int x, int y) => DrawImage (image, x, y);

        /// <summary>Draws a Continuum.Drawing.Image unscaled at a point.</summary>
        public void DrawImageUnscaled (Continuum.Drawing.Image image, Rectangle rect) => DrawImage (image, rect);
#pragma warning restore CA1416

        /// <summary>Rotates the current transform by the specified angle in degrees.</summary>
        public void RotateTransform (float angle) => _canvas?.RotateDegrees (angle);

        /// <summary>Applies a rotation in degrees around the specified point.</summary>
        public void RotateTransform (float angle, Continuum.Drawing.Drawing2D.MatrixOrder order)
            => _canvas?.RotateDegrees (angle);

        /// <summary>Gets or sets the current world transformation matrix. Stub — setting is ignored.</summary>
#pragma warning disable CA1416
        public Continuum.Drawing.Drawing2D.Matrix Transform {
            get => new Continuum.Drawing.Drawing2D.Matrix ();
            set { }
        }
#pragma warning restore CA1416

        /// <summary>Draws a Continuum.Drawing.Drawing2D.GraphicsPath outline using the specified pen.</summary>
#pragma warning disable CA1416
        public void DrawPath (Continuum.Drawing.Pen pen, Continuum.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            using var paint = new SKPaint {
                Color = new SKColor (pen.Color.R, pen.Color.G, pen.Color.B, pen.Color.A),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = pen.Width,
                IsAntialias = SmoothingMode != Continuum.Drawing.Drawing2D.SmoothingMode.None
            };

            using var skPath = new SKPath ();

            foreach (var point in path.PathPoints) {
                if (skPath.PointCount == 0)
                    skPath.MoveTo (point.X, point.Y);
                else
                    skPath.LineTo (point.X, point.Y);
            }

            _canvas.DrawPath (skPath, paint);
        }

        /// <summary>Fills the interior of a Continuum.Drawing.Drawing2D.GraphicsPath using the specified brush.</summary>
        public void FillPath (Continuum.Drawing.Brush brush, Continuum.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            SKColor fillColor;

            if (brush is Continuum.Drawing.SolidBrush sb)
                fillColor = new SKColor (sb.Color.R, sb.Color.G, sb.Color.B, sb.Color.A);
            else
                fillColor = SKColors.Black;

            using var paint = new SKPaint {
                Color = fillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = SmoothingMode != Continuum.Drawing.Drawing2D.SmoothingMode.None
            };

            using var skPath = new SKPath ();

            foreach (var point in path.PathPoints) {
                if (skPath.PointCount == 0)
                    skPath.MoveTo (point.X, point.Y);
                else
                    skPath.LineTo (point.X, point.Y);
            }

            skPath.Close ();
            _canvas.DrawPath (skPath, paint);
        }
#pragma warning restore CA1416

        /// <summary>Sets the clipping region of this Graphics to the intersection of the current clip and a Continuum.Drawing.Drawing2D.GraphicsPath.</summary>
#pragma warning disable CA1416
        public void SetClip (Continuum.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            using var skPath = new SKPath ();
            foreach (var point in path.PathPoints) {
                if (skPath.PointCount == 0)
                    skPath.MoveTo (point.X, point.Y);
                else
                    skPath.LineTo (point.X, point.Y);
            }

            skPath.Close ();
            _canvas.ClipPath (skPath);
        }
#pragma warning restore CA1416

        /// <summary>Sets the clipping region to a rectangle. WinForms compatibility overload.</summary>
        public void SetClip (RectangleF rect)
            => _canvas?.ClipRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom));

        /// <summary>Sets the clipping region to the intersection with an existing region. Stub in Continuum.Forms.</summary>
#pragma warning disable CA1416
        public void SetClip (Continuum.Drawing.Region region) { }
#pragma warning restore CA1416

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (!_disposed) {
                _disposed = true;
                _ownedBitmap?.Dispose ();
            }
        }
    }

    public partial class Control
    {
        /// <summary>
        /// Creates a <see cref="Graphics"/> object for the control's drawing surface.
        /// Use for text measurement only; for actual drawing, use <see cref="PaintEventArgs.Canvas"/>.
        /// </summary>
        public Graphics CreateGraphics () => new Graphics (this);
    }
}
