using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Presents frames as Sixel images. A sixel image lands at the cursor and cannot be edited afterwards, so
    /// the encoder keeps the previous frame, finds the bounding box of the cells that changed, moves the
    /// cursor to that box and draws only that region. A caret blink costs a few cells, not the screen.
    ///
    /// Sixel carries a palette of at most 256 colours, so each region is quantised to its own most-used
    /// colours (at 6 bits per channel); a UI is mostly flat colour plus anti-aliasing, which this keeps.
    /// </summary>
    internal sealed class TerminalSixelEncoder : ITerminalFramePresenter
    {
        private const int MaxColors = 256;
        private const int KeyBits = 6;                      // bits per channel when counting colours
        private const int KeySpace = 1 << (KeyBits * 3);

        private readonly int _cellW;
        private readonly int _cellH;
        private byte[] _previous = Array.Empty<byte> ();
        private int _previousWidth = -1, _previousHeight = -1;

        // Reused across frames: colour key -> palette index (-1 = not assigned yet) and its usage count.
        private readonly short[] _index = new short[KeySpace];
        private readonly int[] _count = new int[KeySpace];
        // The first exact colour seen in each bucket. A UI is mostly flat colour, so this is usually the
        // bucket's only colour and the palette entry is exact rather than the bucket's centre.
        private readonly int[] _exact = new int[KeySpace];

        public TerminalSixelEncoder (int cellWidth, int cellHeight)
        {
            _cellW = Math.Max (1, cellWidth);
            _cellH = Math.Max (1, cellHeight);
            Array.Fill (_index, (short) -1);
        }

        /// <inheritdoc/>
        public string ExitSequence => string.Empty;

        /// <inheritdoc/>
        public void Reset () => _previousWidth = -1;

        /// <inheritdoc/>
        public void Encode (ReadOnlySpan<byte> bgra, int width, int height, int stride, IBufferWriter<byte> output)
        {
            var cols = (width + _cellW - 1) / _cellW;
            var rows = (height + _cellH - 1) / _cellH;

            var full = _previousWidth != width || _previousHeight != height;
            int minCol = 0, maxCol = cols - 1, minRow = 0, maxRow = rows - 1;
            if (!full && !DirtyCells (bgra, width, height, stride, cols, rows, out minCol, out maxCol, out minRow, out maxRow))
                return;

            // Remember this frame (tightly packed) for the next diff.
            if (_previous.Length != width * height * 4)
                _previous = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
                bgra.Slice (y * stride, width * 4).CopyTo (_previous.AsSpan (y * width * 4, width * 4));
            _previousWidth = width;
            _previousHeight = height;

            var x0 = minCol * _cellW;
            var y0 = minRow * _cellH;
            var rw = Math.Min (width, (maxCol + 1) * _cellW) - x0;
            var rh = Math.Min (height, (maxRow + 1) * _cellH) - y0;

            WriteRegion (bgra, stride, x0, y0, rw, rh, minRow, minCol, output);
        }

        // The bounding box, in cells, of everything that differs from the previous frame.
        private bool DirtyCells (ReadOnlySpan<byte> bgra, int width, int height, int stride, int cols, int rows,
                                 out int minCol, out int maxCol, out int minRow, out int maxRow)
        {
            minCol = cols;
            maxCol = -1;
            minRow = rows;
            maxRow = -1;

            for (var y = 0; y < height; y++) {
                var now = bgra.Slice (y * stride, width * 4);
                var before = _previous.AsSpan (y * width * 4, width * 4);
                if (now.SequenceEqual (before))
                    continue;

                var row = y / _cellH;
                minRow = Math.Min (minRow, row);
                maxRow = Math.Max (maxRow, row);

                // Narrow to the first and last differing pixel in the line, then to cells.
                var first = 0;
                while (first < width && now.Slice (first * 4, 4).SequenceEqual (before.Slice (first * 4, 4)))
                    first++;
                var last = width - 1;
                while (last > first && now.Slice (last * 4, 4).SequenceEqual (before.Slice (last * 4, 4)))
                    last--;

                minCol = Math.Min (minCol, first / _cellW);
                maxCol = Math.Max (maxCol, last / _cellW);
            }

            return maxRow >= 0;
        }

        private void WriteRegion (ReadOnlySpan<byte> bgra, int stride, int x0, int y0, int rw, int rh, int row, int col, IBufferWriter<byte> output)
        {
            // ── Palette: the most-used colours of this region, the rest mapped to their nearest. ──
            var used = new List<int> ();
            for (var y = 0; y < rh; y++) {
                var o = (y0 + y) * stride + x0 * 4;
                for (var x = 0; x < rw; x++, o += 4) {
                    var key = Key (bgra[o + 2], bgra[o + 1], bgra[o]);
                    if (_count[key]++ == 0) {
                        used.Add (key);
                        _exact[key] = bgra[o + 2] << 16 | bgra[o + 1] << 8 | bgra[o];
                    }
                }
            }

            if (used.Count > MaxColors)
                used.Sort ((a, b) => _count[b].CompareTo (_count[a]));

            var palette = new List<int> (Math.Min (used.Count, MaxColors));
            for (var i = 0; i < used.Count && i < MaxColors; i++) {
                _index[used[i]] = (short) i;
                palette.Add (used[i]);
            }

            // A colour outside the palette takes its nearest entry; the result is cached in _index.
            for (var i = MaxColors; i < used.Count; i++)
                _index[used[i]] = (short) Nearest (used[i], palette);

            // ── Pixels as palette indices. ──
            var pixels = new short[rw * rh];
            for (var y = 0; y < rh; y++) {
                var o = (y0 + y) * stride + x0 * 4;
                for (var x = 0; x < rw; x++, o += 4)
                    pixels[y * rw + x] = _index[Key (bgra[o + 2], bgra[o + 1], bgra[o])];
            }

            var sb = new StringBuilder (rw * rh / 8);
            sb.Append ("\u001b[?2026h");
            sb.Append ($"\u001b[{row + 1};{col + 1}H");

            // P2=1: a 0 bit leaves the screen alone, so the padding rows of the last band do not paint over
            // the cell below. The raster attributes pin 1:1 pixels and the image size.
            sb.Append ("\u001bP0;1;0q");
            sb.Append ($"\"1;1;{rw};{rh}");

            for (var i = 0; i < palette.Count; i++) {
                var rgb = _exact[palette[i]];
                int r = rgb >> 16 & 0xFF, g = rgb >> 8 & 0xFF, b = rgb & 0xFF;
                // Sixel colours are percentages.
                sb.Append ($"#{i};2;{(r * 100 + 127) / 255};{(g * 100 + 127) / 255};{(b * 100 + 127) / 255}");
            }

            EncodeBands (sb, pixels, rw, rh, palette.Count);
            sb.Append ("\u001b\\\u001b[?2026l");

            // Reset only what was touched, so the next frame does not pay for clearing 2^18 entries.
            foreach (var key in used) {
                _count[key] = 0;
                _index[key] = -1;
            }

            var bytes = Encoding.ASCII.GetBytes (sb.ToString ());
            output.Write (bytes);
        }

        // Six pixel rows per band: for each colour present, one run-length-encoded line of 6-bit columns.
        private static void EncodeBands (StringBuilder sb, short[] pixels, int rw, int rh, int paletteCount)
        {
            var present = new bool[paletteCount];
            var line = new byte[rw];

            for (var top = 0; top < rh; top += 6) {
                var bandRows = Math.Min (6, rh - top);

                Array.Clear (present);
                for (var y = 0; y < bandRows; y++)
                    for (var x = 0; x < rw; x++)
                        present[pixels[(top + y) * rw + x]] = true;

                var firstColor = true;
                for (var c = 0; c < paletteCount; c++) {
                    if (!present[c])
                        continue;

                    if (!firstColor)
                        sb.Append ('$');   // carriage return: draw the next colour over the same band
                    firstColor = false;
                    sb.Append ('#').Append (c);

                    var last = -1;
                    for (var x = 0; x < rw; x++) {
                        var bits = 0;
                        for (var y = 0; y < bandRows; y++)
                            if (pixels[(top + y) * rw + x] == c)
                                bits |= 1 << y;
                        line[x] = (byte) bits;
                        if (bits != 0)
                            last = x;
                    }

                    // Trailing empty columns need no characters.
                    for (var x = 0; x <= last;) {
                        var ch = line[x];
                        var run = 1;
                        while (x + run <= last && line[x + run] == ch)
                            run++;

                        // "!n c" repeats c n times; shorter runs are cheaper spelled out.
                        if (run > 3)
                            sb.Append ('!').Append (run).Append ((char) (63 + ch));
                        else
                            sb.Append ((char) (63 + ch), run);
                        x += run;
                    }
                }

                sb.Append ('-');   // next band
            }
        }

        private static int Key (byte r, byte g, byte b)
            => (r >> (8 - KeyBits)) << (KeyBits * 2) | (g >> (8 - KeyBits)) << KeyBits | (b >> (8 - KeyBits));

        private int Nearest (int key, List<int> palette)
        {
            var self = _exact[key];
            int r = self >> 16 & 0xFF, g = self >> 8 & 0xFF, b = self & 0xFF;
            var best = 0;
            var bestD = int.MaxValue;
            for (var i = 0; i < palette.Count; i++) {
                var other = _exact[palette[i]];
                int pr = other >> 16 & 0xFF, pg = other >> 8 & 0xFF, pb = other & 0xFF;
                var d = (r - pr) * (r - pr) + (g - pg) * (g - pg) + (b - pb) * (b - pb);
                if (d < bestD) {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
