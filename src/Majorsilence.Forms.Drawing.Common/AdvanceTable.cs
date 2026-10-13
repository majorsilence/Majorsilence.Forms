using System;
using System.Collections.Concurrent;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Text
{
    /// <summary>
    /// The advance of every character from U+0000 to U+00FF in one font at one size, so an unshaped run of such
    /// characters is measured by adding them up in managed code instead of calling <c>SKFont.MeasureText</c>.
    /// </summary>
    /// <remarks>
    /// Every <c>SKFont.MeasureText</c> goes through Skia's process-wide glyph cache, which is guarded by one mutex,
    /// so report threads measuring cells at the same time queue for it: eight threads measured more slowly than
    /// one (ConcurrentMeasureBenchmarks, performance plan F11). A table is built once, from Skia's own per-glyph
    /// advances, and is immutable after that, so any number of threads read it without a lock.
    ///
    /// Skia's <c>SkFont::measureText</c> adds each glyph's <c>advanceX</c> to a float starting at zero, in text
    /// order, and multiplies by the strike's scale only when that is not 1. <c>getWidths</c> returns the same
    /// advances, multiplied by the same scale per glyph. So for a strike that is not rescaled the sum below is
    /// the same float, bit for bit (AdvanceTableParityTests). Skia rescales strikes larger than 256 pixels, so
    /// those fonts get no table and keep calling Skia.
    /// </remarks>
    internal sealed class AdvanceTable
    {
        private const int Characters = 256;

        // SkStrikeCommon::kSkSideTooBigForAtlas: a larger size is measured through a canonical strike and rescaled,
        // which a sum of the rescaled per-glyph widths would not match exactly.
        private const float LargestUnscaledSize = 256f;

        // Tables are shared by every Font with the same face and size: a report engine may make a new Font for
        // every cell, and building a table costs two Skia calls. Keyed by the typeface object, not its handle, so a
        // freed typeface's address reused by a new one cannot pick up the old table. Bounded crudely: past the
        // limit the whole map is dropped and refilled, which is rare and only costs rebuilding tables.
        private const int SharedLimit = 1024;
        private static readonly ConcurrentDictionary<(SKTypeface, float, SKFontEdging, bool, SKFontHinting, bool, float, float, bool), AdvanceTable> shared = new ();

        private readonly float[] advances;

        private AdvanceTable (float[] advances) => this.advances = advances;

        /// <summary>
        /// The table for <paramref name="font"/>, whose typeface is <paramref name="typeface"/> (passed in because
        /// reading <c>SKFont.Typeface</c> takes SkiaSharp's handle-table lock), or null when the font is too large
        /// to measure this way.
        /// </summary>
        internal static AdvanceTable? For (SKFont font, SKTypeface? typeface)
        {
            if (typeface is null)
                return null;

            var size = font.Size;
            if (!(size > 0f) || size > LargestUnscaledSize)
                return null;

            // Everything that changes a glyph's advance in Skia's scaler context.
            var key = (typeface, size, font.Edging, font.Subpixel, font.Hinting, font.Embolden, font.ScaleX, font.SkewX, font.LinearMetrics);
            if (shared.TryGetValue (key, out var table))
                return table;

            table = Build (font);
            if (shared.Count >= SharedLimit)
                shared.Clear ();
            return shared.GetOrAdd (key, table);
        }

        private static AdvanceTable Build (SKFont font)
        {
            var characters = new char[Characters];
            for (var i = 0; i < Characters; i++)
                characters[i] = (char)i;

            var glyphs = font.GetGlyphs (new ReadOnlySpan<char> (characters));
            return new AdvanceTable (font.GetGlyphWidths (glyphs));
        }

        /// <summary>
        /// The width <c>font.MeasureText (text)</c> returns, for the font this table was built from.
        /// </summary>
        internal float Measure (SKFont font, ReadOnlySpan<char> text)
        {
            var width = 0f;
            var table = advances;

            foreach (var c in text) {
                // Anything past U+00FF, surrogates included, is left to Skia: the whole run, so the sum stays in
                // Skia's order.
                if (c >= Characters)
                    return font.MeasureText (text);
                width += table[c];
            }

            return width;
        }
    }

    /// <summary>
    /// An <c>SKFont</c> together with its <see cref="AdvanceTable"/>, if it has one: what the unshaped measuring
    /// paths (<c>MeasureCharacterRanges</c>, <c>MeasureRunWidth</c>, word wrap) measure with.
    /// </summary>
    internal readonly struct RunFont
    {
        internal RunFont (SKFont font, AdvanceTable? advances)
        {
            Font = font;
            Advances = advances;
        }

        internal SKFont Font { get; }

        internal AdvanceTable? Advances { get; }

        /// <summary>The same float as <c>Font.MeasureText (text)</c>.</summary>
        internal float Measure (ReadOnlySpan<char> text)
            => Advances is { } table ? table.Measure (Font, text) : Font.MeasureText (text);

        /// <summary>For tests and one-off callers: the font with its shared table looked up.</summary>
        public static implicit operator RunFont (SKFont font) => new (font, AdvanceTable.For (font, font.Typeface));
    }
}
