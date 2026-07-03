using SkiaSharp;


namespace Majorsilence.Forms.Drawing
{
    public class FontFamily
    {
        public static FontFamily GenericMonospace { get; } = new FontFamily("monospace");
        public static FontFamily GenericSansSerif { get; } = new FontFamily("sans-serif");
        public static FontFamily GenericSerif { get; } = new FontFamily("serif");

        public string Name { get; }

        public FontFamily(string name)
        {
            Name = name;
        }

        // Returns cell descent in design units at a nominal em height of 1000.
        public float GetCellDescent(FontStyle fs)
        {
            using var typeface = FontSubstitution.Resolve(Name, GetSkFontStyle(fs));
            using var font = new SKFont(typeface, 1000f);
            return Math.Abs(font.Metrics.Descent);
        }

        // Returns cell ascent in design units at a nominal em height of 1000.
        public float GetCellAscent(FontStyle fs)
        {
            using var typeface = FontSubstitution.Resolve(Name, GetSkFontStyle(fs));
            using var font = new SKFont(typeface, 1000f);
            return Math.Abs(font.Metrics.Ascent);
        }

        // Returns the em height in design units. We use 1000 as the nominal size.
        public float GetEmHeight(FontStyle fs)
        {
            return 1000f;
        }

        // Returns the line spacing in design units.
        public float GetLineSpacing(FontStyle fs)
        {
            using var typeface = FontSubstitution.Resolve(Name, GetSkFontStyle(fs));
            using var font = new SKFont(typeface, 1000f);
            var m = font.Metrics;
            return Math.Abs(m.Ascent) + Math.Abs(m.Descent) + Math.Abs(m.Leading);
        }

        public override string ToString() => Name;

        private static SKFontStyle GetSkFontStyle(FontStyle fs)
        {
            bool bold = (fs & FontStyle.Bold) != 0;
            bool italic = (fs & FontStyle.Italic) != 0;
            if (bold && italic) return SKFontStyle.BoldItalic;
            if (bold) return SKFontStyle.Bold;
            if (italic) return SKFontStyle.Italic;
            return SKFontStyle.Normal;
        }
    }
}
