using System;
using System.Buffers;
using System.Text;
using Majorsilence.Forms.Terminal;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The terminal host's pixel-to-ANSI encoder. Pure bytes in, bytes out, so it needs no terminal and no
    // backend: each test builds a tiny Bgra bitmap and asserts on the escape sequences, which is where
    // the "repaint only what changed" promise actually lives.
    public class TerminalFrameEncoderTests
    {
        private const string Block = "▄";

        private static byte[] Bitmap (int w, int h, params (int X, int Y, byte R, byte G, byte B)[] pixels)
        {
            var data = new byte[w * h * 4];
            foreach (var (x, y, r, g, b) in pixels) {
                var o = (y * w + x) * 4;
                data[o] = b;
                data[o + 1] = g;
                data[o + 2] = r;
                data[o + 3] = 255;
            }
            return data;
        }

        private static string Encode (TerminalFrameEncoder encoder, byte[] bgra, int w, int h)
        {
            var output = new ArrayBufferWriter<byte> ();
            encoder.Encode (bgra, w, h, w * 4, output);
            return Encoding.UTF8.GetString (output.WrittenSpan);
        }

        private static int Count (string text, string needle)
        {
            var n = 0;
            for (var i = text.IndexOf (needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf (needle, i + 1, StringComparison.Ordinal))
                n++;
            return n;
        }

        [Fact]
        public void TopPixelIsTheBackgroundAndBottomPixelIsTheForeground ()
        {
            var bmp = Bitmap (1, 2, (0, 0, 255, 0, 0), (0, 1, 0, 0, 255));
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.TrueColor), bmp, 1, 2);

            Assert.Contains ("\u001b[48;2;255;0;0m", ansi);   // background = top = red
            Assert.Contains ("\u001b[38;2;0;0;255m", ansi);   // foreground = bottom = blue
            Assert.Equal (1, Count (ansi, Block));
        }

        [Fact]
        public void FirstFramePaintsEveryCell ()
        {
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.TrueColor), Bitmap (3, 4), 3, 4);

            Assert.Equal (6, Count (ansi, Block));   // 3 columns x 2 rows
        }

        [Fact]
        public void IdenticalSecondFrameWritesNothing ()
        {
            var encoder = new TerminalFrameEncoder (TerminalColorMode.TrueColor);
            var bmp = Bitmap (4, 4, (1, 1, 10, 20, 30));
            Encode (encoder, bmp, 4, 4);

            Assert.Equal (string.Empty, Encode (encoder, bmp, 4, 4));
        }

        [Fact]
        public void OnlyTheChangedCellIsRewrittenAndTheCursorJumpsToIt ()
        {
            var encoder = new TerminalFrameEncoder (TerminalColorMode.TrueColor);
            Encode (encoder, Bitmap (4, 4), 4, 4);

            // Pixel (2, 3) lives in cell column 2, row 1 (rows pair pixels 2-3).
            var ansi = Encode (encoder, Bitmap (4, 4, (2, 3, 9, 9, 9)), 4, 4);

            Assert.Equal (1, Count (ansi, Block));
            Assert.Contains ("\u001b[2;3H", ansi);   // 1-based row 2, column 3
        }

        [Fact]
        public void AdjacentChangedCellsShareOneCursorMoveAndOneColourSetting ()
        {
            var encoder = new TerminalFrameEncoder (TerminalColorMode.TrueColor);
            Encode (encoder, Bitmap (4, 2), 4, 2);

            var changed = Bitmap (4, 2, (1, 1, 50, 60, 70), (2, 1, 50, 60, 70));
            var ansi = Encode (encoder, changed, 4, 2);

            Assert.Equal (2, Count (ansi, Block));
            Assert.Equal (1, Count (ansi, "H"));                      // one jump, then the cursor advances by itself
            Assert.Equal (1, Count (ansi, "\u001b[38;2;50;60;70m"));  // same colour not re-sent for the second cell
        }

        [Fact]
        public void ChangeInSameColourButDifferentCellStillRepaintsAfterResize ()
        {
            var encoder = new TerminalFrameEncoder (TerminalColorMode.TrueColor);
            Encode (encoder, Bitmap (2, 2), 2, 2);

            // A different size invalidates what is on screen, even though every pixel is black.
            var ansi = Encode (encoder, Bitmap (3, 2), 3, 2);

            Assert.Equal (3, Count (ansi, Block));
        }

        [Fact]
        public void ResetForcesAFullRepaint ()
        {
            var encoder = new TerminalFrameEncoder (TerminalColorMode.TrueColor);
            var bmp = Bitmap (2, 2);
            Encode (encoder, bmp, 2, 2);
            encoder.Reset ();

            Assert.Equal (2, Count (Encode (encoder, bmp, 2, 2), Block));
        }

        [Fact]
        public void OddHeightPairsTheLastRowWithBlack ()
        {
            var bmp = Bitmap (1, 3, (0, 2, 200, 100, 50));
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.TrueColor), bmp, 1, 3);

            Assert.Equal (2, Count (ansi, Block));                 // ceil (3 / 2) rows
            Assert.Contains ("\u001b[48;2;200;100;50m", ansi);     // pixel row 2 is the top of the second cell
            Assert.Contains ("\u001b[38;2;0;0;0m", ansi);          // its missing bottom is black
        }

        [Fact]
        public void FrameIsWrappedInSynchronizedOutput ()
        {
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.TrueColor), Bitmap (1, 2), 1, 2);

            Assert.StartsWith ("\u001b[?2026h", ansi);
            Assert.EndsWith ("\u001b[0m\u001b[?2026l", ansi);
        }

        [Fact]
        public void Ansi256UsesThePaletteIndex ()
        {
            var bmp = Bitmap (1, 2, (0, 0, 255, 0, 0), (0, 1, 255, 255, 255));
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.Ansi256), bmp, 1, 2);

            Assert.Contains ("\u001b[48;5;196m", ansi);   // 16 + 36*5: pure red in the colour cube
            Assert.Contains ("\u001b[38;5;231m", ansi);   // 16 + 215: white is the cube's last entry
        }

        [Fact]
        public void Ansi256PicksTheGreyRampForGreys ()
        {
            var bmp = Bitmap (1, 2, (0, 0, 128, 128, 128), (0, 1, 128, 128, 128));
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.Ansi256), bmp, 1, 2);

            // The nearest cube grey is 135 (index 102); the ramp has 128 exactly (232 + 12).
            Assert.Contains ("\u001b[48;5;244m", ansi);
        }

        [Fact]
        public void Ansi16UsesStandardAndBrightCodes ()
        {
            var bmp = Bitmap (1, 2, (0, 0, 0, 0, 0), (0, 1, 255, 255, 255));
            var ansi = Encode (new TerminalFrameEncoder (TerminalColorMode.Ansi16), bmp, 1, 2);

            Assert.Contains ("\u001b[40m", ansi);    // black background
            Assert.Contains ("\u001b[97m", ansi);    // bright white foreground
        }

        [Fact]
        public void QuantizedEqualColoursAreNotRewritten ()
        {
            var encoder = new TerminalFrameEncoder (TerminalColorMode.Ansi16);
            Encode (encoder, Bitmap (1, 2, (0, 0, 250, 250, 250)), 1, 2);

            // 250 and 255 are both bright white in 16 colours: the terminal could not show a difference.
            Assert.Equal (string.Empty, Encode (encoder, Bitmap (1, 2, (0, 0, 255, 255, 255)), 1, 2));
        }

        [Theory]
        [InlineData ("truecolor", "xterm", TerminalColorMode.TrueColor)]
        [InlineData ("24bit", "xterm", TerminalColorMode.TrueColor)]
        [InlineData (null, "xterm-256color", TerminalColorMode.Ansi256)]
        [InlineData (null, "xterm", TerminalColorMode.Ansi256)]   // real xterm sets plain TERM=xterm and has 256 colours
        [InlineData (null, "linux", TerminalColorMode.Ansi16)]
        [InlineData (null, "screen", TerminalColorMode.Ansi16)]
        [InlineData (null, null, TerminalColorMode.Ansi16)]
        public void ColourModeIsDetectedFromTheEnvironment (string? colorTerm, string? term, TerminalColorMode expected)
        {
            string? Env (string name) => name switch { "COLORTERM" => colorTerm, "TERM" => term, _ => null };

            Assert.Equal (expected, TerminalCapabilities.DetectColorMode (Env));
        }

        [Fact]
        public void WindowsTerminalIsTrueColourWithoutColorterm ()
        {
            string? Env (string name) => name == "WT_SESSION" ? "guid" : null;

            Assert.Equal (TerminalColorMode.TrueColor, TerminalCapabilities.DetectColorMode (Env));
        }
    }
}
