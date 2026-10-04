using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// Reads stdin on a background thread, decodes it, and hands each batch of events to a sink (the
    /// backend, which posts them to the UI thread). A blocking read cannot be cancelled, so the thread is a
    /// background thread that simply dies with the process.
    /// </summary>
    internal sealed class TerminalInputReader : IDisposable
    {
        // How long a lone ESC waits for the rest of a sequence before it is taken as the Escape key.
        private const int EscapeTimeoutMs = 40;

        private readonly TerminalInputParser _parser = new ();
        private readonly Action<List<TerminalInput>> _sink;
        private readonly Stream _input;
        private readonly System.Threading.Timer _escapeTimer;
        private volatile bool _stopped;

        public TerminalInputReader (Action<List<TerminalInput>> sink, Stream? input = null)
        {
            _sink = sink;
            _input = input ?? OpenStdin ();
            _escapeTimer = new System.Threading.Timer (_ => FlushPending (), null, Timeout.Infinite, Timeout.Infinite);
        }

        // On a Unix terminal Console.OpenStandardInput () is a line reader that holds every key back until
        // Enter, whatever the terminal mode. Reading file descriptor 0 directly returns bytes as they come.
        private static Stream OpenStdin ()
            => OperatingSystem.IsWindows ()
                ? Console.OpenStandardInput ()
                : new FileStream (new Microsoft.Win32.SafeHandles.SafeFileHandle ((IntPtr) 0, ownsHandle: false), FileAccess.Read, bufferSize: 1, isAsync: false);

        /// <summary>Gets or sets whether mouse reports are pixel coordinates; see <see cref="TerminalInputParser.MousePixels"/>.</summary>
        public bool MousePixels {
            get { lock (_parser) return _parser.MousePixels; }
            set { lock (_parser) _parser.MousePixels = value; }
        }

        public void Start ()
            => new Thread (ReadLoop) { IsBackground = true, Name = "Majorsilence.Forms terminal input" }.Start ();

        private void ReadLoop ()
        {
            var buffer = new byte[4096];
            var events = new List<TerminalInput> ();

            try {
                while (!_stopped) {
                    var n = _input.Read (buffer, 0, buffer.Length);
                    if (n <= 0)
                        break;   // stdin closed

                    TerminalTrace.Bytes (buffer.AsSpan (0, n));
                    events.Clear ();
                    bool pending;
                    lock (_parser) {
                        _parser.Feed (buffer.AsSpan (0, n), events);
                        pending = _parser.HasPending;
                    }

                    if (events.Count > 0)
                        _sink (new List<TerminalInput> (events));

                    _escapeTimer.Change (pending ? EscapeTimeoutMs : Timeout.Infinite, Timeout.Infinite);
                }
            } catch (Exception ex) when (ex is IOException or ObjectDisposedException) {
                // The terminal went away; there is no more input to read.
            }
        }

        private void FlushPending ()
        {
            var events = new List<TerminalInput> ();
            lock (_parser)
                _parser.Flush (events);

            if (events.Count > 0 && !_stopped)
                _sink (events);
        }

        public void Dispose ()
        {
            _stopped = true;
            _escapeTimer.Dispose ();
        }
    }
}
