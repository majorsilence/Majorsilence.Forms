using System.Threading;
using Majorsilence.Forms.Backends;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// An <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms on classic
    /// System.Windows.Forms. Majorsilence.Forms does its own SkiaSharp drawing; this backend presents
    /// each window through a WinForms <c>SKControl</c> and routes WinForms mouse/keyboard events into
    /// the neutral input path.
    ///
    /// Two ways in:
    /// <list type="bullet">
    /// <item><description><b>Majorsilence.Forms owns the app</b> — set
    /// <c>Platform.Backend = new WinFormsPlatformBackend()</c> before the first window, then
    /// <c>Majorsilence.Forms.Application.Run(new MainForm())</c> drives a real
    /// <see cref="WF.Application"/> message loop.</description></item>
    /// <item><description><b>An existing WinForms app owns the loop</b> — drop a
    /// <see cref="MajorsilenceFormsPresenter"/> (or <c>myControl.ToWinFormsControl()</c>) into any
    /// WinForms form; the presenter installs this backend automatically and the app's own
    /// <c>Application.Run</c> services everything.</description></item>
    /// </list>
    /// Windows-only by definition — see the package README.
    /// </summary>
    public sealed class WinFormsPlatformBackend : IPlatformBackend, IDisposable
    {
        private int _uiThreadId = -1;
        private WF.Control? _marshal;                 // hidden handle the UI thread marshals through
        private WF.ApplicationContext? _context;      // non-null only while RunMainLoop owns the loop

        /// <inheritdoc/>
        public string Name => "WinForms";

        /// <inheritdoc/>
        public void Initialize ()
        {
            if (_uiThreadId != -1)
                return;

            _uiThreadId = Environment.CurrentManagedThreadId;

            // Best-effort process-wide WinForms configuration. These throw if another component (an
            // existing WinForms host app, or ApplicationConfiguration.Initialize()) already configured
            // them or a window handle exists — which is fine, that configuration wins.
            try { WF.Application.SetHighDpiMode (WF.HighDpiMode.PerMonitorV2); } catch { }
            try { WF.Application.EnableVisualStyles (); } catch { }
            try { WF.Application.SetCompatibleTextRenderingDefault (false); } catch { }

            // A concrete handle on the UI thread to BeginInvoke/Invoke through — created eagerly so
            // Post/Invoke work before the first window exists.
            _marshal = new WF.Control ();
            _ = _marshal.Handle;   // force handle creation
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
        {
            Initialize ();
            _uiThreadId = Environment.CurrentManagedThreadId;

            _context = new WF.ApplicationContext ();
            using var registration = token.Register (() => Post (() => _context?.ExitThread ()));
            try {
                WF.Application.Run (_context);
            } finally {
                _context.Dispose ();
                _context = null;
            }
        }

        /// <inheritdoc/>
        public void Stop ()
        {
            var context = _context;
            if (context is not null) {
                Post (() => context.ExitThread ());
                return;
            }

            // Not our loop (an existing WinForms app owns it, e.g. the embedding scenario):
            // stopping the application is the host's decision, so this is a no-op.
        }

        /// <inheritdoc/>
        public void Post (Action action)
        {
            var marshal = _marshal;
            if (marshal is null || !marshal.IsHandleCreated) {
                // Backend not initialized yet (or shutting down); run inline as a last resort.
                action ();
                return;
            }

            try { marshal.BeginInvoke (action); }
            catch (InvalidOperationException) { action (); }   // handle torn down mid-flight
        }

        /// <inheritdoc/>
        public void Invoke (Action action)
        {
            if (CheckAccess ()) {
                action ();
                return;
            }

            var marshal = _marshal ?? throw new InvalidOperationException ("WinFormsPlatformBackend is not initialized.");
            marshal.Invoke (action);
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
        public void DoEvents () => WF.Application.DoEvents ();

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (MF.WindowBase owner, bool isPopup) => new WinFormsWindowHost (owner, isPopup);

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new WinFormsTimer ();

        // ── Clipboard ────────────────────────────────────────────────────────────
        // WinForms clipboard access requires the STA UI thread; marshal and stay best-effort (the
        // clipboard is a shared OS resource another process can hold open).

        /// <inheritdoc/>
        public string GetClipboardText ()
        {
            try { return Invoke (() => WF.Clipboard.ContainsText () ? WF.Clipboard.GetText () : string.Empty); }
            catch { return string.Empty; }
        }

        /// <inheritdoc/>
        public void SetClipboardText (string text)
        {
            // WF.Clipboard.SetText throws on an empty string; an empty set means clear.
            if (string.IsNullOrEmpty (text)) {
                ClearClipboard ();
                return;
            }

            try { Invoke (() => WF.Clipboard.SetText (text)); }
            catch { }
        }

        /// <inheritdoc/>
        public void ClearClipboard ()
        {
            try { Invoke (WF.Clipboard.Clear); }
            catch { }
        }

        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
        {
            var screens = WF.Screen.AllScreens;
            var result = new ScreenInfo[screens.Length];
            for (var i = 0; i < screens.Length; i++)
                result[i] = new ScreenInfo (screens[i].DeviceName, screens[i].Bounds, screens[i].WorkingArea, screens[i].Primary);
            return result;
        }

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            // WinForms has no public "run one nested loop until X" primitive short of ShowDialog, so
            // pump cooperatively — the same pattern the Uno backend uses. DoEvents dispatches all
            // pending messages, then yield briefly so the wait doesn't spin a core.
            while (!completed.IsCompleted) {
                if (CheckAccess ())
                    WF.Application.DoEvents ();
                completed.Wait (1);
            }
        }

        /// <summary>Releases the hidden marshaling handle.</summary>
        public void Dispose ()
        {
            _marshal?.Dispose ();
            _marshal = null;
        }

        private sealed class WinFormsTimer : IPlatformTimer
        {
            private readonly WF.Timer _timer = new ();

            public WinFormsTimer () => _timer.Tick += (_, _) => Tick?.Invoke ();

            public double IntervalMilliseconds {
                get => _timer.Interval;
                set => _timer.Interval = Math.Max (1, (int) value);
            }

            public event Action? Tick;

            public void Start () => _timer.Start ();
            public void Stop () => _timer.Stop ();

            public void Dispose ()
            {
                _timer.Stop ();
                _timer.Dispose ();
            }
        }
    }
}
