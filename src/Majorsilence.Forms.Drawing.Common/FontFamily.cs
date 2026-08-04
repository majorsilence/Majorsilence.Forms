using System;
using System.Linq;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Defines a group of typefaces having a similar basic design. Cross-platform replacement for
    /// <c>System.Drawing.FontFamily</c>.
    /// </summary>
    public sealed class FontFamily : IDisposable
    {
        /// <summary>Initializes a new instance of the FontFamily class with the specified name.</summary>
        public FontFamily (string name)
        {
            Name = string.IsNullOrWhiteSpace (name) ? "Arial" : name;
        }

        /// <summary>Gets the name of this font family.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets the name of this font family in the specified language. Majorsilence.Forms doesn't
        /// track per-language localized family names (no OS font-metadata query), so this always
        /// returns the same Name regardless of the language argument.
        /// </summary>
        public string GetName (int language) => Name;

        /// <summary>Gets a generic sans-serif font family.</summary>
        public static FontFamily GenericSansSerif { get; } = new FontFamily ("Arial");

        /// <summary>Gets a generic serif font family.</summary>
        public static FontFamily GenericSerif { get; } = new FontFamily ("Times New Roman");

        /// <summary>Gets a generic monospace font family.</summary>
        public static FontFamily GenericMonospace { get; } = new FontFamily ("Courier New");

        /// <summary>
        /// Gets an array of FontFamily objects representing the fonts actually installed on this
        /// system (via SkiaSharp's font manager), matching System.Drawing.FontFamily.Families.
        /// </summary>
        public static FontFamily[] Families =>
            SkiaSharp.SKFontManager.Default.FontFamilies.Select (name => new FontFamily (name)).ToArray ();

        /// <summary>
        /// Returns whether the specified style is available for this family, by resolving the family at
        /// that style and checking whether the typeface actually came back with it. A family with no
        /// real bold (or italic) face resolves to its regular face, which is reported as unavailable
        /// rather than as a synthesized match.
        /// </summary>
        /// <remarks>
        /// <see cref="FontStyle.Underline"/> and <see cref="FontStyle.Strikeout"/> are drawn decorations
        /// rather than typeface styles, so they are always available and do not affect the result --
        /// the same way GDI+ treats them.
        /// </remarks>
        public bool IsStyleAvailable (FontStyle style)
        {
            var wantBold = (style & FontStyle.Bold) != 0;
            var wantItalic = (style & FontStyle.Italic) != 0;

            var requested = new SkiaSharp.SKFontStyle (
                wantBold ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal,
                SkiaSharp.SKFontStyleWidth.Normal,
                wantItalic ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright);

            // Deliberately not disposed: FontSubstitution keeps the embedded typefaces in a static
            // cache and hands the shared instance back, so disposing here would break later lookups.
            var typeface = FontSubstitution.Resolve (Name, requested);
            return typeface.IsBold == wantBold && typeface.IsItalic == wantItalic;
        }

        /// <summary>
        /// Returns the font families installed on this system. Same set as <see cref="Families"/>;
        /// the parameter is accepted for System.Drawing compatibility and ignored, since the family
        /// list here does not vary per drawing surface.
        /// </summary>
        public static FontFamily[] GetFamilies (object? graphics) => Families;

        // Font metrics are reported in GDI+'s design-units-per-em space, so they are independent of any
        // particular font size -- callers scale by (size / EmHeight). Skia reports metrics normalized to
        // a 1.0 em, so measuring at exactly DesignUnitsPerEm converts in one step.
        private const int DesignUnitsPerEm = 2048;

        private SkiaSharp.SKFontMetrics MetricsFor (FontStyle style)
        {
            var typeface = FontSubstitution.Resolve (Name, new SkiaSharp.SKFontStyle (
                (style & FontStyle.Bold) != 0 ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal,
                SkiaSharp.SKFontStyleWidth.Normal,
                (style & FontStyle.Italic) != 0 ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright));

            using var font = new SkiaSharp.SKFont (typeface, DesignUnitsPerEm);
            return font.Metrics;
        }

        /// <summary>Returns the em height, in design units, of this family in the specified style.</summary>
        /// <remarks>
        /// These four metrics are computed from the real typeface rather than guessed: a wrong value
        /// here propagates silently into text layout, which is exactly the failure the GDI+ gap plan
        /// (docs/gdi-gap-plan.md) treats as worse than a missing member.
        /// </remarks>
        public int GetEmHeight (FontStyle style) => DesignUnitsPerEm;

        /// <summary>Returns the ascent, in design units, of this family in the specified style.</summary>
        public int GetCellAscent (FontStyle style) => (int)Math.Round (-MetricsFor (style).Ascent);

        /// <summary>Returns the descent, in design units, of this family in the specified style.</summary>
        public int GetCellDescent (FontStyle style) => (int)Math.Round (MetricsFor (style).Descent);

        /// <summary>Returns the line spacing, in design units, of this family in the specified style.</summary>
        public int GetLineSpacing (FontStyle style)
        {
            var metrics = MetricsFor (style);
            return (int)Math.Round (-metrics.Ascent + metrics.Descent + metrics.Leading);
        }

        /// <inheritdoc/>
        public override string ToString () => $"[FontFamily: Name={Name}]";

        /// <inheritdoc/>
        public void Dispose () { }
    }

    /// <summary>Specifies style information applied to text. Matches System.Drawing.FontStyle.</summary>
    [Flags]
    public enum FontStyle
    {
        /// <summary>Normal text.</summary>
        Regular = 0,
        /// <summary>Bold text.</summary>
        Bold = 1,
        /// <summary>Italic text.</summary>
        Italic = 2,
        /// <summary>Underlined text.</summary>
        Underline = 4,
        /// <summary>Text with a line through the middle.</summary>
        Strikeout = 8
    }

    /// <summary>Specifies how an image is rotated or flipped. Matches System.Drawing.RotateFlipType.</summary>
    public enum RotateFlipType
    {
        /// <summary>No rotation and no flipping.</summary>
        RotateNoneFlipNone = 0,
        /// <summary>90-degree rotation without flipping.</summary>
        Rotate90FlipNone = 1,
        /// <summary>180-degree rotation without flipping.</summary>
        Rotate180FlipNone = 2,
        /// <summary>270-degree rotation without flipping.</summary>
        Rotate270FlipNone = 3,
        /// <summary>No rotation and a horizontal flip.</summary>
        RotateNoneFlipX = 4,
        /// <summary>90-degree rotation followed by a horizontal flip.</summary>
        Rotate90FlipX = 5,
        /// <summary>180-degree rotation followed by a horizontal flip.</summary>
        Rotate180FlipX = 6,
        /// <summary>270-degree rotation followed by a horizontal flip.</summary>
        Rotate270FlipX = 7,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Rotate none flip y.</summary>
        RotateNoneFlipY = 6,
        /// <summary>Rotate 90 flip y.</summary>
        Rotate90FlipY = 7,
        /// <summary>Rotate 180 flip y.</summary>
        Rotate180FlipY = 4,
        /// <summary>Rotate 270 flip y.</summary>
        Rotate270FlipY = 5,
        /// <summary>Rotate none flip xy.</summary>
        RotateNoneFlipXY = 2,
        /// <summary>Rotate 90 flip xy.</summary>
        Rotate90FlipXY = 3,
        /// <summary>Rotate 180 flip xy.</summary>
        Rotate180FlipXY = 0,
        /// <summary>Rotate 270 flip xy.</summary>
        Rotate270FlipXY = 1,
    }

    /// <summary>Specifies the unit of measure for drawing operations. Matches System.Drawing.GraphicsUnit.</summary>
    public enum GraphicsUnit
    {
        /// <summary>The world coordinate system unit.</summary>
        World = 0,
        /// <summary>The unit of measure of the display device.</summary>
        Display = 1,
        /// <summary>A device pixel.</summary>
        Pixel = 2,
        /// <summary>A printer's point (1/72 inch).</summary>
        Point = 3,
        /// <summary>An inch.</summary>
        Inch = 4,
        /// <summary>1/300 inch.</summary>
        Document = 5,
        /// <summary>A millimeter.</summary>
        Millimeter = 6
    }
}
