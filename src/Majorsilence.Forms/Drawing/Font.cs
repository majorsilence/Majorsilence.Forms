using System;
using SkiaSharp;

namespace Majorsilence.Drawing
{
    /// <summary>
    /// A lightweight, cross-platform font description backed by SkiaSharp (SKFont). Cross-platform
    /// replacement for <c>System.Drawing.Font</c> (which is Windows-only).
    /// </summary>
    public sealed class Font : IDisposable, ICloneable
    {
        private SKTypeface? typeface;
        private SKFont? font;

        /// <summary>Initializes a new instance of the Font class.</summary>
        public Font (string familyName, float size, bool bold = false, bool italic = false)
        {
            FamilyName = string.IsNullOrWhiteSpace (familyName) ? "Arial" : familyName;
            Size = size <= 0 ? 1 : size;
            Style = (bold ? FontStyle.Bold : 0) | (italic ? FontStyle.Italic : 0);
            Unit = GraphicsUnit.Point;
        }

        /// <summary>Initializes a new instance of the Font class with the specified style.</summary>
        public Font (string familyName, float size, FontStyle style, GraphicsUnit unit = GraphicsUnit.Point)
        {
            FamilyName = string.IsNullOrWhiteSpace (familyName) ? "Arial" : familyName;
            Size = size <= 0 ? 1 : size;
            Style = style;
            Unit = unit;
        }

        /// <summary>Initializes a new instance of the Font class with the specified style and GDI charset.</summary>
        public Font (string familyName, float size, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
            : this (familyName, size, style, unit)
        {
            GdiCharSet = gdiCharSet;
        }

        /// <summary>Initializes a new instance of the Font class from a font family.</summary>
        public Font (FontFamily family, float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
            : this (family?.Name ?? "Arial", size, style, unit)
        {
        }

        /// <summary>Initializes a new instance of the Font class based on an existing font and a new style.</summary>
        public Font (Font prototype, FontStyle newStyle)
            : this (prototype?.FamilyName ?? "Arial", prototype?.Size ?? 9f, newStyle, prototype?.Unit ?? GraphicsUnit.Point)
        {
        }

        /// <summary>Gets the font family name.</summary>
        public string FamilyName { get; }

        /// <summary>Gets the font family name.</summary>
        public string Name => FamilyName;

        /// <summary>Gets the font family.</summary>
        public FontFamily FontFamily => new FontFamily (FamilyName);

        /// <summary>Gets the em size of the font in the unit specified by <see cref="Unit"/>.</summary>
        public float Size { get; }

        /// <summary>Gets the em size of the font, in points.</summary>
        public float SizeInPoints => Unit == GraphicsUnit.Point ? Size : Size * 72f / 96f;

        /// <summary>Gets the unit of measure for this font.</summary>
        public GraphicsUnit Unit { get; }

        /// <summary>Gets the style information for this font.</summary>
        public FontStyle Style { get; }

        /// <summary>Gets the GDI character set used by this font.</summary>
        public byte GdiCharSet { get; } = 1;

        /// <summary>Gets whether this font is bold.</summary>
        public bool Bold => (Style & FontStyle.Bold) == FontStyle.Bold;

        /// <summary>Gets whether this font is italic.</summary>
        public bool Italic => (Style & FontStyle.Italic) == FontStyle.Italic;

        /// <summary>Gets whether this font is underlined.</summary>
        public bool Underline => (Style & FontStyle.Underline) == FontStyle.Underline;

        /// <summary>Gets whether this font has a strikeout line.</summary>
        public bool Strikeout => (Style & FontStyle.Strikeout) == FontStyle.Strikeout;

        /// <summary>Gets the line spacing, in pixels, of this font.</summary>
        public int Height => (int)Math.Ceiling (GetHeight ());

        /// <summary>Gets the line spacing, in pixels, of this font.</summary>
        public float GetHeight ()
        {
            var metrics = GetSKFont ().Metrics;
            return metrics.Descent - metrics.Ascent + metrics.Leading;
        }

        /// <summary>Gets the line spacing, in the current unit, of this font for the given DPI.</summary>
        public float GetHeight (float dpi) => GetHeight ();

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

        /// <summary>Creates an exact copy of this font.</summary>
        public object Clone () => new Font (FamilyName, Size, Style, Unit, GdiCharSet);

        /// <inheritdoc/>
        public override string ToString () => $"[Font: Name={Name}, Size={Size}, Style={Style}, Unit={Unit}]";

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
