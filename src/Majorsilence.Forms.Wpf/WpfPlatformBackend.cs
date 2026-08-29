using System;
using System.Threading;
using System.Windows.Threading;
using Majorsilence.Forms.Backends;
using MF = Majorsilence.Forms;
using WpfApplication = System.Windows.Application;

namespace Majorsilence.Forms.Wpf
{
    /// <summary>
    /// An <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms on WPF (<c>System.Windows</c>).
    /// Majorsilence.Forms does its own SkiaSharp drawing; this backend presents each window through a
    /// WPF <see cref="SkiaWpfElement"/> and routes WPF mouse/keyboard events into the neutral input
    /// path. The message loop is the WPF <see cref="Dispatcher"/>.
    ///
    /// Two ways in:
    /// <list type="bullet">
    /// <item><description><b>Majorsilence.Forms owns the app</b> — set
    /// <c>Platform.Backend = new WpfPlatformBackend()</c> on an STA thread before the first window,
    /// then <c>Majorsilence.Forms.Application.Run(new MainForm())</c> pumps the Dispatcher.</description></item>
    /// <item><description><b>An existing WPF app owns the loop</b> — drop a
    /// <see cref="MajorsilenceFormsPresenter"/> (or <c>myControl.ToWpfElement()</c>) into any WPF
    /// visual tree; the presenter installs this backend automatically and the app's own
    /// <c>Application.Run</c> services everything.</description></item>
    /// </list>
    /// Windows-only, like WPF itself (the assembly compiles on other OSes via EnableWindowsTargeting
    /// so it stays a CI compile gate, but it cannot run there).
    /// </summary>
    public sealed class WpfPlatformBackend : IPlatformBackend, IDisposable
    {
        private Dispatcher? _dispatcher;
        private DispatcherFrame? _loopFrame;
        private bool _ownsApp;

        /// <inheritdoc/>
        public string Name => "WPF";

        /// <inheritdoc/>
        public void Initialize ()
        {
            if (_dispatcher is not null)
                return;

            _dispatcher = Dispatcher.CurrentDispatcher;

            // A WPF Application is required for resource lookup / lifetime; create one if the process
            // doesn't already have it (the embedding scenario always will).
            if (WpfApplication.Current is null)
            {
                _ownsApp = true;
                var app = new WpfApplication { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                _ = app;
            }
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
        {
            Initialize ();

            _loopFrame = new DispatcherFrame ();
            using var registration = token.Register (() => Post (() =>
            {
                if (_loopFrame is not null)
                    _loopFrame.Continue = false;
            }));

            try
            {
                Dispatcher.PushFrame (_loopFrame);
            }
            finally
            {
                _loopFrame = null;
            }
        }

        /// <inheritdoc/>
        public void Stop ()
        {
            var frame = _loopFrame;
            if (frame is not null)
            {
                Post (() => frame.Continue = false);
                return;
            }

            // Not our loop (an existing WPF app owns it): shutting the app down is the host's call.
        }

        /// <inheritdoc/>
        public void Post (Action action)
        {
            var d = _dispatcher;
            if (d is null || d.HasShutdownStarted)
            {
                action ();
                return;
            }
            d.BeginInvoke (DispatcherPriority.Normal, action);
        }

        /// <inheritdoc/>
        public void Invoke (Action action)
        {
            if (CheckAccess ())
            {
                action ();
                return;
            }
            (_dispatcher ?? throw new InvalidOperationException ("WpfPlatformBackend is not initialized.")).Invoke (action);
        }

        /// <inheritdoc/>
        public T Invoke<T> (Func<T> func)
        {
            if (CheckAccess ())
                return func ();
            return (_dispatcher ?? throw new InvalidOperationException ("WpfPlatformBackend is not initialized.")).Invoke (func);
        }

        /// <inheritdoc/>
        public bool CheckAccess () => _dispatcher is null || _dispatcher.CheckAccess ();

        /// <inheritdoc/>
        public void DoEvents ()
        {
            // WPF's idiomatic "pump pending work once": push a frame that a min-priority callback ends.
            var frame = new DispatcherFrame ();
            Dispatcher.CurrentDispatcher.BeginInvoke (DispatcherPriority.Background, new Action (() => frame.Continue = false));
            Dispatcher.PushFrame (frame);
        }

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (MF.WindowBase owner, bool isPopup) => new WpfWindowHost (owner, isPopup);

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new WpfTimer ();

        // ── Clipboard ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string GetClipboardText ()
        {
            try { return Invoke (() => System.Windows.Clipboard.ContainsText () ? System.Windows.Clipboard.GetText () : string.Empty); }
            catch { return string.Empty; }
        }

        /// <inheritdoc/>
        public void SetClipboardText (string text)
        {
            if (string.IsNullOrEmpty (text))
            {
                ClearClipboard ();
                return;
            }
            try { Invoke (() => System.Windows.Clipboard.SetText (text)); }
            catch { }
        }

        /// <inheritdoc/>
        public void ClearClipboard ()
        {
            try { Invoke (() => System.Windows.Clipboard.Clear ()); }
            catch { }
        }

        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
        {
            // WPF exposes no multi-monitor API; report a single primary screen from SystemParameters
            // (device-independent units, which the seam treats as pixels here).
            var w = (int) System.Windows.SystemParameters.PrimaryScreenWidth;
            var h = (int) System.Windows.SystemParameters.PrimaryScreenHeight;
            var work = System.Windows.SystemParameters.WorkArea;
            var bounds = new System.Drawing.Rectangle (0, 0, w, h);
            var working = new System.Drawing.Rectangle ((int) work.X, (int) work.Y, (int) work.Width, (int) work.Height);
            return new[] { new ScreenInfo ("Primary", bounds, working, true) };
        }

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            var frame = new DispatcherFrame ();
            completed.ContinueWith (_ => Post (() => frame.Continue = false),
                System.Threading.Tasks.TaskScheduler.Default);
            if (!completed.IsCompleted)
                Dispatcher.PushFrame (frame);
        }

        /// <summary>Shuts down the WPF Application if this backend created it.</summary>
        public void Dispose ()
        {
            if (_ownsApp && WpfApplication.Current is { } app)
            {
                try { app.Dispatcher.Invoke (() => app.Shutdown ()); } catch { }
            }
        }

        private sealed class WpfTimer : IPlatformTimer
        {
            private readonly DispatcherTimer _timer = new (DispatcherPriority.Normal, Dispatcher.CurrentDispatcher);

            public WpfTimer () => _timer.Tick += (_, _) => Tick?.Invoke ();

            public double IntervalMilliseconds
            {
                get => _timer.Interval.TotalMilliseconds;
                set => _timer.Interval = TimeSpan.FromMilliseconds (Math.Max (1, value));
            }

            public event Action? Tick;

            public void Start () => _timer.Start ();
            public void Stop () => _timer.Stop ();

            public void Dispose () => _timer.Stop ();
        }
    }
}
