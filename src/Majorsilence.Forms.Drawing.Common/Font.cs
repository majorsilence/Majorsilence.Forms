using System;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// A lightweight, cross-platform font description backed by SkiaSharp (SKFont). Cross-platform
    /// replacement for <c>System.Drawing.Font</c> (which is Windows-only).
    /// </summary>
    /// <remarks>
    /// The TypeConverter is load-bearing beyond design time: settings storage asks the type system how to
    /// turn a value into a string, and with no converter it falls back to XML serialization -- which needs a
    /// parameterless constructor this type does not have, and cannot have (a font without a family or size
    /// is not a font). A settings class with a Font-typed default therefore threw on first read until the
    /// converter was findable. <see cref="FontConverter"/> already round-trips the designer's own
    /// "Segoe UI, 9pt, style=Bold" format; it just was not attached to the type.
    /// </remarks>
    [System.ComponentModel.TypeConverter (typeof (FontConverter))]
    public sealed partial class Font : IDisposable, ICloneable
    {
        private SKTypeface? typeface;
        private SKFont? font;
        // False when `typeface` came from PrivateFontCollection (which owns and disposes it).
        private bool ownsTypeface = true;

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

        /// <summary>Initializes a font from a family, size and unit, in the regular style.</summary>
        /// <remarks>The unit-without-style overloads GDI+ offers. Without them a three-argument call
        /// whose last argument is a GraphicsUnit binds to the bool-flags constructor instead and fails
        /// to compile.</remarks>
        public Font (FontFamily family, float size, GraphicsUnit unit)
            : this (family, size, FontStyle.Regular, unit) { }

        /// <inheritdoc cref="Font(FontFamily, float, GraphicsUnit)"/>
        public Font (string familyName, float size, GraphicsUnit unit)
            : this (familyName, size, FontStyle.Regular, unit) { }

        /// <summary>Initializes a new instance of the Font class based on an existing font and a new style.</summary>
        public Font (Font prototype, FontStyle newStyle)
            : this (prototype?.FamilyName ?? "Arial", prototype?.Size ?? 9f, newStyle, prototype?.Unit ?? GraphicsUnit.Point)
        {
        }

        /// <summary>Gets the font family name.</summary>
        public string FamilyName { get; }

        /// <summary>Gets the font family name.</summary>
        public string Name => FamilyName;

        /// <summary>
        /// Gets the face name originally requested, before any substitution. Identical to
        /// <see cref="Name"/> here: substitution happens when a typeface is resolved for drawing, and
        /// the requested family name is what this object keeps.
        /// </summary>
        public string OriginalFontName => FamilyName;

        /// <summary>
        /// Gets the name of the system font this was created from, or an empty string when it was not
        /// created from one. See <see cref="IsSystemFont"/>.
        /// </summary>
        public string SystemFontName { get; internal set; } = string.Empty;

        /// <summary>Gets whether this font was created from a member of <c>SystemFonts</c>.</summary>
        public bool IsSystemFont => !string.IsNullOrEmpty (SystemFontName);

        /// <summary>
        /// Gets whether this font is derived from a GDI vertical font. Always false: vertical GDI faces
        /// are a Windows text-stack concept with no counterpart in the Skia text path.
        /// </summary>
        public bool GdiVerticalFont => false;

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
        /// <summary>Gets the line spacing of this font on the specified surface.</summary>
        /// <remarks>
        /// The parameter is typed <c>object?</c> because GDI+ takes a <c>Graphics</c> here, and that type
        /// lives in Majorsilence.Forms, which depends on this assembly rather than the reverse. An
        /// <c>object?</c> parameter still binds a Graphics argument at the call site. It is unused: this
        /// layer is 96 DPI throughout, which is what the parameterless overload assumes.
        /// </remarks>
        public float GetHeight (object? graphics) => GetHeight ();

        /// <summary>Gets the line spacing of this font at the specified DPI.</summary>
        public float GetHeight (float dpi) => GetHeight ();

        // The WinForms-compatible GetHeight(SkiaGraphics) overload lives in Majorsilence.Forms as an
        // extension method (FontGraphicsExtensions in Drawing/SkiaGraphics.cs): SkiaGraphics depends on
        // ContentAlignment/TextMeasurer and therefore stays in the Forms assembly, which this one cannot
        // reference. The overload ignored its argument anyway, so an extension is behaviourally identical.

        // Lazily resolves and caches the SkiaSharp font.
        internal SKFont GetSKFont ()
        {
            if (font is not null)
                return font;

            var style = new SKFontStyle (
                Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            // A family registered through PrivateFontCollection wins over the system font manager:
            // the whole point of loading a font file at runtime is to use it by name without
            // installing it. The registry owns those typefaces, so we must not dispose them.
            var privateFace = Text.PrivateFontRegistry.Resolve (FamilyName, style);
            if (privateFace is not null) {
                typeface = privateFace;
                ownsTypeface = false;
            } else {
                typeface = SKTypeface.FromFamilyName (FamilyName, style) ?? SKTypeface.Default;
                ownsTypeface = true;
            }

            font = new SKFont (typeface, Size) {
                Edging = SKFontEdging.SubpixelAntialias,
                Subpixel = true
            };

            return font;
        }

        // Lazily resolves and caches the underlying SkiaSharp typeface (used by ControlStyle's
        // implicit conversion from DataGridViewCellStyle, which needs a bare SKTypeface).
        internal SKTypeface GetSKTypeface ()
        {
            GetSKFont ();
            return typeface!;
        }

        /// <summary>Creates an exact copy of this font.</summary>
        public object Clone () => new Font (FamilyName, Size, Style, Unit, GdiCharSet);

        /// <inheritdoc/>
        public override string ToString () => $"[Font: Name={Name}, Size={Size}, Style={Style}, Unit={Unit}]";

        /// <inheritdoc/>
        public void Dispose ()
        {
            font?.Dispose ();
            if (ownsTypeface)
                typeface?.Dispose ();
            font = null;
            typeface = null;
        }
    }
}
