using System;
using System.Linq;
using System.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// The default <see cref="IPlatformBackend"/>: hosts Majorsilence.Forms on Avalonia 12. Application
    /// bootstrap and the message loop are delegated to Avalonia's <see cref="Dispatcher"/>.
    /// </summary>
    public sealed class AvaloniaPlatformBackend : IPlatformBackend, IWebViewFactory, IReducedMotionSource, IAudioBackend, IModalLoopSupport
#if BROWSER
        , IAsyncPlatformBackend
#endif
#if ANDROID || IOS
        , IHapticsBackend
#endif
#if ANDROID
        , INotificationBackend
#endif
#if !BROWSER
        , IKeepScreenAwakeBackend
#endif
#if !SINGLEVIEW
        , ITrayIconBackend
#endif
    {
        /// <inheritdoc/>
        public string Name => "Avalonia";

#if !SINGLEVIEW
        /// <inheritdoc/>
        /// <remarks>Avalonia's <c>TrayIcon</c> (TSM-19); see <see cref="AvaloniaTrayIcon"/> for what it reports.
        /// Not on the browser, Android or iOS rows: none has a notification area.</remarks>
        public ITrayIconHandle? CreateTrayIcon (NotifyIcon owner) => AvaloniaTrayIcon.Create (owner);
#endif

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
            HookDispatcherExceptions ();
            HookApplicationLifecycle ();
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
            HookDispatcherExceptions ();
            HookApplicationLifecycle ();
            await BrowserAccessibility.StartAsync (hostElementId).ConfigureAwait (true);
        }

        /// <inheritdoc/>
        public void RunMainLoop (CancellationToken token)
            // StartBrowserAppAsync already attached Avalonia's dispatcher to the browser's own JS event
            // loop (requestAnimationFrame / setTimeout callbacks); there is no separate loop to pump, and
            // blocking here would freeze the single-threaded WASM runtime instead of driving it.
            => throw new PlatformNotSupportedException (
                "The Avalonia browser backend has no blocking main loop; Application.RunBrowserAsync never calls this.");
#endif

        private static bool dispatcher_hooked;

        /// <summary>
        /// Route exceptions that escape a dispatcher operation to <see cref="Majorsilence.Forms.Application.ThreadException"/>,
        /// the way the Gtk4 and headless backends route the ones that escape their own loops.
        /// </summary>
        /// <remarks>
        /// Without this the Avalonia backend is the only one where a throwing event handler kills the
        /// process: Dispatcher.MainLoop does not catch per-operation exceptions, so one escapes the loop
        /// and the runtime aborts. It arrives by two routes -- a handler that throws directly, and less
        /// obviously Task.ThrowAsync reposting an unobserved Task exception onto the UI
        /// SynchronizationContext, which is how an async void Load handler's failure gets here.
        ///
        /// Marking it Handled keeps the app running: WinForms' UnhandledExceptionMode.Automatic, and what
        /// an app's own error dialog expects. With no ThreadException subscriber, Handled stays false so
        /// the exception still surfaces rather than being swallowed.
        /// </remarks>
        private static void HookDispatcherExceptions ()
        {
            if (dispatcher_hooked)
                return;

            dispatcher_hooked = true;

            Dispatcher.UIThread.UnhandledException += (_, e) => {
                if (Majorsilence.Forms.Application.RaiseThreadException (e.Exception))
                    e.Handled = true;
            };
        }

        private static bool lifecycle_hooked;

        /// <summary>
        /// Routes Avalonia's <see cref="IActivatableLifetime"/> to <see cref="Majorsilence.Forms.Application.Suspended"/>/
        /// <see cref="Majorsilence.Forms.Application.Resumed"/>, and to <see cref="WindowBase.OnBackendActivated"/>/
        /// <see cref="WindowBase.OnBackendDeactivated"/> on the single-view root host (register item F10).
        /// </summary>
        /// <remarks>
        /// A backend-level (application-wide) capability, not a per-window one: one <c>IActivatableLifetime</c> for the whole
        /// process, unlike <see cref="IWindowBackend"/>, which every window gets its own of. Not reached through
        /// <c>Application.Current.ApplicationLifetime</c>: confirmed by inspecting the real <c>Avalonia.Android.dll</c>
        /// (12.1.1) that <c>Avalonia.Android.ApplicationLifetime</c> -- the concrete type <c>ApplicationLifetime</c> resolves
        /// to on Android -- implements only <c>IActivityApplicationLifetime</c>/<c>IApplicationLifetime</c>/
        /// <c>ISingleViewApplicationLifetime</c>, never <see cref="IActivatableLifetime"/>. That capability is a *separate*
        /// object (<c>Avalonia.Android.Platform.AndroidActivatableLifetime</c>) reached through
        /// <see cref="Avalonia.Application.TryGetFeature"/>, Avalonia's own optional-platform-capability lookup, instead.
        /// Desktop's <c>IClassicDesktopStyleApplicationLifetime</c> answers that same query with nothing, so this silently
        /// does nothing there rather than throw -- exactly the same "not every row has this" shape <see cref="IWebViewFactory"/>
        /// and <see cref="IReducedMotionSource"/> use. <see cref="ActivationKind.Background"/> is specifically the
        /// backgrounded/foregrounded transition, as opposed to <c>File</c>/<c>OpenUri</c>/<c>Reopen</c> (the app being asked
        /// to open something, or a macOS dock-icon reactivation of an already-running app) -- those are a different concept
        /// from suspend/resume and are not raised as either event.
        /// </remarks>
        private static void HookApplicationLifecycle ()
        {
            if (lifecycle_hooked)
                return;

            if (Avalonia.Application.Current?.TryGetFeature (typeof (IActivatableLifetime)) is not IActivatableLifetime lifetime)
                return; // this row's lifetime does not support it; try again next Initialize() call in
                        // case a later one resolves a different lifetime object (cheap either way)

            lifecycle_hooked = true;

            lifetime.Deactivated += (_, e) => {
                if (e.Kind != ActivationKind.Background)
                    return;

#if SINGLEVIEW
                MajorsilenceFormsSingleViewHost.MainHost?.Owner.OnBackendDeactivated ();
#endif
                Majorsilence.Forms.Application.RaiseSuspended ();
            };

            lifetime.Activated += (_, e) => {
                if (e.Kind != ActivationKind.Background)
                    return;

#if SINGLEVIEW
                MajorsilenceFormsSingleViewHost.MainHost?.Owner.OnBackendActivated ();
#endif
                Majorsilence.Forms.Application.RaiseResumed ();
            };
        }

        /// <summary>
        /// Reports a platform back button/gesture press (register item F11) to the currently active
        /// window's <see cref="WindowBase.BackRequested"/>, so it can cancel it (keep the app open) or
        /// let it proceed. Returns whether a handler cancelled it.
        /// </summary>
        /// <remarks>
        /// Not automatic the way <see cref="HookApplicationLifecycle"/> is: <c>Avalonia.Android.AvaloniaActivity.BackRequested</c>
        /// is declared directly on the Activity class, and nothing in this assembly can discover "the
        /// current Activity" generically (<c>Avalonia.Android.Platform.AndroidActivatableLifetime</c>,
        /// which does track it, is an internal type in a different assembly) -- confirmed by inspecting
        /// the real <c>Avalonia.Android.dll</c> the same way <see cref="HookApplicationLifecycle"/>'s own
        /// remarks describe. A host app's own <c>MainActivity</c> (already required to subclass
        /// <c>AvaloniaMainActivity</c> and carry an AppCompat theme, #288) forwards its own
        /// <c>BackRequested</c> here instead -- one line, the same shape as wiring
        /// <c>Application.RunAndroid</c> already requires. <see cref="Application.ActivePopupWindow"/> is
        /// tried first, matching <see cref="Application.ScheduleClosePopupsOnDeactivate"/>'s own check for
        /// "which window is really active right now": a sheet/dialog open over the main screen gets the
        /// back-press before the main screen does, so it can close itself instead of the whole app
        /// leaving foreground.
        /// </remarks>
        public static bool RaiseBackRequested ()
        {
#if SINGLEVIEW
            WindowBase? target = Majorsilence.Forms.Application.ActivePopupWindow?.IsActive == true
                ? Majorsilence.Forms.Application.ActivePopupWindow
                : MajorsilenceFormsSingleViewHost.MainHost?.Owner;

            return target?.RaiseBackRequested () ?? false;
#else
            return false;
#endif
        }

#if ANDROID
        /// <summary>
        /// The host app's current <c>Activity</c>, registered through <see cref="RegisterAndroidActivity"/>.
        /// Only <see cref="AndroidNotificationBackend.RequestPermission"/> reads this today -- every other
        /// Android member on this page only ever needs <see cref="global::Android.App.Application.Context"/>.
        /// </summary>
        internal static global::Android.App.Activity? CurrentAndroidActivity { get; private set; }

        /// <summary>
        /// Registers the host app's <c>MainActivity</c> (register item F14), so
        /// <see cref="Notifications.LocalNotifications.RequestPermission"/> has a live <c>Activity</c> to
        /// call <c>ActivityCompat.RequestPermissions</c> on -- nothing in this assembly can discover "the
        /// current Activity" generically, the same gap <see cref="RaiseBackRequested"/>'s own remarks
        /// document. Call once, from <c>OnCreate</c>, the same shape <c>BackRequested</c> forwarding
        /// already requires.
        /// </summary>
        public static void RegisterAndroidActivity (global::Android.App.Activity activity) => CurrentAndroidActivity = activity;

        /// <summary>
        /// Reports the host app's own <see cref="global::Android.Content.Intent"/> (register item F14),
        /// from both <c>MainActivity.OnCreate</c> and <c>OnNewIntent</c> -- a notification tap re-launches
        /// or resumes the app the same generic way <see cref="AndroidNotificationBackend"/>'s own remarks
        /// describe (<c>PackageManager.GetLaunchIntentForPackage</c>), so the host never builds or reads
        /// this backend's tap extra itself; it only forwards its own intent unconditionally. A no-op for
        /// any other intent -- most of an app's launches have nothing to do with a notification at all.
        /// </summary>
        public static void ReportAndroidIntent (global::Android.Content.Intent? intent)
        {
            if (intent is null || !intent.HasExtra (AndroidNotificationBackend.TappedExtraKey))
                return;

            var id = intent.GetIntExtra (AndroidNotificationBackend.TappedExtraKey, -1);
            intent.RemoveExtra (AndroidNotificationBackend.TappedExtraKey);   // OnNewIntent's Intent is reused by GetIntent() later -- do not re-report the same tap on a later, unrelated call

            if (id >= 0)
                Notifications.LocalNotifications.RaiseTapped (id);
        }

        /// <summary>
        /// Reports that <c>MainActivity.OnRequestPermissionsResult</c> fired (register item F14). Does not
        /// need to know which permission or the outcome: <see cref="Notifications.LocalNotifications.IsPermissionGranted"/>
        /// always re-reads the platform fresh, so this only needs to trigger the
        /// <see cref="Notifications.LocalNotifications.PermissionChanged"/> notification itself.
        /// </summary>
        public static void ReportNotificationPermissionResult () => Notifications.LocalNotifications.RaisePermissionChanged ();
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

        // ── IReducedMotionSource ─────────────────────────────────────────────────────────────────────
        // Android and iOS push real change notifications (a ContentObserver, an NSNotificationCenter observer), so those two rows
        // need no polling. The desktop row (Windows, macOS and Linux/GNOME, told apart at run time in DesktopReducedMotion) has no
        // such notification available portably, so PolledSetting re-reads it on a timer instead.
#if ANDROID
        private AndroidReducedMotion? androidReducedMotion;
        private AndroidReducedMotion ReducedMotionSource => androidReducedMotion ??= new AndroidReducedMotion ();
#elif IOS
        private IosReducedMotion? iosReducedMotion;
        private IosReducedMotion ReducedMotionSource => iosReducedMotion ??= new IosReducedMotion ();
#elif BROWSER
        // Out of scope for this register item (Android, iOS, Windows, macOS and GNOME are what was asked for). A browser head could
        // read CSS's prefers-reduced-motion media feature, which would be its own small addition.
#else
        private PolledSetting? desktopReducedMotion;
        // Every 2 s: frequent enough that a setting changed mid-session catches up promptly, cheap enough (one process launch on
        // Linux, one P/Invoke on Windows and macOS) that nothing here is a meaningful drain.
        private PolledSetting ReducedMotionSource => desktopReducedMotion ??= new PolledSetting (DesktopReducedMotion.Read, CreateTimer (), 2000);
#endif

#if BROWSER
        /// <inheritdoc/>
        public bool PrefersReducedMotion => false;

        /// <inheritdoc/>
        public event EventHandler? PrefersReducedMotionChanged { add { } remove { } }
#else
        /// <inheritdoc/>
        public bool PrefersReducedMotion => ReducedMotionSource.Current;

        /// <inheritdoc/>
        public event EventHandler? PrefersReducedMotionChanged {
            add => ReducedMotionSource.Changed += value;
            remove => ReducedMotionSource.Changed -= value;
        }
#endif

        // ── IAudioBackend ── real on Android and iOS (this row's whole reason for existing: neither
        // platform has an OS utility for Media.NativeAudio to spawn). Every other row -- desktop, browser
        // -- has nothing of its own to add over NativeAudio's existing path for PlayFile/PlaySystemSound
        // (SoundPlayer and SystemSounds fall back to NativeAudio whenever this interface answers null,
        // exactly as if it were not implemented at all there), and nothing at all for PlayTrack, which has
        // no such fallback -- AudioPlayer.IsSupported is how a caller checks that ahead of time.
#if ANDROID
        private readonly AndroidAudioBackend audioBackend = new ();

        /// <inheritdoc/>
        public Media.IPlayingSound? PlayFile (string path, bool loop) => audioBackend.PlayFile (path, loop);

        /// <inheritdoc/>
        public Media.IPlayingSound? PlaySystemSound (string name) => audioBackend.PlaySystemSound (name);

        /// <inheritdoc/>
        public Media.IAudioTrack? PlayTrack (string path, bool loop, float volume, Media.AudioUsage usage) => audioBackend.PlayTrack (path, loop, volume, usage);
#elif IOS
        private readonly IosAudioBackend audioBackend = new ();

        /// <inheritdoc/>
        public Media.IPlayingSound? PlayFile (string path, bool loop) => audioBackend.PlayFile (path, loop);

        /// <inheritdoc/>
        public Media.IPlayingSound? PlaySystemSound (string name) => audioBackend.PlaySystemSound (name);

        /// <inheritdoc/>
        public Media.IAudioTrack? PlayTrack (string path, bool loop, float volume, Media.AudioUsage usage) => audioBackend.PlayTrack (path, loop, volume, usage);
#else
        /// <inheritdoc/>
        public Media.IPlayingSound? PlayFile (string path, bool loop) => null;

        /// <inheritdoc/>
        public Media.IPlayingSound? PlaySystemSound (string name) => null;

        /// <inheritdoc/>
        public Media.IAudioTrack? PlayTrack (string path, bool loop, float volume, Media.AudioUsage usage) => null;
#endif

        // ── IHapticsBackend ── real only on Android and iOS, unlike IAudioBackend above: haptics has no
        // desktop/browser equivalent worth a null-returning implementation, and register item F13's own
        // acceptance criterion is "IsSupported false on Headless" -- so, unlike audio, this interface is
        // declared in the class's own base list only under ANDROID/IOS (see the class declaration above),
        // not implemented everywhere with a null/no-op body. Haptics.IsSupported (Backend is IHapticsBackend)
        // is therefore false on every other row with no separate per-row check needed.
#if ANDROID
        private readonly AndroidHapticsBackend hapticsBackend = new ();

        /// <inheritdoc/>
        public void Tap () => hapticsBackend.Tap ();

        /// <inheritdoc/>
        public void Impact () => hapticsBackend.Impact ();

        /// <inheritdoc/>
        public void Vibrate (TimeSpan duration) => hapticsBackend.Vibrate (duration);
#elif IOS
        private readonly IosHapticsBackend hapticsBackend = new ();

        /// <inheritdoc/>
        public void Tap () => hapticsBackend.Tap ();

        /// <inheritdoc/>
        public void Impact () => hapticsBackend.Impact ();

        /// <inheritdoc/>
        public void Vibrate (TimeSpan duration) => hapticsBackend.Vibrate (duration);
#endif

        // ── INotificationBackend ── Android only in this PR (register item F14's mobile half); iOS is
        // tracked separately. Declared in the class's own base list only under ANDROID (see the class
        // declaration above), the same "no supported-but-does-nothing middle state" reasoning IHapticsBackend
        // above already uses.
#if ANDROID
        private readonly AndroidNotificationBackend notificationBackend = new ();

        /// <inheritdoc/>
        public bool IsPermissionGranted => notificationBackend.IsPermissionGranted;

        /// <inheritdoc/>
        public void RequestPermission () => notificationBackend.RequestPermission ();

        /// <inheritdoc/>
        public void RegisterChannel (string id, string name, string? description, Notifications.NotificationImportance importance, bool sound)
            => notificationBackend.RegisterChannel (id, name, description, importance, sound);

        /// <inheritdoc/>
        public void Show (int id, string channelId, string title, string text, bool ongoing, bool fullScreen)
            => notificationBackend.Show (id, channelId, title, text, ongoing, fullScreen);

        /// <inheritdoc/>
        public void Cancel (int id) => notificationBackend.Cancel (id);
#endif

        // ── IKeepScreenAwakeBackend ── real on Android, iOS and all three desktop OSes (register item
        // F12), unlike every other capability seam above: this is the first one with something real to do
        // on desktop too. Declared for every row but browser (see the class declaration above) -- a
        // plain OS-level sleep inhibit, not something a browser tab controls the same way.
#if ANDROID
        private readonly AndroidKeepAwakeBackend keepAwakeBackend = new ();

        /// <inheritdoc/>
        public bool KeepScreenAwake {
            get => keepAwakeBackend.KeepScreenAwake;
            set => keepAwakeBackend.KeepScreenAwake = value;
        }
#elif IOS
        /// <inheritdoc/>
        public bool KeepScreenAwake {
            get => UIKit.UIApplication.SharedApplication.IdleTimerDisabled;
            set => UIKit.UIApplication.SharedApplication.IdleTimerDisabled = value;
        }
#elif !BROWSER
        /// <inheritdoc/>
        public bool KeepScreenAwake {
            get => DesktopKeepAwake.IsEnabled;
            set => DesktopKeepAwake.Set (value);
        }
#endif

#if !SINGLEVIEW
        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup)
            => isPopup ? new MajorsilenceFormsPopupWindowHost (owner) : new MajorsilenceFormsWindowHost (owner);
#else
        /// <inheritdoc/>
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup)
            => new MajorsilenceFormsSingleViewHost (owner, isPopup);
#endif

        /// <inheritdoc/>
        public IPlatformTimer CreateTimer () => new AvaloniaTimer ();

        // ── WebView (Avalonia.Controls.WebView — WebView2/WKWebView/WebKitGTK-WPE native engines) ──
        // No equivalent ships for either single-view platform (AvaloniaWebViewHandle.cs is excluded from
        // both TFM rows), so both members below just report "unsupported" under SINGLEVIEW.
#if SINGLEVIEW
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

                    // On X11 the clipboard read is a real round-trip with the owning application,
                    // driven by SelectionNotify events on the platform message loop -- NOT the
                    // dispatcher job queue, so RunJobs (DispatcherPriority.Input) spins 100 times
                    // without ever completing it and Ctrl+V pasted nothing. Pump a real nested
                    // dispatcher frame (same mechanism as the modal loop) so platform input is
                    // processed, bounded by a timeout so a clipboard that never answers cannot hang.
                    if (!task.IsCompleted) {
                        var frame = new DispatcherFrame ();
                        task.ContinueWith (_ => frame.Continue = false, TaskScheduler.Default);
                        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds (2) };
                        timeout.Tick += (_, _) => { frame.Continue = false; timeout.Stop (); };
                        timeout.Start ();
                        Dispatcher.UIThread.PushFrame (frame);
                        timeout.Stop ();
                    }

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

#if BROWSER || ANDROID || IOS
        /// <inheritdoc/>
        /// <remarks>
        /// <para>False on every single-view row. In the browser, measured with <c>samples/Gallery.Wasm</c>'s
        /// modal check (issue #406), Avalonia.Browser's dispatcher has no nested frame --
        /// <see cref="Dispatcher.PushFrame"/> throws a bare <see cref="PlatformNotSupportedException"/> --
        /// because the page's JavaScript event loop, not .NET, owns the only thread.</para>
        /// <para>On Android and iOS the same check (linked into <c>samples/Gallery.Android</c> and
        /// <c>samples/Gallery.iOS</c>), run on an Android 15 emulator and an iOS 26.5 simulator against
        /// Avalonia 12.1.1, found the same: <see cref="Dispatcher.PushFrame"/> throws that bare exception at
        /// once, after the dialog -- or the native file picker -- is already on screen. It did not hang. See
        /// "Blocking modal calls on Android and iOS" in docs/backends.md.</para>
        /// <para>The blocking modal APIs check this before they show anything.</para>
        /// </remarks>
        public bool CanRunModalLoop => false;

        /// <inheritdoc/>
        /// <remarks>
        /// Not reached through the framework's own modal APIs, which check <see cref="CanRunModalLoop"/>
        /// first. A direct caller gets the same explanation they would, rather than the message-less
        /// exception Avalonia's <see cref="Dispatcher.PushFrame"/> throws here.
        /// </remarks>
        public void RunModalLoop (System.Threading.Tasks.Task completed) =>
            throw new PlatformNotSupportedException (BlockingModal.Message ("A blocking modal loop", "the dialog's async form (ShowDialogAsync, MessageBox.ShowAsync)"));
#else
        /// <inheritdoc/>
        public bool CanRunModalLoop => true;

        /// <inheritdoc/>
        public void RunModalLoop (System.Threading.Tasks.Task completed)
        {
            var frame = new DispatcherFrame ();
            completed.ContinueWith (_ => frame.Continue = false, System.Threading.Tasks.TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame (frame);
        }
#endif

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
