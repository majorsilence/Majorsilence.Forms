using System;
using System.Globalization;

namespace Majorsilence.Forms
{
    // SMP-32 (P0) and SMP-33: the text half of NumericUpDown.
    //
    // There was no OnKeyDown, OnKeyPress, OnKeyUp or ProcessDialogKey anywhere on this control. A user
    // could not type a number into it -- the only way to change the value was clicking an arrow, and
    // until W5.20a that moved by 1 whatever Increment said. On a data-entry form with a quantity or an
    // amount, that is a hard blocker. InterceptArrowKeys and ReadOnly were stored and read by nothing,
    // and ReadOnly could not mean anything because there was no editing to block.
    //
    // The edit text lives here rather than in a hosted TextBox. Upstream's UpDownBase hosts one; this
    // control already paints its own text, and giving it a caret model is a smaller change than
    // rehoming the rendering -- at the cost of no mouse selection, which is recorded rather than
    // pretended.
    public partial class NumericUpDown
    {
        // What the user has typed, or null when the control is showing its Value. Kept separate from
        // Value because upstream does not commit per keystroke: a half-typed "1." or "-" is not a
        // number, and parsing on every key would snap it to something the user did not type.
        private string? edit_text;

        // Where the caret sits inside edit_text.
        private int caret;

        // Whether the next typed character replaces the contents rather than being inserted. Upstream
        // hosts a TextBox and selects all of its text when focus arrives, so tabbing into a spin box and
        // typing a number REPLACES what was there -- the behaviour anyone filling in a form expects.
        // Without this, typing 42 into a field showing 7 produced 742.
        private bool replace_on_next_character = true;

        /// <summary>Gets whether the user has typed into the control since the value was last committed.</summary>
        public bool UserEdit {
            get => edit_text is not null;
            protected set {
                if (!value)
                    edit_text = null;
            }
        }

        /// <summary>The text the control displays: what the user is typing, or the formatted value.</summary>
        internal string DisplayText => edit_text ?? FormatValue (Value);

        /// <summary>Where the caret sits in <see cref="DisplayText"/>, or -1 when the control has no focus.</summary>
        internal int CaretPosition => Focused && edit_text is not null ? Math.Min (caret, edit_text.Length) : -1;

        /// <summary>
        /// Formats a value the way this control displays it: hexadecimal, or a fixed/grouped decimal.
        /// </summary>
        /// <remarks>
        /// SMP-33. The renderer built <c>"F" + DecimalPlaces</c> and ignored
        /// <see cref="Hexadecimal"/> and <see cref="ThousandsSeparator"/> entirely, so a currency field
        /// showed <c>1234567.00</c> and a port-number box showed decimal.
        /// </remarks>
        internal string FormatValue (decimal value)
        {
            if (Hexadecimal)
                return ((long)value).ToString ("X", CultureInfo.InvariantCulture);

            var format = (ThousandsSeparator ? "N" : "F") + DecimalPlaces;

            return value.ToString (format, CultureInfo.CurrentCulture);
        }

        // ---------------- typing

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            Guard.ThrowIfNull (e);

            if (e.Handled || !Enabled)
                return;

            switch (e.KeyCode) {
                // Gated on InterceptArrowKeys, which is what that property is for: a grid or a form that
                // wants the arrows for navigation turns it off.
                case Keys.Up when InterceptArrowKeys:
                    CommitEditText ();
                    UpButton ();
                    e.Handled = true;
                    return;

                case Keys.Down when InterceptArrowKeys:
                    CommitEditText ();
                    DownButton ();
                    e.Handled = true;
                    return;

                // Enter commits without waiting for focus to leave, which is what a user pressing it in
                // a data-entry field expects.
                case Keys.Enter:
                    CommitEditText ();
                    e.Handled = true;
                    return;

                case Keys.Escape:
                    edit_text = null;
                    replace_on_next_character = true;
                    Invalidate ();
                    e.Handled = true;
                    return;

                case Keys.Back when !ReadOnly:
                    Backspace ();
                    e.Handled = true;
                    return;

                case Keys.Delete when !ReadOnly:
                    DeleteForward ();
                    e.Handled = true;
                    return;

                case Keys.Left:
                    MoveCaret (-1);
                    e.Handled = true;
                    return;

                case Keys.Right:
                    MoveCaret (1);
                    e.Handled = true;
                    return;

                case Keys.Home:
                    caret = 0;
                    EnsureEditing ();
                    e.Handled = true;
                    return;

                case Keys.End:
                    EnsureEditing ();
                    caret = edit_text!.Length;
                    e.Handled = true;
                    return;
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyPress (KeyPressEventArgs e)
        {
            base.OnKeyPress (e);

            Guard.ThrowIfNull (e);

            if (e.Handled || ReadOnly || !Enabled)
                return;

            if (!IsAcceptableCharacter (e.KeyChar))
                return;

            if (replace_on_next_character) {
                edit_text = string.Empty;
                caret = 0;
                replace_on_next_character = false;
            }

            EnsureEditing ();
            edit_text = edit_text!.Insert (Math.Min (caret, edit_text.Length), e.KeyChar.ToString ());
            caret++;
            e.Handled = true;
            Invalidate ();
        }

        // What may be typed: digits always; the hex letters when Hexadecimal; the culture's decimal
        // separator when DecimalPlaces allows one; and a leading minus when Minimum is negative. Letting
        // anything else through would build a string that can never parse.
        private bool IsAcceptableCharacter (char c)
        {
            if (Hexadecimal)
                return char.IsDigit (c) || (char.ToUpperInvariant (c) >= 'A' && char.ToUpperInvariant (c) <= 'F');

            if (char.IsDigit (c))
                return true;

            var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            if (DecimalPlaces > 0 && separator.Length == 1 && c == separator[0])
                return (edit_text ?? string.Empty).IndexOf (c) < 0;

            // A minus only at the front, and only when a negative value is reachable.
            return c == '-' && Minimum < 0 && caret == 0 && (edit_text ?? string.Empty).IndexOf ('-') < 0;
        }

        private void EnsureEditing ()
        {
            replace_on_next_character = false;

            if (edit_text is not null)
                return;

            // The first keystroke starts from the displayed value, with the caret at its end -- typing
            // into a spin box appends rather than replacing, as upstream's hosted text box does.
            edit_text = FormatValue (Value);
            caret = edit_text.Length;
        }

        private void MoveCaret (int delta)
        {
            EnsureEditing ();
            caret = Math.Max (0, Math.Min (edit_text!.Length, caret + delta));
            Invalidate ();
        }

        private void Backspace ()
        {
            EnsureEditing ();

            if (caret <= 0 || edit_text!.Length == 0)
                return;

            edit_text = edit_text.Remove (caret - 1, 1);
            caret--;
            Invalidate ();
        }

        private void DeleteForward ()
        {
            EnsureEditing ();

            if (caret >= edit_text!.Length)
                return;

            edit_text = edit_text.Remove (caret, 1);
            Invalidate ();
        }

        /// <summary>
        /// Selects a range of the edit text. Mirrors <c>UpDownBase.Select</c>; the caret is placed at the
        /// end of the range.
        /// </summary>
        /// <remarks>
        /// There is no selection HIGHLIGHT: this control paints its own text and has no selection model,
        /// so the range is honoured only as a caret position. Recorded rather than silently ignored --
        /// it was an empty method body before.
        /// </remarks>
        public new void Select (int start, int length)
        {
            EnsureEditing ();
            caret = Math.Max (0, Math.Min (edit_text!.Length, start + Math.Max (0, length)));
            Invalidate ();
        }

        // ---------------- committing

        /// <summary>
        /// Parses what the user typed into <see cref="Value"/>, clamped to the range. Raises
        /// <see cref="ValueChanged"/> once, and only if the value actually moved.
        /// </summary>
        /// <remarks>
        /// Called on losing focus and on Enter, not per keystroke -- matching upstream, where a
        /// half-typed "1." is not yet a number and committing it would snap the field under the user.
        /// </remarks>
        internal void CommitEditText ()
        {
            // Committing ends the edit, so the next character typed starts a fresh one -- the same state
            // arriving by focus leaves the control in.
            replace_on_next_character = true;

            if (edit_text is null)
                return;

            var typed = edit_text;
            edit_text = null;

            // Not clamped here: Value's own setter already does it, and a second clamp is a redundancy
            // no test can tell from a working one -- removing one enforcement point was W5.19's lesson
            // and W5.2b's before it.
            if (TryParseTyped (typed, out var parsed))
                Value = parsed;

            // An unparseable string is discarded and the control returns to showing its value, which is
            // what upstream's ValidateEditText does.
            Invalidate ();
        }

        private bool TryParseTyped (string text, out decimal value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace (text))
                return false;

            if (Hexadecimal)
                return long.TryParse (text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                    && TrySet (hex, out value);

            return decimal.TryParse (text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);

            static bool TrySet (long from, out decimal to)
            {
                to = from;
                return true;
            }
        }

    }
}
