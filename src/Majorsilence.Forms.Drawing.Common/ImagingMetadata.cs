using System;
using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>
    /// A color palette, matching <c>System.Drawing.Imaging.ColorPalette</c>.
    /// </summary>
    /// <remarks>
    /// Reachable through <see cref="Majorsilence.Forms.Drawing.Image.Palette"/>. Modern SkiaSharp has no
    /// indexed (paletted) bitmap type — every surface here is 32bpp — so a palette is carried as data
    /// rather than driving how pixels are stored: it round-trips, and <see cref="ImageAttributes"/> can
    /// remap it, but assigning one does not re-quantize the image.
    /// </remarks>
    public sealed class ColorPalette
    {
        internal ColorPalette (int count) => Entries = new System.Drawing.Color[Math.Max (0, count)];

        internal ColorPalette (System.Drawing.Color[] entries) => Entries = entries ?? [];

        /// <summary>Gets the array of colors in this palette.</summary>
        public System.Drawing.Color[] Entries { get; }

        /// <summary>Gets flags describing the kind of color data in this palette.</summary>
        public int Flags { get; internal set; }
    }

    /// <summary>
    /// One piece of image metadata (an EXIF/TIFF tag), matching <c>System.Drawing.Imaging.PropertyItem</c>.
    /// </summary>
    public sealed class PropertyItem
    {
        // System.Drawing.Imaging.PropertyItem has no public constructor -- GDI+ only ever hands them
        // out from an image. Ours is internal for the same reason, with a factory for callers building
        // one to pass to SetPropertyItem.
        internal PropertyItem () { }

        /// <summary>Gets or sets the tag identifier (an EXIF/TIFF tag number).</summary>
        public int Id { get; set; }

        /// <summary>Gets or sets the length, in bytes, of <see cref="Value"/>.</summary>
        public int Len { get; set; }

        /// <summary>Gets or sets the tag's data type, as a TIFF type code.</summary>
        public short Type { get; set; }

        /// <summary>Gets or sets the tag's raw value.</summary>
        public byte[]? Value { get; set; }

        /// <summary>
        /// Creates a property item. System.Drawing has no public constructor for this type (GDI+ only
        /// returns them from an image), so this factory exists to make <c>SetPropertyItem</c> usable
        /// without one.
        /// </summary>
        public static PropertyItem Create (int id, short type, byte[] value)
            => new () { Id = id, Type = type, Value = value, Len = value?.Length ?? 0 };
    }

    /// <summary>
    /// Identifies the dimension along which an image holds multiple frames — time (animation) or page
    /// (multi-page documents). Matches <c>System.Drawing.Imaging.FrameDimension</c>.
    /// </summary>
    public sealed class FrameDimension
    {
        /// <summary>Initializes a new frame dimension with the specified GUID.</summary>
        public FrameDimension (Guid guid) => Guid = guid;

        /// <summary>Gets the GUID identifying this dimension.</summary>
        public Guid Guid { get; }

        /// <summary>The time dimension — the frames of an animation, e.g. an animated GIF.</summary>
        public static FrameDimension Time { get; } = new (new Guid ("6aedbd6d-3fb5-418a-83a6-7f45229dc872"));

        /// <summary>The resolution dimension.</summary>
        public static FrameDimension Resolution { get; } = new (new Guid ("84236f7b-3bd3-428f-8dab-4ea1439ca315"));

        /// <summary>The page dimension — the pages of a multi-page document, e.g. a multi-page TIFF.</summary>
        public static FrameDimension Page { get; } = new (new Guid ("7462dc86-6180-4c7e-8e3f-ee7333a7a483"));

        /// <inheritdoc/>
        public override bool Equals (object? obj) => obj is FrameDimension other && other.Guid == Guid;

        /// <inheritdoc/>
        public override int GetHashCode () => Guid.GetHashCode ();

        /// <inheritdoc/>
        public override string ToString () => this == Time ? "Time" : this == Resolution ? "Resolution" : this == Page ? "Page" : Guid.ToString ();
    }

    /// <summary>
    /// Reads EXIF metadata out of encoded image bytes into <see cref="PropertyItem"/>s.
    /// </summary>
    /// <remarks>
    /// SkiaSharp exposes only the orientation tag (<c>SKCodec.EncodedOrigin</c>), not the metadata
    /// block, so the JPEG APP1/TIFF structure is walked directly here. Deliberately narrow: it reads the
    /// primary IFD and the EXIF sub-IFD of a JPEG, which is where the tags real applications ask for
    /// (orientation, resolution, camera make/model, date taken) actually live. Anything else — PNG text
    /// chunks, maker notes, GPS sub-IFDs — is not parsed, and the image simply reports no such property.
    /// </remarks>
    internal static class ExifReader
    {
        // TIFF type -> bytes per component, indexed by the type code (1..12); 0 marks an unknown type.
        private static readonly int[] TypeSizes = [0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8];

        public static List<PropertyItem> Read (byte[]? data)
        {
            var items = new List<PropertyItem> ();
            if (data is null || data.Length < 4)
                return items;

            try {
                var tiffOffset = FindTiffHeader (data);
                if (tiffOffset < 0)
                    return items;

                var littleEndian = data[tiffOffset] == 0x49;
                var ifdOffset = ReadUInt32 (data, tiffOffset + 4, littleEndian);
                if (ifdOffset == 0)
                    return items;

                ReadIfd (data, tiffOffset, (int)ifdOffset, littleEndian, items, followSubIfd: true);
            } catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or OverflowException) {
                // Malformed metadata must never take down loading an otherwise valid image; report
                // whatever was parsed before the structure went bad.
            }

            return items;
        }

        // JPEG: scan the segment chain for APP1 with an "Exif\0\0" signature. A bare TIFF starts with
        // the byte-order mark itself.
        private static int FindTiffHeader (byte[] data)
        {
            if ((data[0] == 0x49 && data[1] == 0x49) || (data[0] == 0x4D && data[1] == 0x4D))
                return 0;

            if (data[0] != 0xFF || data[1] != 0xD8)
                return -1;      // not a JPEG

            var i = 2;
            while (i + 4 < data.Length && data[i] == 0xFF) {
                var marker = data[i + 1];
                var length = (data[i + 2] << 8) | data[i + 3];
                if (marker == 0xE1 && i + 10 < data.Length &&
                    data[i + 4] == 'E' && data[i + 5] == 'x' && data[i + 6] == 'i' && data[i + 7] == 'f')
                    return i + 10;      // skip "Exif\0\0"
                if (marker == 0xDA)
                    break;              // start of scan: no metadata beyond here
                i += 2 + length;
            }
            return -1;
        }

        private static void ReadIfd (byte[] data, int tiffOffset, int ifdOffset, bool littleEndian,
            List<PropertyItem> items, bool followSubIfd)
        {
            var entryBase = tiffOffset + ifdOffset;
            if (entryBase + 2 > data.Length)
                return;

            var count = ReadUInt16 (data, entryBase, littleEndian);
            for (var i = 0; i < count; i++) {
                var entry = entryBase + 2 + i * 12;
                if (entry + 12 > data.Length)
                    return;

                var id = ReadUInt16 (data, entry, littleEndian);
                var type = ReadUInt16 (data, entry + 2, littleEndian);
                var components = (int)ReadUInt32 (data, entry + 4, littleEndian);
                var unit = type < TypeSizes.Length ? TypeSizes[type] : 0;
                if (unit == 0)
                    continue;

                var length = unit * components;
                // Values of 4 bytes or fewer are stored inline in the entry itself.
                var valueOffset = length <= 4 ? entry + 8 : tiffOffset + (int)ReadUInt32 (data, entry + 8, littleEndian);
                if (valueOffset < 0 || valueOffset + length > data.Length)
                    continue;

                // 0x8769 is the pointer to the EXIF sub-IFD, where most interesting tags live.
                if (id == 0x8769 && followSubIfd) {
                    ReadIfd (data, tiffOffset, (int)ReadUInt32 (data, valueOffset, littleEndian), littleEndian, items, followSubIfd: false);
                    continue;
                }

                if (items.Any (x => x.Id == id))
                    continue;

                var value = new byte[length];
                Array.Copy (data, valueOffset, value, 0, length);
                items.Add (PropertyItem.Create (id, (short)type, value));
            }
        }

        private static ushort ReadUInt16 (byte[] d, int offset, bool littleEndian)
            => littleEndian ? (ushort)(d[offset] | (d[offset + 1] << 8)) : (ushort)((d[offset] << 8) | d[offset + 1]);

        private static uint ReadUInt32 (byte[] d, int offset, bool littleEndian)
            => littleEndian
                ? (uint)(d[offset] | (d[offset + 1] << 8) | (d[offset + 2] << 16) | (d[offset + 3] << 24))
                : (uint)((d[offset] << 24) | (d[offset + 1] << 16) | (d[offset + 2] << 8) | d[offset + 3]);
    }
}
