using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Majorsilence.Forms.Backends;
using SkiaSharp;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// An <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms in a terminal. The main window
    /// fills the terminal, drawn by the shared SkiaSharp pipeline into an offscreen bitmap that
    /// <see cref="TerminalFrameEncoder"/> then prints as ANSI half-blocks. Popups (menus, dropdowns) are
    /// composited over it by z-order. Display only for now: input is not wired yet, and Ctrl+C exits.
    /// </summary>
    public sealed class TerminalPlatformBackend : IPlatformBackend, IDisposable
    {
        private readonly TerminalOptions _options;
        private readonly TerminalColorMode _colorMode;
        private TerminalGraphicsMode _graphics;
        private readonly bool _modeFixed;          // set by the app or MF_TERMINAL_GRAPHICS: the terminal query does not override it
        private TerminalProbe? _probe;             // non-null until the terminal has answered (or time ran out)
        private long _probeDeadline;
        private const int ProbeTimeoutMs = 400;
        private ITerminalFramePresenter? _presenter;
        private (int W, int H) _cellPx;
        private (int W, int H)? _terminalCell;     // what the terminal said a cell is, in pixels, in any mode
        private readonly ArrayBufferWriter<byte> _buffer = new ();

        private readonly ConcurrentQueue<Action> _queue = new ();
        private readonly AutoResetEvent _signal = new (false);
        private readonly List<TerminalWindowHost> _shown = new ();
        private TerminalSession? _session;
        private readonly TerminalRawMode _rawMode = new ();
        private TerminalInputReader? _reader;
        private TerminalWindowHost? _active;     // receives the keyboard
        private TerminalWindowHost? _captured;   // owns a mouse drag until the button comes up
        private volatile bool _running;
        private volatile bool _dirty;
        private int _uiThreadId = -1;
        private string _clipboard = string.Empty;
        private (int Cols, int Rows) _cells = (80, 24);

        internal TerminalPlatformBackend (TerminalOptions options, Func<string, string?>? getEnv = null)
        {
            getEnv ??= Environment.GetEnvironmentVariable;
            _options = options;
            _colorMode = options.ColorMode ?? TerminalCapabilities.DetectColorMode (getEnv);
            // The environment gives a first guess; once input is running the terminal is asked and answers win
            // (unless the app or MF_TERMINAL_GRAPHICS pinned a mode).
            _modeFixed = options.GraphicsMode.HasValue || TerminalCapabilities.ExplicitGraphicsMode (getEnv).HasValue;
            _graphics = options.GraphicsMode ?? TerminalCapabilities.DetectGraphicsMode (getEnv);
            // Half-blocks are 1x2 pixels per cell by construction; a graphics mode learns the real size from
            // the terminal and starts from a common guess.
            _cellPx = _graphics == TerminalGraphicsMode.HalfBlock ? (1, 2) : (8, 16);
            _cells = TerminalSession.GetSize ();
        }

        /// <inheritdoc/>
        public string Name => "Terminal";

        /// <summary>Gets the colour mode in use, whether configured or detected. Only half-block output is limited by it.</summary>
        public TerminalColorMode ColorMode => _colorMode;

        /// <summary>Gets how frames reach the terminal, whether configured or detected.</summary>
        public TerminalGraphicsMode GraphicsMode => _graphics;

        internal double Scaling => _options.Scaling > 0 ? _options.Scaling : 1.0;

        /// <summary>The drawable area in device pixels: a half-block cell is 1x2, a graphics cell is the terminal's real cell size.</summary>
        internal System.Drawing.Size PixelSize {
            get {
                // Sixel leaves the last row unused: an image reaching the bottom edge makes the terminal scroll.
                var rows = _graphics == TerminalGraphicsMode.Sixel ? Math.Max (1, _cells.Rows - 1) : _cells.Rows;
                return new (_cells.Cols * _cellPx.W, rows * _cellPx.H);
            }
        }

        /// <summary>The size of one character cell in device pixels.</summary>
        internal (int W, int H) CellPixels => _cellPx;

        private ITerminalFramePresenter Presenter => _presenter ??= _graphics switch {
            TerminalGraphicsMode.Kitty => new TerminalKittyEncoder (_cells.Cols, _cells.Rows),
            TerminalGraphicsMode.Sixel => new TerminalSixelEncoder (_cellPx.W, _cellPx.H),
            _ => new TerminalFrameEncoder (_colorMode),
        };

        // Rebuilds the presenter and wipes the screen after the cell grid or cell size changed.
        private void ResetScreen ()
        {
            _presenter?.Reset ();
            _presenter = null;
            _session?.Raw ("\u001b[2J" + (_graphics == TerminalGraphicsMode.Kitty ? "\u001b_Ga=d,d=A,q=2\u001b\\" : string.Empty));
            QueryCellSize ();
            Invalidate ();
        }

        // ESC[16t asks for the cell size in pixels; the answer arrives as input (CellSize).
        private void QueryCellSize ()
        {
            if (_reader is not null)
                _session?.Raw ("\u001b[16t");
        }

        /// <inheritdoc/>
        public void Initialize ()
        {
            if (_uiThreadId == -1)
                _uiThreadId = Environment.CurrentManagedThreadId;
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
        {
            _uiThreadId = Environment.CurrentManagedThreadId;
            _running = true;
            ExitRequested = false;

            // Input is optional: with stdin redirected, or no stty, the app still renders.
            var input = !Console.IsInputRedirected && _rawMode.Enter ();
            _session = new TerminalSession (_options.UseAlternateScreen);
            // Kitty-family terminals all report pixel mouse positions on request; for Sixel, a cell centre is the best available.
            var mousePixels = _graphics == TerminalGraphicsMode.Kitty;
            _session.Enter (input, mousePixels);
            if (input) {
                _reader = new TerminalInputReader (events => Post (() => Dispatch (events))) { MousePixels = mousePixels };
                _reader.Start ();
                QueryCellSize ();

                // Ask what the terminal supports; the first frame waits for the answer.
                _probe = new TerminalProbe ();
                _probeDeadline = Environment.TickCount64 + ProbeTimeoutMs;
                _session.Raw (TerminalProbe.Query);
            }

            // Raw mode is not entered, so the terminal still turns Ctrl+C into SIGINT; swallow it and stop
            // the loop cleanly so the screen is restored.
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; Stop (); };
            Console.CancelKeyPress += cancel;
            using var registration = token.Register (() => _signal.Set ());

            try {
                while (_running && !token.IsCancellationRequested)
                    Pump (waitMs: 50);

                DrainQueue ();
            } finally {
                Console.CancelKeyPress -= cancel;
                _reader?.Dispose ();
                _reader = null;
                if (_presenter is { ExitSequence.Length: > 0 } presenter)
                    _session.Raw (presenter.ExitSequence);
                _session.Dispose ();
                _rawMode.Leave ();
                _session = null;
            }
        }

        // One turn of the loop: run posted work, notice a resized terminal, repaint if anything asked to.
        private void Pump (int waitMs)
        {
            DrainQueue ();
            Application.RaiseIdle ();
            PollSize ();

            if (_probe is not null && !ProbeFinished ()) {
                _signal.WaitOne (Math.Min (waitMs, 20));
                return;
            }

            if (_dirty)
                Present ();
            else
                _signal.WaitOne (waitMs);
        }

        private bool ProbeFinished ()
        {
            if (_probe is { Complete: false } && Environment.TickCount64 < _probeDeadline)
                return false;

            var probe = _probe!;
            _probe = null;
            ApplyProbe (probe);
            return true;
        }

        /// <summary>Gets whether the Kitty keyboard protocol is on, so key releases are real and not synthesised.</summary>
        internal bool KittyKeyboard { get; private set; }

        // Applies the terminal's answers. A terminal that never answered keeps the environment's guess.
        internal void ApplyProbe (TerminalProbe probe)
        {
            if (!_modeFixed && (probe.Complete || probe.KittyGraphics) && probe.Decide () != _graphics)
                SwitchMode (probe.Decide ());

            if (probe.KittyKeyboard) {
                KittyKeyboard = true;
                _session?.EnableKittyKeyboard ();
            }

            Invalidate ();
        }

        private void SwitchMode (TerminalGraphicsMode mode)
        {
            _graphics = mode;
            _cellPx = mode == TerminalGraphicsMode.HalfBlock ? (1, 2) : _terminalCell ?? (_cellPx.H > 2 ? _cellPx : (8, 16));

            var mousePixels = mode == TerminalGraphicsMode.Kitty;
            _session?.SetMousePixels (mousePixels);
            if (_reader is not null)
                _reader.MousePixels = mousePixels;

            ResetScreen ();
        }

        private void PollSize ()
        {
            var size = TerminalSession.GetSize ();
            if (size == _cells)
                return;

            _cells = size;
            ResetScreen ();
        }

        internal void Invalidate ()
        {
            _dirty = true;
            _signal.Set ();
        }

        internal void Shown (TerminalWindowHost host, bool activate)
        {
            lock (_shown) {
                _shown.Remove (host);
                _shown.Add (host);
            }
            if (activate)
                _active = host;
            Invalidate ();
        }

        internal void Hidden (TerminalWindowHost host)
        {
            lock (_shown) {
                _shown.Remove (host);
                if (_active == host)
                    _active = null;
                if (_captured == host)
                    _captured = null;
            }
            _presenter?.Reset ();
            Invalidate ();
        }

        private void Present ()
        {
            _dirty = false;
            if (_session is not { IsActive: true })
                return;

            var size = PixelSize;
            using var bitmap = new SKBitmap (new SKImageInfo (size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Clear (SKColors.Black);

                TerminalWindowHost[] windows;
                lock (_shown)
                    windows = _shown.ToArray ();

                // Only the topmost window that is not a popup is visible: the terminal has one screen, so
                // windows beneath it (a dialog's owner) are covered. Popups paint over it in order.
                var mainIndex = Array.FindLastIndex (windows, w => !w.IsPopup);
                for (var i = Math.Max (mainIndex, 0); i < windows.Length; i++) {
                    if (windows[i].IsPopup || i == mainIndex)
                        windows[i].RenderInto (canvas);
                }
            }

            _buffer.Clear ();
            Presenter.Encode (bitmap.GetPixelSpan (), size.Width, size.Height, bitmap.RowBytes, _buffer);
            if (_buffer.WrittenCount > 0) {
                _session.Output.Write (_buffer.WrittenSpan);
                _session.Output.Flush ();
            }
        }

        // ── Input routing (UI thread) ─────────────────────────────────────────

        // The window the keyboard goes to: the last one shown activated, else the topmost non-popup.
        private TerminalWindowHost? KeyboardTarget ()
        {
            lock (_shown)
                return _active ?? _shown.FindLast (w => !w.IsPopup);
        }

        // The window under a screen point: the topmost popup that contains it, else the main window.
        private TerminalWindowHost? PointerTarget (int x, int y)
        {
            lock (_shown) {
                var hit = _shown.FindLast (w => w.IsPopup && w.ScreenBounds.Contains (x, y));
                return hit ?? _shown.FindLast (w => !w.IsPopup);
            }
        }

        internal void Dispatch (List<TerminalInput> events)
        {
            foreach (var e in events)
                Dispatch (e);
        }

        internal void Dispatch (TerminalInput e)
        {
            switch (e.Kind) {
                case TerminalInputKind.Key:
                    DispatchKey (e);
                    break;
                case TerminalInputKind.GraphicsReply:
                case TerminalInputKind.KeyboardFlags:
                case TerminalInputKind.DeviceAttributes:
                    _probe?.Observe (e);   // a reply after the probe ended is of no further use
                    break;
                case TerminalInputKind.CellSize:
                    _terminalCell = (e.Col, e.Row);   // remembered even in half-block mode: a probe may switch us to pixels
                    if (_graphics != TerminalGraphicsMode.HalfBlock && (e.Col, e.Row) != _cellPx) {
                        _cellPx = (e.Col, e.Row);
                        ResetScreen ();
                    }
                    break;
                case TerminalInputKind.Text:
                    DispatchText (KeyboardTarget (), e.Text);
                    break;
                default:
                    DispatchPointer (e);
                    break;
            }
        }

        private static void DispatchText (TerminalWindowHost? host, string? text)
        {
            if (host is null || string.IsNullOrEmpty (text))
                return;

            // The core takes one character at a time (a run of WM_CHARs), so a paste is fed in whole code points.
            foreach (var rune in text.EnumerateRunes ()) {
                if (rune.Value == '\r' || rune.Value == '\n')
                    continue;   // Enter travels as a key; a pasted newline must not submit a form
                host.Owner.HandleTextInput (rune.ToString ());
            }
        }

        private void DispatchKey (TerminalInput e)
        {
            // Ctrl+C always ends the app, whichever way the terminal reports it: a legacy terminal turns it
            // into SIGINT, but the Kitty keyboard protocol sends it as a key and no signal is raised.
            if ((e.Key & (Keys.KeyCode | Keys.Modifiers)) == (Keys.C | Keys.Control)) {
                if (e.Event != KeyEventKind.Release)
                    Stop ();
                return;
            }

            var host = KeyboardTarget ();
            if (host is null)
                return;

            var owner = host.Owner;
            var hasKey = (e.Key & Keys.KeyCode) != Keys.None;

            if (e.Event == KeyEventKind.Release) {
                if (hasKey)
                    owner.HandleKeyUp (e.Key);
                return;
            }

            // A repeat is another key-down, as it is on a real keyboard.
            if (hasKey)
                owner.HandleKeyDown (e.Key);

            // Typing: a key with a character and no Ctrl/Alt (those are shortcuts) also types it.
            if (!string.IsNullOrEmpty (e.Text) && (e.Key & (Keys.Control | Keys.Alt)) == Keys.None)
                owner.HandleTextInput (e.Text);

            // Only the Kitty keyboard protocol reports releases; on a legacy terminal the release is synthesised straight away.
            if (hasKey && !KittyKeyboard)
                owner.HandleKeyUp (e.Key);
        }

        private void DispatchPointer (TerminalInput e)
        {
            // Pixel reports are exact. A cell report is the centre of the cell, in device pixels.
            var sx = e.InPixels ? e.Col : e.Col * _cellPx.W + _cellPx.W / 2;
            var sy = e.InPixels ? e.Row : e.Row * _cellPx.H + _cellPx.H / 2;

            // A drag stays with the window it started in, as it would under a real window manager's grab.
            var host = _captured ?? PointerTarget (sx, sy);
            if (host is null)
                return;

            var owner = host.Owner;
            var x = sx - host.Location.X;
            var y = sy - host.Location.Y;
            var mods = e.Key;

            switch (e.Kind) {
                case TerminalInputKind.MouseDown:
                    _captured = host;
                    if (!host.IsPopup)
                        _active = host;   // clicking the main window takes the keyboard back from a popup
                    owner.HandlePointerPressed (e.Button, x, y, mods);
                    break;
                case TerminalInputKind.MouseUp:
                    owner.HandlePointerReleased (e.Button, x, y, mods);
                    _captured = null;
                    break;
                case TerminalInputKind.MouseMove:
                    owner.HandlePointerMoved (e.Button, x, y, mods);
                    break;
                case TerminalInputKind.Wheel:
                    owner.HandlePointerWheel (MouseButtons.None, x, y, new System.Drawing.Point (e.WheelX, e.WheelY), mods);
                    break;
            }
        }

        internal void SetTitle (string title)
        {
            // OSC 2: the terminal tab/window title. Control characters would end the sequence early.
            var clean = title.Replace ("\u001b", string.Empty).Replace ("\u0007", string.Empty);
            _session?.Raw ($"\u001b]2;{clean}\u0007");
        }

        /// <summary>Gets whether something (Ctrl+C, <see cref="Stop"/>) has asked the loop to end.</summary>
        internal bool ExitRequested { get; private set; }

        /// <inheritdoc/>
        public void Stop ()
        {
            ExitRequested = true;
            _running = false;
            _signal.Set ();
        }

        /// <inheritdoc/>
        public void Post (Action action)
        {
            _queue.Enqueue (action);
            _signal.Set ();
        }

        /// <inheritdoc/>
        public void Invoke (Action action)
        {
            if (CheckAccess ()) {
                action ();
                return;
            }

            using var done = new ManualResetEventSlim (false);
            Exception? error = null;
            Post (() => {
                try { action (); }
                catch (Exception ex) { error = ex; }
                finally { done.Set (); }
            });
            done.Wait ();
            if (error is not null)
                throw error;
        }

        /// <inheritdoc/>
        public T Invoke<T> (Func<T> func)
        {
            if (CheckAccess ())
                return func ();

            T result = default!;
            Invoke (() => { result = func (); });
            return result;
        }

        /// <inheritdoc/>
        public bool CheckAccess () => _uiThreadId == -1 || Environment.CurrentManagedThreadId == _uiThreadId;

        /// <inheritdoc/>
        public void DoEvents () => DrainQueue ();

        private void DrainQueue ()
        {
            while (_queue.TryDequeue (out var action)) {
                try {
                    action ();
                } catch (Exception ex) when (Application.RaiseThreadException (ex)) {
                    // Reported to the handler; the loop keeps running, as WinForms' does.
                }
            }
        }

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup) => new TerminalWindowHost (this, owner, isPopup);

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new TerminalTimer (this);

        /// <inheritdoc/>
        public string GetClipboardText () => _clipboard;

        /// <inheritdoc/>
        public void SetClipboardText (string text)
        {
            _clipboard = text ?? string.Empty;

            // OSC 52 hands the text to the terminal's own clipboard, which also works over SSH. There is
            // no portable way to read it back, so paste within the app uses the in-process copy.
            var b64 = Convert.ToBase64String (System.Text.Encoding.UTF8.GetBytes (_clipboard));
            _session?.Raw ($"\u001b]52;c;{b64}\u0007");
        }

        /// <inheritdoc/>
        public void ClearClipboard () => SetClipboardText (string.Empty);

        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
        {
            var size = PixelSize;
            var area = new System.Drawing.Rectangle (0, 0, size.Width, size.Height);
            return new[] { new ScreenInfo ("Terminal", area, area, isPrimary: true) };
        }

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            while (!completed.IsCompleted && _running)
                Pump (waitMs: 10);
            DrainQueue ();
        }

        /// <summary>Stops the loop, restores the terminal and releases the wait handle behind the message queue.</summary>
        public void Dispose ()
        {
            _running = false;
            _session?.Dispose ();
            _signal.Dispose ();
        }

        private sealed class TerminalTimer : IPlatformTimer
        {
            private readonly TerminalPlatformBackend _backend;
            private System.Threading.Timer? _timer;
            private double _interval = 100;

            public TerminalTimer (TerminalPlatformBackend backend) => _backend = backend;

            public double IntervalMilliseconds {
                get => _interval;
                set {
                    _interval = value;
                    _timer?.Change ((int) value, (int) value);
                }
            }

            public event Action? Tick;

            public void Start ()
                => _timer = new System.Threading.Timer (_ => _backend.Post (() => Tick?.Invoke ()), null, (int) _interval, (int) _interval);

            public void Stop ()
            {
                _timer?.Dispose ();
                _timer = null;
            }

            public void Dispose () => Stop ();
        }
    }
}
