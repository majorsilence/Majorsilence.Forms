using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Reads the <em>undecoded</em> payload bytes of entries in a compiled <c>.resources</c> binary,
    /// so <see cref="ComponentResourceManager"/> can recover entries that
    /// <c>System.Resources.Extensions.DeserializingResourceReader</c> will not hand back.
    /// </summary>
    /// <remarks>
    /// The reader deserializes eagerly: asking it for the value of an entry the original designer wrote
    /// through <c>BinaryFormatter</c> throws <see cref="PlatformNotSupportedException"/> outright, because
    /// BinaryFormatter was removed in .NET 9. That is not a corrupt resource — the bytes are a perfectly
    /// readable NRBF payload, and <see cref="NrbfResourceReader"/> already decodes the shapes that matter
    /// (Bitmap, Icon, ImageListStreamer). The only thing missing is a way to get at the bytes: the BCL's
    /// <c>ResourceReader.GetResourceData</c> would do it, but it refuses the file (the header names
    /// DeserializingResourceReader as the reader), and the extensions reader has no such method.
    /// Hence this: enough of the v2 <c>RuntimeResourceSet</c> container to locate an entry and return its
    /// raw payload, with no deserialization of any kind.
    ///
    /// Concretely, without this every <c>ImageList.ImageStream</c> in a migrated app silently stayed null,
    /// so a designer-populated toolbar rendered with all of its button images missing.
    /// </remarks>
    internal static class RawResourcesReader
    {
        // System.Resources.Extensions.SerializationFormat, as written into the data section ahead of
        // each user-typed payload.
        internal enum RawFormat
        {
            BinaryFormatter = 1,
            TypeConverterByteArray = 2,
            TypeConverterString = 3,
            ActivatorStream = 4,
        }

        internal readonly record struct RawEntry (string TypeName, RawFormat Format, byte[] Data);

        // Primitive ResourceTypeCodes occupy 0..0x3F; user types start here and index the type table.
        private const int StartOfUserTypes = 0x40;

        private const int MagicNumber = unchecked ((int) 0xBEEFCACE);

        /// <summary>
        /// Returns the raw payload of every user-typed entry whose name satisfies <paramref name="wanted"/>.
        /// Malformed or unsupported input yields whatever was read up to that point — never an exception,
        /// since this is a recovery path for resources that have already failed to load once.
        /// </summary>
        internal static Dictionary<string, RawEntry> Read (byte[] file, Func<string, bool> wanted)
        {
            var found = new Dictionary<string, RawEntry> (StringComparer.Ordinal);

            try
            {
                using var stream = new MemoryStream (file, writable: false);
                using var reader = new BinaryReader (stream, Encoding.UTF8);

                if (reader.ReadInt32 () != MagicNumber)
                    return found;

                var headerVersion = reader.ReadInt32 ();
                var headerSize = reader.ReadInt32 ();

                if (headerVersion > 1)
                {
                    // A newer header than we know how to read past field-by-field; it is sized precisely
                    // so it can be skipped whole.
                    stream.Seek (headerSize, SeekOrigin.Current);
                }
                else
                {
                    var readerTypeName = reader.ReadString ();
                    reader.ReadString ();   // resource set type name — not needed.

                    // Only the extensions writer's layout is parsed here: it length-prefixes every user
                    // payload and tags it with a SerializationFormat, which is what makes a payload
                    // extractable on its own. A file written by the plain BCL ResourceWriter stores
                    // BinaryFormatter graphs with neither, so an entry's extent is only implied by where
                    // the next one starts — not handled, and not something a modern SDK build produces.
                    if (!readerTypeName.StartsWith ("System.Resources.Extensions.DeserializingResourceReader",
                                                    StringComparison.Ordinal))
                        return found;
                }

                if (reader.ReadInt32 () != 2)   // RuntimeResourceSet version
                    return found;

                var numResources = reader.ReadInt32 ();
                var numTypes = reader.ReadInt32 ();

                if (numResources < 0 || numTypes < 0)
                    return found;

                var typeNames = new string[numTypes];
                for (var i = 0; i < numTypes; i++)
                    typeNames[i] = reader.ReadString ();

                // The hash/position tables are 8-byte aligned.
                var misalignment = (int) (stream.Position & 7);
                if (misalignment != 0)
                    stream.Seek (8 - misalignment, SeekOrigin.Current);

                stream.Seek (numResources * 4L, SeekOrigin.Current);   // name hashes — we match by name.

                var namePositions = new int[numResources];
                for (var i = 0; i < numResources; i++)
                    namePositions[i] = reader.ReadInt32 ();

                var dataSectionOffset = reader.ReadInt32 ();
                var nameSectionOffset = stream.Position;

                for (var i = 0; i < numResources; i++)
                {
                    var namePosition = nameSectionOffset + namePositions[i];
                    if (namePosition < 0 || namePosition >= stream.Length)
                        continue;

                    stream.Position = namePosition;

                    var nameByteCount = reader.Read7BitEncodedIntCompat ();
                    if (nameByteCount < 0 || nameByteCount > stream.Length - stream.Position)
                        continue;

                    // Names are UTF-16, unlike the UTF-8 type names above.
                    var name = Encoding.Unicode.GetString (reader.ReadBytes (nameByteCount));
                    var dataPosition = dataSectionOffset + reader.ReadInt32 ();

                    if (!wanted (name) || dataPosition < 0 || dataPosition >= stream.Length)
                        continue;

                    stream.Position = dataPosition;

                    var typeIndex = reader.Read7BitEncodedIntCompat () - StartOfUserTypes;
                    if (typeIndex < 0 || typeIndex >= numTypes)
                        continue;   // a primitive (string/int/…) — the normal reader handled it already.

                    var format = (RawFormat) reader.Read7BitEncodedIntCompat ();
                    var length = reader.Read7BitEncodedIntCompat ();

                    if (length < 0 || length > stream.Length - stream.Position)
                        continue;

                    found[name] = new RawEntry (typeNames[typeIndex], format, reader.ReadBytes (length));
                }
            }
            catch (Exception e) when (e is IOException or EndOfStreamException or FormatException
                                        or ArgumentException or OverflowException)
            {
                // Truncated or not the layout described above — return the entries already recovered.
            }

            return found;
        }
    }
}
