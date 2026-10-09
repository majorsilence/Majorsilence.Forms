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

            HorizontalScrollBar.Enabled = false;
            HorizontalScrollBar.ValueChanged += (o, e) => DoScroll ((o as HorizontalScrollBar)!.Value - scroll_x, 0);
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
            // TXT-08: a password box never gives up its text. Upstream's Copy is WM_COPY against an
            // ES_PASSWORD edit control, which ignores it (MaskedTextBox.WmCopy says so in as many words:
            // "cannot copy password to clipboard").
            if (!document.IsTextSelected || PasswordProtect)
                return;

            var text = document.SelectedText;
            // Synchronous, on the current (UI) thread. Do NOT offload to a pool thread and block --
            // the clipboard backend marshals to the UI thread, so blocking it here deadlocks
            // (found: Ctrl+C froze the app). Clipboard.SetText is UI-thread-safe (see the backend).
            Majorsilence.Forms.Clipboard.SetText (text);
        }

        // The scaled height of the current font.
        // Virtual so RichTextBox can apply its ZoomFactor here: every caret, selection, scroll-step
        // and measurement in this class reads it, so zooming in one place zooms all of them together.
        internal virtual int CurrentFontSize => LogicalToDeviceUnits (GetEffectiveFontSize ());

        /// <summary>
        /// Gets the height a single-line TextBox should be to exactly fit one line of text at the
        /// current font, plus padding -- the same role System.Windows.Forms.TextBox.PreferredHeight
        /// plays (used to auto-size a textbox instead of hardcoding a pixel height).
        /// </summary>
        public override int PreferredHeight
        {
            get
            {
                var lineHeight = TextMeasurer.LogicalLineHeight (this);
                return lineHeight + (Padding.Top + Padding.Bottom) + 4; // 4px matches the default border/inset
            }
        }

        /// <summary>
        /// Copies the selected text of the TextBox to the clipboard and removes it from the TextBox.
        /// </summary>
        public override void Cut ()
        {
            // TXT-08: and a cut that cannot copy does not delete either, as WM_CUT in password mode.
            if (!document.IsTextSelected || PasswordProtect)
                return;

            var text = document.SelectedText;
            Majorsilence.Forms.Clipboard.SetText (text);   // sync + UI-thread-safe; see Copy()

            document.DeleteSelection ();
        }

        // Upstream's TextBox.PasswordProtect: PasswordChar != '\0', which UseSystemPasswordChar also
        // satisfies, because the getter answers the character actually in use.
        internal bool PasswordProtect => document.PasswordCharacter.HasValue;

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

        /// <summary>
        /// The default focus (<c>:focus</c>) ControlStyle for all instances of TextBox (#366). No built-in theme gives it a look of its own -- it
        /// paints exactly like <see cref="DefaultStyle"/> until a CSS rule sets one, so a theme can draw a focus ring.
        /// </summary>
        public new static readonly ControlStyle DefaultStyleFocus = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override ControlStyle StyleFocus { get; } = new ControlStyle (DefaultStyleFocus);

        internal override bool UsesFocusStyle => true;

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
        /// <remarks>Takes a LOGICAL point, the space a <c>MouseEventArgs</c> location is in; the
        /// layout it hit-tests against is device, and the conversion happens here (RC-8).</remarks>
        public override int GetCharIndexFromPosition (Point pt)
        {
            if (!document.Text.HasValue ())
                return 0;

            var device = LogicalToDeviceUnits (pt);
            return document.GetCharIndexFromPosition (device.X - TextOrigin.X, device.Y - TextOrigin.Y).ClosestCodePointIndex;
        }

        /// <inheritdoc/>
        /// <remarks>Reads the caret rectangle out of the laid-out text, so it accounts for the
        /// current font, wrapping and scroll offset.</remarks>
        /// <remarks>
        /// Answers in LOGICAL units, the space a <c>MouseEventArgs</c> location and every other public
        /// member here works in (RC-8). The layout behind it is device, and the conversion happens at
        /// this boundary -- it used to answer device, so on a scaled display the caller got a point
        /// that was wrong by the display scale while every internal caller happened to want device.
        /// </remarks>
        public override Point GetPositionFromCharIndex (int index)
        {
            var caret = CaretRectangle (index);
            return caret.IsEmpty ? Point.Empty : DeviceToLogicalUnits (caret.Location);
        }

        // The caret's rectangle in device units. An empty box has no text to size it from, so it is sized from one line.
        internal Rectangle CaretRectangle (int index)
        {
            var block = document.GetTextBlock ();
            return TextMeasurer.GetCursorLocation (block, TextOrigin, index, CurrentFontSize, block.MeasuredHeight == 0 ? document.EmptyLineHeight : 0);
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
                    case Keys.PageUp:
                        need_refresh = MovePage (up: true, e.Shift);
                        return true;
                    case Keys.PageDown:
                        need_refresh = MovePage (up: false, e.Shift);
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
                    // TXT-22: upstream's ProcessCmdKey eats the whole shortcut list when
                    // ShortcutsEnabled is false, which is what a kiosk or exam-style field sets to keep
                    // text off the clipboard. These four acted regardless.
                    case Keys.C:
                        if (e.Control && ShortcutsEnabled)
                            Copy ();

                        return e.Control && ShortcutsEnabled;
                    case Keys.X:
                        if (e.Control && ShortcutsEnabled)
                            Cut ();

                        return e.Control && ShortcutsEnabled;
                    case Keys.V:
                        if (e.Control && ShortcutsEnabled)
                            Paste ();

                        return e.Control && ShortcutsEnabled;
                    case Keys.A:
                        if (e.Control && ShortcutsEnabled)
                            SelectAll ();

                        return e.Control && ShortcutsEnabled;
                    // TXT-13: the edit control's own Ctrl+Z (WM_UNDO) -- the undo buffer was only
                    // reachable from a menu. Upstream lists CtrlZ among the shortcuts ShortcutsEnabled
                    // eats, and a read-only edit control refuses the undo.
                    case Keys.Z:
                        if (e.Control && !e.Alt && ShortcutsEnabled && !ReadOnly) {
                            Undo ();
                            need_refresh = true;
                        }

                        return e.Control && ShortcutsEnabled;

                }
            } finally {
                if (need_refresh)
                    ScrollToCaret ();
            }

            return false;
        }

        // TXT-24: PageUp/PageDown move the caret a viewport's height in a multiline box, the native edit
        // control's paging (TextBoxBase.IsInputKey claims both keys so they arrive here). A single-line
        // box has nothing to page through.
        private bool MovePage (bool up, bool select)
        {
            if (!Multiline || document.Text.Length == 0)
                return false;

            var block = document.GetTextBlock ();
            var caret = block.GetCaretInfo (new CaretPosition (document.CursorIndex));
            var page = Math.Max (PaddedClientRectangle.Height, CurrentFontSize);
            var y = caret.CaretRectangle.MidY + (up ? -page : page);
            var index = y < 0 ? 0
                      : y > block.MeasuredHeight ? document.Text.Length
                      : block.HitTest (caret.CaretXCoord, y).ClosestCodePointIndex;

            return document.MoveCaretTo (index, select);
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
        /// <remarks>
        /// Does NOT clear the selection. It used to, and every focus move runs through here (see
        /// <c>ControlAdapter.SelectedControl</c>), so anything that acted on the box's selection after
        /// focus left it -- an Edit menu, a Find dialog, an "insert field" button beside the box, all of
        /// which take focus themselves -- read <c>SelectionLength == 0</c> and inserted at the wrong
        /// place or copied nothing (<c>TXT-07</c>). Upstream keeps the selection and only stops
        /// PAINTING it while unfocused, which is what <see cref="TextBoxBase.HideSelection"/> means and
        /// what the renderer now implements.
        /// </remarks>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            // Still repaint: with HideSelection set (the default) the highlight has to disappear even
            // though the selection itself survives.
            Invalidate ();
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

            var index = GetCharIndexFromPosition (e.Location);

            // TXT-24: the second press of a double-click selects the word under the pointer, which is
            // what the native edit control does on WM_LBUTTONDBLCLK. The release that follows must not
            // collapse it, so no drag starts.
            if (e.Clicks == 2 && document.Text.Length > 0) {
                is_highlighting = false;

                var (start, end) = WordAt (index);
                document.SetSelection (start, end);
                ScrollToCaret ();

                return;
            }

            // TXT-24: Shift+click extends the selection from its anchor (or from the caret) instead of
            // starting a new one. The modifiers carried by THIS press, as ListView reads them (LST-27).
            var extend = (e.Modifiers & Keys.Shift) == Keys.Shift;
            var anchor = !extend ? index
                       : document.IsTextSelected ? document.SelectionStart
                       : document.CursorIndex;

            is_highlighting = true;
            selection_anchor = anchor;

            document.SetSelection (anchor, index);
            ScrollToCaret ();
        }

        // The word -- or the run of spaces or punctuation -- that the character at index belongs to.
        // Same character classes as Ctrl+Backspace (upstream's ClientUtils.GetWordBoundaryStart).
        private (int Start, int End) WordAt (int index)
        {
            var text = document.Text;

            index = MathCompat.Clamp (index, 0, text.Length);

            // Past the last character, the word is the one the caret is touching.
            var probe = index == text.Length ? index - 1 : index;
            var is_word = TextBoxDocument.IsWordCharacter (text[probe]);
            var is_space = char.IsWhiteSpace (text[probe]);

            bool Same (char c) => TextBoxDocument.IsWordCharacter (c) == is_word
                                  && (is_word || char.IsWhiteSpace (c) == is_space);

            var start = probe;
            var end = probe + 1;

            while (start > 0 && Same (text[start - 1]))
                start--;

            while (end < text.Length && Same (text[end]))
                end++;

            return (start, end);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (is_highlighting) {
                document.SetSelection (selection_anchor, GetCharIndexFromPosition (e.Location));
                ScrollToCaret ();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            // A release with no press of ours behind it -- the second half of a double-click, or a drag
            // that started somewhere else -- leaves the selection alone.
            if (e.Button != MouseButtons.Left || !is_highlighting)
                return;

            is_highlighting = false;

            document.SetSelection (selection_anchor, GetCharIndexFromPosition (e.Location));
            ScrollToCaret ();
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

            document.Width = WrapWidth;
        }

        /// <summary>The width text wraps and aligns within.</summary>
        /// <remarks>
        /// Virtual so <see cref="RichTextBox.RightMargin"/> can narrow it: upstream's right margin is a
        /// wrapping width in pixels, independent of how wide the control is.
        /// </remarks>
        internal virtual int WrapWidth => PaddedClientRectangle.Width;

        /// <summary>
        /// Gets or sets a character to display instead of the actual text.
        /// </summary>
        public char? PasswordCharacter {
            get => document.PasswordCharacter;
            set => PasswordChar = value ?? '\0';
        }

        /// <summary>
        /// Gets or sets the password character (WinForms compatibility alias for PasswordCharacter).
        /// </summary>
        /// <remarks>Reads the character in use, so it answers the system character while
        /// <see cref="UseSystemPasswordChar"/> is set, as upstream's <c>EM_GETPASSWORDCHAR</c> does; the
        /// value assigned is kept underneath and comes back when that is turned off (TXT-34).</remarks>
        public char PasswordChar {
            get => document.PasswordCharacter ?? '\0';
            set {
                password_char = value;
                ApplyPasswordChar ();
            }
        }

        private char password_char;
        private bool use_system_password_char;

        // The character the system draws for UseSystemPasswordChar: the black circle the common
        // controls v6 edit control uses.
        internal const char SystemPasswordChar = '\u25CF';

        // Upstream keeps the two as independent flags with UseSystemPasswordChar winning while it is
        // set (TextBox.cs, PasswordChar / UseSystemPasswordChar).
        private void ApplyPasswordChar ()
        {
            var effective = use_system_password_char ? SystemPasswordChar : password_char;

            document.PasswordCharacter = effective == '\0' ? null : effective;
        }

        // TXT-26: `public new ScrollBars ScrollBars { get; set; }` used to live here -- a stored value
        // nothing read, shadowing ScrollControl.ScrollBars, whose setter shows and hides the two bars.
        // The shadow is why NO TextBox ever displayed a scrollbar: UpdateScrollBars enabled the vertical
        // one, but nothing ever made it visible, because the base setter could not be reached. The
        // finding's own impact line has it backwards -- boxes did not grow unwanted bars, they could
        // never get one. Removing the shadow is the whole fix; the base property is inherited as is.

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
            var caret = CaretRectangle (document.CursorIndex);

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
            // TXT-21: upstream's setter is Select (value, SelectionLength) -- the length is kept, so code
            // that sets the length first, or slides an existing highlight along, keeps its selection.
            // It used to collapse it, on the strength of a comment saying WinForms did.
            set => Select (value, SelectionLength);
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
            // Anchor on SelectionStart's WinForms meaning (the caret when nothing is selected), so
            // selecting from a caret position works; a non-positive length clears the selection.
            set => Select (SelectionStart, value);
        }

        /// <summary>
        /// Gets or sets the currently selected text. Setting it replaces the current selection
        /// (or inserts at the caret if nothing is selected), matching
        /// System.Windows.Forms.TextBoxBase.SelectedText.
        /// </summary>
        /// <remarks>
        /// Upstream's <c>SetSelectedTextInternal (text, clearUndo: true)</c> (TextBoxBase.cs): the limit
        /// is lifted for the replacement, because <c>EM_REPLACESEL</c> is not user input -- so neither
        /// <see cref="TextBoxBase.ReadOnly"/> nor <see cref="MaxLength"/> applies -- and then the modify
        /// flag and the undo buffer are cleared "for consistency with Text". It used to go through the
        /// typing path, so a read-only display box updated this way never changed, long text was cut at
        /// <see cref="MaxLength"/>, and the change counted as a user edit (TXT-20).
        /// </remarks>
        public override string SelectedText {
            get => document.SelectedText;
            set {
                ReplaceSelection (value, clearUndo: true);
                ScrollToCaret ();
            }
        }

        // SetSelectedTextInternal. clearUndo is SelectedText's flavour; Paste (string) keeps the undo
        // step and the modify flag, as upstream's does.
        internal void ReplaceSelection (string? value, bool clearUndo)
        {
            if (!clearUndo) {
                document.ReplaceRange (SelectionStart, SelectionLength, value ?? string.Empty, ignoreLimits: true);
                return;
            }

            setting_text = true;

            try {
                document.ReplaceRange (SelectionStart, SelectionLength, value ?? string.Empty, ignoreLimits: true, captureUndo: false);
            } finally {
                setting_text = false;
            }

            Modified = false;
            ClearUndo ();
        }

        /// <summary>
        /// Selects all text in the TextBox.
        /// </summary>
        public override void SelectAll ()
        {
            selection_set = true;
            document.SelectAll ();
        }

        // TXT-21's atomic Select: upstream's EM_SETSEL sets both ends at once, so SelectionChanged is
        // raised once and SelectionStart cannot collapse a selection half-way through.
        private protected override void SelectInternal (int start, int length)
        {
            // Upstream's TextBox.SelectInternal: a selection the program made is not overridden by the
            // select-all on first focus (TXT-28).
            selection_set = true;
            document.Select (start, length);
        }

        /// <summary>Clears all text from the TextBox.</summary>
        public override void Clear () => Text = string.Empty;

        /// <summary>
        /// Appends text to the current text of the TextBox.
        /// </summary>
        /// <remarks>
        /// Appends through the document rather than through <c>Text +=</c> (finding <c>TXT-02</c>, P0).
        /// The <see cref="Text"/> setter resets the caret to 0, clears the undo buffer and sets
        /// <see cref="TextBoxBase.Modified"/> false by design, so every
        /// <c>log.AppendText (line + Environment.NewLine)</c> jumped to the oldest line -- and the
        /// <see cref="ScrollToCaret"/> that usually follows went to the top too, because the caret was
        /// at 0. It also re-laid out the whole document per append. Neither ReadOnly nor MaxLength
        /// applies: upstream brackets its EM_REPLACESEL with EM_LIMITTEXT 0 because an append is not
        /// user input.
        /// </remarks>
        /// <inheritdoc/>
        public override void AppendText (string text)
        {
            if (string.IsNullOrEmpty (text))
                return;

            document.ReplaceRange (TextLength, 0, text, ignoreLimits: true);

            // The caret is now at the end, so this brings the NEW text into view rather than the top.
            ScrollToCaret ();
        }

        /// <summary>
        /// Gets or sets whether text wraps to the next line when the edge is reached.
        /// </summary>
        /// <remarks>
        /// The base keeps the value; this override owns the consequences. It used to be an auto-property
        /// on both types, so multiline text wrapped whatever it was set to -- a log viewer or a
        /// fixed-width report preview that turns wrapping off to keep its columns aligned had them
        /// broken mid-token (<c>TXT-11</c>).
        /// </remarks>
        public override bool WordWrap {
            get => base.WordWrap;
            set {
                if (base.WordWrap == value)
                    return;

                base.WordWrap = value;

                // The laid-out block is what wraps, and whether a horizontal bar is wanted depends on
                // this too.
                document.InvalidateTextBlock ();
                Invalidate ();
            }
        }

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

        /// <summary>Gets or sets whether the system's default password character is used.</summary>
        /// <remarks>
        /// An independent flag, as upstream's is: while it is set the system character is shown
        /// whatever <see cref="PasswordChar"/> holds, and turning it off brings that character back.
        /// It used to be derived from the password character, so <c>UseSystemPasswordChar = false</c>
        /// -- which the designer emits after <c>PasswordChar = '*'</c> -- unmasked the field, and
        /// <c>PasswordChar = '*'</c> alone made this read true (TXT-34).
        /// </remarks>
        public bool UseSystemPasswordChar {
            get => use_system_password_char;
            set {
                if (use_system_password_char == value)
                    return;

                use_system_password_char = value;
                ApplyPasswordChar ();
            }
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

                // Upstream clears this even when the text is unchanged (TextBox.Text's setter).
                selection_set = false;
            }
        }

        // True only while the Text setter is writing to the document, which is how Modified tells a
        // user edit apart from a programmatic assignment -- the document raises the same notification
        // for both.
        private bool setting_text;

        // TXT-28: upstream's _selectionSet. Cleared by assigning Text, set by any programmatic
        // selection and by the first focus; while it is clear, the first keyboard focus selects all.
        private bool selection_set;

        /// <inheritdoc/>
        /// <remarks>Selects all text on the first focus after <see cref="Text"/> was assigned, unless
        /// the program chose a selection or a mouse button is down -- upstream's
        /// <c>TextBox.OnGotFocus</c>. Tabbing through a data-entry form highlights each value for
        /// overtype, as it does in WinForms (TXT-28).</remarks>
        protected override void OnGotFocus (EventArgs e)
        {
            base.OnGotFocus (e);

            if (!selection_set) {
                // One shot: if this focus does not select, the box still acts as though it had.
                selection_set = true;

                if (SelectionLength == 0 && MouseButtons == MouseButtons.None)
                    SelectAll ();
            }

            // The placeholder hides while focused (TXT-27).
            Invalidate ();
        }
        // Raised by TextBoxDocument whenever the text content actually changes (typing, paste, delete, or a
        // programmatic Text set). Bridges to Control.OnTextChanged so the WinForms TextChanged event fires --
        // the overridden Text setter above writes straight to the document and never runs the base setter.
        internal void OnDocumentTextChanged ()
        {
            if (!setting_text)
                Modified = true;

            OnTextChanged (EventArgs.Empty);
        }

        // TXT-29: the selection the owner last reported, so a caret or selection change is announced once
        // per change rather than once per internal step. Starts at the caret's initial (0, 0).
        private int last_selection_start;
        private int last_selection_length;

        // Called by the document whenever the caret or the selection may have moved.
        internal void OnDocumentSelectionMayHaveChanged ()
        {
            var start = SelectionStart;
            var length = SelectionLength;

            if (start == last_selection_start && length == last_selection_length)
                return;

            last_selection_start = start;
            last_selection_length = length;

            OnDocumentSelectionChanged ();
        }

        // RichTextBox raises SelectionChanged from here: upstream's EN_SELCHANGE. A plain TextBox has no
        // such event.
        internal virtual void OnDocumentSelectionChanged () { }

        /// <inheritdoc/>
        /// <remarks>
        /// Counts the lines as laid out, so a wrapped paragraph is several lines -- what
        /// <c>EM_LINEFROMCHAR</c> answers upstream. A line-number gutter or an "Ln, Col" status bar
        /// built on it used to disagree with the screen for any wrapped text, because the base counts
        /// newlines (TXT-31).
        /// </remarks>
        /// <remarks>A single-line box keeps the base's newline count: it lays out on one line, and code
        /// that put newlines into one still expects them counted.</remarks>
        public override int GetLineFromCharIndex (int index)
        {
            if (!Multiline)
                return base.GetLineFromCharIndex (index);

            if (document.Text.Length == 0)
                return 0;

            var lines = document.GetTextBlock ().Lines;

            index = MathCompat.Clamp (index, 0, document.Text.Length);

            for (var i = 0; i < lines.Count; i++)
                if (index < lines[i].Start + lines[i].Length)
                    return i;

            return lines.Count - 1;
        }

        /// <inheritdoc/>
        /// <remarks>Visual lines, as <see cref="GetLineFromCharIndex"/>; -1 past the last one, which is
        /// <c>EM_LINEINDEX</c>'s answer.</remarks>
        public override int GetFirstCharIndexFromLine (int lineNumber)
        {
            if (!Multiline)
                return base.GetFirstCharIndexFromLine (lineNumber);

            if (lineNumber < 0)
                return -1;

            if (document.Text.Length == 0)
                return lineNumber == 0 ? 0 : -1;

            var lines = document.GetTextBlock ().Lines;

            return lineNumber < lines.Count ? lines[lineNumber].Start : -1;
        }

        // TXT-25: upstream's TextBoxBase.BackColor -- with no colour of its own, a read-only box answers
        // SystemColors.Control rather than Window, so it reads as not editable. The theme's control
        // background plays SystemColors.Control here. Only an instance colour counts as "its own", which
        // is ShouldSerializeBackColor's meaning.
        private bool PaintsReadOnlyBackground => ReadOnly && Style.BackgroundColor is null;

        /// <inheritdoc/>
        /// <remarks>A read-only box with no colour of its own answers the theme's control background
        /// (upstream's <c>SystemColors.Control</c>) instead of the editable one (TXT-25).</remarks>
        public override Color BackColor {
            get => PaintsReadOnlyBackground ? Theme.BackgroundColor.ToDrawingColor () : base.BackColor;
            set => base.BackColor = value;
        }

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            if (PaintsUpstreamField) {
                PaintUpstreamField (e);
                return;
            }

            // Only the colour differs; anything else Control's background pass does (an image, a
            // transparent box, the disabled state's own colour) stays with it.
            if (!PaintsReadOnlyBackground || !Enabled || BackgroundImage is not null) {
                base.OnPaintBackground (e);
                return;
            }

            using var device = e.DeviceSpace ();

            e.Canvas.DrawBackground (ScaledBounds, CurrentStyle, Theme.BackgroundColor);
            e.Canvas.DrawBorder (ScaledBounds, CurrentStyle);
        }

        /// <inheritdoc/>
        protected override void OnReadOnlyChanged (EventArgs e)
        {
            base.OnReadOnlyChanged (e);

            // The background follows ReadOnly (TXT-25), so the box repaints in the other colour.
            Invalidate ();
        }

        // Where the text starts, taking scrolling into account. Virtual so a derived box can reserve a
        // strip of its own before the text -- RichTextBox.ShowSelectionMargin (W6 mechanisms).
        internal virtual Point TextOrigin => new Point (PaddedClientRectangle.Location.X - scroll_x,
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

                // An empty box has no measured text, so centre the one line it will have when typed into.
                var measured = (int)document.GetTextBlock ().MeasuredHeight;
                var slack = PaddedClientRectangle.Height - (measured > 0 ? measured : document.EmptyLineHeight);
                return slack > 0 ? slack / 2 : 0;
            }
        }

        // The virtual bounds of what is currently shown to the user.
        private Rectangle TextViewport => new Rectangle (new Point (PaddedClientRectangle.Location.X + scroll_x, PaddedClientRectangle.Location.Y + scroll_y), PaddedClientRectangle.Size);

        // Enables and recalculates scrollbars as needed.
        internal void UpdateScrollBars (TextBlock block)
        {
            UpdateHorizontalScrollBar (block);

            // Something about the document changed, so we need to update the scrollbars. Whether a bar
            // is WANTED is ScrollBars' answer (TXT-26); whether it is NEEDED is the content's. None
            // still scrolls with the caret -- ScrollToCaret works off DoScroll, not off a bar.
            var wanted = ScrollBars == ScrollBars.Vertical || ScrollBars == ScrollBars.Both;
            var overflow = (int)block.MeasuredHeight - PaddedClientRectangle.Height;

            if (wanted && overflow > 0) {
                VerticalScrollBar.Enabled = true;
                // A user-driven scroll stops at Maximum - LargeChange + 1 (ScrollBar.EffectiveMaximum,
                // upstream's rule), so Maximum is the content extent minus one, as ListBox and TreeView
                // set it -- not the overflow. With Maximum = overflow the reachable range was
                // `overflow - viewport + 1`: nothing at all until the text was more than twice the
                // viewport, and always one viewport short of the end (the Theme Studio's editor).
                VerticalScrollBar.Maximum = (int)block.MeasuredHeight - 1;
                VerticalScrollBar.LargeChange = PaddedClientRectangle.Height;
                VerticalScrollBar.SmallChange = CurrentFontSize * 3;

                var new_value = Math.Min (scroll_y, overflow);

                if (VerticalScrollBar.Value != new_value)
                    VerticalScrollBar.Value = new_value;
            } else {
                // Only pull the content back into view when there is no room to scroll into. A box the
                // caller simply did not ask for a bar on still scrolls with its caret.
                if (scroll_y > 0 && (int)block.MeasuredHeight - PaddedClientRectangle.Height <= 0)
                    DoScroll (0, -scroll_y);

                VerticalScrollBar.Enabled = false;
            }
        }

        // A horizontal bar is only ever meaningful with wrapping off: with WordWrap on there is nothing
        // to the right to scroll to (TXT-11, TXT-26).
        private void UpdateHorizontalScrollBar (TextBlock block)
        {
            var wanted = (ScrollBars == ScrollBars.Horizontal || ScrollBars == ScrollBars.Both) && !WordWrap;
            var overflow = (int)block.MeasuredWidth - PaddedClientRectangle.Width;

            if (wanted && overflow > 0) {
                HorizontalScrollBar.Enabled = true;
                // Same convention as the vertical bar above: Maximum is the extent minus one.
                HorizontalScrollBar.Maximum = (int)block.MeasuredWidth - 1;
                HorizontalScrollBar.LargeChange = PaddedClientRectangle.Width;
                HorizontalScrollBar.SmallChange = CurrentFontSize * 3;

                var new_value = Math.Min (scroll_x, overflow);

                if (HorizontalScrollBar.Value != new_value)
                    HorizontalScrollBar.Value = new_value;
            } else {
                if (scroll_x > 0 && overflow <= 0)
                    DoScroll (-scroll_x, 0);

                HorizontalScrollBar.Enabled = false;
            }
        }
    }
}
