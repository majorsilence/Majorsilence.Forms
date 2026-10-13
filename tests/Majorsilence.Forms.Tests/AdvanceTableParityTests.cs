using System;
using System.Collections.Generic;
using Majorsilence.Forms.Drawing.Text;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // AdvanceTable measures unshaped runs by adding up per-character advances in managed code, so report threads do
    // not queue on Skia's glyph-cache mutex (performance plan F11). MeasureCharacterRanges, MeasureRunWidth and
    // word wrap use it, and their results must not move by a single bit: this compares it with SKFont.MeasureText,
    // the oracle, over every substring of a corpus in a spread of faces, sizes and rasterisation settings.
    public class AdvanceTableParityTests
    {
        private static readonly string[] corpus = [
            "Customer number 4821",
            "Quarterly results for the northern region, adjusted for seasonality",
            "0123456789 +-*/=()[]{}<>.,;:!?'\"@#$%^&_|\\~`",
            "Ça été déjà vu: ½ ü ß Ø ÿ ñ « » ¿ ©",
            "tab\there nul\0 del\u007f nbsp shy­end",
            "mixed — “quoted” € naïve",           // past U+00FF: falls back to Skia for the run
            "emoji 😀 surrogate pair",
            "WWWWWiiiiilllll     ....",
            "",
        ];

        private static readonly float[] sizes = [1f, 6f, 7.5f, 8f, 9.333333f, 10f, 12f, 13.333333f, 16f, 24f, 41.666668f, 100f, 255.9f, 256f, 300f, 512f];

        public static IEnumerable<object[]> Faces ()
        {
            foreach (var family in new[] { "Arial", "Times New Roman", "Courier New", "DejaVu Sans", "sans-serif", "serif", "monospace" })
                foreach (var style in new[] { SKFontStyle.Normal, SKFontStyle.Bold, SKFontStyle.Italic })
                    yield return [family, style.Weight, style.Slant == SKFontStyleSlant.Italic];
        }

        [Theory]
        [MemberData (nameof (Faces))]
        public void Measure_matches_SKFont_MeasureText_bit_for_bit (string family, int weight, bool italic)
        {
            using var typeface = SKTypeface.FromFamilyName (family, (SKFontStyleWeight)weight, SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) ?? SKTypeface.Default;

            foreach (var size in sizes) {
                // As Font builds it, and SkiaSharp's defaults (no subpixel positioning, so hinted advances).
                Check (new SKFont (typeface, size) { Edging = SKFontEdging.SubpixelAntialias, Subpixel = true }, typeface);
                Check (new SKFont (typeface, size), typeface);
                Check (new SKFont (typeface, size) { Hinting = SKFontHinting.Full, Edging = SKFontEdging.Alias }, typeface);
            }
        }

        private static void Check (SKFont font, SKTypeface typeface)
        {
            using (font) {
                var table = AdvanceTable.For (font, typeface);
                Assert.Equal (font.Size <= 256f, table is not null);
                var runFont = new RunFont (font, table);

                foreach (var text in corpus) {
                    for (var start = 0; start <= text.Length; start++) {
                        for (var end = start; end <= text.Length; end++) {
                            var span = text.AsSpan (start, end - start);
                            var expected = font.MeasureText (span);
                            var actual = runFont.Measure (span);
                            if (BitConverter.SingleToInt32Bits (expected) != BitConverter.SingleToInt32Bits (actual))
                                Assert.Fail ($"{typeface.FamilyName} {font.Size}px edging={font.Edging} subpixel={font.Subpixel} hinting={font.Hinting}: " +
                                    $"\"{span.ToString ()}\" measured {actual:R}, SKFont.MeasureText {expected:R}");
                        }
                    }
                }
            }
        }

        [Fact]
        public void Fonts_of_the_same_face_and_size_share_one_table ()
        {
            var typeface = SKTypeface.Default;
            using var a = new SKFont (typeface, 12f) { Subpixel = true };
            using var b = new SKFont (typeface, 12f) { Subpixel = true };
            using var c = new SKFont (typeface, 13f) { Subpixel = true };

            Assert.Same (AdvanceTable.For (a, typeface), AdvanceTable.For (b, typeface));
            Assert.NotSame (AdvanceTable.For (a, typeface), AdvanceTable.For (c, typeface));
        }

        [Fact]
        public void A_Font_measures_runs_with_its_table ()
        {
            using var font = new Majorsilence.Forms.Drawing.Font ("Arial", 10f);
            var skFont = font.GetSKFont ();

            var runFont = font.GetRunFont (skFont);

            Assert.NotNull (runFont.Advances);
            Assert.Same (runFont.Advances, font.GetRunFont (skFont).Advances);
            Assert.Equal (skFont.MeasureText ("Customer number 4821"), runFont.Measure ("Customer number 4821"));
        }
    }
}
