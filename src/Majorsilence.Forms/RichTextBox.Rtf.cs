using System;
using System.Globalization;
using System.Text;

namespace Majorsilence.Forms
{
    public partial class RichTextBox
    {
        // The RTF reader and writer live here rather than in RichTextBox.cs, which is a long list of
        // properties; this is the one part of the control with real logic in it.
        //
        // Neither is a full RTF implementation and neither pretends to be. The writer emits a minimal
        // single-font document; the reader keeps the text and the paragraph breaks and throws the
        // formatting away. What matters is that they are a matched pair over PLAIN TEXT: what the
        // control shows is what a save writes, and what a load reads is what the control shows. The
        // getter used to return a string only the setter ever wrote (TXT-04, P0).

        /// <summary>
        /// Renders the control's current text as a minimal RTF document.
        /// </summary>
        /// <remarks>
        /// Character formatting is NOT serialised: the run styles from the <c>Selection*</c> family
        /// are painted but not written out, because that needs a colour table and per-run control
        /// words this writer does not emit. A save therefore preserves the text and the paragraphs
        /// and loses the colours -- which is a documented limit, where returning a stale string was
        /// silent data loss.
        /// </remarks>
        private string ToRtf ()
        {
            var text = Text;
            var font = Font;

            // \fsN is in HALF-points, hence the doubling.
            var half_points = (int)Math.Round (font.SizeInPoints * 2, MidpointRounding.AwayFromZero);
            var rtf = new StringBuilder ();

            rtf.Append (@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil ")
               .Append (EscapeRtf (font.Name))
               .Append (";}}")
               .Append (@"\f0\fs")
               .Append (half_points.ToString (CultureInfo.InvariantCulture))
               .Append (' ');

            // A newline is a paragraph break, not a character: \par is what WordPad and the original
            // WinForms control both write, and what this control's reader turns back into '\n'.
            for (var i = 0; i < text.Length; i++) {
                var c = text[i];

                switch (c) {
                    case '\r':
                        // Swallow CR of a CRLF pair; a bare CR is a break in its own right.
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                            continue;

                        rtf.Append (@"\par").Append ('\n');
                        continue;
                    case '\n':
                        rtf.Append (@"\par").Append ('\n');
                        continue;
                    case '\t':
                        rtf.Append (@"\tab ");
                        continue;
                    case '\\':
                    case '{':
                    case '}':
                        rtf.Append ('\\').Append (c);
                        continue;
                }

                if (c < 0x80) {
                    rtf.Append (c);
                    continue;
                }

                // \uN takes a SIGNED 16-bit value, and the '?' is the fallback an ANSI-only reader
                // shows instead. \uc1 is the default, so exactly one fallback character is skipped.
                rtf.Append (@"\u")
                   .Append (((short)c).ToString (CultureInfo.InvariantCulture))
                   .Append ('?');
            }

            rtf.Append ('}');

            return rtf.ToString ();
        }

        private static string EscapeRtf (string value)
        {
            var escaped = new StringBuilder (value.Length);

            foreach (var c in value)
                if (c == '\\' || c == '{' || c == '}')
                    escaped.Append ('\\').Append (c);
                else
                    escaped.Append (c);

            return escaped.ToString ();
        }

        // Groups whose contents are metadata rather than document text. Everything inside one of
        // these is skipped wholesale, along with any `{\*\...}` destination group.
        private static readonly string[] skipped_destinations =
            ["fonttbl", "colortbl", "stylesheet", "info", "pict", "listtable", "listoverridetable",
             "generator", "themedata", "colorschememapping", "latentstyles", "datastore", "xmlnstbl"];

        /// <summary>
        /// Extracts the plain text from an RTF document.
        /// </summary>
        /// <remarks>
        /// Was a control-word skipper, which is wrong in three ways that all corrupt real documents
        /// (finding <c>TXT-15</c>): <c>\par</c> and <c>\tab</c> vanished instead of becoming a newline
        /// and a tab, so every paragraph of a loaded document ran together on one line; <c>\'e9</c>
        /// and <c>\u233?</c> were dropped or emitted literally, so accented text came back as
        /// <c>'e9</c>; and only text at group depth 1 was kept, so anything a real writer wrapped in a
        /// <c>{...}</c> run -- which WordPad, Word and RichEdit all do routinely -- disappeared
        /// entirely. Because the getter then saved this back (TXT-04), the damage was persisted.
        /// </remarks>
        private static string StripRtf (string rtf)
        {
            if (string.IsNullOrEmpty (rtf) || !rtf.StartsWith (@"{\rtf", StringComparison.Ordinal))
                return rtf;

            var result = new StringBuilder ();

            // The depth at which a skipped destination started, or -1 when text is being kept. Groups
            // nest, so a depth is needed rather than a flag: `{\fonttbl{\f0 Arial;}}` closes the skip
            // only on the OUTER brace.
            var skipping_from = -1;
            var depth = 0;

            // How many characters after a \uN escape are the ANSI fallback, from \ucN (default 1).
            var unicode_fallback = 1;
            var i = 0;

            while (i < rtf.Length) {
                var c = rtf[i];

                if (c == '{') {
                    depth++;
                    i++;
                    continue;
                }

                if (c == '}') {
                    if (skipping_from == depth)
                        skipping_from = -1;

                    depth--;
                    i++;
                    continue;
                }

                if (c != '\\') {
                    // Literal newlines in the markup are formatting of the FILE, not of the document.
                    if (c != '\r' && c != '\n' && skipping_from < 0)
                        result.Append (c);

                    i++;
                    continue;
                }

                i++;   // past the backslash

                if (i >= rtf.Length)
                    break;

                var control = rtf[i];

                // An escaped literal.
                if (control == '\\' || control == '{' || control == '}') {
                    if (skipping_from < 0)
                        result.Append (control);

                    i++;
                    continue;
                }

                // `{\*\destination ...}` -- an extension group a reader that does not know it must skip.
                if (control == '*') {
                    if (skipping_from < 0)
                        skipping_from = depth;

                    i++;
                    continue;
                }

                // \'xx -- one byte, written as two hex digits.
                if (control == '\'') {
                    if (i + 2 < rtf.Length
                        && TryParseHex (rtf.Substring (i + 1, 2), out var b)) {
                        if (skipping_from < 0)
                            result.Append (AnsiToChar (b));

                        i += 3;
                        continue;
                    }

                    i++;
                    continue;
                }

                // A line break in the markup after a backslash: \<newline> means \par in some writers.
                if (control == '\r' || control == '\n') {
                    if (skipping_from < 0)
                        result.Append ('\n');

                    i++;
                    continue;
                }

                if (!char.IsLetter (control)) {
                    i++;
                    continue;
                }

                var word_start = i;

                while (i < rtf.Length && char.IsLetter (rtf[i]))
                    i++;

                var word = rtf.Substring (word_start, i - word_start);
                var has_parameter = false;
                var parameter = 0;
                var negative = false;

                if (i < rtf.Length && (rtf[i] == '-' || char.IsDigit (rtf[i]))) {
                    has_parameter = true;
                    negative = rtf[i] == '-';

                    if (negative)
                        i++;

                    while (i < rtf.Length && char.IsDigit (rtf[i])) {
                        parameter = parameter * 10 + (rtf[i] - '0');
                        i++;
                    }

                    if (negative)
                        parameter = -parameter;
                }

                // A single space after a control word is its delimiter and not content.
                if (i < rtf.Length && rtf[i] == ' ')
                    i++;

                if (Array.IndexOf (skipped_destinations, word) >= 0) {
                    if (skipping_from < 0)
                        skipping_from = depth;

                    continue;
                }

                if (word == "uc" && has_parameter) {
                    unicode_fallback = Math.Max (0, parameter);
                    continue;
                }

                if (word == "u" && has_parameter) {
                    if (skipping_from < 0)
                        result.Append ((char)(ushort)parameter);

                    // Skip the ANSI fallback that follows, which may itself be a \'xx escape.
                    i = SkipUnicodeFallback (rtf, i, unicode_fallback);
                    continue;
                }

                if (skipping_from >= 0)
                    continue;

                switch (word) {
                    case "par":
                    case "line":
                    case "sect":
                        result.Append ('\n');
                        break;
                    case "tab":
                        result.Append ('\t');
                        break;
                    case "emdash":
                        result.Append ('—');
                        break;
                    case "endash":
                        result.Append ('–');
                        break;
                    case "lquote":
                        result.Append ('‘');
                        break;
                    case "rquote":
                        result.Append ('’');
                        break;
                    case "ldblquote":
                        result.Append ('“');
                        break;
                    case "rdblquote":
                        result.Append ('”');
                        break;
                    case "bullet":
                        result.Append ('•');
                        break;
                    case "nbsp":
                        result.Append (' ');
                        break;
                }
            }

            // The closing \par of the last paragraph is a terminator, not an empty line.
            var text = result.ToString ();

            // Indexed rather than EndsWith: the char overload CA1865 asks for does not exist on
            // netstandard2.0, and the string overload is what the analyzer objects to.
            return text.Length > 0 && text[text.Length - 1] == '\n' ? text.Substring (0, text.Length - 1) : text;
        }

        private static int SkipUnicodeFallback (string rtf, int i, int count)
        {
            for (var skipped = 0; skipped < count && i < rtf.Length; skipped++) {
                if (rtf[i] == '\\' && i + 3 < rtf.Length && rtf[i + 1] == '\'') {
                    i += 4;   // \'xx
                    continue;
                }

                if (rtf[i] == '{' || rtf[i] == '}' || rtf[i] == '\\')
                    break;

                i++;
            }

            return i;
        }

        private static bool TryParseHex (string value, out int result)
            => int.TryParse (value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);

        // \'xx is a byte in the document's code page. cp1252 agrees with Latin-1 everywhere except
        // 0x80-0x9F, so those 32 are mapped explicitly and everything else is the byte itself.
        private static char AnsiToChar (int b)
        {
            if (b < 0x80 || b > 0x9F)
                return (char)b;

            return cp1252_high[b - 0x80];
        }

        private static readonly char[] cp1252_high =
        [
            // 0x80-0x9F. Escapes rather than glyphs: five of these code points are UNDEFINED in
            // cp1252 (0x81, 0x8D, 0x8F, 0x90, 0x9D) and have no character to paste.
            '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
            '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
            '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
            '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178',
        ];

        // Whether a stream type means RTF. RichNoOleObjs is RTF without embedded objects, and this
        // control has no embedded objects to leave out.
        private static bool IsRichText (RichTextBoxStreamType type)
            => type == RichTextBoxStreamType.RichText || type == RichTextBoxStreamType.RichNoOleObjs;
    }
}
