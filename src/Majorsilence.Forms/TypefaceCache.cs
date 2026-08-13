using System.Collections.Concurrent;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Caches <see cref="SKTypeface"/> lookups by family name and style.
    /// </summary>
    /// <remarks>
    /// <see cref="SKTypeface.FromFamilyName(string)"/> is a font-manager query — fontconfig on Linux —
    /// costing milliseconds per call whether or not the family resolves. The paint path asks for a
    /// typeface once per string drawn and once per string measured, and the backend repaints on a
    /// 60 fps timer, so an uncached lookup is on its own enough to saturate a core on a window with
    /// a handful of custom-painted labels.
    ///
    /// Typefaces handed out here are shared for the process lifetime and must not be disposed by
    /// callers. Call sites that own their typeface (a metafile player building one per record, say)
    /// should keep calling SkiaSharp directly.
    /// </remarks>
    internal static class TypefaceCache
    {
        private static readonly ConcurrentDictionary<(string Family, int Weight, int Width, int Slant), SKTypeface?> cache = new ();

        /// <summary>
        /// Resolves the typeface a <see cref="Majorsilence.Forms.Drawing.Font"/> actually renders with.
        /// </summary>
        /// <remarks>
        /// Measuring APIs must go through this rather than looking the family name up directly. The
        /// paint path resolves via <c>Font.GetSKTypeface()</c> (see Control.Font and ControlStyle),
        /// which honours families loaded through <c>PrivateFontCollection</c> and carries the font's
        /// slant; a name-only cache lookup consults the system font manager alone, so it silently
        /// measures a *different* face than the one drawn for any privately-loaded or italic font.
        /// Text laid out from those metrics is clipped or mis-centred.
        /// </remarks>
        public static SKTypeface Resolve (Majorsilence.Forms.Drawing.Font? font)
            => font?.GetSKTypeface () ?? Theme.UIFont;

        /// <summary>Gets a typeface for the family at normal weight/width/slant.</summary>
        public static SKTypeface? Get (string? family)
            => Get (family, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        /// <summary>Gets a typeface for the family at the given weight, keeping normal width and slant.</summary>
        public static SKTypeface? Get (string? family, SKFontStyleWeight weight)
            => Get (family, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

        /// <summary>Gets a typeface for the family in the given style.</summary>
        public static SKTypeface? Get (string? family, SKFontStyle style)
            => Get (family, (SKFontStyleWeight)style.Weight, (SKFontStyleWidth)style.Width, style.Slant);

        /// <summary>Gets a typeface for the family in the given weight, width and slant.</summary>
        public static SKTypeface? Get (string? family, SKFontStyleWeight weight, SKFontStyleWidth width, SKFontStyleSlant slant)
            => cache.GetOrAdd (
                (family ?? string.Empty, (int)weight, (int)width, (int)slant),
                static key => SKTypeface.FromFamilyName (
                    key.Family.Length == 0 ? null : key.Family,
                    (SKFontStyleWeight)key.Weight,
                    (SKFontStyleWidth)key.Width,
                    (SKFontStyleSlant)key.Slant));
    }
}
