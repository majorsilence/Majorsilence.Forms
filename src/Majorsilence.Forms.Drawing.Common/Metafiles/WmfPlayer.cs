using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging.Metafiles
{
    /// <summary>Interprets a Windows metafile's records (MS-WMF).</summary>
    /// <remarks>
    /// WMF is the 16-bit predecessor of EMF and differs in three ways that matter here: coordinates
    /// are 16-bit, several records take their arguments in reverse order, and objects are selected by
    /// an index into a table the metafile fills sequentially rather than by a handle the record names.
    /// The last one is the easy thing to get wrong -- an object table that is appended to in creation
    /// order, with deletes leaving a reusable hole, is what makes SelectObject land on the right pen.
    /// </remarks>
    internal sealed class WmfPlayer (SKCanvas canvas, SKMatrix output, SKSize deviceExtent)
        : MetafilePlayer (canvas, output, deviceExtent)
    {
        private readonly List<object?> table = [];

        internal void Play (IReadOnlyList<MetafileRecord> records)
        {
            foreach (var record in records) {
                try {
                    Execute (record);
                } catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or OverflowException) {
                    Skip ();
                }
            }
        }

        private void Execute (MetafileRecord r)
        {
            switch (r.Type) {
            case Meta.Eof:
                break;

            // ── Coordinate space. WMF stores these y-first. ─────────────────────
            case Meta.SetWindowOrg:
                Dc.WindowOrigin = new SKPoint (r.Int16 (2), r.Int16 (0));
                Dc.ExtentsSet = true;
                break;
            case Meta.SetWindowExt:
                Dc.WindowExtent = new SKSize (r.Int16 (2), r.Int16 (0));
                Dc.ExtentsSet = true;
                break;
            case Meta.SetViewportOrg:
                Dc.ViewportOrigin = new SKPoint (r.Int16 (2), r.Int16 (0));
                Dc.ExtentsSet = true;
                break;
            case Meta.SetViewportExt:
                Dc.ViewportExtent = new SKSize (r.Int16 (2), r.Int16 (0));
                Dc.ExtentsSet = true;
                break;
            case Meta.OffsetWindowOrg:
                Dc.WindowOrigin = new SKPoint (Dc.WindowOrigin.X + r.Int16 (2), Dc.WindowOrigin.Y + r.Int16 (0));
                break;
            case Meta.OffsetViewportOrg:
                Dc.ViewportOrigin = new SKPoint (Dc.ViewportOrigin.X + r.Int16 (2), Dc.ViewportOrigin.Y + r.Int16 (0));
                break;
            case Meta.ScaleWindowExt:
                // yDenom(0) yNum(2) xDenom(4) xNum(6)
                Dc.WindowExtent = Scale (Dc.WindowExtent, r.Int16 (6), r.Int16 (4), r.Int16 (2), r.Int16 (0));
                break;
            case Meta.ScaleViewportExt:
                Dc.ViewportExtent = Scale (Dc.ViewportExtent, r.Int16 (6), r.Int16 (4), r.Int16 (2), r.Int16 (0));
                break;
            case Meta.SetMapMode:
                Dc.MapMode = r.Int16 (0);
                break;

            // ── Device-context state ────────────────────────────────────────────
            case Meta.SaveDC:
                SaveDc ();
                break;
            case Meta.RestoreDC:
                RestoreDc (r.Int16 (0));
                break;
            case Meta.SetTextColor:
                Dc.TextColor = ColorRef (r.UInt32 (0));
                break;
            case Meta.SetBkColor:
                Dc.BackColor = ColorRef (r.UInt32 (0));
                break;
            case Meta.SetBkMode:
                Dc.BackMode = r.Int16 (0);
                break;
            case Meta.SetTextAlign:
                Dc.TextAlign = r.Int16 (0);
                break;
            case Meta.SetPolyFillMode:
                Dc.PolyFillMode = r.Int16 (0);
                break;
            case Meta.SetRop2:
            case Meta.SetStretchBltMode:
                Skip ();
                break;

            // ── Objects ─────────────────────────────────────────────────────────
            case Meta.CreatePenIndirect:
                // LOGPEN: style(0) width x(2) width y(4) color(6)
                Add (new MetaPen {
                    Style = r.Int16 (0),
                    Width = r.Int16 (2),
                    Color = ColorRef (r.UInt32 (6)),
                });
                break;
            case Meta.CreateBrushIndirect:
                // LOGBRUSH: style(0) color(2) hatch(6)
                Add (new MetaBrush {
                    Style = r.Int16 (0),
                    Color = ColorRef (r.UInt32 (2)),
                    Hatch = r.Int16 (6),
                });
                break;
            case Meta.CreateFontIndirect:
                Add (ReadFont (r));
                break;
            case Meta.SelectObject:
                Select (r.Int16 (0));
                break;
            case Meta.DeleteObject:
                Remove (r.Int16 (0));
                break;

            // ── Lines ───────────────────────────────────────────────────────────
            case Meta.MoveTo:
                Dc.Current = new SKPoint (r.Int16 (2), r.Int16 (0));
                break;
            case Meta.LineTo: {
                var to = new SKPoint (r.Int16 (2), r.Int16 (0));
                using var path = new SKPath ();
                path.MoveTo (Dc.Current);
                path.LineTo (to);
                StrokeOnly (path);
                Dc.Current = to;
                break;
            }
            case Meta.Polyline:
            case Meta.Polygon: {
                var points = ReadPoints (r, 2, r.Int16 (0));

                if (points.Length == 0)
                    break;

                using var path = BuildPath (points, close: r.Type == Meta.Polygon);

                if (r.Type == Meta.Polygon)
                    FillThenStroke (path);
                else
                    StrokeOnly (path);

                break;
            }
            case Meta.PolyPolygon: {
                var polygons = r.Int16 (0);

                if (polygons <= 0)
                    break;

                using var path = new SKPath ();
                var offset = 2 + (polygons * 2);

                for (var i = 0; i < polygons; i++) {
                    var count = r.Int16 (2 + (i * 2));
                    var points = ReadPoints (r, offset, count);

                    if (points.Length == 0)
                        break;

                    path.MoveTo (points[0]);

                    for (var p = 1; p < points.Length; p++)
                        path.LineTo (points[p]);

                    path.Close ();
                    offset += count * 4;
                }

                FillThenStroke (path);
                break;
            }

            // ── Closed shapes. WMF states rectangles bottom-right first. ────────
            case Meta.Rectangle: {
                using var path = new SKPath ();
                path.AddRect (ReadRect (r, 0));
                FillThenStroke (path);
                break;
            }
            case Meta.Ellipse: {
                using var path = new SKPath ();
                path.AddOval (ReadRect (r, 0));
                FillThenStroke (path);
                break;
            }
            case Meta.RoundRect: {
                // height(0) width(2) then the rectangle
                using var path = new SKPath ();
                path.AddRoundRect (ReadRect (r, 4), r.Int16 (2) / 2f, r.Int16 (0) / 2f);
                FillThenStroke (path);
                break;
            }
            case Meta.Arc:
            case Meta.Chord:
            case Meta.Pie: {
                // yEnd(0) xEnd(2) yStart(4) xStart(6) then the rectangle
                var kind = r.Type switch {
                    Meta.Pie => ArcKind.Pie,
                    Meta.Chord => ArcKind.Chord,
                    _ => ArcKind.Arc,
                };

                using var path = new SKPath ();
                AddArc (path, ReadRect (r, 8),
                    new SKPoint (r.Int16 (6), r.Int16 (4)),
                    new SKPoint (r.Int16 (2), r.Int16 (0)), kind);

                if (kind == ArcKind.Arc)
                    StrokeOnly (path);
                else
                    FillThenStroke (path);

                break;
            }
            case Meta.SetPixel: {
                ApplyTransform ();
                using var paint = new SKPaint { Color = ColorRef (r.UInt32 (0)) };
                Canvas.DrawPoint (r.Int16 (6), r.Int16 (4), paint);
                break;
            }

            case Meta.IntersectClipRect:
                ApplyTransform ();
                Canvas.ClipRect (ReadRect (r, 0));
                break;

            // ── Text ────────────────────────────────────────────────────────────
            case Meta.TextOut: {
                // count(0) string(2...) then y and x after the padded string
                var count = r.Int16 (0);

                if (count <= 0)
                    break;

                var text = System.Text.Encoding.Latin1.GetString (r.Data, 2, Math.Min (count, r.Data.Length - 2));
                var after = 2 + count + (count & 1);

                DrawText (r.Int16 (after + 2), r.Int16 (after), text);
                break;
            }
            case Meta.ExtTextOut: {
                // y(0) x(2) count(4) options(6) [rect(8..16)] string
                var count = r.Int16 (4);
                var options = r.Int16 (6);

                if (count <= 0)
                    break;

                // ETO_OPAQUE (2) and ETO_CLIPPED (4) each prefix a rectangle before the string.
                var offset = 8 + ((options & 6) != 0 ? 8 : 0);

                if (offset >= r.Data.Length)
                    break;

                var text = System.Text.Encoding.Latin1.GetString (r.Data, offset, Math.Min (count, r.Data.Length - offset));
                DrawText (r.Int16 (2), r.Int16 (0), text);
                break;
            }

            // ── Bitmaps ─────────────────────────────────────────────────────────
            case Meta.StretchDIB: {
                // rop(0) usage(4) srcHeight(6) srcWidth(8) srcY(10) srcX(12)
                // destHeight(14) destWidth(16) destY(18) destX(20) then the DIB
                var bits = DecodeDib (r, 22);
                DrawBits (bits, r.Int16 (20), r.Int16 (18), r.Int16 (16), r.Int16 (14));
                break;
            }
            case Meta.DibStretchBlt: {
                // rop(0..3) srcHeight(4) srcWidth(6) srcY(8) srcX(10)
                // destHeight(12) destWidth(14) destY(16) destX(18) then the DIB
                var bits = DecodeDib (r, 20);
                DrawBits (bits, r.Int16 (18), r.Int16 (16), r.Int16 (14), r.Int16 (12));
                break;
            }

            default:
                Skip ();
                break;
            }
        }

        // WMF names objects by their index in a table the metafile fills as it creates them; a
        // delete leaves a hole the next create reuses, so appending unconditionally would put every
        // later object one slot out.
        private void Add (object value)
        {
            var hole = table.IndexOf (null);

            if (hole >= 0)
                table[hole] = value;
            else
                table.Add (value);
        }

        private void Remove (int index)
        {
            if (index >= 0 && index < table.Count)
                table[index] = null;
        }

        private void Select (int index)
        {
            if (index < 0 || index >= table.Count)
                return;

            switch (table[index]) {
            case MetaPen pen: Dc.Pen = pen; break;
            case MetaBrush brush: Dc.Brush = brush; break;
            case MetaFont font: Dc.Font = font; break;
            }
        }

        private static SKSize Scale (SKSize extent, int xNum, int xDenom, int yNum, int yDenom) => new (
            xDenom == 0 ? extent.Width : extent.Width * xNum / xDenom,
            yDenom == 0 ? extent.Height : extent.Height * yNum / yDenom);

        private static SKPoint[] ReadPoints (MetafileRecord r, int offset, int count)
        {
            if (count <= 0)
                return [];

            var available = (r.Data.Length - offset) / 4;
            var points = new SKPoint[Math.Min (count, Math.Max (available, 0))];

            for (var i = 0; i < points.Length; i++)
                points[i] = new SKPoint (r.Int16 (offset + (i * 4)), r.Int16 (offset + (i * 4) + 2));

            return points;
        }

        // WMF rectangles are stored bottom, right, top, left -- reversed from EMF's left, top,
        // right, bottom, which is the single most common way to misread a WMF.
        private static SKRect ReadRect (MetafileRecord r, int offset)
            => SKRect.Create (r.Int16 (offset + 6), r.Int16 (offset + 4),
                r.Int16 (offset + 2) - r.Int16 (offset + 6),
                r.Int16 (offset) - r.Int16 (offset + 4)).Standardized;

        private static SKBitmap? DecodeDib (MetafileRecord r, int offset)
        {
            if (offset + 40 > r.Data.Length)
                return null;

            var headerLength = r.Int32 (offset);
            var bitCount = r.Int16 (offset + 14);
            var paletteEntries = r.Int32 (offset + 32);
            var colours = paletteEntries > 0 ? paletteEntries : bitCount <= 8 ? 1 << bitCount : 0;

            return DeviceIndependentBitmap.Decode (r.Data, offset, headerLength,
                offset + headerLength + (colours * 4), r.Data.Length - offset);
        }

        private static MetaFont ReadFont (MetafileRecord r)
        {
            // LOGFONT: height(0) width(2) escapement(4) orientation(6) weight(8) italic(10)
            // underline(11) strikeout(12) charset(13) ... faceName at 18, ANSI, up to 32 bytes.
            var height = Math.Abs (r.Int16 (0));
            var name = System.Text.Encoding.Latin1.GetString (r.Data, Math.Min (18, r.Data.Length),
                Math.Max (0, Math.Min (32, r.Data.Length - 18)));
            var end = name.IndexOf ('\0');

            if (end >= 0)
                name = name[..end];

            return new MetaFont {
                Height = height > 0 ? height : 12f,
                Escapement = r.Int16 (4),
                Bold = r.Int16 (8) >= 700,
                Italic = r.Data.Length > 10 && r.Data[10] != 0,
                Underline = r.Data.Length > 11 && r.Data[11] != 0,
                Strikeout = r.Data.Length > 12 && r.Data[12] != 0,
                Name = name.Length > 0 ? name : "Arial",
            };
        }
    }
}
