using System.Drawing;
using SkiaSharp;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    internal sealed class TextBoxDocument
    {
        private readonly TextBox textbox;

        private string text = string.Empty;
        private string placeholder = string.Empty;

        private TextBlock? cached_text_block;

        private bool enabled = true;
        private int cursor_index;
        private bool read_only;
        private int selection_start = -1;
        private int selection_end = -1;
        private int max_length;     // 0 == no limit, the WinForms convention; see MaxLength
        private bool multiline;
        private char? password_char;
        private int width = -1;
        private SKTypeface font = Theme.UIFont;
        private TextAlignment alignment = TextAlignment.Left;
        private SKColor placeholder_font_color = Theme.ForegroundDisabledColor;
        private SKColor selection_color = Theme.TextSelectionBackgroundColor;

        private static readonly string[] invalid_singleline_characters = ["\r", "\n"];

        internal TextBoxDocument (TextBox textbox)
        {
            this.textbox = textbox;
            width = textbox.PaddedClientRectangle.Width;
        }

        public TextAlignment Alignment {
            get => alignment;
            set {
                if (alignment != value) {
                    alignment = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public bool AtBeginning => cursor_index == 0;

        public bool AtEnd => cursor_index == text.Length;

        public int CursorIndex => cursor_index;

        public bool DeleteSelection ()
        {
            if (!IsTextSelected || read_only)
                return false;

            var start = Math.Min (selection_start, selection_end);
            var end = Math.Max (selection_start, selection_end);

            SetCursorToCharIndex (start);

            RemoveText (start, end - start);

            Deselect ();

            return true;
        }

        public bool DeleteText (bool forward, bool wholeWord)
        {
            // TODO: wholeWord not implemented
            if (read_only)
                return false;

            if (DeleteSelection ())
                return true;

            if (forward && !AtEnd) {
                RemoveText (cursor_index, 1);
                return true;
            }

            if (!forward && !AtBeginning) {
                SetCursorToCharIndex (cursor_index - 1);

                RemoveText (cursor_index, 1);

                return true;
            }

            return false;
        }

        public bool Deselect ()
        {
            if (!IsTextSelected)
                return false;

            selection_start = -1;
            selection_end = -1;

            return true;
        }

        // Win32 edit controls keep a single-level undo buffer that Undo toggles: undo, then undo again
        // to redo. Consecutive edits of the same kind coalesce, so undoing a run of typing reverts the
        // whole run rather than one character -- which is what makes single-level undo usable.
        private enum EditKind { None, Insert, Remove }

        private string? undo_text;
        private EditKind last_edit = EditKind.None;
        private bool suppress_undo_capture;

        // Snapshots the pre-edit text at an operation boundary. Same-kind edits in a row keep the
        // snapshot they started with, which is what groups a typing run into one undo step.
        private void CaptureUndo (EditKind kind)
        {
            if (suppress_undo_capture)
                return;

            if (last_edit != kind)
                undo_text = text;

            last_edit = kind;
        }

        /// <summary>Gets whether <see cref="Undo"/> has an edit to reverse.</summary>
        public bool CanUndo => undo_text is not null;

        /// <summary>Discards the undo buffer.</summary>
        public void ClearUndo ()
        {
            undo_text = null;
            last_edit = EditKind.None;
        }

        /// <summary>
        /// Reverses the last edit. Calling it again reapplies that edit, matching the toggle behaviour
        /// of the Win32 edit control's single-level buffer.
        /// </summary>
        public bool Undo ()
        {
            if (undo_text is null)
                return false;

            (text, undo_text) = (undo_text, text);
            cached_text_block = null;

            // The next edit starts a new group, and the caret may be past the restored end of text.
            last_edit = EditKind.None;
            SetCursorToCharIndex (Math.Min (cursor_index, text.Length));
            Invalidate ();

            return true;
        }

        public string DisplayText => text.Length == 0 ? placeholder :
                                     password_char.HasValue ? new string (password_char.Value, text.Length) :
                                     text;

        public bool Enabled {
            get => enabled;
            set {
                if (enabled != value) {
                    enabled = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public SKTypeface Font {
            get => font;
            set {
                if (font != value) {
                    font = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public HitTestResult GetCharIndexFromPosition (int x, int y)
        {
            var hit = GetTextBlock ().HitTest (x, y);

            return hit;
        }

        public TextSelection GetTextSelection () => new TextSelection (selection_start, selection_end, selection_color);

        public TextBlock GetTextBlock ()
        {
            if (cached_text_block != null)
                return cached_text_block;

            // A single line normally lays out in an unbounded width, so long text scrolls sideways
            // instead of wrapping. Centre/right alignment needs a real right edge to measure from --
            // aligned inside int.MaxValue every glyph lands off the end of the world and the control
            // paints blank -- so bound it to the visible width. MaxLines is 1 here, so bounding the
            // width cannot introduce wrapping.
            // Unbounded width is what lets long lines scroll sideways instead of wrapping. Centre and
            // right alignment need a real right edge to measure from -- aligned inside int.MaxValue
            // every glyph lands off the end of the world and the control paints blank -- so those stay
            // bounded to the visible width even when wrapping is off. For a multiline box that means
            // non-left-aligned text still wraps with WordWrap = false; left-aligned, which is what a
            // log view or a code view uses, does not (TXT-11).
            var unbounded = alignment == TextAlignment.Left ? TextMeasurer.MaxSize
                          : new Size (Math.Max (width, 1), int.MaxValue);
            var max_size = multiline && !textbox.WordWrap ? unbounded
                         : multiline ? new Size (width, int.MaxValue)
                         : unbounded;
            var color = !Enabled ? Theme.ForegroundDisabledColor :
                        Text.HasValue () ? textbox.GetEffectiveForegroundColor () :
                                placeholder_font_color;

            if (textbox.Colorizer is { } colorizer && DisplayText.HasValue ())
                return cached_text_block = BuildColorizedTextBlock (colorizer, max_size, color);

            return cached_text_block = TextMeasurer.CreateTextBlock (DisplayText, font, textbox.CurrentFontSize, max_size, alignment, color, MaxLines);
        }

        // Builds a TextBlock from multiple styled runs using the attached Colorizer, instead of
        // TextMeasurer's single-style cache (per-instance colorizer output isn't safe to share
        // across controls/keys the way the plain-text cache is). Spans outside [0, text.Length)
        // or that overlap a previous span are skipped defensively -- a buggy colorizer should
        // degrade to partially-colored text, not throw during paint.
        private TextBlock BuildColorizedTextBlock (System.Func<string, System.Collections.Generic.IEnumerable<TextSpanStyle>> colorizer, Size max_size, SKColor defaultColor)
        {
            var text = DisplayText;
            var tb = new TextBlock {
                MaxWidth = max_size.Width,
                MaxHeight = max_size.Height == int.MaxValue ? (int?)null : max_size.Height,
                Alignment = alignment,
                MaxLines = MaxLines,
            };

            var defaultStyle = new Style { FontFamily = font.FamilyName, FontSize = textbox.CurrentFontSize, TextColor = defaultColor, FontWeight = font.FontWeight };
            var next = 0;

            foreach (var span in colorizer (text)) {
                if (span.Start < next || span.Length <= 0 || span.Start + span.Length > text.Length)
                    continue;

                if (span.Start > next)
                    tb.AddText (text.AsSpan (next, span.Start - next), defaultStyle);

                var spanStyle = new Style {
                    FontFamily = font.FamilyName,
                    FontSize = textbox.CurrentFontSize,
                    TextColor = span.Color,
                    FontWeight = span.Bold ? (int)SkiaSharp.SKFontStyleWeight.Bold : font.FontWeight,
                    Underline = span.Underline ? UnderlineStyle.Solid : UnderlineStyle.None,
                    // Italic and a run background are what RichTextBox.SelectionItalic and
                    // SelectionBackColor need to reach the paint (TXT-17); a transparent BackColor is
                    // the "unset" value and paints nothing.
                    FontItalic = span.Italic,
                    BackgroundColor = span.BackColor,
                };
                tb.AddText (text.AsSpan (span.Start, span.Length), spanStyle);
                next = span.Start + span.Length;
            }

            if (next < text.Length)
                tb.AddText (text.AsSpan (next), defaultStyle);

            return tb;
        }

        /// <summary>
        /// Discards the cached text layout so it is rebuilt on the next paint. The foreground colour is
        /// baked into the cached <see cref="TextBlock"/>, so this must be called when the colour changes
        /// out-of-band (e.g. a theme change) for the new colour to take effect without a text edit.
        /// </summary>
        internal void InvalidateTextBlock () => cached_text_block = null;

        /// <summary>
        /// Replaces <c>[start, start + length)</c> with <paramref name="value"/> as ONE document edit,
        /// leaving the caret after the inserted text.
        /// </summary>
        /// <remarks>
        /// The primitive every text-mutating verb should use instead of assigning <see cref="Text"/>.
        /// That setter is DEFINED to reset the caret to 0, clear the undo buffer and drop Modified,
        /// which is right for a programmatic assignment and wrong for an append or a replace: routing
        /// AppendText through it made a log window scroll to its oldest line on every append, and a
        /// following ScrollToCaret went to the top as well, because the caret was at 0 (TXT-02, P0).
        /// <para>
        /// <paramref name="ignoreLimits"/> is EM_REPLACESEL's behaviour, which upstream's AppendText
        /// brackets with EM_LIMITTEXT 0: an append is not user input, so neither ReadOnly nor MaxLength
        /// applies to it.
        /// </para>
        /// </remarks>
        internal bool ReplaceRange (int start, int length, string value, bool ignoreLimits = false, bool captureUndo = true)
        {
            if (read_only && !ignoreLimits)
                return false;

            start = MathCompat.Clamp (start, 0, text.Length);
            length = MathCompat.Clamp (length, 0, text.Length - start);
            value = ApplyCasing (StripInvalidCharacters (value ?? string.Empty));

            if (!ignoreLimits && max_length > 0) {
                // The replaced run frees room, so the limit applies to what the text will BE.
                var room = max_length - (text.Length - length);

                if (room <= 0)
                    return false;

                if (value.Length > room)
                    value = value.Substring (0, room);
            }

            if (length == 0 && value.Length == 0)
                return false;

            if (captureUndo)
                CaptureUndo (EditKind.Insert);

            // One undo step for the whole replacement, as Win32 reverses a replace-selection in one.
            suppress_undo_capture = true;

            try {
                text = text.Remove (start, length).Insert (start, value);
            } finally {
                suppress_undo_capture = false;
            }

            cached_text_block = null;

            // The caret ends AFTER the new text and the selection collapses -- EM_REPLACESEL again.
            Deselect ();
            SetCursorToCharIndex (start + value.Length);

            // Notifies exactly once, and only because the content changed; see Invalidate.
            Invalidate ();

            return true;
        }

        public bool InsertText (string str)
        {
            if (read_only)
                return false;

            // One undo step for the whole operation: replacing a selection is a delete plus an insert
            // and Win32 reverses it in a single Undo, so the RemoveText inside DeleteSelection must not
            // open a step of its own.
            CaptureUndo (EditKind.Insert);

            // Delete any currently selected text
            suppress_undo_capture = true;
            try {
                DeleteSelection ();
            } finally {
                suppress_undo_capture = false;
            }

            str = StripInvalidCharacters (str);

            // MaxLength limits USER INPUT and does not affect text already present -- upstream's
            // EM_LIMITTEXT says so explicitly -- so input at or past the limit is simply rejected.
            // Computing the substring length as `max_length - text.Length` went NEGATIVE the moment the
            // existing text was longer than the limit, and threw ArgumentOutOfRangeException out of
            // OnKeyPress: `Text = <value from the database>` followed by `MaxLength = 10` (or the
            // designer setting the limit before binding fills the box -- the normal order) crashed the
            // application on the next keystroke (TXT-05).
            if (max_length > 0) {
                var room = max_length - text.Length;

                if (room <= 0)
                    return false;

                if (str.Length > room)
                    str = str.Substring (0, room);
            }

            // TXT-12: upstream applies ES_UPPERCASE/ES_LOWERCASE in the edit control itself, so typed,
            // pasted and programmatically assigned text is all converted.
            str = ApplyCasing (str);

            text = text.Insert (cursor_index, str);
            cached_text_block = null;

            // TODO: Need to properly handle code points
            SetCursorToCharIndex (cursor_index + str.Length);

            // The two low-level mutators (this and RemoveText) are what make the "every text mutation
            // funnels through Invalidate()" contract above true. Neither used to call it, so typing,
            // Enter and Backspace all changed the text without ever raising TextChanged -- only a
            // programmatic `Text = ...` did. A migrated editor's dirty flag is set from that event, so
            // it stayed false no matter what was typed and the save-on-close prompt never appeared.
            Invalidate ();

            return true;
        }

        // Tracks the text last reported to the owner so a content change raises TextChanged exactly once,
        // regardless of which mutation site produced it. Every text mutation funnels through Invalidate();
        // caret/selection/scroll invalidates leave `text` unchanged and so raise nothing.
        private string _lastNotifiedText = string.Empty;

        public void Invalidate ()
        {
            if (text != _lastNotifiedText) {
                _lastNotifiedText = text;
                // Raise the WinForms TextChanged event on the owning control (matches WinForms, which fires
                // TextChanged on every content change -- each keystroke and each programmatic Text set).
                textbox.OnDocumentTextChanged ();
            }

            textbox.Invalidate ();
        }

        public bool IsMultiline {
            get => multiline;
            set {
                if (multiline != value) {
                    multiline = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public bool IsTextSelected => selection_start >= 0 && selection_end >= 0 && SelectionLength != 0;

        // 0 means "no limit", the WinForms convention. Stored AS GIVEN, because a caller that sets
        // int.MaxValue has to read int.MaxValue back: representing "unlimited" as int.MaxValue made the
        // two indistinguishable, so `MaxLength = int.MaxValue` reported 0 -- "no limit" -- instead of
        // the value that was set. Surfaced by ComboBox.MaxLength forwarding here (W5.10).
        public int MaxLength {
            get => max_length;
            set => max_length = value < 0 ? 0 : value;
        }

        private int? MaxLines => multiline ? (int?)null : 1;

        public bool MoveCursor (ArrowDirection direction, bool select, bool wholeWord, bool end)
        {
            if (!select)
                Deselect ();

            // Nothing to move through, and the block the movement would be derived from is the
            // placeholder rather than the content (TXT-06).
            if (text.Length == 0)
                return false;

            var new_index = -1;
            var block = GetTextBlock ();
            var current_caret = block.GetCaretInfo (new CaretPosition (cursor_index));

            switch (direction) {
                case ArrowDirection.Left:

                    // Ctrl-Home - Go to the beginning of the document
                    if (end && wholeWord)
                        new_index = block.CaretIndicies[0];
                    // Home - Go to the beginning of the current line
                    else if (end)
                        new_index = block.HitTest (0, current_caret.CaretRectangle.MidY).ClosestCodePointIndex;
                    // Ctrl-Left - Go left one word
                    else if (wholeWord)
                        new_index = TextMeasurer.FindNextSeparator (text, cursor_index, false);
                    // Left - Go left one character
                    else
                        new_index = block.CaretIndicies.ElementAt (Math.Max (cursor_index - 1, 0));

                    break;

                case ArrowDirection.Up:

                    // Multiline - Go up one line
                    if (multiline)
                        new_index = GetCharIndexFromPosition ((int)current_caret.CaretXCoord, (int)current_caret.CaretRectangle.MidY - textbox.CurrentFontSize).ClosestCodePointIndex;
                    // Single line - Go left one character
                    else
                        new_index = block.CaretIndicies.ElementAt (Math.Max (cursor_index - 1, 0));

                    break;

                case ArrowDirection.Right:

                    // Ctrl-End - Go to the end of the document
                    if (end && wholeWord)
                        new_index = block.CaretIndicies.Last ();
                    // End - Go to the end of the current line
                    else if (end)
                        new_index = block.HitTest (int.MaxValue, current_caret.CaretRectangle.MidY).ClosestCodePointIndex;
                    // Ctrl-Right - Go right one word
                    else if (wholeWord)
                        new_index = TextMeasurer.FindNextSeparator (text, cursor_index, true);
                    // Right - Go right one character
                    else
                        new_index = block.CaretIndicies.ElementAt (Math.Min (cursor_index + 1, block.CaretIndicies.Count - 1));

                    break;

                case ArrowDirection.Down:

                    // Multiline - Go down one line
                    if (multiline)
                        new_index = GetCharIndexFromPosition ((int)current_caret.CaretXCoord, (int)current_caret.CaretRectangle.MidY + textbox.CurrentFontSize).ClosestCodePointIndex;
                    // Single line - Go left one character
                    else
                        new_index = block.CaretIndicies.ElementAt (Math.Min (cursor_index + 1, block.CaretIndicies.Count - 1));

                    break;
            }

            if (new_index != -1 && new_index != cursor_index) {
                var prev_index = cursor_index;
                SetCursorToCharIndex (new_index);

                if (!select || CursorIndex == SelectionStart) {
                    SelectionStart = -1;
                    SelectionEnd = -1;
                } else {
                    SelectionStart = (SelectionStart < 0 ? prev_index : SelectionStart);
                    SelectionEnd = new_index;
                }

                return true;
            }

            return false;
        }

        public char? PasswordCharacter {
            get => password_char;
            set {
                if (password_char != value) {
                    password_char = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public string Placeholder {
            get => placeholder;
            set {
                if (placeholder != value) {
                    placeholder = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public SKColor PlaceholderFontColor {
            get => placeholder_font_color;
            set {
                if (placeholder_font_color != value) {
                    placeholder_font_color = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }

        public bool ReadOnly {
            get => read_only;
            set {
                if (read_only != value) {
                    read_only = value;
                    Invalidate ();
                }
            }
        }

        private void RemoveText (int start, int length)
        {
            CaptureUndo (EditKind.Remove);

            text = text.Remove (start, length);
            cached_text_block = null;

            Invalidate ();
        }

        public void Reset () => cached_text_block = null;

        public void SelectAll ()
        {
            selection_start = 0;
            selection_end = text.Length;

            Invalidate ();
        }

        public string SelectedText => IsTextSelected ? text.Substring (Math.Min (selection_start, selection_end), SelectionLength) : string.Empty;

        public SKColor SelectionColor {
            get => selection_color;
            set {
                if (selection_color != value) {
                    selection_color = value;
                    Invalidate ();
                }
            }
        }

        public int SelectionEnd {
            get => selection_end;
            set {
                if (selection_end != value) {
                    selection_end = value;
                    Invalidate ();
                }
            }
        }

        public int SelectionLength => Math.Abs (selection_end - selection_start);

        public int SelectionStart {
            get => selection_start;
            set {
                if (selection_start != value) {
                    selection_start = value;
                    Invalidate ();
                }
            }
        }

        public bool SetCursorToCharIndex (int index)
        {
            // Clamped to the REAL text. The laid-out block is the placeholder while the text is empty
            // (see DisplayText), so Right/End/Down in an empty box with PlaceholderText = "Search"
            // moved the caret to index 1..6 -- inside a string that is not the content -- and the next
            // insert did text.Insert (6, "a") on an empty string and threw (TXT-06).
            index = MathCompat.Clamp (index, 0, text.Length);

            if (cursor_index == index)
                return false;

            cursor_index = index;

            return true;
        }

        // TXT-12. The culture is the thread's, as upstream: ES_UPPERCASE is a Win32 edit-control flag
        // and CharUpper follows the current locale, so a Turkish user's dotless-i behaves as it does in
        // WinForms rather than invariantly.
        internal string ApplyCasing (string value)
        {
            if (value.Length == 0)
                return value;

            return textbox.CharacterCasing switch {
                CharacterCasing.Upper => value.ToUpper (System.Globalization.CultureInfo.CurrentCulture),
                CharacterCasing.Lower => value.ToLower (System.Globalization.CultureInfo.CurrentCulture),
                _ => value,
            };
        }

        private string StripInvalidCharacters (string text)
        {
            if (multiline)
                return text;

            foreach (var c in invalid_singleline_characters)
                text = text.Replace (c, string.Empty);

            return text;
        }

        public string Text {
            get => text;
            set {
                // Backstop for the same WinForms coercion TextBox.Text applies: DisplayText and
                // StripInvalidCharacters both dereference this field, so it must never hold null.
                value ??= string.Empty;

                // Programmatic assignment is cased too: upstream's flag is on the edit control, so it
                // converts WM_SETTEXT the same as typing. A code field marked Upper therefore reads
                // back upper case whether the user typed it or the binding filled it (TXT-12).
                value = ApplyCasing (value);

                if (text != value) {
                    // WM_SETTEXT resets the Win32 edit control's undo buffer, so a programmatic
                    // assignment discards the step rather than becoming one.
                    ClearUndo ();

                    text = value;
                    cached_text_block = null;

                    // If the Text property is changed, we need to reset the cursor to the top
                    SetCursorToCharIndex (0);
                    Invalidate ();
                }
            }
        }

        public int Width {
            get => width;
            set {
                if (width != value) {
                    width = value;
                    cached_text_block = null;
                    Invalidate ();
                }
            }
        }
    }
}
