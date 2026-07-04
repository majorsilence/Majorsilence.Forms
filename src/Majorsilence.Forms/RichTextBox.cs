using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: a multi-line text box that accepts RTF and plain text.
    /// Majorsilence.Forms does not render RTF; the RTF markup is stripped and plain text is shown.
    /// </summary>
    public class RichTextBox : TextBox
    {
        private string _rtf = string.Empty;

        /// <summary>
        /// Initializes a new instance of the RichTextBox class.
        /// </summary>
        public RichTextBox ()
        {
            // WinForms RichTextBox is multi-line by default (unlike the base TextBox).
            MultiLine = true;
        }

        /// <summary>
        /// Gets or sets the text in RTF format. The RTF is stored verbatim for round-trip
        /// compatibility; only the plain-text content is rendered.
        /// </summary>
        public string Rtf {
            get => _rtf;
            set {
                _rtf = value ?? string.Empty;
                Text = StripRtf (_rtf);
            }
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

        /// <summary>Appends the specified text to the current contents of the text box.</summary>
        public new void AppendText (string text)
        {
            Text += text;
            SelectionStart = Text.Length;
        }

        /// <summary>Loads the contents of a file into the control. Plain text only in Majorsilence.Forms.</summary>
        public void LoadFile (string path, RichTextBoxStreamType fileType = RichTextBoxStreamType.PlainText)
        {
            Text = System.IO.File.ReadAllText (path);
        }

        /// <summary>Loads from a stream into the control. Plain text only in Majorsilence.Forms.</summary>
        public void LoadFile (System.IO.Stream data, RichTextBoxStreamType fileType = RichTextBoxStreamType.PlainText)
        {
            using var reader = new System.IO.StreamReader (data, leaveOpen: true);
            Text = reader.ReadToEnd ();
        }

        /// <summary>Saves the contents of the control to a file. Plain text only in Majorsilence.Forms.</summary>
        public void SaveFile (string path, RichTextBoxStreamType fileType = RichTextBoxStreamType.PlainText)
        {
            System.IO.File.WriteAllText (path, Text);
        }

        /// <summary>Saves the contents of the control to a stream. Plain text only in Majorsilence.Forms.</summary>
        public void SaveFile (System.IO.Stream data, RichTextBoxStreamType fileType = RichTextBoxStreamType.PlainText)
        {
            using var writer = new System.IO.StreamWriter (data, leaveOpen: true);
            writer.Write (Text);
        }

        /// <summary>Gets or sets the color of the currently selected text. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color SelectionColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the font of the currently selected text. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Font? SelectionFont { get; set; }

        /// <summary>Gets or sets the alignment of the currently selected text. Stub in Majorsilence.Forms.</summary>
        public HorizontalAlignment SelectionAlignment { get; set; } = HorizontalAlignment.Left;

        /// <summary>Gets or sets the indentation of the currently selected text. Stub in Majorsilence.Forms.</summary>
        public int SelectionIndent { get; set; }

        /// <summary>Gets or sets whether the current selection or insertion point is bulleted. Stub in Majorsilence.Forms.</summary>
        public bool SelectionBullet { get; set; }

        /// <summary>Gets or sets whether the selected text has bold formatting. Stub in Majorsilence.Forms.</summary>
        public bool SelectionBold { get; set; }

        /// <summary>Gets or sets whether the selected text has italic formatting. Stub in Majorsilence.Forms.</summary>
        public bool SelectionItalic { get; set; }

        /// <summary>Gets or sets whether the selected text is underlined. Stub in Majorsilence.Forms.</summary>
        public bool SelectionUnderline { get; set; }

        /// <summary>Gets or sets the character length of the control's content. Stub — same as Text.Length.</summary>
        public new int TextLength => Text.Length;

        /// <summary>Gets or sets the bullet indent size. Stub in Majorsilence.Forms.</summary>
        public int BulletIndent { get; set; }

        /// <summary>Gets or sets the background color of the selected text. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color SelectionBackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the selected text. Setting it replaces the current selection (or inserts at the caret if nothing is selected).</summary>
        public new string SelectedText {
            get => SelectionLength > 0 && SelectionStart >= 0 ? Text.Substring (SelectionStart, Math.Min (SelectionLength, Text.Length - SelectionStart)) : string.Empty;
            set {
                value ??= string.Empty;
                int start = SelectionStart;
                int length = Math.Max (SelectionLength, 0);
                string current = Text;
                Text = string.Concat (current.AsSpan (0, start), value, current.AsSpan (start + length));
                SelectionStart = start + value.Length;
                SelectionLength = 0;
            }
        }

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
        public int Find (string str) => Text.IndexOf (str, StringComparison.Ordinal);

        /// <summary>Searches for the specified text starting at the given offset. Returns start index or -1.</summary>
        public int Find (string str, int start, RichTextBoxFinds options = RichTextBoxFinds.None)
            => Text.IndexOf (str, start, StringComparison.Ordinal);

        /// <summary>Searches for text within the specified range. Returns start index or -1.</summary>
        public int Find (string str, int start, int end, RichTextBoxFinds options = RichTextBoxFinds.None)
        {
            var range = Text.Substring (start, Math.Max (0, Math.Min (end, Text.Length) - start));
            var idx = range.IndexOf (str, StringComparison.Ordinal);
            return idx < 0 ? -1 : start + idx;
        }

        /// <summary>Returns the line number of the line that contains the specified character position.</summary>
        public int GetLineFromCharIndex (int index)
        {
            var text = Text;
            if (index <= 0 || string.IsNullOrEmpty (text)) return 0;
            index = Math.Min (index, text.Length);
            var line = 0;
            for (var i = 0; i < index; i++)
                if (text[i] == '\n') line++;
            return line;
        }

        /// <summary>Returns the character index of the first character on a given line.</summary>
        public int GetFirstCharIndexFromLine (int lineNumber)
        {
            var text = Text;
            if (string.IsNullOrEmpty (text) || lineNumber <= 0) return 0;
            var line = 0;
            for (var i = 0; i < text.Length; i++) {
                if (text[i] == '\n') {
                    line++;
                    if (line == lineNumber) return i + 1;
                }
            }
            return text.Length;
        }

        /// <summary>Returns the character index of the first character on the current line.</summary>
        public int GetFirstCharIndexOfCurrentLine () => GetFirstCharIndexFromLine (GetLineFromCharIndex (SelectionStart));

        /// <summary>Returns the character index of the character at the specified location. Stub in Majorsilence.Forms.</summary>
        public int GetCharIndexFromPosition (System.Drawing.Point pt) => SelectionStart;

        /// <summary>Returns the location of the character at the specified index. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Point GetPositionFromCharIndex (int index) => System.Drawing.Point.Empty;

        /// <summary>Paste from the clipboard into the specified format. Stub in Majorsilence.Forms (pastes plain text).</summary>
        public void Paste (DataFormat clipFormat) => base.Paste ();

        /// <summary>Raised when the control's contents are resized. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<ContentsResizedEventArgs>? ContentsResized { add { } remove { } }

        /// <summary>Raised when the user clicks a link in the RichTextBox. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<LinkClickedEventArgs>? LinkClicked { add { } remove { } }

        /// <summary>Raised when the selection changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? SelectionChanged { add { } remove { } }

        private static string StripRtf (string rtf)
        {
            if (string.IsNullOrEmpty (rtf) || !rtf.StartsWith (@"{\rtf", StringComparison.Ordinal))
                return rtf;

            var result = new System.Text.StringBuilder ();
            var depth = 0;
            var i = 0;

            while (i < rtf.Length) {
                var c = rtf[i];

                if (c == '{') {
                    depth++;
                    i++;
                } else if (c == '}') {
                    depth--;
                    i++;
                } else if (c == '\\') {
                    i++;
                    if (i >= rtf.Length)
                        break;

                    if (rtf[i] == '\\' || rtf[i] == '{' || rtf[i] == '}') {
                        if (depth == 1)
                            result.Append (rtf[i]);
                        i++;
                    } else if (rtf[i] == '\n' || rtf[i] == '\r') {
                        i++;
                    } else {
                        // skip control word
                        while (i < rtf.Length && char.IsLetter (rtf[i]))
                            i++;
                        // skip optional numeric parameter
                        if (i < rtf.Length && (rtf[i] == '-' || char.IsDigit (rtf[i]))) {
                            while (i < rtf.Length && (rtf[i] == '-' || char.IsDigit (rtf[i])))
                                i++;
                        }
                        // consume trailing space delimiter
                        if (i < rtf.Length && rtf[i] == ' ')
                            i++;
                    }
                } else {
                    if (depth == 1)
                        result.Append (c);
                    i++;
                }
            }

            return result.ToString ().Trim ();
        }
    }

    /// <summary>Specifies the type of scroll bars shown in a RichTextBox.</summary>
    public enum RichTextBoxScrollBars
    {
        /// <summary>No scroll bars.</summary>
        None,
        /// <summary>Only horizontal scroll bars.</summary>
        Horizontal,
        /// <summary>Only vertical scroll bars.</summary>
        Vertical,
        /// <summary>Both horizontal and vertical scroll bars.</summary>
        Both,
        /// <summary>Forced horizontal scroll bar.</summary>
        ForcedHorizontal,
        /// <summary>Forced vertical scroll bar.</summary>
        ForcedVertical,
        /// <summary>Both forced scroll bars.</summary>
        ForcedBoth
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
    }

    /// <summary>WinForms compatibility: provides static members for clipboard data formats. Stub in Majorsilence.Forms.</summary>
    public static class DataFormats
    {
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

        /// <summary>Returns the format with the specified name.</summary>
        public static DataFormat GetFormat (string format) => new DataFormat (format, 0);
    }
}
