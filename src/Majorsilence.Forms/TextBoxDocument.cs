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
        private int max_length = int.MaxValue;
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

            var max_size = multiline ? new Size (width, int.MaxValue) : TextMeasurer.MaxSize;
            var color = !Enabled ? Theme.ForegroundDisabledColor :
                        Text.HasValue () ? textbox.CurrentStyle.GetForegroundColor () :
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

        public bool InsertText (string str)
        {
            if (read_only)
                return false;

            // Delete any currently selected text
            DeleteSelection ();

            str = StripInvalidCharacters (str);

            if (text.Length + str.Length > max_length)
                str = str.Substring (0, max_length - text.Length);

            text = text.Insert (cursor_index, str);
            cached_text_block = null;

            // TODO: Need to properly handle code points
            SetCursorToCharIndex (cursor_index + str.Length);

            return true;
        }

        public void Invalidate ()
        {
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

        public int MaxLength {
            // 0 means "no limit" (WinForms convention), stored internally as int.MaxValue.
            get => max_length == int.MaxValue ? 0 : max_length;
            set => max_length = value <= 0 ? int.MaxValue : value;
        }

        private int? MaxLines => multiline ? (int?)null : 1;

        public bool MoveCursor (ArrowDirection direction, bool select, bool wholeWord, bool end)
        {
            if (!select)
                Deselect ();

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
            text = text.Remove (start, length);
            cached_text_block = null;
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
            if (cursor_index == index)
                return false;

            cursor_index = index;

            return true;
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
                if (text != value) {
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
