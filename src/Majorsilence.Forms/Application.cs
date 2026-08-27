using System.ComponentModel;
using System.Reflection;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides static methods and properties to manage an application, such as methods to start and stop an application.
    /// </summary>
    public static partial class Application
    {
        private static CancellationTokenSource? _mainLoopCancellationTokenSource;
        private static bool is_exiting;
        private static FormCollection? open_forms;
        private static string? startup_path;

        /// <summary>
        /// This is the top level active menu, if any.
        /// </summary>
        internal static MenuBase? ActiveMenu { get; set; }

        /// <summary>
        /// This is the open popup window, like the ComboBox dropdown, if any.
        /// </summary>
        internal static PopupWindow? ActivePopupWindow { get; set; }

        /// <summary>
        /// A window deactivated. Menus/popups must close when focus leaves our app, but NOT when the
        /// deactivation is merely the side effect of one of our own popups (or a nested submenu)
        /// stealing focus as it opens. We cannot know synchronously which window is gaining focus.
        ///
        /// This used to be decided with an activation-generation counter: schedule a close, cancel it
        /// if any of our own windows activated between scheduling and running. That assumed the
        /// parent's Deactivated always arrives before the popup's own Activated -- empirically false:
        /// logging real clicks (Linux, Avalonia, Mutter/XWayland) showed the popup's Activated
        /// consistently arriving BEFORE its parent's Deactivated, sometimes by 20-30ms. Since nothing
        /// else activates afterward, the counter never moved between scheduling and running, and the
        /// close always won -- the drop down flashed open and closed on every click, not intermittently.
        ///
        /// Fixed by checking current state instead of a before/after delta: <see cref="WindowBase.IsActive"/>
        /// on <see cref="ActivePopupWindow"/> reflects reality at the moment we ask, however the
        /// activate/deactivate pair for this transition happened to interleave. A synchronous check
        /// covers the (observed, common) case where the popup is already active; the posted fallback
        /// re-checks the same thing one tick later, for the reverse ordering.
        /// </summary>
        internal static void ScheduleClosePopupsOnDeactivate ()
        {
            if (ActiveMenu == null && ActivePopupWindow == null)
                return;

            if (ActivePopupWindow?.IsActive == true)
                return;

            Backends.Platform.Backend.Post (() => {
                if (ActivePopupWindow?.IsActive != true)
                    ClosePopups ();
            });
        }

        /// <summary>
        /// Hides any open popups.
        /// </summary>
        internal static void ClosePopups (bool closeMenus = true, bool closePopups = true)
        {
            if (closeMenus)
                ActiveMenu?.Deactivate ();

            if (closePopups)
                ActivePopupWindow?.Hide ();
        }

        /// <summary>
        /// Embedded (non-top-level) surfaces that aren't in <see cref="OpenForms"/> but still need theme
        /// change notifications — e.g. a <see cref="HostedSurface"/> hosted inside Avalonia/Uno.
        /// </summary>
        internal static readonly System.Collections.Generic.List<HostedSurface> EmbeddedSurfaces = new ();

        /// <summary>
        /// Raises the OnThemeChanged event for all open forms and embedded surfaces. Runs *after* the
        /// Theme.ThemeChanged broadcast (see <see cref="Theme"/>), so every ControlStyle's cached colors
        /// are already refreshed before anything repaints — important for backends that repaint
        /// synchronously on Invalidate (e.g. Uno).
        /// </summary>
        internal static void DoThemeChanged ()
        {
            foreach (var form in OpenForms)
                form.OnThemeChanged (EventArgs.Empty);

            foreach (var surface in EmbeddedSurfaces.ToArray ())
                surface.OnThemeChanged (EventArgs.Empty);
        }

        /// <summary>
        /// Enables visual styles for the application. No-op in Majorsilence.Forms.
        /// </summary>
        public static void EnableVisualStyles () { }

        /// <summary>
        /// Sets compatible text rendering default. No-op in Majorsilence.Forms.
        /// </summary>
        public static void SetCompatibleTextRenderingDefault (bool defaultValue) { }

        /// <summary>
        /// Sets the high DPI mode for the application. No-op in Majorsilence.Forms (the platform backend handles DPI automatically).
        /// </summary>
        public static bool SetHighDpiMode (HighDpiMode highDpiMode) => true;

        /// <summary>
        /// Sets the ambient default font every control without an explicit Font inherits.
        /// </summary>
        /// <remarks>
        /// WinForms requires this before the first window is created and throws otherwise; here it is
        /// simply applied from the point it is called, so controls already created keep resolving to
        /// whatever the default was when they were measured. Passing null restores the platform default.
        /// </remarks>
        public static void SetDefaultFont (Majorsilence.Forms.Drawing.Font font)
            => SystemFonts.SetDefaultFont (font);

        private static double? ui_scale;

        /// <summary>
        /// An extra zoom factor applied to the whole UI, on top of the display's own scale factor.
        /// Defaults to 1.0 (no change).
        /// </summary>
        /// <remarks>
        /// This is the same machinery HiDPI already uses, just with a factor you choose instead of one
        /// the display reports: it multiplies <c>WindowBase.Scaling</c>, so every logical unit that
        /// goes through <c>LogicalToDeviceUnits</c> -- font sizes, paddings, control sizes, glyphs --
        /// grows together. Nothing about font sizes or designer coordinates changes, so layout
        /// arithmetic that a designer computed still holds; only the number of device pixels each
        /// logical unit turns into does.
        ///
        /// It exists because display scaling does not cover every case. A large, dense monitor that the
        /// OS reports at scale 1.0 renders WinForms' classic 8.25pt default font at its true, very small
        /// physical size, and no amount of DPI *detection* helps -- the OS is saying this is not a HiDPI
        /// display. This is the knob for that.
        ///
        /// Deliberately does not affect <c>WindowBase.DesktopScaling</c>, which stays the real display
        /// factor: <c>Control.PointToScreen</c> converts through <c>DesktopScaling / Scaling</c>, so
        /// leaving it alone makes that ratio compensate for the zoom automatically and screen
        /// coordinates keep round-tripping.
        ///
        /// The environment variable <c>MAJORSILENCE_UI_SCALE</c> seeds the initial value, so a scale can
        /// be tried against an app without rebuilding it. An explicit assignment overrides it.
        ///
        /// Caveat: anything that hardcodes pixel sizes instead of converting through
        /// <c>LogicalToDeviceUnits</c> will not grow with the rest, so a scale far from 1.0 can show up
        /// such spots.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is not greater than zero.</exception>
        public static double UiScale {
            get => ui_scale ??= ReadUiScaleFromEnvironment ();
            set {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual (value, 0);

                if (ui_scale == value)
                    return;

                ui_scale = value;

                // Every cached size in every open window was computed against the old factor.
                foreach (var form in OpenForms.ToArray ()) {
                    form.PerformLayout ();
                    form.Invalidate ();
                }
            }
        }

        private static double ReadUiScaleFromEnvironment ()
            => double.TryParse (Environment.GetEnvironmentVariable ("MAJORSILENCE_UI_SCALE"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale > 0
                ? scale
                : 1.0;

        /// <summary>
        /// Exits the application.
        /// </summary>
        public static void Exit () => Exit (null);

        /// <summary>Exits the application, giving handlers a chance to cancel.</summary>
        /// <remarks>The Cancel flag is honoured: if a handler sets it before this is called, the
        /// shutdown does not happen. That is the point of the overload.</remarks>
        public static void Exit (CancelEventArgs? e)
        {
            if (e?.Cancel == true)
                return;

            is_exiting = true;

            // Close the open forms first, as upstream does: Exit walks OpenForms raising FormClosing
            // (which a handler may cancel) and then FormClosed, so a form's "save your work?" prompt
            // and its cleanup both run. This used to cancel the loop and leave every form unclosed, so
            // an application shut down by Exit () ran none of that.
            //
            // Over a copy, because closing mutates OpenForms.
            // ApplicationExitCall, not UserClosing: the "minimise to tray unless the app is really
            // exiting" pattern reads CloseReason to tell those apart.
            foreach (var form in OpenForms.Cast<Form> ().ToArray ())
                form.PendingCloseReason = CloseReason.ApplicationExitCall;

            foreach (var form in OpenForms.Cast<Form> ().ToArray ()) {
                var closing = new CancelEventArgs ();
                form.RaiseClosing (closing);

                if (closing.Cancel) {
                    is_exiting = false;

                    foreach (var form2 in OpenForms.Cast<Form> ().ToArray ())
                        form2.PendingCloseReason = CloseReason.UserClosing;

                    return;
                }
            }

            foreach (var form in OpenForms.Cast<Form> ().ToArray ())
                form.Close ();

            OnExit?.Invoke (null, EventArgs.Empty);
            ApplicationExit?.Invoke (null, EventArgs.Empty);

            _mainLoopCancellationTokenSource?.Cancel ();
        }

        /// <summary>
        /// Exits the message loop on the current thread. In Majorsilence.Forms this is equivalent to <see cref="Exit()"/>.
        /// </summary>
        public static void ExitThread () => Exit ();

        /// <summary>
        /// Sets the application-wide color mode (light/dark/system).
        /// </summary>
        /// <remarks>This used to discard its argument, which left <see cref="ColorMode"/> with nothing
        /// to report. The value is now stored and is what ColorMode and IsDarkModeEnabled read.</remarks>
        public static void SetColorMode (SystemColorMode colorMode) => ColorMode = colorMode;

        /// <summary>
        /// Raised when the application is exiting.
        /// </summary>
        public static event EventHandler? OnExit;

        /// <summary>
        ///  Gets the forms collection associated with this application.
        /// </summary>
        public static FormCollection OpenForms => open_forms ??= [];

        /// <summary>
        /// The open forms that can own a modal dialog: those that actually own an OS window.
        /// </summary>
        /// <remarks>
        /// Frame-hosted forms -- MDI children, and forms placed in a control tree with
        /// <c>Controls.Add (form)</c> -- appear in <see cref="OpenForms"/> just as they do in WinForms,
        /// but they are composited into someone else's window rather than owning one. Making such a form
        /// a modal owner disables a backend that was never realized and then blocks forever in the modal
        /// loop, so every owner search goes through here instead of over OpenForms directly.
        /// </remarks>
        internal static IEnumerable<Form> ModalOwnerCandidates => OpenForms.Where (f => !f.IsFrameHosted);

        /// <summary>Gets the main form of the application (the first form passed to Run).</summary>
        public static Form? MainForm => OpenForms.Count > 0 ? OpenForms[0] : null;

        /// <summary>Runs a message loop with no main form.</summary>
        /// <remarks>The loop ends when <see cref="Exit()"/> is called, since there is no form whose
        /// closing would end it -- which is the shape of a tray or background application.</remarks>
        public static void Run () => Run (new ApplicationContext ());

        /// <summary>
        /// Begins running a standard application message loop on the current thread, and makes the specified form visible.
        /// </summary>
        /// <param name="mainForm">A Form that represents the form to make visible.</param>
        public static void Run (Form mainForm)
        {
            Platform.Backend.Initialize ();

            mainForm.Show ();
            Run ((WindowBase)mainForm);
        }

        /// <summary>
        /// Begins running a standard application message loop on the current thread using an
        /// ApplicationContext. Matches real WinForms: the loop ends when <paramref name="context"/>
        /// raises <see cref="ApplicationContext.ThreadExit"/> (by default when MainForm closes, or any
        /// time <see cref="ApplicationContext.ExitThread"/> is called explicitly — e.g. a headless
        /// context with no MainForm driving its own shutdown), and the context is disposed once the
        /// loop exits.
        /// </summary>
        public static void Run (ApplicationContext context)
        {
            EventHandler onThreadExit = (s, e) => Exit ();
            context.ThreadExit += onThreadExit;
            try {
                if (context.MainForm != null) {
                    context.MainForm.Show ();
                    Run ((WindowBase)context.MainForm);
                } else {
                    // No MainForm to show/track: run a loop with nothing to auto-close it. The context
                    // is expected to end the loop itself via ExitThread()/ExitThreadCore() (e.g. an IPC
                    // host reacting to its own shutdown signal).
                    RunCore ();
                }
            } finally {
                context.ThreadExit -= onThreadExit;
                context.Dispose ();
            }
        }

        /// <summary>
        /// Runs the application's main loop until the given window is closed.
        /// </summary>
        /// <param name="closable">The window to track.</param>
        public static void Run (WindowBase closable)
        {
            closable.Closed += (s, e) => Exit ();
            RunCore ();
        }

        /// <summary>
        /// Starts the application on a backend with asynchronous, host-driven startup (currently only
        /// the Avalonia browser/WebAssembly backend). Unlike <see cref="Run(Form)"/> this does not block:
        /// once the backend finishes its async bootstrap and the form returned by
        /// <paramref name="createMainForm"/> is shown, control returns to the caller and the host's own
        /// event loop (the browser tab's JS event loop, for Avalonia-in-WASM) drives the UI from then on —
        /// there is no <see cref="RunCore"/>/main-loop call to make, and none should be made.
        /// </summary>
        /// <param name="createMainForm">
        /// Creates the form to make visible. A factory rather than a ready-made <see cref="Form"/>
        /// because constructing any <see cref="WindowBase"/> touches the platform backend — on the
        /// browser backend that only succeeds once async bootstrap has completed, so the form must not
        /// be constructed until after that await below.
        /// </param>
        /// <param name="hostElementId">
        /// The id of the host page element to attach the UI to (e.g. a browser div id). Ignored by
        /// backends that don't implement <see cref="IAsyncPlatformBackend"/>.
        /// </param>
        public static async Task RunBrowserAsync (Func<Form> createMainForm, string hostElementId = "out")
        {
            if (Platform.Backend is IAsyncPlatformBackend asyncBackend)
                await asyncBackend.InitializeAsync (hostElementId).ConfigureAwait (true);
            else
                Platform.Backend.Initialize ();

            createMainForm ().Show ();
        }

        /// <summary>
        /// Starts the application on a backend with synchronous, host-driven startup where the host (not
        /// this method) already owns and pumps the platform's main loop — currently the Avalonia Android
        /// backend, whose <c>AvaloniaMainActivity</c> bootstraps Avalonia and starts pumping its dispatcher
        /// via the Activity's own Looper before this is ever called. Like <see cref="RunBrowserAsync"/>
        /// this does not block: once the backend initializes and the form returned by
        /// <paramref name="createMainForm"/> is shown, control returns to the caller (typically
        /// <c>MainActivity.OnCreate</c>) and the host's own event loop drives the UI from then on — there
        /// is no <see cref="RunCore"/>/main-loop call to make, and none should be made.
        /// </summary>
        /// <param name="createMainForm">
        /// Creates the form to make visible. A factory rather than a ready-made <see cref="Form"/> for the
        /// same reason as <see cref="RunBrowserAsync"/>: constructing any <see cref="WindowBase"/> touches
        /// the platform backend, so callers should not construct one before the backend is initialized.
        /// </param>
        public static void RunAndroid (Func<Form> createMainForm)
        {
            Platform.Backend.Initialize ();
            createMainForm ().Show ();
        }

        /// <summary>
        /// Starts the application on a backend with synchronous, host-driven startup where the host (not
        /// this method) already owns and pumps the platform's main loop — currently the Avalonia iOS
        /// backend, whose <c>AvaloniaAppDelegate</c> bootstraps Avalonia and starts pumping its dispatcher
        /// via the OS's own run loop before this is ever called. Identical to <see cref="RunAndroid"/>
        /// (both are host-driven, synchronous-bootstrap platforms) — kept as its own named entry point
        /// for symmetry with <see cref="RunAndroid"/>/<see cref="RunBrowserAsync"/> rather than reusing
        /// the Android name for a different platform. Like the others, this does not block: once the
        /// backend initializes and the form returned by <paramref name="createMainForm"/> is shown,
        /// control returns to the caller (typically <c>AppDelegate.FinishedLaunching</c>) and the host's
        /// own event loop drives the UI from then on — there is no <see cref="RunCore"/>/main-loop call
        /// to make, and none should be made.
        /// </summary>
        /// <param name="createMainForm">
        /// Creates the form to make visible. A factory rather than a ready-made <see cref="Form"/> for the
        /// same reason as <see cref="RunAndroid"/>: constructing any <see cref="WindowBase"/> touches
        /// the platform backend, so callers should not construct one before the backend is initialized.
        /// </param>
        public static void RunIOS (Func<Form> createMainForm)
        {
            Platform.Backend.Initialize ();
            createMainForm ().Show ();
        }

        /// <summary>Runs the platform backend's message loop until <see cref="Exit()"/> is called.</summary>
        private static void RunCore ()
        {
            if (_mainLoopCancellationTokenSource != null)
                throw new InvalidOperationException ("Run should only be called once");

            Platform.Backend.Initialize ();

            _mainLoopCancellationTokenSource = new CancellationTokenSource ();

            Platform.Backend.RunMainLoop (_mainLoopCancellationTokenSource.Token);

            // Make sure we call OnExit in case an error happened and Exit() wasn't called explicitly
            if (!is_exiting)
                OnExit?.Invoke (null, EventArgs.Empty);
        }

        /// <summary>
        /// Performs the desired Action on the UI thread.
        /// </summary>
        /// <param name="action">The action to perform on the UI thread.</param>
        public static void RunOnUIThread (Action action)
        {
            Platform.Backend.Post (action);
        }

        /// <summary>
        /// Gets the path for the executable file that started the application, not including the executable name.
        /// </summary>
        public static string StartupPath => startup_path ??= AppContext.BaseDirectory;

        /// <summary>Gets the path to the executable file that started the application.</summary>
        public static string ExecutablePath =>
            System.Diagnostics.Process.GetCurrentProcess ().MainModule?.FileName
            ?? System.Reflection.Assembly.GetEntryAssembly ()?.Location
            ?? StartupPath;

        /// <summary>Gets or sets whether the application is running in user-interactive mode. Stub in Majorsilence.Forms.</summary>
        public static bool UserInteractive => true;

        /// <summary>Gets or sets the format string for the caption of top-level windows. Stub in Majorsilence.Forms.</summary>
        public static string SafeTopLevelCaptionFormat { get; set; } = "{0}";

        /// <summary>Gets or sets the visual style state of the application. Stub in Majorsilence.Forms.</summary>
        public static VisualStyleState VisualStyleState { get; set; } = VisualStyleState.ClientAndNonClientAreasEnabled;

        /// <summary>Gets the product name associated with this application.</summary>
        public static string? ProductName =>
            System.Reflection.Assembly.GetEntryAssembly ()
                ?.GetCustomAttribute<System.Reflection.AssemblyProductAttribute> ()
                ?.Product;

        /// <summary>Gets the product version associated with this application.</summary>
        public static string? ProductVersion =>
            System.Reflection.Assembly.GetEntryAssembly ()
                ?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute> ()
                ?.InformationalVersion
            ?? System.Reflection.Assembly.GetEntryAssembly ()?.GetName ().Version?.ToString ();

        private static ApplicationInfo? application_info;

        /// <summary>
        /// Gets a <see cref="ApplicationInfo"/> facade grouping assembly metadata — the backing type for
        /// classic VB's <c>My.Application.Info</c> (Title/AssemblyName/Version/Copyright/CompanyName/
        /// Description/ProductName). Wraps the same entry-assembly reflection as <see cref="ProductName"/>/
        /// <see cref="ProductVersion"/>/<see cref="CompanyName"/> rather than recomputing it separately.
        /// </summary>
        public static ApplicationInfo Info =>
            application_info ??= new ApplicationInfo(System.Reflection.Assembly.GetEntryAssembly());

        /// <summary>Gets the company name associated with this application.</summary>
        public static string? CompanyName =>
            System.Reflection.Assembly.GetEntryAssembly ()
                ?.GetCustomAttribute<System.Reflection.AssemblyCompanyAttribute> ()
                ?.Company;

        /// <summary>Gets the common application data path for all users.</summary>
        public static string CommonAppDataPath =>
            System.IO.Path.Combine (
                Environment.GetFolderPath (Environment.SpecialFolder.CommonApplicationData),
                CompanyName ?? string.Empty,
                ProductName ?? string.Empty);

        /// <summary>Gets the user-specific application data path.</summary>
        public static string UserAppDataPath =>
            System.IO.Path.Combine (
                Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
                CompanyName ?? string.Empty,
                ProductName ?? string.Empty);

        /// <summary>Gets the local user-specific application data path.</summary>
        public static string LocalUserAppDataPath =>
            System.IO.Path.Combine (
                Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData),
                CompanyName ?? string.Empty,
                ProductName ?? string.Empty);

        /// <summary>Processes all messages currently in the message queue.</summary>
        public static void DoEvents () => Platform.Backend.DoEvents ();

        /// <summary>Restarts the application: relaunches the current executable and exits this instance.</summary>
        /// <remarks>
        /// This used to be <c>Environment.Exit (0)</c> — it quit without relaunching, so "restart to
        /// apply your changes" simply closed the application. The new process is started before this
        /// one shuts down, which is the order upstream uses.
        /// </remarks>
        public static void Restart ()
        {
            var executable = Environment.ProcessPath;

            if (!string.IsNullOrEmpty (executable)) {
                try {
                    System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (executable) {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                    });
                } catch (System.ComponentModel.Win32Exception) {
                    // Relaunch refused (policy, a deleted image). Still exit, as before: reporting a
                    // failure here would be a behaviour change on a path that used to be silent.
                }
            }

            Exit ();
            Environment.Exit (0);
        }

        /// <summary>Gets or sets the current input language. Stub in Majorsilence.Forms.</summary>
        public static System.Globalization.CultureInfo CurrentCulture {
            get => System.Globalization.CultureInfo.CurrentCulture;
            set => System.Threading.Thread.CurrentThread.CurrentCulture = value;
        }

        /// <summary>Gets or sets the current UI culture. Stub in Majorsilence.Forms.</summary>
        public static System.Globalization.CultureInfo CurrentInputLanguage {
            get => System.Globalization.CultureInfo.CurrentUICulture;
            set => System.Threading.Thread.CurrentThread.CurrentUICulture = value;
        }

        /// <summary>Raised when a thread exception occurs that is not otherwise handled.</summary>
        public static event System.Threading.ThreadExceptionEventHandler? ThreadException;

        /// <summary>Raised when the application is about to exit.</summary>
        /// <remarks>Raised by <see cref="Exit()"/>. It was declared <c>add { } remove { }</c> — it
        /// accepted a handler and threw it away, so the standard place to flush settings or a log on
        /// shutdown ran nothing.</remarks>
        public static event EventHandler? ApplicationExit;

        /// <summary>Raised when the application becomes idle.</summary>
        /// <remarks>Raised by the message loop when it has no queued work left. Nothing used to raise
        /// it, so the common "do deferred setup on first idle" idiom never ran.</remarks>
        public static event EventHandler? Idle;

        /// <summary>Raises <see cref="Idle"/>. Called by the backend's message loop when it drains.</summary>
        internal static void RaiseIdle () => Idle?.Invoke (null, EventArgs.Empty);

        /// <summary>
        /// Reports an unhandled exception from an event handler to <see cref="ThreadException"/>.
        /// </summary>
        /// <returns>True when a handler was attached and the exception was reported.</returns>
        /// <remarks>
        /// <see cref="ThreadException"/> is what WinForms applications rely on to show an error dialog
        /// rather than dying. Nothing raised it, so an exception from a handler took the process down.
        /// With no subscriber this returns false and the caller rethrows, matching
        /// <see cref="UnhandledExceptionMode.Automatic"/>.
        /// </remarks>
        internal static bool RaiseThreadException (Exception exception)
        {
            var handler = ThreadException;

            if (handler is null)
                return false;

            // The handler's sender is non-nullable; upstream reports the thread the exception came
            // from, which is also the only useful thing to name here.
            handler (System.Threading.Thread.CurrentThread, new System.Threading.ThreadExceptionEventArgs (exception));
            return true;
        }

        /// <summary>Sets the default exception handler for unhandled exceptions. Stub in Majorsilence.Forms.</summary>
        public static void SetUnhandledExceptionMode (UnhandledExceptionMode mode) { }

        /// <inheritdoc cref="SetUnhandledExceptionMode(UnhandledExceptionMode)"/>
        public static void SetUnhandledExceptionMode (UnhandledExceptionMode mode, bool threadScope) { }

        // Copy-on-write so FilterMessage can walk the list without holding the lock: a filter is free
        // to add or remove filters while being called, which would otherwise mutate the collection
        // mid-enumeration.
        private static readonly object _messageFilterGate = new ();
        private static IMessageFilter[] _messageFilters = [];

        /// <summary>
        /// Adds a filter that sees input messages before they are dispatched to a control.
        /// </summary>
        /// <remarks>
        /// This is the portable way to watch input application-wide — the thing ported code otherwise
        /// reaches for a global OS hook to do (for example, dismissing a popup when a click lands
        /// outside it). Filters run for mouse and keyboard input; see
        /// <see cref="WindowMessages"/> for the message ids raised. WinForms keeps filters per-thread;
        /// here the list is process-wide, which matches single-UI-thread apps and is why a filter added
        /// on a worker thread still applies.
        /// </remarks>
        public static void AddMessageFilter (IMessageFilter value)
        {
            if (value is null)
                return;

            lock (_messageFilterGate)
                _messageFilters = [.. _messageFilters, value];
        }

        /// <summary>Removes a filter previously added by <see cref="AddMessageFilter"/>.</summary>
        public static void RemoveMessageFilter (IMessageFilter value)
        {
            if (value is null)
                return;

            lock (_messageFilterGate) {
                var index = System.Array.IndexOf (_messageFilters, value);
                if (index < 0)
                    return;

                var next = new IMessageFilter[_messageFilters.Length - 1];
                System.Array.Copy (_messageFilters, 0, next, 0, index);
                System.Array.Copy (_messageFilters, index + 1, next, index, next.Length - index);
                _messageFilters = next;
            }
        }


        /// <summary>Gets whether the application is still running the main message loop.</summary>
        public static bool MessageLoop => true;
    }

    /// <summary>Defines a message filter for the application's message loop. Stub in Majorsilence.Forms.</summary>
    public interface IMessageFilter
    {
        /// <summary>Filters an OS message, returning true to suppress the message.</summary>
        bool PreFilterMessage (ref Message m);
    }

    /// <summary>Represents a Windows message. Stub in Majorsilence.Forms — all fields are zero.</summary>
    public struct Message
    {
        /// <summary>Gets or sets the window handle.</summary>
        public IntPtr HWnd { get; set; }

        /// <summary>Gets or sets the message identifier.</summary>
        public int Msg { get; set; }

        /// <summary>Gets or sets additional message information.</summary>
        public IntPtr WParam { get; set; }

        /// <summary>Gets or sets additional message information.</summary>
        public IntPtr LParam { get; set; }

        /// <summary>Gets or sets the return value of the message.</summary>
        public IntPtr Result { get; set; }

        /// <summary>Creates a new message.</summary>
        public static Message Create (IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) =>
            new () { HWnd = hWnd, Msg = msg, WParam = wparam, LParam = lparam };

        /// <summary>
        /// Marshals <see cref="LParam"/> into an instance of <paramref name="cls"/>, as WinForms does
        /// for messages that pass a struct by pointer (WM_COPYDATA and friends).
        /// </summary>
        /// <returns>
        /// The marshalled instance, or null when <see cref="LParam"/> is zero — which is what a filter
        /// or WndProc override sees on the backends here, since nothing synthesises struct pointers.
        /// </returns>
        public readonly object? GetLParam (Type cls)
        {
            ArgumentNullException.ThrowIfNull (cls);

            return LParam == IntPtr.Zero
                ? null
                : System.Runtime.InteropServices.Marshal.PtrToStructure (LParam, cls);
        }
    }

    /// <summary>Specifies the visual style state of the application.</summary>
    public enum VisualStyleState
    {
        /// <summary>Visual styles are not applied to any areas of application windows.</summary>
        NoneEnabled = 0,
        /// <summary>Visual styles are applied only to the client area.</summary>
        ClientAreaEnabled = 2,
        /// <summary>Visual styles are applied only to the non-client area.</summary>
        NonClientAreaEnabled = 1,
        /// <summary>Visual styles are applied to both client and non-client areas (default).</summary>
        ClientAndNonClientAreasEnabled = 3
    }

    /// <summary>Specifies how application exceptions are handled.</summary>
    public enum UnhandledExceptionMode
    {
        /// <summary>Throw the exception.</summary>
        ThrowException = 1,
        /// <summary>Catch the exception and notify the ThreadException handler.</summary>
        CatchException = 2,
        /// <summary>Automatically choose based on whether a handler is attached.</summary>
        Automatic = 0,
    }

    /// <summary>Specifies the application-wide color mode. WinForms compatibility.</summary>
    public enum SystemColorMode
    {
        /// <summary>Follow the operating system setting.</summary>
        System = 1,
        /// <summary>Use the classic (light) color set.</summary>
        Classic = 0,
        /// <summary>Use the dark color set.</summary>
        Dark = 2,
    }
}
