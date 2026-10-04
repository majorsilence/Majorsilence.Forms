using System;
using System.Buffers;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Turns a bitmap into the ANSI that paints it. A terminal cell is shown as one pixel wide and two
    /// tall: <c>▄</c> (lower half block) takes the lower pixel as its foreground and the upper pixel as its
    /// background, so a <c>cols × rows*2</c> bitmap fills a <c>cols × rows</c> screen at the highest
    /// fidelity plain text allows.
    ///
    /// Stateful on purpose: it remembers what it last wrote and emits only the cells that changed, with
    /// cursor jumps only across gaps and SGR changes only when a colour differs. Repainting the whole
    /// screen every frame (the naive loop) flickers and is unusable over SSH.
    /// </summary>
    internal sealed class TerminalFrameEncoder : ITerminalFramePresenter
    {
        // ▄ U+2584 in UTF-8.
        private static ReadOnlySpan<byte> LowerHalfBlock => new byte[] { 0xE2, 0x96, 0x84 };

        private readonly TerminalColorMode _mode;
        // Per cell: the top colour key in the high 32 bits, the bottom in the low.
        private ulong[] _previousKeys = Array.Empty<ulong> ();
        private int _cols;
        private int _rows;

        public TerminalFrameEncoder (TerminalColorMode mode) => _mode = mode;

        /// <inheritdoc/>
        public string ExitSequence => string.Empty;

        /// <summary>Forgets what is on screen, so the next <see cref="Encode"/> paints every cell (after a resize or a screen clear).</summary>
        public void Reset () => _cols = _rows = 0;

        /// <summary>
        /// Appends the escape sequences that bring the screen from the previous frame to this one.
        /// Writes nothing when nothing changed.
        /// </summary>
        /// <param name="bgra">Bgra8888 pixels, rows <paramref name="stride"/> bytes apart. Alpha is ignored: a window is opaque.</param>
        /// <param name="width">Pixel width, which is also the cell column count.</param>
        /// <param name="height">Pixel height; the cell row count is half of it, rounded up (an odd last row pairs with black).</param>
        /// <param name="stride">Bytes per bitmap row.</param>
        /// <param name="output">Receives the bytes to write to the terminal.</param>
        public void Encode (ReadOnlySpan<byte> bgra, int width, int height, int stride, IBufferWriter<byte> output)
        {
            var rows = (height + 1) / 2;
            var full = width != _cols || rows != _rows;
            if (full) {
                _cols = width;
                _rows = rows;
                _previousKeys = new ulong[width * rows];
            }

            var wroteAny = false;
            // The colours the terminal is currently set to; -1 means "unknown, emit".
            long fg = -1, bg = -1;
            // Where the cursor will be after the last write; (-1, -1) forces a jump.
            int curRow = -1, curCol = -1;

            for (var row = 0; row < rows; row++) {
                for (var col = 0; col < width; col++) {
                    var top = Key (bgra, row * 2, col, height, stride);
                    var bottom = Key (bgra, row * 2 + 1, col, height, stride);
                    var cell = ((ulong) top << 32) | bottom;
                    var idx = row * width + col;

                    if (!full && _previousKeys[idx] == cell)
                        continue;
                    _previousKeys[idx] = cell;

                    if (!wroteAny) {
                        // Synchronized output: the terminal holds the frame until it is complete, so a
                        // partial repaint never shows. Ignored by terminals that do not know it.
                        Write (output, "\u001b[?2026h");
                        wroteAny = true;
                    }

                    if (curRow != row || curCol != col)
                        Write (output, $"\u001b[{row + 1};{col + 1}H");

                    if (bg != top) {
                        WriteColor (output, top, background: true);
                        bg = top;
                    }
                    if (fg != bottom) {
                        WriteColor (output, bottom, background: false);
                        fg = bottom;
                    }

                    var span = output.GetSpan (3);
                    LowerHalfBlock.CopyTo (span);
                    output.Advance (3);

                    curRow = row;
                    curCol = col + 1;
                }
            }

            if (wroteAny)
                Write (output, "\u001b[0m\u001b[?2026l");
        }

        // The colour a pixel is shown as, quantised to the mode so that two pixels the terminal could not
        // tell apart compare equal and are not rewritten.
        private uint Key (ReadOnlySpan<byte> bgra, int y, int x, int height, int stride)
        {
            if (y >= height)
                return Quantize (0, 0, 0);

            var o = y * stride + x * 4;
            return Quantize (bgra[o + 2], bgra[o + 1], bgra[o]);
        }

        private uint Quantize (byte r, byte g, byte b) => TerminalColors.Quantize (_mode, r, g, b);

        private void WriteColor (IBufferWriter<byte> output, uint key, bool background)
            => TerminalColors.WriteColor (output, _mode, key, background);

        private static void Write (IBufferWriter<byte> output, string text) => TerminalColors.Write (output, text);
    }
}
