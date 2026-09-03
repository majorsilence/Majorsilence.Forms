namespace Majorsilence.Forms
{
    /// <summary>
    /// A TextBox that restricts user input to a mask.
    /// </summary>
    /// <remarks>
    /// The mask is enforced as of W5.13 (finding TXT-03, P0). Before that it was stored and read by
    /// nothing: <c>Text</c> was whatever had been typed, <c>MaskCompleted</c> and <c>MaskFull</c> were
    /// <c>=> true</c> unconditionally, and no prompt characters were shown -- so a phone/SSN/date field
    /// accepted anything, <c>if (!mtb.MaskCompleted) { error }</c> never fired, and the text an app
    /// parsed no longer contained the literal separators it expected.
    /// <para>
    /// The engine is the BCL's own <see cref="System.ComponentModel.MaskedTextProvider"/>, which is
    /// cross-platform and is what upstream uses too, so nothing here reimplements mask semantics. This
    /// class owns one live provider and keeps the document showing
    /// <c>provider.ToDisplayString ()</c>; an empty <see cref="Mask"/> means no provider and plain
    /// <see cref="TextBox"/> behaviour, as upstream's null-mask path does.
    /// </para>
    /// </remarks>
    public partial class MaskedTextBox : TextBox
    {
        private string _mask = string.Empty;
        private System.ComponentModel.MaskedTextProvider? provider;

        /// <summary>Gets or sets the type used to validate the committed text.</summary>
        public System.Type? ValidatingType { get; set; }

        /// <summary>Gets or sets the input mask string.</summary>
        public string Mask {
            get => _mask;
            set {
                var mask = value ?? string.Empty;

                if (string.Equals (_mask, mask, StringComparison.Ordinal))
                    return;

                // The text already in the box is carried into the new mask where it fits, which is what
                // upstream does -- a designer sets Text before Mask as often as after.
                var current = provider is null ? base.Text : provider.ToString (false, false);

                _mask = mask;
                provider = CreateProvider (current);

                ApplyProviderToDocument ();
                OnMaskChanged (EventArgs.Empty);
            }
        }

        private System.ComponentModel.MaskedTextProvider? CreateProvider (string? seed)
        {
            if (string.IsNullOrEmpty (_mask))
                return null;

            // AsciiOnly is a constructor argument on the provider, not a settable property.
            var created = new System.ComponentModel.MaskedTextProvider (
                _mask, Culture ?? System.Globalization.CultureInfo.CurrentCulture, AsciiOnly) {
                PromptChar = PromptChar,
                ResetOnPrompt = ResetOnPrompt,
                ResetOnSpace = ResetOnSpace,
                SkipLiterals = SkipLiterals,
            };

            if (!string.IsNullOrEmpty (seed))
                created.Set (seed);

            return created;
        }

        // Pushes what the provider holds into the document, and keeps the caret at the next edit
        // position -- the caret matters as much as the text, because a mask moves it over literals.
        private void ApplyProviderToDocument (int? caret = null)
        {
            if (provider is null)
                return;

            // Straight to the base: typed input arrives through InsertTypedCharacter rather than
            // through a document-changed callback, so there is no echo to guard against here.
            base.Text = provider.ToDisplayString ();

            var position = caret ?? provider.LastAssignedPosition + 1;

            SelectionStart = Math.Max (0, Math.Min (position, base.Text.Length));
        }

        /// <summary>Gets or sets the character used to prompt for required input.</summary>
        public char PromptChar {
            get => prompt_char;
            set {
                if (prompt_char == value)
                    return;

                prompt_char = value;

                if (provider is not null) {
                    provider.PromptChar = value;
                    ApplyProviderToDocument ();
                }
            }
        }

        private char prompt_char = '_';

        /// <summary>Gets or sets whether the system password character is used.</summary>
        /// <remarks>Forwards to <see cref="TextBox"/>, which actually masks the display. It used to be a
        /// <c>new bool</c> shadow storing a flag nothing read, so a PIN or account-number box with this
        /// set showed its contents in clear text (TXT-18).</remarks>
        public new bool UseSystemPasswordChar {
            get => base.UseSystemPasswordChar;
            set => base.UseSystemPasswordChar = value;
        }

        /// <summary>Gets or sets whether the prompt characters are removed when focus leaves. Stub in Majorsilence.Forms.</summary>
        public bool HidePromptOnLeave { get; set; }

        /// <summary>Gets whether every required mask position has been filled.</summary>
        /// <remarks>From the provider. It was <c>=> true</c>, so mask validation always passed and the
        /// documented <c>if (!MaskCompleted)</c> guard was dead code (TXT-03).</remarks>
        public bool MaskCompleted => provider?.MaskCompleted ?? true;

        /// <summary>Gets whether every editable mask position has been filled.</summary>
        /// <inheritdoc cref="MaskCompleted" path="/remarks"/>
        public bool MaskFull => provider?.MaskFull ?? true;

        /// <summary>Gets or sets which parts of the mask are placed on the clipboard by cut and copy.</summary>
        /// <remarks>
        /// WinForms types this as <see cref="MaskFormat"/>; it was a <c>bool</c> here, which meant the
        /// property existed under the right name with a type nothing could assign to it. Stored: the mask
        /// is not enforced, so there is no literal-versus-input distinction to act on.
        /// </remarks>
        public MaskFormat CutCopyMaskFormat { get; set; } = MaskFormat.IncludeLiterals;

        /// <summary>Gets or sets the culture used for separator characters.</summary>
        public System.Globalization.CultureInfo? Culture {
            get => culture;
            set {
                culture = value;
                RebuildProvider ();
            }
        }

        private System.Globalization.CultureInfo? culture;

        // The provider takes its culture and its ASCII-only rule as constructor arguments, so changing
        // either means a new provider seeded from what the current one holds.
        private void RebuildProvider ()
        {
            if (provider is null)
                return;

            provider = CreateProvider (provider.ToString (false, false));
            ApplyProviderToDocument ();
        }

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
        public System.ComponentModel.MaskedTextProvider? MaskedTextProvider
            // A clone, not the live one: handing out the instance this box types into would let a
            // caller desynchronise the document from the provider behind its back. Upstream clones too.
            => provider is null ? null : (System.ComponentModel.MaskedTextProvider)provider.Clone ();

        /// <summary>Gets or sets which parts of the mask <see cref="Text"/> includes.</summary>
        public MaskFormat TextMaskFormat {
            get => text_mask_format;
            set {
                text_mask_format = value;

                // Text is computed from this, so its value changes even though nothing was typed.
                if (provider is not null)
                    OnTextChanged (EventArgs.Empty);
            }
        }

        private MaskFormat text_mask_format = MaskFormat.IncludeLiterals;

        /// <summary>Gets or sets whether only ASCII characters are accepted.</summary>
        public bool AsciiOnly {
            get => ascii_only;
            set {
                ascii_only = value;
                RebuildProvider ();
            }
        }

        private bool ascii_only;

        /// <summary>Gets or sets whether a beep occurs when invalid input is detected. Stub in Majorsilence.Forms.</summary>
        public bool BeepOnError { get; set; }

        /// <summary>
        /// Gets or sets the text in the box, formatted per <see cref="TextMaskFormat"/>.
        /// </summary>
        /// <remarks>
        /// With a mask, this is the provider's view rather than the raw document: the default
        /// <see cref="MaskFormat.IncludeLiterals"/> is what an app parses (<c>"(555) 123-4567"</c>),
        /// where the unmasked box returned whatever had been typed. Assigning runs the value through
        /// the provider, so a value that does not fit the mask is rejected rather than stored.
        /// </remarks>
        public override string Text {
            get {
                if (provider is null)
                    return base.Text;

                return provider.ToString (
                    includePrompt: text_mask_format is MaskFormat.IncludePrompt or MaskFormat.IncludePromptAndLiterals,
                    includeLiterals: text_mask_format is MaskFormat.IncludeLiterals or MaskFormat.IncludePromptAndLiterals);
            }
            set {
                if (provider is null) {
                    base.Text = value;
                    return;
                }

                provider.Clear ();

                if (!string.IsNullOrEmpty (value)) {
                    // Set reports the first offending position rather than throwing, which is what
                    // MaskInputRejected exists to carry.
                    if (!provider.Set (value, out var position, out var hint) && position >= 0)
                        OnMaskInputRejected (new MaskInputRejectedEventArgs (position, Translate (hint)));
                }

                ApplyProviderToDocument (caret: 0);
                OnTextChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// What the field shows: the provider's display string, prompts and literals included.
        /// </summary>
        /// <remarks>
        /// The document is this control's display buffer, and <see cref="Text"/> deliberately reports
        /// something else (the provider's value under <see cref="TextMaskFormat"/>), so the two are not
        /// interchangeable — no prompt characters appearing at all is what made a masked box look like
        /// a plain <see cref="TextBox"/>. This is the string the renderer draws.
        /// </remarks>
        public string DisplayedMaskText => base.Text;

        /// <inheritdoc/>
        protected override bool InsertTypedCharacter (KeyPressEventArgs e)
        {
            if (provider is null)
                return base.InsertTypedCharacter (e);

            var position = SelectionStart;

            // Replace, not InsertAt: a mask has fixed positions, so typing overwrites the character at
            // the caret rather than pushing the rest of the field along.
            if (provider.Replace (e.KeyChar, position, out var resultPosition, out var hint)) {
                ApplyProviderToDocument (caret: resultPosition + 1);
                OnTextChanged (EventArgs.Empty);

                return true;
            }

            // Every rejected character is reported, which is the whole point of the event: a
            // "digits only" field can flash a hint instead of silently swallowing the keystroke.
            OnMaskInputRejected (new MaskInputRejectedEventArgs (position, Translate (hint)));

            return false;
        }

        /// <inheritdoc/>
        protected override bool DeleteAtCaret (bool forward, bool wholeWord)
        {
            if (provider is null)
                return base.DeleteAtCaret (forward, wholeWord);

            var position = forward ? SelectionStart : SelectionStart - 1;

            if (position < 0 || position >= provider.Length)
                return false;

            // Clearing a position, not removing it: the mask's literals and length are fixed, so
            // Backspace blanks the character back to its prompt rather than shortening the field.
            if (!provider.Replace (provider.PromptChar, position, out _, out var hint)) {
                OnMaskInputRejected (new MaskInputRejectedEventArgs (position, Translate (hint)));
                return false;
            }

            ApplyProviderToDocument (caret: position);
            OnTextChanged (EventArgs.Empty);

            return true;
        }

        /// <inheritdoc/>
        /// <remarks>Runs the type validation upstream runs here, so the documented way to validate a
        /// masked box -- <see cref="TypeValidationCompleted"/> -- actually fires, and a handler can
        /// cancel the focus change (TXT-19).</remarks>
        protected override void OnValidating (System.ComponentModel.CancelEventArgs e)
        {
            base.OnValidating (e);

            if (e.Cancel || ValidatingType is null)
                return;

            var args = PerformTypeValidation ();

            OnTypeValidationCompleted (args);

            if (args.Cancel)
                e.Cancel = true;
        }

        private TypeValidationEventArgs PerformTypeValidation ()
        {
            var text = provider is null ? base.Text : provider.ToString (false, true);

            if (provider is not null && !provider.MaskCompleted)
                return new TypeValidationEventArgs (ValidatingType, false, null,
                    "The mask has not been completely filled in.");

            try {
                var value = Convert.ChangeType (text, ValidatingType!,
                    Culture ?? System.Globalization.CultureInfo.CurrentCulture);

                return new TypeValidationEventArgs (ValidatingType, true, value, string.Empty);
            } catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException) {
                return new TypeValidationEventArgs (ValidatingType, false, null, ex.Message);
            }
        }

        // The provider's hints and this layer's enum carry the same numbers where they overlap; anything
        // unrecognised becomes Unknown rather than a cast to an undefined value.
        private static MaskedTextResultHint Translate (System.ComponentModel.MaskedTextResultHint hint)
            => EnumCompat.IsDefined ((MaskedTextResultHint)(int)hint)
                ? (MaskedTextResultHint)(int)hint
                : MaskedTextResultHint.Unknown;

        /// <summary>Raised when user input is rejected by the mask.</summary>
        public event MaskInputRejectedEventHandler? MaskInputRejected;

        /// <summary>Raises the <see cref="MaskInputRejected"/> event.</summary>
        protected virtual void OnMaskInputRejected (MaskInputRejectedEventArgs e) => MaskInputRejected?.Invoke (this, e);

        /// <summary>Raised when type validation completes.</summary>
        public event TypeValidationEventHandler? TypeValidationCompleted;

        /// <summary>Raises the <see cref="TypeValidationCompleted"/> event.</summary>
        protected virtual void OnTypeValidationCompleted (TypeValidationEventArgs e) => TypeValidationCompleted?.Invoke (this, e);
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
