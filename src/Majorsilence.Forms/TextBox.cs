using System.Drawing;
using Majorsilence.Forms.Renderers;
using Topten.RichTextKit;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TextBox control.
    /// </summary>
    public class TextBox : TextBoxBase
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
        public void Copy ()
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
        public int PreferredHeight
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
        public void Cut ()
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
        private int GetCharIndexFromPosition (Point location)
        {
            if (!document.Text.HasValue ())
                return 0;

            return document.GetCharIndexFromPosition (location.X - TextOrigin.X, location.Y - TextOrigin.Y).ClosestCodePointIndex;
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
                        need_refresh = document.DeleteText (true, e.Control);
                        return true;
                    case Keys.Back:
                        need_refresh = document.DeleteText (false, e.Control);
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
        public int MaxLength {
            get => document.MaxLength;
            set => document.MaxLength = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if the TextBox supports multiple lines of text.
        /// Only the WinForms spelling exists: VB is case-insensitive, so a MultiLine/Multiline
        /// pair makes the member unusable from VB.
        /// </summary>
        public bool Multiline {
            get => document.IsMultiline;
            set {
                if (document.IsMultiline != value) {

                    if (Padding == DefaultPadding)
                        Padding = new Padding (value ? 4 : 1, 0, 0, 0);

                    document.IsMultiline = value;
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
                if (document.InsertText (e.Text))
                    ScrollToCaret ();
            }
        }

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
        public void Paste ()
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
        public int TextLength => Text.Length;

        /// <summary>
        /// Gets or sets a value indicating if the text can be edited.
        /// </summary>
        public bool ReadOnly {
            get => document.ReadOnly;
            set => document.ReadOnly = value;
        }

        /// <summary>
        /// Scrolls the TextBox so that the caret is visible.
        /// </summary>
        public void ScrollToCaret ()
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
        public override int SelectionStart {
            get => document.SelectionStart;
            set => document.SelectionStart = value;
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
                var start = document.SelectionStart < 0 ? 0 : document.SelectionStart;
                document.SelectionEnd = start + value;
            }
        }

        /// <summary>
        /// Gets or sets the currently selected text. Setting it replaces the current selection
        /// (or inserts at the caret if nothing is selected), matching
        /// System.Windows.Forms.TextBoxBase.SelectedText.
        /// </summary>
        public string SelectedText {
            get => document.SelectedText;
            set => document.InsertText (value ?? string.Empty);
        }

        /// <summary>
        /// Selects all text in the TextBox.
        /// </summary>
        public void SelectAll () => document.SelectAll ();

        /// <summary>Clears all text from the TextBox.</summary>
        public void Clear () => Text = string.Empty;

        /// <summary>
        /// Appends text to the current text of the TextBox.
        /// </summary>
        public void AppendText (string text)
        {
            if (string.IsNullOrEmpty (text))
                return;

            Text += text;
        }

        /// <summary>
        /// Gets or sets whether text wraps to the next line when the edge is reached.
        /// </summary>
        public bool WordWrap { get; set; } = true;

        /// <summary>Gets or sets whether pressing Enter in a multiline TextBox creates a new line. Stub in Majorsilence.Forms.</summary>
        public bool AcceptsReturn { get; set; }

        /// <summary>Gets or sets whether pressing Tab in a TextBox inserts a tab character. Stub in Majorsilence.Forms.</summary>
        public bool AcceptsTab { get; set; }

        /// <summary>Gets or sets the character casing applied to text. Stub in Majorsilence.Forms.</summary>
        public CharacterCasing CharacterCasing { get; set; } = CharacterCasing.Normal;

        /// <summary>Gets or sets whether the selection is hidden when the control loses focus. Stub in Majorsilence.Forms.</summary>
        public bool HideSelection { get; set; } = true;

        /// <summary>Gets or sets the auto-complete mode. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteMode AutoCompleteMode { get; set; } = AutoCompleteMode.None;

        /// <summary>Gets or sets the auto-complete source. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

        /// <summary>Gets or sets a custom list of strings used for auto-complete. Stub in Majorsilence.Forms.</summary>
        public System.Collections.Specialized.StringCollection AutoCompleteCustomSource { get; set; } = new System.Collections.Specialized.StringCollection ();

        /// <summary>Gets or sets whether the system's default password character is used. Stub in Majorsilence.Forms.</summary>
        public bool UseSystemPasswordChar {
            get => PasswordCharacter.HasValue;
            set { if (value && PasswordCharacter is null) PasswordCharacter = '*'; else if (!value) PasswordCharacter = null; }
        }

        /// <summary>Selects text in the TextBox starting at the specified position.</summary>
        public void Select (int start, int length) { SelectionStart = start; SelectionLength = length; }

        /// <summary>Undoes the last edit. Stub in Majorsilence.Forms.</summary>
        public void Undo () { }

        /// <summary>Clears the undo buffer. Stub in Majorsilence.Forms.</summary>
        public void ClearUndo () { }

        /// <summary>Raised when the TextAlign property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? TextAlignChanged { add { } remove { } }

        /// <summary>Raised when the AcceptsTab property changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? AcceptsTabChanged { add { } remove { } }

        /// <summary>Gets or sets whether the text has been modified since it was last set. Stub in Majorsilence.Forms.</summary>
        public bool Modified { get; set; }

        /// <summary>Gets or sets the lines of text in the TextBox.</summary>
        public string[] Lines {
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

        /// <summary>Gets or sets the horizontal alignment of text in the TextBox. Stub in Majorsilence.Forms.</summary>
        public HorizontalAlignment TextAlign { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets the border style of the text box. Stub in Majorsilence.Forms.</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override string Text {
            get => document.Text;
            set {
                if (document.Text != value) {
                    document.Text = value;
                    ScrollToCaret ();
                }
            }
        }

        // Raised by TextBoxDocument whenever the text content actually changes (typing, paste, delete, or a
        // programmatic Text set). Bridges to Control.OnTextChanged so the WinForms TextChanged event fires --
        // the overridden Text setter above writes straight to the document and never runs the base setter.
        internal void OnDocumentTextChanged () => OnTextChanged (EventArgs.Empty);

        // Where the text starts, taking scrolling into account
        internal Point TextOrigin => new Point (PaddedClientRectangle.Location.X - scroll_x, PaddedClientRectangle.Location.Y - scroll_y);

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
