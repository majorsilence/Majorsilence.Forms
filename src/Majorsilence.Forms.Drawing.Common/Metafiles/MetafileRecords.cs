using System;
using System.Collections.Generic;
using System.IO;

namespace Majorsilence.Forms.Drawing.Imaging.Metafiles
{
    // Reading EMF and WMF record streams.
    //
    // Both formats are published specifications (MS-EMF, MS-WMF) and neither needs Windows to read:
    // a metafile is a length-prefixed sequence of records, each a little-endian struct. What needs
    // Windows is asking GDI to *replay* one -- which is why the players in MetafilePlayer.cs
    // interpret the records themselves rather than handing them to the OS.
    //
    // Both readers are deliberately tolerant. A metafile from an unknown producer routinely carries
    // records nobody documents, and a truncated one is common enough on the clipboard; in both cases
    // stopping at the bad record and drawing what came before beats throwing away the whole picture.

    /// <summary>One record from a metafile: its function code and its payload.</summary>
    internal readonly struct MetafileRecord (int type, byte[] data)
    {
        /// <summary>The record type -- an EMR_* constant for EMF, a META_* function for WMF.</summary>
        public int Type { get; } = type;

        /// <summary>The record body, excluding the type and size fields that precede it.</summary>
        public byte[] Data { get; } = data;

        public int Int32 (int offset) => offset + 4 <= Data.Length ? BitConverter.ToInt32 (Data, offset) : 0;

        public uint UInt32 (int offset) => offset + 4 <= Data.Length ? BitConverter.ToUInt32 (Data, offset) : 0u;

        public short Int16 (int offset) => offset + 2 <= Data.Length ? BitConverter.ToInt16 (Data, offset) : (short) 0;

        public ushort UInt16 (int offset) => offset + 2 <= Data.Length ? BitConverter.ToUInt16 (Data, offset) : (ushort) 0;

        public float Single (int offset) => offset + 4 <= Data.Length ? BitConverter.ToSingle (Data, offset) : 0f;
    }

    /// <summary>Reads the record stream of an enhanced metafile.</summary>
    internal static class EmfReader
    {
        /// <summary>Reads every record in an EMF, stopping at EOF or at the first malformed record.</summary>
        internal static List<MetafileRecord> Read (byte[] bytes)
        {
            var records = new List<MetafileRecord> ();
            var offset = 0;

            while (offset + 8 <= bytes.Length) {
                var type = BitConverter.ToInt32 (bytes, offset);
                var size = BitConverter.ToInt32 (bytes, offset + 4);

                // Every EMF record is a multiple of four bytes and at least the eight-byte prologue.
                // Anything else means the stream has gone out of step, and continuing from a
                // misaligned offset would decode noise as drawing commands.
                if (size < 8 || size % 4 != 0 || offset + size > bytes.Length)
                    break;

                var payload = new byte[size - 8];
                Array.Copy (bytes, offset + 8, payload, 0, payload.Length);
                records.Add (new MetafileRecord (type, payload));

                offset += size;

                if (type == Emr.Eof)
                    break;
            }

            return records;
        }
    }

    /// <summary>Reads the record stream of a Windows metafile.</summary>
    internal static class WmfReader
    {
        /// <summary>Reads every record in a WMF, skipping a placeable header if one is present.</summary>
        internal static List<MetafileRecord> Read (byte[] bytes, out short unitsPerInch, out System.Drawing.Rectangle bounds)
        {
            var records = new List<MetafileRecord> ();
            var offset = 0;

            unitsPerInch = 0;
            bounds = System.Drawing.Rectangle.Empty;

            // A placeable WMF prefixes the real metafile with a 22-byte header carrying the bounding
            // box and scale -- the only place a WMF records its own aspect ratio, so it is worth
            // reading rather than skipping blindly.
            if (bytes.Length >= 22 && BitConverter.ToUInt32 (bytes, 0) == 0x9AC6CDD7) {
                var left = BitConverter.ToInt16 (bytes, 6);
                var top = BitConverter.ToInt16 (bytes, 8);
                bounds = System.Drawing.Rectangle.FromLTRB (left, top,
                    BitConverter.ToInt16 (bytes, 10), BitConverter.ToInt16 (bytes, 12));
                unitsPerInch = BitConverter.ToInt16 (bytes, 18);
                offset = 22;
            }

            // Then the METAHEADER: 18 bytes, whose contents the player does not need.
            if (offset + 18 > bytes.Length)
                return records;

            offset += 18;

            while (offset + 6 <= bytes.Length) {
                // Size is in 16-bit words and counts the size and function fields themselves.
                var size = BitConverter.ToUInt32 (bytes, offset);
                var function = BitConverter.ToUInt16 (bytes, offset + 4);

                if (size < 3)
                    break;

                var total = checked ((long) size * 2);

                if (offset + total > bytes.Length)
                    break;

                var payload = new byte[total - 6];
                Array.Copy (bytes, offset + 6, payload, 0, payload.Length);
                records.Add (new MetafileRecord (function, payload));

                offset += (int) total;

                if (function == Meta.Eof)
                    break;
            }

            return records;
        }
    }

    /// <summary>EMF record types (MS-EMF), limited to the ones the player acts on.</summary>
    internal static class Emr
    {
        internal const int Header = 1;
        internal const int PolyBezier = 2;
        internal const int Polygon = 3;
        internal const int Polyline = 4;
        internal const int PolyBezierTo = 5;
        internal const int PolylineTo = 6;
        internal const int PolyPolyline = 7;
        internal const int PolyPolygon = 8;
        internal const int SetWindowExtEx = 9;
        internal const int SetWindowOrgEx = 10;
        internal const int SetViewportExtEx = 11;
        internal const int SetViewportOrgEx = 12;
        internal const int Eof = 14;
        internal const int SetPixelV = 15;
        internal const int SetMapMode = 17;
        internal const int SetBkMode = 18;
        internal const int SetPolyFillMode = 19;
        internal const int SetTextAlign = 22;
        internal const int SetTextColor = 24;
        internal const int SetBkColor = 25;
        internal const int MoveToEx = 27;
        internal const int IntersectClipRect = 30;
        internal const int SaveDC = 33;
        internal const int RestoreDC = 34;
        internal const int SetWorldTransform = 35;
        internal const int ModifyWorldTransform = 36;
        internal const int SelectObject = 37;
        internal const int CreatePen = 38;
        internal const int CreateBrushIndirect = 39;
        internal const int DeleteObject = 40;
        internal const int Ellipse = 42;
        internal const int Rectangle = 43;
        internal const int RoundRect = 44;
        internal const int Arc = 45;
        internal const int Chord = 46;
        internal const int Pie = 47;
        internal const int LineTo = 54;
        internal const int BeginPath = 59;
        internal const int EndPath = 60;
        internal const int CloseFigure = 61;
        internal const int FillPath = 62;
        internal const int StrokeAndFillPath = 63;
        internal const int StrokePath = 64;
        internal const int SelectClipPath = 67;
        internal const int AbortPath = 68;
        internal const int Comment = 70;
        internal const int BitBlt = 76;
        internal const int StretchBlt = 77;
        internal const int StretchDIBits = 81;
        internal const int ExtCreateFontIndirectW = 82;
        internal const int ExtTextOutA = 83;
        internal const int ExtTextOutW = 84;
        internal const int PolyBezier16 = 85;
        internal const int Polygon16 = 86;
        internal const int Polyline16 = 87;
        internal const int PolyBezierTo16 = 88;
        internal const int PolylineTo16 = 89;
        internal const int PolyPolyline16 = 90;
        internal const int PolyPolygon16 = 91;
        internal const int ExtCreatePen = 95;
        internal const int AlphaBlend = 114;
    }

    /// <summary>WMF record functions (MS-WMF), limited to the ones the player acts on.</summary>
    internal static class Meta
    {
        internal const int Eof = 0x0000;
        internal const int SaveDC = 0x001E;
        internal const int SetBkMode = 0x0102;
        internal const int SetMapMode = 0x0103;
        internal const int SetRop2 = 0x0104;
        internal const int SetPolyFillMode = 0x0106;
        internal const int SetStretchBltMode = 0x0107;
        internal const int RestoreDC = 0x0127;
        internal const int SelectObject = 0x012D;
        internal const int SetTextAlign = 0x012E;
        internal const int DeleteObject = 0x01F0;
        internal const int SetBkColor = 0x0201;
        internal const int SetTextColor = 0x0209;
        internal const int SetWindowOrg = 0x020B;
        internal const int SetWindowExt = 0x020C;
        internal const int SetViewportOrg = 0x020D;
        internal const int SetViewportExt = 0x020E;
        internal const int OffsetWindowOrg = 0x020F;
        internal const int OffsetViewportOrg = 0x0211;
        internal const int LineTo = 0x0213;
        internal const int MoveTo = 0x0214;
        internal const int CreatePenIndirect = 0x02FA;
        internal const int CreateFontIndirect = 0x02FB;
        internal const int CreateBrushIndirect = 0x02FC;
        internal const int Polygon = 0x0324;
        internal const int Polyline = 0x0325;
        internal const int ScaleWindowExt = 0x0410;
        internal const int ScaleViewportExt = 0x0412;
        internal const int IntersectClipRect = 0x0416;
        internal const int Ellipse = 0x0418;
        internal const int Rectangle = 0x041B;
        internal const int SetPixel = 0x041F;
        internal const int TextOut = 0x0521;
        internal const int PolyPolygon = 0x0538;
        internal const int RoundRect = 0x061C;
        internal const int Arc = 0x0817;
        internal const int Pie = 0x081A;
        internal const int Chord = 0x0830;
        internal const int ExtTextOut = 0x0A32;
        internal const int StretchDIB = 0x0F43;
        internal const int DibStretchBlt = 0x0B41;
    }
}
