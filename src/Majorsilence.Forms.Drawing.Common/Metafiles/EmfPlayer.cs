using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging.Metafiles
{
    /// <summary>Interprets an enhanced metafile's records (MS-EMF).</summary>
    internal sealed class EmfPlayer (SKCanvas canvas, SKMatrix output, SKSize deviceExtent)
        : MetafilePlayer (canvas, output, deviceExtent)
    {
        // Offsets inside EMF records are stated from the start of the record, which includes the
        // eight-byte type/size prologue the reader has already stripped.
        private const int PrologueSize = 8;

        internal void Play (IReadOnlyList<MetafileRecord> records)
        {
            foreach (var record in records) {
                try {
                    Execute (record);
                } catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or OverflowException) {
                    // One malformed record must not cost the whole picture. The counter is what makes
                    // a systematically misread file visible instead of silently half-drawn.
                    Skip ();
                }
            }
        }

        private void Execute (MetafileRecord r)
        {
            switch (r.Type) {
            case Emr.Header:
            case Emr.Eof:
            case Emr.Comment:
                break;

            // ── Coordinate space ────────────────────────────────────────────────
            case Emr.SetWindowOrgEx:
                Dc.WindowOrigin = new SKPoint (r.Int32 (0), r.Int32 (4));
                Dc.ExtentsSet = true;
                break;
            case Emr.SetWindowExtEx:
                Dc.WindowExtent = new SKSize (r.Int32 (0), r.Int32 (4));
                Dc.ExtentsSet = true;
                break;
            case Emr.SetViewportOrgEx:
                Dc.ViewportOrigin = new SKPoint (r.Int32 (0), r.Int32 (4));
                Dc.ExtentsSet = true;
                break;
            case Emr.SetViewportExtEx:
                Dc.ViewportExtent = new SKSize (r.Int32 (0), r.Int32 (4));
                Dc.ExtentsSet = true;
                break;
            case Emr.SetMapMode:
                Dc.MapMode = r.Int32 (0);
                break;

            case Emr.SetWorldTransform:
                Dc.World = ReadTransform (r, 0);
                break;
            case Emr.ModifyWorldTransform: {
                var transform = ReadTransform (r, 0);

                // MWT_IDENTITY = 1, MWT_LEFTMULTIPLY = 2, MWT_RIGHTMULTIPLY = 3.
                Dc.World = r.Int32 (24) switch {
                    1 => SKMatrix.Identity,
                    2 => transform.PreConcat (Dc.World),
                    _ => Dc.World.PreConcat (transform),
                };

                break;
            }

            // ── Device-context state ────────────────────────────────────────────
            case Emr.SaveDC:
                SaveDc ();
                break;
            case Emr.RestoreDC:
                RestoreDc (r.Int32 (0));
                break;
            case Emr.SetTextColor:
                Dc.TextColor = ColorRef (r.UInt32 (0));
                break;
            case Emr.SetBkColor:
                Dc.BackColor = ColorRef (r.UInt32 (0));
                break;
            case Emr.SetBkMode:
                Dc.BackMode = r.Int32 (0);
                break;
            case Emr.SetTextAlign:
                Dc.TextAlign = r.Int32 (0);
                break;
            case Emr.SetPolyFillMode:
                Dc.PolyFillMode = r.Int32 (0);
                break;

            // ── Objects ─────────────────────────────────────────────────────────
            case Emr.CreatePen:
                StoreObject (r.Int32 (0), new MetaPen {
                    Style = r.Int32 (4),
                    Width = r.Int32 (8),
                    Color = ColorRef (r.UInt32 (16)),
                });
                break;
            case Emr.ExtCreatePen:
                // ihPen(0) offBmi(4) cbBmi(8) offBits(12) cbBits(16) then LOGPEN: style(20)
                // width(24) brushStyle(28) color(32).
                StoreObject (r.Int32 (0), new MetaPen {
                    Style = r.Int32 (20) & 0xF,
                    Width = r.Int32 (24),
                    Color = ColorRef (r.UInt32 (32)),
                });
                break;
            case Emr.CreateBrushIndirect:
                StoreObject (r.Int32 (0), new MetaBrush {
                    Style = r.Int32 (4),
                    Color = ColorRef (r.UInt32 (8)),
                    Hatch = r.Int32 (12),
                });
                break;
            case Emr.ExtCreateFontIndirectW:
                StoreObject (r.Int32 (0), ReadFont (r, 4));
                break;
            case Emr.SelectObject:
                SelectObject (r.Int32 (0));
                break;
            case Emr.DeleteObject:
                DeleteObject (r.Int32 (0));
                break;

            // ── Lines ───────────────────────────────────────────────────────────
            case Emr.MoveToEx:
                Dc.Current = new SKPoint (r.Int32 (0), r.Int32 (4));
                break;
            case Emr.LineTo: {
                var to = new SKPoint (r.Int32 (0), r.Int32 (4));
                using var path = new SKPath ();
                path.MoveTo (Dc.Current);
                path.LineTo (to);
                Emit (path, close: false);
                Dc.Current = to;
                break;
            }

            case Emr.Polyline:
            case Emr.Polyline16:
                DrawPoly (r, closed: false, continues: false);
                break;
            case Emr.PolylineTo:
            case Emr.PolylineTo16:
                DrawPoly (r, closed: false, continues: true);
                break;
            case Emr.Polygon:
            case Emr.Polygon16:
                DrawPoly (r, closed: true, continues: false);
                break;

            case Emr.PolyBezier:
            case Emr.PolyBezier16:
            case Emr.PolyBezierTo:
            case Emr.PolyBezierTo16: {
                var small = r.Type is Emr.PolyBezier16 or Emr.PolyBezierTo16;
                var continues = r.Type is Emr.PolyBezierTo or Emr.PolyBezierTo16;
                var points = ReadPoints (r, 16, r.Int32 (16), small, 20);

                using var path = BuildBezier (points, continues ? Dc.Current : null);
                Emit (path, close: false);

                if (points.Length > 0)
                    Dc.Current = points[^1];

                break;
            }

            case Emr.PolyPolyline:
            case Emr.PolyPolyline16:
                DrawPolyPoly (r, closed: false);
                break;
            case Emr.PolyPolygon:
            case Emr.PolyPolygon16:
                DrawPolyPoly (r, closed: true);
                break;

            // ── Closed shapes ───────────────────────────────────────────────────
            case Emr.Rectangle: {
                using var path = new SKPath ();
                path.AddRect (ReadRect (r, 0));
                Emit (path, close: true);
                break;
            }
            case Emr.Ellipse: {
                using var path = new SKPath ();
                path.AddOval (ReadRect (r, 0));
                Emit (path, close: true);
                break;
            }
            case Emr.RoundRect: {
                using var path = new SKPath ();
                path.AddRoundRect (ReadRect (r, 0), r.Int32 (16) / 2f, r.Int32 (20) / 2f);
                Emit (path, close: true);
                break;
            }
            case Emr.Arc:
            case Emr.Chord:
            case Emr.Pie: {
                var kind = r.Type switch {
                    Emr.Pie => ArcKind.Pie,
                    Emr.Chord => ArcKind.Chord,
                    _ => ArcKind.Arc,
                };

                using var path = new SKPath ();
                AddArc (path, ReadRect (r, 0),
                    new SKPoint (r.Int32 (16), r.Int32 (20)),
                    new SKPoint (r.Int32 (24), r.Int32 (28)), kind);

                Emit (path, close: kind != ArcKind.Arc);
                break;
            }

            case Emr.SetPixelV: {
                ApplyTransform ();
                using var paint = new SKPaint { Color = ColorRef (r.UInt32 (8)) };
                Canvas.DrawPoint (r.Int32 (0), r.Int32 (4), paint);
                break;
            }

            // ── Paths ───────────────────────────────────────────────────────────
            case Emr.BeginPath:
                RecordingPath?.Dispose ();
                RecordingPath = new SKPath ();
                RecordingPath.MoveTo (Dc.Current);
                break;
            case Emr.EndPath:
                CompletedPath?.Dispose ();
                CompletedPath = RecordingPath;
                RecordingPath = null;
                break;
            case Emr.CloseFigure:
                RecordingPath?.Close ();
                break;
            case Emr.AbortPath:
                RecordingPath?.Dispose ();
                RecordingPath = null;
                break;
            case Emr.FillPath:
            case Emr.StrokePath:
            case Emr.StrokeAndFillPath:
                if (CompletedPath is { } finished) {
                    if (r.Type == Emr.StrokePath)
                        StrokeOnly (finished);
                    else
                        FillThenStroke (finished);
                }

                break;
            case Emr.SelectClipPath:
                if (CompletedPath is { } clip) {
                    ApplyTransform ();
                    Canvas.ClipPath (clip);
                }

                break;

            // ── Clipping ────────────────────────────────────────────────────────
            case Emr.IntersectClipRect:
                ApplyTransform ();
                Canvas.ClipRect (ReadRect (r, 0));
                break;

            // ── Text ────────────────────────────────────────────────────────────
            case Emr.ExtTextOutA:
            case Emr.ExtTextOutW: {
                // EMRTEXT begins after Bounds(16) + iGraphicsMode(4) + exScale(4) + eyScale(4).
                const int TextAt = 28;

                var x = r.Int32 (TextAt);
                var y = r.Int32 (TextAt + 4);
                var count = r.Int32 (TextAt + 8);
                var offset = r.Int32 (TextAt + 12) - PrologueSize;

                if (count <= 0 || count > 0x10000 || offset < 0)
                    break;

                var text = r.Type == Emr.ExtTextOutW
                    ? ReadName (r.Data, offset, count)
                    : ReadAnsi (r.Data, offset, count);

                DrawText (x, y, text);
                break;
            }

            // ── Bitmaps ─────────────────────────────────────────────────────────
            case Emr.StretchDIBits: {
                // Bounds(16) xDest(16) yDest(20) xSrc(24) ySrc(28) cxSrc(32) cySrc(36)
                // offBmiSrc(40) cbBmiSrc(44) offBitsSrc(48) cbBitsSrc(52) iUsage(56) rop(60)
                // cxDest(64) cyDest(68)
                var bits = DeviceIndependentBitmap.Decode (r.Data,
                    r.Int32 (40) - PrologueSize, r.Int32 (44),
                    r.Int32 (48) - PrologueSize, r.Int32 (52));

                DrawBits (bits, r.Int32 (16), r.Int32 (20), r.Int32 (64), r.Int32 (68));
                break;
            }
            case Emr.BitBlt:
            case Emr.StretchBlt:
            case Emr.AlphaBlend: {
                // Bounds(16) xDest(16) yDest(20) cxDest(24) cyDest(28) rop(32) ... then the same
                // offBmi/cbBmi/offBits/cbBits quartet, after the source origin and transform.
                var bits = DeviceIndependentBitmap.Decode (r.Data,
                    r.Int32 (64) - PrologueSize, r.Int32 (68),
                    r.Int32 (72) - PrologueSize, r.Int32 (76));

                DrawBits (bits, r.Int32 (16), r.Int32 (20), r.Int32 (24), r.Int32 (28));
                break;
            }

            default:
                Skip ();
                break;
            }
        }

        // Emits a path as either an outline or a filled shape, honouring an open BeginPath: while a
        // path is being recorded, drawing records contribute to it instead of painting.
        private void Emit (SKPath path, bool close)
        {
            if (RecordingPath is { } recording) {
                recording.AddPath (path);
                return;
            }

            if (close)
                FillThenStroke (path);
            else
                StrokeOnly (path);
        }

        private void DrawPoly (MetafileRecord r, bool closed, bool continues)
        {
            var small = r.Type is Emr.Polyline16 or Emr.Polygon16 or Emr.PolylineTo16;
            var count = r.Int32 (16);
            var points = ReadPoints (r, 16, count, small, 20);

            if (points.Length == 0)
                return;

            using var path = new SKPath ();

            if (continues) {
                path.MoveTo (Dc.Current);

                foreach (var point in points)
                    path.LineTo (point);
            } else {
                path.MoveTo (points[0]);

                for (var i = 1; i < points.Length; i++)
                    path.LineTo (points[i]);

                if (closed)
                    path.Close ();
            }

            Emit (path, closed);
            Dc.Current = points[^1];
        }

        private void DrawPolyPoly (MetafileRecord r, bool closed)
        {
            var small = r.Type is Emr.PolyPolyline16 or Emr.PolyPolygon16;
            var polygons = r.Int32 (16);
            var total = r.Int32 (20);

            if (polygons <= 0 || polygons > 0x10000 || total <= 0 || total > 0x100000)
                return;

            var counts = new int[polygons];

            for (var i = 0; i < polygons; i++)
                counts[i] = r.Int32 (24 + (i * 4));

            var points = ReadPoints (r, 0, total, small, 24 + (polygons * 4));
            using var path = new SKPath ();
            var index = 0;

            foreach (var count in counts) {
                if (count <= 0 || index + count > points.Length)
                    break;

                path.MoveTo (points[index]);

                for (var i = 1; i < count; i++)
                    path.LineTo (points[index + i]);

                if (closed)
                    path.Close ();

                index += count;
            }

            Emit (path, closed);
        }

        private static SKPoint[] ReadPoints (MetafileRecord r, int _, int count, bool small, int offset)
        {
            if (count <= 0 || count > 0x100000)
                return [];

            var size = small ? 4 : 8;
            var available = (r.Data.Length - offset) / size;
            var points = new SKPoint[Math.Min (count, Math.Max (available, 0))];

            for (var i = 0; i < points.Length; i++) {
                var at = offset + (i * size);

                points[i] = small
                    ? new SKPoint (r.Int16 (at), r.Int16 (at + 2))
                    : new SKPoint (r.Int32 (at), r.Int32 (at + 4));
            }

            return points;
        }

        private static SKRect ReadRect (MetafileRecord r, int offset)
            => SKRect.Create (r.Int32 (offset), r.Int32 (offset + 4),
                r.Int32 (offset + 8) - r.Int32 (offset),
                r.Int32 (offset + 12) - r.Int32 (offset + 4)).Standardized;

        private static SKMatrix ReadTransform (MetafileRecord r, int offset) => new () {
            ScaleX = r.Single (offset),
            SkewY = r.Single (offset + 4),
            SkewX = r.Single (offset + 8),
            ScaleY = r.Single (offset + 12),
            TransX = r.Single (offset + 16),
            TransY = r.Single (offset + 20),
            Persp2 = 1f,
        };

        private static MetaFont ReadFont (MetafileRecord r, int offset)
        {
            // LOGFONTW: height(0) width(4) escapement(8) orientation(12) weight(16) italic(20)
            // underline(21) strikeout(22) charset(23) ... faceName at 28, 32 UTF-16 characters.
            var height = r.Int32 (offset);

            return new MetaFont {
                // Negative height is a character height, positive a cell height; the magnitude is
                // what a renderer needs either way.
                Height = Math.Abs (height) is var h && h > 0 ? h : 12f,
                Escapement = r.Int32 (offset + 8),
                Bold = r.Int32 (offset + 16) >= 700,
                Italic = offset + 20 < r.Data.Length && r.Data[offset + 20] != 0,
                Underline = offset + 21 < r.Data.Length && r.Data[offset + 21] != 0,
                Strikeout = offset + 22 < r.Data.Length && r.Data[offset + 22] != 0,
                Name = ReadName (r.Data, offset + 28, 32) is { Length: > 0 } name ? name : "Arial",
            };
        }

        private static string ReadAnsi (byte[] data, int offset, int count)
        {
            if (offset < 0 || offset >= data.Length)
                return string.Empty;

            var length = Math.Min (count, data.Length - offset);
            var end = Array.IndexOf (data, (byte) 0, offset, length);

            if (end >= 0)
                length = end - offset;

            return length <= 0 ? string.Empty : System.Text.Encoding.Latin1.GetString (data, offset, length);
        }
    }
}
