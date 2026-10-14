using System;
using System.Collections.Concurrent;
using System.Threading;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Text
{
    /// <summary>
    /// The advance of every character in the Basic Multilingual Plane in one font at one size, so an unshaped run
    /// of such characters is measured by adding them up in managed code instead of calling <c>SKFont.MeasureText</c>.
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
    ///
    /// The table is kept in pages of 256 characters. U+0000 to U+00FF is built with the table; any other page the
    /// first time a run needs it, so Polish, Greek or Cyrillic text costs one more page, not a 64K-entry table
    /// (ConcurrentMeasureBenchmarks.MeasureRunWidthExtended, performance plan F13).
    /// </remarks>
    internal sealed class AdvanceTable
    {
        private const int PageSize = 256;
        private const int Pages = 256;   // the whole BMP

        // SkStrikeCommon::kSkSideTooBigForAtlas: a larger size is measured through a canonical strike and rescaled,
        // which a sum of the rescaled per-glyph widths would not match exactly.
        private const float LargestUnscaledSize = 256f;

        // Tables are shared by every Font with the same face and size: a report engine may make a new Font for
        // every cell, and building a table costs two Skia calls. Keyed by the typeface object, not its handle, so a
        // freed typeface's address reused by a new one cannot pick up the old table. Bounded crudely: past the
        // limit the whole map is dropped and refilled, which is rare and only costs rebuilding tables.
        private const int SharedLimit = 1024;
        private static readonly ConcurrentDictionary<(SKTypeface, float, SKFontEdging, bool, SKFontHinting, bool, float, float, bool, bool, bool, bool), AdvanceTable> shared = new ();

        // Indexed by the high byte of the character. Page 0 is always there; another is null until a run needs it, and
        // once published it never changes, so readers take no lock. Two threads needing a new page at once may both
        // build it; they build the same advances.
        private readonly float[]?[] pages = new float[]?[Pages];

        private AdvanceTable (float[] latin, SKFontMetrics metrics)
        {
            pages[0] = latin;
            Metrics = metrics;
        }

        /// <summary>
        /// The font's metrics. <c>SKFont.Metrics</c> goes through the same locked glyph cache as measuring: it was
        /// two thirds of a one-range <c>MeasureCharacterRanges</c> call (ConcurrentMeasureBenchmarks, F14).
        /// </summary>
        internal SKFontMetrics Metrics { get; }

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

            // Everything that goes into Skia's scaler context, which gives both the advances and the metrics.
            var key = (typeface, size, font.Edging, font.Subpixel, font.Hinting, font.Embolden, font.ScaleX, font.SkewX, font.LinearMetrics,
                font.BaselineSnap, font.ForceAutoHinting, font.EmbeddedBitmaps);
            if (shared.TryGetValue (key, out var table))
                return table;

            table = Build (font);
            if (shared.Count >= SharedLimit)
                shared.Clear ();
            return shared.GetOrAdd (key, table);
        }

        private static AdvanceTable Build (SKFont font) => new (BuildPage (font, 0), font.Metrics);

        // Any font with this table's key gives the same advances, so a page is built from whichever font asks first.
        private static float[] BuildPage (SKFont font, int page)
        {
            Span<char> characters = stackalloc char[PageSize];
            for (var i = 0; i < PageSize; i++)
                characters[i] = (char)(page * PageSize + i);

            var glyphs = font.GetGlyphs (characters);
            return font.GetGlyphWidths (glyphs);
        }

        private float[] Page (SKFont font, int page)
        {
            var built = BuildPage (font, page);
            return Interlocked.CompareExchange (ref pages[page], built, null) ?? built;
        }

        /// <summary>
        /// The width <c>font.MeasureText (text)</c> returns, for the font this table was built from.
        /// </summary>
        internal float Measure (SKFont font, ReadOnlySpan<char> text)
        {
            var width = 0f;
            var latin = pages[0]!;

            // Latin-1 in a loop of its own: with the paging in it as well, a 20-character cell took 49 ns, not 19
            // (TextMeasureBenchmarks.WidthViaMeasureRunWidth).
            for (var i = 0; i < text.Length; i++) {
                var c = text[i];
                if (c >= PageSize)
                    return MeasureFrom (font, text, i, width);
                width += latin[c];
            }

            return width;
        }

        // The rest of a run from the first character past U+00FF, carrying on the same sum in the same order.
        private float MeasureFrom (SKFont font, ReadOnlySpan<char> text, int start, float width)
        {
            for (var i = start; i < text.Length; i++) {
                var c = text[i];

                // A surrogate pair is one glyph that no page holds: the whole run is left to Skia, so the sum stays
                // in Skia's order.
                if (char.IsSurrogate (c))
                    return font.MeasureText (text);

                var page = c / PageSize;
                width += (Volatile.Read (ref pages[page]) ?? Page (font, page))[c % PageSize];
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

        /// <summary><c>Font.Metrics</c>, from the table when there is one.</summary>
        internal SKFontMetrics Metrics => Advances is { } table ? table.Metrics : Font.Metrics;

        /// <summary>The same float as <c>Font.MeasureText (text)</c>.</summary>
        internal float Measure (ReadOnlySpan<char> text)
            => Advances is { } table ? table.Measure (Font, text) : Font.MeasureText (text);

        /// <summary>For tests and one-off callers: the font with its shared table looked up.</summary>
        public static implicit operator RunFont (SKFont font) => new (font, AdvanceTable.For (font, font.Typeface));
    }
}
