using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Describes the style to apply to a run of characters within a TextBox/RichTextBox's text,
    /// for use with <see cref="TextBox.Colorizer"/> (e.g. syntax highlighting). Spans should be
    /// non-overlapping and given in left-to-right order; gaps between spans render in the
    /// control's normal foreground color.
    /// </summary>
    /// <remarks>
    /// <paramref name="BackColor"/> left at its default (a fully transparent colour) means "do not
    /// paint a background", which is how an unset value is told apart from a deliberate one --
    /// <see cref="RichTextBox.SelectionBackColor"/> relies on that.
    /// </remarks>
    public readonly record struct TextSpanStyle (int Start, int Length, SKColor Color, bool Bold = false, bool Underline = false,
                                                 bool Italic = false, SKColor BackColor = default);
}
