using System;

namespace Continuum.Drawing
{
    /// <summary>
    /// Encapsulates text layout information (alignment, trimming, flags). Cross-platform replacement
    /// for <c>System.Drawing.StringFormat</c>.
    /// </summary>
    public sealed class StringFormat : IDisposable, ICloneable
    {
        /// <summary>Initializes a new instance of the StringFormat class.</summary>
        public StringFormat () { }

        /// <summary>Initializes a new instance of the StringFormat class with the specified flags.</summary>
        public StringFormat (StringFormatFlags options)
        {
            FormatFlags = options;
        }

        /// <summary>Initializes a new instance copied from an existing StringFormat.</summary>
        public StringFormat (StringFormat format)
        {
            if (format is null)
                return;

            Alignment = format.Alignment;
            LineAlignment = format.LineAlignment;
            FormatFlags = format.FormatFlags;
            Trimming = format.Trimming;
            HotkeyPrefix = format.HotkeyPrefix;
        }

        /// <summary>Gets a generic default StringFormat.</summary>
        public static StringFormat GenericDefault => new StringFormat ();

        /// <summary>Gets a generic typographic StringFormat.</summary>
        public static StringFormat GenericTypographic => new StringFormat { Trimming = StringTrimming.None };

        /// <summary>Gets or sets the horizontal alignment of the text.</summary>
        public StringAlignment Alignment { get; set; } = StringAlignment.Near;

        /// <summary>Gets or sets the vertical alignment of the text.</summary>
        public StringAlignment LineAlignment { get; set; } = StringAlignment.Near;

        /// <summary>Gets or sets the format flags.</summary>
        public StringFormatFlags FormatFlags { get; set; }

        /// <summary>Gets or sets how text is trimmed when it doesn't fit.</summary>
        public StringTrimming Trimming { get; set; } = StringTrimming.Character;

        /// <summary>Gets or sets the type of hotkey prefix processing.</summary>
        public HotkeyPrefix HotkeyPrefix { get; set; } = HotkeyPrefix.None;

        /// <summary>Creates an exact copy of this StringFormat.</summary>
        public object Clone () => new StringFormat (this);

        /// <inheritdoc/>
        public void Dispose () { }
    }

    /// <summary>Specifies the alignment of text. Matches System.Drawing.StringAlignment.</summary>
    public enum StringAlignment
    {
        /// <summary>Aligned to the near edge (left/top).</summary>
        Near = 0,
        /// <summary>Centered.</summary>
        Center = 1,
        /// <summary>Aligned to the far edge (right/bottom).</summary>
        Far = 2
    }

    /// <summary>Specifies how text is trimmed. Matches System.Drawing.StringTrimming.</summary>
    public enum StringTrimming
    {
        /// <summary>No trimming.</summary>
        None = 0,
        /// <summary>Trim to the nearest character.</summary>
        Character = 1,
        /// <summary>Trim to the nearest word.</summary>
        Word = 2,
        /// <summary>Trim to the nearest character and add an ellipsis.</summary>
        EllipsisCharacter = 3,
        /// <summary>Trim to the nearest word and add an ellipsis.</summary>
        EllipsisWord = 4,
        /// <summary>Trim the center of a path and add an ellipsis.</summary>
        EllipsisPath = 5
    }

    /// <summary>Specifies text layout flags. Matches System.Drawing.StringFormatFlags.</summary>
    [Flags]
    public enum StringFormatFlags
    {
        /// <summary>Text is laid out vertically.</summary>
        DirectionVertical = 1,
        /// <summary>Text is laid out right-to-left.</summary>
        DirectionRightToLeft = 2,
        /// <summary>Parts of characters are allowed to overhang the layout rectangle.</summary>
        FitBlackBox = 4,
        /// <summary>Control characters are displayed.</summary>
        DisplayFormatControl = 32,
        /// <summary>Fallback to alternate fonts is disabled.</summary>
        NoFontFallback = 1024,
        /// <summary>Wrapping of text between lines is disabled.</summary>
        NoWrap = 4096,
        /// <summary>Clipping of text is disabled.</summary>
        LineLimit = 8192,
        /// <summary>Text extending outside the layout rectangle is not clipped.</summary>
        NoClip = 16384
    }

    /// <summary>Specifies how to process the hotkey prefix in text. Matches System.Drawing.Text.HotkeyPrefix.</summary>
    public enum HotkeyPrefix
    {
        /// <summary>No hotkey prefix processing.</summary>
        None = 0,
        /// <summary>Display the hotkey prefix (underline the following character).</summary>
        Show = 1,
        /// <summary>Do not display the hotkey prefix.</summary>
        Hide = 2
    }
}
