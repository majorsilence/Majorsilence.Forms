using System.Collections.Concurrent;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    // RichTextKit's built-in FontMapper.Default resolves an IStyle's FontFamily string to an
    // SKTypeface via SKFontManager.MatchFamily on *every* TextBlock layout pass, not just the
    // first time a given family/weight/width/slant combination is used. On Linux, that call goes
    // through fontconfig, which costs several milliseconds -- and since a TextBlock's actual
    // layout is computed lazily (the first time e.g. MeasuredWidth or Lines is read, not when
    // AddText is called), that cost lands on every distinct piece of text rendered anywhere in
    // the app: property grid rows, tree nodes, report data values, etc. TextMeasurer's own
    // TextBlock cache only helps when the exact same text is redrawn; it does nothing for the
    // (much larger, in a data-heavy app) set of text that's merely new. Caching the typeface
    // lookup itself -- independent of the text being laid out -- fixes that for every caller.
    internal sealed class CachingFontMapper : FontMapper
    {
        private readonly record struct Key (string Family, int Weight, SKFontStyleWidth Width, bool Italic, bool IgnoreFontVariants);

        private readonly ConcurrentDictionary<Key, SKTypeface> _cache = new ();
        private readonly FontMapper _inner = new ();

        public override SKTypeface TypefaceFromStyle (IStyle style, bool ignoreFontVariants)
        {
            var key = new Key (style.FontFamily ?? string.Empty, style.FontWeight, style.FontWidth, style.FontItalic, ignoreFontVariants);
            return _cache.GetOrAdd (key, static (_, ctx) => ctx.inner.TypefaceFromStyle (ctx.style, ctx.ignoreFontVariants), (inner: _inner, style, ignoreFontVariants));
        }

        // Installs this mapper as RichTextKit's process-wide default. Idempotent -- safe to call
        // more than once (e.g. from Theme's static constructor and a host's startup path).
        internal static void Install ()
        {
            if (FontMapper.Default is not CachingFontMapper)
                FontMapper.Default = new CachingFontMapper ();
        }
    }
}
