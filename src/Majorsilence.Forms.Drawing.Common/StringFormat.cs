using System;

namespace Majorsilence.Forms.Drawing
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
            MeasurableCharacterRanges = format.MeasurableCharacterRanges is { } r ? (CharacterRange[])r.Clone () : null;
        }

        /// <summary>
        /// Gets the character ranges most recently supplied to <see cref="SetMeasurableCharacterRanges"/>,
        /// used by Graphics.MeasureCharacterRanges.
        /// </summary>
        public CharacterRange[]? MeasurableCharacterRanges { get; private set; }

        /// <summary>Specifies the character ranges measured by Graphics.MeasureCharacterRanges.</summary>
        public void SetMeasurableCharacterRanges (CharacterRange[] ranges) => MeasurableCharacterRanges = ranges;

        private float firstTabOffset;
        private float[] tabStops = [];

        /// <summary>Sets the tab stops for this format, as offsets in the text's own units.</summary>
        /// <param name="firstTabOffset">The distance from the origin to the first tab stop.</param>
        /// <param name="tabStops">The distance between each subsequent tab stop.</param>
        /// <remarks>
        /// Stored and round-tripped. The text renderer here measures and draws runs without a tab-stop
        /// pass, so tabs are not yet laid out to these positions.
        /// </remarks>
        public void SetTabStops (float firstTabOffset, float[] tabStops)
        {
            this.firstTabOffset = firstTabOffset;
            this.tabStops = tabStops is null ? [] : (float[])tabStops.Clone ();
        }

        /// <summary>Gets the tab stops previously set by <see cref="SetTabStops"/>.</summary>
        public float[] GetTabStops (out float firstTabOffset)
        {
            firstTabOffset = this.firstTabOffset;
            return (float[])tabStops.Clone ();
        }

        /// <summary>Gets the digit substitution method set by <see cref="SetDigitSubstitution"/>.</summary>
        public StringDigitSubstitute DigitSubstitutionMethod { get; private set; } = StringDigitSubstitute.User;

        /// <summary>Gets the language set by <see cref="SetDigitSubstitution"/>, as a language ID.</summary>
        public int DigitSubstitutionLanguage { get; private set; }

        /// <summary>
        /// Sets how digits are substituted for the given language. Stored and round-tripped; the text
        /// path draws the code points it is given without locale-based digit substitution.
        /// </summary>
        public void SetDigitSubstitution (int language, StringDigitSubstitute substitute)
        {
            DigitSubstitutionLanguage = language;
            DigitSubstitutionMethod = substitute;
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
        public Majorsilence.Forms.Drawing.Text.HotkeyPrefix HotkeyPrefix { get; set; } = Majorsilence.Forms.Drawing.Text.HotkeyPrefix.None;

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
        // These two were transposed relative to GDI+ (DirectionVertical was 1 and DirectionRightToLeft
        // was 2). Corrected in Phase 2 of docs/gdi-gap-plan.md: the values are persisted as raw integers
        // by designer/.resx code, so a swap silently turns right-to-left text into vertical text.
        /// <summary>Text is laid out right-to-left.</summary>
        DirectionRightToLeft = 1,
        /// <summary>Text is laid out vertically.</summary>
        DirectionVertical = 2,
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
        NoClip = 16384,

        // --- Aliases and values completed from upstream System.Drawing.Common (see docs/gdi-gap-plan.md, Phase 2). ---
        /// <summary>Measure trailing spaces.</summary>
        MeasureTrailingSpaces = 0x800,
    }

    // HotkeyPrefix is declared in Majorsilence.Forms.Drawing.Text, where System.Drawing puts it.
    // A second copy here made the name ambiguous for any file importing both namespaces.
}
