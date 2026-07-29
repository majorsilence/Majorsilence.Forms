using System;
using System.Linq;
using System.Threading;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// The default <see cref="IPlatformBackend"/>: hosts Majorsilence.Forms on Avalonia 12. Application
    /// bootstrap and the message loop are delegated to Avalonia's <see cref="Dispatcher"/>.
    /// </summary>
    public sealed class AvaloniaPlatformBackend : IPlatformBackend, IWebViewFactory
#if BROWSER
        , IAsyncPlatformBackend
#endif
    {
        /// <inheritdoc/>
        public string Name => "Avalonia";

#if !BROWSER
        /// <inheritdoc/>
        public void Initialize ()
        {
            AvaloniaBootstrap.EnsureInitialized ();
            AvaloniaSynchronizationContext.InstallIfNeeded ();
            // Pre-load fonts synchronously on the UI thread so the first render is fast.
            // fontconfig is not thread-safe; doing this here (not on a background thread) avoids
            // lock contention with the render loop.
            Majorsilence.Forms.Theme.WarmupFonts ();
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token) => Dispatcher.UIThread.MainLoop (token);
#else
        /// <inheritdoc/>
        public void Initialize ()
        {
            // WindowBase's constructor calls this unconditionally (every Form/PopupWindow, not just the
            // first one). Once InitializeAsync has run, subsequent windows just no-op here, matching the
            // desktop path's idempotency — only the very first call (before RunBrowserAsync has awaited
            // InitializeAsync) is actually an error, since bootstrap can't complete synchronously.
            if (!AvaloniaBootstrap.IsInitialized)
                throw new PlatformNotSupportedException (
                    "The Avalonia browser backend starts asynchronously; call Majorsilence.Forms.Application.RunBrowserAsync instead of Application.Run.");
        }

        /// <inheritdoc/>
        public async Task InitializeAsync (string hostElementId)
        {
            await AvaloniaBootstrap.EnsureInitializedBrowserAsync (hostElementId).ConfigureAwait (true);
            AvaloniaSynchronizationContext.InstallIfNeeded ();
            Majorsilence.Forms.Theme.WarmupFonts ();
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
            // StartBrowserAppAsync already attached Avalonia's dispatcher to the browser's own JS event
            // loop (requestAnimationFrame / setTimeout callbacks); there is no separate loop to pump, and
            // blocking here would freeze the single-threaded WASM runtime instead of driving it.
            => throw new PlatformNotSupportedException (
                "The Avalonia browser backend has no blocking main loop; Application.RunBrowserAsync never calls this.");
#endif

        /// <inheritdoc/>
        public void Stop () { /* Loop exit is driven by the cancellation token passed to RunMainLoop. */ }

        /// <inheritdoc/>
        public void Post (Action action) => Dispatcher.UIThread.Post (action);

        /// <inheritdoc/>
        public void Invoke (Action action)
        {
            if (Dispatcher.UIThread.CheckAccess ())
                action ();
            else
                Dispatcher.UIThread.InvokeAsync (action).GetAwaiter ().GetResult ();
        }

        /// <inheritdoc/>
        public T Invoke<T> (Func<T> func)
            => Dispatcher.UIThread.CheckAccess ()
                ? func ()
                : Dispatcher.UIThread.InvokeAsync (func).GetAwaiter ().GetResult ();

        /// <inheritdoc/>
        public bool CheckAccess () => Dispatcher.UIThread.CheckAccess ();

        /// <inheritdoc/>
        public void DoEvents () => Dispatcher.UIThread.RunJobs ();

#if !BROWSER
        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup)
            => isPopup ? new MajorsilenceFormsPopupWindowHost (owner) : new MajorsilenceFormsWindowHost (owner);
#else
        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup)
            => new MajorsilenceFormsBrowserHost (owner, isPopup);
#endif

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new AvaloniaTimer ();

        // ── WebView (Avalonia.Controls.WebView — WebView2/WKWebView/WebKitGTK-WPE native engines) ──
        // No browser equivalent ships (AvaloniaWebViewHandle.cs is excluded from the browser TFM row),
        // so both members below just report "unsupported" under BROWSER.
#if BROWSER
        /// <inheritdoc/>
        public bool IsSupported => false;

        /// <inheritdoc/>
        public IWebViewHandle? CreateWebView () => null;
#else
        private static bool? _webViewSupported;

        /// <inheritdoc/>
        public bool IsSupported {
            get {
                if (_webViewSupported is bool cached)
                    return cached;

                bool supported;
                try {
                    // Cheap, non-throwing-by-design probe (Avalonia.Controls.WebView ships this exact
                    // shape for the purpose): confirmed via the Phase 0 spike on Windows/WebView2. The
                    // analogous adapter types for macOS (WkWebView) and Linux (WebKitGtk/WpeWebKit) are
                    // not yet exercised on those platforms — this still degrades safely (catch below)
                    // rather than throwing if the probe itself is unsupported for the running OS.
                    var adapterType = OperatingSystem.IsWindows () ? Avalonia.Platform.WebViewAdapterType.WebView2
                        : OperatingSystem.IsMacOS () ? Avalonia.Platform.WebViewAdapterType.WkWebView
                        : Avalonia.Platform.WebViewAdapterType.WpeWebKit;
                    supported = Avalonia.Platform.WebViewAdapterInfo.GetAdapterInfo (adapterType).IsSupported;
                } catch {
                    supported = false;
                }

                _webViewSupported = supported;
                return supported;
            }
        }

        /// <inheritdoc/>
        public IWebViewHandle? CreateWebView ()
        {
            try {
                // NativeWebView requires the UI thread (it's an Avalonia Control) and — on Windows —
                // requires the process to be STA and the host app to carry a manifest with a supportedOS
                // list, or attaching it to the visual tree throws. Both are host-application concerns
                // (see Phase 0 spike findings); this factory can only guard against engine-level failures.
                return Dispatcher.UIThread.CheckAccess ()
                    ? new AvaloniaWebViewHandle ()
                    : Dispatcher.UIThread.Invoke (() => (IWebViewHandle) new AvaloniaWebViewHandle ());
            } catch {
                return null;
            }
        }
#endif

        // ── Clipboard ──
        // Avalonia exposes the clipboard per-TopLevel; use the first open window's clipboard.
        private static IClipboard? Clipboard
            => (Application.OpenForms.FirstOrDefault ()?.Backend as MajorsilenceFormsWindowHost)?.Clipboard;

        // Clipboard access must happen on the UI thread. These are called synchronously (WinForms'
        // Clipboard API is synchronous), so the CALLER is frequently already on the UI thread --
        // e.g. a TextBox handling Ctrl+C. In that case we must NOT marshal via
        // Dispatcher.UIThread.InvokeAsync(...).GetResult(): InvokeAsync queues the work and
        // GetResult() blocks the UI thread waiting for it, so the queued work can never run -- a
        // hard deadlock (found: Ctrl+C froze the whole app). Only marshal when called off the UI
        // thread; when already on it, touch the clipboard directly.

        /// <inheritdoc/>
        public string GetClipboardText ()
        {
            try {
                if (Dispatcher.UIThread.CheckAccess ()) {
                    var cb = Clipboard;
                    if (cb is null)
                        return string.Empty;
                    var task = cb.TryGetTextAsync ();
                    // The OS clipboard read is synchronous under Avalonia's async surface, so the task
                    // is normally already complete. Pump a bounded number of dispatcher passes for the
                    // rare case it isn't, then give up with empty rather than hanging.
                    for (var i = 0; i < 100 && !task.IsCompleted; i++)
                        Dispatcher.UIThread.RunJobs (DispatcherPriority.Input);
                    return task.Status == TaskStatus.RanToCompletion ? (task.Result ?? string.Empty) : string.Empty;
                }

                return Dispatcher.UIThread.InvokeAsync (async () => {
                    var cb = Clipboard;
                    return cb is null ? string.Empty : await cb.TryGetTextAsync ().ConfigureAwait (false) ?? string.Empty;
                }).GetAwaiter ().GetResult ();
            } catch {
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public void SetClipboardText (string text)
        {
            try {
                if (Dispatcher.UIThread.CheckAccess ()) {
                    // Already on the UI thread: start the set without blocking (blocking would deadlock).
                    // Setting the clipboard is a fire-and-forget side effect; it completes promptly.
                    _ = Clipboard?.SetTextAsync (text);
                    return;
                }

                Dispatcher.UIThread.InvokeAsync (async () => {
                    var cb = Clipboard;
                    if (cb is not null)
                        await cb.SetTextAsync (text).ConfigureAwait (false);
                }).GetAwaiter ().GetResult ();
            } catch { }
        }

        /// <inheritdoc/>
        public void ClearClipboard ()
        {
            try {
                if (Dispatcher.UIThread.CheckAccess ()) {
                    _ = Clipboard?.ClearAsync ();
                    return;
                }

                Dispatcher.UIThread.InvokeAsync (async () => {
                    var cb = Clipboard;
                    if (cb is not null)
                        await cb.ClearAsync ().ConfigureAwait (false);
                }).GetAwaiter ().GetResult ();
            } catch { }
        }

        // ── Screens ──
        /// <inheritdoc/>
        public ScreenInfo[] GetScreens ()
        {
            var lifetime = Avalonia.Application.Current?.ApplicationLifetime
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var host = Application.OpenForms.FirstOrDefault ()?.Backend as MajorsilenceFormsWindowHost;
            var screens = host?.Screens?.All ?? lifetime?.MainWindow?.Screens?.All;

            if (screens is null)
                return Array.Empty<ScreenInfo> ();

            return screens.Select (s => new ScreenInfo (
                s.DisplayName ?? string.Empty,
                new System.Drawing.Rectangle (s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                new System.Drawing.Rectangle (s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height),
                s.IsPrimary)).ToArray ();
        }

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            var frame = new DispatcherFrame ();
            completed.ContinueWith (_ => frame.Continue = false, System.Threading.Tasks.TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame (frame);
        }

        private sealed class AvaloniaTimer : IPlatformTimer
        {
            private readonly DispatcherTimer _timer = new ();

            public AvaloniaTimer () => _timer.Tick += (_, _) => Tick?.Invoke ();

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
