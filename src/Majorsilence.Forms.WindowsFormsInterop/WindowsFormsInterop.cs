using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.Interop
{
    /// <summary>
    /// Bridge for incremental WinForms ↔ Majorsilence.Forms migration on Windows.
    ///
    /// <para><b>Direction A — Majorsilence.Forms app → legacy WinForms form</b><br/>
    /// Use <see cref="Show(WF.Form,MF.Form?)"/> / <see cref="ShowDialog(WF.Form,MF.Form?)"/> to open a
    /// <see cref="System.Windows.Forms.Form"/> from a Majorsilence.Forms event handler.</para>
    ///
    /// <para><b>Direction B — WinForms app → Majorsilence.Forms form</b><br/>
    /// Call <see cref="InitializeMajorsilence"/> once in Program.Main after WinForms initializes, then use
    /// <see cref="ShowMajorsilenceForm(MF.Form,WF.Form?)"/> / <see cref="ShowMajorsilenceDialog(MF.Form,WF.Form?)"/>
    /// to open Majorsilence.Forms windows from WinForms event handlers. On Windows, Avalonia's Win32 backend
    /// shares the existing Win32 message pump, so both toolkits are serviced by a single
    /// <see cref="WF.Application.Run"/> call — no second thread or loop is needed.</para>
    ///
    /// <para>Windows-only by definition (System.Windows.Forms does not exist off Windows).</para>
    ///
    /// <para>Threading contract: call on the UI (STA) thread. From a background thread, marshal first:<br/>
    /// <c>Majorsilence.Forms.Application.RunOnUIThread(() => WindowsFormsInterop.Show(form));</c></para>
    /// </summary>
    public static class WindowsFormsInterop
    {
        private static bool _initialized;
        private static readonly List<WF.Form> _openModeless = new();

        /// <summary>
        /// Optional. Once Majorsilence.Forms exposes the host window's Win32 HWND, set this so dialogs can be
        /// owned by (and modal to) their Majorsilence parent. Until set, forms are shown unowned.
        ///
        /// Suggested accessor in the Avalonia backend:
        ///     IntPtr Majorsilence.Forms.Avalonia.GetWin32Handle(Majorsilence.Forms.Form form)
        ///         => /* presenter.TopLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero */;
        /// then:  WindowsFormsInterop.OwnerHandleResolver = Majorsilence.Forms.Avalonia.GetWin32Handle;
        /// </summary>
        public static Func<MF.Form, IntPtr>? OwnerHandleResolver { get; set; }

        /// <summary>
        /// One-time WinForms initialization. Safe to call repeatedly; runs once. Call before showing the first
        /// form (Show/ShowDialog call it for you).
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            EnsureWindows();
            WF.Application.EnableVisualStyles();
            WF.Application.SetCompatibleTextRenderingDefault(false);
            // Match Avalonia's DPI model so the hosted form scales with the rest of the app.
            WF.Application.SetHighDpiMode(WF.HighDpiMode.PerMonitorV2);
            _initialized = true;
        }

        /// <summary>Show a legacy form modeless. Returns immediately; the form runs on the shared Win32 pump.</summary>
        public static void Show(WF.Form form, MF.Form? owner = null)
        {
            ArgumentNullException.ThrowIfNull(form);
            EnsureWindows();
            Initialize();

            _openModeless.Add(form);
            form.FormClosed += (_, _) =>
            {
                _openModeless.Remove(form);
                form.Dispose();
            };

            var ownerWin = WrapOwner(owner);
            if (ownerWin is not null) form.Show(ownerWin);
            else form.Show();
        }

        /// <summary>Show a legacy form modally. Blocks the UI thread (WinForms' own nested Win32 loop) and returns the result.</summary>
        public static WF.DialogResult ShowDialog(WF.Form form, MF.Form? owner = null)
        {
            ArgumentNullException.ThrowIfNull(form);
            EnsureWindows();
            Initialize();

            using (form) // a modal dialog is owned by the caller's scope; dispose when done
            {
                var ownerWin = WrapOwner(owner);
                return ownerWin is not null ? form.ShowDialog(ownerWin) : form.ShowDialog();
            }
        }

        // Factory overloads: construct the form on the UI thread, at show time.
        public static void Show(Func<WF.Form> factory, MF.Form? owner = null)
            => Show((factory ?? throw new ArgumentNullException(nameof(factory)))(), owner);

        public static WF.DialogResult ShowDialog(Func<WF.Form> factory, MF.Form? owner = null)
            => ShowDialog((factory ?? throw new ArgumentNullException(nameof(factory)))(), owner);

        // ── Direction B: WinForms app → Majorsilence.Forms form ─────────────────────

        /// <summary>
        /// One-time Majorsilence.Forms (Avalonia) initialization for a WinForms host application.
        /// Call once in Program.Main after WinForms initialization, before the first Majorsilence form
        /// is shown. Safe to call multiple times; runs only once. Direction A callers (Majorsilence app
        /// opening WinForms forms) do not need this — it is called automatically by Show/ShowDialog.
        /// </summary>
        public static void InitializeMajorsilence()
        {
            EnsureWindows();
            MF.Backends.Platform.Backend.Initialize();
        }

        /// <summary>
        /// Show a Majorsilence.Forms window modeless from a WinForms application. Returns immediately;
        /// the existing WinForms Win32 message pump services both toolkits transparently on Windows.
        /// The <paramref name="owner"/> parameter is accepted for API symmetry but Win32-level parenting
        /// is not currently enforced by the Majorsilence Avalonia backend.
        /// </summary>
        public static void ShowMajorsilenceForm(MF.Form form, WF.Form? owner = null)
        {
            ArgumentNullException.ThrowIfNull(form);
            EnsureWindows();
            MF.Backends.Platform.Backend.Initialize();
            form.Show();
        }

        /// <summary>Construct a Majorsilence.Forms window on the UI thread and show it modeless.</summary>
        public static void ShowMajorsilenceForm(Func<MF.Form> factory, WF.Form? owner = null)
            => ShowMajorsilenceForm((factory ?? throw new ArgumentNullException(nameof(factory)))(), owner);

        /// <summary>
        /// Show a Majorsilence.Forms window modally from a WinForms application. Blocks the UI thread
        /// via a nested Avalonia/Win32 dispatcher frame until the window closes, then returns its
        /// <see cref="MF.DialogResult"/>. A result of <see cref="MF.DialogResult.None"/> means the
        /// window was closed without an explicit result (e.g. the user clicked the title-bar close button).
        /// The <paramref name="owner"/> parameter is accepted for API symmetry but Win32-level parenting
        /// is not currently enforced by the Majorsilence Avalonia backend.
        /// </summary>
        public static MF.DialogResult ShowMajorsilenceDialog(MF.Form form, WF.Form? owner = null)
        {
            ArgumentNullException.ThrowIfNull(form);
            EnsureWindows();
            MF.Backends.Platform.Backend.Initialize();

            var tcs = new TaskCompletionSource<MF.DialogResult>();
            form.Closed += (_, _) => tcs.TrySetResult(form.DialogResult);
            form.Show();
            MF.Backends.Platform.Backend.RunModalLoop(tcs.Task);
            return tcs.Task.GetAwaiter().GetResult();
        }

        /// <summary>Construct a Majorsilence.Forms window on the UI thread and show it modally.</summary>
        public static MF.DialogResult ShowMajorsilenceDialog(Func<MF.Form> factory, WF.Form? owner = null)
            => ShowMajorsilenceDialog((factory ?? throw new ArgumentNullException(nameof(factory)))(), owner);

        private static Win32Window? WrapOwner(MF.Form? owner)
        {
            var resolver = OwnerHandleResolver;
            if (owner is null || resolver is null) return null;
            var h = resolver(owner);
            return h == IntPtr.Zero ? null : new Win32Window(h);
        }

        private static void EnsureWindows()
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "System.Windows.Forms interop is only available on Windows. On other platforms, open the " +
                    "migrated Majorsilence.Forms equivalent instead.");
        }

        private sealed class Win32Window : WF.IWin32Window
        {
            public Win32Window(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }
    }
}
