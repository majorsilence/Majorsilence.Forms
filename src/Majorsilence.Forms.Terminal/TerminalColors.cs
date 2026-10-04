using System;
using System.Buffers;
using System.Text;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Colour handling shared by the cell-based presenters: matching a colour to the terminal's palette and
    /// writing the SGR that sets it.
    /// </summary>
    internal static class TerminalColors
    {
        private static readonly (int R, int G, int B)[] Vga = {
            (0, 0, 0), (170, 0, 0), (0, 170, 0), (170, 85, 0), (0, 0, 170), (170, 0, 170), (0, 170, 170), (170, 170, 170),
            (85, 85, 85), (255, 85, 85), (85, 255, 85), (255, 255, 85), (85, 85, 255), (255, 85, 255), (85, 255, 255), (255, 255, 255),
        };

        // xterm's colour cube levels (6 per channel) and a 24-step grey ramp after it.
        private static readonly int[] CubeLevels = { 0, 95, 135, 175, 215, 255 };

        /// <summary>
        /// The colour as the terminal can show it, as a key that is equal for two colours the terminal cannot
        /// tell apart (so they are not rewritten): 24-bit RGB, or a palette index.
        /// </summary>
        public static uint Quantize (TerminalColorMode mode, int r, int g, int b) => mode switch {
            TerminalColorMode.TrueColor => (uint) (r << 16 | g << 8 | b),
            TerminalColorMode.Ansi256 => (uint) To256 (r, g, b),
            _ => (uint) To16 (r, g, b),
        };

        /// <summary>The RGB a quantised key stands for (the palette entry for the palette modes).</summary>
        public static (int R, int G, int B) Rgb (TerminalColorMode mode, uint key)
        {
            switch (mode) {
                case TerminalColorMode.TrueColor:
                    return ((int) (key >> 16 & 0xFF), (int) (key >> 8 & 0xFF), (int) (key & 0xFF));
                case TerminalColorMode.Ansi256:
                    return key < 16 ? Vga[key]
                         : key >= 232 ? (8 + (int) (key - 232) * 10, 8 + (int) (key - 232) * 10, 8 + (int) (key - 232) * 10)
                         : (CubeLevels[(key - 16) / 36], CubeLevels[(key - 16) / 6 % 6], CubeLevels[(key - 16) % 6]);
                default:
                    return Vga[key & 15];
            }
        }

        /// <summary>Appends the SGR that sets the foreground or background to <paramref name="key"/>.</summary>
        public static void WriteColor (IBufferWriter<byte> output, TerminalColorMode mode, uint key, bool background)
        {
            switch (mode) {
                case TerminalColorMode.TrueColor:
                    Write (output, $"\u001b[{(background ? 48 : 38)};2;{key >> 16 & 0xFF};{key >> 8 & 0xFF};{key & 0xFF}m");
                    break;
                case TerminalColorMode.Ansi256:
                    Write (output, $"\u001b[{(background ? 48 : 38)};5;{key}m");
                    break;
                default:
                    // 0-7 are 30-37 / 40-47; 8-15 are the bright 90-97 / 100-107.
                    var code = key < 8 ? (background ? 40 : 30) + key : (background ? 100 : 90) + (key - 8);
                    Write (output, $"\u001b[{code}m");
                    break;
            }
        }

        public static void Write (IBufferWriter<byte> output, string text)
        {
            var max = Encoding.UTF8.GetMaxByteCount (text.Length);
            var span = output.GetSpan (max);
            output.Advance (Encoding.UTF8.GetBytes (text, span));
        }

        public static int To16 (int r, int g, int b)
        {
            var best = 0;
            var bestD = int.MaxValue;
            for (var i = 0; i < Vga.Length; i++) {
                var d = Distance (r, g, b, Vga[i].R, Vga[i].G, Vga[i].B);
                if (d < bestD) {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        // Colour -> palette index. A UI repeats a handful of colours (and a frame asks for two per cell), so the
        // exact search below runs once per distinct colour.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> Cache256 = new ();

        // The nearest of the 6x6x6 cube and the grey ramp (palette entries 16-255) under the perceptual
        // distance. Rounding each cube channel separately is not the nearest colour under that metric
        // (pastels land on the wrong hue), so every entry is compared, once per distinct colour.
        public static int To256 (int r, int g, int b)
            => Cache256.GetOrAdd (r << 16 | g << 8 | b, key => {
                var best = 16;
                var bestD = int.MaxValue;
                for (var i = 16; i < 256; i++) {
                    var (pr, pg, pb) = Rgb (TerminalColorMode.Ansi256, (uint) i);
                    var d = Distance (r, g, b, pr, pg, pb);
                    if (d < bestD) {
                        bestD = d;
                        best = i;
                    }
                }
                return (byte) best;
            });

        /// <summary>
        /// A cheap perceptual distance ("redmean"): green counts most and the weights of red and blue shift with
        /// how red the colour is, which tracks what an eye sees far better than plain RGB distance.
        /// </summary>
        public static int Distance (int r1, int g1, int b1, int r2, int g2, int b2)
        {
            var mean = (r1 + r2) / 2;
            int dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
            return (((512 + mean) * dr * dr) >> 8) + 4 * dg * dg + (((767 - mean) * db * db) >> 8);
        }
    }
}
