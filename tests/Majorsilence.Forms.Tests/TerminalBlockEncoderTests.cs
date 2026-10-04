using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Majorsilence.Forms.Terminal;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The block-element presenter (2x4 pixels per cell). Each test decodes the bytes it wrote back into
    // sub-pixels with a reader written here from the glyph geometry, so a glyph drawn upside down or a
    // colour assigned to the wrong half fails: that is what a terminal would show.
    public class TerminalBlockEncoderTests
    {
        private const int W = 2, H = 4;   // sub-pixels per cell

        // Which of a cell's 8 sub-pixels (index = row * 2 + column) each glyph fills, from its shape.
        private static readonly Dictionary<char, int[]> Fills = new () {
            [' '] = Array.Empty<int> (),
            ['▄'] = new[] { 4, 5, 6, 7 },     // ▄ bottom half
            ['▌'] = new[] { 0, 2, 4, 6 },     // ▌ left half
            ['▘'] = new[] { 0, 2 },           // ▘ top left
            ['▝'] = new[] { 1, 3 },           // ▝ top right
            ['▖'] = new[] { 4, 6 },           // ▖ bottom left
            ['▗'] = new[] { 5, 7 },           // ▗ bottom right
            ['▚'] = new[] { 0, 2, 5, 7 },     // ▚ top left and bottom right
            ['▂'] = new[] { 6, 7 },           // ▂ bottom quarter
            ['▆'] = new[] { 2, 3, 4, 5, 6, 7 }, // ▆ bottom three quarters
        };

        private sealed record Cell (int Row, int Col, char Glyph, (int R, int G, int B) Fg, (int R, int G, int B) Bg);

        private static byte[] Bitmap (int w, int h, Func<int, int, (int R, int G, int B)> pixel)
        {
            var data = new byte[w * h * 4];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) {
                    var (r, g, b) = pixel (x, y);
                    var o = (y * w + x) * 4;
                    data[o] = (byte) b;
                    data[o + 1] = (byte) g;
                    data[o + 2] = (byte) r;
                    data[o + 3] = 255;
                }
            return data;
        }

        private static string Encode (TerminalBlockEncoder encoder, byte[] bgra, int w, int h)
        {
            var output = new ArrayBufferWriter<byte> ();
            encoder.Encode (bgra, w, h, w * 4, output);
            return Encoding.UTF8.GetString (output.WrittenSpan);
        }

        // Reads truecolor output: cursor moves, colour settings and glyphs, tracking the terminal's state.
        private static List<Cell> Decode (string ansi)
        {
            var cells = new List<Cell> ();
            (int, int, int) fg = (0, 0, 0), bg = (0, 0, 0);
            int row = 0, col = 0;
            var i = 0;
            while (i < ansi.Length) {
                var m = Regex.Match (ansi[i..], "^\u001b\\[(?:(\\d+);(\\d+)H|(38|48);2;(\\d+);(\\d+);(\\d+)m|\\?2026[hl]|0m)");
                if (m.Success) {
                    if (m.Groups[1].Success) {
                        row = int.Parse (m.Groups[1].Value) - 1;
                        col = int.Parse (m.Groups[2].Value) - 1;
                    } else if (m.Groups[3].Success) {
                        var c = (int.Parse (m.Groups[4].Value), int.Parse (m.Groups[5].Value), int.Parse (m.Groups[6].Value));
                        if (m.Groups[3].Value == "38") fg = c; else bg = c;
                    }
                    i += m.Length;
                    continue;
                }

                if (Fills.ContainsKey (ansi[i])) {
                    cells.Add (new Cell (row, col, ansi[i], fg, bg));
                    col++;
                }
                i++;
            }
            return cells;
        }

        // The 8 colours a cell draws: foreground on the glyph's filled sub-pixels, background elsewhere.
        private static (int R, int G, int B)[] Pixels (Cell cell)
        {
            var px = new (int, int, int)[8];
            for (var p = 0; p < 8; p++)
                px[p] = Fills[cell.Glyph].Contains (p) ? cell.Fg : cell.Bg;
            return px;
        }

        private static readonly (int R, int G, int B) Ink = (20, 30, 40), Paper = (230, 220, 210);

        // A 2x4 patch where the sub-pixels in `ink` are ink and the rest paper.
        private static byte[] Patch (params int[] ink)
            => Bitmap (W, H, (x, y) => ink.Contains (y * W + x) ? Ink : Paper);

        private static string One (byte[] patch) => Encode (new TerminalBlockEncoder (TerminalColorMode.TrueColor), patch, W, H);

        // ── Every shape a cell can draw round-trips exactly ───────────────────

        public static IEnumerable<object[]> Shapes () => new[] {
            new object[] { "bottom half", new[] { 4, 5, 6, 7 } },
            new object[] { "top half", new[] { 0, 1, 2, 3 } },
            new object[] { "left half", new[] { 0, 2, 4, 6 } },
            new object[] { "right half", new[] { 1, 3, 5, 7 } },
            new object[] { "top left", new[] { 0, 2 } },
            new object[] { "top right", new[] { 1, 3 } },
            new object[] { "bottom left", new[] { 4, 6 } },
            new object[] { "bottom right", new[] { 5, 7 } },
            new object[] { "diagonal", new[] { 0, 2, 5, 7 } },
            new object[] { "other diagonal", new[] { 1, 3, 4, 6 } },
            new object[] { "bottom quarter", new[] { 6, 7 } },
            new object[] { "all but the bottom quarter", new[] { 0, 1, 2, 3, 4, 5 } },
            new object[] { "bottom three quarters", new[] { 2, 3, 4, 5, 6, 7 } },
            new object[] { "top quarter", new[] { 0, 1 } },
            new object[] { "three quadrants (no top left)", new[] { 1, 3, 4, 5, 6, 7 } },
            new object[] { "three quadrants (no bottom right)", new[] { 0, 1, 2, 3, 4, 6 } },
        };

        [Theory]
        [MemberData (nameof (Shapes))]
        public void EveryShapeACellCanDrawIsDrawnExactly (string shape, int[] ink)
        {
            var cell = Assert.Single (Decode (One (Patch (ink))));
            var drawn = Pixels (cell);

            Assert.True (
                Enumerable.Range (0, 8).All (p => drawn[p] == (ink.Contains (p) ? Ink : Paper)),
                $"{shape}: glyph {cell.Glyph} with fg {cell.Fg} bg {cell.Bg} does not reproduce the patch");
        }

        [Fact]
        public void AFlatCellIsABlankOnItsColour ()
        {
            var cell = Assert.Single (Decode (One (Bitmap (W, H, (_, _) => (12, 34, 56)))));

            Assert.Equal (' ', cell.Glyph);
            Assert.Equal ((12, 34, 56), cell.Bg);
        }

        [Fact]
        public void ABlankCellSetsNoForeground ()
        {
            var ansi = One (Bitmap (W, H, (_, _) => (12, 34, 56)));

            Assert.DoesNotContain ("\u001b[38;", ansi);
        }

        [Fact]
        public void AnInexactPatternIsApproximatedBetterThanAFlatFill ()
        {
            // A one-pixel-tall line through the middle has no glyph; the nearest drawable pattern still has to be
            // closer to it than a single flat colour would be.
            var patch = Patch (2, 3);
            var cell = Assert.Single (Decode (One (patch)));
            var drawn = Pixels (cell);
            var want = Enumerable.Range (0, 8).Select (p => new[] { 2, 3 }.Contains (p) ? Ink : Paper).ToArray ();

            static long Error ((int R, int G, int B)[] a, (int R, int G, int B)[] b)
                => a.Zip (b, (x, y) => (long) ((x.R - y.R) * (x.R - y.R) + (x.G - y.G) * (x.G - y.G) + (x.B - y.B) * (x.B - y.B))).Sum ();

            var meanR = want.Average (c => c.R); var meanG = want.Average (c => c.G); var meanB = want.Average (c => c.B);
            var flat = Enumerable.Repeat (((int) meanR, (int) meanG, (int) meanB), 8).ToArray ();

            Assert.True (Error (drawn, want) < Error (flat, want));
        }

        // ── Diffing ──────────────────────────────────────────────────────────

        // 8x8 sub-pixels = 4 columns x 2 rows of cells.
        private static byte[] Screen (Func<int, int, (int R, int G, int B)> pixel) => Bitmap (8, 8, pixel);

        [Fact]
        public void FirstFramePaintsEveryCell ()
            => Assert.Equal (8, Decode (Encode (new TerminalBlockEncoder (TerminalColorMode.TrueColor), Screen ((_, _) => Paper), 8, 8)).Count);

        [Fact]
        public void AnUnchangedFrameWritesNothing ()
        {
            var encoder = new TerminalBlockEncoder (TerminalColorMode.TrueColor);
            var screen = Screen ((x, y) => x == 3 && y == 5 ? Ink : Paper);
            Encode (encoder, screen, 8, 8);

            Assert.Equal (string.Empty, Encode (encoder, screen, 8, 8));
        }

        [Fact]
        public void OnlyTheChangedCellIsRewrittenAndTheCursorJumpsToIt ()
        {
            var encoder = new TerminalBlockEncoder (TerminalColorMode.TrueColor);
            Encode (encoder, Screen ((_, _) => Paper), 8, 8);

            // Sub-pixel (5, 6) is in cell column 2, row 1.
            var ansi = Encode (encoder, Screen ((x, y) => x == 5 && y == 6 ? Ink : Paper), 8, 8);
            var cell = Assert.Single (Decode (ansi));

            Assert.Equal ((1, 2), (cell.Row, cell.Col));
            Assert.Contains ("\u001b[2;3H", ansi);
        }

        [Fact]
        public void AdjacentChangedCellsShareOneCursorMoveAndOneBackgroundSetting ()
        {
            var encoder = new TerminalBlockEncoder (TerminalColorMode.TrueColor);
            Encode (encoder, Screen ((_, _) => Paper), 8, 8);

            // Two neighbouring cells on row 0 turn the same flat colour.
            var ansi = Encode (encoder, Screen ((x, y) => y < 4 && x >= 2 && x < 6 ? (1, 2, 3) : Paper), 8, 8);

            Assert.Equal (2, Decode (ansi).Count);
            Assert.Equal (1, Regex.Count (ansi, "H"));
            Assert.Equal (1, Regex.Count (ansi, "48;2;1;2;3m"));
        }

        [Fact]
        public void ResetAndResizeRepaintEverything ()
        {
            var encoder = new TerminalBlockEncoder (TerminalColorMode.TrueColor);
            var screen = Screen ((_, _) => Paper);
            Encode (encoder, screen, 8, 8);
            encoder.Reset ();
            Assert.Equal (8, Decode (Encode (encoder, screen, 8, 8)).Count);

            Assert.Equal (4, Decode (Encode (encoder, Bitmap (4, 8, (_, _) => Paper), 4, 8)).Count);   // 2 columns x 2 rows
        }

        [Fact]
        public void ASizeThatIsNotWholeCellsRepeatsTheEdgeInsteadOfGrowingABlackBorder ()
        {
            // 3 x 5 sub-pixels: 2 columns x 2 rows of cells, the last of each only partly covered.
            var cells = Decode (Encode (new TerminalBlockEncoder (TerminalColorMode.TrueColor), Bitmap (3, 5, (_, _) => Paper), 3, 5));

            Assert.Equal (4, cells.Count);
            Assert.All (cells, c => Assert.Equal (Paper, c.Bg));
        }

        [Fact]
        public void FrameIsWrappedInSynchronizedOutput ()
        {
            var ansi = One (Patch (0, 1));

            Assert.StartsWith ("\u001b[?2026h", ansi);
            Assert.EndsWith ("\u001b[0m\u001b[?2026l", ansi);
        }

        // ── Colour modes ─────────────────────────────────────────────────────

        [Fact]
        public void TwoColoursTheTerminalCannotTellApartAreOneBlankCell ()
        {
            // 250 and 255 are both bright white in 16 colours: the pattern would be invisible.
            var encoder = new TerminalBlockEncoder (TerminalColorMode.Ansi16);
            var ansi = Encode (encoder, Bitmap (W, H, (x, y) => y < 2 ? (250, 250, 250) : (255, 255, 255)), W, H);

            Assert.Contains (" ", ansi);
            Assert.DoesNotContain ("▄", ansi);
        }

        [Fact]
        public void Ansi256WritesPaletteIndexes ()
        {
            var ansi = Encode (new TerminalBlockEncoder (TerminalColorMode.Ansi256), Bitmap (W, H, (_, y) => y < 2 ? (255, 0, 0) : (255, 255, 255)), W, H);

            Assert.Contains ("48;5;196m", ansi);   // red top half is the background of ▄
            Assert.Contains ("38;5;231m", ansi);   // white bottom half
            Assert.Contains ("▄", ansi);
        }

        // ── The shared palette ───────────────────────────────────────────────

        [Fact]
        public void EveryPaletteColourQuantisesToItself ()
        {
            for (uint i = 16; i < 256; i++) {
                var (r, g, b) = TerminalColors.Rgb (TerminalColorMode.Ansi256, i);

                Assert.Equal (i, TerminalColors.Quantize (TerminalColorMode.Ansi256, r, g, b));
            }
        }

        [Fact]
        public void The256MatchIsTheNearestPaletteColourUnderTheSameMetric ()
        {
            // Brute force over the 240 palette entries on a grid of colours (pastels are where rounding each
            // channel separately goes wrong).
            for (var r = 0; r < 256; r += 17)
                for (var g = 0; g < 256; g += 17)
                    for (var b = 0; b < 256; b += 17) {
                        var best = int.MaxValue;
                        for (uint k = 16; k < 256; k++) {
                            var (pr, pg, pb) = TerminalColors.Rgb (TerminalColorMode.Ansi256, k);
                            best = Math.Min (best, TerminalColors.Distance (r, g, b, pr, pg, pb));
                        }

                        var chosen = TerminalColors.Rgb (TerminalColorMode.Ansi256, TerminalColors.Quantize (TerminalColorMode.Ansi256, r, g, b));
                        Assert.Equal (best, TerminalColors.Distance (r, g, b, chosen.R, chosen.G, chosen.B));
                    }
        }

        [Fact]
        public void DistanceCountsGreenMostAndIsZeroForTheSameColour ()
        {
            Assert.Equal (0, TerminalColors.Distance (10, 20, 30, 10, 20, 30));
            Assert.True (TerminalColors.Distance (100, 100, 100, 100, 130, 100) > TerminalColors.Distance (100, 100, 100, 130, 100, 100));
            Assert.True (TerminalColors.Distance (100, 100, 100, 100, 130, 100) > TerminalColors.Distance (100, 100, 100, 100, 100, 130));
        }
    }
}
