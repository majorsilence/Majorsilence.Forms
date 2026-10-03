namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// A backend-neutral hint about what a focused text control expects, so a single-view backend
    /// (browser / Android / iOS) can pick an appropriate on-screen keyboard layout. Desktop backends
    /// ignore it — the hardware keyboard has no layout to choose.
    /// </summary>
    /// <remarks>
    /// WinForms has no notion of input "type" beyond password masking, so <see cref="Normal"/>, <see cref="Multiline"/> and
    /// <see cref="Password"/> are derived from the control. The rest are what a control asks for with
    /// <see cref="TextBoxBase.InputKind"/> (#368); a masked box that asks for <see cref="Number"/> or <see cref="Phone"/> reports
    /// <see cref="Pin"/>.
    /// </remarks>
    public enum TextInputKind
    {
        /// <summary>Single-line free text — the default keyboard.</summary>
        Normal,
        /// <summary>Multi-line free text — the keyboard's return key inserts a newline rather than submitting.</summary>
        Multiline,
        /// <summary>Masked entry — no autocorrect, no suggestions, no keystroke previews.</summary>
        Password,
        /// <summary>An e-mail address — the keyboard surfaces <c>@</c> and <c>.</c>.</summary>
        Email,
        /// <summary>A number — a numeric keypad.</summary>
        Number,
        /// <summary>A URL — the keyboard surfaces <c>/</c> and <c>.com</c>.</summary>
        Url,
        /// <summary>A telephone number — a phone dial pad.</summary>
        Phone,
        /// <summary>
        /// A PIN — a numeric keypad that is also masked: no autocorrect, no suggestions, no keystroke previews. Neither <see cref="Number"/>
        /// nor <see cref="Password"/> alone says both, so a masked box that asks for a number keyboard reports this.
        /// </summary>
        Pin
    }
}
