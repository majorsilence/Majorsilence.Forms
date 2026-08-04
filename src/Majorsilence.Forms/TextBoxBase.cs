using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: the common base of the TextBox-family controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to declare two abstract members and nothing else, while <see cref="TextBox"/> carried
    /// the entire editing surface. That reads as a detail until you write
    /// <c>void Bind (TextBoxBase box) =&gt; box.Clear ();</c> — extremely ordinary WinForms code, and it
    /// did not compile. Thirty-eight members were missing from the base.
    /// </para>
    /// <para>
    /// The split here is deliberate. Anything derivable from <see cref="Control.Text"/> and the
    /// selection is <em>implemented</em> on this base, so it is correct for any subclass without that
    /// subclass writing a line: <see cref="Lines"/>, <see cref="TextLength"/>, the line/character index
    /// family, <see cref="SelectAll"/>, <see cref="Clear"/>. Only what genuinely needs a text engine —
    /// the clipboard verbs, hit-testing a point, and the caret rectangle — is left virtual with a
    /// harmless default for <see cref="TextBox"/> to override against its document.
    /// </para>
    /// <para>
    /// Per item 3's lesson in docs/winforms-gap-plan.md, none of <see cref="TextBox"/>'s existing
    /// implementations were deleted in favour of these; they became overrides.
    /// </para>
    /// </remarks>
    public abstract partial class TextBoxBase : ScrollControl
    {
        private bool accepts_tab;
        private BorderStyle border_style = BorderStyle.Fixed3D;
        private bool hide_selection = true;
        private bool modified;
        private bool multiline;
        private bool read_only;

        /// <summary>Gets or sets the start of the selected text.</summary>
        public abstract int SelectionStart { get; set; }

        /// <summary>Gets or sets the length of the selected text.</summary>
        public abstract int SelectionLength { get; set; }

        /// <summary>Gets or sets whether pressing Tab inserts a tab character instead of moving focus.</summary>
        public virtual bool AcceptsTab {
            get => accepts_tab;
            set {
                if (accepts_tab == value)
                    return;

                accepts_tab = value;
                OnAcceptsTabChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the border drawn around the control.</summary>
        public virtual BorderStyle BorderStyle {
            get => border_style;
            set {
                if (border_style == value)
                    return;

                border_style = value;
                OnBorderStyleChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        /// <summary>Gets or sets whether the selection stays visible when the control loses focus.</summary>
        public virtual bool HideSelection {
            get => hide_selection;
            set {
                if (hide_selection == value)
                    return;

                hide_selection = value;
                OnHideSelectionChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets whether the text has been changed since it was last set programmatically.</summary>
        public virtual bool Modified {
            get => modified;
            set {
                if (modified == value)
                    return;

                modified = value;
                OnModifiedChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets whether the control holds more than one line of text.</summary>
        public virtual bool Multiline {
            get => multiline;
            set {
                if (multiline == value)
                    return;

                multiline = value;
                OnMultilineChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets whether the text is read-only.</summary>
        public virtual bool ReadOnly {
            get => read_only;
            set {
                if (read_only == value)
                    return;

                read_only = value;
                OnReadOnlyChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the maximum number of characters the control accepts.</summary>
        public virtual int MaxLength { get; set; } = 32767;

        /// <summary>Gets or sets whether text wraps to the next line at the control's edge.</summary>
        public virtual bool WordWrap { get; set; } = true;

        /// <summary>Gets or sets whether the standard editing shortcuts (Ctrl+C, Ctrl+V, ...) are enabled.</summary>
        public virtual bool ShortcutsEnabled { get; set; } = true;

        /// <summary>Gets whether there is an edit that <see cref="Undo"/> would reverse.</summary>
        /// <remarks>False here: this layer has no undo stack yet, and reporting true for an
        /// <see cref="Undo"/> that does nothing would make a Undo menu item enable itself and then
        /// silently fail.</remarks>
        public virtual bool CanUndo => false;

        /// <summary>Gets the number of characters of text in the control.</summary>
        public virtual int TextLength => Text.Length;

        /// <summary>Gets the height one line of text needs at the current font, including padding.</summary>
        public virtual int PreferredHeight
            => (int)Math.Ceiling (TextMeasurer.MeasureText ("Wg", this).Height) + Padding.Top + Padding.Bottom + 4;

        /// <summary>Gets or sets the lines of text in the control.</summary>
        public virtual string[] Lines {
            // WinForms returns an empty array (not a single empty string) when there is no text.
            get => Text.Length == 0 ? [] : NormalizeNewlines (Text).Split ('\n');
            set => Text = value is null ? string.Empty : string.Join ("\n", value);
        }

        /// <summary>Gets or sets the currently selected text.</summary>
        public virtual string SelectedText {
            get {
                var text = Text;
                var start = Math.Clamp (SelectionStart, 0, text.Length);
                var length = Math.Clamp (SelectionLength, 0, text.Length - start);
                return text.Substring (start, length);
            }
            set {
                var text = Text;
                var start = Math.Clamp (SelectionStart, 0, text.Length);
                var length = Math.Clamp (SelectionLength, 0, text.Length - start);
                Text = string.Concat (text.AsSpan (0, start), value ?? string.Empty, text.AsSpan (start + length));
            }
        }

        /// <summary>Appends text to the current text of the control.</summary>
        public virtual void AppendText (string text)
        {
            if (!string.IsNullOrEmpty (text))
                Text += text;
        }

        /// <summary>Clears all text from the control.</summary>
        public virtual void Clear () => Text = string.Empty;

        /// <summary>Clears the undo buffer.</summary>
        public virtual void ClearUndo () { }

        /// <summary>Copies the selection to the clipboard.</summary>
        public virtual void Copy ()
        {
            var selected = SelectedText;
            if (selected.Length > 0)
                Clipboard.SetText (selected);
        }

        /// <summary>Copies the selection to the clipboard and removes it from the control.</summary>
        public virtual void Cut ()
        {
            var selected = SelectedText;
            if (selected.Length == 0)
                return;

            Clipboard.SetText (selected);
            SelectedText = string.Empty;
        }

        /// <summary>Replaces the selection with the clipboard's text.</summary>
        public virtual void Paste ()
        {
            var text = Clipboard.GetText ();
            if (!string.IsNullOrEmpty (text))
                SelectedText = text;
        }

        /// <summary>Selects all text in the control.</summary>
        public virtual void SelectAll ()
        {
            SelectionStart = 0;
            SelectionLength = Text.Length;
        }

        /// <summary>Clears the selection, leaving the caret where the selection started.</summary>
        public virtual void DeselectAll () => SelectionLength = 0;

        /// <summary>Undoes the last edit.</summary>
        public virtual void Undo () { }

        /// <summary>Scrolls the control so the caret is visible.</summary>
        public virtual void ScrollToCaret () { }

        /// <summary>Gets the zero-based line the given character index falls on.</summary>
        /// <remarks>Logical lines — those separated by a newline in <see cref="Control.Text"/> — not the
        /// wrapped visual lines, which depend on the current width.</remarks>
        public virtual int GetLineFromCharIndex (int index)
        {
            var text = NormalizeNewlines (Text);
            var line = 0;

            for (var i = 0; i < Math.Min (index, text.Length); i++)
                if (text[i] == '\n')
                    line++;

            return line;
        }

        /// <summary>Gets the character index the given line starts at, or -1 when there is no such line.</summary>
        public virtual int GetFirstCharIndexFromLine (int lineNumber)
        {
            if (lineNumber < 0)
                return -1;

            var text = NormalizeNewlines (Text);
            if (lineNumber == 0)
                return 0;

            var line = 0;
            for (var i = 0; i < text.Length; i++) {
                if (text[i] != '\n')
                    continue;
                if (++line == lineNumber)
                    return i + 1;
            }

            return -1;
        }

        /// <summary>Gets the character index the line containing the caret starts at.</summary>
        public virtual int GetFirstCharIndexOfCurrentLine ()
            => GetFirstCharIndexFromLine (GetLineFromCharIndex (SelectionStart));

        /// <summary>Gets the index of the character nearest the given control-relative point.</summary>
        public virtual int GetCharIndexFromPosition (Point pt) => 0;

        /// <summary>Gets the character nearest the given control-relative point.</summary>
        public virtual char GetCharFromPosition (Point pt)
        {
            var text = Text;
            var index = GetCharIndexFromPosition (pt);
            return index >= 0 && index < text.Length ? text[index] : '\0';
        }

        /// <summary>Gets the control-relative location of the given character index.</summary>
        public virtual Point GetPositionFromCharIndex (int index) => Point.Empty;

        /// <summary>Raised when <see cref="AcceptsTab"/> changes.</summary>
        public event EventHandler? AcceptsTabChanged;

        /// <summary>Raised when <see cref="BorderStyle"/> changes.</summary>
        public event EventHandler? BorderStyleChanged;

        /// <summary>Raised when <see cref="HideSelection"/> changes.</summary>
        public event EventHandler? HideSelectionChanged;

        /// <summary>Raised when <see cref="Modified"/> changes.</summary>
        public event EventHandler? ModifiedChanged;

        /// <summary>Raised when <see cref="Multiline"/> changes.</summary>
        public event EventHandler? MultilineChanged;

        /// <summary>Raised when <see cref="ReadOnly"/> changes.</summary>
        public event EventHandler? ReadOnlyChanged;

        // BackgroundImageChanged, BackgroundImageLayoutChanged and StyleChanged are inherited from
        // Control, which declares them for every control rather than each repeating the same
        // never-raised event.

        /// <summary>Raises the <see cref="AcceptsTabChanged"/> event.</summary>
        protected virtual void OnAcceptsTabChanged (EventArgs e) => AcceptsTabChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="BorderStyleChanged"/> event.</summary>
        protected virtual void OnBorderStyleChanged (EventArgs e) => BorderStyleChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="HideSelectionChanged"/> event.</summary>
        protected virtual void OnHideSelectionChanged (EventArgs e) => HideSelectionChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ModifiedChanged"/> event.</summary>
        protected virtual void OnModifiedChanged (EventArgs e) => ModifiedChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="MultilineChanged"/> event.</summary>
        protected virtual void OnMultilineChanged (EventArgs e) => MultilineChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ReadOnlyChanged"/> event.</summary>
        protected virtual void OnReadOnlyChanged (EventArgs e) => ReadOnlyChanged?.Invoke (this, e);

        // Both CRLF and a bare CR count as one line break, so the index arithmetic above is done on a
        // single-character form rather than being written three times.
        private static string NormalizeNewlines (string text)
            => text.Replace ("\r\n", "\n").Replace ('\r', '\n');
    }
}
