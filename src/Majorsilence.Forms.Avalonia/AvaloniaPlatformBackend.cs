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
    {
        /// <inheritdoc/>
        public string Name => "Avalonia";

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

        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup)
            => isPopup ? new MajorsilenceFormsPopupWindowHost (owner) : new MajorsilenceFormsWindowHost (owner);

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new AvaloniaTimer ();

        // ── WebView (Avalonia.Controls.WebView — WebView2/WKWebView/WebKitGTK-WPE native engines) ──
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

        // ── Clipboard ──
        // Avalonia exposes the clipboard per-TopLevel; use the first open window's clipboard.
        private static IClipboard? Clipboard
            => (Application.OpenForms.FirstOrDefault ()?.Backend as MajorsilenceFormsWindowHost)?.Clipboard;

        /// <inheritdoc/>
        public string GetClipboardText ()
        {
            try {
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
