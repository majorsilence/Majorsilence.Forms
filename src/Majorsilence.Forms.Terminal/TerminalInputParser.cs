using System;
using System.Collections.Generic;
using System.Text;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Decodes the bytes a terminal sends for the keyboard and mouse into <see cref="TerminalInput"/>s.
    /// Handles UTF-8 text, control characters, CSI and SS3 key sequences with modifiers, SGR (1006) mouse
    /// reports and bracketed paste. Stateful because a read can end in the middle of a sequence; a lone
    /// ESC is ambiguous (the Escape key, or the start of a sequence) until more bytes arrive or
    /// <see cref="Flush"/> is called after a short wait.
    ///
    /// A terminal reports only key presses, never releases, so there is no key-up here: the host sends
    /// one straight after each key-down.
    /// </summary>
    internal sealed class TerminalInputParser
    {
        private const int Esc = 0x1B;
        private static readonly byte[] PasteEnd = { Esc, (byte) '[', (byte) '2', (byte) '0', (byte) '1', (byte) '~' };

        private readonly List<byte> _pending = new ();

        /// <summary>
        /// Gets or sets whether mouse reports carry pixel coordinates (xterm's 1016 mode) rather than cells,
        /// which decides <see cref="TerminalInput.InPixels"/> on mouse events.
        /// </summary>
        public bool MousePixels { get; set; }

        /// <summary>Whether bytes are held back waiting for the rest of a sequence.</summary>
        public bool HasPending => _pending.Count > 0;

        /// <summary>Consumes <paramref name="bytes"/>, appending every event now complete to <paramref name="output"/>.</summary>
        public void Feed (ReadOnlySpan<byte> bytes, List<TerminalInput> output)
        {
            for (var i = 0; i < bytes.Length; i++)
                _pending.Add (bytes[i]);

            var buffer = _pending.ToArray ();
            var offset = 0;
            while (offset < buffer.Length) {
                if (!TryParse (buffer.AsSpan (offset), out var consumed, output))
                    break;
                offset += consumed;
            }

            _pending.RemoveRange (0, offset);
        }

        /// <summary>
        /// Resolves what is held back once no more bytes are coming: a lone ESC is the Escape key, and a
        /// truncated sequence is dropped (guessing at it would type garbage).
        /// </summary>
        public void Flush (List<TerminalInput> output)
        {
            if (_pending.Count > 0 && _pending[0] == Esc && _pending.Count == 1)
                output.Add (new TerminalInput (TerminalInputKind.Key, Keys.Escape));

            _pending.Clear ();
        }

        // Parses one event from the front of the buffer. False means the buffer ends mid-sequence.
        private bool TryParse (ReadOnlySpan<byte> s, out int consumed, List<TerminalInput> output)
        {
            consumed = 1;
            var b = s[0];

            if (b == Esc)
                return TryParseEscape (s, out consumed, output);

            if (b < 0x20 || b == 0x7F) {
                ParseControl (b, Keys.None, output);
                return true;
            }

            return TryParseText (s, Keys.None, out consumed, output);
        }

        private bool TryParseEscape (ReadOnlySpan<byte> s, out int consumed, List<TerminalInput> output)
        {
            consumed = 1;
            if (s.Length == 1)
                return false;

            switch (s[1]) {
                case (byte) '[':
                    return TryParseCsi (s, out consumed, output);
                case (byte) 'O':
                    if (s.Length < 3)
                        return false;
                    consumed = 3;
                    var ss3 = Ss3Key (s[2]);
                    if (ss3 != Keys.None)
                        output.Add (new TerminalInput (TerminalInputKind.Key, ss3));
                    return true;
                case (byte) '_':
                    return TryParseApc (s, out consumed, output);
                case Esc:
                    // ESC ESC: the first is the Escape key; the next starts its own sequence.
                    output.Add (new TerminalInput (TerminalInputKind.Key, Keys.Escape));
                    return true;
            }

            // ESC followed by a character is Alt+that character.
            var rest = s[1..];
            bool done;
            int inner;
            if (rest[0] < 0x20 || rest[0] == 0x7F) {
                ParseControl (rest[0], Keys.Alt, output);
                done = true;
                inner = 1;
            } else {
                done = TryParseText (rest, Keys.Alt, out inner, output);
            }

            if (!done)
                return false;

            consumed = 1 + inner;
            return true;
        }

        private bool TryParseCsi (ReadOnlySpan<byte> s, out int consumed, List<TerminalInput> output)
        {
            consumed = 2;

            // Parameter bytes 0x30-0x3F, intermediates 0x20-0x2F, then one final byte 0x40-0x7E.
            var i = 2;
            while (i < s.Length && s[i] >= 0x20 && s[i] <= 0x3F)
                i++;
            if (i >= s.Length)
                return false;

            var final = (char) s[i];
            var parameters = Encoding.ASCII.GetString (s.Slice (2, i - 2));
            consumed = i + 1;

            if (final == '~' && parameters == "200")
                return TryParsePaste (s, out consumed, output);

            if (parameters.StartsWith ('<') && (final == 'M' || final == 'm')) {
                ParseMouse (parameters[1..], final == 'M', output);
                return true;
            }

            // Private-marker replies to our own queries. Anything else with a marker is not input and is swallowed.
            if (parameters.StartsWith ('?')) {
                if (final == 'c')
                    output.Add (new TerminalInput (TerminalInputKind.DeviceAttributes, Text: parameters[1..]));
                else if (final == 'u' && int.TryParse (parameters.AsSpan (1), out var flags))
                    output.Add (new TerminalInput (TerminalInputKind.KeyboardFlags, Col: flags));
                return true;
            }

            var groups = ParseGroups (parameters);
            var nums = groups.ConvertAll (g => g[0]);

            // Kitty keyboard protocol: CSI code[:shifted[:base]] ; mods[:event] ; text u
            if (final == 'u') {
                ParseKittyKey (groups, output);
                return true;
            }

            // Reply to ESC[16t: ESC[6;<cell height>;<cell width>t.
            if (final == 't') {
                if (nums.Count >= 3 && nums[0] == 6 && nums[1] > 0 && nums[2] > 0)
                    output.Add (new TerminalInput (TerminalInputKind.CellSize, Col: nums[2], Row: nums[1]));
                return true;
            }

            var modifiers = nums.Count >= 2 ? ModifierBits (nums[1]) : Keys.None;
            var kind = groups.Count >= 2 && groups[1].Length >= 2 ? EventKind (groups[1][1]) : KeyEventKind.Press;

            Keys key;
            switch (final) {
                case 'A': key = Keys.Up; break;
                case 'B': key = Keys.Down; break;
                case 'C': key = Keys.Right; break;
                case 'D': key = Keys.Left; break;
                case 'H': key = Keys.Home; break;
                case 'F': key = Keys.End; break;
                case 'P': key = Keys.F1; break;
                case 'Q': key = Keys.F2; break;
                case 'R': key = Keys.F3; break;
                case 'S': key = Keys.F4; break;
                case 'Z': key = Keys.Tab; modifiers |= Keys.Shift; break;
                case '~': key = TildeKey (nums.Count > 0 ? nums[0] : 0); break;
                default: key = Keys.None; break;   // an unknown sequence is swallowed, never typed
            }

            if (key != Keys.None)
                output.Add (new TerminalInput (TerminalInputKind.Key, key | modifiers, Event: kind));
            return true;
        }

        // APC (ESC _ ... ST) carries Kitty graphics replies. Anything else is swallowed whole: its payload must
        // never be typed into a text box.
        private bool TryParseApc (ReadOnlySpan<byte> s, out int consumed, List<TerminalInput> output)
        {
            consumed = s.Length;

            // Terminated by ST (ESC \) or, from some terminals, BEL.
            var end = -1;
            var terminator = 0;
            for (var i = 2; i < s.Length; i++) {
                if (s[i] == 0x07) {
                    end = i;
                    terminator = 1;
                    break;
                }
                if (s[i] == Esc && i + 1 < s.Length && s[i + 1] == (byte) '\\') {
                    end = i;
                    terminator = 2;
                    break;
                }
            }
            if (end < 0)
                return false;

            consumed = end + terminator;
            var body = Encoding.ASCII.GetString (s.Slice (2, end - 2));
            if (!body.StartsWith ('G'))
                return true;

            // G<key=value,...>;<message>: for a reply the message is OK or an error.
            var semi = body.IndexOf (';');
            var control = semi < 0 ? body[1..] : body[1..semi];
            var message = semi < 0 ? string.Empty : body[(semi + 1)..];
            var id = 0;
            foreach (var pair in control.Split (','))
                if (pair.StartsWith ("i=", StringComparison.Ordinal))
                    id = int.TryParse (pair.AsSpan (2), out var parsed) ? parsed : 0;

            output.Add (new TerminalInput (TerminalInputKind.GraphicsReply, Text: message, Col: id));
            return true;
        }

        private static void ParseKittyKey (List<int[]> groups, List<TerminalInput> output)
        {
            if (groups.Count == 0)
                return;

            var code = groups[0][0];
            var shifted = groups[0].Length > 1 ? groups[0][1] : 0;
            var baseLayout = groups[0].Length > 2 ? groups[0][2] : 0;
            var modParam = groups.Count > 1 ? groups[1][0] : 1;
            var kind = groups.Count > 1 && groups[1].Length > 1 ? EventKind (groups[1][1]) : KeyEventKind.Press;
            var modifiers = ModifierBits (modParam);

            // The key's identity is its position on a US layout (the base-layout key when the terminal says
            // which), so Ctrl+Z is Ctrl+Z on any layout; what the key types comes from the text.
            var key = KittyKey (baseLayout > 0 ? baseLayout : code);
            if (key == Keys.None)
                return;

            string? text = null;
            if (kind != KeyEventKind.Release && (modifiers & (Keys.Control | Keys.Alt)) == Keys.None) {
                if (groups.Count > 2 && groups[2].Length > 0 && groups[2][0] != 0)
                    text = ToText (groups[2]);
                else if (IsTextCode (code))
                    // A terminal that does not send the associated text still gets typing: derive it.
                    text = ToText (new[] { (modifiers & Keys.Shift) != Keys.None && shifted > 0 ? shifted : code });
            }

            output.Add (new TerminalInput (TerminalInputKind.Key, key | modifiers, text, Event: kind));
        }

        private static bool IsTextCode (int code) => code >= 32 && code != 127 && code < 57344;

        private static string? ToText (int[] codepoints)
        {
            var sb = new StringBuilder ();
            foreach (var cp in codepoints) {
                if (cp < 32 || cp == 127 || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
                    continue;   // a control character or an invalid scalar is never typed
                sb.Append (char.ConvertFromUtf32 (cp));
            }
            return sb.Length == 0 ? null : sb.ToString ();
        }

        private static KeyEventKind EventKind (int code) => code switch {
            2 => KeyEventKind.Repeat,
            3 => KeyEventKind.Release,
            _ => KeyEventKind.Press,
        };

        // Maps a Kitty key code (a Unicode scalar, or a private-use code for keys with no character).
        private static Keys KittyKey (int code)
        {
            if (code >= 'a' && code <= 'z') return Keys.A + (code - 'a');
            if (code >= 'A' && code <= 'Z') return Keys.A + (code - 'A');
            if (code >= '0' && code <= '9') return Keys.D0 + (code - '0');

            switch (code) {
                case 27: return Keys.Escape;
                case 13: return Keys.Return;
                case 9: return Keys.Tab;
                case 127: return Keys.Back;
                case 32: return Keys.Space;
                case ',': return Keys.Oemcomma;
                case '.': return Keys.OemPeriod;
                case '-': return Keys.OemMinus;
                case '=': return Keys.Oemplus;
                case ';': return Keys.OemSemicolon;
                case '/': return Keys.OemQuestion;
                case '`': return Keys.Oemtilde;
                case '[': return Keys.OemOpenBrackets;
                case ']': return Keys.OemCloseBrackets;
                case '\\': return Keys.OemPipe;
                case '\'': return Keys.OemQuotes;

                // Private-use block: keys with no character (the protocol's table).
                case 57358: return Keys.Capital;
                case 57359: return Keys.Scroll;
                case 57360: return Keys.NumLock;
                case 57361: return Keys.PrintScreen;
                case 57362: return Keys.Pause;
                case 57363: return Keys.Apps;
                case 57414: return Keys.Return;      // keypad Enter
                case 57409: return Keys.Decimal;
                case 57410: return Keys.Divide;
                case 57411: return Keys.Multiply;
                case 57412: return Keys.Subtract;
                case 57413: return Keys.Add;
                case 57441: case 57447: return Keys.ShiftKey;
                case 57442: case 57448: return Keys.ControlKey;
                case 57443: case 57449: return Keys.Menu;      // Alt
            }

            if (code >= 57399 && code <= 57408) return Keys.NumPad0 + (code - 57399);
            if (code >= 57376 && code <= 57387) return Keys.F13 + (code - 57376);   // F13-F24
            return Keys.None;
        }

        private static bool TryParsePaste (ReadOnlySpan<byte> s, out int consumed, List<TerminalInput> output)
        {
            consumed = s.Length;
            const int start = 6;   // past ESC [ 2 0 0 ~
            var body = s[start..];
            var end = body.IndexOf (PasteEnd);
            if (end < 0)
                return false;   // keep buffering until the paste is complete

            consumed = start + end + PasteEnd.Length;
            var text = Encoding.UTF8.GetString (body[..end]);
            if (text.Length > 0)
                output.Add (new TerminalInput (TerminalInputKind.Text, Text: text));
            return true;
        }

        private void ParseMouse (string parameters, bool press, List<TerminalInput> output)
        {
            var nums = ParseNumbers (parameters);
            if (nums.Count < 3)
                return;

            var code = nums[0];
            var col = Math.Max (0, nums[1] - 1);
            var row = Math.Max (0, nums[2] - 1);
            var pixels = MousePixels;

            var modifiers = Keys.None;
            if ((code & 4) != 0) modifiers |= Keys.Shift;
            if ((code & 8) != 0) modifiers |= Keys.Alt;
            if ((code & 16) != 0) modifiers |= Keys.Control;

            if ((code & 64) != 0) {
                if (!press)
                    return;
                const int Notch = 120;
                int wx = 0, wy = 0;
                switch (code & 3) {
                    case 0: wy = Notch; break;
                    case 1: wy = -Notch; break;
                    case 2: wx = -Notch; break;
                    default: wx = Notch; break;
                }
                output.Add (new TerminalInput (TerminalInputKind.Wheel, modifiers, Col: col, Row: row, WheelX: wx, WheelY: wy, InPixels: pixels));
                return;
            }

            var button = (code & 3) switch {
                0 => MouseButtons.Left,
                1 => MouseButtons.Middle,
                2 => MouseButtons.Right,
                _ => MouseButtons.None,
            };

            var kind = (code & 32) != 0 ? TerminalInputKind.MouseMove
                : press ? TerminalInputKind.MouseDown
                : TerminalInputKind.MouseUp;
            output.Add (new TerminalInput (kind, modifiers, Button: button, Col: col, Row: row, InPixels: pixels));
        }

        private static void ParseControl (byte b, Keys extra, List<TerminalInput> output)
        {
            Keys key;
            switch (b) {
                case 0x0D:
                case 0x0A: key = Keys.Return; break;
                case 0x09: key = Keys.Tab; break;
                case 0x08:
                case 0x7F: key = Keys.Back; break;
                case 0x00: key = Keys.Control | Keys.Space; break;
                case >= 0x01 and <= 0x1A: key = Keys.Control | (Keys) (Keys.A + (b - 1)); break;
                default: return;   // Ctrl+\ ] ^ _ have no useful WinForms meaning
            }

            output.Add (new TerminalInput (TerminalInputKind.Key, key | extra));
        }

        private static bool TryParseText (ReadOnlySpan<byte> s, Keys extra, out int consumed, List<TerminalInput> output)
        {
            var lead = s[0];
            var length = lead < 0x80 ? 1 : lead >= 0xF0 ? 4 : lead >= 0xE0 ? 3 : lead >= 0xC0 ? 2 : 1;
            consumed = 1;
            if (s.Length < length)
                return false;

            consumed = length;
            if (lead >= 0x80 && length == 1)
                return true;   // a stray continuation byte

            var text = Encoding.UTF8.GetString (s[..length]);
            var key = Keys.None;
            if (text.Length == 1) {
                var c = text[0];
                if (c >= 'a' && c <= 'z') key = (Keys) (Keys.A + (c - 'a'));
                else if (c >= 'A' && c <= 'Z') key = (Keys) (Keys.A + (c - 'A')) | Keys.Shift;
                else if (c >= '0' && c <= '9') key = (Keys) (Keys.D0 + (c - '0'));
                else if (c == ' ') key = Keys.Space;
            }

            // Alt+x is a shortcut, not typing.
            output.Add (new TerminalInput (TerminalInputKind.Key, key | extra, (extra & Keys.Alt) != 0 ? null : text));
            return true;
        }

        private static Keys Ss3Key (byte b) => (char) b switch {
            'A' => Keys.Up,
            'B' => Keys.Down,
            'C' => Keys.Right,
            'D' => Keys.Left,
            'H' => Keys.Home,
            'F' => Keys.End,
            'P' => Keys.F1,
            'Q' => Keys.F2,
            'R' => Keys.F3,
            'S' => Keys.F4,
            _ => Keys.None,
        };

        private static Keys TildeKey (int n) => n switch {
            1 or 7 => Keys.Home,
            2 => Keys.Insert,
            3 => Keys.Delete,
            4 or 8 => Keys.End,
            5 => Keys.PageUp,
            6 => Keys.PageDown,
            11 => Keys.F1,
            12 => Keys.F2,
            13 => Keys.F3,
            14 => Keys.F4,
            15 => Keys.F5,
            17 => Keys.F6,
            18 => Keys.F7,
            19 => Keys.F8,
            20 => Keys.F9,
            21 => Keys.F10,
            23 => Keys.F11,
            24 => Keys.F12,
            _ => Keys.None,
        };

        // xterm's modifier parameter is 1 + (shift 1 | alt 2 | ctrl 4 | meta 8).
        private static Keys ModifierBits (int param)
        {
            var bits = Math.Max (0, param - 1);
            var keys = Keys.None;
            if ((bits & 1) != 0) keys |= Keys.Shift;
            if ((bits & 2) != 0) keys |= Keys.Alt;
            if ((bits & 4) != 0) keys |= Keys.Control;
            return keys;
        }

        private static List<int> ParseNumbers (string parameters)
            => ParseGroups (parameters).ConvertAll (g => g[0]);

        // "97:65;5:3;97" -> [[97, 65], [5, 3], [97]]: ';' separates parameters, ':' sub-parameters, empty means 0.
        private static List<int[]> ParseGroups (string parameters)
        {
            var list = new List<int[]> ();
            foreach (var part in parameters.Split (';')) {
                var subs = part.Split (':');
                var values = new int[subs.Length];
                for (var i = 0; i < subs.Length; i++)
                    values[i] = int.TryParse (subs[i], out var n) ? n : 0;
                list.Add (values);
            }
            return list;
        }
    }
}
