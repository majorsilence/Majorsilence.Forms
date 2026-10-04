using System;
using System.IO;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Owns the terminal while the app runs: the alternate screen, the hidden cursor, and the byte stream
    /// the encoder writes to. Disposing (or process exit) always puts the terminal back, because a crashed
    /// TUI that leaves the cursor hidden and the screen in the alternate buffer is a bad experience.
    /// </summary>
    internal sealed class TerminalSession : IDisposable
    {
        private readonly bool _alternateScreen;
        private readonly Stream _out;
        private bool _active;

        public TerminalSession (bool alternateScreen, Stream? output = null)
        {
            _alternateScreen = alternateScreen;
            _out = output ?? Console.OpenStandardOutput ();
        }

        public Stream Output => _out;

        // Any-motion + drag tracking, SGR coordinates (no 223-column limit), bracketed paste.
        private const string InputOn = "\u001b[?1000h\u001b[?1002h\u001b[?1003h\u001b[?1006h\u001b[?2004h";
        private const string InputOff = "\u001b[?2004l\u001b[?1006l\u001b[?1003l\u001b[?1002l\u001b[?1000l";

        // Pixel coordinates in mouse reports (needs 1006). A terminal that does not know it ignores it.
        private const string MousePixelsOn = "\u001b[?1016h";
        private const string MousePixelsOff = "\u001b[?1016l";

        private bool _input;
        private bool _mousePixels;
        private bool _kittyKeyboard;

        // Kitty keyboard protocol: push flags 1|2|4|8|16 = disambiguate escape codes, report press/repeat/release,
        // report alternate keys, report every key as an escape code, report the text a key types. Pushed onto a
        // stack and popped on the way out, so the user's shell gets its own keyboard mode back.
        private const string KittyKeyboardOn = "\u001b[>31u";
        private const string KittyKeyboardOff = "\u001b[<u";

        /// <summary>Takes over the terminal; <paramref name="input"/> also asks it to report the mouse and pastes, in pixels when <paramref name="mousePixels"/>.</summary>
        public void Enter (bool input = false, bool mousePixels = false)
        {
            if (_active)
                return;
            _active = true;
            _input = input;
            _mousePixels = input && mousePixels;

            // ?7l: no autowrap, so painting the bottom-right cell cannot scroll the screen.
            Raw ((_alternateScreen ? "\u001b[?1049h" : string.Empty) + "\u001b[?25l\u001b[?7l\u001b[2J" + (input ? InputOn : string.Empty) + (_mousePixels ? MousePixelsOn : string.Empty));
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        public void Leave ()
        {
            if (!_active)
                return;
            _active = false;

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Raw ((_kittyKeyboard ? KittyKeyboardOff : string.Empty) + (_mousePixels ? MousePixelsOff : string.Empty) + (_input ? InputOff : string.Empty) + "\u001b[0m\u001b[?7h\u001b[?25h" + (_alternateScreen ? "\u001b[?1049l" : "\n"));
        }

        public bool IsActive => _active;

        /// <summary>Gets whether the Kitty keyboard protocol was switched on, so key releases and exact modifiers are reported.</summary>
        public bool KittyKeyboard => _kittyKeyboard;

        /// <summary>Switches the Kitty keyboard protocol on. Only call it once the terminal has answered a query for it.</summary>
        public void EnableKittyKeyboard ()
        {
            if (!_active || _kittyKeyboard)
                return;
            _kittyKeyboard = true;
            Raw (KittyKeyboardOn);
        }

        /// <summary>Switches pixel-coordinate mouse reports on or off to match the graphics mode.</summary>
        public void SetMousePixels (bool pixels)
        {
            if (!_active || !_input || _mousePixels == pixels)
                return;
            _mousePixels = pixels;
            Raw (pixels ? MousePixelsOn : MousePixelsOff);
        }

        public void Raw (string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes (text);
            _out.Write (bytes, 0, bytes.Length);
            _out.Flush ();
        }

        /// <summary>The terminal size in character cells. Falls back to <c>COLUMNS</c>/<c>LINES</c>, then 80×24, when stdout is not a terminal.</summary>
        public static (int Cols, int Rows) GetSize ()
        {
            try {
                var w = Console.WindowWidth;
                var h = Console.WindowHeight;
                if (w > 0 && h > 0)
                    return (w, h);
            } catch (IOException) {
                // Redirected: no console to ask.
            }

            return (EnvInt ("COLUMNS", 80), EnvInt ("LINES", 24));
        }

        private static int EnvInt (string name, int fallback)
            => int.TryParse (Environment.GetEnvironmentVariable (name), out var v) && v > 0 ? v : fallback;

        private void OnProcessExit (object? sender, EventArgs e) => Leave ();

        public void Dispose () => Leave ();
    }
}
