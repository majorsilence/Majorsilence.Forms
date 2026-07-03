using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Imaging
{
    /// <summary>
    /// Writes multi-page, little-endian TIFF files to a seekable stream without external
    /// dependencies. Supports color (RGB, uncompressed) and bitonal (1bpp, uncompressed)
    /// output at arbitrary DPI.
    /// </summary>
    public sealed class TiffWriter : IDisposable
    {
        private readonly Stream _stream;
        private long _prevNextIfdPos = -1;
        private int _pageIndex;
        private bool _disposed;

        // Tag numbers in the order required by the TIFF spec (ascending).
        private const ushort TagNewSubfileType            = 254;
        private const ushort TagImageWidth                = 256;
        private const ushort TagImageLength               = 257;
        private const ushort TagBitsPerSample             = 258;
        private const ushort TagCompression               = 259;
        private const ushort TagPhotometricInterpretation = 262;
        private const ushort TagStripOffsets              = 273;
        private const ushort TagSamplesPerPixel           = 277;
        private const ushort TagRowsPerStrip              = 278;
        private const ushort TagStripByteCounts           = 279;
        private const ushort TagXResolution               = 282;
        private const ushort TagYResolution               = 283;
        private const ushort TagResolutionUnit            = 296;
        private const ushort TagPageNumber                = 297;

        // TIFF field types
        private const ushort TypeShort    = 3;
        private const ushort TypeLong     = 4;
        private const ushort TypeRational = 5;

        public TiffWriter(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            WriteFileHeader();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Encode one page. Call for each page in document order.
        /// <paramref name="color"/>: true = RGB (24bpp), false = bitonal (1bpp).
        /// Threshold for bitonal: pixels whose R+G+B sum exceeds 500 become white.
        /// </summary>
        public void WritePage(SKBitmap bitmap, bool color, float dpiX, float dpiY)
        {
#if NET8_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_disposed, this);
#else
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
#endif

            int width  = bitmap.Width;
            int height = bitmap.Height;
            SKColor[] pixels = bitmap.Pixels;

            byte[] imageData;
            int bitsPerSample, samplesPerPixel, photometric;

            if (color)
            {
                imageData = BuildColorStrip(pixels, width, height);
                bitsPerSample  = 8;
                samplesPerPixel = 3;
                photometric    = 2; // RGB
            }
            else
            {
                imageData = BuildBitonalStrip(pixels, width, height);
                bitsPerSample  = 1;
                samplesPerPixel = 1;
                photometric    = 1; // MinIsBlack
            }

            uint stripOffset = (uint)_stream.Position;
            _stream.Write(imageData, 0, imageData.Length);

            WriteIfd(width, height, bitsPerSample, samplesPerPixel, photometric,
                     stripOffset, (uint)imageData.Length, dpiX, dpiY);

            _pageIndex++;
        }

        public void Finish() => _stream.Flush();

        public void Dispose()
        {
            _disposed = true;
        }

        // ── Image data builders ───────────────────────────────────────────────

        private static byte[] BuildColorStrip(SKColor[] pixels, int width, int height)
        {
            byte[] data = new byte[width * height * 3];
            int dst = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                data[dst++] = pixels[i].Red;
                data[dst++] = pixels[i].Green;
                data[dst++] = pixels[i].Blue;
            }
            return data;
        }

        private static byte[] BuildBitonalStrip(SKColor[] pixels, int width, int height)
        {
            int stride = (width + 7) / 8;
            byte[] data = new byte[stride * height];
            const int threshold = 500;
            for (int row = 0; row < height; row++)
            {
                int rowBase = row * width;
                for (int col = 0; col < width; col++)
                {
                    SKColor p = pixels[rowBase + col];
                    if (p.Red + p.Green + p.Blue > threshold)
                        data[row * stride + (col >> 3)] |= (byte)(0x80 >> (col & 7));
                }
            }
            return data;
        }

        // ── IFD writer ────────────────────────────────────────────────────────

        private void WriteIfd(int width, int height,
            int bitsPerSample, int samplesPerPixel, int photometric,
            uint stripOffset, uint stripByteCount,
            float dpiX, float dpiY)
        {
            bool color     = samplesPerPixel == 3;
            const int nEntries = 14;

            uint ifdPos = (uint)_stream.Position;

            // Patch the file-header first-IFD pointer, or the previous page's next-IFD pointer.
            if (_pageIndex == 0)
                PatchU32(4, ifdPos);
            else if (_prevNextIfdPos >= 0)
                PatchU32(_prevNextIfdPos, ifdPos);

            Write2(nEntries);

            // Extra-data region starts right after:  2 + 14*12 + 4 = 174 bytes from ifdPos.
            long extraBase = _stream.Position + (long)nEntries * 12 + 4;
            uint extraPos  = (uint)extraBase;

            // Reserve space for color BitsPerSample array (3×SHORT = 6 bytes).
            uint bpsArrayOffset = extraPos;
            if (color) extraPos += 6;

            // Reserve space for XResolution and YResolution (each RATIONAL = 8 bytes).
            uint xResOffset = extraPos; extraPos += 8;
            uint yResOffset = extraPos;

            // Entries must be in ascending tag order.
            WriteEntry(TagNewSubfileType,            TypeLong,  1, 2u);
            WriteEntry(TagImageWidth,                TypeLong,  1, (uint)width);
            WriteEntry(TagImageLength,               TypeLong,  1, (uint)height);

            if (color)
                WriteEntry(TagBitsPerSample, TypeShort, 3, bpsArrayOffset);
            else
                WriteEntry(TagBitsPerSample, TypeShort, 1, (uint)bitsPerSample);

            WriteEntry(TagCompression,               TypeShort, 1, 1u);   // 1 = no compression
            WriteEntry(TagPhotometricInterpretation, TypeShort, 1, (uint)photometric);
            WriteEntry(TagStripOffsets,              TypeLong,  1, stripOffset);
            WriteEntry(TagSamplesPerPixel,           TypeShort, 1, (uint)samplesPerPixel);
            WriteEntry(TagRowsPerStrip,              TypeLong,  1, (uint)height);
            WriteEntry(TagStripByteCounts,           TypeLong,  1, stripByteCount);
            WriteEntry(TagXResolution,               TypeRational, 1, xResOffset);
            WriteEntry(TagYResolution,               TypeRational, 1, yResOffset);
            WriteEntry(TagResolutionUnit,            TypeShort, 1, 2u);   // 2 = inch
            // PageNumber: two SHORTs packed into one 32-bit value (little-endian):
            //   low word = this-page index (0-based), high word = total pages (0 = unknown).
            WriteEntry(TagPageNumber, TypeShort, 2, (uint)(_pageIndex & 0xFFFF));

            // "Next IFD" pointer — will be patched when the next page starts.
            _prevNextIfdPos = _stream.Position;
            Write4(0u);

            // ── Extra data ─────────────────────────────────────────────────────
            // Stream should now be at extraBase.

            if (color)
            {
                // BitsPerSample[3] = { 8, 8, 8 }
                Write2(8); Write2(8); Write2(8);
            }

            // XResolution = dpiX / 1
            Write4((uint)Math.Round(dpiX)); Write4(1u);

            // YResolution = dpiY / 1
            Write4((uint)Math.Round(dpiY)); Write4(1u);
        }

        // ── Low-level write helpers ───────────────────────────────────────────

        private void WriteFileHeader()
        {
            // Little-endian byte-order mark
            _stream.WriteByte(0x49); // 'I'
            _stream.WriteByte(0x49); // 'I'
            Write2(42);  // TIFF magic number
            Write4(0u);  // First IFD offset placeholder; patched in WriteIfd for page 0.
        }

        private void WriteEntry(ushort tag, ushort type, uint count, uint valueOrOffset)
        {
            Write2(tag);
            Write2(type);
            Write4(count);
            Write4(valueOrOffset);
        }

        private void PatchU32(long streamPos, uint value)
        {
            long saved = _stream.Position;
            _stream.Position = streamPos;
            Write4(value);
            _stream.Position = saved;
        }

        private void Write2(ushort v)
        {
            _stream.WriteByte((byte)(v & 0xFF));
            _stream.WriteByte((byte)(v >> 8));
        }

        private void Write4(uint v)
        {
            _stream.WriteByte((byte)(v        & 0xFF));
            _stream.WriteByte((byte)((v >>  8) & 0xFF));
            _stream.WriteByte((byte)((v >> 16) & 0xFF));
            _stream.WriteByte((byte)(v >> 24));
        }
    }
}
