using System.Drawing;
using SkiaSharp;

#pragma warning disable CA1711  // WinForms compat: TextFormatFlags is the canonical WinForms name

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: provides static methods for measuring and rendering text using GDI.
    /// In Majorsilence.Forms, text measurement delegates to <see cref="TextMeasurer"/>.
    /// </summary>
    public static class TextRenderer
    {
        /// <summary>
        /// Measures the size of the specified text when drawn with the specified font.
        /// </summary>
        public static Size MeasureText (string text, SKTypeface font, int fontSize = -1)
        {
            var size = fontSize <= 0
                ? TextMeasurer.MeasureText (text, font, Theme.FontSize)
                : TextMeasurer.MeasureText (text, font, fontSize);

            return new Size ((int)Math.Ceiling (size.Width), (int)Math.Ceiling (size.Height));
        }

        /// <summary>
        /// Measures the size of the specified text constrained to the specified bounds.
        /// </summary>
        public static Size MeasureText (string text, SKTypeface font, Size proposedSize, int fontSize = -1)
        {
            var size = fontSize <= 0
                ? TextMeasurer.MeasureText (text, font, Theme.FontSize, proposedSize)
                : TextMeasurer.MeasureText (text, font, fontSize, proposedSize);

            return new Size ((int)Math.Ceiling (size.Width), (int)Math.Ceiling (size.Height));
        }

        /// <summary>
        /// Measures the size of the specified text using the control's current font settings.
        /// </summary>
        public static Size MeasureText (string text, Control control)
        {
            var size = TextMeasurer.MeasureText (text, control);
            return new Size ((int)Math.Ceiling (size.Width), (int)Math.Ceiling (size.Height));
        }

        /// <summary>
        /// Draws the specified text on the canvas at the given location. Stub in Majorsilence.Forms.
        /// </summary>
        public static void DrawText (SKCanvas canvas, string text, SKTypeface font, Rectangle bounds, SKColor foreColor) { }

        /// <summary>
        /// Draws the specified text on the canvas at the given location. Stub in Majorsilence.Forms.
        /// </summary>
        public static void DrawText (PaintEventArgs e, string text, SKTypeface font, Rectangle bounds, SKColor foreColor)
        {
            e.Canvas.DrawText (text, font, Theme.FontSize, bounds, foreColor, ContentAlignment.MiddleLeft);
        }

#pragma warning disable CA1416
        /// <summary>Measures text using a Majorsilence.Forms.Drawing.Font. Delegates to SKTypeface approximation.</summary>
        public static Size MeasureText (string text, Majorsilence.Forms.Drawing.Font font)
        {
            var tf = SKTypeface.FromFamilyName (font.Name) ?? Theme.UIFont;
            return MeasureText (text, tf, (int)font.SizeInPoints);
        }

        /// <summary>Measures text using a Majorsilence.Forms.Drawing.Font with size constraints.</summary>
        public static Size MeasureText (string text, Majorsilence.Forms.Drawing.Font font, Size proposedSize)
        {
            var tf = SKTypeface.FromFamilyName (font.Name) ?? Theme.UIFont;
            return MeasureText (text, tf, proposedSize, (int)font.SizeInPoints);
        }

        /// <summary>Draws text at the given point using a Majorsilence.Forms.Drawing.Font. Stub in Majorsilence.Forms.</summary>
        public static void DrawText (Graphics g, string text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor)
        {
            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (foreColor);
            g.DrawString (text, font, brush, new RectangleF (bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }

        /// <summary>Draws text at the given point using a Majorsilence.Forms.Drawing.Font with flags. Stub in Majorsilence.Forms.</summary>
        public static void DrawText (Graphics g, string text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (g, text, font, bounds, foreColor);

        /// <summary>Draws text at the specified point using a Majorsilence.Forms.Drawing.Font.</summary>
        public static void DrawText (Graphics g, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor)
            => DrawText (g, text, font, new Rectangle (pt.X, pt.Y, 0, 0), foreColor);

        /// <summary>Draws text at the specified point with flags using a Majorsilence.Forms.Drawing.Font.</summary>
        public static void DrawText (Graphics g, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (g, text, font, new Rectangle (pt.X, pt.Y, 0, 0), foreColor);
#pragma warning restore CA1416
    }

    /// <summary>Specifies how text is formatted and aligned.</summary>
    [System.Flags]
    public enum TextFormatFlags
    {
        /// <summary>Text is left-aligned.</summary>
        Left = 0,
        /// <summary>Text is centered horizontally.</summary>
        HorizontalCenter = 0x0001,
        /// <summary>Text is right-aligned.</summary>
        Right = 0x0002,
        /// <summary>Text is top-aligned.</summary>
        Top = 0,
        /// <summary>Text is centered vertically.</summary>
        VerticalCenter = 0x0004,
        /// <summary>Text is bottom-aligned.</summary>
        Bottom = 0x0008,
        /// <summary>Words are wrapped.</summary>
        WordBreak = 0x0010,
        /// <summary>Text is trimmed with ellipsis.</summary>
        EndEllipsis = 0x0020,
        /// <summary>Modify string to word-break to match ellipsis.</summary>
        WordEllipsis = 0x0040,
        /// <summary>Do not clip.</summary>
        NoClipping = 0x0100,
        /// <summary>Single line only.</summary>
        SingleLine = 0x0200,
        /// <summary>Prefix characters are not underlined.</summary>
        NoPrefix = 0x0800,
        /// <summary>Remove mnemonic prefix character.</summary>
        HidePrefix = 0x00100000,
        /// <summary>Glyphs are not passed through the font mapper.</summary>
        NoPadding = 0x10000000,
        /// <summary>GlyphOverhangPadding is removed from the left and right.</summary>
        LeftAndRightPadding = 0x20000000,
        /// <summary>Preserve internal leading.</summary>
        PreserveGraphicsTranslateTransform = 0x00000400,
        /// <summary>Preserve clipping.</summary>
        PreserveGraphicsClipping = 0x01000000
    }
}
