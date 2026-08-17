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
        public static Size MeasureText (Majorsilence.Forms.Drawing.Graphics g, string text, Majorsilence.Forms.Drawing.Font font)
            => MeasureText (text, font);

        /// <summary>Measures text drawn with the given font.</summary>
        public static Size MeasureText (string text, Majorsilence.Forms.Drawing.Font font)
        {
            var tf = TypefaceCache.Resolve (font);
            return MeasureText (text, tf, (int)font.SizeInPoints);
        }

        /// <summary>Measures text using a Majorsilence.Forms.Drawing.Font with size constraints.</summary>
        public static Size MeasureText (string text, Majorsilence.Forms.Drawing.Font font, Size proposedSize)
        {
            var tf = TypefaceCache.Resolve (font);
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

            if (string.IsNullOrEmpty (text))
                return;

            var display = DisplayText (text, flags, out var mnemonic);

            var box = new RectangleF (bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var origin = g.AlignTextInBounds (display, font, box, HorizontalFactor (flags), VerticalFactor (flags));
            var clip = flags.HasFlag (TextFormatFlags.NoClipping) ? (RectangleF?)null : box;

            using var brush = new Majorsilence.Forms.Drawing.SolidBrush (foreColor);

            // PrefixOnly draws the underline and nothing else -- it is how a control paints in the accelerator
            // cue after the fact, when the caption was already drawn without one.
            if (!flags.HasFlag (TextFormatFlags.PrefixOnly))
                g.DrawStringClipped (display, font, brush, origin, clip);

            if (mnemonic >= 0 && mnemonic < display.Length)
                g.DrawMnemonicUnderline (display, mnemonic, font, brush, origin, clip ?? box);
        }

        /// <summary>
        /// Applies the flags' hotkey-prefix handling, returning the text to draw and the index within it of
        /// the character to underline (-1 for none).
        /// </summary>
        /// <remarks>
        /// Processing prefixes is the DEFAULT, as in WinForms: it is <see cref="TextFormatFlags.NoPrefix"/>
        /// that turns it off, not a flag that turns it on. Krypton reaches this method for every piece of
        /// solid-coloured text it draws and never sets any prefix flag, so "&amp;Open in explorer" was
        /// rendering its ampersand literally on every button and check box in the suite. Callers with text
        /// that genuinely contains an ampersand have to pass NoPrefix -- again matching WinForms, where the
        /// same requirement applies.
        /// </remarks>
        private static string DisplayText (string text, TextFormatFlags flags, out int mnemonic)
        {
            mnemonic = -1;

            if (flags.HasFlag (TextFormatFlags.NoPrefix))
                return text;

            var display = Mnemonics.Parse (text, out mnemonic);

            if (flags.HasFlag (TextFormatFlags.HidePrefix))
                mnemonic = -1;   // Prefix removed, but no accelerator cue drawn.

            return display;
        }

        // TextFormatFlags.Left and .Top are both 0, so they are the absence of the other flags
        // rather than values to test for; HorizontalCenter/Right and VerticalCenter/Bottom are the
        // only bits that move anything.
        private static float HorizontalFactor (TextFormatFlags flags)
            => flags.HasFlag (TextFormatFlags.HorizontalCenter) ? 0.5f
             : flags.HasFlag (TextFormatFlags.Right) ? 1f
             : 0f;

        private static float VerticalFactor (TextFormatFlags flags)
            => flags.HasFlag (TextFormatFlags.VerticalCenter) ? 0.5f
             : flags.HasFlag (TextFormatFlags.Bottom) ? 1f
             : 0f;

        /// <summary>Draws text with its top-left corner at the given point.</summary>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, System.Drawing.Color.Empty, TextFormatFlags.Left | TextFormatFlags.NoClipping);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, TextFormatFlags flags)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, System.Drawing.Color.Empty, flags | TextFormatFlags.NoClipping);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, System.Drawing.Color backColor)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, backColor, TextFormatFlags.Left | TextFormatFlags.NoClipping);

        /// <inheritdoc cref="DrawText(Majorsilence.Forms.Drawing.IDeviceContext,string,Majorsilence.Forms.Drawing.Font,System.Drawing.Point,System.Drawing.Color)"/>
        public static void DrawText (Majorsilence.Forms.Drawing.IDeviceContext dc, string text, Majorsilence.Forms.Drawing.Font font, System.Drawing.Point pt, System.Drawing.Color foreColor, System.Drawing.Color backColor, TextFormatFlags flags)
            => DrawText (dc, text, font, AtPoint (pt, text, font), foreColor, backColor, flags | TextFormatFlags.NoClipping);

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
            // Measure what will be DRAWN: with prefix processing on (the default) the ampersand disappears,
            // so measuring the raw string sizes a control for a character that never appears -- which both
            // widens it and knocks centred text off-centre by the same amount.
            text = DisplayText (text, flags, out _);

            // SingleLine means "do not wrap", so the proposed width must not constrain the answer.
            var constraint = flags.HasFlag (TextFormatFlags.SingleLine)
                ? new Size (int.MaxValue, proposedSize.Height)
                : proposedSize;

            return MeasureText (text, font, constraint);
        }

        // WinForms' point-based DrawText measures the text and draws it in that box, rather than
        // passing a zero-sized rectangle through to the renderer -- which is what the previous
        // implementation did, so nothing was drawn.
        //
        // The box is exactly text-sized, so the alignment flags have no slack to work with and the
        // point stays the top-left corner. That matches the documented WinForms contract for this
        // overload; callers who want alignment need to pass a rectangle. The callers above add
        // NoClipping because a text-tight clip would shave antialiased edges and glyph overhang,
        // and WinForms does not clip point-positioned text at all.
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
        /// <summary>Applies the default formatting.</summary>
        Default = 0,
        /// <summary>Expands tab characters.</summary>
        ExpandTabs = 64,
        /// <summary>Includes the font's external leading in the line height.</summary>
        ExternalLeading = 512,
        /// <summary>Adds padding for glyphs that overhang their cell.</summary>
        GlyphOverhangPadding = 0,
        /// <summary>Reserved.</summary>
        Internal = 4096,
        /// <summary>Modifies the supplied string to match the text drawn.</summary>
        ModifyString = 65536,
        /// <summary>Does not break on a double-width character.</summary>
        NoFullWidthCharacterBreak = 524288,
        /// <summary>Trims the middle of the path, keeping the file name.</summary>
        PathEllipsis = 16384,
        /// <summary>Draws only the mnemonic underline, not the text.</summary>
        PrefixOnly = 2097152,
        /// <summary>Lays the text out right to left.</summary>
        RightToLeft = 131072,
        /// <summary>Uses the text box control's line-height rules.</summary>
        TextBoxControl = 8192,
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
