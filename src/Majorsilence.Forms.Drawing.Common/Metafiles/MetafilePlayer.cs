using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging.Metafiles
{
    // Playing a metafile back onto a Skia canvas.
    //
    // The shared half: device-context state, the pen/brush/font to SKPaint mapping, and the drawing
    // helpers. The format-specific halves are the record switches in EmfPlayer and WmfPlayer, which
    // decode their own record layouts and then call in here.
    //
    // The guiding rule throughout is that an unrecognised or malformed record is skipped rather than
    // thrown on. A metafile routinely carries records from a producer nobody has heard of, and a
    // clipboard metafile is routinely truncated; drawing everything that did parse is far more useful
    // than discarding a picture because one record in three hundred was unfamiliar.

    /// <summary>Replays a metafile's records onto a canvas.</summary>
    internal abstract class MetafilePlayer
    {
        private readonly Stack<MetaDeviceContext> saved = new ();
        private readonly Dictionary<int, object> objects = [];
        private readonly SKMatrix output;

        protected MetafilePlayer (SKCanvas canvas, SKMatrix output, SKSize deviceExtent)
        {
            Canvas = canvas;
            this.output = output;

            // The viewport extent starts at the device size, which is what GDI does and what makes
            // a bare SetWindowExt mean "fill the device".
            Dc = new MetaDeviceContext {
                ViewportExtent = deviceExtent.Width > 0 && deviceExtent.Height > 0 ? deviceExtent : new SKSize (1, 1),
            };
        }

        /// <summary>The live device-context state.</summary>
        protected MetaDeviceContext Dc { get; private set; }

        /// <summary>The canvas being drawn onto.</summary>
        protected SKCanvas Canvas { get; }

        /// <summary>The path being recorded, between BeginPath and EndPath.</summary>
        protected SKPath? RecordingPath { get; set; }

        /// <summary>The completed path, between EndPath and the operation that consumes it.</summary>
        protected SKPath? CompletedPath { get; set; }

        /// <summary>Counts records the player did not act on, for diagnostics.</summary>
        internal int SkippedRecords { get; private set; }

        /// <summary>Notes that a record was recognised but not acted on.</summary>
        protected void Skip () => SkippedRecords++;

        // ── Device-context stack ────────────────────────────────────────────────

        /// <summary>Pushes a copy of the device-context state.</summary>
        protected void SaveDc ()
        {
            saved.Push (Dc.Clone ());
            Canvas.Save ();
        }

        /// <summary>Pops device-context state. GDI allows a relative depth, hence the count.</summary>
        protected void RestoreDc (int relative)
        {
            // SaveDC/RestoreDC take a level: -1 is the most recent, -2 the one before it. A positive
            // level is an absolute depth, which metafiles use far more rarely.
            var levels = relative < 0 ? -relative : Math.Max (saved.Count - relative, 1);

            for (var i = 0; i < levels && saved.Count > 0; i++) {
                Dc = saved.Pop ();
                Canvas.Restore ();
            }
        }

        // ── Object table ────────────────────────────────────────────────────────

        /// <summary>Stores a created GDI object under its handle.</summary>
        protected void StoreObject (int handle, object value) => objects[handle] = value;

        /// <summary>Removes a deleted GDI object.</summary>
        protected void DeleteObject (int handle) => objects.Remove (handle);

        /// <summary>Selects a created or stock object into the device context.</summary>
        protected void SelectObject (int handle)
        {
            // The top bit marks a stock object, which is never in the table because the metafile
            // never created it.
            if ((handle & 0x80000000) != 0) {
                var index = handle & 0x7FFFFFFF;

                if (StockObjects.Pen (index) is { } stockPen)
                    Dc.Pen = stockPen;
                else if (StockObjects.Brush (index) is { } stockBrush)
                    Dc.Brush = stockBrush;
                else if (StockObjects.Font (index) is { } stockFont)
                    Dc.Font = stockFont;

                return;
            }

            if (!objects.TryGetValue (handle, out var value))
                return;

            switch (value) {
            case MetaPen pen: Dc.Pen = pen; break;
            case MetaBrush brush: Dc.Brush = brush; break;
            case MetaFont font: Dc.Font = font; break;
            }
        }

        // ── Transforms ──────────────────────────────────────────────────────────

        /// <summary>Puts the canvas into the metafile's current logical coordinate space.</summary>
        protected void ApplyTransform ()
            => Canvas.SetMatrix (output.PreConcat (Dc.MapTransform).PreConcat (Dc.World));

        // ── Paint ───────────────────────────────────────────────────────────────

        /// <summary>Builds the stroke paint for the selected pen, or null when it draws nothing.</summary>
        protected SKPaint? StrokePaint ()
        {
            if (Dc.Pen.IsNull)
                return null;

            var paint = new SKPaint {
                Style = SKPaintStyle.Stroke,
                Color = Dc.Pen.Color,
                // GDI treats width zero as "one device pixel, however the space is scaled". Skia
                // means the same thing by zero, so it passes through rather than being clamped.
                StrokeWidth = Dc.Pen.Width,
                IsAntialias = true,
            };

            if (Dc.Pen.DashPattern is { } dashes)
                paint.PathEffect = SKPathEffect.CreateDash (dashes, 0);

            return paint;
        }

        /// <summary>Builds the fill paint for the selected brush, or null when it fills nothing.</summary>
        protected SKPaint? FillPaint ()
        {
            if (Dc.Brush.IsNull)
                return null;

            var paint = new SKPaint {
                Style = SKPaintStyle.Fill,
                Color = Dc.Brush.Color,
                IsAntialias = true,
            };

            // BS_HATCHED
            if (Dc.Brush.Style == 2)
                paint.Shader = HatchShader (Dc.Brush.Hatch, Dc.Brush.Color, Dc.BackColor, Dc.BackMode == 2);
            else if (Dc.Brush.Pattern is { } pattern)
                paint.Shader = SKShader.CreateBitmap (pattern, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);

            return paint;
        }

        private static SKShader HatchShader (int hatch, SKColor foreground, SKColor background, bool opaque)
        {
            const int cell = 8;

            using var bitmap = new SKBitmap (cell, cell, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = new SKCanvas (bitmap);

            surface.Clear (opaque ? background : SKColors.Transparent);

            using var pen = new SKPaint { Color = foreground, StrokeWidth = 1, Style = SKPaintStyle.Stroke };

            switch (hatch) {
            case 0: // HS_HORIZONTAL
                surface.DrawLine (0, cell / 2f, cell, cell / 2f, pen);
                break;
            case 1: // HS_VERTICAL
                surface.DrawLine (cell / 2f, 0, cell / 2f, cell, pen);
                break;
            case 2: // HS_FDIAGONAL
                surface.DrawLine (0, 0, cell, cell, pen);
                break;
            case 3: // HS_BDIAGONAL
                surface.DrawLine (0, cell, cell, 0, pen);
                break;
            case 4: // HS_CROSS
                surface.DrawLine (0, cell / 2f, cell, cell / 2f, pen);
                surface.DrawLine (cell / 2f, 0, cell / 2f, cell, pen);
                break;
            default: // HS_DIAGCROSS
                surface.DrawLine (0, 0, cell, cell, pen);
                surface.DrawLine (0, cell, cell, 0, pen);
                break;
            }

            return SKShader.CreateBitmap (bitmap.Copy (), SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }

        // ── Drawing ─────────────────────────────────────────────────────────────

        /// <summary>Fills a closed shape with the brush, then outlines it with the pen.</summary>
        /// <remarks>That order is GDI's, and it is visible: the outline is drawn over the fill, so a
        /// fat pen on a small shape eats into the fill rather than sitting behind it.</remarks>
        protected void FillThenStroke (SKPath path)
        {
            ApplyTransform ();
            path.FillType = Dc.PolyFillMode == 2 ? SKPathFillType.Winding : SKPathFillType.EvenOdd;

            using var fill = FillPaint ();
            if (fill is not null)
                Canvas.DrawPath (path, fill);

            using var stroke = StrokePaint ();
            if (stroke is not null)
                Canvas.DrawPath (path, stroke);
        }

        /// <summary>Outlines an open shape with the pen only.</summary>
        protected void StrokeOnly (SKPath path)
        {
            ApplyTransform ();

            using var stroke = StrokePaint ();
            if (stroke is not null)
                Canvas.DrawPath (path, stroke);
        }

        /// <summary>Draws text at a reference point, honouring the current alignment and font.</summary>
        protected void DrawText (float x, float y, string text)
        {
            if (string.IsNullOrEmpty (text))
                return;

            ApplyTransform ();

            var style = SKFontStyle.Normal;

            if (Dc.Font.Bold && Dc.Font.Italic)
                style = SKFontStyle.BoldItalic;
            else if (Dc.Font.Bold)
                style = SKFontStyle.Bold;
            else if (Dc.Font.Italic)
                style = SKFontStyle.Italic;

            using var typeface = SKTypeface.FromFamilyName (Dc.Font.Name, style) ?? SKTypeface.Default;
            using var font = new SKFont (typeface, Math.Abs (Dc.Font.Height));
            using var paint = new SKPaint { Color = Dc.TextColor, IsAntialias = true };

            var width = font.MeasureText (text);

            // TA_CENTER is 6, which has TA_RIGHT's bit set too -- so the wider value has to be tested
            // first or centred text silently right-aligns.
            var horizontal = Dc.TextAlign & 6;
            var left = horizontal switch {
                6 => x - (width / 2f),
                2 => x - width,
                _ => x,
            };

            var metrics = font.Metrics;
            var vertical = Dc.TextAlign & 24;
            var baseline = vertical switch {
                24 => y,                     // TA_BASELINE
                8 => y + metrics.Descent,    // TA_BOTTOM
                _ => y - metrics.Ascent,     // TA_TOP
            };

            if (Dc.BackMode == 2) {
                using var back = new SKPaint { Color = Dc.BackColor, Style = SKPaintStyle.Fill };
                Canvas.DrawRect (left, baseline + metrics.Ascent, width, metrics.Descent - metrics.Ascent, back);
            }

            // Escapement is tenths of a degree counter-clockwise about the reference point.
            if (Dc.Font.Escapement != 0) {
                Canvas.Save ();
                Canvas.RotateDegrees (-Dc.Font.Escapement / 10f, x, y);
                Canvas.DrawText (text, left, baseline, font, paint);
                Canvas.Restore ();
                return;
            }

            Canvas.DrawText (text, left, baseline, font, paint);

            if (Dc.Font.Underline || Dc.Font.Strikeout) {
                using var rule = new SKPaint { Color = Dc.TextColor, StrokeWidth = Math.Max (1f, Dc.Font.Height / 14f) };

                if (Dc.Font.Underline)
                    Canvas.DrawLine (left, baseline + 1, left + width, baseline + 1, rule);
                if (Dc.Font.Strikeout)
                    Canvas.DrawLine (left, baseline + (metrics.Ascent / 3f), left + width, baseline + (metrics.Ascent / 3f), rule);
            }
        }

        /// <summary>Draws a decoded bitmap into a destination rectangle.</summary>
        protected void DrawBits (SKBitmap? bits, float x, float y, float width, float height)
        {
            if (bits is null || width == 0 || height == 0)
                return;

            ApplyTransform ();

            using var owned = bits;
            using var paint = new SKPaint { IsAntialias = true };

            // A negative extent means the blit is mirrored, which GDI expresses in the rectangle
            // rather than in a transform.
            var rect = SKRect.Create (x, y, width, height).Standardized;
            Canvas.DrawBitmap (owned, rect, paint);
        }

        /// <summary>Builds a path from a polygon or polyline's points.</summary>
        protected static SKPath BuildPath (SKPoint[] points, bool close)
        {
            var path = new SKPath ();

            if (points.Length == 0)
                return path;

            path.MoveTo (points[0]);

            for (var i = 1; i < points.Length; i++)
                path.LineTo (points[i]);

            if (close)
                path.Close ();

            return path;
        }

        /// <summary>Builds a path from a sequence of cubic Bézier control points.</summary>
        protected static SKPath BuildBezier (SKPoint[] points, SKPoint? start)
        {
            var path = new SKPath ();
            var index = 0;

            if (start is { } from)
                path.MoveTo (from);
            else if (points.Length > 0)
                path.MoveTo (points[index++]);

            // Three points per curve after the start; a trailing partial group is a malformed record
            // and is left undrawn rather than guessed at.
            for (; index + 2 < points.Length; index += 3)
                path.CubicTo (points[index], points[index + 1], points[index + 2]);

            return path;
        }

        /// <summary>Adds an arc, chord or pie to a path, given GDI's box-and-radials form.</summary>
        protected static void AddArc (SKPath path, SKRect box, SKPoint from, SKPoint to, ArcKind kind)
        {
            var centre = new SKPoint (box.MidX, box.MidY);
            var startAngle = Degrees (centre, from);
            var sweep = Degrees (centre, to) - startAngle;

            // GDI sweeps counter-clockwise by default, so a negative difference wraps rather than
            // reversing; drawing the short way round would silently mirror the arc.
            if (sweep <= 0)
                sweep += 360f;

            switch (kind) {
            case ArcKind.Pie:
                path.MoveTo (centre);
                path.ArcTo (box, startAngle, sweep, forceMoveTo: false);
                path.Close ();
                break;
            case ArcKind.Chord:
                path.AddArc (box, startAngle, sweep);
                path.Close ();
                break;
            default:
                path.AddArc (box, startAngle, sweep);
                break;
            }
        }

        private static float Degrees (SKPoint centre, SKPoint point)
        {
            var angle = (float) (Math.Atan2 (point.Y - centre.Y, point.X - centre.X) * 180 / Math.PI);
            return angle < 0 ? angle + 360f : angle;
        }

        /// <summary>What a box-and-radials record draws.</summary>
        protected enum ArcKind
        {
            /// <summary>The arc alone.</summary>
            Arc,
            /// <summary>The arc closed by a straight line between its ends.</summary>
            Chord,
            /// <summary>The arc closed through the centre.</summary>
            Pie,
        }

        /// <summary>Converts a GDI COLORREF (0x00BBGGRR) to a colour.</summary>
        protected static SKColor ColorRef (uint value)
            => new ((byte) (value & 0xFF), (byte) ((value >> 8) & 0xFF), (byte) ((value >> 16) & 0xFF), 0xFF);

        /// <summary>Reads a fixed-length UTF-16 name, stopping at the first null.</summary>
        protected static string ReadName (byte[] data, int offset, int maxChars)
        {
            if (offset < 0 || offset >= data.Length)
                return string.Empty;

            var builder = new StringBuilder ();

            for (var i = 0; i < maxChars; i++) {
                var at = offset + (i * 2);

                if (at + 1 >= data.Length)
                    break;

                var ch = (char) BitConverter.ToUInt16 (data, at);

                if (ch == '\0')
                    break;

                builder.Append (ch);
            }

            return builder.ToString ();
        }
    }
}
