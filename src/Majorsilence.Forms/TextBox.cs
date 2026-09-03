using System.Drawing;
using Majorsilence.Forms.Renderers;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TextBox control.
    /// </summary>
    public partial class TextBox : TextBoxBase
    {
        internal readonly TextBoxDocument document;

        private System.Func<string, System.Collections.Generic.IEnumerable<TextSpanStyle>>? colorizer;

        /// <summary>
        /// Gets or sets a function that computes syntax-highlighting spans for this control's
        /// current text (called with <see cref="Text"/>, returning non-overlapping, left-to-right
        /// <see cref="TextSpanStyle"/> spans). When set, painting uses these spans instead of a
        /// single uniform foreground color; gaps between spans use the normal foreground color.
        /// Intended for code-editor-style subclasses (e.g. a Scintilla-compatible shim) rather
        /// than typical single-style text entry.
        /// </summary>
        public System.Func<string, System.Collections.Generic.IEnumerable<TextSpanStyle>>? Colorizer {
            get => colorizer;
            set {
                colorizer = value;
                document.InvalidateTextBlock ();
                Invalidate ();
            }
        }

        private bool is_highlighting;
        private int selection_anchor = -1;
        private int scroll_x;
        private int scroll_y;

        /// <summary>
        /// Initializes a new instance of the TextBox class.
        /// </summary>
        public TextBox ()
        {
            Cursor = Cursors.IBeam;

            document = new TextBoxDocument (this);

            VerticalScrollBar.Enabled = false;
            VerticalScrollBar.ValueChanged += (o, e) => DoScroll (0, (o as VerticalScrollBar)!.Value - scroll_y);
        }

        /// <inheritdoc/>
        protected internal override void OnThemeChanged (EventArgs e)
        {
            // The text layout caches its foreground colour; drop it so the new theme colour is applied on
            // the next paint instead of only after a focus/edit rebuilds it.
            document.InvalidateTextBlock ();
            base.OnThemeChanged (e);
        }

        /// <summary>
        /// Copies the selected text of the TextBox to the clipboard.
        /// </summary>
        public override void Copy ()
        {
            if (!document.IsTextSelected)
                return;

            var text = document.SelectedText;
            // Synchronous, on the current (UI) thread. Do NOT offload to a pool thread and block --
            // the clipboard backend marshals to the UI thread, so blocking it here deadlocks
            // (found: Ctrl+C froze the app). Clipboard.SetText is UI-thread-safe (see the backend).
            Majorsilence.Forms.Clipboard.SetText (text);
        }

        // The scaled height of the current font.
        internal int CurrentFontSize => LogicalToDeviceUnits (GetEffectiveFontSize ());

        /// <summary>
        /// Gets the height a single-line TextBox should be to exactly fit one line of text at the
        /// current font, plus padding -- the same role System.Windows.Forms.TextBox.PreferredHeight
        /// plays (used to auto-size a textbox instead of hardcoding a pixel height).
        /// </summary>
        public override int PreferredHeight
        {
            get
            {
                var lineHeight = (int)System.Math.Ceiling (TextMeasurer.MeasureText ("Wg", this).Height);
                return lineHeight + (Padding.Top + Padding.Bottom) + 4; // 4px matches the default border/inset
            }
        }

        /// <summary>
        /// Copies the selected text of the TextBox to the clipboard and removes it from the TextBox.
        /// </summary>
        public override void Cut ()
        {
            if (!document.IsTextSelected)
                return;

            var text = document.SelectedText;
            Majorsilence.Forms.Clipboard.SetText (text);   // sync + UI-thread-safe; see Copy()

            document.DeleteSelection ();
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (1, 0, 0, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 25);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        // Scrolls the TextBox by the specified amounts.
        private void DoScroll (int x, int y)
        {
            scroll_x += x;
            scroll_y += y;

            if (y != 0)
                VScroll?.Invoke (this, EventArgs.Empty);
            if (x != 0)
                HScroll?.Invoke (this, EventArgs.Empty);

            Invalidate ();
        }

        /// <summary>
        /// Raised when the vertical scroll position changes, matching System.Windows.Forms.
        /// TextBoxBase.VScroll (a real event there, distinct from Control.VScroll's unrelated
        /// bool property -- `new` shadows that property for this type and its subclasses).
        /// </summary>
        public new event EventHandler? VScroll;

        /// <summary>Raised when the horizontal scroll position changes. See VScroll.</summary>
        public new event EventHandler? HScroll;

        // Gets the index of the character at the specified location.
        /// <inheritdoc/>
        /// <remarks>Hit-tests the laid-out text through the document, so it answers the real
        /// character under the point rather than the base's placeholder.</remarks>
        public override int GetCharIndexFromPosition (Point pt)
        {
            if (!document.Text.HasValue ())
                return 0;

            return document.GetCharIndexFromPosition (pt.X - TextOrigin.X, pt.Y - TextOrigin.Y).ClosestCodePointIndex;
        }

        /// <inheritdoc/>
        /// <remarks>Reads the caret rectangle out of the laid-out text, so it accounts for the
        /// current font, wrapping and scroll offset.</remarks>
        public override Point GetPositionFromCharIndex (int index)
        {
            var caret = TextMeasurer.GetCursorLocation (document.GetTextBlock (), TextOrigin, index, CurrentFontSize);
            return caret.IsEmpty ? Point.Empty : caret.Location;
        }

        /// <summary>
        /// Gets the index of the character at the specified control-relative location. Used by
        /// <see cref="Majorsilence.Forms.SpellCheck.TextBoxSpellCheck"/> to determine which word was
        /// right-clicked.
        /// </summary>
        internal int GetSpellCheckCharIndexFromPosition (Point location) => GetCharIndexFromPosition (location);

        // Handles key down events.
        private bool HandleKeyDown (KeyEventArgs e)
        {
            var need_refresh = false;

            try {
                switch (e.KeyData & Keys.KeyCode) {
                    case Keys.Left:
                        need_refresh = document.MoveCursor (ArrowDirection.Left, e.Shift, e.Control, false);
                        return true;
                    case Keys.Right:
                        need_refresh = document.MoveCursor (ArrowDirection.Right, e.Shift, e.Control, false);
                        return true;
                    case Keys.Home:
                        need_refresh = document.MoveCursor (ArrowDirection.Left, e.Shift, e.Control, true);
                        return true;
                    case Keys.End:
                        need_refresh = document.MoveCursor (ArrowDirection.Right, e.Shift, e.Control, true);
                        return true;
                    case Keys.Up:
                        need_refresh = document.MoveCursor (ArrowDirection.Up, e.Shift, e.Control, false);
                        return true;
                    case Keys.Down:
                        need_refresh = document.MoveCursor (ArrowDirection.Down, e.Shift, e.Control, false);
                        return true;
                    case Keys.Delete:
                        need_refresh = DeleteAtCaret (forward: true, wholeWord: e.Control);
                        return true;
                    case Keys.Back:
                        need_refresh = DeleteAtCaret (forward: false, wholeWord: e.Control);
                        return true;
                    case Keys.Return:
                        // Enter has to insert the newline from the key-down path. OnKeyPress has a
                        // KeyChar == 13 branch for it, but that only ever fires on a backend that
                        // reports Enter as text input, and Avalonia -- like most -- does not: it
                        // delivers Enter as a key event only, so on a real window that branch is dead
                        // and a multiline box silently refused to take a new line.
                        if (!Multiline)
                            return false;

                        need_refresh = document.InsertText ("\n");
                        return true;
                    case Keys.C:
                        if (e.Control)
                            Copy ();

                        return e.Control;
                    case Keys.X:
                        if (e.Control)
                            Cut ();

                        return e.Control;
                    case Keys.V:
                        if (e.Control)
                            Paste ();

                        return e.Control;
                    case Keys.A:
                        if (e.Control)
                            document.SelectAll ();

                        return e.Control;

                }
            } finally {
                if (need_refresh)
                    ScrollToCaret ();
            }

            return false;
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum length of text the TextBox can hold.
        /// </summary>
        public override int MaxLength {
            get => document.MaxLength;
            set => document.MaxLength = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if the TextBox supports multiple lines of text.
        /// Only the WinForms spelling exists: VB is case-insensitive, so a MultiLine/Multiline
        /// pair makes the member unusable from VB.
        /// </summary>
        public override bool Multiline {
            get => document.IsMultiline;
            set {
                if (document.IsMultiline != value) {

                    if (Padding == DefaultPadding)
                        Padding = new Padding (value ? 4 : 1, 0, 0, 0);

                    document.IsMultiline = value;
                    // The state lives in the document rather than the base's field, so this override
                    // owns raising the notification the base would otherwise have raised.
                    OnMultilineChanged (EventArgs.Empty);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            document.Deselect ();
        }

        /// <inheritdoc/>
        protected override void OnEnabledChanged (EventArgs e)
        {
            base.OnEnabledChanged (e);

            document.Enabled = Enabled;
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            e.Handled = HandleKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyPress (KeyPressEventArgs e)
        {
            base.OnKeyPress (e);

            // Enter = 13
            if (e.KeyChar == 13 && Multiline) {
                if (document.InsertText ("\n"))
                    ScrollToCaret ();
            }

            // Printable characters (except backspace)
            if (e.KeyChar >= 32 && e.KeyChar != 127) {
                if (InsertTypedCharacter (e))
                    ScrollToCaret ();
            }
        }

        /// <summary>
        /// Puts a typed character into the document, and reports whether anything changed.
        /// </summary>
        /// <remarks>The one place typed text reaches the document, so a derived box can filter it
        /// instead of receiving it after the fact: <see cref="MaskedTextBox"/> routes the character
        /// through its <c>MaskedTextProvider</c> here, which it cannot do from
        /// <see cref="OnKeyPress"/> (that would have to skip this class's own insert).</remarks>
        protected virtual bool InsertTypedCharacter (KeyPressEventArgs e) => document.InsertText (e.Text);

        /// <summary>
        /// Deletes at the caret -- forwards for Delete, backwards for Backspace -- and reports whether
        /// anything changed.
        /// </summary>
        /// <inheritdoc cref="InsertTypedCharacter" path="/remarks"/>
        protected virtual bool DeleteAtCaret (bool forward, bool wholeWord) => document.DeleteText (forward, wholeWord);

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (e.Button != MouseButtons.Left)
                return;

            SetCursorToCharIndex (GetCharIndexFromPosition (e.Location));

            is_highlighting = true;
            selection_anchor = document.CursorIndex;

            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (is_highlighting) {
                SetCursorToCharIndex (GetCharIndexFromPosition (e.Location));

                if (document.CursorIndex == selection_anchor) {
                    document.SelectionStart = -1;
                    document.SelectionEnd = -1;
                } else {
                    document.SelectionStart = selection_anchor;
                    document.SelectionEnd = document.CursorIndex;
                }

                Invalidate ();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (e.Button != MouseButtons.Left)
                return;

            SetCursorToCharIndex (GetCharIndexFromPosition (e.Location));

            is_highlighting = false;

            if (document.CursorIndex == selection_anchor) {
                document.SelectionStart = -1;
                document.SelectionEnd = -1;
            } else {
                document.SelectionStart = selection_anchor;
                document.SelectionEnd = document.CursorIndex;
            }

            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <inheritdoc/>
        protected override void OnParentChanged (EventArgs e)
        {
            base.OnParentChanged (e);

            // Changing parent may mean changing scaling, which
            // means we need to recalculate the document.
            document.Reset ();
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            document.Width = PaddedClientRectangle.Width;
        }

        /// <summary>
        /// Gets or sets a character to display instead of the actual text.
        /// </summary>
        public char? PasswordCharacter {
            get => document.PasswordCharacter;
            set => document.PasswordCharacter = value;
        }

        /// <summary>
        /// Gets or sets the password character (WinForms compatibility alias for PasswordCharacter).
        /// </summary>
        public char PasswordChar {
            get => document.PasswordCharacter ?? '\0';
            set => document.PasswordCharacter = value == '\0' ? null : value;
        }

        /// <summary>
        /// Gets or sets which scroll bars appear (informational; Majorsilence.Forms shows scroll bars automatically).
        /// </summary>
        public new ScrollBars ScrollBars { get; set; }

        /// <summary>
        /// Inserts any text on the clipboard into the TextBox.
        /// </summary>
        public override void Paste ()
        {
            if (document.ReadOnly)
                return;

            var text = Majorsilence.Forms.Clipboard.GetText ();   // sync + UI-thread-safe; see Copy()

            if (!string.IsNullOrEmpty (text) && document.InsertText (text))
                ScrollToCaret ();
        }

        /// <summary>
        /// Gets or sets text to display if the TextBox contains no text.
        /// </summary>
        public string Placeholder {
            get => document.Placeholder;
            set => document.Placeholder = value;
        }

        /// <summary>
        /// Gets or sets text to display if the TextBox contains no text.
        /// WinForms-compatible alias for <see cref="Placeholder"/>; a null value is coerced to an empty string.
        /// </summary>
        public string PlaceholderText {
            get => document.Placeholder;
            set => document.Placeholder = value ?? string.Empty;
        }

        /// <summary>Gets the number of characters of text currently in the TextBox.</summary>
        public override int TextLength => Text.Length;

        /// <summary>
        /// Gets or sets a value indicating if the text can be edited.
        /// </summary>
        public override bool ReadOnly {
            get => document.ReadOnly;
            set {
                if (document.ReadOnly == value)
                    return;

                document.ReadOnly = value;
                OnReadOnlyChanged (EventArgs.Empty);   // see Multiline
            }
        }

        /// <summary>
        /// Scrolls the TextBox so that the caret is visible.
        /// </summary>
        public override void ScrollToCaret ()
        {
            var caret = TextMeasurer.GetCursorLocation (document.GetTextBlock (), TextOrigin, document.CursorIndex, CurrentFontSize);

            if (caret.IsEmpty)
                return;

            caret.Offset (scroll_x, scroll_y);

            var dx = 0;
            var dy = 0;
            var viewport = TextViewport;

            if (caret.Top < viewport.Top)
                dy = caret.Top - viewport.Top - 1;
            else if (caret.Bottom > viewport.Bottom)
                dy = caret.Bottom - viewport.Bottom + 3;

            if (caret.Left < viewport.Left)
                dx = caret.Left - viewport.Left - 1;
            else if (caret.Right > viewport.Right)
                dx = caret.Right - viewport.Right + 3;

            DoScroll (dx, dy);
        }

        /// <summary>
        /// Gets or sets a value indicating the end of the TextBox's selected text.
        /// </summary>
        public int SelectionEnd {
            get => document.SelectionEnd;
            set => document.SelectionEnd = value;
        }

        /// <summary>
        /// Gets or sets a value indicating the start of the TextBox's selected text.
        /// </summary>
        /// <remarks>
        /// The document tracks a selection anchor (−1 when nothing is selected) separately from the
        /// caret, but WinForms exposes one number for both: with no selection <c>SelectionStart</c> is
        /// the caret, and with a selection it is the lower of its two ends. Returning the raw anchor
        /// handed callers −1 for the common case of a caret and no selection, which reads as a
        /// character index and quietly corrupts any arithmetic done on it — a status bar computing
        /// line/column from it lands outside the text and gets nothing back.
        /// </remarks>
        public override int SelectionStart {
            get {
                if (document.SelectionStart < 0 || document.SelectionEnd < 0)
                    return document.CursorIndex;

                return Math.Min (document.SelectionStart, document.SelectionEnd);
            }
            set {
                // Moving the caret drops the selection, as in WinForms — the `SelectionStart = x;
                // SelectionLength = n;` pair rebuilds it from the new caret via the setter below.
                document.SelectionStart = -1;
                document.SelectionEnd = -1;
                document.SetCursorToCharIndex (MathCompat.Clamp (value, 0, TextLength));
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the number of characters selected.
        /// </summary>
        public override int SelectionLength {
            get {
                if (document.SelectionStart < 0 || document.SelectionEnd < 0)
                    return 0;

                return Math.Abs (document.SelectionEnd - document.SelectionStart);
            }
            set {
                // Anchor on SelectionStart's WinForms meaning (the caret when nothing is selected), so
                // selecting from a caret position works; a non-positive length clears the selection.
                var start = SelectionStart;

                if (value <= 0) {
                    document.SelectionStart = -1;
                    document.SelectionEnd = -1;
                } else {
                    document.SelectionStart = start;
                    document.SelectionEnd = MathCompat.Clamp (start + value, 0, TextLength);
                }

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the currently selected text. Setting it replaces the current selection
        /// (or inserts at the caret if nothing is selected), matching
        /// System.Windows.Forms.TextBoxBase.SelectedText.
        /// </summary>
        public override string SelectedText {
            get => document.SelectedText;
            set => document.InsertText (value ?? string.Empty);
        }

        /// <summary>
        /// Selects all text in the TextBox.
        /// </summary>
        public override void SelectAll () => document.SelectAll ();

        /// <summary>Clears all text from the TextBox.</summary>
        public override void Clear () => Text = string.Empty;

        /// <summary>
        /// Appends text to the current text of the TextBox.
        /// </summary>
        public override void AppendText (string text)
        {
            if (string.IsNullOrEmpty (text))
                return;

            Text += text;
        }

        /// <summary>
        /// Gets or sets whether text wraps to the next line when the edge is reached.
        /// </summary>
        public override bool WordWrap { get; set; } = true;

        /// <summary>Gets or sets whether pressing Enter in a multiline TextBox creates a new line.</summary>
        /// <remarks>
        /// Consulted by <see cref="IsInputKey"/>, which is what gives it effect: with this set, Enter
        /// belongs to the text box and never reaches <see cref="Form.AcceptButton"/>. It was a
        /// stored-only property for as long as Enter was intercepted at the top of the input path,
        /// which meant every multiline box on a form with a default button submitted the form instead
        /// of adding a line.
        /// </remarks>
        public bool AcceptsReturn { get; set; }

        /// <summary>
        /// Claims Enter for the text box when it is multiline and <see cref="AcceptsReturn"/> is set.
        /// </summary>
        /// <remarks>Mirrors <c>TextBox.IsInputKey</c>; everything else defers to
        /// <see cref="TextBoxBase.IsInputKey"/>.</remarks>
        protected override bool IsInputKey (Keys keyData)
        {
            if (Multiline && (keyData & Keys.Alt) == Keys.None
                && (keyData & Keys.KeyCode) == Keys.Return)
                return AcceptsReturn;

            return base.IsInputKey (keyData);
        }

        /// <summary>Gets or sets the character casing applied to text. Stub in Majorsilence.Forms.</summary>
        public CharacterCasing CharacterCasing { get; set; } = CharacterCasing.Normal;

        /// <summary>Gets or sets the auto-complete mode. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteMode AutoCompleteMode { get; set; } = AutoCompleteMode.None;

        /// <summary>Gets or sets the auto-complete source. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

        /// <summary>Gets or sets a custom list of strings used for auto-complete. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = new AutoCompleteStringCollection ();

        /// <summary>Gets or sets whether the system's default password character is used. Stub in Majorsilence.Forms.</summary>
        public bool UseSystemPasswordChar {
            get => PasswordCharacter.HasValue;
            set { if (value && PasswordCharacter is null) PasswordCharacter = '*'; else if (!value) PasswordCharacter = null; }
        }

        // Select (int, int) is inherited from TextBoxBase, which is where WinForms declares it.

        /// <summary>Raised when the <see cref="TextAlign"/> property changes.</summary>
        public event EventHandler? TextAlignChanged;

        /// <summary>Raises the TextAlignChanged event.</summary>
        /// <remarks>
        /// WinForms declares this protected virtual on TextBoxBase, and control libraries that decorate
        /// a text box override it to re-lay-out their own chrome when alignment changes.
        /// </remarks>
        protected virtual void OnTextAlignChanged (EventArgs e) => TextAlignChanged?.Invoke (this, e);

        /// <summary>Gets or sets the lines of text in the TextBox.</summary>
        public override string[] Lines {
            // WinForms returns an empty array (not a single empty string) when there is no text.
            get => Text.Length == 0 ? Array.Empty<string> () : Text.Replace ("\r\n", "\n").Replace ("\r", "\n").Split ('\n');
            set => Text = value is null ? string.Empty : string.Join ("\n", value);
        }

        // Sets cursor to specified character index and scrolls TextBox to cursor.
        private void SetCursorToCharIndex (int index)
        {
            if (document.SetCursorToCharIndex (index))
                ScrollToCaret ();
        }

        private HorizontalAlignment text_align = HorizontalAlignment.Left;

        /// <summary>Gets or sets the horizontal alignment of text in the TextBox.</summary>
        /// <remarks>
        /// Applied to the document, so the caret and hit-testing follow the laid-out text rather than
        /// only the painted glyphs moving. A right-aligned display box -- a calculator readout, a
        /// currency field -- reads as broken without it, and the designer emits nothing else.
        /// </remarks>
        public HorizontalAlignment TextAlign {
            get => text_align;
            set {
                if (text_align == value)
                    return;

                text_align = value;
                document.Alignment = value switch {
                    HorizontalAlignment.Center => Topten.RichTextKit.TextAlignment.Center,
                    HorizontalAlignment.Right => Topten.RichTextKit.TextAlignment.Right,
                    _ => Topten.RichTextKit.TextAlignment.Left,
                };
                OnTextAlignChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override bool CanUndo => document.CanUndo;

        /// <inheritdoc/>
        public override void Undo ()
        {
            // Guarded by setting_text for the same reason the Text setter is: the document raises the
            // same change notification for an undo as for a user edit, and an undo is not a user edit.
            setting_text = true;
            try {
                document.Undo ();
            } finally {
                setting_text = false;
            }
        }

        /// <inheritdoc/>
        public override void ClearUndo () => document.ClearUndo ();

        /// <inheritdoc/>
        public override string Text {
            get => document.Text;
            set {
                // WinForms compat: Text is never null — a null assignment is coerced to empty. This
                // override bypasses the coercion in Control.Text, so it has to repeat it; without it
                // a null lands in the document and TextBoxDocument.DisplayText dereferences it.
                value ??= string.Empty;

                if (document.Text != value) {
                    setting_text = true;
                    try {
                        document.Text = value;
                    } finally {
                        setting_text = false;
                    }

                    // WinForms clears Modified when Text is assigned: the property means "edited since
                    // it was last set", so a programmatic assignment is the new baseline.
                    Modified = false;
                    ScrollToCaret ();
                }
            }
        }

        // True only while the Text setter is writing to the document, which is how Modified tells a
        // user edit apart from a programmatic assignment -- the document raises the same notification
        // for both.
        private bool setting_text;

        // Raised by TextBoxDocument whenever the text content actually changes (typing, paste, delete, or a
        // programmatic Text set). Bridges to Control.OnTextChanged so the WinForms TextChanged event fires --
        // the overridden Text setter above writes straight to the document and never runs the base setter.
        internal void OnDocumentTextChanged ()
        {
            if (!setting_text)
                Modified = true;

            OnTextChanged (EventArgs.Empty);
        }

        // Where the text starts, taking scrolling into account
        internal Point TextOrigin => new Point (PaddedClientRectangle.Location.X - scroll_x,
                                                PaddedClientRectangle.Location.Y - scroll_y + SingleLineVerticalOffset);

        // A single-line TextBox centres its text vertically -- what a Win32 EDIT without ES_MULTILINE
        // does, and what every WinForms layout assumes. Only visible on a box taller than its font,
        // which the designer produces whenever AutoSize is off: top-aligned text there floats against
        // the upper edge, and anything overlapping that edge (a label sharing the strip, a border)
        // clips the glyphs. Multiline keeps its text at the top, as WinForms does.
        private int SingleLineVerticalOffset {
            get {
                if (Multiline)
                    return 0;

                var slack = PaddedClientRectangle.Height - (int) document.GetTextBlock ().MeasuredHeight;
                return slack > 0 ? slack / 2 : 0;
            }
        }

        // The virtual bounds of what is currently shown to the user.
        private Rectangle TextViewport => new Rectangle (new Point (PaddedClientRectangle.Location.X + scroll_x, PaddedClientRectangle.Location.Y + scroll_y), PaddedClientRectangle.Size);

        // Enables and recalculates scrollbars as needed.
        internal void UpdateScrollBars (TextBlock block)
        {
            // TODO: Horizontal scrollbar not supported
            // Something about the document changed, so we need to update the scrollbars
            if ((int)block.MeasuredHeight - PaddedClientRectangle.Height > 0) {
                VerticalScrollBar.Enabled = true;
                VerticalScrollBar.Maximum = (int)block.MeasuredHeight - PaddedClientRectangle.Height;
                VerticalScrollBar.LargeChange = PaddedClientRectangle.Height;
                VerticalScrollBar.SmallChange = CurrentFontSize * 3;

                var new_value = Math.Min (scroll_y, VerticalScrollBar.Maximum);

                if (VerticalScrollBar.Value != new_value)
                    VerticalScrollBar.Value = new_value;
            } else {
                if (scroll_y > 0)
                    DoScroll (0, -scroll_y);

                VerticalScrollBar.Enabled = false;
            }
        }
    }
}
