using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Majorsilence.Forms.Tests
{
    // A test proving that a family resolves only because a PrivateFontCollection holds it needs a family that no host
    // has installed. The bundled fonts' own names do not qualify: Caladea, for one, is installed on some machines, and
    // there the system font answered for it whether the collection did or not. A test of that kind then failed when it
    // checked that the family did not resolve without the collection, and passed without proving anything when it
    // checked that the family did resolve with it.
    //
    // So those tests load the bundled Caladea under a family name of their own: the same glyphs and metrics, with the
    // font's name table replaced.
    internal static class PrivateTestFont
    {
        // A family name nothing installs.
        internal const string Family = "Private Only Family 7C31D2";

        private static readonly Lazy<byte[]> data = new (() => Renamed (File.ReadAllBytes (BundledFile), Family));

        internal static string BundledFile => Path.Combine (Majorsilence.Forms.Drawing.FontResourceLoader.GetFontDirectory (), "Caladea-Regular.ttf");

        // The bundled Caladea Regular, its family named Family.
        internal static byte[] Data => data.Value;

        // A copy of an sfnt (TrueType or OpenType) font whose name table gives family as its family, full and PostScript
        // name. The new table is appended and the directory pointed at it; checksums are recomputed.
        internal static byte[] Renamed (byte[] font, string family)
        {
            var nameTable = NameTable (family);
            var nameOffset = Align4 (font.Length);
            var result = new byte[nameOffset + Align4 (nameTable.Length)];
            font.CopyTo (result, 0);
            nameTable.CopyTo (result, nameOffset);

            var numTables = BinaryPrimitives.ReadUInt16BigEndian (result.AsSpan (4));
            var headOffset = -1;
            var renamed = false;

            for (var i = 0; i < numTables; i++) {
                var entry = result.AsSpan (12 + (16 * i), 16);
                var tag = Encoding.ASCII.GetString (entry[..4]);

                if (tag == "name") {
                    BinaryPrimitives.WriteUInt32BigEndian (entry[4..], Checksum (result.AsSpan (nameOffset, Align4 (nameTable.Length))));
                    BinaryPrimitives.WriteUInt32BigEndian (entry[8..], (uint) nameOffset);
                    BinaryPrimitives.WriteUInt32BigEndian (entry[12..], (uint) nameTable.Length);
                    renamed = true;
                } else if (tag == "head") {
                    headOffset = (int) BinaryPrimitives.ReadUInt32BigEndian (entry[8..]);
                }
            }

            if (!renamed || headOffset < 0)
                throw new InvalidDataException ("not an sfnt font with name and head tables");

            // head.checkSumAdjustment makes the whole file sum to 0xB1B0AFBA; it is summed as zero.
            BinaryPrimitives.WriteUInt32BigEndian (result.AsSpan (headOffset + 8), 0);
            BinaryPrimitives.WriteUInt32BigEndian (result.AsSpan (headOffset + 8), unchecked (0xB1B0AFBA - Checksum (result)));

            return result;
        }

        // A format 0 name table with Windows (platform 3, Unicode BMP, US English) records only.
        private static byte[] NameTable (string family)
        {
            (ushort id, string value)[] records = [
                (1, family),                        // family
                (2, "Regular"),                     // subfamily
                (3, family + " Regular"),           // unique identifier
                (4, family),                        // full name
                (6, family.Replace (" ", "")),      // PostScript name
            ];

            var stringOffset = 6 + (12 * records.Length);
            using var table = new MemoryStream ();
            using var strings = new MemoryStream ();
            var u16 = new byte[2];

            void Write (ushort value)
            {
                BinaryPrimitives.WriteUInt16BigEndian (u16, value);
                table.Write (u16);
            }

            Write (0);
            Write ((ushort) records.Length);
            Write ((ushort) stringOffset);

            foreach (var (id, value) in records) {
                var bytes = Encoding.BigEndianUnicode.GetBytes (value);
                Write (3);
                Write (1);
                Write (0x0409);
                Write (id);
                Write ((ushort) bytes.Length);
                Write ((ushort) strings.Length);
                strings.Write (bytes);
            }

            strings.WriteTo (table);
            return table.ToArray ();
        }

        // The sum of a 4-byte-aligned span's big-endian 32-bit words.
        private static uint Checksum (ReadOnlySpan<byte> bytes)
        {
            uint sum = 0;
            for (var i = 0; i + 4 <= bytes.Length; i += 4)
                sum = unchecked (sum + BinaryPrimitives.ReadUInt32BigEndian (bytes[i..]));
            return sum;
        }

        private static int Align4 (int length) => (length + 3) & ~3;
    }
}
