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
    // the oracle, over every substring of a corpus in a spread of faces, sizes and rasterisation settings, Latin-1
    // and the pages past it alike.
    public class AdvanceTableParityTests
    {
        private static readonly string[] corpus = [
            "Customer number 4821",
            "Quarterly results for the northern region, adjusted for seasonality",
            "0123456789 +-*/=()[]{}<>.,;:!?'\"@#$%^&_|\\~`",
            "Ça été déjà vu: ½ ü ß Ø ÿ ñ « » ¿ ©",
            "tab\there nul\0 del\u007f nbsp shy­end",
            "mixed — “quoted” € naïve",           // past U+00FF: pages built on first use (F13)
            "Zażółć gęślą jaźń, Příliš žluťoučký kůň",
            "Τριμηνιαία αποτελέσματα Квартальные результаты",
            "ﬁ ﬂ \ufeff \u200b ‰ ™ → ∑ ≤ ￼ \uffff",  // ligature code points, zero-width, specials, a non-character
            "שלום مرحبا 中文 한국어",               // characters the face may not have: glyph 0's advance, as in Skia
            "emoji 😀 surrogate pair",            // a surrogate: Skia measures the whole run
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
                Check (new SKFont (typeface, size) { Hinting = SKFontHinting.Full, ForceAutoHinting = true, BaselineSnap = false, EmbeddedBitmaps = true }, typeface);
            }
        }

        private static void Check (SKFont font, SKTypeface typeface)
        {
            using (font) {
                var table = AdvanceTable.For (font, typeface);
                Assert.Equal (font.Size <= 256f, table is not null);
                var runFont = new RunFont (font, table);

                // The metrics the table keeps are the font's own (F14).
                var metrics = font.Metrics;
                if (!metrics.Equals (runFont.Metrics))
                    Assert.Fail ($"{typeface.FamilyName} {font.Size}px edging={font.Edging} subpixel={font.Subpixel} hinting={font.Hinting}: metrics differ");

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

        // Pages past U+00FF are built by whichever thread needs them first, with no lock.
        [Fact]
        public void Threads_building_pages_at_once_all_get_Skia_s_widths ()
        {
            var texts = new[] { corpus[5], corpus[6], corpus[7], corpus[8], corpus[9] };
            for (var round = 0; round < 20; round++) {
                // A size no other test uses, so every page starts unbuilt.
                var size = 17.25f + round / 64f;
                using var font = new SKFont (SKTypeface.Default, size) { Subpixel = true };
                var expected = Array.ConvertAll (texts, t => font.MeasureText (t));
                var wrong = new System.Collections.Concurrent.ConcurrentBag<string> ();

                System.Threading.Tasks.Parallel.For (0, 8, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, t => {
                    using var own = new SKFont (SKTypeface.Default, size) { Subpixel = true };
                    RunFont runFont = own;
                    for (var i = 0; i < texts.Length; i++) {
                        var k = (i + t) % texts.Length;
                        if (BitConverter.SingleToInt32Bits (runFont.Measure (texts[k])) != BitConverter.SingleToInt32Bits (expected[k]))
                            wrong.Add (texts[k]);
                    }
                });

                Assert.Empty (wrong);
            }
        }

        // Every setting that goes into Skia's scaler context is in the key, so fonts that differ only in one of them do
        // not share a table built for the other.
        [Fact]
        public void Fonts_that_differ_in_a_scaler_setting_do_not_share_a_table ()
        {
            var typeface = SKTypeface.Default;
            using var plain = new SKFont (typeface, 12f);
            using var autoHinted = new SKFont (typeface, 12f) { ForceAutoHinting = true };
            using var bitmaps = new SKFont (typeface, 12f) { EmbeddedBitmaps = true };
            using var unsnapped = new SKFont (typeface, 12f) { BaselineSnap = false };

            var table = AdvanceTable.For (plain, typeface);
            Assert.NotSame (table, AdvanceTable.For (autoHinted, typeface));
            Assert.NotSame (table, AdvanceTable.For (bitmaps, typeface));
            Assert.NotSame (table, AdvanceTable.For (unsnapped, typeface));
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
