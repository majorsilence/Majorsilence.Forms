namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: a TextBox that restricts user input based on a mask string.
    /// In Majorsilence.Forms, the mask is not enforced; this behaves as a plain TextBox.
    /// </summary>
    public class MaskedTextBox : TextBox
    {
        private string _mask = string.Empty;

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

        /// <summary>Gets or sets whether to cut/copy the mask along with the text. Stub in Majorsilence.Forms.</summary>
        public bool CutCopyMaskFormat { get; set; }

        /// <summary>Gets or sets the culture used for separator characters. Stub in Majorsilence.Forms.</summary>
        public System.Globalization.CultureInfo? Culture { get; set; }

        /// <summary>Gets the text without the formatting characters defined by the mask.</summary>
        public string? MaskedTextProvider => Text;

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
        IncludeLiterals = 1,
        /// <summary>Include prompt characters but not literal characters.</summary>
        IncludePrompt = 2,
        /// <summary>Include both prompt and literal characters.</summary>
        IncludePromptAndLiterals = 3
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
