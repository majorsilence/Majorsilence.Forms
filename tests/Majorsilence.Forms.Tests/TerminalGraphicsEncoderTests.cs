using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Majorsilence.Forms.Terminal;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The Kitty and Sixel presenters. Neither can be eyeballed in a unit test, so each test decodes the
    // bytes the encoder wrote with an independent decoder (a protocol reader written here, not the
    // encoder's inverse) and compares pixels: that is what a terminal would draw.
    public class TerminalGraphicsEncoderTests
    {
        private static byte[] Bitmap (int w, int h, Func<int, int, (byte R, byte G, byte B)> pixel)
        {
            var data = new byte[w * h * 4];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++) {
                    var (r, g, b) = pixel (x, y);
                    var o = (y * w + x) * 4;
                    data[o] = b;
                    data[o + 1] = g;
                    data[o + 2] = r;
                    data[o + 3] = 255;
                }
            return data;
        }

        private static string Encode (ITerminalFramePresenter presenter, byte[] bgra, int w, int h)
        {
            var output = new ArrayBufferWriter<byte> ();
            presenter.Encode (bgra, w, h, w * 4, output);
            return Encoding.ASCII.GetString (output.WrittenSpan);
        }

        // ── Kitty ─────────────────────────────────────────────────────────────

        private sealed record KittyTile (int Row, int Col, Dictionary<string, string> Control, byte[] Rgb);

        // Each tile is "cursor to its first cell, then one APC command in 1..n chunks". Reassembles them.
        private static List<KittyTile> DecodeKitty (string ansi)
        {
            var tiles = new List<KittyTile> ();
            var moves = Regex.Matches (ansi, "\u001b\\[(\\d+);(\\d+)H");
            for (var i = 0; i < moves.Count; i++) {
                var start = moves[i].Index + moves[i].Length;
                var end = i + 1 < moves.Count ? moves[i + 1].Index : ansi.Length;
                var chunks = Regex.Matches (ansi[start..end], "\u001b_G([^;]*);([^\u001b]*)\u001b\\\\");
                if (chunks.Count == 0)
                    continue;

                var control = new Dictionary<string, string> ();
                foreach (var pair in chunks[0].Groups[1].Value.Split (','))
                    control[pair.Split ('=')[0]] = pair.Split ('=')[1];

                var payload = new StringBuilder ();
                foreach (Match chunk in chunks)
                    payload.Append (chunk.Groups[2].Value);

                using var z = new ZLibStream (new MemoryStream (Convert.FromBase64String (payload.ToString ())), CompressionMode.Decompress);
                using var raw = new MemoryStream ();
                z.CopyTo (raw);
                tiles.Add (new KittyTile (int.Parse (moves[i].Groups[1].Value), int.Parse (moves[i].Groups[2].Value), control, raw.ToArray ()));
            }
            return tiles;
        }

        // Lays tiles out as a terminal would (each at its first cell) and returns the RGB frame.
        private static byte[] Compose (List<KittyTile> tiles, int width, int height, int cols, int rows, byte[]? onto = null)
        {
            var canvas = onto ?? new byte[width * height * 3];
            foreach (var t in tiles) {
                var x0 = (int) Math.Round ((t.Col - 1) * (double) width / cols);
                var y0 = (int) Math.Round ((t.Row - 1) * (double) height / rows);
                var tw = int.Parse (t.Control["s"]);
                var th = int.Parse (t.Control["v"]);
                for (var y = 0; y < th; y++)
                    Array.Copy (t.Rgb, y * tw * 3, canvas, ((y0 + y) * width + x0) * 3, tw * 3);
            }
            return canvas;
        }

        private static byte[] ToRgb (byte[] bgra)
        {
            var rgb = new byte[bgra.Length / 4 * 3];
            for (var i = 0; i < bgra.Length / 4; i++) {
                rgb[i * 3] = bgra[i * 4 + 2];
                rgb[i * 3 + 1] = bgra[i * 4 + 1];
                rgb[i * 3 + 2] = bgra[i * 4];
            }
            return rgb;
        }

        // 40x16 pixels over 8x4 cells (5x4 px each), in tiles of 4x2 cells: a 2x2 grid of tiles.
        private static (TerminalKittyEncoder Kitty, byte[] Frame) TwoByTwo ()
            => (new TerminalKittyEncoder (8, 4, tileCols: 4, tileRows: 2),
                Bitmap (40, 16, (x, y) => ((byte) (x * 6), (byte) (y * 15), (byte) (x * y % 251))));

        [Fact]
        public void KittyTilesReassembleToTheSourceFrame ()
        {
            var (kitty, frame) = TwoByTwo ();
            var tiles = DecodeKitty (Encode (kitty, frame, 40, 16));

            Assert.Equal (4, tiles.Count);
            Assert.Equal (ToRgb (frame), Compose (tiles, 40, 16, 8, 4));
        }

        [Fact]
        public void KittyTilesCoverEveryCellExactlyOnceIncludingSmallerEdgeTiles ()
        {
            // 10x5 cells in 4x2 tiles: 3 tiles across (4, 4, 2 cells) and 3 down (2, 2, 1 rows).
            var kitty = new TerminalKittyEncoder (10, 5, tileCols: 4, tileRows: 2);
            var tiles = DecodeKitty (Encode (kitty, Bitmap (50, 20, (_, _) => (1, 2, 3)), 50, 20));

            Assert.Equal (9, tiles.Count);
            Assert.Equal (50, tiles.Sum (t => int.Parse (t.Control["c"]) * int.Parse (t.Control["r"])));
            Assert.Equal (9, tiles.Select (t => t.Control["i"]).Distinct ().Count ());
            Assert.Contains (tiles, t => t.Control["c"] == "2" && t.Control["r"] == "1");   // the corner tile
        }

        [Fact]
        public void KittyTilesMeetExactlyWhenThePixelWidthDoesNotDivideTheCells ()
        {
            // 47 px over 8 cells is 5.875 px a cell. Tiles must neither gap nor overlap in pixels.
            var frame = Bitmap (47, 16, (x, y) => ((byte) (x * 5), (byte) (y * 15), (byte) 9));
            var tiles = DecodeKitty (Encode (new TerminalKittyEncoder (8, 4, tileCols: 4, tileRows: 2), frame, 47, 16));

            Assert.Equal (ToRgb (frame), Compose (tiles, 47, 16, 8, 4));
        }

        [Fact]
        public void KittyEachTileIsPlacedOverItsOwnCellsAndLeavesTheCursor ()
        {
            var (kitty, frame) = TwoByTwo ();
            var ansi = Encode (kitty, frame, 40, 16);
            var tile = DecodeKitty (ansi)[0];

            Assert.Equal ("T", tile.Control["a"]);
            Assert.Equal ("24", tile.Control["f"]);
            Assert.Equal ("z", tile.Control["o"]);
            Assert.Equal ("4", tile.Control["c"]);   // stretched over its 4x2 cells, so a wrong cell-size guess still fits
            Assert.Equal ("2", tile.Control["r"]);
            Assert.Equal ("1", tile.Control["C"]);   // the cursor does not move
            Assert.Equal ("2", tile.Control["q"]);   // no reply is requested: it would arrive as input
            Assert.True (int.Parse (tile.Control["i"]) >= TerminalKittyEncoder.FirstImageId);
            Assert.StartsWith ("\u001b[?2026h", ansi);
            Assert.EndsWith ("\u001b[?2026l", ansi);
        }

        [Fact]
        public void KittyChangedPixelResendsOnlyItsTile ()
        {
            var (kitty, frame) = TwoByTwo ();
            Encode (kitty, frame, 40, 16);

            // Pixel (22, 9): cell column 4, row 2 -> tile column 1, tile row 1 -> the fourth tile.
            var changed = Bitmap (40, 16, (x, y) => x == 22 && y == 9 ? ((byte) 255, (byte) 255, (byte) 255) : ((byte) (x * 6), (byte) (y * 15), (byte) (x * y % 251)));
            var tiles = DecodeKitty (Encode (kitty, changed, 40, 16));

            var tile = Assert.Single (tiles);
            Assert.Equal ((3, 5), (tile.Row, tile.Col));   // 1-based: cell row 2, column 4
            Assert.Equal ((TerminalKittyEncoder.FirstImageId + 3).ToString (), tile.Control["i"]);

            // Applying just that tile to the previous screen gives the new frame.
            var screen = Compose (DecodeKitty (Encode (new TerminalKittyEncoder (8, 4, 4, 2), frame, 40, 16)), 40, 16, 8, 4);
            Assert.Equal (ToRgb (changed), Compose (tiles, 40, 16, 8, 4, onto: screen));
        }

        [Fact]
        public void KittyChangesInTwoTilesResendTwoTiles ()
        {
            var (kitty, frame) = TwoByTwo ();
            Encode (kitty, frame, 40, 16);

            var changed = Bitmap (40, 16, (x, y) => (x == 1 && y == 1) || (x == 38 && y == 14) ? ((byte) 255, (byte) 0, (byte) 255) : ((byte) (x * 6), (byte) (y * 15), (byte) (x * y % 251)));

            Assert.Equal (2, DecodeKitty (Encode (kitty, changed, 40, 16)).Count);
        }

        [Fact]
        public void KittyUnchangedFrameSendsNothing ()
        {
            var (kitty, frame) = TwoByTwo ();
            Encode (kitty, frame, 40, 16);

            Assert.Equal (string.Empty, Encode (kitty, frame, 40, 16));
        }

        [Fact]
        public void KittyResetAndResizeResendEveryTile ()
        {
            var (kitty, frame) = TwoByTwo ();
            Encode (kitty, frame, 40, 16);
            kitty.Reset ();
            Assert.Equal (4, DecodeKitty (Encode (kitty, frame, 40, 16)).Count);

            // A different pixel size is a different frame, even if every pixel were the same colour.
            var resized = Bitmap (45, 16, (_, _) => (1, 1, 1));
            Assert.Equal (4, DecodeKitty (Encode (kitty, resized, 45, 16)).Count);
        }

        [Fact]
        public void KittyLargeTileIsChunkedAndEveryChunkButTheLastContinues ()
        {
            // Noise does not compress, so one tile's payload is well over one 4096-character chunk.
            var rng = new Random (1);
            var frame = Bitmap (64, 64, (_, _) => ((byte) rng.Next (256), (byte) rng.Next (256), (byte) rng.Next (256)));
            var ansi = Encode (new TerminalKittyEncoder (8, 4, tileCols: 8, tileRows: 4), frame, 64, 64);

            var matches = Regex.Matches (ansi, "\u001b_G([^;]*);([^\u001b]*)\u001b\\\\");
            Assert.True (matches.Count > 1);
            for (var i = 0; i < matches.Count; i++) {
                Assert.True (matches[i].Groups[2].Length <= 4096);
                Assert.Contains (i < matches.Count - 1 ? "m=1" : "m=0", matches[i].Groups[1].Value);
            }

            Assert.Equal (ToRgb (frame), Compose (DecodeKitty (ansi), 64, 64, 8, 4));
        }

        [Fact]
        public void KittyExitDeletesItsImages ()
            => Assert.Equal ("\u001b_Ga=d,d=A,q=2\u001b\\", new TerminalKittyEncoder (1, 1).ExitSequence);

        // ── Sixel ─────────────────────────────────────────────────────────────

        private sealed record SixelImage (int Row, int Col, int Width, int Height, (int R, int G, int B)?[,] Pixels);

        // A minimal Sixel reader: palette definitions, colour selection, run-length repeats, CR and band feed.
        private static SixelImage DecodeSixel (string ansi)
        {
            var pos = Regex.Match (ansi, "\u001b\\[(\\d+);(\\d+)H\u001bP");
            Assert.True (pos.Success, "no cursor move before the sixel image");

            var start = ansi.IndexOf ('q', ansi.IndexOf ("\u001bP", StringComparison.Ordinal)) + 1;
            var end = ansi.IndexOf ("\u001b\\", start, StringComparison.Ordinal);
            var body = ansi[start..end];

            var raster = Regex.Match (body, "^\"1;1;(\\d+);(\\d+)");
            Assert.True (raster.Success, "no raster attributes");
            var w = int.Parse (raster.Groups[1].Value);
            var h = int.Parse (raster.Groups[2].Value);
            var pixels = new (int, int, int)?[h, w];

            var palette = new Dictionary<int, (int, int, int)> ();
            var color = 0;
            int x = 0, band = 0;
            var i = raster.Length;
            while (i < body.Length) {
                var c = body[i];
                if (c == '#') {
                    var m = Regex.Match (body[i..], "^#(\\d+)(?:;2;(\\d+);(\\d+);(\\d+))?");
                    color = int.Parse (m.Groups[1].Value);
                    if (m.Groups[2].Success)
                        palette[color] = (Pct (m.Groups[2]), Pct (m.Groups[3]), Pct (m.Groups[4]));
                    i += m.Length;
                } else if (c == '$') {
                    x = 0;
                    i++;
                } else if (c == '-') {
                    x = 0;
                    band++;
                    i++;
                } else {
                    var repeat = 1;
                    if (c == '!') {
                        var m = Regex.Match (body[i..], "^!(\\d+)");
                        repeat = int.Parse (m.Groups[1].Value);
                        i += m.Length;
                        c = body[i];
                    }
                    var bits = c - 63;
                    for (var r = 0; r < repeat; r++, x++)
                        for (var bit = 0; bit < 6; bit++) {
                            var y = band * 6 + bit;
                            if ((bits >> bit & 1) != 0 && y < h && x < w)
                                pixels[y, x] = palette[color];
                        }
                    i++;
                }
            }

            return new SixelImage (int.Parse (pos.Groups[1].Value), int.Parse (pos.Groups[2].Value), w, h, pixels);

            static int Pct (Group g) => (int.Parse (g.Value) * 255 + 50) / 100;
        }

        private static void AssertClose ((int R, int G, int B)? actual, (byte R, byte G, byte B) expected)
        {
            Assert.NotNull (actual);
            // Sixel colours are percentages and the encoder keeps 6 bits a channel: a few steps of 255.
            Assert.InRange (actual.Value.R, expected.R - 6, expected.R + 6);
            Assert.InRange (actual.Value.G, expected.G - 6, expected.G + 6);
            Assert.InRange (actual.Value.B, expected.B - 6, expected.B + 6);
        }

        [Fact]
        public void SixelFirstFrameDrawsTheWholeImageFromTheTopLeftCell ()
        {
            // 16x14 is deliberately not a multiple of 6 rows, to exercise the padded last band.
            var bmp = Bitmap (16, 14, (x, y) => x < 8 ? ((byte) 200, (byte) 40, (byte) 40) : y < 7 ? ((byte) 30, (byte) 90, (byte) 220) : ((byte) 250, (byte) 250, (byte) 250));
            var img = DecodeSixel (Encode (new TerminalSixelEncoder (8, 7), bmp, 16, 14));

            Assert.Equal ((1, 1, 16, 14), (img.Row, img.Col, img.Width, img.Height));
            AssertClose (img.Pixels[0, 0], (200, 40, 40));
            AssertClose (img.Pixels[13, 7], (200, 40, 40));
            AssertClose (img.Pixels[0, 8], (30, 90, 220));
            AssertClose (img.Pixels[13, 15], (250, 250, 250));
        }

        [Fact]
        public void SixelFlatColoursAreExactNotTheBucketCentre ()
        {
            // 240 sits in a 6-bit bucket whose centre is 243; a UI's flat grey must not shift.
            var img = DecodeSixel (Encode (new TerminalSixelEncoder (4, 6), Bitmap (4, 6, (_, _) => (240, 240, 240)), 4, 6));

            // Sixel colours are whole percents: 240/255 = 94.1% -> 94% -> 240.
            Assert.Equal ((240, 240, 240), img.Pixels[0, 0]);
        }

        [Fact]
        public void SixelEveryPixelOfAGradientIsWithinToleranceWhenColoursFitThePalette ()
        {
            // 16 x 12 = 192 distinct colours, all of which fit the 256-entry palette.
            var bmp = Bitmap (16, 12, (x, y) => ((byte) (x * 10), (byte) (y * 20), (byte) 128));
            var img = DecodeSixel (Encode (new TerminalSixelEncoder (4, 6), bmp, 16, 12));

            for (var y = 0; y < 12; y++)
                for (var x = 0; x < 16; x++)
                    AssertClose (img.Pixels[y, x], ((byte) (x * 10), (byte) (y * 20), (byte) 128));
        }

        [Fact]
        public void SixelMoreColoursThanThePaletteHoldMapToTheNearest ()
        {
            // 40x40 distinct colours spread over the cube: far over 256, so some must be merged.
            var bmp = Bitmap (40, 40, (x, y) => ((byte) (x * 6), (byte) (y * 6), (byte) ((x + y) * 3)));
            var ansi = Encode (new TerminalSixelEncoder (8, 8), bmp, 40, 40);
            var img = DecodeSixel (ansi);

            Assert.True (Regex.Count (ansi, "#\\d+;2;") <= 256);
            for (var y = 0; y < 40; y++)
                for (var x = 0; x < 40; x++)
                    Assert.NotNull (img.Pixels[y, x]);   // every pixel is drawn, even when its colour was approximated
        }

        [Fact]
        public void SixelUnchangedFrameSendsNothing ()
        {
            var sixel = new TerminalSixelEncoder (4, 4);
            var bmp = Bitmap (8, 8, (_, _) => (20, 30, 40));
            Encode (sixel, bmp, 8, 8);

            Assert.Equal (string.Empty, Encode (sixel, bmp, 8, 8));
        }

        [Fact]
        public void SixelChangedPixelRepaintsOnlyItsCellAtItsPosition ()
        {
            var sixel = new TerminalSixelEncoder (8, 16);
            var before = Bitmap (80, 64, (_, _) => (10, 10, 10));
            Encode (sixel, before, 80, 64);

            // Pixel (35, 40) is in cell column 4, row 2 (8x16 cells).
            var after = Bitmap (80, 64, (x, y) => x == 35 && y == 40 ? ((byte) 255, (byte) 0, (byte) 0) : ((byte) 10, (byte) 10, (byte) 10));
            var img = DecodeSixel (Encode (sixel, after, 80, 64));

            Assert.Equal ((3, 5), (img.Row, img.Col));          // 1-based row 3, column 5
            Assert.Equal ((8, 16), (img.Width, img.Height));    // exactly one cell
            AssertClose (img.Pixels[40 - 32, 35 - 32], (255, 0, 0));
            AssertClose (img.Pixels[0, 0], (10, 10, 10));
        }

        [Fact]
        public void SixelRepaintCoversTheBoundingBoxOfSeparateChanges ()
        {
            var sixel = new TerminalSixelEncoder (8, 16);
            Encode (sixel, Bitmap (80, 64, (_, _) => (10, 10, 10)), 80, 64);

            var after = Bitmap (80, 64, (x, y) => (x == 1 && y == 1) || (x == 79 && y == 60) ? ((byte) 255, (byte) 255, (byte) 255) : ((byte) 10, (byte) 10, (byte) 10));
            var img = DecodeSixel (Encode (sixel, after, 80, 64));

            Assert.Equal ((1, 1), (img.Row, img.Col));
            Assert.Equal ((80, 64), (img.Width, img.Height));   // from the first changed cell to the last
        }

        [Fact]
        public void SixelWithAGuessedCellSizeRepaintsTheWholeImageFromTheTopLeft ()
        {
            // A region is placed by cell position, which is only right if the cell size is the terminal's.
            // xterm (which never answers the size query) drew duplicated text when it was not.
            var sixel = new TerminalSixelEncoder (8, 16, cellsKnown: false);
            Encode (sixel, Bitmap (80, 64, (_, _) => (10, 10, 10)), 80, 64);

            var after = Bitmap (80, 64, (x, y) => x == 35 && y == 40 ? ((byte) 255, (byte) 0, (byte) 0) : ((byte) 10, (byte) 10, (byte) 10));
            var img = DecodeSixel (Encode (sixel, after, 80, 64));

            Assert.Equal ((1, 1), (img.Row, img.Col));
            Assert.Equal ((80, 64), (img.Width, img.Height));
            AssertClose (img.Pixels[40, 35], (255, 0, 0));
        }

        [Fact]
        public void SixelWithAGuessedCellSizeStillSkipsAnUnchangedFrame ()
        {
            var sixel = new TerminalSixelEncoder (8, 16, cellsKnown: false);
            var bmp = Bitmap (80, 64, (_, _) => (10, 10, 10));
            Encode (sixel, bmp, 80, 64);

            Assert.Equal (string.Empty, Encode (sixel, bmp, 80, 64));
        }

        [Theory]
        [InlineData (100, 30, 800, 600, 8, 20)]    // xterm: pixel fields filled in
        [InlineData (120, 40, 1080, 720, 9, 18)]
        [InlineData (100, 30, 850, 610, 8, 20)]    // a border leaves a remainder: whole pixels per cell
        public void CellSizeIsThePixelSizeOverTheGrid (int cols, int rows, int xpx, int ypx, int w, int h)
            => Assert.Equal ((w, h), TerminalWindowSize.CellFromWinSize (cols, rows, xpx, ypx));

        [Theory]
        [InlineData (100, 30, 0, 0)]       // a terminal that does not know its pixel size reports zeros
        [InlineData (100, 30, 800, 0)]
        [InlineData (0, 30, 800, 600)]
        [InlineData (100, 30, 50, 600)]    // fewer pixels than columns: not a real size
        public void ZeroOrImplausiblePixelSizesGiveNoCellSize (int cols, int rows, int xpx, int ypx)
            => Assert.Null (TerminalWindowSize.CellFromWinSize (cols, rows, xpx, ypx));

        [Fact]
        public void SixelResizeRepaintsEverything ()
        {
            var sixel = new TerminalSixelEncoder (8, 16);
            Encode (sixel, Bitmap (16, 32, (_, _) => (1, 1, 1)), 16, 32);

            var img = DecodeSixel (Encode (sixel, Bitmap (24, 32, (_, _) => (1, 1, 1)), 24, 32));

            Assert.Equal ((24, 32), (img.Width, img.Height));
        }

        [Fact]
        public void SixelImageDoesNotPaintItsPaddingRows ()
        {
            // P2=1 in the DCS header: a zero bit leaves the screen alone, so the unused rows of the last band
            // cannot overwrite the cell below.
            var ansi = Encode (new TerminalSixelEncoder (8, 7), Bitmap (8, 7, (_, _) => (9, 9, 9)), 8, 7);

            Assert.Contains ("\u001bP0;1;0q", ansi);
        }

        // ── Mode detection and input additions ────────────────────────────────

        private static Func<string, string?> Env (params (string Key, string Value)[] vars)
        {
            var map = new Dictionary<string, string> ();
            foreach (var (k, v) in vars)
                map[k] = v;
            return name => map.TryGetValue (name, out var v) ? v : null;
        }

        [Theory]
        [InlineData ("TERM", "xterm-kitty", TerminalGraphicsMode.Kitty)]
        [InlineData ("KITTY_WINDOW_ID", "3", TerminalGraphicsMode.Kitty)]
        [InlineData ("TERM_PROGRAM", "WezTerm", TerminalGraphicsMode.Kitty)]
        [InlineData ("TERM_PROGRAM", "ghostty", TerminalGraphicsMode.Kitty)]
        [InlineData ("TERM", "foot", TerminalGraphicsMode.Sixel)]
        [InlineData ("TERM", "mlterm", TerminalGraphicsMode.Sixel)]
        [InlineData ("TERM_PROGRAM", "iTerm.app", TerminalGraphicsMode.Sixel)]
        [InlineData ("TERM", "xterm-256color", TerminalGraphicsMode.HalfBlock)]
        [InlineData ("WT_SESSION", "x", TerminalGraphicsMode.HalfBlock)]
        public void GraphicsModeIsDetectedFromTheEnvironment (string key, string value, TerminalGraphicsMode expected)
            => Assert.Equal (expected, TerminalCapabilities.DetectGraphicsMode (Env ((key, value))));

        [Fact]
        public void NothingKnownMeansHalfBlocks ()
            => Assert.Equal (TerminalGraphicsMode.HalfBlock, TerminalCapabilities.DetectGraphicsMode (Env ()));

        [Fact]
        public void MultiplexersForceHalfBlocksEvenInAGraphicsTerminal ()
        {
            Assert.Equal (TerminalGraphicsMode.HalfBlock, TerminalCapabilities.DetectGraphicsMode (Env (("TERM", "xterm-kitty"), ("TMUX", "/tmp/tmux-1/default,1,0"))));
            Assert.Equal (TerminalGraphicsMode.HalfBlock, TerminalCapabilities.DetectGraphicsMode (Env (("KITTY_WINDOW_ID", "1"), ("STY", "123.pts-0"))));
        }

        [Theory]
        [InlineData ("kitty", TerminalGraphicsMode.Kitty)]
        [InlineData ("SIXEL", TerminalGraphicsMode.Sixel)]
        [InlineData ("halfblock", TerminalGraphicsMode.HalfBlock)]
        public void TheEnvironmentOverrideBeatsDetectionAndMultiplexers (string value, TerminalGraphicsMode expected)
            => Assert.Equal (expected, TerminalCapabilities.DetectGraphicsMode (Env (("MF_TERMINAL_GRAPHICS", value), ("TMUX", "x"), ("TERM", "foot"))));

        private static List<TerminalInput> Parse (string text, bool mousePixels = false)
        {
            var parser = new TerminalInputParser { MousePixels = mousePixels };
            var events = new List<TerminalInput> ();
            parser.Feed (Encoding.UTF8.GetBytes (text), events);
            return events;
        }

        [Fact]
        public void CellSizeReplyCarriesWidthThenHeightFromTheHeightFirstWireOrder ()
        {
            // ESC[6;<height>;<width>t -- height comes first on the wire.
            var e = Assert.Single (Parse ("\u001b[6;18;9t"));

            Assert.Equal (TerminalInputKind.CellSize, e.Kind);
            Assert.Equal (9, e.Col);
            Assert.Equal (18, e.Row);
        }

        [Theory]
        [InlineData ("\u001b[6;0;0t")]      // a terminal that does not know its pixel size says zero
        [InlineData ("\u001b[8;24;80t")]    // a different report
        [InlineData ("\u001b[6;18t")]       // truncated
        public void ImplausibleSizeRepliesAreIgnored (string reply)
            => Assert.Empty (Parse (reply));

        [Fact]
        public void MouseReportsAreFlaggedAsPixelsOnlyWhenPixelModeIsOn ()
        {
            Assert.False (Assert.Single (Parse ("\u001b[<0;100;50M")).InPixels);

            var e = Assert.Single (Parse ("\u001b[<0;100;50M", mousePixels: true));
            Assert.True (e.InPixels);
            Assert.Equal (99, e.Col);
            Assert.Equal (49, e.Row);
        }
    }
}
