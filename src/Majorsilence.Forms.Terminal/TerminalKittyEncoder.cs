using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Presents frames through the Kitty graphics protocol, as a grid of tiles. Each tile is its own image
    /// (id <see cref="FirstImageId"/> + its index) placed over a fixed block of cells; a frame re-sends only
    /// the tiles whose pixels changed, so a caret blink costs one tile, not the screen.
    ///
    /// Tiles rather than the protocol's frame-edit command (<c>a=f</c>), because replacing an image by id is
    /// the baseline every Kitty-protocol terminal implements, while frame editing is animation machinery
    /// that terminals other than kitty itself implement partly or not at all, and a missing implementation
    /// would silently leave stale pixels. Each tile is stretched by the terminal over its cells, so tiles
    /// meet exactly even when the cell pixel size we assumed was off.
    ///
    /// Pixels are deflated (<c>o=z</c>): a UI is mostly flat colour, and the sample's 1080x720 frame measured
    /// 2.3 MB raw against 19 KB deflated (a busier screen compresses less).
    /// </summary>
    internal sealed class TerminalKittyEncoder : ITerminalFramePresenter
    {
        // The protocol caps one escape's payload at 4096 base64 characters; longer images continue with m=1.
        private const int ChunkSize = 4096;

        // Clear of the probe's query id.
        internal const int FirstImageId = 100;

        private readonly int _cols;
        private readonly int _rows;
        private readonly int _tileCols;
        private readonly int _tileRows;
        private byte[] _previous = Array.Empty<byte> ();
        private int _previousWidth = -1, _previousHeight = -1;

        public TerminalKittyEncoder (int cols, int rows, int tileCols = 16, int tileRows = 8)
        {
            _cols = Math.Max (1, cols);
            _rows = Math.Max (1, rows);
            _tileCols = Math.Max (1, tileCols);
            _tileRows = Math.Max (1, tileRows);
        }

        /// <inheritdoc/>
        // a=d,d=A deletes every image and placement; q=2 asks for no reply, which would arrive as input.
        public string ExitSequence => "\u001b_Ga=d,d=A,q=2\u001b\\";

        /// <inheritdoc/>
        public void Reset () => _previousWidth = -1;

        /// <inheritdoc/>
        public void Encode (ReadOnlySpan<byte> bgra, int width, int height, int stride, IBufferWriter<byte> output)
        {
            var full = _previousWidth != width || _previousHeight != height;
            var tilesAcross = (_cols + _tileCols - 1) / _tileCols;
            var tilesDown = (_rows + _tileRows - 1) / _tileRows;

            // Pixels per cell, as the terminal will stretch them: the image may not divide evenly.
            double pxPerCol = (double) width / _cols, pxPerRow = (double) height / _rows;

            if (_previous.Length != width * height * 4)
                _previous = new byte[width * height * 4];

            var wrote = false;
            for (var ty = 0; ty < tilesDown; ty++) {
                for (var tx = 0; tx < tilesAcross; tx++) {
                    var col0 = tx * _tileCols;
                    var row0 = ty * _tileRows;
                    var cols = Math.Min (_tileCols, _cols - col0);
                    var rows = Math.Min (_tileRows, _rows - row0);

                    var x0 = (int) Math.Round (col0 * pxPerCol);
                    var y0 = (int) Math.Round (row0 * pxPerRow);
                    var x1 = Math.Min (width, (int) Math.Round ((col0 + cols) * pxPerCol));
                    var y1 = Math.Min (height, (int) Math.Round ((row0 + rows) * pxPerRow));
                    var tw = x1 - x0;
                    var th = y1 - y0;
                    if (tw <= 0 || th <= 0)
                        continue;

                    if (!full && !TileChanged (bgra, stride, width, x0, y0, tw, th))
                        continue;

                    if (!wrote) {
                        Write (output, "\u001b[?2026h");   // synchronized: every changed tile lands in one repaint
                        wrote = true;
                    }

                    var id = FirstImageId + ty * tilesAcross + tx;
                    WriteTile (output, bgra, stride, x0, y0, tw, th, id, col0, row0, cols, rows);
                }
            }

            if (wrote)
                Write (output, "\u001b[?2026l");

            // Remember this frame for the next diff.
            for (var y = 0; y < height; y++)
                bgra.Slice (y * stride, width * 4).CopyTo (_previous.AsSpan (y * width * 4, width * 4));
            _previousWidth = width;
            _previousHeight = height;
        }

        private bool TileChanged (ReadOnlySpan<byte> bgra, int stride, int width, int x0, int y0, int tw, int th)
        {
            for (var y = y0; y < y0 + th; y++) {
                var now = bgra.Slice (y * stride + x0 * 4, tw * 4);
                var before = _previous.AsSpan (y * width * 4 + x0 * 4, tw * 4);
                if (!now.SequenceEqual (before))
                    return true;
            }
            return false;
        }

        private static void WriteTile (IBufferWriter<byte> output, ReadOnlySpan<byte> bgra, int stride,
                                       int x0, int y0, int tw, int th, int id, int col, int row, int cols, int rows)
        {
            var rgb = new byte[tw * th * 3];
            for (var y = 0; y < th; y++) {
                var src = (y0 + y) * stride + x0 * 4;
                var dst = y * tw * 3;
                for (var x = 0; x < tw; x++, src += 4, dst += 3) {
                    rgb[dst] = bgra[src + 2];
                    rgb[dst + 1] = bgra[src + 1];
                    rgb[dst + 2] = bgra[src];
                }
            }

            using var compressed = new MemoryStream ();
            using (var z = new ZLibStream (compressed, CompressionLevel.Fastest, leaveOpen: true))
                z.Write (rgb, 0, rgb.Length);
            var base64 = Convert.ToBase64String (compressed.GetBuffer (), 0, (int) compressed.Length);

            // Home the cursor to the tile's first cell: the placement lands where the cursor is.
            Write (output, $"\u001b[{row + 1};{col + 1}H");

            for (var offset = 0; offset < base64.Length; offset += ChunkSize) {
                var count = Math.Min (ChunkSize, base64.Length - offset);
                var more = offset + count < base64.Length ? 1 : 0;

                // a=T transmit+display; f=24 RGB; o=z deflate; c/r stretch over the tile's cells; C=1 leave
                // the cursor put; q=2 no replies. Re-sending an id replaces that image and placement in place.
                var control = offset == 0
                    ? $"a=T,f=24,o=z,s={tw},v={th},i={id},p=1,c={cols},r={rows},C=1,q=2,m={more}"
                    : $"m={more},q=2";

                Write (output, $"\u001b_G{control};");
                Write (output, base64.AsSpan (offset, count));
                Write (output, "\u001b\\");
            }
        }

        private static void Write (IBufferWriter<byte> output, string text) => Write (output, text.AsSpan ());

        private static void Write (IBufferWriter<byte> output, ReadOnlySpan<char> text)
        {
            var span = output.GetSpan (text.Length);
            output.Advance (Encoding.ASCII.GetBytes (text, span));
        }
    }
}
