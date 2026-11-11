using System.Collections.Concurrent;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    // RichTextKit's font fallback asks the platform font manager about every code point the run's typeface has no
    // glyph for, every time a block is laid out, and keeps no answer. A line end is such a code point in most fonts,
    // so laying out a document asked fontconfig once per line: four fifths of laying out a long TextBox's text (#683),
    // and a native call for every LF in every multi-line label. The answer for a family, style and code point does not
    // change while the process runs, so it is kept here, the same way CachingFontMapper keeps the family lookup.
    internal sealed class CachingCharacterMatcher : ICharacterMatcher
    {
        private readonly record struct Key (string Family, int Weight, int Width, SKFontStyleSlant Slant, int Character);

        // Text in a script the UI font lacks asks about each of its characters; past this many entries the cache is
        // dropped and refilled rather than growing without bound.
        private const int MaxEntries = 16384;

        private readonly ConcurrentDictionary<Key, SKTypeface?> cache = new ();
        private readonly ICharacterMatcher inner;

        internal CachingCharacterMatcher (ICharacterMatcher inner) => this.inner = inner;

        public SKTypeface? MatchCharacter (string familyName, int weight, int width, SKFontStyleSlant slant, string[]? bcp47, int character)
        {
            // Language-tagged queries are rare (FontFallback passes none) and their answer depends on the tags.
            if (bcp47 is { Length: > 0 })
                return inner.MatchCharacter (familyName, weight, width, slant, bcp47, character);

            var key = new Key (familyName ?? string.Empty, weight, width, slant, character);

            if (cache.TryGetValue (key, out var typeface))
                return typeface;

            typeface = inner.MatchCharacter (familyName!, weight, width, slant, bcp47!, character);

            if (cache.Count >= MaxEntries)
                cache.Clear ();

            return cache.GetOrAdd (key, typeface);
        }

        // Wraps whatever matcher is installed, RichTextKit's own unless an app replaced it. Idempotent.
        internal static void Install ()
        {
            if (FontFallback.CharacterMatcher is not CachingCharacterMatcher)
                FontFallback.CharacterMatcher = new CachingCharacterMatcher (FontFallback.CharacterMatcher);
        }
    }
}
