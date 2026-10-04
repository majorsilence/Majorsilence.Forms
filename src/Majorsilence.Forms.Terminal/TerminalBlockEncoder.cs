using System;
using System.Buffers;
using System.Text;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Presents frames as Unicode block elements at 2x4 pixels per cell, four times the pixels the classic
    /// half-block output gets. A cell can show two colours in a pattern drawn from the quadrant and
    /// quarter-block glyphs (<c>▘ ▝ ▖ ▗ ▚ ▌ ▄ ▂ ▆</c> and a blank), and the encoder picks, for each 2x4 patch of
    /// the bitmap, the partition into two colour groups with the least squared error, then the glyph that
    /// draws it. Text is mostly two colours (ink and paper), which is exactly what a cell can hold, so
    /// strokes come out at roughly twice the horizontal and vertical detail of half-blocks.
    ///
    /// A terminal cell is about twice as tall as it is wide, so 2 columns by 4 rows of sub-pixels are square
    /// pixels and the picture is not stretched.
    ///
    /// Like the half-block encoder it remembers the previous frame and writes only cells whose glyph or
    /// colours changed.
    /// </summary>
    internal sealed class TerminalBlockEncoder : ITerminalFramePresenter
    {
        /// <summary>Sub-pixels per cell across.</summary>
        public const int SubWidth = 2;
        /// <summary>Sub-pixels per cell down.</summary>
        public const int SubHeight = 4;

        // A partition of the 8 sub-pixels (bit = row * 2 + column) into a foreground group (the glyph's
        // filled part) and the rest. The complement of a partition is the same partition with the colours
        // swapped, so these nine plus "one colour" cover every pattern the glyphs can draw.
        private readonly record struct Glyph (byte Mask, string Text, byte[] Utf8);

        private static readonly Glyph[] Glyphs = {
            Make (0x00, " "),
            Make (0xF0, "▄"),   // ▄ bottom half
            Make (0x55, "▌"),   // ▌ left half
            Make (0x05, "▘"),   // ▘ top left
            Make (0x0A, "▝"),   // ▝ top right
            Make (0x50, "▖"),   // ▖ bottom left
            Make (0xA0, "▗"),   // ▗ bottom right
            Make (0xA5, "▚"),   // ▚ top left and bottom right
            Make (0xC0, "▂"),   // ▂ bottom quarter
            Make (0xFC, "▆"),   // ▆ bottom three quarters
        };

        private static Glyph Make (byte mask, string text) => new (mask, text, Encoding.UTF8.GetBytes (text));

        // Error weights per channel: green matters most to the eye, blue least.
        private const int WeightR = 2, WeightG = 4, WeightB = 3;

        private readonly TerminalColorMode _mode;
        // Per cell: glyph index in the low 8 bits, then the foreground and background keys (24 bits each).
        private ulong[] _previous = Array.Empty<ulong> ();
        private int _cols, _rows;

        public TerminalBlockEncoder (TerminalColorMode mode) => _mode = mode;

        /// <inheritdoc/>
        public string ExitSequence => string.Empty;

        /// <inheritdoc/>
        public void Reset () => _cols = _rows = 0;

        /// <inheritdoc/>
        public void Encode (ReadOnlySpan<byte> bgra, int width, int height, int stride, IBufferWriter<byte> output)
        {
            var cols = (width + SubWidth - 1) / SubWidth;
            var rows = (height + SubHeight - 1) / SubHeight;
            var full = cols != _cols || rows != _rows;
            if (full) {
                _cols = cols;
                _rows = rows;
                _previous = new ulong[cols * rows];
            }

            Span<int> px = stackalloc int[SubWidth * SubHeight * 3];
            var wrote = false;
            long fg = -1, bg = -1;              // what the terminal is currently set to; -1 = unknown
            int curRow = -1, curCol = -1;       // where the cursor will be after the last write

            for (var row = 0; row < rows; row++) {
                for (var col = 0; col < cols; col++) {
                    LoadCell (bgra, width, height, stride, col, row, px);
                    var (glyphIndex, fgKey, bgKey) = Choose (px);

                    var cell = (ulong) (uint) glyphIndex | (ulong) fgKey << 8 | (ulong) bgKey << 32;
                    var idx = row * cols + col;
                    if (!full && _previous[idx] == cell)
                        continue;
                    _previous[idx] = cell;

                    if (!wrote) {
                        TerminalColors.Write (output, "\u001b[?2026h");   // synchronized output
                        wrote = true;
                    }

                    if (curRow != row || curCol != col)
                        TerminalColors.Write (output, $"\u001b[{row + 1};{col + 1}H");

                    if (bg != bgKey) {
                        TerminalColors.WriteColor (output, _mode, bgKey, background: true);
                        bg = bgKey;
                    }
                    // A blank cell shows only its background: its foreground need not be set.
                    if (glyphIndex != 0 && fg != fgKey) {
                        TerminalColors.WriteColor (output, _mode, fgKey, background: false);
                        fg = fgKey;
                    }

                    var utf8 = Glyphs[glyphIndex].Utf8;
                    utf8.CopyTo (output.GetSpan (utf8.Length));
                    output.Advance (utf8.Length);

                    curRow = row;
                    curCol = col + 1;
                }
            }

            if (wrote)
                TerminalColors.Write (output, "\u001b[0m\u001b[?2026l");
        }

        // Reads a cell's 8 sub-pixels as RGB. A cell that hangs over the bitmap's edge repeats the edge, so a
        // bitmap that is not a whole number of cells does not grow a black border.
        private static void LoadCell (ReadOnlySpan<byte> bgra, int width, int height, int stride, int col, int row, Span<int> px)
        {
            for (var sy = 0; sy < SubHeight; sy++) {
                var y = Math.Min (height - 1, row * SubHeight + sy);
                for (var sx = 0; sx < SubWidth; sx++) {
                    var x = Math.Min (width - 1, col * SubWidth + sx);
                    var o = y * stride + x * 4;
                    var i = (sy * SubWidth + sx) * 3;
                    px[i] = bgra[o + 2];
                    px[i + 1] = bgra[o + 1];
                    px[i + 2] = bgra[o];
                }
            }
        }

        // The glyph and colours that best draw these 8 pixels.
        private (int Glyph, uint Fg, uint Bg) Choose (ReadOnlySpan<int> px)
        {
            var bestGlyph = 0;
            var bestError = long.MaxValue;
            int fr = 0, fgc = 0, fb = 0, br = 0, bgc = 0, bb = 0;

            for (var g = 0; g < Glyphs.Length; g++) {
                var mask = Glyphs[g].Mask;
                long sr1 = 0, sg1 = 0, sb1 = 0, sr0 = 0, sg0 = 0, sb0 = 0;
                int n1 = 0, n0 = 0;
                for (var p = 0; p < 8; p++) {
                    var i = p * 3;
                    if ((mask >> p & 1) != 0) {
                        sr1 += px[i]; sg1 += px[i + 1]; sb1 += px[i + 2];
                        n1++;
                    } else {
                        sr0 += px[i]; sg0 += px[i + 1]; sb0 += px[i + 2];
                        n0++;
                    }
                }

                // Group means, then the squared error of every pixel from its group's mean.
                int m1r = n1 > 0 ? (int) ((sr1 + n1 / 2) / n1) : 0, m1g = n1 > 0 ? (int) ((sg1 + n1 / 2) / n1) : 0, m1b = n1 > 0 ? (int) ((sb1 + n1 / 2) / n1) : 0;
                int m0r = (int) ((sr0 + n0 / 2) / n0), m0g = (int) ((sg0 + n0 / 2) / n0), m0b = (int) ((sb0 + n0 / 2) / n0);

                long error = 0;
                for (var p = 0; p < 8; p++) {
                    var i = p * 3;
                    var inFg = (mask >> p & 1) != 0;
                    int dr = px[i] - (inFg ? m1r : m0r), dg = px[i + 1] - (inFg ? m1g : m0g), db = px[i + 2] - (inFg ? m1b : m0b);
                    error += WeightR * dr * dr + WeightG * dg * dg + WeightB * db * db;
                }

                // Strictly less: on a tie the earlier (simpler) glyph, blank first, wins.
                if (error < bestError) {
                    bestError = error;
                    bestGlyph = g;
                    fr = m1r; fgc = m1g; fb = m1b;
                    br = m0r; bgc = m0g; bb = m0b;
                }
            }

            var bgKey = TerminalColors.Quantize (_mode, br, bgc, bb);
            var fgKey = TerminalColors.Quantize (_mode, fr, fgc, fb);

            // Two groups the terminal cannot tell apart are one colour: draw a blank, whatever the pattern.
            if (bestGlyph != 0 && fgKey == bgKey)
                return (0, bgKey, bgKey);

            return bestGlyph == 0 ? (0, bgKey, bgKey) : (bestGlyph, fgKey, bgKey);
        }
    }
}
