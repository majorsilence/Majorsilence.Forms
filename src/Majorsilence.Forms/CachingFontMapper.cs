// Stays in Majorsilence.Forms rather than moving to Majorsilence.Forms.Drawing.Common with the rest of
// the font/drawing types. Unlike the four files under Drawing/, nothing here forms a circular reference
// -- this could move. It shouldn't: it installs a process-wide default for Topten.RichTextKit, the text
// *layout* engine, and every RichTextKit consumer (TextMeasurer, TextBoxDocument, TextBox,
// TextBoxRenderer, SkiaTextExtensions, Theme) lives in this assembly. Majorsilence.Forms.Drawing.Common
// contains no RichTextKit code at all -- its text path is SkiaSharp SKFont-based -- so moving this one
// internal class, whose sole caller is Theme's static constructor below, would force a Topten.RichTextKit
// package dependency onto the standalone drawing package for something none of its consumers can use.
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
            // A family loaded at runtime through PrivateFontCollection is unknown to the system font
            // manager, so the inner mapper cannot find it and silently substitutes a default face.
            // Because a TextBlock carries only the family *name*, that substitution is what the whole
            // layout is measured from -- text drawn with the private font would be measured with a
            // different one, and laid out to the wrong width.
            //
            // Deliberately ahead of, and excluded from, the cache: the registry is an in-memory
            // dictionary lookup with no font-manager query to amortise, and caching it would both pin
            // a typeface the collection owns and disposes, and hide a family registered after the
            // name's first use. The IsEmpty check keeps the (overwhelmingly common) no-private-fonts
            // path down to one uncontended lock.
            if (!Drawing.Text.PrivateFontRegistry.IsEmpty) {
                var privateFace = Drawing.Text.PrivateFontRegistry.Resolve (
                    style.FontFamily ?? string.Empty,
                    new SKFontStyle (
                        (SKFontStyleWeight)style.FontWeight,
                        style.FontWidth,
                        style.FontItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright));

                if (privateFace is not null)
                    return privateFace;
            }

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
