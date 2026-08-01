using System.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Majorsilence.Forms.Drawing.Text;
using SkiaSharp;

#pragma warning disable CA1416  // WinForms compat layer — intentionally uses Windows-only System.Drawing APIs

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: wraps an <see cref="SKCanvas"/> to provide a GDI-like drawing surface.
    /// Use <see cref="Control.CreateGraphics"/> for text measurement; for painting use <see cref="PaintEventArgs.Canvas"/>.
    /// </summary>
    public sealed class Graphics : IDisposable
    {
        private readonly Control? _control;
        private readonly SKCanvas? _canvas;
        private readonly bool _ownsCanvas;
        private bool _disposed;

        internal Graphics (Control? control = null) { _control = control; }

        internal Graphics (SKCanvas canvas, Control? control = null) { _canvas = canvas; _control = control; }

        private Graphics (SKCanvas canvas, bool ownsCanvas)
        {
            _canvas = canvas;
            _ownsCanvas = ownsCanvas;
        }

        /// <summary>Creates a Graphics object for drawing on the specified Majorsilence.Forms.Drawing.Image.
        /// Drawing goes directly into the image's backing bitmap, matching System.Drawing semantics.</summary>
        public static Graphics FromImage (Majorsilence.Forms.Drawing.Image image)
        {
            ArgumentNullException.ThrowIfNull (image);
            var backing = image.GetSKBitmap () ?? throw new ArgumentException ("Image has no backing bitmap.", nameof (image));
            return new Graphics (new SKCanvas (backing), ownsCanvas: true);
        }

        /// <summary>Creates a Graphics object for the specified window handle. Returns a no-op instance in Majorsilence.Forms.</summary>
        public static Graphics FromHwnd (IntPtr hwnd) => new Graphics ();

        /// <summary>Creates a Graphics object from a device context handle. Returns a no-op instance in Majorsilence.Forms.</summary>
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

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font (maps to SKTypeface at the font's size).</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            return MeasureString (text, face, (int)font.Size);
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font, constrained to a size.</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, SizeF layoutArea)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            return MeasureString (text, face, (int)layoutArea.Width, (int)font.Size);
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font and StringFormat (format is ignored).</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.StringFormat? format)
            => MeasureString (text, font);

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font, constrained to int width (StringFormat ignored).</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, int width, Majorsilence.Forms.Drawing.StringFormat? format)
        {
            if (string.IsNullOrEmpty (text) || font is null) return SizeF.Empty;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            return MeasureString (text, face, width, (int)font.Size);
        }

        /// <summary>Measures the string with a Majorsilence.Forms.Drawing.Font, constrained to SizeF (StringFormat ignored).</summary>
        public SizeF MeasureString (string text, Majorsilence.Forms.Drawing.Font font, SizeF layoutArea, Majorsilence.Forms.Drawing.StringFormat? format)
            => MeasureString (text, font, layoutArea);

        /// <summary>
        /// Returns one <see cref="Majorsilence.Forms.Drawing.Region"/> per character range previously
        /// supplied to <see cref="Majorsilence.Forms.Drawing.StringFormat.SetMeasurableCharacterRanges"/>,
        /// describing where that range lands when <paramref name="text"/> is laid out inside
        /// <paramref name="layoutRect"/>.
        /// </summary>
        /// <remarks>
        /// A range that wraps across lines produces a region containing one rectangle per line, the same
        /// as GDI+. Wrapping is greedy word wrap against <c>layoutRect.Width</c>; a width of zero (or a
        /// <see cref="Majorsilence.Forms.Drawing.StringFormat"/> with no ranges set) means no wrapping.
        /// Text is positioned from the left edge of <paramref name="layoutRect"/> — horizontal
        /// <c>StringFormat.Alignment</c> is not applied to the measurement.
        /// </remarks>
        public Majorsilence.Forms.Drawing.Region[] MeasureCharacterRanges (
            string text,
            Majorsilence.Forms.Drawing.Font font,
            RectangleF layoutRect,
            Majorsilence.Forms.Drawing.StringFormat? stringFormat)
        {
            var ranges = stringFormat?.MeasurableCharacterRanges;
            if (ranges is null || ranges.Length == 0)
                return [];

            var result = new Majorsilence.Forms.Drawing.Region[ranges.Length];
            if (string.IsNullOrEmpty (text) || font is null) {
                for (var i = 0; i < ranges.Length; i++)
                    result[i] = EmptyRegion ();
                return result;
            }

            var skFont = font.GetSKFont ();
            var metrics = skFont.Metrics;
            var lineHeight = metrics.Descent - metrics.Ascent;
            var lines = LayoutLines (text, skFont, layoutRect.Width);

            for (var i = 0; i < ranges.Length; i++) {
                var region = EmptyRegion ();
                var rangeStart = ranges[i].First;
                var rangeEnd = rangeStart + ranges[i].Length;

                foreach (var (start, length, index) in lines) {
                    // Clip this range against the slice of text that landed on this line.
                    var segStart = Math.Max (rangeStart, start);
                    var segEnd = Math.Min (rangeEnd, start + length);
                    if (segEnd <= segStart)
                        continue;

                    var x = layoutRect.X + skFont.MeasureText (text.AsSpan (start, segStart - start));
                    var width = skFont.MeasureText (text.AsSpan (segStart, segEnd - segStart));
                    region.Union (new RectangleF (x, layoutRect.Y + index * lineHeight, width, lineHeight));
                }

                result[i] = region;
            }

            return result;
        }

        private static Majorsilence.Forms.Drawing.Region EmptyRegion ()
        {
            var region = new Majorsilence.Forms.Drawing.Region ();
            region.MakeEmpty ();
            return region;
        }

        /// <summary>
        /// Splits <paramref name="text"/> into laid-out lines, honoring explicit newlines and greedy
        /// word wrapping at <paramref name="maxWidth"/>. Each entry keeps its start index into the
        /// original string so character ranges can be mapped back onto it.
        /// </summary>
        private static List<(int Start, int Length, int Index)> LayoutLines (string text, SKFont font, float maxWidth)
        {
            var lines = new List<(int, int, int)> ();
            var lineIndex = 0;

            for (var paragraphStart = 0; paragraphStart <= text.Length;) {
                var newline = text.IndexOf ('\n', paragraphStart);
                var paragraphEnd = newline < 0 ? text.Length : newline;
                // A trailing \r belongs to the break, not to the measured text.
                var contentEnd = paragraphEnd > paragraphStart && text[paragraphEnd - 1] == '\r' ? paragraphEnd - 1 : paragraphEnd;

                if (maxWidth <= 0) {
                    lines.Add ((paragraphStart, contentEnd - paragraphStart, lineIndex++));
                } else {
                    var cursor = paragraphStart;
                    while (cursor <= contentEnd) {
                        var take = FitRun (text, cursor, contentEnd, font, maxWidth);
                        lines.Add ((cursor, take, lineIndex++));
                        cursor += take;
                        if (take == 0)
                            break;      // nothing fits at all — avoid spinning forever
                        // Consume the space the wrap happened on, so it isn't re-measured next line.
                        while (cursor < contentEnd && text[cursor] == ' ')
                            cursor++;
                    }
                }

                if (newline < 0)
                    break;
                paragraphStart = paragraphEnd + 1;
            }

            return lines;
        }

        /// <summary>
        /// Returns how many characters starting at <paramref name="start"/> fit within
        /// <paramref name="maxWidth"/>, breaking on the last space that fits. Falls back to a hard
        /// character break when a single word is itself wider than the line.
        /// </summary>
        private static int FitRun (string text, int start, int end, SKFont font, float maxWidth)
        {
            if (start >= end)
                return 0;

            var lastBreak = -1;
            for (var i = start; i < end; i++) {
                var width = font.MeasureText (text.AsSpan (start, i - start + 1));
                if (width > maxWidth) {
                    if (lastBreak >= 0)
                        return lastBreak - start;        // break at the last space that fit
                    return Math.Max (1, i - start);      // single over-long word: hard break
                }
                if (text[i] == ' ')
                    lastBreak = i;
            }
            return end - start;
        }

        // --- Transform stubs ---

        /// <summary>Gets the dots-per-inch (always 96 in Majorsilence.Forms).</summary>
        public float DpiX => 96f;

        /// <inheritdoc cref="DpiX"/>
        public float DpiY => 96f;

        /// <summary>Gets the clip bounds (returns control bounds or Empty).</summary>
        public RectangleF ClipBounds => _control is not null
            ? new RectangleF (0, 0, _control.Width, _control.Height)
            : RectangleF.Empty;

        /// <summary>Gets the visible clip bounds. Alias for ClipBounds in Majorsilence.Forms.</summary>
        public RectangleF VisibleClipBounds => ClipBounds;

        /// <summary>Gets whether the visible clip region is empty. Always false in Majorsilence.Forms.</summary>
        public bool IsVisibleClipEmpty => false;

        /// <summary>Gets or sets the unit of measure for page coordinates. Stub in Majorsilence.Forms — always Pixel.</summary>
        public Majorsilence.Forms.Drawing.GraphicsUnit PageUnit { get; set; } = Majorsilence.Forms.Drawing.GraphicsUnit.Pixel;

        /// <summary>Gets or sets the scaling factor for page-to-world coordinates. Stub in Majorsilence.Forms.</summary>
        public float PageScale { get; set; } = 1f;

        /// <summary>
        /// Saves the current graphics state (transform and clip) and returns a token that
        /// <see cref="Restore"/> can rewind to.
        /// </summary>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsState Save ()
            => new Majorsilence.Forms.Drawing.Drawing2D.GraphicsState (_canvas?.Save () ?? 0);

        /// <summary>Restores the graphics state to a token previously returned by <see cref="Save"/>.</summary>
        public void Restore (Majorsilence.Forms.Drawing.Drawing2D.GraphicsState state)
        {
            if (_canvas is null)
                return;
            if (state is null)
                _canvas.Restore ();
            else
                _canvas.RestoreToCount (state.Count);
        }

        /// <summary>
        /// Opens a new graphics container: saves the current transform and clip so that
        /// <see cref="EndContainer"/> restores them exactly. Containers share the underlying canvas
        /// state stack with <see cref="Save"/>/<see cref="Restore"/>, so the two nest freely.
        /// </summary>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer BeginContainer ()
            => new Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer (_canvas?.Save () ?? 0);

        /// <summary>
        /// Opens a new graphics container that also maps <paramref name="srcrect"/> onto
        /// <paramref name="dstrect"/> — drawing inside the container uses <paramref name="srcrect"/>'s
        /// coordinate space and lands in <paramref name="dstrect"/>, clipped to it.
        /// </summary>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer BeginContainer (
            RectangleF dstrect, RectangleF srcrect, Majorsilence.Forms.Drawing.GraphicsUnit unit)
        {
            var container = BeginContainer ();
            if (_canvas is null || srcrect.Width == 0 || srcrect.Height == 0)
                return container;

            _canvas.ClipRect (new SKRect (dstrect.Left, dstrect.Top, dstrect.Right, dstrect.Bottom));
            _canvas.Translate (dstrect.Left, dstrect.Top);
            _canvas.Scale (dstrect.Width / srcrect.Width, dstrect.Height / srcrect.Height);
            _canvas.Translate (-srcrect.Left, -srcrect.Top);
            return container;
        }

        /// <inheritdoc cref="BeginContainer(RectangleF, RectangleF, Majorsilence.Forms.Drawing.GraphicsUnit)"/>
        public Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer BeginContainer (
            Rectangle dstrect, Rectangle srcrect, Majorsilence.Forms.Drawing.GraphicsUnit unit)
            => BeginContainer ((RectangleF)dstrect, (RectangleF)srcrect, unit);

        /// <summary>Closes a container opened by <see cref="BeginContainer()"/>, restoring the saved state.</summary>
        public void EndContainer (Majorsilence.Forms.Drawing.Drawing2D.GraphicsContainer container)
        {
            if (_canvas is null)
                return;
            if (container is null)
                _canvas.Restore ();
            else
                _canvas.RestoreToCount (container.Count);
        }

        /// <summary>Gets or sets the smoothing mode. Stub in Majorsilence.Forms (always anti-aliased).</summary>
        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;

        /// <summary>Gets or sets the interpolation mode. Stub in Majorsilence.Forms.</summary>
        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;

        /// <summary>Gets or sets the text rendering hint. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Text.TextRenderingHint TextRenderingHint { get; set; } = Majorsilence.Forms.Drawing.Text.TextRenderingHint.SystemDefault;

        /// <summary>Gets or sets the compositing quality. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Drawing2D.CompositingQuality CompositingQuality { get; set; } = Majorsilence.Forms.Drawing.Drawing2D.CompositingQuality.Default;

        /// <summary>Gets or sets the pixel offset mode. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Drawing2D.PixelOffsetMode PixelOffsetMode { get; set; } = Majorsilence.Forms.Drawing.Drawing2D.PixelOffsetMode.Default;

        /// <summary>Gets or sets the compositing mode. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Drawing2D.CompositingMode CompositingMode { get; set; } = Majorsilence.Forms.Drawing.Drawing2D.CompositingMode.SourceOver;

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

        /// <summary>Intersects the clipping region with the given rectangle. Stub in Majorsilence.Forms.</summary>
        public void IntersectClip (Rectangle rect) => SetClip (rect);

        /// <summary>Intersects the clipping region with the given rectangle. Stub in Majorsilence.Forms.</summary>
        public void IntersectClip (RectangleF rect) => SetClip (rect);

        /// <summary>Gets or sets the clipping region. Stub in Majorsilence.Forms — always null.</summary>
        public Majorsilence.Forms.Drawing.Region? Clip { get => null; set { } }

        /// <summary>Excludes a rectangle from the clipping region. Stub in Majorsilence.Forms.</summary>
        public void ExcludeClip (Rectangle rect) { }

        /// <summary>Excludes a region from the clipping region. Stub in Majorsilence.Forms.</summary>
        public void ExcludeClip (Majorsilence.Forms.Drawing.Region region) { }

        /// <summary>Returns whether the specified point is within the clipping region. Always returns true in Majorsilence.Forms.</summary>
        public bool IsVisible (Point point) => true;

        /// <summary>Returns whether the specified rectangle is within the clipping region. Always returns true in Majorsilence.Forms.</summary>
        public bool IsVisible (Rectangle rect) => true;

        /// <summary>Returns whether the specified rectangle is within the clipping region. Always returns true in Majorsilence.Forms.</summary>
        public bool IsVisible (RectangleF rect) => true;

        /// <summary>Applies a matrix transform to the current transform. Stub in Majorsilence.Forms.</summary>
        public void MultiplyTransform (Majorsilence.Forms.Drawing.Drawing2D.Matrix matrix) { }

        /// <summary>Applies a matrix transform to the current transform. Stub in Majorsilence.Forms.</summary>
        public void MultiplyTransform (Majorsilence.Forms.Drawing.Drawing2D.Matrix matrix, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder order) { }

        // --- Drawing operations (Skia-backed when canvas is available) ---

        /// <summary>Draws a Majorsilence.Forms.Drawing.Icon at the specified location. Converts to bitmap internally.</summary>
#pragma warning disable CA1416
        public void DrawIcon (Majorsilence.Forms.Drawing.Icon icon, int x, int y)
        {
            if (icon == null || _canvas == null) return;
            using var bmp = icon.ToBitmap ();
            using var skBmp = bmp.ToSKBitmap ();
            if (skBmp != null) _canvas.DrawBitmap (skBmp, new SKPoint (x, y));
        }

        /// <summary>Draws a Majorsilence.Forms.Drawing.Icon stretched to fill the destination rectangle.</summary>
        public void DrawIcon (Majorsilence.Forms.Drawing.Icon icon, Rectangle targetRect)
        {
            if (icon == null || _canvas == null) return;
            using var bmp = icon.ToBitmap ();
            using var skBmp = bmp.ToSKBitmap ();
            if (skBmp != null) _canvas.DrawBitmap (skBmp, new SKRect (targetRect.Left, targetRect.Top, targetRect.Right, targetRect.Bottom));
        }

        /// <summary>Draws an unscaled Majorsilence.Forms.Drawing.Icon at the specified location.</summary>
        public void DrawIconUnstretched (Majorsilence.Forms.Drawing.Icon icon, Rectangle targetRect) => DrawIcon (icon, targetRect.X, targetRect.Y);

        /// <summary>Returns the device context handle. Returns IntPtr.Zero in Majorsilence.Forms (stub).</summary>
        public IntPtr GetHdc () => IntPtr.Zero;

        /// <summary>Releases the device context handle. No-op in Majorsilence.Forms (stub).</summary>
        public void ReleaseHdc (IntPtr hdc) { }

        /// <summary>Releases the device context handle. No-op in Majorsilence.Forms (stub).</summary>
        public void ReleaseHdc () { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Majorsilence.Forms.</summary>
        public void CopyFromScreen (int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize) { }

        /// <summary>Copies the contents of the screen to this Graphics surface. Stub in Majorsilence.Forms.</summary>
        public void CopyFromScreen (System.Drawing.Point upperLeftSource, System.Drawing.Point upperLeftDestination, Size blockRegionSize) { }

        private static SKColor ToSKColor (System.Drawing.Color c) => new SKColor (c.R, c.G, c.B, c.A);

        /// <summary>
        /// Builds a fill <see cref="SKPaint"/> from a brush, honouring its actual type: a solid colour,
        /// or a gradient/hatch/texture shader. Previously every non-solid brush was collapsed to opaque
        /// black, which turned e.g. a soft <c>PathGradientBrush</c> glow into a solid black box. Anti-
        /// aliasing follows the current <see cref="SmoothingMode"/> so solid fills keep their prior look.
        /// Caller owns disposal.
        /// </summary>
        private SKPaint CreateFillPaint (Majorsilence.Forms.Drawing.Brush brush)
        {
            var paint = brush.CreatePaint ();
            paint.IsAntialias = SmoothingMode != Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode.None;
            return paint;
        }

        private static float PenWidth (Majorsilence.Forms.Drawing.Pen pen) => pen.Width;
        private static SKColor PenColor (Majorsilence.Forms.Drawing.Pen pen) => ToSKColor (pen.Color);

        /// <summary>Clears the canvas with the given color.</summary>
        public void Clear (System.Drawing.Color color) => _canvas?.Clear (ToSKColor (color));

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = CreateFillPaint (brush);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, RectangleF rect)
        {
            if (_canvas is null) return;
            using var paint = CreateFillPaint (brush);
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, float x, float y, float width, float height)
            => FillRectangle (brush, new RectangleF (x, y, width, height));

        /// <summary>Fills a rectangle using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillRectangle (Majorsilence.Forms.Drawing.Brush brush, int x, int y, int width, int height)
            => FillRectangle (brush, new Rectangle (x, y, width, height));

        /// <summary>Draws a rectangle outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawRect (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws a rectangle outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, int x, int y, int width, int height)
            => DrawRectangle (pen, new Rectangle (x, y, width, height));

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, Point p1, Point p2)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, int x1, int y1, int x2, int y2)
            => DrawLine (pen, new Point (x1, y1), new Point (x2, y2));

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, PointF p1, PointF p2)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawLine (p1.X, p1.Y, p2.X, p2.Y, paint);
        }

        /// <summary>Draws a line using a Majorsilence.Forms.Drawing.Pen and float coordinates.</summary>
        public void DrawLine (Majorsilence.Forms.Drawing.Pen pen, float x1, float y1, float x2, float y2)
            => DrawLine (pen, new PointF (x1, y1), new PointF (x2, y2));

        /// <summary>Draws a rectangle with float coordinates.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height)
            => DrawRectangle (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a rectangle with a RectangleF.</summary>
        public void DrawRectangle (Majorsilence.Forms.Drawing.Pen pen, RectangleF rect)
            => DrawRectangle (pen, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Draws an arc.</summary>
        public void DrawArc (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws an arc with float coordinates.</summary>
        public void DrawArc (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => DrawArc (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height), startAngle, sweepAngle);

        /// <summary>Draws a pie section.</summary>
        public void DrawPie (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect, float startAngle, float sweepAngle)
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
        public void FillPie (Majorsilence.Forms.Drawing.Brush brush, Rectangle rect, float startAngle, float sweepAngle)
        {
            if (_canvas is null) return;
            using var paint = CreateFillPaint (brush);
            using var path = new SKPath ();
            path.MoveTo (rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            path.AddArc (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), startAngle, sweepAngle);
            path.Close ();
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills a pie section with float coordinates.</summary>
        public void FillPie (Majorsilence.Forms.Drawing.Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
            => FillPie (brush, new Rectangle ((int)x, (int)y, (int)width, (int)height), startAngle, sweepAngle);

        /// <summary>Draws a cubic Bezier curve.</summary>
        public void DrawBezier (Majorsilence.Forms.Drawing.Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (pt1.X, pt1.Y);
            path.CubicTo (pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a cubic Bezier curve using integer Point coordinates.</summary>
        public void DrawBezier (Majorsilence.Forms.Drawing.Pen pen, Point pt1, Point pt2, Point pt3, Point pt4)
            => DrawBezier (pen, new PointF (pt1.X, pt1.Y), new PointF (pt2.X, pt2.Y), new PointF (pt3.X, pt3.Y), new PointF (pt4.X, pt4.Y));

        /// <summary>Draws multiple cubic Bezier curves.</summary>
        public void DrawBeziers (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 4) return;
            for (int i = 0; i + 3 < points.Length; i += 3)
                DrawBezier (pen, points[i], points[i + 1], points[i + 2], points[i + 3]);
        }

        /// <summary>Draws multiple cubic Bezier curves using integer Point coordinates.</summary>
        public void DrawBeziers (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 4) return;
            for (int i = 0; i + 3 < points.Length; i += 3)
                DrawBezier (pen, points[i], points[i + 1], points[i + 2], points[i + 3]);
        }

        /// <summary>Draws a cardinal spline curve through the specified points.</summary>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a cardinal spline curve using integer Point coordinates.</summary>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>
        /// Draws a segment of a cardinal spline through <paramref name="points"/>, starting at
        /// <paramref name="offset"/> and spanning <paramref name="numberOfSegments"/> segments,
        /// with the curve's tightness controlled by <paramref name="tension"/> (0 = straight
        /// lines between points, matching System.Drawing.Graphics.DrawCurve's default of 0.5).
        /// Unlike the plain (Pen, PointF[]) overload above (which just connects points with
        /// straight lines), this is a real Catmull-Rom spline through the points.
        /// </summary>
        public void DrawCurve (Majorsilence.Forms.Drawing.Pen pen, PointF[] points, int offset, int numberOfSegments, float tension)
        {
            if (_canvas is null || points.Length < 2 || numberOfSegments < 1) return;

            int last = offset + numberOfSegments;
            if (last >= points.Length) last = points.Length - 1;
            if (offset < 0 || offset >= last) return;

            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            using var path = new SKPath ();
            path.MoveTo (points[offset].X, points[offset].Y);

            // Catmull-Rom, converted to tension-scaled tangents (matches GDI+'s interpretation
            // of the `tension` parameter): for each segment P1->P2, use the neighboring points
            // P0 and P3 (clamped to the array ends) to compute tangents, subdivided into a fixed
            // number of steps per segment for a smooth curve.
            const int stepsPerSegment = 24;
            for (int i = offset; i < last; i++)
            {
                var p0 = points[System.Math.Max (i - 1, 0)];
                var p1 = points[i];
                var p2 = points[i + 1];
                var p3 = points[System.Math.Min (i + 2, points.Length - 1)];

                for (int s = 1; s <= stepsPerSegment; s++)
                {
                    float t = s / (float)stepsPerSegment;
                    float t2 = t * t;
                    float t3 = t2 * t;

                    float m0x = tension * (p2.X - p0.X);
                    float m0y = tension * (p2.Y - p0.Y);
                    float m1x = tension * (p3.X - p1.X);
                    float m1y = tension * (p3.Y - p1.Y);

                    float h00 = 2 * t3 - 3 * t2 + 1;
                    float h10 = t3 - 2 * t2 + t;
                    float h01 = -2 * t3 + 3 * t2;
                    float h11 = t3 - t2;

                    float x = h00 * p1.X + h10 * m0x + h01 * p2.X + h11 * m1x;
                    float y = h00 * p1.Y + h10 * m0y + h01 * p2.Y + h11 * m1y;

                    path.LineTo (x, y);
                }
            }

            _canvas.DrawPath (path, paint);
        }

        /// <summary>Fills an ellipse using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = CreateFillPaint (brush);
            _canvas.DrawOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Fills an ellipse using a Majorsilence.Forms.Drawing.Brush.</summary>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, float x, float y, float width, float height)
            => FillEllipse (brush, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws an ellipse outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, Rectangle rect)
        {
            if (_canvas is null) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            _canvas.DrawOval (new SKRect (rect.Left, rect.Top, rect.Right, rect.Bottom), paint);
        }

        /// <summary>Draws an ellipse outline using a Majorsilence.Forms.Drawing.Pen (RectangleF overload).</summary>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, RectangleF rect)
            => DrawEllipse (pen, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Draws an ellipse outline using a Majorsilence.Forms.Drawing.Pen.</summary>
        public void DrawEllipse (Majorsilence.Forms.Drawing.Pen pen, float x, float y, float width, float height)
            => DrawEllipse (pen, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Fills an ellipse using a Majorsilence.Forms.Drawing.Brush (RectangleF overload).</summary>
        public void FillEllipse (Majorsilence.Forms.Drawing.Brush brush, RectangleF rect)
            => FillEllipse (brush, new Rectangle ((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

        /// <summary>Fills a closed polygon.</summary>
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = CreateFillPaint (brush);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws a closed polygon outline.</summary>
        public void DrawPolygon (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
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
        public void DrawPolygon (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
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
        public void FillPolygon (Majorsilence.Forms.Drawing.Brush brush, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            var path = new SKPath ();
            path.MoveTo (points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++) path.LineTo (points[i].X, points[i].Y);
            path.Close ();
            using var paint = CreateFillPaint (brush);
            _canvas.DrawPath (path, paint);
        }

        /// <summary>Draws an open polyline.</summary>
        public void DrawLines (Majorsilence.Forms.Drawing.Pen pen, Point[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            for (int i = 1; i < points.Length; i++)
                _canvas.DrawLine (points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, paint);
        }

        /// <summary>Draws an open polyline using floating-point coordinates.</summary>
        public void DrawLines (Majorsilence.Forms.Drawing.Pen pen, PointF[] points)
        {
            if (_canvas is null || points.Length < 2) return;
            using var paint = new SKPaint { Color = PenColor (pen), Style = SKPaintStyle.Stroke, StrokeWidth = PenWidth (pen) };
            for (int i = 1; i < points.Length; i++)
                _canvas.DrawLine (points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, paint);
        }

        /// <summary>Draws a series of rectangles.</summary>
        public void DrawRectangles (Majorsilence.Forms.Drawing.Pen pen, Rectangle[] rects)
        {
            foreach (var r in rects) DrawRectangle (pen, r);
        }

        /// <summary>Draws a series of rectangles using floating-point coordinates.</summary>
        public void DrawRectangles (Majorsilence.Forms.Drawing.Pen pen, RectangleF[] rects)
        {
            foreach (var r in rects) DrawRectangle (pen, new Rectangle ((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height));
        }

        /// <summary>Fills a series of rectangles.</summary>
        public void FillRectangles (Majorsilence.Forms.Drawing.Brush brush, Rectangle[] rects)
        {
            foreach (var r in rects) FillRectangle (brush, r);
        }

        /// <summary>Fills a series of rectangles using floating-point coordinates.</summary>
        public void FillRectangles (Majorsilence.Forms.Drawing.Brush brush, RectangleF[] rects)
        {
            foreach (var r in rects) FillRectangle (brush, r);
        }

        /// <summary>Draws a string with the given Majorsilence.Forms.Drawing.Font and Brush.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, float x, float y)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            var face = SKTypeface.FromFamilyName (font.FontFamily.Name, font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            using var skFont = new SKFont (face, font.Size);
            using var paint = brush.CreatePaint ();
            _canvas.DrawText (text, x, y + font.Size, SKTextAlign.Left, skFont, paint);
        }

        /// <summary>Draws a string at the given PointF.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, PointF point)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string at the given Point.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, Point point)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string within the specified rectangle.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds)
        {
            if (_canvas is null || string.IsNullOrEmpty (text)) return;
            _canvas.Save ();
            _canvas.ClipRect (new SKRect (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            DrawString (text, font, brush, bounds.Left, bounds.Top);
            _canvas.Restore ();
        }

        /// <summary>Draws a string within the specified rectangle (StringFormat is ignored).</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds, Majorsilence.Forms.Drawing.StringFormat? format)
            => DrawString (text, font, brush, bounds);

        /// <summary>Draws a string within the specified rectangle.</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, RectangleF bounds, object? format)
            => DrawString (text, font, brush, bounds);

        /// <summary>Draws a string at the given PointF (StringFormat is ignored).</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, PointF point, Majorsilence.Forms.Drawing.StringFormat? format)
            => DrawString (text, font, brush, point.X, point.Y);

        /// <summary>Draws a string at the given float coordinates (StringFormat is ignored).</summary>
        public void DrawString (string text, Majorsilence.Forms.Drawing.Font font, Majorsilence.Forms.Drawing.Brush brush, float x, float y, Majorsilence.Forms.Drawing.StringFormat? format)
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
        public void DrawImage (SKBitmap image, Rectangle destRect, Rectangle srcRect, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
        {
            if (_canvas is null || image is null) return;
            var src = new SKRect (srcRect.Left, srcRect.Top, srcRect.Right, srcRect.Bottom);
            var dst = new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
            _canvas.DrawBitmap (image, src, dst);
        }

        /// <summary>Draws an SKBitmap scaled to fill the destination rectangle.</summary>
        public void DrawImage (SKBitmap image, float x, float y, float width, float height)
            => DrawImage (image, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image at the specified location.</summary>
#pragma warning disable CA1416
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, int x, int y)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, x, y);
        }

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image at the specified location and size (int overload).</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, int x, int y, int width, int height)
            => DrawImage (image, new Rectangle (x, y, width, height));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image scaled to fill the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, destRect);
        }

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image scaled to fill the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, RectangleF destRect)
            => DrawImage (image, Rectangle.Round (destRect));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image at (x,y) scaled to (width,height).</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, float x, float y, float width, float height)
            => DrawImage (image, new Rectangle ((int)x, (int)y, (int)width, (int)height));

        /// <summary>Draws a Majorsilence.Forms.Drawing.Bitmap at the specified location.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Bitmap bitmap, int x, int y) => DrawImage ((Majorsilence.Forms.Drawing.Image)bitmap, x, y);

        /// <summary>Draws a Majorsilence.Forms.Drawing.Bitmap scaled to fill the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Bitmap bitmap, Rectangle destRect) => DrawImage ((Majorsilence.Forms.Drawing.Image)bitmap, destRect);

        /// <summary>Draws a portion of a Majorsilence.Forms.Drawing.Image to the destination rectangle.</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, Rectangle srcRect, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
        {
            using var bmp = image?.ToSKBitmap ();
            if (bmp != null) DrawImage (bmp, destRect, srcRect, srcUnit);
        }

        /// <summary>Draws a portion of a Majorsilence.Forms.Drawing.Image to the destination rectangle (float-rectangle overload, rounded to integer device rects).</summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, RectangleF destRect, RectangleF srcRect, Majorsilence.Forms.Drawing.GraphicsUnit srcUnit)
            => DrawImage (image, Rectangle.Round (destRect), Rectangle.Round (srcRect), srcUnit);

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image unscaled at a point.</summary>
        public void DrawImageUnscaled (Majorsilence.Forms.Drawing.Image image, int x, int y) => DrawImage (image, x, y);

        /// <summary>Draws a Majorsilence.Forms.Drawing.Image unscaled at a point.</summary>
        public void DrawImageUnscaled (Majorsilence.Forms.Drawing.Image image, Rectangle rect) => DrawImage (image, rect);

        /// <summary>
        /// Draws a portion of an image into <paramref name="destRect"/>, applying the color
        /// adjustments described by <paramref name="imageAttrs"/> (color matrix, gamma, transparent
        /// color key, remap table). This is the GDI+ image color-remapping path: the matrix and gamma
        /// become an <c>SKColorFilter</c> on the draw paint, and the lookup-style adjustments are
        /// baked into a temporary copy of the source pixels first.
        /// </summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect,
            float srcX, float srcY, float srcWidth, float srcHeight,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
        {
            if (_canvas is null || image is null)
                return;

            using var bmp = image.ToSKBitmap ();
            if (bmp is null)
                return;

            using var adjusted = imageAttrs?.ApplyPixelAdjustments (bmp);
            var source = adjusted ?? bmp;

            using var colorFilter = imageAttrs?.ToSKColorFilter ();
            using var paint = colorFilter is null ? null : new SKPaint { ColorFilter = colorFilter, IsAntialias = true };

            var src = new SKRect (srcX, srcY, srcX + srcWidth, srcY + srcHeight);
            var dst = new SKRect (destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
            _canvas.DrawBitmap (source, src, dst, paint);
        }

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, float, float, float, float, Majorsilence.Forms.Drawing.GraphicsUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect,
            int srcX, int srcY, int srcWidth, int srcHeight,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
            => DrawImage (image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs);

        /// <inheritdoc cref="DrawImage(Majorsilence.Forms.Drawing.Image, Rectangle, float, float, float, float, Majorsilence.Forms.Drawing.GraphicsUnit, Majorsilence.Forms.Drawing.Imaging.ImageAttributes)"/>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect, Rectangle srcRect,
            Majorsilence.Forms.Drawing.GraphicsUnit srcUnit,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
            => DrawImage (image, destRect, (float)srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, srcUnit, imageAttrs);

        /// <summary>
        /// Draws the whole image into <paramref name="destRect"/> with the given color adjustments.
        /// </summary>
        public void DrawImage (Majorsilence.Forms.Drawing.Image image, Rectangle destRect,
            Majorsilence.Forms.Drawing.Imaging.ImageAttributes? imageAttrs)
            => DrawImage (image, destRect, 0f, 0f, image?.Width ?? 0, image?.Height ?? 0,
                Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, imageAttrs);
#pragma warning restore CA1416

        /// <summary>Rotates the current transform by the specified angle in degrees.</summary>
        public void RotateTransform (float angle) => _canvas?.RotateDegrees (angle);

        /// <summary>Applies a rotation in degrees around the specified point.</summary>
        public void RotateTransform (float angle, Majorsilence.Forms.Drawing.Drawing2D.MatrixOrder order)
            => _canvas?.RotateDegrees (angle);

        /// <summary>Gets or sets the current world transformation matrix. Stub — setting is ignored.</summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Drawing2D.Matrix Transform {
            get => new Majorsilence.Forms.Drawing.Drawing2D.Matrix ();
            set { }
        }
#pragma warning restore CA1416

        /// <summary>Draws a Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath outline using the specified pen.</summary>
#pragma warning disable CA1416
        public void DrawPath (Majorsilence.Forms.Drawing.Pen pen, Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            using var paint = new SKPaint {
                Color = new SKColor (pen.Color.R, pen.Color.G, pen.Color.B, pen.Color.A),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = pen.Width,
                IsAntialias = SmoothingMode != Majorsilence.Forms.Drawing.Drawing2D.SmoothingMode.None
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

        /// <summary>Fills the interior of a Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath using the specified brush.</summary>
        public void FillPath (Majorsilence.Forms.Drawing.Brush brush, Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path)
        {
            if (_canvas is null || path is null) return;

            using var paint = CreateFillPaint (brush);

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

        /// <summary>Sets the clipping region of this Graphics to the intersection of the current clip and a Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath.</summary>
#pragma warning disable CA1416
        public void SetClip (Majorsilence.Forms.Drawing.Drawing2D.GraphicsPath path)
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

        /// <summary>Sets the clipping region to the intersection with an existing region. Stub in Majorsilence.Forms.</summary>
#pragma warning disable CA1416
        public void SetClip (Majorsilence.Forms.Drawing.Region region) { }
#pragma warning restore CA1416

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (!_disposed) {
                _disposed = true;
                if (_ownsCanvas)
                    _canvas?.Dispose ();
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
