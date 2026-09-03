using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: a multi-line text box that accepts RTF and plain text.
    /// Majorsilence.Forms does not render RTF; the RTF markup is stripped and plain text is shown.
    /// </summary>
    public partial class RichTextBox : TextBox
    {
        /// <summary>
        /// Initializes a new instance of the RichTextBox class.
        /// </summary>
        public RichTextBox ()
        {
            // WinForms RichTextBox is multi-line by default (unlike the base TextBox).
            Multiline = true;
        }

        /// <summary>
        /// Gets or sets the control's contents as an RTF document.
        /// </summary>
        /// <remarks>
        /// The getter renders the CURRENT text (finding <c>TXT-04</c>, P0). It used to return a
        /// stored string that only the setter ever wrote, so the standard persistence pattern --
        /// <c>note.Body = rtb.Rtf</c> on save -- stored an empty string for everything the user had
        /// typed, or the RTF of the last programmatic assignment, overwriting the edits with stale
        /// data. Even <c>rtb.Rtf = rtb.Rtf</c> dropped them.
        /// <para>
        /// See <see cref="ToRtf"/> for what a generated document does and does not contain: the text
        /// and its paragraphs, not the character formatting.
        /// </para>
        /// </remarks>
        public string Rtf {
            get => ToRtf ();
            set => Text = StripRtf (value ?? string.Empty);
        }

        /// <summary>Gets or sets whether automatic URL detection is enabled. Stub in Majorsilence.Forms.</summary>
        public bool DetectUrls { get; set; } = true;

        /// <summary>Gets or sets whether the control is in read-only mode.</summary>
        public new bool ReadOnly {
            get => base.ReadOnly;
            set => base.ReadOnly = value;
        }

        /// <summary>Gets or sets the scroll bars to show. Stub in Majorsilence.Forms (always shows vertical).</summary>
        public new RichTextBoxScrollBars ScrollBars { get; set; } = RichTextBoxScrollBars.Both;

        /// <summary>Gets or sets the selection start in the text.</summary>
        public new int SelectionStart {
            get => base.SelectionStart;
            set => base.SelectionStart = value;
        }

        /// <summary>Gets or sets the selection length in the text.</summary>
        public new int SelectionLength {
            get => base.SelectionLength;
            set => base.SelectionLength = value;
        }

        // `public new void AppendText` used to live here, assigning Text and then patching the caret
        // back -- which only happened when it was called through a RichTextBox-typed reference, and
        // never scrolled. TextBox.AppendText is correct now, so the shadow is gone (TXT-02).

        /// <summary>Loads the contents of a file into the control.</summary>
        /// <remarks>
        /// <see cref="RichTextBoxStreamType.RichText"/> is the default, as it is upstream, and the
        /// <paramref name="fileType"/> argument is now honoured (finding <c>TXT-16</c>). Both used to
        /// be plain text, so <c>rtb.LoadFile ("notes.rtf")</c> displayed raw <c>{\rtf1\ansi…</c>
        /// markup and <c>rtb.SaveFile ("notes.rtf")</c> wrote a plain-text file that the application
        /// this one replaced would refuse to open.
        /// </remarks>
        public void LoadFile (string path, RichTextBoxStreamType fileType = RichTextBoxStreamType.RichText)
        {
            var content = System.IO.File.ReadAllText (path);

            if (IsRichText (fileType))
                Rtf = content;
            else
                Text = content;
        }

        /// <summary>Loads from a stream into the control. Plain text only in Majorsilence.Forms.</summary>
        public void LoadFile (System.IO.Stream data, RichTextBoxStreamType fileType = RichTextBoxStreamType.RichText)
        {
#if NETSTANDARD2_0
            // The (Stream, ..., leaveOpen) StreamReader ctor with defaulted encoding is a later addition.
            using var reader = new System.IO.StreamReader (data, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
#else
            using var reader = new System.IO.StreamReader (data, leaveOpen: true);
#endif
            var content = reader.ReadToEnd ();

            if (IsRichText (fileType))
                Rtf = content;
            else
                Text = content;
        }

        /// <summary>Saves the contents of the control to a file.</summary>
        /// <inheritdoc cref="LoadFile(string, RichTextBoxStreamType)" path="/remarks"/>
        public void SaveFile (string path, RichTextBoxStreamType fileType = RichTextBoxStreamType.RichText)
        {
            System.IO.File.WriteAllText (path, IsRichText (fileType) ? Rtf : Text);
        }

        /// <summary>Saves the contents of the control to a stream. Plain text only in Majorsilence.Forms.</summary>
        public void SaveFile (System.IO.Stream data, RichTextBoxStreamType fileType = RichTextBoxStreamType.RichText)
        {
#if NETSTANDARD2_0
            using var writer = new System.IO.StreamWriter (data, new System.Text.UTF8Encoding (encoderShouldEmitUTF8Identifier: false), bufferSize: 1024, leaveOpen: true);
#else
            using var writer = new System.IO.StreamWriter (data, leaveOpen: true);
#endif
            writer.Write (IsRichText (fileType) ? Rtf : Text);
        }

        /// <summary>Gets or sets the color of the currently selected text.</summary>
        /// <remarks>
        /// Real as of W5.14 (finding <c>TXT-17</c>). With text selected this colours it; with nothing
        /// selected it sets the format for what is typed or appended next, which is what makes
        /// <c>SelectionColor = Color.Red; AppendText ("ERROR ...")</c> produce a red line. The getter
        /// reports the colour at the insertion point rather than the last value assigned.
        /// </remarks>
        public System.Drawing.Color SelectionColor {
            get => FormatAt (SelectionStart).ForeColor ?? ForeColor;
            set => SetFormat (f => { f.ForeColor = value; return f; });
        }

        /// <summary>Gets or sets the font of the currently selected text.</summary>
        /// <remarks>
        /// Only the style flags are per-run: bold, italic and underline are carried by the run, while
        /// the family and size come from the control's own <see cref="Control.Font"/>. A per-run
        /// typeface would have to reach the layout engine's per-span font, which the span type this
        /// is painted through does not carry.
        /// </remarks>
        public Majorsilence.Forms.Drawing.Font? SelectionFont {
            get {
                var format = FormatAt (SelectionStart);
                var style = Majorsilence.Forms.Drawing.FontStyle.Regular;

                if (format.Bold ?? Font.Bold)
                    style |= Majorsilence.Forms.Drawing.FontStyle.Bold;

                if (format.Italic ?? Font.Italic)
                    style |= Majorsilence.Forms.Drawing.FontStyle.Italic;

                if (format.Underline ?? Font.Underline)
                    style |= Majorsilence.Forms.Drawing.FontStyle.Underline;

                return new Majorsilence.Forms.Drawing.Font (Font.Name, Font.Size, style, Font.Unit);
            }
            set {
                if (value is null)
                    return;

                SetFormat (f => {
                    f.Bold = value.Bold;
                    f.Italic = value.Italic;
                    f.Underline = value.Underline;
                    return f;
                });
            }
        }

        /// <summary>Gets or sets the alignment of the currently selected text. Stub in Majorsilence.Forms.</summary>
        public HorizontalAlignment SelectionAlignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets the indentation of the currently selected text. Stub in Majorsilence.Forms.</summary>
        public int SelectionIndent { get; set; }

        /// <summary>Gets or sets whether the current selection or insertion point is bulleted. Stub in Majorsilence.Forms.</summary>
        public bool SelectionBullet { get; set; }

        /// <summary>Gets or sets whether the selected text has bold formatting.</summary>
        /// <inheritdoc cref="SelectionColor" path="/remarks"/>
        public bool SelectionBold {
            get => FormatAt (SelectionStart).Bold ?? Font.Bold;
            set => SetFormat (f => { f.Bold = value; return f; });
        }

        /// <summary>Gets or sets whether the selected text has italic formatting.</summary>
        /// <inheritdoc cref="SelectionColor" path="/remarks"/>
        public bool SelectionItalic {
            get => FormatAt (SelectionStart).Italic ?? Font.Italic;
            set => SetFormat (f => { f.Italic = value; return f; });
        }

        /// <summary>Gets or sets whether the selected text is underlined.</summary>
        /// <inheritdoc cref="SelectionColor" path="/remarks"/>
        public bool SelectionUnderline {
            get => FormatAt (SelectionStart).Underline ?? Font.Underline;
            set => SetFormat (f => { f.Underline = value; return f; });
        }

        /// <summary>Gets or sets the character length of the control's content. Stub — same as Text.Length.</summary>
        public new int TextLength => Text.Length;

        /// <summary>Gets or sets the bullet indent size. Stub in Majorsilence.Forms.</summary>
        public int BulletIndent { get; set; }

        /// <summary>Gets or sets the background color of the selected text.</summary>
        /// <inheritdoc cref="SelectionColor" path="/remarks"/>
        /// <remarks>Unlike <see cref="SelectionColor"/>, an unformatted run reports
        /// <see cref="System.Drawing.Color.Empty"/> rather than the control's colour: upstream reads
        /// CFE_AUTOBACKCOLOR and answers Empty for it, because "no background" and "a background that
        /// happens to match the control" are different things to a caller about to save the
        /// document.</remarks>
        public System.Drawing.Color SelectionBackColor {
            get => FormatAt (SelectionStart).BackColor ?? System.Drawing.Color.Empty;
            set => SetFormat (f => { f.BackColor = value; return f; });
        }

        // `public new string SelectedText` used to live here, rebuilding the whole string and
        // assigning Text -- so replacing a selection in a long document scrolled to the top, cleared
        // undo and raised TextChanged for the entire document, and did all that only when called
        // through a RichTextBox-typed reference. The inherited TextBox.SelectedText goes through the
        // document instead (TXT-35).

        /// <summary>Gets or sets the zoom factor. Stub in Majorsilence.Forms (always 1.0).</summary>
        public float ZoomFactor { get; set; } = 1.0f;

        /// <summary>Gets or sets whether auto-drag-drop is enabled. Stub in Majorsilence.Forms.</summary>
        public bool EnableAutoDragDrop { get; set; }

        /// <summary>Gets or sets the right-edge indentation of the selection. Stub in Majorsilence.Forms.</summary>
        public int SelectionRightIndent { get; set; }

        /// <summary>Gets or sets the hanging indent of the selection. Stub in Majorsilence.Forms.</summary>
        public int SelectionHangingIndent { get; set; }

        /// <summary>Gets or sets the character offset (superscript/subscript) of the selection. Stub in Majorsilence.Forms.</summary>
        public int SelectionCharOffset { get; set; }

        /// <summary>Gets or sets whether the selected text is protected from editing. Stub in Majorsilence.Forms.</summary>
        public bool SelectionProtected { get; set; }

        /// <summary>Searches for the specified text in the RichTextBox. Returns the start index or -1.</summary>
        /// <remarks>
        /// Case-INsensitive unless <see cref="RichTextBoxFinds.MatchCase"/> is given, and the match is
        /// selected and scrolled into view unless <see cref="RichTextBoxFinds.NoHighlight"/> is. Both
        /// are upstream's defaults and neither used to happen (finding <c>TXT-14</c>), which broke the
        /// standard highlight-every-occurrence loop twice over: it missed differently-cased hits, and
        /// because nothing was ever selected, the <c>SelectionColor</c> assignment inside the loop had
        /// no selection to colour.
        /// </remarks>
        public int Find (string str) => FindCore (str, 0, -1, RichTextBoxFinds.None);

        /// <summary>Searches for the given text, honouring the search options.</summary>
        public int Find (string str, RichTextBoxFinds options) => FindCore (str, 0, -1, options);

        /// <summary>Searches for the first occurrence of any of the given characters.</summary>
        public int Find (char[] characterSet) => Find (characterSet, 0, -1);

        /// <inheritdoc cref="Find(char[])"/>
        public int Find (char[] characterSet, int start) => Find (characterSet, start, -1);

        /// <inheritdoc cref="Find(char[])"/>
        public int Find (char[] characterSet, int start, int end)
        {
            Guard.ThrowIfNull (characterSet);

            var text = Text;
            var last = end < 0 ? text.Length : Math.Min (end, text.Length);

            for (var i = Math.Max (0, start); i < last; i++)
                if (Array.IndexOf (characterSet, text[i]) >= 0)
                    return i;

            return -1;
        }

        /// <summary>Searches for the specified text starting at the given offset. Returns start index or -1.</summary>
        /// <inheritdoc cref="Find(string)" path="/remarks"/>
        public int Find (string str, int start, RichTextBoxFinds options = RichTextBoxFinds.None)
            => FindCore (str, start, -1, options);

        /// <summary>Searches for text within the specified range. Returns start index or -1.</summary>
        /// <inheritdoc cref="Find(string)" path="/remarks"/>
        public int Find (string str, int start, int end, RichTextBoxFinds options = RichTextBoxFinds.None)
            => FindCore (str, start, end, options);

        // The one implementation the four string overloads share.
        private int FindCore (string str, int start, int end, RichTextBoxFinds options)
        {
            Guard.ThrowIfNull (str);

            var text = Text;

            // Upstream throws for these rather than returning -1, and a caller that has computed a bad
            // range wants to know: silently finding nothing is how an off-by-one becomes a mystery.
            if (start < 0 || start > text.Length)
                throw new ArgumentOutOfRangeException (nameof (start), start, "Start index is outside the text.");

            if (end < -1)
                throw new ArgumentOutOfRangeException (nameof (end), end, "End index must be -1 or a position in the text.");

            // -1 means "to the end of the text", which is the documented whole-text form. It used to
            // produce an EMPTY range through Math.Min (end, Text.Length), so `Find (s, 0, -1, opts)`
            // never matched anything at all.
            var last = end < 0 ? text.Length : Math.Min (end, text.Length);

            if (start > last)
                throw new ArgumentOutOfRangeException (nameof (end), end, "End index precedes the start index.");

            if (str.Length == 0)
                return -1;

            var comparison = (options & RichTextBoxFinds.MatchCase) != 0
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;
            var whole_word = (options & RichTextBoxFinds.WholeWord) != 0;
            var reverse = (options & RichTextBoxFinds.Reverse) != 0;
            var range = text.Substring (start, last - start);
            var found = -1;

            // Walk from whichever end the direction asks for, skipping hits that fail the whole-word
            // test rather than giving up at the first one -- "cat" inside "concatenate" must not stop
            // the search from reaching the real word later in the line.
            for (var offset = reverse ? range.Length - str.Length : 0;
                 reverse ? offset >= 0 : offset <= range.Length - str.Length;
                 offset += reverse ? -1 : 1) {
                if (string.Compare (range, offset, str, 0, str.Length, comparison) != 0)
                    continue;

                if (whole_word && !IsWholeWordAt (text, start + offset, str.Length))
                    continue;

                found = start + offset;
                break;
            }

            if (found < 0)
                return -1;

            // Selecting the hit is what makes the caller's next SelectionColor/SelectedText assignment
            // apply to it, and is upstream's behaviour unless NoHighlight is passed.
            if ((options & RichTextBoxFinds.NoHighlight) == 0) {
                Select (found, str.Length);
                ScrollToCaret ();
            }

            return found;
        }

        // A match is a whole word when neither neighbour is alphanumeric. Upstream asks RichEdit, which
        // uses its own word-break procedure; letter-or-digit is the portable approximation of it.
        private static bool IsWholeWordAt (string text, int index, int length)
        {
            if (index > 0 && char.IsLetterOrDigit (text[index - 1]))
                return false;

            var after = index + length;

            return after >= text.Length || !char.IsLetterOrDigit (text[after]);
        }

        // The line/character index family used to be duplicated here. It has moved to TextBoxBase,
        // which is where WinForms declares it and where every text control can share one
        // implementation. These copies were not carrying behaviour worth keeping -- they were
        // strictly weaker: GetFirstCharIndexFromLine returned Text.Length rather than -1 for a line
        // that does not exist, and none of them treated a bare CR as a line break. The two
        // hit-testing ones were stubs that hid TextBox's real, document-backed implementation, so
        // deleting them is what makes a RichTextBox answer GetCharIndexFromPosition correctly.


        /// <summary>Paste from the clipboard into the specified format. Stub in Majorsilence.Forms (pastes plain text).</summary>
        public void Paste (DataFormat clipFormat) => base.Paste ();

        /// <summary>Pastes the clipboard's contents in the given format.</summary>
        public void Paste (DataFormats.Format clipFormat) => base.Paste ();

        /// <summary>Raised when the control's contents are resized. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<ContentsResizedEventArgs>? ContentsResized { add { } remove { } }

        /// <summary>Raised when the user clicks a link in the RichTextBox. Stub in Majorsilence.Forms.</summary>
        public event LinkClickedEventHandler? LinkClicked;

        /// <summary>Raises the LinkClicked event.</summary>
        protected virtual void OnLinkClicked (LinkClickedEventArgs e) => LinkClicked?.Invoke (this, e);

        /// <summary>Raised when the selection changes.</summary>
        /// <remarks>
        /// A real event now, for the same reason as <see cref="LinkClicked"/>'s neighbors: empty
        /// accessors let a handler attach and then silently drop it. Not yet raised by the caret/mouse
        /// selection path -- <see cref="OnSelectionChanged"/> is public enough to call, which is what
        /// lets a ported override of the real WinForms hook keep compiling instead of failing with
        /// CS0115.
        /// </remarks>
        public event EventHandler? SelectionChanged;

        /// <summary>Raises the <see cref="SelectionChanged"/> event.</summary>
        protected virtual void OnSelectionChanged (EventArgs e) => SelectionChanged?.Invoke (this, e);

    }

    /// <summary>Specifies the type of scroll bars shown in a RichTextBox.</summary>
    public enum RichTextBoxScrollBars
    {
        /// <summary>No scroll bars.</summary>
        None = 0,
        /// <summary>Only horizontal scroll bars.</summary>
        Horizontal = 1,
        /// <summary>Only vertical scroll bars.</summary>
        Vertical = 2,
        /// <summary>Both horizontal and vertical scroll bars.</summary>
        Both = 3,
        /// <summary>Forced horizontal scroll bar.</summary>
        ForcedHorizontal = 17,
        /// <summary>Forced vertical scroll bar.</summary>
        ForcedVertical = 18,
        /// <summary>Both forced scroll bars.</summary>
        ForcedBoth = 19,
    }

    /// <summary>Specifies the data format of a file opened or saved with a RichTextBox.</summary>
    public enum RichTextBoxStreamType
    {
        /// <summary>RTF format.</summary>
        RichText,
        /// <summary>Plain-text format.</summary>
        PlainText,
        /// <summary>RTF with no OLE objects.</summary>
        RichNoOleObjs,
        /// <summary>Text with spaces instead of OLE objects.</summary>
        TextTextOleObjs,
        /// <summary>Unicode plain-text format.</summary>
        UnicodePlainText
    }

    /// <summary>Specifies options for the RichTextBox.Find method.</summary>
    [System.Flags]
    public enum RichTextBoxFinds
    {
        /// <summary>No special options.</summary>
        None = 0,
        /// <summary>Perform a whole-word search.</summary>
        WholeWord = 2,
        /// <summary>Perform a case-sensitive search.</summary>
        MatchCase = 4,
        /// <summary>Search from the end of the selection.</summary>
        Reverse = 16,
        /// <summary>Do not highlight the match.</summary>
        NoHighlight = 8
    }

    /// <summary>WinForms compatibility: represents a clipboard data format. Stub in Majorsilence.Forms.</summary>
    public class DataFormat
    {
        /// <summary>Gets the name of this format.</summary>
        public string Name { get; }

        /// <summary>Gets the ID of this format.</summary>
        public int Id { get; }

        /// <summary>Initializes a new instance.</summary>
        public DataFormat (string name, int id) { Name = name; Id = id; }

        /// <summary>Converts to the format name, so a DataFormats constant can be used wherever
        /// System.Windows.Forms takes a format string -- <c>new DataObject (DataFormats.FileDrop, files)</c>
        /// being the usual one.</summary>
        public static implicit operator string (DataFormat format) => format.Name;

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>WinForms compatibility: provides static members for clipboard data formats. Stub in Majorsilence.Forms.</summary>
    public static partial class DataFormats
    {
        /// <summary>Represents a clipboard data format.</summary>
        /// <remarks>WinForms nests this type inside DataFormats and names it <c>Format</c>; this
        /// layer declared it at namespace scope as <c>DataFormat</c>. Both names now exist and mean
        /// the same shape, so <c>DataFormats.Format</c> in migrated code resolves.</remarks>
        public sealed class Format : DataFormat
        {
            /// <summary>Initializes a new instance of the <see cref="Format"/> class.</summary>
            public Format (string name, int id) : base (name, id) { }
        }

        /// <summary>Text format.</summary>
        public static DataFormat Text { get; } = new DataFormat ("Text", 1);

        /// <summary>Unicode text format.</summary>
        public static DataFormat UnicodeText { get; } = new DataFormat ("UnicodeText", 13);

        /// <summary>RTF format.</summary>
        public static DataFormat Rtf { get; } = new DataFormat ("Rich Text Format", 49156);

        /// <summary>Bitmap image format.</summary>
        public static DataFormat Bitmap { get; } = new DataFormat ("Bitmap", 2);

        /// <summary>File drop list format.</summary>
        public static DataFormat FileDrop { get; } = new DataFormat ("FileDrop", 15);

        /// <summary>HTML format.</summary>
        public static DataFormat Html { get; } = new DataFormat ("HTML Format", 0xC004);

        /// <summary>OEM text format.</summary>
        public static DataFormat OemText { get; } = new DataFormat ("OEMText", 7);

        /// <summary>Comma-separated-value format (what a grid puts on the clipboard alongside plain text).</summary>
        public static DataFormat CommaSeparatedValue { get; } = new DataFormat ("Csv", 0xC005);

        /// <summary>Returns the format with the specified name.</summary>
        public static DataFormat GetFormat (string format) => new DataFormat (format, 0);

        /// <summary>Returns the format with the given numeric id.</summary>
        /// <remarks>Matches one of the standard formats when the id is one of theirs, so a round trip
        /// through the id gives back the same object rather than a new nameless one.</remarks>
        public static DataFormat GetFormat (int id)
        {
            DataFormat[] standard = [
                Text, UnicodeText, Rtf, Bitmap, FileDrop, Html, OemText, CommaSeparatedValue,
                Dib, Dif, EnhancedMetafile, Locale, MetafilePict, Palette, PenData, Riff,
                Serializable, StringFormat, SymbolicLink, Tiff, WaveAudio,
            ];
            return Array.Find (standard, f => f.Id == id) ?? new DataFormat ($"Format{id}", id);
        }
    }
}
