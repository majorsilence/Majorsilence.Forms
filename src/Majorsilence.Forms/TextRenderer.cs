using System;
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
        /// <summary>Measures text drawn with the given font. Mirrors the WinForms overload that takes
        /// a device context; measurement here is device-independent so the Graphics is not consulted.</summary>
        public static Size MeasureText (Majorsilence.Forms.Graphics g, string text, Majorsilence.Forms.Drawing.Font font)
            => MeasureText (text, font);

        /// <summary>Measures text drawn with the given font.</summary>
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

        // TextRenderer's whole public surface is declared in terms of IDeviceContext upstream, not
        // Graphics -- so these take the interface. A caller passing a Graphics is unaffected (it
        // implements the interface), and a caller who declared a helper taking an IDeviceContext now
        // compiles too, which was the point.
        //
        // The backColor overloads fill the text's box before drawing, which is what WinForms does and
        // what makes them worth having rather than silently ignoring the argument.

        /// <summary>Draws text inside the given rectangle.</summary>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor)
            => DrawText (dc, text, font, bounds, foreColor, System.Drawing.Color.Empty, TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (dc, text, font, bounds, foreColor, System.Drawing.Color.Empty, flags);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => DrawText (dc, text, font, bounds, foreColor, backColor, TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, System.Drawing.Color backColor, TextFormatFlags flags)
        {
            if (dc is not Graphics g || font is null)
                return;

            if (!backColor.IsEmpty && backColor != System.Drawing.Color.Transparent) {
                using var background = new Majorsilence.Forms.Drawing.SolidBrush (backColor);
                g.FillRectangle (background, bounds);
            }

            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (foreColor);
            g.DrawString (text, font, brush, new RectangleF (bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }

        /// <summary>Draws text with its top-left corner at the given point.</summary>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, System.Drawing.Color.Empty, TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, System.Drawing.Color.Empty, flags);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, backColor, TextFormatFlags.Left);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, System.Drawing.Color backColor, TextFormatFlags flags)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, backColor, flags);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor)
            => DrawText (dc, text.ToString (), font, bounds, foreColor);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (dc, text.ToString (), font, bounds, foreColor, flags);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => DrawText (dc, text.ToString (), font, bounds, foreColor, backColor);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,Rectangle,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Rectangle bounds, System.Drawing.Color foreColor, System.Drawing.Color backColor, TextFormatFlags flags)
            => DrawText (dc, text.ToString (), font, bounds, foreColor, backColor, flags);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor)
            => DrawText (dc, text.ToString (), font, pt, foreColor);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (dc, text.ToString (), font, pt, foreColor, flags);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => DrawText (dc, text.ToString (), font, pt, foreColor, backColor);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, System.Drawing.Color backColor, TextFormatFlags flags)
            => DrawText (dc, text.ToString (), font, pt, foreColor, backColor, flags);

        /// <summary>Measures text drawn on the given surface.</summary>
        public static Size MeasureText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font)
            => MeasureText (text, font);

        /// <inheritdoc cref="MeasureText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, Size proposedSize)
            => MeasureText (text, font, proposedSize);

        /// <inheritdoc cref="MeasureText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, Size proposedSize, TextFormatFlags flags)
            => MeasureText (text, font, proposedSize, flags);

        /// <inheritdoc cref="MeasureText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font)
            => MeasureText (text.ToString (), font);

        /// <inheritdoc cref="MeasureText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Size proposedSize)
            => MeasureText (text.ToString (), font, proposedSize);

        /// <inheritdoc cref="MeasureText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (Majorsilence.Forms.Drawing.IDeviceContext dc, ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Size proposedSize, TextFormatFlags flags)
            => MeasureText (text.ToString (), font, proposedSize, flags);

        /// <inheritdoc cref="MeasureText(string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font)
            => MeasureText (text.ToString (), font);

        /// <inheritdoc cref="MeasureText(string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Size proposedSize)
            => MeasureText (text.ToString (), font, proposedSize);

        /// <inheritdoc cref="MeasureText(string,Majorsilence.Forms.Drawing.Font)"/>
        public static Size MeasureText (ReadOnlySpan<char> text, Majorsilence.Forms.Drawing.Font font, Size proposedSize, TextFormatFlags flags)
            => MeasureText (text.ToString (), font, proposedSize, flags);

        /// <summary>Measures text within the given bounds, honouring the single-line and wrapping flags.</summary>
        public static Size MeasureText (string text, Majorsilence.Forms.Drawing.Font font, Size proposedSize, TextFormatFlags flags)
        {
            // SingleLine means "do not wrap", so the proposed width must not constrain the answer.
            var constraint = flags.HasFlag (TextFormatFlags.SingleLine)
                ? new Size (int.MaxValue, proposedSize.Height)
                : proposedSize;

            return MeasureText (text, font, constraint);
        }

        // WinForms' point-based DrawText measures the text and draws it in that box, rather than
        // passing a zero-sized rectangle through to the renderer -- which is what the previous
        // implementation did, so nothing was drawn.
        private static Rectangle AtPoint (System.Drawing.Point pt, string text, Majorsilence.Forms.Drawing.Font font)
        {
            var size = MeasureText (text, font);
            return new Rectangle (pt.X, pt.Y, size.Width, size.Height);
        }

#pragma warning restore CA1416
    }

    /// <summary>Specifies how text is formatted and aligned.</summary>
    [System.Flags]
    public enum TextFormatFlags
    {
        /// <summary>Text is left-aligned.</summary>
        Left = 0,
        /// <summary>Text is centered horizontally.</summary>
        HorizontalCenter = 1,
        /// <summary>Text is right-aligned.</summary>
        Right = 2,
        /// <summary>Text is top-aligned.</summary>
        Top = 0,
        /// <summary>Text is centered vertically.</summary>
        VerticalCenter = 4,
        /// <summary>Text is bottom-aligned.</summary>
        Bottom = 8,
        /// <summary>Words are wrapped.</summary>
        WordBreak = 16,
        /// <summary>Text is trimmed with ellipsis.</summary>
        EndEllipsis = 32768,
        /// <summary>Modify string to word-break to match ellipsis.</summary>
        WordEllipsis = 262144,
        /// <summary>Do not clip.</summary>
        NoClipping = 256,
        /// <summary>Single line only.</summary>
        SingleLine = 32,
        /// <summary>Prefix characters are not underlined.</summary>
        NoPrefix = 2048,
        /// <summary>Remove mnemonic prefix character.</summary>
        HidePrefix = 1048576,
        /// <summary>Glyphs are not passed through the font mapper.</summary>
        NoPadding = 268435456,
        /// <summary>GlyphOverhangPadding is removed from the left and right.</summary>
        LeftAndRightPadding = 536870912,
        /// <summary>Preserve internal leading.</summary>
        PreserveGraphicsTranslateTransform = 33554432,
        /// <summary>Preserve clipping.</summary>
        PreserveGraphicsClipping = 16777216,
    }
}
