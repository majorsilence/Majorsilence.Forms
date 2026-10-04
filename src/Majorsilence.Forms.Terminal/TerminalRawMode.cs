using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Puts the terminal into the mode a TUI needs: keys arrive immediately and unechoed, as bytes. Ctrl+C
    /// still raises SIGINT so the app always has an exit. Always paired with <see cref="Leave"/>, which
    /// restores exactly what <see cref="Enter"/> found.
    ///
    /// Unix drives <c>stty</c> (inherits the real stdin, works the same on Linux and macOS, and avoids a
    /// per-platform <c>termios</c> layout); Windows sets the console modes through kernel32.
    /// </summary>
    internal sealed class TerminalRawMode
    {
        private string? _savedStty;
        private uint _savedInMode, _savedOutMode;
        private bool _active;

        /// <summary>Gets whether the terminal is in raw mode. False after a failed <see cref="Enter"/>, in which case input is unavailable.</summary>
        public bool IsActive => _active;

        public bool Enter ()
        {
            if (_active)
                return true;

            try {
                _active = OperatingSystem.IsWindows () ? EnterWindows () : EnterUnix ();
            } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException) {
                _active = false;   // no stty / no console: display-only
            }

            return _active;
        }

        public void Leave ()
        {
            if (!_active)
                return;
            _active = false;

            try {
                if (OperatingSystem.IsWindows ())
                    LeaveWindows ();
                else if (_savedStty is not null)
                    Stty (_savedStty);
            } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) {
                // The terminal is already gone; nothing left to restore.
            }
        }

        // ── Unix ──────────────────────────────────────────────────────────────

        private bool EnterUnix ()
        {
            var saved = Stty ("-g")?.Trim ();
            if (string.IsNullOrEmpty (saved))
                return false;   // stdin is not a terminal

            _savedStty = saved;
            // -icanon/-echo: bytes immediately, unechoed. -ixon: Ctrl+S/Q reach the app. -icrnl: Enter
            // arrives as CR, distinct from Ctrl+J. isig stays on so Ctrl+C is still SIGINT.
            return Stty ("-icanon -echo -ixon -icrnl min 1 time 0") is not null;
        }

        // Runs stty against the real terminal (stdin is inherited) and returns its stdout, or null on failure.
        private static string? Stty (string args)
        {
            var info = new ProcessStartInfo ("stty", args) {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start (info);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd ();
            process.WaitForExit ();
            return process.ExitCode == 0 ? output : null;
        }

        // ── Windows ───────────────────────────────────────────────────────────

        private const int StdInput = -10, StdOutput = -11;
        private const uint EnableProcessedInput = 0x0001, EnableLineInput = 0x0002, EnableEchoInput = 0x0004, EnableVirtualTerminalInput = 0x0200;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        [DllImport ("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle (int handle);
        [DllImport ("kernel32.dll", SetLastError = true)] private static extern bool GetConsoleMode (IntPtr handle, out uint mode);
        [DllImport ("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleMode (IntPtr handle, uint mode);

        private bool EnterWindows ()
        {
            var input = GetStdHandle (StdInput);
            var output = GetStdHandle (StdOutput);
            if (!GetConsoleMode (input, out _savedInMode) || !GetConsoleMode (output, out _savedOutMode))
                return false;

            // The output side must interpret escape sequences at all, or the frame prints as garbage.
            if (!SetConsoleMode (output, _savedOutMode | EnableVirtualTerminalProcessing))
                return false;

            // VT input turns keys and mouse reports into the same byte sequences a Unix terminal sends.
            // Processed input stays on so Ctrl+C remains an exit.
            var mode = (_savedInMode | EnableVirtualTerminalInput | EnableProcessedInput) & ~(EnableLineInput | EnableEchoInput);
            return SetConsoleMode (input, mode);
        }

        private void LeaveWindows ()
        {
            SetConsoleMode (GetStdHandle (StdInput), _savedInMode);
            SetConsoleMode (GetStdHandle (StdOutput), _savedOutMode);
        }
    }
}
