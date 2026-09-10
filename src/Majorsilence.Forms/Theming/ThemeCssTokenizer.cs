using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Majorsilence.Forms
{
    internal enum CssTokenKind
    {
        Ident,
        Function,     // an ident immediately followed by '(' -- Value is the function name
        AtKeyword,    // '@theme' -- Value is the keyword without '@'
        Hash,         // '#abc' -- Value is the text after '#'
        String,       // "..." or '...' -- Value is the unescaped content
        Number,       // Number is the value; Value is the unit ("" for none, "%" for a percentage)
        Colon,
        Semicolon,
        Comma,
        OpenBrace,
        CloseBrace,
        OpenParen,
        CloseParen,
        Delim,        // any other single character -- Value is that character
        EndOfFile
    }

    internal readonly struct CssToken
    {
        public CssToken (CssTokenKind kind, string value, double number, int line, int column, string raw)
        {
            Kind = kind;
            Value = value;
            Number = number;
            Line = line;
            Column = column;
            Raw = raw;
        }

        public CssTokenKind Kind { get; }
        public string Value { get; }
        public double Number { get; }
        public int Line { get; }
        public int Column { get; }

        /// <summary>The source text of the token, for quoting back in diagnostics.</summary>
        public string Raw { get; }

        public bool Is (CssTokenKind kind) => Kind == kind;
        public bool IsDelim (char c) => Kind == CssTokenKind.Delim && Value.Length == 1 && Value[0] == c;
        public bool IsIdent (string name) => Kind == CssTokenKind.Ident && string.Equals (Value, name, StringComparison.OrdinalIgnoreCase);

        public override string ToString () => Raw;
    }

    // A deliberately small CSS tokenizer: comments, strings, numbers with units, idents (including
    // '--custom-properties'), hashes, functions, at-keywords and the structural punctuation. It never
    // throws -- malformed input becomes Delim tokens or a diagnostic from the caller, so a broken theme
    // reports what is wrong rather than crashing the app that loaded it.
    internal sealed class ThemeCssTokenizer
    {
        private readonly string _text;
        private int _pos;
        private int _line = 1;
        private int _lineStart;
        private readonly List<ThemeCssDiagnostic> _diagnostics;

        public ThemeCssTokenizer (string text, List<ThemeCssDiagnostic> diagnostics)
        {
            _text = text;
            _diagnostics = diagnostics;
        }

        public List<CssToken> Tokenize ()
        {
            var tokens = new List<CssToken> ();

            while (true) {
                SkipWhitespaceAndComments ();

                if (_pos >= _text.Length) {
                    tokens.Add (new CssToken (CssTokenKind.EndOfFile, string.Empty, 0, _line, Column (), string.Empty));
                    return tokens;
                }

                tokens.Add (NextToken ());
            }
        }

        private int Column () => _pos - _lineStart + 1;

        private char Peek (int offset = 0) => _pos + offset < _text.Length ? _text[_pos + offset] : '\0';

        private void Advance ()
        {
            if (_text[_pos] == '\n') {
                _line++;
                _lineStart = _pos + 1;
            }

            _pos++;
        }

        private void SkipWhitespaceAndComments ()
        {
            while (_pos < _text.Length) {
                var c = _text[_pos];

                if (char.IsWhiteSpace (c)) {
                    Advance ();
                } else if (c == '/' && Peek (1) == '*') {
                    var line = _line;
                    var column = Column ();
                    var end = _text.IndexOf ("*/", _pos + 2, StringComparison.Ordinal);

                    if (end < 0) {
                        _diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Error, line, column,
                            "Unterminated comment: a '/*' has no matching '*/'. Everything after it was ignored."));
                        while (_pos < _text.Length)
                            Advance ();
                        return;
                    }

                    while (_pos < end + 2)
                        Advance ();
                } else {
                    return;
                }
            }
        }

        private CssToken NextToken ()
        {
            var line = _line;
            var column = Column ();
            var start = _pos;
            var c = _text[_pos];

            CssToken Make (CssTokenKind kind, string value, double number = 0)
                => new CssToken (kind, value, number, line, column, _text.Substring (start, _pos - start));

            switch (c) {
                case '{': Advance (); return Make (CssTokenKind.OpenBrace, "{");
                case '}': Advance (); return Make (CssTokenKind.CloseBrace, "}");
                case '(': Advance (); return Make (CssTokenKind.OpenParen, "(");
                case ')': Advance (); return Make (CssTokenKind.CloseParen, ")");
                case ':': Advance (); return Make (CssTokenKind.Colon, ":");
                case ';': Advance (); return Make (CssTokenKind.Semicolon, ";");
                case ',': Advance (); return Make (CssTokenKind.Comma, ",");
                case '"':
                case '\'':
                    return ReadString (c, line, column, start);
                case '#':
                    Advance ();
                    var hashStart = _pos;
                    while (_pos < _text.Length && IsNameChar (_text[_pos]))
                        Advance ();
                    return Make (CssTokenKind.Hash, _text.Substring (hashStart, _pos - hashStart));
                case '@':
                    if (IsIdentStart (_pos + 1)) {
                        Advance ();
                        return Make (CssTokenKind.AtKeyword, ReadName ());
                    }
                    break;
            }

            if (IsNumberStart (_pos)) {
                var number = ReadNumber ();
                var unit = string.Empty;

                if (Peek () == '%') {
                    Advance ();
                    unit = "%";
                } else if (IsIdentStart (_pos)) {
                    unit = ReadName ();
                }

                return Make (CssTokenKind.Number, unit, number);
            }

            if (IsIdentStart (_pos)) {
                var name = ReadName ();

                if (Peek () == '(') {
                    Advance ();
                    return Make (CssTokenKind.Function, name);
                }

                return Make (CssTokenKind.Ident, name);
            }

            Advance ();
            return Make (CssTokenKind.Delim, c.ToString ());
        }

        private CssToken ReadString (char quote, int line, int column, int start)
        {
            Advance ();   // opening quote
            var sb = new StringBuilder ();

            while (_pos < _text.Length) {
                var c = _text[_pos];

                if (c == quote) {
                    Advance ();
                    return new CssToken (CssTokenKind.String, sb.ToString (), 0, line, column, _text.Substring (start, _pos - start));
                }

                if (c == '\n') {
                    _diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Error, line, column,
                        "Unterminated string: the closing quote is missing before the end of the line."));
                    return new CssToken (CssTokenKind.String, sb.ToString (), 0, line, column, _text.Substring (start, _pos - start));
                }

                if (c == '\\' && _pos + 1 < _text.Length) {
                    Advance ();
                    sb.Append (_text[_pos]);
                    Advance ();
                    continue;
                }

                sb.Append (c);
                Advance ();
            }

            _diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Error, line, column,
                "Unterminated string: the closing quote is missing."));
            return new CssToken (CssTokenKind.String, sb.ToString (), 0, line, column, _text.Substring (start, _pos - start));
        }

        private string ReadName ()
        {
            var start = _pos;

            while (_pos < _text.Length && IsNameChar (_text[_pos]))
                Advance ();

            return _text.Substring (start, _pos - start);
        }

        private double ReadNumber ()
        {
            var start = _pos;

            if (Peek () == '+' || Peek () == '-')
                Advance ();

            while (char.IsDigit (Peek ()))
                Advance ();

            if (Peek () == '.' && char.IsDigit (Peek (1))) {
                Advance ();
                while (char.IsDigit (Peek ()))
                    Advance ();
            }

#if NETSTANDARD2_0
            return double.Parse (_text.Substring (start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
#else
            return double.Parse (_text.AsSpan (start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
#endif
        }

        private bool IsNumberStart (int index)
        {
            var c = CharAt (index);

            if (char.IsDigit (c))
                return true;

            if (c == '.')
                return char.IsDigit (CharAt (index + 1));

            if (c == '+' || c == '-')
                return char.IsDigit (CharAt (index + 1)) || (CharAt (index + 1) == '.' && char.IsDigit (CharAt (index + 2)));

            return false;
        }

        private bool IsIdentStart (int index)
        {
            var c = CharAt (index);

            if (c == '-')
                return IsIdentStart (index + 1) || CharAt (index + 1) == '-';

            return char.IsLetter (c) || c == '_';
        }

        private char CharAt (int index) => index < _text.Length ? _text[index] : '\0';

        private static bool IsNameChar (char c) => char.IsLetterOrDigit (c) || c == '-' || c == '_';
    }
}
