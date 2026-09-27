using System;
using System.Linq;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;
using Color = System.Drawing.Color;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.Tests
{
    // A character followed by VARIATION SELECTOR-16 (U+FE0F) asks for its emoji presentation. WARNING SIGN (U+26A0) is the case that
    // exposed this: most text fonts include its plain monochrome triangle, so FontSubstitution.Covering saw primary already covering
    // "⚠" and never looked for an emoji face for the selector that followed -- drawn as a monochrome outline instead of the colour
    // triangle. Filed and evidenced on majorsilence/alert-buddy's tracker as #271/#281 ("Text: colour emoji on Android and iOS").
    public class EmojiVariationSelectorTests
    {
        private const string WarningSign = "⚠";
        private const string EmojiSelector = "️";
        private const string TextSelector = "︎";
        // An unambiguous emoji, no selector needed; a regression guard. U+1FAE0 MELTING FACE (Unicode 14.0) is new enough
        // that no plain text font's own glyph set will have picked it up by accident (checked below, not assumed).
        private const string MeltingFace = "\U0001FAE0";

        private static SKTypeface? _plainFontWithWarningGlyph;

        // The bug needs a plain text font that already contains a *monochrome* glyph for U+26A0 -- DejaVu Sans is that font
        // on the ubuntu-latest runner, but Windows and macOS runners don't have it installed, and a hardcoded name silently
        // resolves to some unrelated substitute (SKTypeface.FromFamilyName never returns null) whose FamilyName then fails
        // every assertion in this file for a reason that has nothing to do with the bug. Finding whichever installed family
        // actually satisfies the premise -- has the base glyph, renders it without colour, and does not also have the
        // regression guard's emoji -- makes every test here mean the same thing on every runner instead of assuming one.
        private static SKTypeface PlainFontWithWarningGlyph ()
        {
            if (_plainFontWithWarningGlyph is not null)
                return _plainFontWithWarningGlyph;

            foreach (var name in SKFontManager.Default.FontFamilies) {
                var typeface = SKTypeface.FromFamilyName (name);
                if (!string.Equals (typeface.FamilyName, name, StringComparison.OrdinalIgnoreCase))
                    continue; // FromFamilyName fell back to something else -- this name isn't really installed

                if (!typeface.ContainsGlyph (0x26A0) || typeface.ContainsGlyph (0x1FAE0))
                    continue;

                if (!IsMonochrome (typeface, 0x26A0))
                    continue;

                return _plainFontWithWarningGlyph = typeface;
            }

            throw new InvalidOperationException ("No installed font has a plain (monochrome) glyph for U+26A0 WARNING SIGN; this test fixture needs one to exist.");
        }

        // A monochrome glyph antialiased onto white only ever produces gray (R == G == B); this is the same test the pixel-level
        // tests below make of Graphics.DrawString's actual output, applied here directly to a candidate typeface at discovery time.
        private static bool IsMonochrome (SKTypeface typeface, int codepoint)
        {
            using var bitmap = new SKBitmap (48, 48, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas (bitmap);
            canvas.Clear (SKColors.White);
            using var font = new SKFont (typeface, 32);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText (char.ConvertFromUtf32 (codepoint), 4, 40, font, paint);
            canvas.Flush ();

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha == 0)
                        continue;

                    var spread = Math.Max (c.Red, Math.Max (c.Green, c.Blue)) - Math.Min (c.Red, Math.Min (c.Green, c.Blue));
                    if (spread > 24)
                        return false;
                }

            return true;
        }

        [Fact]
        public void The_warning_sign_alone_keeps_its_plain_face ()
        {
            // No selector: today's behaviour, unaffected. Pinned so the fix below is not mistaken for "always prefer emoji".
            var primary = PlainFontWithWarningGlyph ();

            var runs = FontSubstitution.SplitByCoverage (WarningSign, primary);

            var run = Assert.Single (runs);
            Assert.Equal (WarningSign, run.Text);
            Assert.Same (primary, run.Typeface);
        }

        [Fact]
        public void The_warning_sign_with_the_emoji_selector_switches_to_an_emoji_capable_face ()
        {
            var primary = PlainFontWithWarningGlyph ();

            var runs = FontSubstitution.SplitByCoverage (WarningSign + EmojiSelector, primary);

            var run = Assert.Single (runs);
            Assert.Equal (WarningSign + EmojiSelector, run.Text);
            Assert.NotSame (primary, run.Typeface);
            Assert.True (run.Typeface.ContainsGlyph (0x26A0), "the chosen face should still have a glyph for the base character");
        }

        [Fact]
        public void The_emoji_selector_never_starts_a_run_of_its_own ()
        {
            // Both codepoints in one run is the point: the selector is invisible and would resolve to some arbitrary fallback (or
            // primary itself) if walked on its own, which is exactly the bug -- not "wrong face", but "measured as its own glyph".
            var primary = PlainFontWithWarningGlyph ();

            var runs = FontSubstitution.SplitByCoverage (WarningSign + EmojiSelector, primary);

            Assert.Single (runs);
        }

        [Fact]
        public void The_warning_sign_with_the_text_selector_keeps_its_plain_face ()
        {
            // VARIATION SELECTOR-15 asks for the opposite: explicit text presentation. It must not trigger the emoji search, but the
            // selector still has to land in the same run as its base character.
            var primary = PlainFontWithWarningGlyph ();

            var runs = FontSubstitution.SplitByCoverage (WarningSign + TextSelector, primary);

            var run = Assert.Single (runs);
            Assert.Equal (WarningSign + TextSelector, run.Text);
            Assert.Same (primary, run.Typeface);
        }

        [Fact]
        public void An_unambiguous_emoji_with_no_selector_is_unaffected ()
        {
            var primary = PlainFontWithWarningGlyph ();
            Assert.False (primary.ContainsGlyph (0x1FAE0), "the plain font should not itself have a melting face glyph");

            var runs = FontSubstitution.SplitByCoverage (MeltingFace, primary);

            var run = Assert.Single (runs);
            Assert.Equal (MeltingFace, run.Text);
            Assert.NotSame (primary, run.Typeface);
        }

        [Fact]
        public void The_emoji_selector_at_the_end_of_a_longer_string_only_affects_its_own_run ()
        {
            var primary = PlainFontWithWarningGlyph ();

            var runs = FontSubstitution.SplitByCoverage ("Hot! " + WarningSign + EmojiSelector, primary);

            Assert.Equal (2, runs.Count);
            Assert.Equal ("Hot! ", runs[0].Text);
            Assert.Same (primary, runs[0].Typeface);
            Assert.Equal (WarningSign + EmojiSelector, runs[1].Text);
            Assert.NotSame (primary, runs[1].Typeface);
        }

        [Fact]
        public void A_selector_after_a_codepoint_no_font_at_all_covers_falls_back_the_same_way_the_plain_path_does ()
        {
            // A Private Use Area codepoint: no installed font defines a real glyph for it, so the emoji-hinted
            // lookup finds nothing either. What "falls back" resolves to is platform-dependent -- CoreText on
            // macOS hands back its own generic ".LastResort" box-glyph face instead of null the way fontconfig
            // does on Linux -- so the invariant this proves is that EmojiCovering's fallback exactly matches
            // whatever the ordinary, no-selector path already resolves to for the same codepoint on this
            // platform, not a specific face chosen in advance.
            const string PrivateUse = "";
            var primary = PlainFontWithWarningGlyph ();

            var expected = Assert.Single (FontSubstitution.SplitByCoverage (PrivateUse, primary)).Typeface;
            var runs = FontSubstitution.SplitByCoverage (PrivateUse + EmojiSelector, primary);

            var run = Assert.Single (runs);
            Assert.Same (expected, run.Typeface);
        }

        [Fact]
        public void A_trailing_selector_with_nothing_before_it_does_not_throw ()
        {
            var primary = PlainFontWithWarningGlyph ();

            var runs = FontSubstitution.SplitByCoverage (EmojiSelector, primary);

            Assert.Single (runs);
        }

        // ---- TextMeasurer.CreateTextBlock: the Label/DrawString path, at the run level ---------------

        private static Topten.RichTextKit.TextBlock Block (string text)
        {
            // TextMeasurer's TextBlock cache is a static field. A persistent VSTest test host that outlives one
            // `dotnet test` invocation can carry a correct, cached result across into a later run of the very
            // same process, silently reusing it -- caught as a real mutation-testing false pass, not a theory:
            // a broken CreateTextBlock still "passed" here until this line was added. Clearing it first makes
            // every call in this class measure the current code, not whatever ran earlier in the same host.
            TextMeasurer.ClearTextBlockCache ();
            var primary = PlainFontWithWarningGlyph ();
            return TextMeasurer.CreateTextBlock (text, primary, 24, new Size (2000, 2000));
        }

        private static int TotalRunLength (Topten.RichTextKit.TextBlock block)
        {
            var total = 0;
            foreach (var run in block.FontRuns)
                total += run.Length;
            return total;
        }

        [Fact]
        public void CreateTextBlock_uses_one_typeface_when_there_is_no_selector ()
        {
            var block = Block ("Workshop is warm");

            var typefaces = block.FontRuns.Select (r => r.Typeface).Distinct ().ToList ();

            Assert.Single (typefaces);
            Assert.Equal ("Workshop is warm".Length, TotalRunLength (block));
        }

        [Fact]
        public void CreateTextBlock_uses_a_second_typeface_only_for_the_emoji_selector_sequence ()
        {
            var text = "Hot! " + WarningSign + EmojiSelector;

            var block = Block (text);

            var typefaces = block.FontRuns.Select (r => r.Typeface).Distinct ().ToList ();
            Assert.Equal (2, typefaces.Count);
            // The whole string is still accounted for -- no run silently dropped at either end.
            Assert.Equal (text.Length, TotalRunLength (block));
        }

        [Fact]
        public void CreateTextBlock_keeps_the_text_after_the_sequence_too ()
        {
            // The emoji sequence sits in the middle here, unlike the "Hot! ⚠️" test above, so a run dropped after it
            // (rather than before it) is what this catches.
            var text = "Hot! " + WarningSign + EmojiSelector + " zone";

            var block = Block (text);

            var typefaces = block.FontRuns.Select (r => r.Typeface).Distinct ().ToList ();
            Assert.Equal (2, typefaces.Count);
            Assert.Equal (text.Length, TotalRunLength (block));
        }

        [Fact]
        public void CreateTextBlock_keeps_one_typeface_for_the_text_selector ()
        {
            var text = "Hot! " + WarningSign + TextSelector;

            var block = Block (text);

            var typefaces = block.FontRuns.Select (r => r.Typeface).Distinct ().ToList ();
            Assert.Single (typefaces);
            Assert.Equal (text.Length, TotalRunLength (block));
        }

        // ---- Rendered, not just chosen: the same claim, at the pixel level -------------------------

        private static SKBitmap Draw (string text, Font font)
        {
            var bitmap = new SKBitmap (60, 60, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas (bitmap);
            canvas.Clear (SKColors.White);
            using (var graphics = new Graphics (canvas))
                graphics.DrawString (text, font, new SolidBrush (Color.Black), 4, 4);
            canvas.Flush ();
            return bitmap;
        }

        // A monochrome glyph antialiased onto white only ever produces gray (R == G == B); a colour glyph (Noto Color Emoji's
        // warning triangle is yellow/orange) produces pixels where the channels genuinely differ.
        private static bool HasColourInk (SKBitmap bitmap)
        {
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha == 0)
                        continue;

                    var spread = Math.Max (c.Red, Math.Max (c.Green, c.Blue)) - Math.Min (c.Red, Math.Min (c.Green, c.Blue));
                    if (spread > 24)
                        return true;
                }

            return false;
        }

        [Fact]
        public void The_warning_sign_alone_draws_no_colour_ink ()
        {
            HeadlessRenderer.Use ();
            using var font = new Font (PlainFontWithWarningGlyph ().FamilyName, 28f, FontStyle.Regular, GraphicsUnit.Pixel);

            using var bitmap = Draw (WarningSign, font);

            Assert.False (HasColourInk (bitmap), "a plain warning sign should render as a monochrome glyph");
        }

        [Fact]
        public void The_warning_sign_with_the_emoji_selector_draws_real_colour_ink ()
        {
            HeadlessRenderer.Use ();
            using var font = new Font (PlainFontWithWarningGlyph ().FamilyName, 28f, FontStyle.Regular, GraphicsUnit.Pixel);

            using var bitmap = Draw (WarningSign + EmojiSelector, font);

            if (OperatingSystem.IsMacOS ()) {
                // Confirmed on a real macOS CI runner, not assumed: the coverage-level tests above already
                // prove FontSubstitution picks Apple Color Emoji correctly for this run (the fix this PR
                // makes), but Topten.RichTextKit's TextBlock.Paint still rasterises that face's sbix colour
                // table as a monochrome shape on macOS specifically -- a Skia/CoreGraphics colour-glyph
                // rendering gap in a third-party dependency (see e.g. mono/SkiaSharp#3244), not something
                // this change can reach from here. What this platform CAN still prove is that a real glyph
                // was drawn at all, not silently dropped.
                Assert.True (HasAnyInk (bitmap), "expected the emoji-selector sequence to draw something, even without colour on this platform");
                return;
            }

            Assert.True (HasColourInk (bitmap), "⚠️ (with the emoji selector) should draw as a coloured emoji glyph, not a grayscale outline");
        }

        private static bool HasAnyInk (SKBitmap bitmap)
        {
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y).Alpha != 0)
                        return true;

            return false;
        }
    }
}
