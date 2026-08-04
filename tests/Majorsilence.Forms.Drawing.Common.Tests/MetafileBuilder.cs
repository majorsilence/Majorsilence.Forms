using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // Builds real EMF and WMF byte streams for the playback tests.
    //
    // The tests need genuine metafiles, and the platform cannot produce one here -- that is the whole
    // reason the players exist. So they are assembled byte by byte from the published record layouts
    // (MS-EMF, MS-WMF). That is the point: a player tested against files this builder produced is
    // tested against the spec, not against a mock of itself.
    internal sealed class EmfBuilder
    {
        private readonly List<byte[]> records = [];

        /// <summary>Appends a record: its type, then its payload, with the size filled in.</summary>
        internal EmfBuilder Record (int type, params byte[] payload)
        {
            // Every EMF record is padded to a four-byte boundary.
            var padded = (payload.Length + 3) & ~3;
            var record = new byte[8 + padded];

            BitConverter.TryWriteBytes (record.AsSpan (0), type);
            BitConverter.TryWriteBytes (record.AsSpan (4), record.Length);
            payload.CopyTo (record, 8);
            records.Add (record);

            return this;
        }

        internal EmfBuilder SetWindowOrg (int x, int y) => Record (10, Ints (x, y));

        internal EmfBuilder SetWindowExt (int cx, int cy) => Record (9, Ints (cx, cy));

        internal EmfBuilder SetViewportOrg (int x, int y) => Record (12, Ints (x, y));

        internal EmfBuilder SetViewportExt (int cx, int cy) => Record (11, Ints (cx, cy));

        internal EmfBuilder CreateBrush (int handle, uint colour, int style = 0, int hatch = 0)
            => Record (39, Ints (handle, style, (int) colour, hatch));

        internal EmfBuilder CreatePen (int handle, uint colour, int width, int style = 0)
            => Record (38, Ints (handle, style, width, 0, (int) colour));

        internal EmfBuilder SelectObject (int handle) => Record (37, Ints (handle));

        internal EmfBuilder SelectStock (int index) => Record (37, Ints (unchecked ((int) (0x80000000 | (uint) index))));

        internal EmfBuilder Rectangle (int l, int t, int r, int b) => Record (43, Ints (l, t, r, b));

        internal EmfBuilder Ellipse (int l, int t, int r, int b) => Record (42, Ints (l, t, r, b));

        internal EmfBuilder MoveTo (int x, int y) => Record (27, Ints (x, y));

        internal EmfBuilder LineTo (int x, int y) => Record (54, Ints (x, y));

        internal EmfBuilder Polygon16 (params (short X, short Y)[] points)
        {
            var payload = new byte[16 + 4 + (points.Length * 4)];

            // Bounds are advisory; the player reads the count and the points.
            BitConverter.TryWriteBytes (payload.AsSpan (16), points.Length);

            for (var i = 0; i < points.Length; i++) {
                BitConverter.TryWriteBytes (payload.AsSpan (20 + (i * 4)), points[i].X);
                BitConverter.TryWriteBytes (payload.AsSpan (22 + (i * 4)), points[i].Y);
            }

            return Record (86, payload);
        }

        internal EmfBuilder SetTextColor (uint colour) => Record (24, Ints ((int) colour));

        internal EmfBuilder SetBkMode (int mode) => Record (18, Ints (mode));

        internal EmfBuilder SetWorldTransform (float m11, float m12, float m21, float m22, float dx, float dy)
        {
            var payload = new byte[24];

            BitConverter.TryWriteBytes (payload.AsSpan (0), m11);
            BitConverter.TryWriteBytes (payload.AsSpan (4), m12);
            BitConverter.TryWriteBytes (payload.AsSpan (8), m21);
            BitConverter.TryWriteBytes (payload.AsSpan (12), m22);
            BitConverter.TryWriteBytes (payload.AsSpan (16), dx);
            BitConverter.TryWriteBytes (payload.AsSpan (20), dy);

            return Record (35, payload);
        }

        internal EmfBuilder SaveDc () => Record (33);

        internal EmfBuilder RestoreDc (int level) => Record (34, Ints (level));

        internal EmfBuilder ExtTextOutW (int x, int y, string text)
        {
            var chars = Encoding.Unicode.GetBytes (text);
            var payload = new byte[28 + 40 + chars.Length];

            // EMRTEXT at 28: reference point, character count, then the offset to the string --
            // which is stated from the start of the record, so the eight-byte prologue counts.
            BitConverter.TryWriteBytes (payload.AsSpan (28), x);
            BitConverter.TryWriteBytes (payload.AsSpan (32), y);
            BitConverter.TryWriteBytes (payload.AsSpan (36), text.Length);
            BitConverter.TryWriteBytes (payload.AsSpan (40), 8 + 68);
            chars.CopyTo (payload, 68);

            return Record (84, payload);
        }

        /// <summary>Emits the whole metafile: header, records, EOF.</summary>
        internal byte[] Build (int left, int top, int right, int bottom)
        {
            var body = new List<byte> ();

            foreach (var record in records)
                body.AddRange (record);

            // EMR_EOF
            body.AddRange (new byte[] { 14, 0, 0, 0, 20, 0, 0, 0 });
            body.AddRange (new byte[12]);

            var header = new byte[88];
            BitConverter.TryWriteBytes (header.AsSpan (0), 1);                    // EMR_HEADER
            BitConverter.TryWriteBytes (header.AsSpan (4), 88);                   // header size
            BitConverter.TryWriteBytes (header.AsSpan (8), left);
            BitConverter.TryWriteBytes (header.AsSpan (12), top);
            BitConverter.TryWriteBytes (header.AsSpan (16), right);
            BitConverter.TryWriteBytes (header.AsSpan (20), bottom);
            BitConverter.TryWriteBytes (header.AsSpan (40), 0x464D4520u);         // " EMF"
            BitConverter.TryWriteBytes (header.AsSpan (44), 0x10000);             // version
            BitConverter.TryWriteBytes (header.AsSpan (48), 88 + body.Count);     // total size
            BitConverter.TryWriteBytes (header.AsSpan (72), 1920);                // device pixels
            BitConverter.TryWriteBytes (header.AsSpan (80), 508);                 // device mm -> 96 dpi

            var all = new byte[88 + body.Count];
            header.CopyTo (all, 0);
            body.CopyTo (all, 88);

            return all;
        }

        internal Stream BuildStream (int left, int top, int right, int bottom)
            => new MemoryStream (Build (left, top, right, bottom));

        private static byte[] Ints (params int[] values)
        {
            var bytes = new byte[values.Length * 4];

            for (var i = 0; i < values.Length; i++)
                BitConverter.TryWriteBytes (bytes.AsSpan (i * 4), values[i]);

            return bytes;
        }
    }

    /// <summary>Builds a placeable Windows metafile.</summary>
    internal sealed class WmfBuilder
    {
        private readonly List<byte[]> records = [];

        /// <summary>Appends a record: its function, then its 16-bit parameters.</summary>
        internal WmfBuilder Record (int function, params short[] parameters)
        {
            // Size is in words and counts the size and function fields themselves.
            var words = 3 + parameters.Length;
            var record = new byte[words * 2];

            BitConverter.TryWriteBytes (record.AsSpan (0), (uint) words);
            BitConverter.TryWriteBytes (record.AsSpan (4), (ushort) function);

            for (var i = 0; i < parameters.Length; i++)
                BitConverter.TryWriteBytes (record.AsSpan (6 + (i * 2)), parameters[i]);

            records.Add (record);
            return this;
        }

        internal WmfBuilder SetWindowOrg (short x, short y) => Record (0x020B, y, x);

        internal WmfBuilder SetWindowExt (short cx, short cy) => Record (0x020C, cy, cx);

        // LOGBRUSH: style, then the colour as two words, then hatch.
        internal WmfBuilder CreateBrush (uint colour, short style = 0, short hatch = 0)
            => Record (0x02FC, style, (short) (colour & 0xFFFF), (short) ((colour >> 16) & 0xFFFF), hatch);

        internal WmfBuilder CreatePen (uint colour, short width, short style = 0)
            => Record (0x02FA, style, width, 0, (short) (colour & 0xFFFF), (short) ((colour >> 16) & 0xFFFF));

        internal WmfBuilder SelectObject (short index) => Record (0x012D, index);

        // WMF states rectangles bottom, right, top, left.
        internal WmfBuilder Rectangle (short l, short t, short r, short b) => Record (0x041B, b, r, t, l);

        internal WmfBuilder Ellipse (short l, short t, short r, short b) => Record (0x0418, b, r, t, l);

        internal WmfBuilder MoveTo (short x, short y) => Record (0x0214, y, x);

        internal WmfBuilder LineTo (short x, short y) => Record (0x0213, y, x);

        internal WmfBuilder Polygon (params (short X, short Y)[] points)
        {
            var parameters = new short[1 + (points.Length * 2)];
            parameters[0] = (short) points.Length;

            for (var i = 0; i < points.Length; i++) {
                parameters[1 + (i * 2)] = points[i].X;
                parameters[2 + (i * 2)] = points[i].Y;
            }

            return Record (0x0324, parameters);
        }

        /// <summary>Emits the whole metafile: placeable header, METAHEADER, records, EOF.</summary>
        internal byte[] Build (short left, short top, short right, short bottom, short unitsPerInch = 96)
        {
            var body = new List<byte> ();

            foreach (var record in records)
                body.AddRange (record);

            body.AddRange (new byte[] { 3, 0, 0, 0, 0, 0 }); // META_EOF

            var all = new byte[22 + 18 + body.Count];

            BitConverter.TryWriteBytes (all.AsSpan (0), 0x9AC6CDD7u);
            BitConverter.TryWriteBytes (all.AsSpan (6), left);
            BitConverter.TryWriteBytes (all.AsSpan (8), top);
            BitConverter.TryWriteBytes (all.AsSpan (10), right);
            BitConverter.TryWriteBytes (all.AsSpan (12), bottom);
            BitConverter.TryWriteBytes (all.AsSpan (18), unitsPerInch);

            // METAHEADER: type, header size in words, version.
            BitConverter.TryWriteBytes (all.AsSpan (22), (short) 1);
            BitConverter.TryWriteBytes (all.AsSpan (24), (short) 9);
            BitConverter.TryWriteBytes (all.AsSpan (26), (short) 0x300);

            body.CopyTo (all, 40);
            return all;
        }

        internal Stream BuildStream (short left, short top, short right, short bottom, short unitsPerInch = 96)
            => new MemoryStream (Build (left, top, right, bottom, unitsPerInch));
    }
}
