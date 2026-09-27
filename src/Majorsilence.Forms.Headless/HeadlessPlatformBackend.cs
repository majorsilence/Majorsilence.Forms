using System;
using System.Collections.Concurrent;
using System.Threading;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Headless
{
    /// <summary>One <see cref="Media.SoundPlayer"/>/<see cref="Media.SystemSounds"/> play request the Headless backend recorded.</summary>
    /// <param name="Value">The .wav path (<see cref="HeadlessPlatformBackend.PlayFile"/>) or system sound name (<see cref="HeadlessPlatformBackend.PlaySystemSound"/>).</param>
    /// <param name="Loop">Whether native looping was requested (always <c>false</c> for a system sound).</param>
    /// <param name="IsSystemSound">Whether this came from <see cref="HeadlessPlatformBackend.PlaySystemSound"/> rather than <see cref="HeadlessPlatformBackend.PlayFile"/>.</param>
    public readonly record struct AudioPlayRequest (string Value, bool Loop, bool IsSystemSound);

    /// <summary>
    /// A dependency-free <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms entirely in memory:
    /// windows render to offscreen SkiaSharp surfaces and the "message loop" is a simple work queue.
    ///
    /// It serves two purposes: (1) offscreen rendering for tests and headless/server scenarios, and
    /// (2) a reference second backend proving the <see cref="IPlatformBackend"/>/<see cref="IWindowBackend"/>
    /// seam is genuinely toolkit-agnostic — the same shape a real Uno backend follows.
    /// </summary>
    public sealed class HeadlessPlatformBackend : IPlatformBackend, IAnimationFrameSource, IReducedMotionSource, IAudioBackend, IDisposable
    {
        /// <summary>Gets the animation frames, which run only when stepped by hand.</summary>
        public HeadlessAnimationClock AnimationClock { get; } = new ();

        /// <inheritdoc/>
        public void RequestAnimationFrame (Action<TimeSpan> callback) => AnimationClock.Request (callback);

        private bool prefersReducedMotion;

        /// <summary>Gets or sets the answer <see cref="SystemInformation.PrefersReducedMotion"/> reports while this backend is active, for a test to set directly instead of a real setting to poll.</summary>
        public bool PrefersReducedMotion {
            get => prefersReducedMotion;
            set {
                if (prefersReducedMotion == value)
                    return;

                prefersReducedMotion = value;
                PrefersReducedMotionChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        public event EventHandler? PrefersReducedMotionChanged;

        // ── IAudioBackend ── a recording fake, not a real player: there is nothing to actually play back
        // in a headless test process, so this exists purely so SoundPlayer/SystemSounds routing (try the
        // backend, fall back to NativeAudio) can be asserted without spawning a real OS utility. A
        // ConcurrentQueue, not a List: SoundPlayer.PlayLooping's desktop-style respawn fallback plays
        // through a background Task, so a test asserting "the backend was never asked" or "asked exactly
        // once" can race a still-running respawn loop's own writes here otherwise.
        private readonly ConcurrentQueue<AudioPlayRequest> _audioRequests = new ();

        /// <summary>Gets every <see cref="Media.SoundPlayer"/>/<see cref="Media.SystemSounds"/> request this backend has been asked to play, in order.</summary>
        public System.Collections.Generic.IReadOnlyList<AudioPlayRequest> AudioRequests => _audioRequests.ToArray ();

        /// <summary>Clears <see cref="AudioRequests"/> between tests.</summary>
        public void ClearAudioRequests ()
        {
            while (_audioRequests.TryDequeue (out _)) { }
        }

        /// <summary>
        /// Gets or sets whether this backend answers a play request at all. False by default -- matching
        /// "no in-process audio available" (real on this backend, since there is no OS to actually play
        /// through): every other test in the suite runs with the Headless backend already active as the
        /// process-wide default (parallelization is off; <see cref="HeadlessRenderer.Use"/> is called once and left set),
        /// so an opt-OUT default here would silently intercept unrelated tests that expect
        /// <see cref="Media.SoundPlayer"/>/<see cref="Media.SystemSounds"/> to reach
        /// <see cref="Media.NativeAudio"/>'s launcher seam instead. A test that wants to assert the
        /// backend-first routing sets this true itself.
        /// </summary>
        public bool AudioIsSupported { get; set; }

        /// <inheritdoc/>
        public Media.IPlayingSound? PlayFile (string path, bool loop)
        {
            _audioRequests.Enqueue (new AudioPlayRequest (path, loop, IsSystemSound: false));
            return AudioIsSupported ? new FakePlayingSound () : null;
        }

        /// <inheritdoc/>
        public Media.IPlayingSound? PlaySystemSound (string name)
        {
            _audioRequests.Enqueue (new AudioPlayRequest (name, false, IsSystemSound: true));
            return AudioIsSupported ? new FakePlayingSound () : null;
        }

        private sealed class FakePlayingSound : Media.IPlayingSound
        {
            public void Wait () { }
            public void Dispose () { }
        }

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

                // The loop has nothing left to do, which is what Application.Idle means. Raised here
                // rather than on every pass so a handler that queues work sees the queue drained
                // first. Nothing used to raise it at all.
                Majorsilence.Forms.Application.RaiseIdle ();

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
            while (_queue.TryDequeue (out var action)) {
                // An exception from posted work used to escape the loop and take the process down.
                // WinForms routes it to Application.ThreadException, which is what an application's
                // "something went wrong" dialog hangs off; with no handler attached it still throws,
                // matching UnhandledExceptionMode.Automatic.
                try {
                    action ();
                } catch (Exception ex) when (Majorsilence.Forms.Application.RaiseThreadException (ex)) {
                    // Reported to the handler; the loop keeps running, as upstream's does.
                }
            }
        }

        /// <summary>Stops the loop and releases the wait handle backing the message queue.</summary>
        public void Dispose ()
        {
            _running = false;
            _signal.Dispose ();
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
