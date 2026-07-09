using System.Reflection;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides static methods and properties to manage an application, such as methods to start and stop an application.
    /// </summary>
    public static class Application
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

        // Generation counter for the "delayed close cancelled by re-activation" dismissal below.
        // Bumped whenever any of our own windows activates; a scheduled close only fires if no
        // activation happened between scheduling and running.
        private static int _activationGeneration;

        /// <summary>
        /// A window deactivated. Menus/popups must close when focus leaves our app, but NOT when the
        /// deactivation is merely the side effect of one of our own popups (or a nested submenu)
        /// stealing focus as it opens. We cannot know synchronously which window is gaining focus, so
        /// post the close and cancel it if any of our own windows activates first: opening a popup
        /// always produces a matching activation (the popup itself), which cancels the close;
        /// switching to another application produces no such activation, so the close proceeds.
        /// Replaces an earlier timing flag that reset on a fixed dispatcher tick and raced against
        /// late-delivered deactivation events (menus opening then instantly closing).
        /// </summary>
        internal static void ScheduleClosePopupsOnDeactivate ()
        {
            if (ActiveMenu == null && ActivePopupWindow == null)
                return;

            var generation = _activationGeneration;
            Backends.Platform.Backend.Post (() => {
                if (_activationGeneration == generation)
                    ClosePopups ();
            });
        }

        /// <summary>One of our windows activated — cancels any pending deactivate-driven popup close.</summary>
        internal static void NotifyWindowActivated () => _activationGeneration++;

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
        /// Sets the default font for the application. No-op in Majorsilence.Forms.
        /// </summary>
        public static void SetDefaultFont (Majorsilence.Forms.Drawing.Font font) { }

        /// <summary>
        /// Exits the application.
        /// </summary>
        public static void Exit ()
        {
            is_exiting = true;

            OnExit?.Invoke (null, EventArgs.Empty);

            _mainLoopCancellationTokenSource?.Cancel ();
        }

        /// <summary>
        /// Exits the message loop on the current thread. In Majorsilence.Forms this is equivalent to <see cref="Exit"/>.
        /// </summary>
        public static void ExitThread () => Exit ();

        /// <summary>
        /// Sets the application-wide color mode (light/dark/system). Stub in Majorsilence.Forms.
        /// </summary>
        public static void SetColorMode (SystemColorMode colorMode) { }

        /// <summary>
        /// Raised when the application is exiting.
        /// </summary>
        public static event EventHandler? OnExit;

        /// <summary>
        ///  Gets the forms collection associated with this application.
        /// </summary>
        public static FormCollection OpenForms => open_forms ??= [];

        /// <summary>Gets the main form of the application (the first form passed to Run).</summary>
        public static Form? MainForm => OpenForms.Count > 0 ? OpenForms[0] : null;

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

        /// <summary>Runs the platform backend's message loop until <see cref="Exit"/> is called.</summary>
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

        /// <summary>Restarts the application. No-op in Majorsilence.Forms — call Environment.Exit(0) and relaunch if needed.</summary>
        public static void Restart () => Environment.Exit (0);

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

        /// <summary>Raised when a thread exception occurs that is not otherwise handled. Stub in Majorsilence.Forms.</summary>
        public static event System.Threading.ThreadExceptionEventHandler? ThreadException { add { } remove { } }

        /// <summary>Raised when the application is about to exit.</summary>
        public static event EventHandler? ApplicationExit { add { } remove { } }

        /// <summary>Raised when the application becomes idle.</summary>
        public static event EventHandler? Idle { add { } remove { } }

        /// <summary>Sets the default exception handler for unhandled exceptions. Stub in Majorsilence.Forms.</summary>
        public static void SetUnhandledExceptionMode (UnhandledExceptionMode mode) { }

        /// <summary>Adds a message filter to the application. Stub in Majorsilence.Forms.</summary>
        public static void AddMessageFilter (IMessageFilter value) { }

        /// <summary>Removes a message filter from the application. Stub in Majorsilence.Forms.</summary>
        public static void RemoveMessageFilter (IMessageFilter value) { }

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
        ThrowException,
        /// <summary>Catch the exception and notify the ThreadException handler.</summary>
        CatchException,
        /// <summary>Automatically choose based on whether a handler is attached.</summary>
        Automatic
    }

    /// <summary>Specifies the application-wide color mode. WinForms compatibility.</summary>
    public enum SystemColorMode
    {
        /// <summary>Follow the operating system setting.</summary>
        System = 0,
        /// <summary>Use the classic (light) color set.</summary>
        Classic = 1,
        /// <summary>Use the dark color set.</summary>
        Dark = 2
    }
}
