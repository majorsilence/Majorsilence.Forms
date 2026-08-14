namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: a TextBox that restricts user input based on a mask string.
    /// In Majorsilence.Forms, the mask is not enforced; this behaves as a plain TextBox.
    /// </summary>
    public partial class MaskedTextBox : TextBox
    {
        private string _mask = string.Empty;

        /// <summary>Gets or sets the type used to validate the committed text (WinForms compat; stored, no validation is performed).</summary>
        public System.Type? ValidatingType { get; set; }

        /// <summary>Gets or sets the input mask string. Not enforced in Majorsilence.Forms.</summary>
        public string Mask {
            get => _mask;
            set => _mask = value ?? string.Empty;
        }

        /// <summary>Gets or sets the character used for prompting for required input. Not enforced in Majorsilence.Forms.</summary>
        public char PromptChar { get; set; } = '_';

        /// <summary>Gets or sets whether the mask is used to filter input. Stub in Majorsilence.Forms.</summary>
        public new bool UseSystemPasswordChar { get; set; }

        /// <summary>Gets or sets whether the prompt characters are included in the text. Stub in Majorsilence.Forms.</summary>
        public bool HidePromptOnLeave { get; set; }

        /// <summary>Gets whether the text currently satisfies the mask. Always returns true in Majorsilence.Forms.</summary>
        public bool MaskCompleted => true;

        /// <summary>Gets whether all required positions in the mask are satisfied. Always returns true in Majorsilence.Forms.</summary>
        public bool MaskFull => true;

        /// <summary>Gets or sets which parts of the mask are placed on the clipboard by cut and copy.</summary>
        /// <remarks>
        /// WinForms types this as <see cref="MaskFormat"/>; it was a <c>bool</c> here, which meant the
        /// property existed under the right name with a type nothing could assign to it. Stored: the mask
        /// is not enforced, so there is no literal-versus-input distinction to act on.
        /// </remarks>
        public MaskFormat CutCopyMaskFormat { get; set; } = MaskFormat.IncludeLiterals;

        /// <summary>Gets or sets the culture used for separator characters. Stub in Majorsilence.Forms.</summary>
        public System.Globalization.CultureInfo? Culture { get; set; }

        /// <summary>Gets a mask provider describing this box's mask.</summary>
        /// <remarks>
        /// A real <see cref="System.ComponentModel.MaskedTextProvider"/> -- the type is in the BCL and is
        /// cross-platform, so there is no reason to stand in for it. It returned the box's own <c>Text</c>
        /// as a string before, which is the wrong type and the wrong value: callers use this to ask the
        /// mask questions (<c>ToDisplayString</c>, <c>MaskCompleted</c>, edit positions), not to read the
        /// text back.
        ///
        /// Built on demand from <see cref="Mask"/> and seeded with the current text, and null when no mask
        /// is set -- matching WinForms, where a maskless box has no provider. This does not make the box
        /// enforce its mask; it makes the description of the mask available and correct.
        /// </remarks>
        public System.ComponentModel.MaskedTextProvider? MaskedTextProvider {
            get {
                if (string.IsNullOrEmpty (_mask))
                    return null;

                // AsciiOnly is a constructor argument on the provider, not a settable property.
                var provider = new System.ComponentModel.MaskedTextProvider (
                    _mask, Culture ?? System.Globalization.CultureInfo.CurrentCulture, AsciiOnly) {
                    PromptChar = PromptChar,
                };

                if (Text.Length > 0)
                    provider.Set (Text);

                return provider;
            }
        }

        /// <summary>Gets or sets the text mask format used for cut and copy operations. Stub in Majorsilence.Forms.</summary>
        public MaskFormat TextMaskFormat { get; set; } = MaskFormat.IncludeLiterals;

        /// <summary>Gets or sets whether only ASCII characters are accepted. Stub in Majorsilence.Forms.</summary>
        public bool AsciiOnly { get; set; }

        /// <summary>Gets or sets whether a beep occurs when invalid input is detected. Stub in Majorsilence.Forms.</summary>
        public bool BeepOnError { get; set; }

        /// <summary>Raised when user input is rejected by the mask. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<MaskInputRejectedEventArgs>? MaskInputRejected { add { } remove { } }

        /// <summary>Raised when type validation completes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<TypeValidationEventArgs>? TypeValidationCompleted { add { } remove { } }
    }

    /// <summary>Specifies how literal characters in the mask are included in the text.</summary>
    public enum MaskFormat
    {
        /// <summary>Only the raw input characters.</summary>
        ExcludePromptAndLiterals = 0,
        /// <summary>Include literal characters but not the prompt character.</summary>
        IncludeLiterals = 2,
        /// <summary>Include prompt characters but not literal characters.</summary>
        IncludePrompt = 1,
        /// <summary>Include both prompt and literal characters.</summary>
        IncludePromptAndLiterals = 3,
    }

    /// <summary>Provides data for the MaskInputRejected event.</summary>
    public class MaskInputRejectedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of MaskInputRejectedEventArgs.</summary>
        public MaskInputRejectedEventArgs (int position, MaskedTextResultHint rejectionHint)
        {
            Position = position;
            RejectionHint = rejectionHint;
        }

        /// <summary>Gets the position of the rejected input.</summary>
        public int Position { get; }

        /// <summary>Gets the hint describing why the input was rejected.</summary>
        public MaskedTextResultHint RejectionHint { get; }
    }

    /// <summary>Provides data for the TypeValidationCompleted event.</summary>
    public class TypeValidationEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of TypeValidationEventArgs.</summary>
        public TypeValidationEventArgs (Type? validatingType, bool isValidInput, object? returnValue, string message)
        {
            ValidatingType = validatingType;
            IsValidInput = isValidInput;
            ReturnValue = returnValue;
            Message = message;
        }

        /// <summary>Gets the type that was validated.</summary>
        public Type? ValidatingType { get; }

        /// <summary>Gets whether the text was valid for the type.</summary>
        public bool IsValidInput { get; }

        /// <summary>Gets or sets whether this event should be cancelled.</summary>
        public bool Cancel { get; set; }

        /// <summary>Gets the validated value, or null if validation failed.</summary>
        public object? ReturnValue { get; }

        /// <summary>Gets a message describing the validation result.</summary>
        public string Message { get; }
    }

    /// <summary>Provides hints about why masked text input was rejected.</summary>
    public enum MaskedTextResultHint
    {
        /// <summary>Operation succeeded.</summary>
        Success = 1,
        /// <summary>Side effect — a prompt character was moved.</summary>
        SideEffectNoCharacterAdded = -1,
        /// <summary>No character was shifted.</summary>
        NoCharacterShifted = -2,
        /// <summary>Unknown.</summary>
        Unknown = -256,
        /// <summary>Character is not an ASCII character and AsciiOnly is true.</summary>
        AsciiCharacterExpected = -55,
        /// <summary>Character does not match the mask.</summary>
        DigitExpected = -57,
        /// <summary>Letter expected but not provided.</summary>
        LetterExpected = -56,
        /// <summary>Alphanumeric character expected.</summary>
        AlphanumericCharacterExpected = -54,
        /// <summary>Sign expected.</summary>
        SignedDigitExpected = -53,
        /// <summary>Position is not editable.</summary>
        UnavailableEditPosition = -52,
        /// <summary>The input falls outside the valid input range.</summary>
        PositionOutOfRange = -51,
    }
}
