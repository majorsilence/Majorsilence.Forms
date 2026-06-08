using System;
using SkiaSharp;

namespace Modern.Forms.Drawing
{
    /// <summary>
    /// A lightweight, cross-platform font description backed by SkiaSharp (SKFont). Provided as a
    /// cross-platform alternative to System.Drawing.Font (which is Windows-only) for code that needs
    /// to draw text onto a <see cref="SkiaGraphics"/> surface.
    /// </summary>
    public sealed class Font : IDisposable
    {
        private SKTypeface? typeface;
        private SKFont? font;

        /// <summary>Initializes a new instance of the Font class.</summary>
        public Font (string familyName, float size, bool bold = false, bool italic = false)
        {
            FamilyName = string.IsNullOrWhiteSpace (familyName) ? "Arial" : familyName;
            Size = size <= 0 ? 1 : size;
            Bold = bold;
            Italic = italic;
        }

        /// <summary>Gets the font family name.</summary>
        public string FamilyName { get; }

        /// <summary>Gets the em size of the font in the surface's current units.</summary>
        public float Size { get; }

        /// <summary>Gets whether the font is bold.</summary>
        public bool Bold { get; }

        /// <summary>Gets whether the font is italic.</summary>
        public bool Italic { get; }

        // Lazily resolves and caches the SkiaSharp font.
        internal SKFont GetSKFont ()
        {
            if (font is not null)
                return font;

            var style = new SKFontStyle (
                Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            typeface = SKTypeface.FromFamilyName (FamilyName, style) ?? SKTypeface.Default;
            font = new SKFont (typeface, Size) {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true
            };

            return font;
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            font?.Dispose ();
            typeface?.Dispose ();
            font = null;
            typeface = null;
        }
    }
}
