using System;
using System.Collections.Concurrent;
using System.Threading;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Headless
{
    /// <summary>
    /// A dependency-free <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms entirely in memory:
    /// windows render to offscreen SkiaSharp surfaces and the "message loop" is a simple work queue.
    ///
    /// It serves two purposes: (1) offscreen rendering for tests and headless/server scenarios, and
    /// (2) a reference second backend proving the <see cref="IPlatformBackend"/>/<see cref="IWindowBackend"/>
    /// seam is genuinely toolkit-agnostic — the same shape a real Uno backend follows.
    /// </summary>
    public sealed class HeadlessPlatformBackend : IPlatformBackend
    {
        private readonly ConcurrentQueue<Action> _queue = new ();
        private readonly AutoResetEvent _signal = new (false);
        private volatile bool _running;
        private int _uiThreadId = -1;
        private string _clipboard = string.Empty;

        /// <inheritdoc/>
        public string Name => "Headless";

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

            using var registration = token.Register (() => _signal.Set ());

            while (_running && !token.IsCancellationRequested) {
                DrainQueue ();
                _signal.WaitOne (50);
            }

            DrainQueue ();
        }

        /// <inheritdoc/>
        public void Stop ()
        {
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
            while (_queue.TryDequeue (out var action))
                action ();
        }

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup) => new HeadlessWindowHost (owner);

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new HeadlessTimer (this);

        /// <inheritdoc/>
        public string GetClipboardText () => _clipboard;

        /// <inheritdoc/>
        public void SetClipboardText (string text) => _clipboard = text ?? string.Empty;

        /// <inheritdoc/>
        public void ClearClipboard () => _clipboard = string.Empty;

        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
            => new[] {
                new ScreenInfo (
                    "Headless",
                    new System.Drawing.Rectangle (0, 0, 1920, 1080),
                    new System.Drawing.Rectangle (0, 0, 1920, 1080),
                    isPrimary: true)
            };

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            while (!completed.IsCompleted) {
                DrainQueue ();
                _signal.WaitOne (10);
            }
            DrainQueue ();
        }

        private sealed class HeadlessTimer : IPlatformTimer
        {
            private readonly HeadlessPlatformBackend _backend;
            private System.Threading.Timer? _timer;
            private double _interval = 100;

            public HeadlessTimer (HeadlessPlatformBackend backend) => _backend = backend;

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
