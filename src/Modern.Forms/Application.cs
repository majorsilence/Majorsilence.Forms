using System.Reflection;
using Avalonia.Input;
using Avalonia.Threading;

namespace Modern.Forms
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
        /// Raises the OnThemeChanged event for all open forms.
        /// </summary>
        internal static void DoThemeChanged ()
        {
            foreach (var form in OpenForms)
                form.OnThemeChanged (EventArgs.Empty);
        }

        /// <summary>
        /// Enables visual styles for the application. No-op in Modern.Forms.
        /// </summary>
        public static void EnableVisualStyles () { }

        /// <summary>
        /// Sets compatible text rendering default. No-op in Modern.Forms.
        /// </summary>
        public static void SetCompatibleTextRenderingDefault (bool defaultValue) { }

        /// <summary>
        /// Sets the high DPI mode for the application. No-op in Modern.Forms (Avalonia handles DPI automatically).
        /// </summary>
        public static bool SetHighDpiMode (HighDpiMode highDpiMode) => true;

        /// <summary>
        /// Sets the default font for the application. No-op in Modern.Forms.
        /// </summary>
        public static void SetDefaultFont (Modern.Drawing.Font font) { }

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
            AvaloniaBootstrap.EnsureInitialized ();
            AvaloniaSynchronizationContext.InstallIfNeeded ();

            mainForm.Show ();
            Run ((ICloseable)mainForm);
        }

        /// <summary>Begins running a standard application message loop on the current thread using an ApplicationContext.</summary>
        public static void Run (ApplicationContext context)
        {
            if (context.MainForm != null)
                Run (context.MainForm);
            else
                Run ((ICloseable)(context.MainForm ?? new Form ()));
        }

        /// <summary>
        /// Runs the application's main loop until the <see cref="ICloseable"/> is closed.
        /// </summary>
        /// <param name="closable">The closable to track.</param>
        public static void Run (ICloseable closable)
        {
            if (_mainLoopCancellationTokenSource != null)
                throw new InvalidOperationException ("Run should only be called once");

            AvaloniaBootstrap.EnsureInitialized ();
            AvaloniaSynchronizationContext.InstallIfNeeded ();
            closable.Closed += (s, e) => Exit ();

            _mainLoopCancellationTokenSource = new CancellationTokenSource ();

            Dispatcher.UIThread.MainLoop (_mainLoopCancellationTokenSource.Token);

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
            Dispatcher.UIThread.Post (action);
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

        /// <summary>Gets or sets whether the application is running in user-interactive mode. Stub in Modern.Forms.</summary>
        public static bool UserInteractive => true;

        /// <summary>Gets or sets the format string for the caption of top-level windows. Stub in Modern.Forms.</summary>
        public static string SafeTopLevelCaptionFormat { get; set; } = "{0}";

        /// <summary>Gets or sets the visual style state of the application. Stub in Modern.Forms.</summary>
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
        public static void DoEvents () => Avalonia.Threading.Dispatcher.UIThread.RunJobs ();

        /// <summary>Restarts the application. No-op in Modern.Forms — call Environment.Exit(0) and relaunch if needed.</summary>
        public static void Restart () => Environment.Exit (0);

        /// <summary>Gets or sets the current input language. Stub in Modern.Forms.</summary>
        public static System.Globalization.CultureInfo CurrentCulture {
            get => System.Globalization.CultureInfo.CurrentCulture;
            set => System.Threading.Thread.CurrentThread.CurrentCulture = value;
        }

        /// <summary>Gets or sets the current UI culture. Stub in Modern.Forms.</summary>
        public static System.Globalization.CultureInfo CurrentInputLanguage {
            get => System.Globalization.CultureInfo.CurrentUICulture;
            set => System.Threading.Thread.CurrentThread.CurrentUICulture = value;
        }

        /// <summary>Raised when a thread exception occurs that is not otherwise handled. Stub in Modern.Forms.</summary>
        public static event System.Threading.ThreadExceptionEventHandler? ThreadException { add { } remove { } }

        /// <summary>Raised when the application is about to exit.</summary>
        public static event EventHandler? ApplicationExit { add { } remove { } }

        /// <summary>Raised when the application becomes idle.</summary>
        public static event EventHandler? Idle { add { } remove { } }

        /// <summary>Sets the default exception handler for unhandled exceptions. Stub in Modern.Forms.</summary>
        public static void SetUnhandledExceptionMode (UnhandledExceptionMode mode) { }

        /// <summary>Adds a message filter to the application. Stub in Modern.Forms.</summary>
        public static void AddMessageFilter (IMessageFilter value) { }

        /// <summary>Removes a message filter from the application. Stub in Modern.Forms.</summary>
        public static void RemoveMessageFilter (IMessageFilter value) { }

        /// <summary>Gets whether the application is still running the main message loop.</summary>
        public static bool MessageLoop => true;
    }

    /// <summary>Defines a message filter for the application's message loop. Stub in Modern.Forms.</summary>
    public interface IMessageFilter
    {
        /// <summary>Filters an OS message, returning true to suppress the message.</summary>
        bool PreFilterMessage (ref Message m);
    }

    /// <summary>Represents a Windows message. Stub in Modern.Forms — all fields are zero.</summary>
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
}
