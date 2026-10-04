using System;
using System.IO;
using System.Text;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// An opt-in log of what the terminal sent and what the host decided, for finding out why an emulator
    /// behaves differently from the documentation. Set <c>MF_TERMINAL_TRACE</c> to a file path. Off by
    /// default and costing one null check when off.
    /// </summary>
    internal static class TerminalTrace
    {
        private static readonly string? path = Environment.GetEnvironmentVariable ("MF_TERMINAL_TRACE");
        private static readonly object gate = new ();

        public static bool Enabled => !string.IsNullOrEmpty (path);

        public static void Write (string line)
        {
            if (!Enabled)
                return;

            lock (gate)
                File.AppendAllText (path!, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }

        /// <summary>Logs bytes read from the terminal with control characters spelled out, so an escape sequence is readable.</summary>
        public static void Bytes (ReadOnlySpan<byte> bytes)
        {
            if (!Enabled)
                return;

            var sb = new StringBuilder ("read: ");
            foreach (var b in bytes) {
                if (b == 0x1B) sb.Append ("<ESC>");
                else if (b < 0x20 || b == 0x7F) sb.Append ($"<{b:X2}>");
                else sb.Append ((char) b);
            }
            Write (sb.ToString ());
        }
    }
}
