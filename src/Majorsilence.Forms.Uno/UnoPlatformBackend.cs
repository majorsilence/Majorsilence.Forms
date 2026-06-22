using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms.Uno
{
    /// <summary>
    /// An <see cref="IPlatformBackend"/> that hosts Majorsilence.Forms on Uno Platform's Skia desktop
    /// target. Majorsilence.Forms does its own SkiaSharp drawing; this backend presents each window through
    /// an <c>SKXamlCanvas</c> and routes Uno pointer/keyboard events into the neutral input path.
    ///
    /// NOTE: Uno owns the application lifecycle — the *head* project (referencing the platform Skia
    /// runtime, e.g. Uno.WinUI.Runtime.Skia.X11/.MacOS/.Win32) calls
    /// <c>Microsoft.UI.Xaml.Application.Start(...)</c>. This backend runs inside that app and drives
    /// work through the <see cref="DispatcherQueue"/>. See docs/backends.md.
    /// </summary>
    public sealed class UnoPlatformBackend : IPlatformBackend
    {
        private DispatcherQueue? _dispatcher;
        private readonly ManualResetEventSlim _stop = new (false);
        private string _clipboard = string.Empty;

        /// <inheritdoc/>
        public string Name => "Uno";

        private DispatcherQueue Dispatcher
            => _dispatcher ??= DispatcherQueue.GetForCurrentThread ()
               ?? throw new InvalidOperationException ("No Uno DispatcherQueue on this thread. The Uno app head must start the application before Majorsilence.Forms creates windows.");

        /// <inheritdoc/>
        public void Initialize () => _dispatcher ??= DispatcherQueue.GetForCurrentThread ();

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
        {
            // The Uno head owns the real message loop (Application.Start). Block until cancelled.
            using var reg = token.Register (() => _stop.Set ());
            _stop.Wait ();
        }

        /// <inheritdoc/>
        public void Stop () => _stop.Set ();

        /// <inheritdoc/>
        public void Post (Action action) => Dispatcher.TryEnqueue (() => action ());

        /// <inheritdoc/>
        public void Invoke (Action action)
        {
            if (CheckAccess ()) {
                action ();
                return;
            }

            using var done = new ManualResetEventSlim (false);
            Exception? error = null;
            Dispatcher.TryEnqueue (() => {
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
            T result = default!;
            Invoke (() => { result = func (); });
            return result;
        }

        /// <inheritdoc/>
        public bool CheckAccess () => _dispatcher is null || _dispatcher.HasThreadAccess;

        /// <inheritdoc/>
        public void DoEvents ()
        {
            // Uno has no synchronous "pump pending work" primitive; the dispatcher drains on its own.
        }

        private IUnoHostSurface? _mainHost;

        /// <summary>
        /// Registers the surface that popups (combo dropdowns, menus, tooltips) should attach their
        /// overlay to. Called by <see cref="MajorsilenceFormsPresenter"/> when Majorsilence.Forms is embedded
        /// in an existing Uno app (where no top-level Majorsilence window is created).
        /// </summary>
        internal void RegisterHostSurface (IUnoHostSurface host) => _mainHost = host;

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup)
        {
            // Popups render as in-window overlays parented to the main surface's XamlRoot.
            var host = new UnoWindowHost (owner, isPopup, isPopup ? _mainHost : null);
            if (!isPopup)
                _mainHost = host;
            return host;
        }

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new UnoTimer ();

        // ── Clipboard (best-effort via WinUI clipboard, falling back to an in-process value) ──
        /// <inheritdoc/>
        public string GetClipboardText ()
        {
            try {
                var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent ();
                if (content is not null && content.Contains (Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) {
                    // Read off the UI thread: blocking on the async result directly on the UI thread
                    // would deadlock (the continuation needs that same thread).
                    var text = System.Threading.Tasks.Task.Run (() => content.GetTextAsync ().AsTask ()).GetAwaiter ().GetResult ();
                    return text ?? _clipboard;
                }
            } catch { }
            return _clipboard;
        }

        /// <inheritdoc/>
        public void SetClipboardText (string text)
        {
            _clipboard = text ?? string.Empty;
            try {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage ();
                package.SetText (_clipboard);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent (package);
            } catch { }
        }

        /// <inheritdoc/>
        public void ClearClipboard ()
        {
            _clipboard = string.Empty;
            try { Windows.ApplicationModel.DataTransfer.Clipboard.Clear (); } catch { }
        }

        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
        {
            // A single logical desktop; refined per-display info can come from Microsoft.UI.Windowing.
            return new[] {
                new ScreenInfo (
                    "Uno",
                    new System.Drawing.Rectangle (0, 0, 1920, 1080),
                    new System.Drawing.Rectangle (0, 0, 1920, 1080),
                    isPrimary: true)
            };
        }

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            // Uno has no nested-message-loop primitive exposed; spin the dispatcher cooperatively.
            while (!completed.IsCompleted) {
                if (CheckAccess ())
                    System.Threading.Thread.Sleep (1);
                else
                    completed.Wait (1);
            }
        }

        private sealed class UnoTimer : IPlatformTimer
        {
            private readonly Microsoft.UI.Xaml.DispatcherTimer _timer = new ();

            public UnoTimer () => _timer.Tick += (_, _) => Tick?.Invoke ();

            public double IntervalMilliseconds {
                get => _timer.Interval.TotalMilliseconds;
                set => _timer.Interval = TimeSpan.FromMilliseconds (value);
            }

            public event Action? Tick;

            public void Start () => _timer.Start ();
            public void Stop () => _timer.Stop ();
            public void Dispose () => _timer.Stop ();
        }
    }
}
